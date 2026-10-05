namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

public class Asset : AuditableSoftDeletableEntity
{
    public string AssetCode { get; set; } = string.Empty;

    public string? SerialNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int AssetTypeId { get; set; }

    public string? Brand { get; set; }

    public string? Model { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    public decimal? PurchaseCost { get; set; }

    public DateOnly? WarrantyExpiration { get; set; }

    /// <summary>Solo cambia via <see cref="AssignTo"/>, <see cref="Return"/> y <see cref="ChangeStatus"/>.</summary>
    public AssetStatus Status { get; private set; } = AssetStatus.Available;

    /// <summary>Desnormalizado desde la asignacion activa para consultas rapidas.</summary>
    public int? CurrentUserId { get; private set; }

    public int? LocationId { get; set; }

    public int? DepartmentId { get; set; }

    public int? VendorId { get; set; }

    public int? ParentAssetId { get; set; }

    public string? Notes { get; set; }

    public AssetType? AssetType { get; set; }

    public Location? Location { get; set; }

    public Department? Department { get; set; }

    public Asset? ParentAsset { get; set; }

    public ICollection<Asset>? Children { get; set; }

    /// <summary>
    /// Asigna el activo a un usuario (SPECS.md seccion 20). Solo un activo disponible puede asignarse.
    /// Devuelve el nuevo registro de historial: las asignaciones previas nunca se modifican.
    /// </summary>
    public AssetAssignment AssignTo(int userId, int assignedById, DateTime now, string? condition, string? notes)
    {
        if (Status != AssetStatus.Available)
        {
            throw new ConflictException("ASSET_NOT_AVAILABLE",
                $"El activo {AssetCode} no esta disponible (estado actual: {Status}).");
        }

        Status = AssetStatus.Assigned;
        CurrentUserId = userId;

        return new AssetAssignment
        {
            AssetId = Id,
            Asset = this,
            UserId = userId,
            AssignedById = assignedById,
            AssignmentDate = now,
            ConditionAtAssignment = condition,
            Notes = notes,
            IsActive = true
        };
    }

    /// <summary>
    /// Devuelve el activo: cierra la asignacion vigente y deja el activo en <paramref name="resultingStatus"/>
    /// (Available por defecto; Maintenance o Repair si vuelve con fallas).
    /// </summary>
    public void Return(
        AssetAssignment activeAssignment,
        int returnedById,
        DateTime now,
        string? condition,
        string? notes,
        AssetStatus resultingStatus = AssetStatus.Available)
    {
        ArgumentNullException.ThrowIfNull(activeAssignment);

        if (Status != AssetStatus.Assigned || !activeAssignment.IsActive || activeAssignment.AssetId != Id)
        {
            throw new ConflictException("ASSET_NOT_ASSIGNED", $"El activo {AssetCode} no tiene una asignacion vigente.");
        }

        if (resultingStatus is not (AssetStatus.Available or AssetStatus.Maintenance or AssetStatus.Repair))
        {
            throw new DomainValidationException("INVALID_RETURN_STATUS",
                "Al devolver un activo solo puede quedar Available, Maintenance o Repair.");
        }

        activeAssignment.Close(returnedById, now, condition, notes);
        Status = resultingStatus;
        CurrentUserId = null;
    }

    /// <summary>
    /// Cambio de estado administrativo. Assigned solo se alcanza/abandona con AssignTo/Return.
    /// </summary>
    public void ChangeStatus(AssetStatus newStatus)
    {
        if (newStatus == Status)
        {
            return;
        }

        if (!AssetStatusRules.CanChange(Status, newStatus))
        {
            throw new ConflictException("INVALID_STATUS_TRANSITION",
                $"No se puede cambiar el estado de {Status} a {newStatus}.");
        }

        Status = newStatus;
    }

    public void EnsureCanBeDeleted()
    {
        if (Status == AssetStatus.Assigned)
        {
            throw new ConflictException("ASSET_ASSIGNED", "No se puede eliminar un activo asignado; devuelvalo primero.");
        }
    }
}

/// <summary>
/// Transiciones de estado permitidas fuera del flujo de asignacion.
/// </summary>
public static class AssetStatusRules
{
    private static readonly Dictionary<AssetStatus, AssetStatus[]> Allowed = new()
    {
        [AssetStatus.Available] = [AssetStatus.Maintenance, AssetStatus.Repair, AssetStatus.Retired, AssetStatus.Lost, AssetStatus.Disposed],
        [AssetStatus.Maintenance] = [AssetStatus.Available, AssetStatus.Repair, AssetStatus.Retired, AssetStatus.Disposed],
        [AssetStatus.Repair] = [AssetStatus.Available, AssetStatus.Maintenance, AssetStatus.Retired, AssetStatus.Disposed],
        [AssetStatus.Retired] = [AssetStatus.Available, AssetStatus.Disposed],
        [AssetStatus.Lost] = [AssetStatus.Available, AssetStatus.Disposed],
        [AssetStatus.Assigned] = [],
        [AssetStatus.Disposed] = []
    };

    public static bool CanChange(AssetStatus from, AssetStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}

/// <summary>
/// Registro de historial de asignacion. Una vez cerrado (ReturnDate) no vuelve a modificarse.
/// </summary>
public class AssetAssignment : AuditableEntity
{
    public int AssetId { get; set; }

    public int UserId { get; set; }

    public DateTime AssignmentDate { get; set; }

    public DateTime? ReturnDate { get; private set; }

    public int AssignedById { get; set; }

    public int? ReturnedById { get; private set; }

    public string? ConditionAtAssignment { get; set; }

    public string? ConditionAtReturn { get; private set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public Asset? Asset { get; set; }

    internal void Close(int returnedById, DateTime now, string? condition, string? notes)
    {
        if (!IsActive)
        {
            throw new ConflictException("ASSIGNMENT_CLOSED", "La asignacion ya fue cerrada.");
        }

        IsActive = false;
        ReturnDate = now;
        ReturnedById = returnedById;
        ConditionAtReturn = condition;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            Notes = string.IsNullOrWhiteSpace(Notes) ? notes : $"{Notes}\n{notes}";
        }
    }
}

/// <summary>
/// Bitacora inmutable de movimientos del activo (asignaciones, devoluciones, cambios
/// de estado, ubicacion o departamento). UserName es una copia historica.
/// </summary>
public class AssetMovement : Entity
{
    public int AssetId { get; set; }

    public AssetMovementType MovementType { get; set; }

    public string? FromValue { get; set; }

    public string? ToValue { get; set; }

    public int? FromLocationId { get; set; }

    public int? ToLocationId { get; set; }

    public int? UserId { get; set; }

    public string? UserName { get; set; }

    public string? Notes { get; set; }

    public DateTime Timestamp { get; set; }

    public string? CorrelationId { get; set; }

    public Asset? Asset { get; set; }
}
