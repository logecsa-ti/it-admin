namespace TIAdmin.Application.Common;

using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;

/// <summary>
/// Agrega filas a la bitacora inmutable de movimientos del activo con el usuario, la hora
/// y el CorrelationId de la peticion actual. Lo usan los flujos que cambian un activo
/// (asignaciones, mantenimientos).
/// </summary>
public sealed class AssetMovementLog(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IAuditContext auditContext,
    IClock clock)
{
    public async Task AddAsync(
        Asset asset,
        AssetMovementType type,
        string? from,
        string? to,
        string? notes,
        CancellationToken cancellationToken,
        int? fromLocationId = null,
        int? toLocationId = null)
    {
        ArgumentNullException.ThrowIfNull(asset);

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
    }

    private static string? Truncate(string? value) => value is { Length: > 300 } ? value[..300] : value;
}
