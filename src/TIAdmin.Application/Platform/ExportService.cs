namespace TIAdmin.Application.Platform;

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Reporting;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Exportacion de reportes a CSV/Excel (SPECS.md seccion 48, ADR-036). Hasta Exports.AsyncThreshold filas
/// se devuelve el archivo; por encima se encola un trabajo que lo genera en segundo plano y notifica al
/// solicitante con el enlace de descarga. PDF queda pendiente de Q-03.
/// </summary>
public interface IExportService
{
    IReadOnlyCollection<string> Reports { get; }

    Task<ExportResult> ExportAsync(string report, string format, IReadOnlyDictionary<string, string?> parameters, CancellationToken cancellationToken = default);

    Task<ExportJobDto> GetJobAsync(int id, CancellationToken cancellationToken = default);

    Task<DocumentContent> DownloadJobAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lo ejecuta el worker de exportaciones.</summary>
    Task ProcessJobAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class ExportService(
    IUnitOfWork unitOfWork,
    IReportService reports,
    ITabularFileWriter writer,
    IFileStorage storage,
    IExportQueue queue,
    INotificationService notifications,
    ICurrentUserService currentUser,
    ISystemSettings settings,
    IClock clock,
    ILogger<ExportService> logger)
    : IExportService
{
    public const string AsyncThresholdKey = "Exports.AsyncThreshold";
    private const int PageSize = 200;

    private sealed record ReportDefinition(
        string Permission,
        Func<ExportService, IReadOnlyDictionary<string, string?>, CancellationToken, Task<int?>> Count,
        Func<ExportService, IReadOnlyDictionary<string, string?>, CancellationToken, Task<TabularData>> Build);

    /// <summary>Reportes exportables y el permiso de modulo que exige cada uno (ademas de REPORTS.EXPORT).</summary>
    private static readonly Dictionary<string, ReportDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["assets"] = new(Permissions.AssetsView, (s, p, ct) => s.CountAssetsAsync(p, ct), (s, p, ct) => s.BuildAssetsAsync(p, ct)),
        ["tickets"] = new(Permissions.TicketsView, (s, p, ct) => s.CountTicketsAsync(p, ct), (s, p, ct) => s.BuildTicketsAsync(p, ct)),
        ["audit"] = new(Permissions.AuditView, (s, p, ct) => s.CountAuditAsync(p, ct), (s, p, ct) => s.BuildAuditAsync(p, ct)),
        ["assets-by-user"] = new(Permissions.AssetsView, (_, _, _) => Task.FromResult<int?>(null), (s, p, ct) => s.BuildAssetsByUserAsync(p, ct)),
        ["licenses"] = new(Permissions.LicensesView, (_, _, _) => Task.FromResult<int?>(null), (s, _, ct) => s.BuildLicensesAsync(ct)),
        ["costs"] = new(Permissions.ReportsView, (_, _, _) => Task.FromResult<int?>(null), (s, p, ct) => s.BuildCostsAsync(p, ct)),
        ["sla"] = new(Permissions.TicketsView, (_, _, _) => Task.FromResult<int?>(null), (s, p, ct) => s.BuildSlaAsync(p, ct))
    };

    public IReadOnlyCollection<string> Reports => Definitions.Keys;

