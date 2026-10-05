namespace TIAdmin.Application.Assets;

using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Casos de uso del inventario de activos y sus asignaciones (SPECS.md secciones 19-20).
/// Las reglas de estado viven en <see cref="Asset"/>; este servicio valida referencias,
/// registra movimientos (<see cref="AssetMovement"/>) y persiste.
/// </summary>
public interface IAssetService
{
    Task<PagedResult<AssetListItemDto>> SearchAsync(PagedQuery query, AssetFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Activos asignados al usuario autenticado.</summary>
    Task<PagedResult<AssetListItemDto>> GetMineAsync(PagedQuery query, CancellationToken cancellationToken = default);

    Task<AssetDetailDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<AssetDetailDto> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken = default);

    Task<AssetDetailDto> UpdateAsync(int id, UpdateAssetRequest request, CancellationToken cancellationToken = default);

    Task<AssetDetailDto> ChangeStatusAsync(int id, ChangeAssetStatusRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<AssetDetailDto> AssignAsync(int id, AssignAssetRequest request, CancellationToken cancellationToken = default);

    Task<AssetDetailDto> ReturnAsync(int id, ReturnAssetRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<AssetAssignmentDto>> GetAssignmentsAsync(PagedQuery query, AssignmentFilter filter, CancellationToken cancellationToken = default);

    Task<PagedResult<AssetMovementDto>> GetMovementsAsync(int id, PagedQuery query, CancellationToken cancellationToken = default);
}

public sealed class AssetService(
    IUnitOfWork unitOfWork,
    IUserDirectory userDirectory,
    ICurrentUserService currentUser,
    IAuditContext auditContext,
    IClock clock)
    : IAssetService
{
    public Task<PagedResult<AssetListItemDto>> SearchAsync(
        PagedQuery query,
        AssetFilter filter,
        CancellationToken cancellationToken = default) =>
        unitOfWork.Assets.SearchAsync(query, filter, cancellationToken);

    public Task<PagedResult<AssetListItemDto>> GetMineAsync(PagedQuery query, CancellationToken cancellationToken = default) =>
        unitOfWork.Assets.SearchAsync(
            query,
            new AssetFilter(null, null, null, null, CurrentUserId(), null),
            cancellationToken);

    public async Task<AssetDetailDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await unitOfWork.Assets.GetDetailAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Asset", id);

    public async Task<AssetDetailDto> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assetCode = request.AssetCode.Trim().ToUpperInvariant();
        if (await unitOfWork.Assets.ExistsAssetCodeAsync(assetCode, cancellationToken))
        {
            throw new ConflictException("ASSET_CODE_ALREADY_EXISTS", $"El codigo de activo {assetCode} ya existe.");
        }

        await ValidateReferencesAsync(request, assetId: null, currentTypeId: null, currentVendorId: null, cancellationToken);

        var asset = new Asset { AssetCode = assetCode };
        Apply(asset, request);

        await unitOfWork.Assets.AddAsync(asset, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(asset.Id, cancellationToken);
    }

    public async Task<AssetDetailDto> UpdateAsync(int id, UpdateAssetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await FindAsync(id, cancellationToken);
        await ValidateReferencesAsync(request, asset.Id, asset.AssetTypeId, asset.VendorId, cancellationToken);

        if (asset.LocationId != request.LocationId)
        {
            await AddMovementAsync(asset, AssetMovementType.LocationChange,
                await LocationNameAsync(asset.LocationId, cancellationToken),
                await LocationNameAsync(request.LocationId, cancellationToken),
                notes: null, cancellationToken, fromLocationId: asset.LocationId, toLocationId: request.LocationId);
        }

        if (asset.DepartmentId != request.DepartmentId)
        {
            await AddMovementAsync(asset, AssetMovementType.DepartmentChange,
                await DepartmentNameAsync(asset.DepartmentId, cancellationToken),
                await DepartmentNameAsync(request.DepartmentId, cancellationToken),
                notes: null, cancellationToken);
        }

        Apply(asset, request);
        unitOfWork.Assets.Update(asset);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<AssetDetailDto> ChangeStatusAsync(
        int id,
        ChangeAssetStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await FindAsync(id, cancellationToken);
        var previous = asset.Status;
        asset.ChangeStatus(request.Status);

        if (previous != asset.Status)
        {
            await AddMovementAsync(asset, AssetMovementType.StatusChange,
                previous.ToString(), asset.Status.ToString(), Normalize(request.Notes), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var asset = await FindAsync(id, cancellationToken);
        asset.EnsureCanBeDeleted();

        unitOfWork.Assets.Delete(asset);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AssetDetailDto> AssignAsync(int id, AssignAssetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await FindAsync(id, cancellationToken);
        var assignee = await userDirectory.FindAsync(request.UserId, cancellationToken);
        if (assignee is not { IsActive: true })
        {
            throw new DomainValidationException("USER_NOT_AVAILABLE", "El usuario indicado no existe o esta inactivo.");
        }

        var assignment = asset.AssignTo(assignee.Id, CurrentUserId(), clock.UtcNow,
            Normalize(request.Condition), Normalize(request.Notes));

        await unitOfWork.AssetAssignments.AddAsync(assignment, cancellationToken);
        await AddMovementAsync(asset, AssetMovementType.Assignment, null, assignee.Label,
            Normalize(request.Notes), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<AssetDetailDto> ReturnAsync(int id, ReturnAssetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await FindAsync(id, cancellationToken);
        var active = await unitOfWork.AssetAssignments.GetActiveAssignmentAsync(id, cancellationToken)
            ?? throw new ConflictException("ASSET_NOT_ASSIGNED", $"El activo {asset.AssetCode} no tiene una asignacion vigente.");
        var previousHolder = await userDirectory.FindAsync(active.UserId, cancellationToken);

        asset.Return(active, CurrentUserId(), clock.UtcNow, Normalize(request.Condition), Normalize(request.Notes),
            request.ResultingStatus ?? AssetStatus.Available);

        await AddMovementAsync(asset, AssetMovementType.Return,
            previousHolder?.Label ?? $"UserId={active.UserId}", asset.Status.ToString(),
            Normalize(request.Notes), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public Task<PagedResult<AssetAssignmentDto>> GetAssignmentsAsync(
        PagedQuery query,
        AssignmentFilter filter,
        CancellationToken cancellationToken = default) =>
        unitOfWork.AssetAssignments.SearchAsync(query, filter, cancellationToken);

    public async Task<PagedResult<AssetMovementDto>> GetMovementsAsync(
        int id,
        PagedQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await unitOfWork.Assets.ExistsAsync(id, cancellationToken))
        {
            throw new EntityNotFoundException("Asset", id);
        }

        return await unitOfWork.AssetMovements.GetByAssetAsync(id, query, cancellationToken);
    }

    private async Task<Asset> FindAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.Assets.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Asset", id);

    private int CurrentUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("Operacion sin usuario autenticado.");

    private async Task ValidateReferencesAsync(
        IAssetData data,
        int? assetId,
        int? currentTypeId,
        int? currentVendorId,
        CancellationToken cancellationToken)
    {
        // Un tipo inactivo no admite activos nuevos, pero los existentes pueden conservarlo.
        if (data.AssetTypeId != currentTypeId)
        {
            var type = await unitOfWork.AssetTypes.GetByIdAsync(data.AssetTypeId, cancellationToken);
            if (type is not { IsActive: true })
            {
                throw new DomainValidationException("ASSET_TYPE_NOT_AVAILABLE", "El tipo de activo no existe o esta inactivo.");
            }
        }

        if (Normalize(data.SerialNumber) is { } serial
            && await unitOfWork.Assets.ExistsSerialNumberAsync(serial, assetId, cancellationToken))
        {
            throw new ConflictException("SERIAL_NUMBER_ALREADY_EXISTS", $"El numero de serie {serial} ya esta registrado.");
        }

        if (data.LocationId is { } locationId && !await unitOfWork.Locations.ExistsAsync(locationId, cancellationToken))
        {
            throw new DomainValidationException("LOCATION_NOT_FOUND", "La ubicacion indicada no existe.");
        }

        if (data.DepartmentId is { } departmentId && !await unitOfWork.Departments.ExistsAsync(departmentId, cancellationToken))
        {
            throw new DomainValidationException("DEPARTMENT_NOT_FOUND", "El departamento indicado no existe.");
        }

        // Un proveedor existente se conserva aunque se haya bloqueado; uno nuevo debe estar activo.
        if (data.VendorId is { } vendorId && vendorId != currentVendorId)
        {
            var vendor = await unitOfWork.Vendors.GetByIdAsync(vendorId, cancellationToken);
            if (vendor is not { Status: VendorStatus.Active })
            {
                throw new DomainValidationException("VENDOR_NOT_AVAILABLE", "El proveedor no existe o no esta activo.");
            }
        }

        if (data.ParentAssetId is { } parentId)
        {
            if (!await unitOfWork.Assets.ExistsAsync(parentId, cancellationToken))
            {
                throw new DomainValidationException("PARENT_ASSET_NOT_FOUND", "El activo padre no existe.");
            }

            if (assetId is { } id && await unitOfWork.Assets.WouldCreateCycleAsync(id, parentId, cancellationToken))
            {
                throw new DomainValidationException("PARENT_ASSET_CYCLE",
                    "El activo padre no puede ser el mismo activo ni uno de sus componentes.");
            }
        }
    }

    private static void Apply(Asset asset, IAssetData data)
    {
        asset.SerialNumber = Normalize(data.SerialNumber);
        asset.Name = data.Name.Trim();
        asset.Description = Normalize(data.Description);
        asset.AssetTypeId = data.AssetTypeId;
        asset.Brand = Normalize(data.Brand);
        asset.Model = Normalize(data.Model);
        asset.PurchaseDate = data.PurchaseDate;
        asset.PurchaseCost = data.PurchaseCost;
        asset.WarrantyExpiration = data.WarrantyExpiration;
        asset.LocationId = data.LocationId;
        asset.DepartmentId = data.DepartmentId;
        asset.VendorId = data.VendorId;
        asset.ParentAssetId = data.ParentAssetId;
        asset.Notes = Normalize(data.Notes);
    }

    private async Task AddMovementAsync(
        Asset asset,
        AssetMovementType type,
        string? from,
        string? to,
        string? notes,
        CancellationToken cancellationToken,
        int? fromLocationId = null,
        int? toLocationId = null) =>
        await unitOfWork.AssetMovements.AddAsync(new AssetMovement
        {
            AssetId = asset.Id,
            MovementType = type,
            FromValue = Truncate(from),
            ToValue = Truncate(to),
            FromLocationId = fromLocationId,
            ToLocationId = toLocationId,
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            Notes = notes,
            Timestamp = clock.UtcNow,
            CorrelationId = auditContext.CorrelationId
        }, cancellationToken);

    private async Task<string?> LocationNameAsync(int? id, CancellationToken cancellationToken) =>
        id is { } value ? (await unitOfWork.Locations.GetByIdAsync(value, cancellationToken))?.Name : null;

    private async Task<string?> DepartmentNameAsync(int? id, CancellationToken cancellationToken) =>
        id is { } value ? (await unitOfWork.Departments.GetByIdAsync(value, cancellationToken))?.Name : null;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value) => value is { Length: > 300 } ? value[..300] : value;
}
