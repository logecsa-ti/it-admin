namespace TIAdmin.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;

public class VendorRepository : BaseRepository<Vendor>, IVendorRepository
{
    public VendorRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(v => v.Name == name && (excludeId == null || v.Id != excludeId), cancellationToken);

    public async Task<PagedResult<VendorDto>> SearchAsync(PagedQuery query, VendorFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var vendors = Set.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            vendors = vendors.Where(v => v.Name.Contains(search)
                || (v.Code != null && v.Code.Contains(search))
                || (v.TaxId != null && v.TaxId.Contains(search))
                || (v.ContactName != null && v.ContactName.Contains(search))
                || (v.Email != null && v.Email.Contains(search)));
        }

        if (filter.Status.HasValue)
        {
            vendors = vendors.Where(v => v.Status == filter.Status.Value);
        }

        var descending = query.SortDirection == SortDirection.Descending;
        vendors = query.SortBy?.ToLowerInvariant() switch
        {
            "code" => descending ? vendors.OrderByDescending(v => v.Code) : vendors.OrderBy(v => v.Code),
            "rating" => descending ? vendors.OrderByDescending(v => v.Rating) : vendors.OrderBy(v => v.Rating),
            "createdat" => descending ? vendors.OrderByDescending(v => v.CreatedAt) : vendors.OrderBy(v => v.CreatedAt),
            _ => descending ? vendors.OrderByDescending(v => v.Name) : vendors.OrderBy(v => v.Name)
        };

        var projected = vendors.Select(v => new VendorDto(
            v.Id, v.Code, v.Name, v.TaxId, v.ContactName, v.Email, v.Phone, v.Address, v.City, v.Country,
            v.Website, v.Status, v.Notes, v.Rating));

        return await ToPagedResultAsync(projected, query, cancellationToken);
    }

    public async Task<bool> HasOpenContractsAsync(int vendorId, DateOnly today, CancellationToken cancellationToken = default) =>
        await Context.Contracts.AnyAsync(c => c.VendorId == vendorId
            && (c.Status == ContractStatus.Draft || (c.Status == ContractStatus.Active && c.EndDate >= today)),
            cancellationToken);
}

public class ContractRepository : BaseRepository<Contract>, IContractRepository
{
    public ContractRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsNumberAsync(string number, CancellationToken cancellationToken = default) =>
        await Set.IgnoreQueryFilters().AnyAsync(c => c.Number == number, cancellationToken);

    public async Task<PagedResult<ContractDto>> SearchAsync(
        PagedQuery query,
        ContractFilter filter,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var contracts = Set.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            contracts = contracts.Where(c => c.Number.Contains(search) || c.Name.Contains(search)
                || Context.Vendors.Any(v => v.Id == c.VendorId && v.Name.Contains(search)));
        }

        if (filter.VendorId.HasValue)
        {
            contracts = contracts.Where(c => c.VendorId == filter.VendorId.Value);
        }

        if (filter.Type.HasValue)
        {
            contracts = contracts.Where(c => c.Type == filter.Type.Value);
        }

        if (filter.EndsBefore.HasValue)
        {
            contracts = contracts.Where(c => c.EndDate <= filter.EndsBefore.Value);
        }

        if (filter.Status.HasValue)
        {
            contracts = FilterByEffectiveStatus(contracts, filter.Status.Value, today);
        }

        var descending = query.SortDirection == SortDirection.Descending;
        contracts = query.SortBy?.ToLowerInvariant() switch
        {
            "number" => descending ? contracts.OrderByDescending(c => c.Number) : contracts.OrderBy(c => c.Number),
            "name" => descending ? contracts.OrderByDescending(c => c.Name) : contracts.OrderBy(c => c.Name),
            "startdate" => descending ? contracts.OrderByDescending(c => c.StartDate) : contracts.OrderBy(c => c.StartDate),
            _ => descending ? contracts.OrderByDescending(c => c.EndDate) : contracts.OrderBy(c => c.EndDate)
        };

        var page = await ToPagedResultAsync(Project(contracts), query, cancellationToken);
        var vendorNames = await VendorNameLookup.GetAsync(Context, page.Items.Select(c => (int?)c.VendorId), cancellationToken);

