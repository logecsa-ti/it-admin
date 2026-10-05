namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Assets;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Validators;
using TIAdmin.Domain.Enums;
using Perms = TIAdmin.Application.Common.Constants.Permissions;

[ApiController]
[Route("api/v1/assets")]
[Authorize]
[Produces("application/json")]
public sealed class AssetsController(IAssetService assets) : ControllerBase
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

    [HttpGet("{id:int}/movements")]
    [Authorize(Policy = Perms.AssetsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AssetMovementDto>>>> GetMovements(
        int id,
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<AssetMovementDto>>.Ok(await assets.GetMovementsAsync(id, query, cancellationToken)));
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
