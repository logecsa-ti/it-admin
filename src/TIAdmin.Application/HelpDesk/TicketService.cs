namespace TIAdmin.Application.HelpDesk;

using TIAdmin.Application.Common;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;
using TIAdmin.Domain.Services;

/// <summary>
/// Help Desk: incidentes y solicitudes de servicio en una sola entidad (Q-07, SPECS.md secciones 21-22).
/// Visibilidad (ADR-026): TICKETS.VIEW / REQUESTS.VIEW permiten ver todos los incidentes / solicitudes;
/// sin ellos, cada usuario ve solo los tickets que solicito y nunca los comentarios internos.
/// </summary>
public interface ITicketService
{
    Task<PagedResult<TicketListItemDto>> SearchAsync(PagedQuery query, TicketFilter filter, CancellationToken cancellationToken = default);

    Task<TicketDetailDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken = default);

    Task<TicketDetailDto> UpdateAsync(int id, UpdateTicketRequest request, CancellationToken cancellationToken = default);

    Task<TicketDetailDto> ChangeStatusAsync(int id, ChangeTicketStatusRequest request, CancellationToken cancellationToken = default);

    Task<TicketDetailDto> AssignAsync(int id, AssignTicketRequest request, CancellationToken cancellationToken = default);

    Task<TicketDetailDto> ApproveAsync(int id, ApprovalDecisionRequest request, CancellationToken cancellationToken = default);

    Task<TicketDetailDto> RejectAsync(int id, ApprovalDecisionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketCommentDto>> GetCommentsAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketCommentDto>> AddCommentAsync(int id, AddTicketCommentRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketHistoryDto>> GetHistoryAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class TicketService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IUserDirectory userDirectory,
    IPermissionService permissionService,
    ISystemSettings settings,
    IClock clock)
    : ITicketService
{
    public const string NumberPrefixKey = "Tickets.NumberPrefix";

    public async Task<PagedResult<TicketListItemDto>> SearchAsync(
        PagedQuery query,
        TicketFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var scope = new TicketScope(Me, Has(Permissions.TicketsView), Has(Permissions.RequestsView));
        return await unitOfWork.Tickets.SearchAsync(query, filter, scope, clock.UtcNow, cancellationToken);
    }

    public async Task<TicketDetailDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await FindVisibleAsync(id, cancellationToken);
        return (await unitOfWork.Tickets.GetDetailAsync(id, clock.UtcNow, cancellationToken))!;
    }

    public async Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = await unitOfWork.TicketCategories.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is not { IsActive: true })
        {
            throw new DomainValidationException("CATEGORY_NOT_AVAILABLE", "La categoria no existe o esta inactiva.");
        }

        Require(category.Type == TicketType.Incident ? Permissions.TicketsCreate : Permissions.RequestsCreate);

        var requesterId = request.RequesterId ?? Me;
        if (requesterId != Me)
        {
            // Registrar en nombre de otro (p. ej. una llamada telefonica) es tarea de agentes.
            Require(Permissions.TicketsUpdate);
            if (await userDirectory.FindAsync(requesterId, cancellationToken) is not { IsActive: true })
            {
                throw new DomainValidationException("USER_NOT_AVAILABLE", "El solicitante no existe o esta inactivo.");
            }
        }

        await ValidateReferencesAsync(request.DepartmentId, request.AssetId, cancellationToken);

        var now = clock.UtcNow;
        var ticket = new Ticket
        {
            // Numero provisional unico; el definitivo se deriva del Id tras insertar.
            TicketNumber = DocumentNumbers.Provisional(),
            Type = category.Type,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CategoryId = category.Id,
            // La prioridad la fija el agente; el solicitante recibe la de la categoria.
            Priority = IsAgent && request.Priority is { } priority ? priority : category.DefaultPriority,
            RequesterId = requesterId,
            DepartmentId = request.DepartmentId ?? category.DepartmentId,
            AssetId = request.AssetId,
            NeededByDate = request.NeededByDate
        };

        if (category.Type == TicketType.ServiceRequest && category.RequiresApproval)
        {
            // El reloj del SLA arranca al aprobar.
            ticket.RequireApproval();
        }
        else
        {
            await ApplySlaAsync(ticket, now, cancellationToken);
        }

        await unitOfWork.Tickets.AddAsync(ticket, cancellationToken);
        await unitOfWork.Tickets.AddHistoryAsync(new TicketStatusHistory
        {
            Ticket = ticket,
            ToStatus = TicketStatus.New,
            UserId = Me,
            UserName = currentUser.UserName,
            Comment = ticket.IsAwaitingApproval ? "Creado (pendiente de aprobacion)" : "Creado",
            Timestamp = now
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        ticket.TicketNumber = await DocumentNumbers.FormatAsync(settings, clock, NumberPrefixKey, "TKT", ticket.Id, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(ticket.Id, cancellationToken);
    }

    public async Task<TicketDetailDto> UpdateAsync(int id, UpdateTicketRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await FindAsync(id, cancellationToken);
        if (!ticket.IsOpen)
        {
            throw new ConflictException("TICKET_CLOSED", $"No se puede editar un ticket en estado {ticket.Status}.");
        }

        if (request.CategoryId != ticket.CategoryId)
        {
            var category = await unitOfWork.TicketCategories.GetByIdAsync(request.CategoryId, cancellationToken);
            if (category is not { IsActive: true })
            {
                throw new DomainValidationException("CATEGORY_NOT_AVAILABLE", "La categoria no existe o esta inactiva.");
            }

            if (category.Type != ticket.Type)
            {
                throw new DomainValidationException("CATEGORY_TYPE_MISMATCH",
                    "La nueva categoria debe ser del mismo tipo (incidente o solicitud) que el ticket.");
            }
        }

        await ValidateReferencesAsync(request.DepartmentId, request.AssetId, cancellationToken);

        var slaInputsChanged = request.CategoryId != ticket.CategoryId
            || request.Priority != ticket.Priority
            || request.DepartmentId != ticket.DepartmentId;

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.CategoryId = request.CategoryId;
        ticket.Priority = request.Priority;
        ticket.DepartmentId = request.DepartmentId;
        ticket.AssetId = request.AssetId;
        ticket.NeededByDate = request.NeededByDate;

        // El SLA se recalcula desde que empezo a correr (creacion o aprobacion), no desde ahora.
        if (slaInputsChanged && ticket.DueAtResolution is not null)
        {
            await ApplySlaAsync(ticket, ticket.ApprovedAt ?? ticket.CreatedAt, cancellationToken);
        }

        unitOfWork.Tickets.Update(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<TicketDetailDto> ChangeStatusAsync(int id, ChangeTicketStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await FindVisibleAsync(id, cancellationToken);
        var isRequester = ticket.RequesterId == Me;

        var allowed = request.Status switch
        {
            TicketStatus.Resolved => Has(Permissions.TicketsResolve),
            // El solicitante puede confirmar (cerrar) o reabrir su ticket resuelto, y cancelar el suyo.
            TicketStatus.Closed => Has(Permissions.TicketsClose) || (isRequester && ticket.Status == TicketStatus.Resolved),
            TicketStatus.InProgress when ticket.Status == TicketStatus.Resolved => IsAgent || isRequester,
            TicketStatus.Cancelled => IsAgent || isRequester,
            _ => IsAgent
        };

        if (!allowed)
        {
            throw new UnauthorizedAccessException($"No tiene permiso para pasar el ticket a {request.Status}.");
        }

        var history = ticket.ChangeStatus(request.Status, Me, currentUser.UserName, clock.UtcNow,
            Normalize(request.Comment), request.ResolutionNotes);

        await unitOfWork.Tickets.AddHistoryAsync(history, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<TicketDetailDto> AssignAsync(int id, AssignTicketRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await FindAsync(id, cancellationToken);

        if (await userDirectory.FindAsync(request.AssignedToId, cancellationToken) is not { IsActive: true })
        {
            throw new DomainValidationException("USER_NOT_AVAILABLE", "El usuario indicado no existe o esta inactivo.");
        }

        var assigneePermissions = await permissionService.GetPermissionsAsync(request.AssignedToId, cancellationToken);
        if (!assigneePermissions.Contains(Permissions.TicketsUpdate, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainValidationException("ASSIGNEE_NOT_AGENT", "El usuario indicado no puede atender tickets (requiere TICKETS.UPDATE).");
        }

        if (ticket.Assign(request.AssignedToId, Me, currentUser.UserName, clock.UtcNow) is { } history)
        {
            await unitOfWork.Tickets.AddHistoryAsync(history, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<TicketDetailDto> ApproveAsync(int id, ApprovalDecisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await FindAsync(id, cancellationToken);
        var now = clock.UtcNow;

        var history = ticket.Approve(Me, currentUser.UserName, now, Normalize(request.Comment));
        await ApplySlaAsync(ticket, now, cancellationToken);

        await unitOfWork.Tickets.AddHistoryAsync(history, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<TicketDetailDto> RejectAsync(int id, ApprovalDecisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await FindAsync(id, cancellationToken);
        var history = ticket.Reject(Me, currentUser.UserName, clock.UtcNow, request.Comment!.Trim());

        await unitOfWork.Tickets.AddHistoryAsync(history, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<TicketCommentDto>> GetCommentsAsync(int id, CancellationToken cancellationToken = default)
    {
        var ticket = await FindVisibleAsync(id, cancellationToken);
        return await unitOfWork.Tickets.GetCommentsAsync(id, CanViewAll(ticket.Type), cancellationToken);
    }

    public async Task<IReadOnlyList<TicketCommentDto>> AddCommentAsync(
        int id,
        AddTicketCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await FindVisibleAsync(id, cancellationToken);
        var isRequester = ticket.RequesterId == Me;

        // Supervisores y auditores ven todo pero no intervienen; los internos son solo de agentes.
        if ((!isRequester && !IsAgent) || (request.IsInternal && !IsAgent))
        {
            throw new UnauthorizedAccessException("No tiene permiso para comentar este ticket.");
        }

        if (!ticket.IsOpen && ticket.Status != TicketStatus.Resolved)
        {
            throw new ConflictException("TICKET_CLOSED", $"No se puede comentar un ticket en estado {ticket.Status}.");
        }

        var now = clock.UtcNow;
        await unitOfWork.Tickets.AddCommentAsync(new TicketComment
        {
            TicketId = id,
            UserId = Me,
            Content = request.Content.Trim(),
            IsInternal = request.IsInternal
        }, cancellationToken);

        if (!isRequester && !request.IsInternal)
        {
            ticket.RegisterFirstResponse(now);
        }

        // La respuesta del solicitante reactiva un ticket que esperaba por el.
        if (isRequester && ticket.Status == TicketStatus.WaitingUser)
        {
            var history = ticket.ChangeStatus(TicketStatus.InProgress, Me, currentUser.UserName, now, "Respuesta del solicitante");
            await unitOfWork.Tickets.AddHistoryAsync(history, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await unitOfWork.Tickets.GetCommentsAsync(id, CanViewAll(ticket.Type), cancellationToken);
    }

    public async Task<IReadOnlyList<TicketHistoryDto>> GetHistoryAsync(int id, CancellationToken cancellationToken = default)
    {
        await FindVisibleAsync(id, cancellationToken);
        return await unitOfWork.Tickets.GetHistoryAsync(id, cancellationToken);
    }

    private int Me => currentUser.UserId ?? throw new UnauthorizedAccessException("Operacion sin usuario autenticado.");

    private bool Has(string permission) => currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private bool IsAgent => Has(Permissions.TicketsUpdate);

    private bool CanViewAll(TicketType type) =>
        Has(type == TicketType.Incident ? Permissions.TicketsView : Permissions.RequestsView);

    private void Require(string permission)
    {
        if (!Has(permission))
        {
            throw new UnauthorizedAccessException($"Requiere el permiso {permission}.");
        }
    }

    private async Task<Ticket> FindAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.Tickets.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Ticket", id);

    /// <summary>Un ticket ajeno sin permiso de ver todos responde 404: no revela que existe.</summary>
    private async Task<Ticket> FindVisibleAsync(int id, CancellationToken cancellationToken)
    {
        var ticket = await FindAsync(id, cancellationToken);
        if (ticket.RequesterId != Me && !CanViewAll(ticket.Type))
        {
            throw new EntityNotFoundException("Ticket", id);
        }

        return ticket;
    }

    private async Task ValidateReferencesAsync(int? departmentId, int? assetId, CancellationToken cancellationToken)
    {
        if (departmentId is { } dep && !await unitOfWork.Departments.ExistsAsync(dep, cancellationToken))
        {
            throw new DomainValidationException("DEPARTMENT_NOT_FOUND", "El departamento indicado no existe.");
        }

        if (assetId is { } asset && !await unitOfWork.Assets.ExistsAsync(asset, cancellationToken))
        {
            throw new DomainValidationException("ASSET_NOT_FOUND", "El activo indicado no existe.");
        }
    }

    /// <summary>
    /// Elige la politica mas especifica y calcula los vencimientos en horario laboral. Sin politicas
    /// configuradas usa Sla.Default.* (lunes a viernes, 08:00-17:00).
    /// </summary>
    private async Task ApplySlaAsync(Ticket ticket, DateTime startUtc, CancellationToken cancellationToken)
    {
        var policies = await unitOfWork.SlaPolicies.GetActiveAsync(cancellationToken);
        var policy = SlaPolicySelector.Select(policies, ticket.CategoryId, ticket.Priority, ticket.Type, ticket.DepartmentId);

        int responseMinutes, resolutionMinutes;
        WorkSchedule schedule;
        if (policy is not null)
        {
            (responseMinutes, resolutionMinutes, schedule) = (policy.ResponseTimeMinutes, policy.ResolutionTimeMinutes, policy.Schedule);
        }
        else
        {
            responseMinutes = await settings.GetIntAsync("Sla.Default.ResponseMinutes", 240, cancellationToken);
            resolutionMinutes = await settings.GetIntAsync("Sla.Default.ResolutionMinutes", 1440, cancellationToken);
            schedule = new WorkSchedule(true, new TimeOnly(8, 0), new TimeOnly(17, 0), WorkSchedule.ParseDays("1,2,3,4,5"));
        }

        ticket.ApplySla(
            policy,
            BusinessHours.AddWorkingMinutes(startUtc, responseMinutes, schedule, clock.TimeZone),
            BusinessHours.AddWorkingMinutes(startUtc, resolutionMinutes, schedule, clock.TimeZone));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
