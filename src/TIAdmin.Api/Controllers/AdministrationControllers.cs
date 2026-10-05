namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Administration;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Platform;
using TIAdmin.Application.Reporting;
using TIAdmin.Domain.Enums;
using Perms = TIAdmin.Application.Common.Constants.Permissions;

/// <summary>Bitacora de auditoria (solo lectura; SPECS.md seccion 18).</summary>
[ApiController]
[Route("api/v1/audit")]
[Authorize(Policy = Perms.AuditView)]
[Produces("application/json")]
public sealed class AuditController(IAuditQueryService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] int? userId,
        [FromQuery] AuditAction? action,
        [FromQuery] string? module,
        [FromQuery] string? entityName,
        [FromQuery] string? entityId,
        [FromQuery] string? ipAddress,
        [FromQuery] string? correlationId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var filter = new AuditFilter(userId, action, module, entityName, entityId, ipAddress, correlationId, from, to);
        return Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(await audit.SearchAsync(query, filter, cancellationToken)));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<AuditLogDto>>> GetById(long id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AuditLogDto>.Ok(await audit.GetAsync(id, cancellationToken)));
}

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Policy = Perms.DashboardView)]
[Produces("application/json")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    /// <summary>KPIs segun los permisos del usuario: las secciones sin permiso vienen en null.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> Summary(CancellationToken cancellationToken) =>
        Ok(ApiResponse<DashboardSummaryDto>.Ok(await dashboard.GetSummaryAsync(cancellationToken)));
}

/// <summary>
/// Reportes en JSON (SPECS.md seccion 48). Periodos con fechas de negocio inclusivas (<c>from</c>, <c>to</c>)
/// en la zona de la organizacion. La exportacion (Excel/CSV/PDF, REPORTS.EXPORT) llega en la Fase 10.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = Perms.ReportsView)]
[Produces("application/json")]
public sealed class ReportsController(IReportService reports, IExportService exports) : ControllerBase
{
    [HttpGet("assets/summary")]
    public async Task<ActionResult<ApiResponse<AssetSummaryReport>>> AssetSummary(CancellationToken cancellationToken) =>
        Ok(ApiResponse<AssetSummaryReport>.Ok(await reports.GetAssetSummaryAsync(cancellationToken)));

    [HttpGet("assets/by-user")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AssetsByUserRow>>>> AssetsByUser([FromQuery] int? departmentId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<AssetsByUserRow>>.Ok(await reports.GetAssetsByUserAsync(departmentId, cancellationToken)));

    [HttpGet("tickets")]
    public async Task<ActionResult<ApiResponse<TicketReport>>> Tickets([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(ApiResponse<TicketReport>.Ok(await reports.GetTicketReportAsync(from, to, cancellationToken)));

    [HttpGet("sla")]
    public async Task<ActionResult<ApiResponse<SlaReport>>> Sla([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SlaReport>.Ok(await reports.GetSlaReportAsync(from, to, cancellationToken)));

    [HttpGet("licenses")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LicenseUsageReportRow>>>> Licenses(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<LicenseUsageReportRow>>.Ok(await reports.GetLicenseUsageAsync(cancellationToken)));

    /// <summary>Sin periodo: el anio en curso.</summary>
    [HttpGet("costs")]
    public async Task<ActionResult<ApiResponse<CostReport>>> Costs([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CostReport>.Ok(await reports.GetCostReportAsync(from, to, cancellationToken)));

    /// <summary>
    /// Exporta un reporte (assets, tickets, audit, assets-by-user, licenses, costs, sla) a csv o xlsx con los mismos
    /// filtros que su consulta. Si supera Exports.AsyncThreshold filas responde 202 con el trabajo encolado.
    /// </summary>
    [HttpGet("{report}/export")]
    [Authorize(Policy = Perms.ReportsExport)]
    public async Task<IActionResult> Export(string report, [FromQuery] string format, CancellationToken cancellationToken)
    {
        var parameters = Request.Query
            .Where(q => !string.Equals(q.Key, "format", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(q => q.Key, q => (string?)q.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var result = await exports.ExportAsync(report, format, parameters, cancellationToken);
        return result.File is { } file
            ? File(file.Content, file.MimeType, file.FileName)
            : Accepted(ApiResponse<ExportJobDto>.Ok(result.Job!, "La exportacion se procesara en segundo plano; recibira una notificacion."));
    }

    [HttpGet("exports/{id:int}")]
    [Authorize(Policy = Perms.ReportsExport)]
    public async Task<ActionResult<ApiResponse<ExportJobDto>>> GetExport(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ExportJobDto>.Ok(await exports.GetJobAsync(id, cancellationToken)));

    [HttpGet("exports/{id:int}/download")]
    [Authorize(Policy = Perms.ReportsExport)]
    public async Task<IActionResult> DownloadExport(int id, CancellationToken cancellationToken)
    {
        var file = await exports.DownloadJobAsync(id, cancellationToken);
        return File(file.Content, file.MimeType, file.FileName);
    }
}

[ApiController]
[Route("api/v1/configuration")]
[Authorize]
[Produces("application/json")]
public sealed class ConfigurationController(IConfigurationService configuration) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.ConfigurationView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConfigurationItemDto>>>> GetAll([FromQuery] string? group, CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<ConfigurationItemDto>>.Ok(await configuration.GetAllAsync(group, cancellationToken)));

    /// <summary>Parametros publicos para el frontend (zona horaria, nombre...). No requiere sesion.</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyDictionary<string, string?>>>> GetPublic(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyDictionary<string, string?>>.Ok(await configuration.GetPublicAsync(cancellationToken)));

    [HttpPut("{key}")]
    [Authorize(Policy = Perms.ConfigurationManage)]
    public async Task<ActionResult<ApiResponse<ConfigurationItemDto>>> Update(string key, [FromBody] UpdateConfigurationRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ConfigurationItemDto>.Ok(await configuration.UpdateAsync(key, request.Value, cancellationToken), "Parametro actualizado."));

    [HttpPost("{key}/reset")]
    [Authorize(Policy = Perms.ConfigurationManage)]
    public async Task<ActionResult<ApiResponse<ConfigurationItemDto>>> Reset(string key, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ConfigurationItemDto>.Ok(await configuration.ResetAsync(key, cancellationToken), "Parametro restablecido."));
}
