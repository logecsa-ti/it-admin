namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;

/// <summary>
/// Consultas de solo lectura para dashboard y reportes (agregados calculados en la base de datos).
/// Las fechas de periodo son instantes UTC; los limites "hoy" son fechas de negocio.
/// </summary>
public interface IReportingQueries
{
    Task<MyKpis> GetMyKpisAsync(int userId, CancellationToken cancellationToken = default);

    Task<AssetKpis> GetAssetKpisAsync(CancellationToken cancellationToken = default);

    Task<TicketKpis> GetTicketKpisAsync(DateTime now, CancellationToken cancellationToken = default);

    Task<LicenseKpis> GetLicenseKpisAsync(DateOnly today, DateOnly expiringUntil, CancellationToken cancellationToken = default);

    Task<ContractKpis> GetContractKpisAsync(DateOnly today, DateOnly expiringUntil, CancellationToken cancellationToken = default);

    Task<MaintenanceKpis> GetMaintenanceKpisAsync(DateTime now, CancellationToken cancellationToken = default);

    Task<CostReport> GetCostReportAsync(DateTime from, DateTime to, TimeZoneInfo timeZone, CancellationToken cancellationToken = default);

    Task<AssetSummaryReport> GetAssetSummaryAsync(DateOnly today, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetsByUserRow>> GetAssetsByUserAsync(int? departmentId, CancellationToken cancellationToken = default);

    Task<TicketReport> GetTicketReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<SlaReport> GetSlaReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LicenseUsageReportRow>> GetLicenseUsageAsync(DateOnly today, CancellationToken cancellationToken = default);
}

public interface IAuditLogRepository
{
    Task<PagedResult<AuditLogDto>> SearchAsync(PagedQuery query, AuditFilter filter, CancellationToken cancellationToken = default);

    Task<AuditLogDto?> GetAsync(long id, CancellationToken cancellationToken = default);
}

public interface ISystemConfigurationRepository
{
    Task<IReadOnlyList<SystemConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Con seguimiento de cambios.</summary>
    Task<SystemConfiguration?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
}
