namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Assets;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Platform;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Validators;
using TIAdmin.Domain.Enums;
using Perms = TIAdmin.Application.Common.Constants.Permissions;

[ApiController]
[Route("api/v1/assets")]
[Authorize]
[Produces("application/json")]
public sealed class AssetsController(
    IAssetService assets,
    IAssetImportService importer,
    ITabularFileWriter writer,
    IAssetHandoverService handover) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.AssetsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AssetListItemDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] AssetStatus? status,
        [FromQuery] int? assetTypeId,
        [FromQuery] int? departmentId,
        [FromQuery] int? locationId,
        [FromQuery] int? currentUserId,
        [FromQuery] DateOnly? warrantyExpiresBefore,
        CancellationToken cancellationToken)
    {
        var filter = new AssetFilter(status, assetTypeId, departmentId, locationId, currentUserId, warrantyExpiresBefore);
        return Ok(ApiResponse<PagedResult<AssetListItemDto>>.Ok(await assets.SearchAsync(query, filter, cancellationToken)));
    }

    /// <summary>Activos asignados al usuario autenticado. Solo requiere sesion.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<ApiResponse<PagedResult<AssetListItemDto>>>> GetMine(
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<AssetListItemDto>>.Ok(await assets.GetMineAsync(query, cancellationToken)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.AssetsView)]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AssetDetailDto>.Ok(await assets.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.AssetsCreate)]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> Create(
        [FromBody] CreateAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CreateAssetRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var asset = await assets.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = asset.Id }, ApiResponse<AssetDetailDto>.Ok(asset, "Activo creado."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.AssetsUpdate)]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> Update(
        int id,
        [FromBody] UpdateAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new UpdateAssetRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<AssetDetailDto>.Ok(await assets.UpdateAsync(id, request, cancellationToken)));
    }

    /// <summary>Cambio de estado administrativo (mantenimiento, reparacion, baja...). No asigna ni devuelve.</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = Perms.AssetsUpdate)]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> ChangeStatus(
        int id,
        [FromBody] ChangeAssetStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new ChangeAssetStatusRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<AssetDetailDto>.Ok(await assets.ChangeStatusAsync(id, request, cancellationToken)));
    }

    /// <summary>Baja logica (soft delete). Un activo asignado debe devolverse primero.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Perms.AssetsDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        await assets.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Activo eliminado."));
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Policy = Perms.AssetsAssign)]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> Assign(
        int id,
        [FromBody] AssignAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new AssignAssetRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<AssetDetailDto>.Ok(await assets.AssignAsync(id, request, cancellationToken), "Activo asignado."));
    }

    [HttpPost("{id:int}/return")]
    [Authorize(Policy = Perms.AssetsUnassign)]
    public async Task<ActionResult<ApiResponse<AssetDetailDto>>> Return(
        int id,
        [FromBody] ReturnAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new ReturnAssetRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<AssetDetailDto>.Ok(await assets.ReturnAsync(id, request, cancellationToken), "Activo devuelto."));
    }

    /// <summary>Historial de asignaciones del activo, lo mas reciente primero.</summary>
    [HttpGet("{id:int}/assignments")]
    [Authorize(Policy = Perms.AssignmentsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AssetAssignmentDto>>>> GetAssignments(
        int id,
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<AssetAssignmentDto>>.Ok(
            await assets.GetAssignmentsAsync(query, new AssignmentFilter(null, id, null), cancellationToken)));

    /// <summary>
    /// Importa activos desde csv/xlsx (multipart <c>file</c>). Todo o nada: con errores no importa ninguna fila.
    /// <c>dryRun=true</c> solo valida.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = Perms.AssetsCreate)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ImportResult>>> Import(IFormFile? file, [FromQuery] bool dryRun, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(ApiResponse.Fail("Adjunte un archivo.", [new ApiError("FILE_REQUIRED", "Adjunte un archivo.")]));
        }

        var format = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        await using var content = file.OpenReadStream();
        var result = await importer.ImportAsync(content, format, dryRun, cancellationToken);

        return result.Errors.Count > 0
            ? BadRequest(ApiResponse<ImportResult>.Fail($"El archivo tiene {result.Errors.Count} errores; no se importo ninguna fila.",
                result.Errors.Take(100).Select(e => new ApiError("IMPORT_ROW_ERROR", $"Fila {e.Row}, {e.Field}: {e.Message}"))))
            : Ok(ApiResponse<ImportResult>.Ok(result, dryRun ? "Validacion correcta." : $"{result.Imported} activos importados."));
    }

    [HttpGet("import/template")]
    [Authorize(Policy = Perms.AssetsCreate)]
    public async Task<IActionResult> ImportTemplate([FromQuery] string? format, CancellationToken cancellationToken)
    {
        var normalized = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "xlsx";
        var output = new MemoryStream();
        await writer.WriteAsync(importer.Template(), normalized, output, cancellationToken);
        output.Position = 0;
        return File(output, normalized == "csv" ? "text/csv; charset=utf-8" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"plantilla-activos.{normalized}");
    }

    [HttpGet("{id:int}/movements")]
    [Authorize(Policy = Perms.AssetsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AssetMovementDto>>>> GetMovements(
        int id,
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<AssetMovementDto>>.Ok(await assets.GetMovementsAsync(id, query, cancellationToken)));

    /// <summary>
    /// Acta de entrega (<c>delivery</c>) o devolucion (<c>return</c>) de una asignacion en PDF: la copia archivada
    /// al asignar/devolver, o generada al momento para asignaciones anteriores (ADR-040).
    /// </summary>
    [HttpGet("{id:int}/assignments/{assignmentId:int}/handover/{kind}")]
    [Authorize(Policy = Perms.AssetsView)]
    [Produces("application/pdf", "application/json")]
    public async Task<IActionResult> GetHandoverDocument(int id, int assignmentId, HandoverKind kind, CancellationToken cancellationToken)
    {
        var content = await handover.GetAsync(id, assignmentId, kind, cancellationToken);
        return File(content.Content, content.MimeType, content.FileName);
    }
}

[ApiController]
[Route("api/v1/assignments")]
[Authorize]
[Produces("application/json")]
public sealed class AssignmentsController(IAssetService assets) : ControllerBase
{
    /// <summary>Asignaciones (vigentes e historicas), filtrables por usuario o activo.</summary>
    [HttpGet]
    [Authorize(Policy = Perms.AssignmentsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AssetAssignmentDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] int? userId,
        [FromQuery] int? assetId,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<AssetAssignmentDto>>.Ok(
            await assets.GetAssignmentsAsync(query, new AssignmentFilter(userId, assetId, activeOnly), cancellationToken)));
}
