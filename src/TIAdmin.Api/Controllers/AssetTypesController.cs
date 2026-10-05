namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Common;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Validators;
using TIAdmin.Domain.Entities;
using Perms = TIAdmin.Application.Common.Constants.Permissions;

/// <summary>
/// Catalogo de tipos de activo. No se eliminan: se desactivan (IsActive) para conservar
/// la referencia de los activos existentes.
/// </summary>
[ApiController]
[Route("api/v1/asset-types")]
[Authorize]
[Produces("application/json")]
public sealed class AssetTypesController(IUnitOfWork unitOfWork, CatalogCache cache) : ControllerBase
{
    private const string Catalog = "asset-types";

    [HttpGet]
    [Authorize(Policy = Perms.AssetsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AssetTypeDto>>>> GetAll(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<AssetTypeDto>>.Ok(await cache.GetOrCreateAsync(Catalog, isActive?.ToString() ?? "all",
            () => unitOfWork.AssetTypes.ListAsync(isActive, cancellationToken))));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.AssetsView)]
    public async Task<ActionResult<ApiResponse<AssetTypeDto>>> GetById(int id, CancellationToken cancellationToken)
    {
        var type = await unitOfWork.AssetTypes.GetByIdAsync(id, cancellationToken);
        return type is null
            ? NotFound(ApiResponse<AssetTypeDto>.Fail("Tipo de activo no encontrado."))
            : Ok(ApiResponse<AssetTypeDto>.Ok(ToDto(type)));
    }

    [HttpPost]
    [Authorize(Policy = Perms.AssetTypesManage)]
    public async Task<ActionResult<ApiResponse<AssetTypeDto>>> Create(
        [FromBody] CreateAssetTypeRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CreateAssetTypeRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var name = request.Name.Trim();
        if (await unitOfWork.AssetTypes.ExistsCodeAsync(request.Code, cancellationToken))
        {
            return Conflict(ApiResponse<AssetTypeDto>.Fail("El codigo del tipo de activo ya existe.",
                [new ApiError("ASSET_TYPE_CODE_ALREADY_EXISTS", "El codigo del tipo de activo ya existe.")]));
        }

        if (await unitOfWork.AssetTypes.ExistsNameAsync(name, null, cancellationToken))
        {
            return Conflict(ApiResponse<AssetTypeDto>.Fail("El nombre del tipo de activo ya existe.",
                [new ApiError("ASSET_TYPE_NAME_ALREADY_EXISTS", "El nombre del tipo de activo ya existe.")]));
        }

        var type = new AssetType
        {
            Code = request.Code,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true
        };

        await unitOfWork.AssetTypes.AddAsync(type, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate(Catalog);

        return CreatedAtAction(nameof(GetById), new { id = type.Id }, ApiResponse<AssetTypeDto>.Ok(ToDto(type)));
    }

    /// <summary>El codigo es inmutable; nombre, descripcion y estado activo son editables.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.AssetTypesManage)]
    public async Task<ActionResult<ApiResponse<AssetTypeDto>>> Update(
        int id,
        [FromBody] UpdateAssetTypeRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new UpdateAssetTypeRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var type = await unitOfWork.AssetTypes.GetByIdAsync(id, cancellationToken);
        if (type is null)
        {
            return NotFound(ApiResponse<AssetTypeDto>.Fail("Tipo de activo no encontrado."));
        }

        var name = request.Name.Trim();
        if (await unitOfWork.AssetTypes.ExistsNameAsync(name, id, cancellationToken))
        {
            return Conflict(ApiResponse<AssetTypeDto>.Fail("El nombre del tipo de activo ya existe.",
                [new ApiError("ASSET_TYPE_NAME_ALREADY_EXISTS", "El nombre del tipo de activo ya existe.")]));
        }

        type.Name = name;
        type.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        type.IsActive = request.IsActive;

        unitOfWork.AssetTypes.Update(type);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate(Catalog);

        return Ok(ApiResponse<AssetTypeDto>.Ok(ToDto(type)));
    }

    private static AssetTypeDto ToDto(AssetType type) => new(type.Id, type.Code, type.Name, type.Description, type.IsActive);
}
