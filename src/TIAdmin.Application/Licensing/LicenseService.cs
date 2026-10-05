namespace TIAdmin.Application.Licensing;

using TIAdmin.Application.Common;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Licencias de software, sus instalaciones y alertas (SPECS.md seccion 23).
/// La clave de licencia se cifra con <see cref="ISecretProtector"/> y solo se revela via
/// <see cref="RevealKeyAsync"/>, que deja registro en la auditoria.
/// </summary>
public interface ILicenseService
{
    Task<PagedResult<LicenseDto>> SearchAsync(PagedQuery query, LicenseFilter filter, CancellationToken cancellationToken = default);

    Task<LicenseDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<LicenseDto> CreateAsync(CreateLicenseRequest request, CancellationToken cancellationToken = default);

    Task<LicenseDto> UpdateAsync(int id, UpdateLicenseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Baja logica. No se permite con instalaciones activas.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<LicenseKeyDto> RevealKeyAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResult<InstallationDto>> GetInstallationsAsync(int licenseId, bool activeOnly, PagedQuery query, CancellationToken cancellationToken = default);

    Task<InstallationDto> InstallAsync(int licenseId, InstallLicenseRequest request, CancellationToken cancellationToken = default);

    Task UninstallAsync(int licenseId, int installationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LicenseAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default);
}

public sealed class LicenseService(
    IUnitOfWork unitOfWork,
    IUserDirectory userDirectory,
    ISecretProtector secretProtector,
    IAuditLogger auditLogger,
    ISystemSettings settings,
    IClock clock)
    : ILicenseService
{
    public const string AlertDaysKey = "Alerts.License.Days";
    public const string LowUtilizationKey = "Alerts.License.LowUtilizationPercent";
    private static readonly int[] DefaultAlertDays = [90, 30, 14, 7];
    private const int DefaultLowUtilizationPercent = 20;

    private DateOnly Today => DateOnly.FromDateTime(clock.Today);

    public Task<PagedResult<LicenseDto>> SearchAsync(PagedQuery query, LicenseFilter filter, CancellationToken cancellationToken = default) =>
        unitOfWork.Licenses.SearchAsync(query, filter, cancellationToken);

    public async Task<LicenseDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await unitOfWork.Licenses.GetDtoAsync(id, cancellationToken) ?? throw new EntityNotFoundException("License", id);

    public async Task<LicenseDto> CreateAsync(CreateLicenseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await ValidateReferencesAsync(request, current: null, cancellationToken);

        var license = new SoftwareLicense();
        Apply(license, request);
        license.LicenseKey = Protect(request.LicenseKey);

        await unitOfWork.Licenses.AddAsync(license, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(license.Id, cancellationToken);
    }

    public async Task<LicenseDto> UpdateAsync(int id, UpdateLicenseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var license = await FindAsync(id, cancellationToken);
        await ValidateReferencesAsync(request, license, cancellationToken);

        Apply(license, request);
        license.IsActive = request.IsActive;
        if (request.LicenseKey is not null)
        {
            // null conserva la clave; "" la elimina; cualquier otro valor la reemplaza.
            license.LicenseKey = Protect(request.LicenseKey);
        }

        unitOfWork.Licenses.Update(license);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var license = await FindAsync(id, cancellationToken);
        license.EnsureCanBeDeleted();

        unitOfWork.Licenses.Delete(license);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<LicenseKeyDto> RevealKeyAsync(int id, CancellationToken cancellationToken = default)
    {
        var license = await FindAsync(id, cancellationToken);
        if (license.LicenseKey is null)
        {
            return new LicenseKeyDto(id, null);
        }

        var key = secretProtector.Unprotect(license.LicenseKey);
        await auditLogger.LogAsync(AuditAction.SensitiveRead, "Licenses", nameof(SoftwareLicense),
            id.ToString(System.Globalization.CultureInfo.InvariantCulture), "Consulta de clave de licencia", cancellationToken);

        return new LicenseKeyDto(id, key);
    }

    public async Task<PagedResult<InstallationDto>> GetInstallationsAsync(
        int licenseId,
        bool activeOnly,
        PagedQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await unitOfWork.Licenses.ExistsAsync(licenseId, cancellationToken))
        {
            throw new EntityNotFoundException("License", licenseId);
        }

        return await unitOfWork.Installations.GetByLicenseAsync(licenseId, activeOnly, query, cancellationToken);
    }

    public async Task<InstallationDto> InstallAsync(int licenseId, InstallLicenseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var license = await FindAsync(licenseId, cancellationToken);

        if (!await unitOfWork.Assets.ExistsAsync(request.AssetId, cancellationToken))
        {
            throw new DomainValidationException("ASSET_NOT_FOUND", "El activo indicado no existe.");
        }

        if (request.UserId is { } userId && await userDirectory.FindAsync(userId, cancellationToken) is not { IsActive: true })
        {
            throw new DomainValidationException("USER_NOT_AVAILABLE", "El usuario indicado no existe o esta inactivo.");
        }

        if (await unitOfWork.Installations.ExistsActiveAsync(licenseId, request.AssetId, cancellationToken))
        {
            throw new ConflictException("ALREADY_INSTALLED", "La licencia ya esta instalada en ese activo.");
        }

        // Concurrencia: SoftwareLicense tiene rowversion; dos instalaciones simultaneas sobre el
        // ultimo puesto no pueden ambas incrementar UsedQuantity (la segunda recibe 409).
        var installation = license.Install(request.AssetId, request.UserId, clock.UtcNow, Today);
        await unitOfWork.Installations.AddAsync(installation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await unitOfWork.Installations.GetDtoAsync(installation.Id, cancellationToken))!;
    }

    public async Task UninstallAsync(int licenseId, int installationId, CancellationToken cancellationToken = default)
    {
        var license = await FindAsync(licenseId, cancellationToken);
        var installation = await unitOfWork.Installations.GetByIdAsync(installationId, cancellationToken);
        if (installation is null || installation.LicenseId != licenseId)
        {
            throw new EntityNotFoundException("Installation", installationId);
        }

        license.Uninstall(installation, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LicenseAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        var windows = await settings.GetIntListAsync(AlertDaysKey, DefaultAlertDays, cancellationToken);
        var lowPercent = await settings.GetIntAsync(LowUtilizationKey, DefaultLowUtilizationPercent, cancellationToken);
        var today = Today;
        var alerts = new List<LicenseAlertDto>();

        foreach (var row in await unitOfWork.Licenses.GetActiveUsageAsync(cancellationToken))
        {
            int? daysRemaining = row.ExpirationDate is { } expiration ? expiration.DayNumber - today.DayNumber : null;
            var expired = daysRemaining < 0;

            if (expired)
            {
                alerts.Add(ToAlert(row, LicenseAlertType.Expired, daysRemaining, null));
            }
            else if (daysRemaining is { } days && AlertWindows.Resolve(days, windows) is { } window)
            {
                alerts.Add(ToAlert(row, LicenseAlertType.ExpiringSoon, daysRemaining, window));
            }

            if (row.Quantity > 0 && row.UsedQuantity >= row.Quantity)
            {
                alerts.Add(ToAlert(row, LicenseAlertType.Exhausted, daysRemaining, null));
            }
            else if (!expired && row.Quantity > 0 && row.UsedQuantity * 100 < row.Quantity * lowPercent)
            {
                alerts.Add(ToAlert(row, LicenseAlertType.LowUtilization, daysRemaining, null));
            }
        }

        return alerts.OrderBy(a => a.AlertType).ThenBy(a => a.DaysRemaining ?? int.MaxValue).ToList();
    }

    private static LicenseAlertDto ToAlert(LicenseUsageRow row, LicenseAlertType type, int? daysRemaining, int? window) =>
        new(row.Id, row.Name, row.SoftwareName, type, row.ExpirationDate, daysRemaining, window, row.Quantity, row.UsedQuantity);

    private async Task<SoftwareLicense> FindAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.Licenses.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("License", id);

    private async Task ValidateReferencesAsync(ILicenseData data, SoftwareLicense? current, CancellationToken cancellationToken)
    {
        if (data.SoftwareId != current?.SoftwareId)
        {
            var software = await unitOfWork.Software.GetByIdAsync(data.SoftwareId, cancellationToken);
            if (software is not { IsActive: true })
            {
                throw new DomainValidationException("SOFTWARE_NOT_AVAILABLE", "El software no existe o esta inactivo.");
            }
        }

        if (data.VendorId is { } vendorId && vendorId != current?.VendorId)
        {
            var vendor = await unitOfWork.Vendors.GetByIdAsync(vendorId, cancellationToken);
            if (vendor is not { Status: VendorStatus.Active })
            {
                throw new DomainValidationException("VENDOR_NOT_AVAILABLE", "El proveedor no existe o no esta activo.");
            }
        }

        if (data.ContractId is { } contractId)
        {
            var contract = await unitOfWork.Contracts.GetByIdAsync(contractId, cancellationToken)
                ?? throw new DomainValidationException("CONTRACT_NOT_FOUND", "El contrato indicado no existe.");

            if (data.VendorId is { } licenseVendor && contract.VendorId != licenseVendor)
            {
                throw new DomainValidationException("CONTRACT_VENDOR_MISMATCH",
                    "El contrato pertenece a otro proveedor distinto al de la licencia.");
            }
        }
    }

    private static void Apply(SoftwareLicense license, ILicenseData data)
    {
        license.SoftwareId = data.SoftwareId;
        license.VendorId = data.VendorId;
        license.ContractId = data.ContractId;
        license.Name = data.Name.Trim();
        license.LicenseType = data.LicenseType;
        license.SetQuantity(data.Quantity);
        license.PurchaseDate = data.PurchaseDate;
        license.ExpirationDate = data.ExpirationDate;
        license.Cost = data.Cost;
        license.SupportEndDate = data.SupportEndDate;
        license.Notes = string.IsNullOrWhiteSpace(data.Notes) ? null : data.Notes.Trim();
    }

    private string? Protect(string? plaintext) =>
        string.IsNullOrWhiteSpace(plaintext) ? null : secretProtector.Protect(plaintext.Trim());
}