    public async Task<ExportResult> ExportAsync(
        string report,
        string format,
        IReadOnlyDictionary<string, string?> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var definition = Resolve(report, out var reportName);
        format = NormalizeFormat(format);

        if (!currentUser.Permissions.Contains(definition.Permission, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Exportar {reportName} requiere el permiso {definition.Permission}.");
        }

        var threshold = await settings.GetIntAsync(AsyncThresholdKey, 5000, cancellationToken);
        var rows = await definition.Count(this, parameters, cancellationToken);

        if (rows > threshold)
        {
            var job = new ExportJob
            {
                RequestedById = currentUser.UserId ?? throw new UnauthorizedAccessException("Operacion sin usuario autenticado."),
                ReportName = reportName,
                Format = format,
                Parameters = JsonSerializer.Serialize(parameters),
                CreatedAt = clock.UtcNow
            };
            await unitOfWork.ExportJobs.AddAsync(job, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            queue.Enqueue(job.Id);
            return new ExportResult(null, ToDto(job));
        }

        var data = await definition.Build(this, parameters, cancellationToken);
        var output = new MemoryStream();
        await writer.WriteAsync(data, format, output, cancellationToken);
        output.Position = 0;
        return new ExportResult(new DocumentContent(FileName(reportName, format), ContentType(format), output), null);
    }

    public async Task<ExportJobDto> GetJobAsync(int id, CancellationToken cancellationToken = default) =>
        ToDto(await FindOwnJobAsync(id, cancellationToken));

    public async Task<DocumentContent> DownloadJobAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await FindOwnJobAsync(id, cancellationToken);
        if (job.Status != ExportJobStatus.Completed || job.StoragePath is null)
        {
            throw new ConflictException("EXPORT_NOT_READY", $"La exportacion esta en estado {job.Status}.");
        }

        var stream = await storage.OpenReadAsync(job.StoragePath, cancellationToken)
            ?? throw new EntityNotFoundException($"archivo de la exportacion {id}");
        return new DocumentContent(job.FileName!, ContentType(job.Format), stream);
    }

    public async Task ProcessJobAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await unitOfWork.ExportJobs.GetAsync(id, cancellationToken);
        if (job is not { Status: ExportJobStatus.Pending })
        {
            return;
        }

        job.Start();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var definition = Resolve(job.ReportName, out _);
            var parameters = JsonSerializer.Deserialize<Dictionary<string, string?>>(job.Parameters ?? "{}") ?? [];
            var data = await definition.Build(this, parameters, cancellationToken);

            await using var output = new MemoryStream();
            await writer.WriteAsync(data, job.Format, output, cancellationToken);
            output.Position = 0;

            var now = clock.UtcNow;
            var path = $"exports/{now:yyyy}/{now:MM}/{job.Id}-{Guid.NewGuid():N}.{job.Format}";
            await storage.SaveAsync(output, path, cancellationToken);
            job.Complete(FileName(job.ReportName, job.Format), path, data.Rows.Count, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await notifications.NotifyAsync(new NotificationRequest([job.RequestedById], "ExportReady",
                $"Exportacion lista: {job.ReportName}", $"{data.Rows.Count} filas en formato {job.Format}.",
                "ExportJob", job.Id, $"/api/v1/reports/exports/{job.Id}/download", SendEmail: false), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Fallo la exportacion {JobId} ({Report}).", job.Id, job.ReportName);
            job.Fail(exception.Message, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static ReportDefinition Resolve(string report, out string reportName)
    {
        if (!Definitions.TryGetValue(report ?? string.Empty, out var definition))
        {
            throw new DomainValidationException("REPORT_NOT_FOUND",
                $"Reporte desconocido: {report}. Disponibles: {string.Join(", ", Definitions.Keys)}.");
        }

        reportName = Definitions.Keys.First(k => string.Equals(k, report, StringComparison.OrdinalIgnoreCase));
        return definition;
    }

    private async Task<ExportJob> FindOwnJobAsync(int id, CancellationToken cancellationToken)
    {
        var job = await unitOfWork.ExportJobs.GetAsync(id, cancellationToken);

        // Solo quien la solicito: otros usuarios no deben saber que existe.
        if (job is null || job.RequestedById != currentUser.UserId)
        {
            throw new EntityNotFoundException("ExportJob", id);
        }

        return job;
    }

    // ---------- Definiciones de reportes ----------

    private async Task<int?> CountAssetsAsync(IReadOnlyDictionary<string, string?> p, CancellationToken ct) =>
        (int)(await unitOfWork.Assets.SearchAsync(new PagedQuery { PageSize = 1 }, AssetFilterFrom(p), ct)).TotalItems;

    private async Task<TabularData> BuildAssetsAsync(IReadOnlyDictionary<string, string?> p, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<object?>>();
        var filter = AssetFilterFrom(p);
        for (var page = 1; ; page++)
        {
            var result = await unitOfWork.Assets.SearchAsync(new PagedQuery { Page = page, PageSize = PageSize }, filter, ct);
            rows.AddRange(result.Items.Select(a => (IReadOnlyList<object?>)
                [a.AssetCode, a.SerialNumber, a.Name, a.AssetTypeName, a.Brand, a.Model, a.Status.ToString(), a.CurrentUserName,
                    a.DepartmentName, a.LocationName, a.WarrantyExpiration]));
            if (!result.HasNext)
            {
                break;
            }
        }

        return new TabularData("Inventario de activos",
            ["Codigo", "Serie", "Nombre", "Tipo", "Marca", "Modelo", "Estado", "Asignado a", "Departamento", "Ubicacion", "Garantia hasta"], rows);
    }

    private async Task<int?> CountTicketsAsync(IReadOnlyDictionary<string, string?> p, CancellationToken ct) =>
        (int)(await unitOfWork.Tickets.SearchAsync(new PagedQuery { PageSize = 1 }, TicketFilterFrom(p), AllTickets, clock.UtcNow, ct)).TotalItems;

    private async Task<TabularData> BuildTicketsAsync(IReadOnlyDictionary<string, string?> p, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<object?>>();
        var filter = TicketFilterFrom(p);
        var now = clock.UtcNow;
        for (var page = 1; ; page++)
        {
            var result = await unitOfWork.Tickets.SearchAsync(new PagedQuery { Page = page, PageSize = PageSize }, filter, AllTickets, now, ct);
            rows.AddRange(result.Items.Select(t => (IReadOnlyList<object?>)
                [t.TicketNumber, t.Type.ToString(), t.Title, t.CategoryName, t.Priority.ToString(), t.Status.ToString(), t.RequesterName,
                    t.AssignedToName, t.CreatedAt, t.Sla.DueAtResolution, t.Sla.IsSlaBreached]));
            if (!result.HasNext)
            {
                break;
            }
        }

        return new TabularData("Tickets",
            ["Numero", "Tipo", "Titulo", "Categoria", "Prioridad", "Estado", "Solicitante", "Asignado a", "Creado (UTC)", "Vence (UTC)", "SLA vencido"], rows);
    }

    private async Task<int?> CountAuditAsync(IReadOnlyDictionary<string, string?> p, CancellationToken ct) =>
        (int)(await unitOfWork.AuditLogs.SearchAsync(new PagedQuery { PageSize = 1 }, AuditFilterFrom(p), ct)).TotalItems;

    private async Task<TabularData> BuildAuditAsync(IReadOnlyDictionary<string, string?> p, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<object?>>();
        var filter = AuditFilterFrom(p);
        for (var page = 1; ; page++)
        {
            var result = await unitOfWork.AuditLogs.SearchAsync(new PagedQuery { Page = page, PageSize = PageSize }, filter, ct);
            rows.AddRange(result.Items.Select(a => (IReadOnlyList<object?>)
                [a.Timestamp, a.UserName, a.Action.ToString(), a.Module, a.EntityName, a.EntityId, a.IpAddress, a.CorrelationId]));
            if (!result.HasNext)
            {
                break;
            }
        }

        return new TabularData("Auditoria", ["Fecha (UTC)", "Usuario", "Accion", "Modulo", "Entidad", "Id", "IP", "CorrelationId"], rows);
    }

    private async Task<TabularData> BuildAssetsByUserAsync(IReadOnlyDictionary<string, string?> p, CancellationToken ct)
    {
        var rows = await reports.GetAssetsByUserAsync(Int(p, "departmentId"), ct);
        return new TabularData("Activos por usuario", ["Usuario", "Correo", "Cantidad", "Activos"],
            rows.Select(r => (IReadOnlyList<object?>)[r.UserName, r.Email, r.AssetCount, string.Join(", ", r.AssetCodes)]).ToList());
    }

    private async Task<TabularData> BuildLicensesAsync(CancellationToken ct)
    {
        var rows = await reports.GetLicenseUsageAsync(ct);
        return new TabularData("Licencias", ["Software", "Licencias", "Puestos", "En uso", "Uso %", "Vencidas", "Proximo vencimiento"],
            rows.Select(r => (IReadOnlyList<object?>)[r.Software, r.Licenses, r.Seats, r.UsedSeats, r.UtilizationPercent, r.Expired, r.NextExpiration]).ToList());
    }

    private async Task<TabularData> BuildCostsAsync(IReadOnlyDictionary<string, string?> p, CancellationToken ct)
    {
        var report = await reports.GetCostReportAsync(Date(p, "from"), Date(p, "to"), ct);
        return new TabularData("Costos de TI", ["Anio", "Mes", "Compras", "Mantenimiento", "Licencias", "Total"],
            report.ByMonth.Select(m => (IReadOnlyList<object?>)[m.Year, m.Month, m.Purchases, m.Maintenance, m.Licenses, m.Total]).ToList());
    }

    private async Task<TabularData> BuildSlaAsync(IReadOnlyDictionary<string, string?> p, CancellationToken ct)
    {
        var report = await reports.GetSlaReportAsync(Date(p, "from"), Date(p, "to"), ct);
        var rows = report.ByPriority.Select(r => (IReadOnlyList<object?>)["Prioridad", r.Label, r.Resolved, r.WithinSla, r.CompliancePercent])
            .Concat(report.ByCategory.Select(r => (IReadOnlyList<object?>)["Categoria", r.Label, r.Resolved, r.WithinSla, r.CompliancePercent]))
            .ToList();
        return new TabularData("Cumplimiento SLA", ["Agrupacion", "Valor", "Resueltos", "Dentro de SLA", "Cumplimiento %"], rows);
    }

    // ---------- Parametros ----------

    /// <summary>Exportar tickets exige TICKETS.VIEW (verificado antes); el reporte cubre todos los tipos.</summary>
    private TicketScope AllTickets => new(currentUser.UserId ?? 0, AllIncidents: true, AllServiceRequests: true);

    private static AssetFilter AssetFilterFrom(IReadOnlyDictionary<string, string?> p) =>
        new(Enum<AssetStatus>(p, "status"), Int(p, "assetTypeId"), Int(p, "departmentId"), Int(p, "locationId"), Int(p, "currentUserId"), Date(p, "warrantyExpiresBefore"));

    private static TicketFilter TicketFilterFrom(IReadOnlyDictionary<string, string?> p) =>
        new(Enum<TicketType>(p, "type"), Enum<TicketStatus>(p, "status"), Enum<TicketPriority>(p, "priority"), Int(p, "categoryId"),
            Int(p, "assignedToId"), Int(p, "requesterId"), Int(p, "departmentId"), Bool(p, "overdue"), null, null);

    private static AuditFilter AuditFilterFrom(IReadOnlyDictionary<string, string?> p) =>
        new(Int(p, "userId"), Enum<AuditAction>(p, "action"), Str(p, "module"), Str(p, "entityName"), Str(p, "entityId"), null, null,
            DateTimeValue(p, "from"), DateTimeValue(p, "to"));

    private static string? Str(IReadOnlyDictionary<string, string?> p, string key) =>
        p.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static int? Int(IReadOnlyDictionary<string, string?> p, string key) =>
        int.TryParse(Str(p, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static bool? Bool(IReadOnlyDictionary<string, string?> p, string key) =>
        bool.TryParse(Str(p, key), out var b) ? b : null;

    private static DateOnly? Date(IReadOnlyDictionary<string, string?> p, string key) =>
        DateOnly.TryParseExact(Str(p, key), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static DateTime? DateTimeValue(IReadOnlyDictionary<string, string?> p, string key) =>
        DateTime.TryParse(Str(p, key), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    private static TEnum? Enum<TEnum>(IReadOnlyDictionary<string, string?> p, string key)
        where TEnum : struct, System.Enum =>
        System.Enum.TryParse<TEnum>(Str(p, key), ignoreCase: true, out var value) ? value : null;

    private string FileName(string report, string format) =>
        $"{report}-{TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, clock.TimeZone):yyyyMMdd-HHmm}.{format}";

    private static string NormalizeFormat(string format) => (format ?? "xlsx").Trim().ToLowerInvariant() switch
    {
        "csv" => "csv",
        "xlsx" or "excel" => "xlsx",
        var other => throw new DomainValidationException("EXPORT_FORMAT_NOT_SUPPORTED",
            $"Formato no soportado: {other}. Use csv o xlsx (PDF pendiente de Q-03).")
    };

    private static string ContentType(string format) => format == "csv"
        ? "text/csv; charset=utf-8"
        : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static ExportJobDto ToDto(ExportJob job) => new(
        job.Id, job.ReportName, job.Format, job.Status, job.RowCount, job.Error, job.CreatedAt, job.CompletedAt,
        job.Status == ExportJobStatus.Completed ? $"/api/v1/reports/exports/{job.Id}/download" : null);
}
