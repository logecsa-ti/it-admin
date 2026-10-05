namespace TIAdmin.Application.Reporting;

using TIAdmin.Application.Common;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Dashboard (SPECS.md seccion 28). Cada seccion se incluye solo si el usuario tiene el permiso de
/// su modulo (ADR-032): DASHBOARD.VIEW no debe revelar lo que los permisos de modulo ocultan.
/// </summary>
public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public sealed class DashboardService(
    IReportingQueries queries,
    ICurrentUserService currentUser,
    ISystemSettings settings,
    IClock clock)
    : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Operacion sin usuario autenticado.");
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(clock.Today);

        var mine = await queries.GetMyKpisAsync(userId, cancellationToken);

        AssetKpis? assets = Has(Permissions.AssetsView) ? await queries.GetAssetKpisAsync(cancellationToken) : null;

        TicketKpis? tickets = Has(Permissions.TicketsView) ? await queries.GetTicketKpisAsync(now, cancellationToken) : null;

        LicenseKpis? licenses = null;
        if (Has(Permissions.LicensesView))
        {
            var windows = await settings.GetIntListAsync("Alerts.License.Days", [90, 30, 14, 7], cancellationToken);
            licenses = await queries.GetLicenseKpisAsync(today, today.AddDays(AlertWindows.Max(windows)), cancellationToken);
        }

        ContractKpis? contracts = null;
        if (Has(Permissions.ContractsView))
        {
            var windows = await settings.GetIntListAsync("Alerts.Contract.Days", [90, 60, 30, 15, 7], cancellationToken);
            contracts = await queries.GetContractKpisAsync(today, today.AddDays(AlertWindows.Max(windows)), cancellationToken);
        }

        MaintenanceKpis? maintenance = Has(Permissions.MaintenanceView)
            ? await queries.GetMaintenanceKpisAsync(now, cancellationToken)
            : null;

        CostKpis? costs = null;
        if (Has(Permissions.ReportsView))
        {
            var (from, to) = YearRange(clock);
            var report = await queries.GetCostReportAsync(from, to, clock.TimeZone, cancellationToken);
            costs = new CostKpis(TimeZoneInfo.ConvertTimeFromUtc(now, clock.TimeZone).Year, report.Purchases, report.Maintenance,
                report.Licenses, report.Total);
        }

        return new DashboardSummaryDto(now, mine, assets, tickets, licenses, contracts, maintenance, costs);
    }

    private bool Has(string permission) => currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    /// <summary>1 de enero a 1 de enero siguiente, en la zona de la organizacion, expresado en UTC.</summary>
    internal static (DateTime From, DateTime To) YearRange(IClock clock)
    {
        var year = clock.Today.Year;
        return (
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), clock.TimeZone),
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), clock.TimeZone));
    }
}

/// <summary>
/// Reportes (SPECS.md seccion 48) en JSON; la exportacion a Excel/CSV/PDF llega en la Fase 10.
/// Los periodos se indican con fechas de negocio [from, to] inclusivas en la zona de la organizacion.
/// </summary>
public interface IReportService
{
    Task<AssetSummaryReport> GetAssetSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetsByUserRow>> GetAssetsByUserAsync(int? departmentId, CancellationToken cancellationToken = default);

    Task<TicketReport> GetTicketReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    Task<SlaReport> GetSlaReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LicenseUsageReportRow>> GetLicenseUsageAsync(CancellationToken cancellationToken = default);

    Task<CostReport> GetCostReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}

public sealed class ReportService(IReportingQueries queries, IClock clock) : IReportService
{
    private const int MaxRangeDays = 731;

    public Task<AssetSummaryReport> GetAssetSummaryAsync(CancellationToken cancellationToken = default) =>
        queries.GetAssetSummaryAsync(DateOnly.FromDateTime(clock.Today), cancellationToken);

    public Task<IReadOnlyList<AssetsByUserRow>> GetAssetsByUserAsync(int? departmentId, CancellationToken cancellationToken = default) =>
        queries.GetAssetsByUserAsync(departmentId, cancellationToken);

    public Task<TicketReport> GetTicketReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var (start, end) = Range(from, to, defaultDays: 30);
        return queries.GetTicketReportAsync(start, end, cancellationToken);
    }

    public Task<SlaReport> GetSlaReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var (start, end) = Range(from, to, defaultDays: 30);
        return queries.GetSlaReportAsync(start, end, cancellationToken);
    }

    public Task<IReadOnlyList<LicenseUsageReportRow>> GetLicenseUsageAsync(CancellationToken cancellationToken = default) =>
        queries.GetLicenseUsageAsync(DateOnly.FromDateTime(clock.Today), cancellationToken);

    public Task<CostReport> GetCostReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var (start, end) = from is null && to is null ? DashboardService.YearRange(clock) : Range(from, to, defaultDays: 365);
        return queries.GetCostReportAsync(start, end, clock.TimeZone, cancellationToken);
    }

    /// <summary>[from 00:00, to+1 00:00) en la zona de la organizacion, convertido a UTC.</summary>
    private (DateTime From, DateTime To) Range(DateOnly? from, DateOnly? to, int defaultDays)
    {
        var end = to ?? DateOnly.FromDateTime(clock.Today);
        var start = from ?? end.AddDays(-(defaultDays - 1));

        if (start > end)
        {
            throw new DomainValidationException("INVALID_DATE_RANGE", "La fecha inicial no puede ser posterior a la final.");
        }

        if (end.DayNumber - start.DayNumber > MaxRangeDays)
        {
            throw new DomainValidationException("DATE_RANGE_TOO_LARGE", $"El periodo no puede exceder {MaxRangeDays} dias.");
        }

        return (ToUtc(start), ToUtc(end.AddDays(1)));
    }

    private DateTime ToUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), clock.TimeZone);
}

/// <summary>Consulta de la bitacora de auditoria. No hay escritura: la bitacora es inmutable via API.</summary>
public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> SearchAsync(PagedQuery query, AuditFilter filter, CancellationToken cancellationToken = default);

    Task<AuditLogDto> GetAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class AuditQueryService(IUnitOfWork unitOfWork) : IAuditQueryService
{
    public Task<PagedResult<AuditLogDto>> SearchAsync(PagedQuery query, AuditFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.From > filter.To)
        {
            throw new DomainValidationException("INVALID_DATE_RANGE", "La fecha inicial no puede ser posterior a la final.");
        }

        return unitOfWork.AuditLogs.SearchAsync(query, filter, cancellationToken);
    }

    public async Task<AuditLogDto> GetAsync(long id, CancellationToken cancellationToken = default) =>
        await unitOfWork.AuditLogs.GetAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException($"AuditLog {id}");
}