        return new PagedResult<ContractDto>(
            page.Items.Select(c => Complete(c, today, vendorNames)).ToList(), page.Page, page.PageSize, page.TotalItems);
    }

    public async Task<ContractDto?> GetDtoAsync(int id, DateOnly today, CancellationToken cancellationToken = default)
    {
        var dto = await Project(Set.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync(cancellationToken);
        if (dto is null)
        {
            return null;
        }

        var vendorNames = await VendorNameLookup.GetAsync(Context, [dto.VendorId], cancellationToken);
        return Complete(dto, today, vendorNames);
    }

    public async Task<IReadOnlyList<ContractExpiryRow>> GetActiveEndingByAsync(DateOnly endsOnOrBefore, CancellationToken cancellationToken = default)
    {
        var rows = await Set.AsNoTracking()
            .Where(c => c.Status == ContractStatus.Active && c.EndDate <= endsOnOrBefore)
            .OrderBy(c => c.EndDate)
            .Select(c => new { c.Id, c.Number, c.Name, c.VendorId, c.EndDate, c.AutoRenew, c.ResponsibleUserId })
            .ToListAsync(cancellationToken);

        var vendorNames = await VendorNameLookup.GetAsync(Context, rows.Select(r => (int?)r.VendorId), cancellationToken);
        return rows
            .Select(r => new ContractExpiryRow(r.Id, r.Number, r.Name, vendorNames.GetValueOrDefault(r.VendorId, string.Empty),
                r.EndDate, r.AutoRenew, r.ResponsibleUserId))
            .ToList();
    }

    /// <summary>Traduce el estado efectivo (derivado de las fechas) a condiciones SQL.</summary>
    private static IQueryable<Contract> FilterByEffectiveStatus(IQueryable<Contract> contracts, ContractStatus status, DateOnly today) =>
        status switch
        {
            ContractStatus.Expired => contracts.Where(c => c.Status == ContractStatus.Active && c.EndDate < today),
            ContractStatus.Expiring => contracts.Where(c => c.Status == ContractStatus.Active
                && c.EndDate >= today && c.EndDate <= today.AddDays(c.RenewalNoticeDays)),
            ContractStatus.Active => contracts.Where(c => c.Status == ContractStatus.Active
                && c.EndDate > today.AddDays(c.RenewalNoticeDays)),
            _ => contracts.Where(c => c.Status == status)
        };

    /// <summary>
    /// Proyecta el estado almacenado; <see cref="Complete"/> lo refina en memoria y agrega el
    /// nombre del proveedor. No se navega a Vendor: al ser una relacion requerida con soft delete,
    /// EF filtraria los contratos de proveedores eliminados.
    /// </summary>
    private IQueryable<ContractDto> Project(IQueryable<Contract> contracts) =>
        contracts.Select(c => new ContractDto(
            c.Id,
            c.Number,
            c.Name,
            c.VendorId,
            string.Empty,
            c.Type,
            c.StartDate,
            c.EndDate,
            c.Value,
            c.Currency,
            c.AutoRenew,
            c.RenewalNoticeDays,
            c.ResponsibleUserId,
            Context.Users.Where(u => u.Id == c.ResponsibleUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            c.Status,
            0,
            c.Notes,
            c.RenewedFromContractId));

    private static ContractDto Complete(ContractDto dto, DateOnly today, Dictionary<int, string> vendorNames) => dto with
    {
        VendorName = vendorNames.GetValueOrDefault(dto.VendorId, string.Empty),
        Status = ContractStatusRules.Effective(dto.Status, dto.EndDate, dto.RenewalNoticeDays, today),
        DaysRemaining = dto.EndDate.DayNumber - today.DayNumber
    };
}

public class SoftwareRepository : BaseRepository<Software>, ISoftwareRepository
{
    public SoftwareRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsNameVersionAsync(string name, string? version, int? excludeId = null, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(s => s.Name == name && s.Version == version && (excludeId == null || s.Id != excludeId), cancellationToken);

    public async Task<PagedResult<SoftwareDto>> SearchAsync(PagedQuery query, SoftwareFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var software = Set.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            software = software.Where(s => s.Name.Contains(search)
                || (s.Publisher != null && s.Publisher.Contains(search))
                || (s.Category != null && s.Category.Contains(search)));
        }

        if (filter.IsActive.HasValue)
        {
            software = software.Where(s => s.IsActive == filter.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            software = software.Where(s => s.Category == filter.Category);
        }

        var descending = query.SortDirection == SortDirection.Descending;
        software = query.SortBy?.ToLowerInvariant() switch
        {
            "publisher" => descending ? software.OrderByDescending(s => s.Publisher) : software.OrderBy(s => s.Publisher),
            "category" => descending ? software.OrderByDescending(s => s.Category) : software.OrderBy(s => s.Category),
            _ => descending ? software.OrderByDescending(s => s.Name).ThenByDescending(s => s.Version) : software.OrderBy(s => s.Name).ThenBy(s => s.Version)
        };

        return await ToPagedResultAsync(Project(software), query, cancellationToken);
    }

    public async Task<SoftwareDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default) =>
        await Project(Set.AsNoTracking().Where(s => s.Id == id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> HasLicensesAsync(int softwareId, CancellationToken cancellationToken = default) =>
        await Context.SoftwareLicenses.AnyAsync(l => l.SoftwareId == softwareId, cancellationToken);

    /// <summary>Incluye el resumen de puestos de sus licencias activas.</summary>
    private IQueryable<SoftwareDto> Project(IQueryable<Software> software) =>
        software.Select(s => new SoftwareDto(
            s.Id,
            s.Name,
            s.Version,
            s.Publisher,
            s.Category,
            s.Description,
            s.IsActive,
            Context.SoftwareLicenses.Count(l => l.SoftwareId == s.Id),
            Context.SoftwareLicenses.Where(l => l.SoftwareId == s.Id && l.IsActive).Sum(l => (int?)l.Quantity) ?? 0,
            Context.SoftwareLicenses.Where(l => l.SoftwareId == s.Id && l.IsActive).Sum(l => (int?)l.UsedQuantity) ?? 0));
}

public class SoftwareLicenseRepository : BaseRepository<SoftwareLicense>, ISoftwareLicenseRepository
{
    public SoftwareLicenseRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<LicenseDto>> SearchAsync(PagedQuery query, LicenseFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        var licenses = Set.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            licenses = licenses.Where(l => l.Name.Contains(search) || l.Software!.Name.Contains(search));
        }

        if (filter.SoftwareId.HasValue)
        {
            licenses = licenses.Where(l => l.SoftwareId == filter.SoftwareId.Value);
        }

        if (filter.VendorId.HasValue)
        {
            licenses = licenses.Where(l => l.VendorId == filter.VendorId.Value);
        }

        if (filter.LicenseType.HasValue)
        {
            licenses = licenses.Where(l => l.LicenseType == filter.LicenseType.Value);
        }

        if (filter.IsActive.HasValue)
        {
            licenses = licenses.Where(l => l.IsActive == filter.IsActive.Value);
        }

        if (filter.ExpiresBefore.HasValue)
        {
            licenses = licenses.Where(l => l.ExpirationDate != null && l.ExpirationDate <= filter.ExpiresBefore.Value);
        }

        var descending = query.SortDirection == SortDirection.Descending;
        licenses = query.SortBy?.ToLowerInvariant() switch
        {
            "expirationdate" => descending ? licenses.OrderByDescending(l => l.ExpirationDate) : licenses.OrderBy(l => l.ExpirationDate),
            "quantity" => descending ? licenses.OrderByDescending(l => l.Quantity) : licenses.OrderBy(l => l.Quantity),
            "usedquantity" => descending ? licenses.OrderByDescending(l => l.UsedQuantity) : licenses.OrderBy(l => l.UsedQuantity),
            _ => descending ? licenses.OrderByDescending(l => l.Name) : licenses.OrderBy(l => l.Name)
        };

        var page = await ToPagedResultAsync(Project(licenses), query, cancellationToken);
        var vendorNames = await VendorNameLookup.GetAsync(Context, page.Items.Select(l => l.VendorId), cancellationToken);

        return new PagedResult<LicenseDto>(
            page.Items.Select(l => WithVendorName(l, vendorNames)).ToList(), page.Page, page.PageSize, page.TotalItems);
    }

    public async Task<LicenseDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default)
    {
        var dto = await Project(Set.AsNoTracking().Where(l => l.Id == id)).FirstOrDefaultAsync(cancellationToken);
        if (dto is null)
        {
            return null;
        }

        return WithVendorName(dto, await VendorNameLookup.GetAsync(Context, [dto.VendorId], cancellationToken));
    }

    private static LicenseDto WithVendorName(LicenseDto dto, Dictionary<int, string> vendorNames) =>
        dto with { VendorName = dto.VendorId is { } id ? vendorNames.GetValueOrDefault(id) : null };

    public async Task<IReadOnlyList<LicenseUsageRow>> GetActiveUsageAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(l => l.IsActive)
            .Select(l => new LicenseUsageRow(l.Id, l.Name, l.Software!.Name, l.ExpirationDate, l.Quantity, l.UsedQuantity))
            .ToListAsync(cancellationToken);

    /// <summary>Nunca proyecta la clave, solo si existe.</summary>
    private static IQueryable<LicenseDto> Project(IQueryable<SoftwareLicense> licenses) =>
        licenses.Select(l => new LicenseDto(
            l.Id,
            l.SoftwareId,
            l.Software!.Name,
            l.VendorId,
            null,
            l.ContractId,
            l.Contract != null ? l.Contract.Number : null,
            l.Name,
            l.LicenseType,
            l.LicenseKey != null,
            l.Quantity,
            l.UsedQuantity,
            l.Quantity - l.UsedQuantity,
            l.PurchaseDate,
            l.ExpirationDate,
            l.Cost,
            l.SupportEndDate,
            l.Notes,
            l.IsActive));
}

public class SoftwareInstallationRepository : BaseRepository<SoftwareInstallation>, ISoftwareInstallationRepository
{
    public SoftwareInstallationRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsActiveAsync(int licenseId, int assetId, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(i => i.LicenseId == licenseId && i.AssetId == assetId && i.IsActive, cancellationToken);

    public async Task<PagedResult<InstallationDto>> GetByLicenseAsync(
        int licenseId,
        bool activeOnly,
        PagedQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var installations = Set.AsNoTracking().Where(i => i.LicenseId == licenseId && (!activeOnly || i.IsActive))
            .OrderByDescending(i => i.InstalledAt)
            .ThenByDescending(i => i.Id);

        return await ToPagedResultAsync(Project(installations), query, cancellationToken);
    }

    public async Task<InstallationDto?> GetDtoAsync(int installationId, CancellationToken cancellationToken = default) =>
        await Project(Set.AsNoTracking().Where(i => i.Id == installationId)).FirstOrDefaultAsync(cancellationToken);

    /// <summary>IgnoreQueryFilters sobre Assets: el historial sigue mostrando activos dados de baja.</summary>
    private IQueryable<InstallationDto> Project(IQueryable<SoftwareInstallation> installations) =>
        installations.Select(i => new InstallationDto(
            i.Id,
            i.LicenseId,
            i.AssetId,
            Context.Assets.IgnoreQueryFilters().Where(a => a.Id == i.AssetId).Select(a => a.AssetCode).First(),
            Context.Assets.IgnoreQueryFilters().Where(a => a.Id == i.AssetId).Select(a => a.Name).First(),
            i.UserId,
            Context.Users.Where(u => u.Id == i.UserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            i.InstalledAt,
            i.UninstalledAt,
            i.IsActive));
}

/// <summary>
/// Nombres de proveedor incluyendo eliminados, en una consulta aparte: IgnoreQueryFilters dentro
/// de una proyeccion desactivaria los filtros de toda la consulta (p. ej. mostraria activos eliminados).
/// </summary>
internal static class VendorNameLookup
{
    public static async Task<Dictionary<int, string>> GetAsync(
        TIAdminDbContext context,
        IEnumerable<int?> vendorIds,
        CancellationToken cancellationToken)
    {
        var ids = vendorIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await context.Vendors
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Name, cancellationToken);
    }
}
