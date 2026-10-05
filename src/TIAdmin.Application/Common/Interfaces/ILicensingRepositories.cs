namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;

public interface IVendorRepository : IRepository<Vendor>
{
    /// <summary>Solo entre proveedores no eliminados (el indice unico de Name excluye los eliminados).</summary>
    Task<bool> ExistsNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<PagedResult<VendorDto>> SearchAsync(PagedQuery query, VendorFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Contratos en Draft o Active que aun no vencen.</summary>
    Task<bool> HasOpenContractsAsync(int vendorId, DateOnly today, CancellationToken cancellationToken = default);
}

public interface ISoftwareRepository : IRepository<Software>
{
    /// <summary>Nombre + version unicos entre productos no eliminados.</summary>
    Task<bool> ExistsNameVersionAsync(string name, string? version, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<PagedResult<SoftwareDto>> SearchAsync(PagedQuery query, SoftwareFilter filter, CancellationToken cancellationToken = default);

    Task<SoftwareDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> HasLicensesAsync(int softwareId, CancellationToken cancellationToken = default);
}

public interface ISoftwareLicenseRepository : IRepository<SoftwareLicense>
{
    Task<PagedResult<LicenseDto>> SearchAsync(PagedQuery query, LicenseFilter filter, CancellationToken cancellationToken = default);

    Task<LicenseDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Licencias activas con sus datos de uso y vencimiento, para calcular alertas.</summary>
    Task<IReadOnlyList<LicenseUsageRow>> GetActiveUsageAsync(CancellationToken cancellationToken = default);
}

public interface ISoftwareInstallationRepository : IRepository<SoftwareInstallation>
{
    Task<bool> ExistsActiveAsync(int licenseId, int assetId, CancellationToken cancellationToken = default);

    Task<PagedResult<InstallationDto>> GetByLicenseAsync(int licenseId, bool activeOnly, PagedQuery query, CancellationToken cancellationToken = default);

    Task<InstallationDto?> GetDtoAsync(int installationId, CancellationToken cancellationToken = default);
}

public interface IContractRepository : IRepository<Contract>
{
    /// <summary>Incluye eliminados: el numero de contrato queda reservado.</summary>
    Task<bool> ExistsNumberAsync(string number, CancellationToken cancellationToken = default);

    Task<PagedResult<ContractDto>> SearchAsync(PagedQuery query, ContractFilter filter, DateOnly today, CancellationToken cancellationToken = default);

    Task<ContractDto?> GetDtoAsync(int id, DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>Contratos en estado Active cuya fecha final es anterior o igual a <paramref name="endsOnOrBefore"/>.</summary>
    Task<IReadOnlyList<ContractExpiryRow>> GetActiveEndingByAsync(DateOnly endsOnOrBefore, CancellationToken cancellationToken = default);
}

public record LicenseUsageRow(int Id, string Name, string SoftwareName, DateOnly? ExpirationDate, int Quantity, int UsedQuantity);

public record ContractExpiryRow(
    int Id,
    string Number,
    string Name,
    string VendorName,
    DateOnly EndDate,
    bool AutoRenew,
    int? ResponsibleUserId);

/// <summary>Lectura tipada de SystemConfigurations (los valores por defecto viven en el seed).</summary>
public interface ISystemSettings
{
    Task<int> GetIntAsync(string key, int fallback, CancellationToken cancellationToken = default);

    Task<string> GetStringAsync(string key, string fallback, CancellationToken cancellationToken = default);

    /// <summary>Lista de enteros separados por coma (p. ej. "90,60,30"); valores invalidos se ignoran.</summary>
    Task<IReadOnlyList<int>> GetIntListAsync(string key, IReadOnlyList<int> fallback, CancellationToken cancellationToken = default);
}

/// <summary>Cifrado de secretos de negocio (p. ej. claves de licencia). Implementacion intercambiable (Q-09).</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}

/// <summary>
/// Registro explicito en AuditLog para acciones que no son escrituras de entidades
/// (p. ej. revelar una clave). Las escrituras ya las audita AuditTrailInterceptor.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(AuditAction action, string module, string entityName, string entityId, string? details, CancellationToken cancellationToken = default);
}
