namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;

public interface IMaintenanceRepository : IRepository<Maintenance>
{
    Task<PagedResult<MaintenanceDto>> SearchAsync(PagedQuery query, MaintenanceFilter filter, DateTime now, CancellationToken cancellationToken = default);

    Task<MaintenanceDto?> GetDtoAsync(int id, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Otro mantenimiento del activo en curso (excluyendo <paramref name="excludeId"/>).</summary>
    Task<bool> HasOtherInProgressAsync(int assetId, int excludeId, CancellationToken cancellationToken = default);

    /// <summary>Planificados/programados con fecha anterior o igual a <paramref name="until"/> (incluye vencidos).</summary>
    Task<IReadOnlyList<MaintenanceAlertDto>> GetPendingUntilAsync(DateTime until, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ultimo preventivo completado por activo con NextDueDate anterior o igual a <paramref name="until"/>,
    /// sin un mantenimiento abierto posterior.
    /// </summary>
    Task<IReadOnlyList<MaintenanceAlertDto>> GetPreventiveDueAsync(DateOnly until, CancellationToken cancellationToken = default);
}

public interface IChangeRequestRepository : IRepository<ChangeRequest>
{
    Task<PagedResult<ChangeRequestDto>> SearchAsync(PagedQuery query, ChangeFilter filter, int currentUserId, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default);
}

public interface IPurchaseRequestRepository : IRepository<PurchaseRequest>
{
    /// <summary>Con sus partidas, con seguimiento de cambios.</summary>
    Task<PurchaseRequest?> GetWithItemsAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResult<PurchaseRequestDto>> SearchAsync(PagedQuery query, PurchaseFilter filter, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default);
}
