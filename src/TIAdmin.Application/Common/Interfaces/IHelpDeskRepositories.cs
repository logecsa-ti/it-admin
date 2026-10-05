namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;

public interface ITicketCategoryRepository : IRepository<TicketCategory>
{
    Task<IReadOnlyList<TicketCategoryDto>> ListAsync(bool? isActive, CancellationToken cancellationToken = default);

    /// <summary>Incluye eliminadas: el codigo queda reservado.</summary>
    Task<bool> ExistsCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> ExistsNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
}

public interface ISlaPolicyRepository : IRepository<SlaPolicy>
{
    Task<IReadOnlyList<SlaPolicy>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Quita la marca IsDefault de las demas politicas (solo puede haber una por defecto).</summary>
    Task ClearDefaultAsync(int exceptId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alcance de visibilidad del usuario actual: si puede ver todos los incidentes y/o todas las
/// solicitudes; en caso contrario solo ve los tickets que solicito (<paramref name="UserId"/>).
/// </summary>
public record TicketScope(int UserId, bool AllIncidents, bool AllServiceRequests);

public record SlaBreachRow(int Id, string TicketNumber, string Title, int? AssignedToId, bool ResponseBreached, bool ResolutionBreached);

public interface ITicketRepository : IRepository<Ticket>
{
    Task<PagedResult<TicketListItemDto>> SearchAsync(
        PagedQuery query,
        TicketFilter filter,
        TicketScope scope,
        DateTime now,
        CancellationToken cancellationToken = default);

    Task<TicketDetailDto?> GetDetailAsync(int id, DateTime now, CancellationToken cancellationToken = default);

    Task AddCommentAsync(TicketComment comment, CancellationToken cancellationToken = default);

    Task AddHistoryAsync(TicketStatusHistory history, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketCommentDto>> GetCommentsAsync(int ticketId, bool includeInternal, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketHistoryDto>> GetHistoryAsync(int ticketId, CancellationToken cancellationToken = default);

    /// <summary>Tickets abiertos con el SLA de respuesta o de resolucion vencido.</summary>
    Task<IReadOnlyList<SlaBreachRow>> GetOpenSlaBreachesAsync(DateTime now, CancellationToken cancellationToken = default);
}
