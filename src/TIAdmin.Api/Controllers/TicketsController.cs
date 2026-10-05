namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.HelpDesk;
using TIAdmin.Application.Validators;
using TIAdmin.Domain.Enums;
using Perms = TIAdmin.Application.Common.Constants.Permissions;

/// <summary>
/// Help Desk (incidentes y solicitudes, Q-07). Las rutas sin politica solo exigen sesion: el servicio
/// aplica la visibilidad (ADR-026) y los permisos que dependen del tipo de ticket o del solicitante.
/// </summary>
[ApiController]
[Route("api/v1/tickets")]
[Authorize]
[Produces("application/json")]
public sealed class TicketsController(ITicketService tickets) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TicketListItemDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] TicketType? type,
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] int? categoryId,
        [FromQuery] int? assignedToId,
        [FromQuery] int? requesterId,
        [FromQuery] int? departmentId,
        [FromQuery] bool? overdue,
        [FromQuery] bool? mine,
        [FromQuery] bool? assignedToMe,
        CancellationToken cancellationToken)
    {
        var filter = new TicketFilter(type, status, priority, categoryId, assignedToId, requesterId, departmentId, overdue, mine, assignedToMe);
        return Ok(ApiResponse<PagedResult<TicketListItemDto>>.Ok(await tickets.SearchAsync(query, filter, cancellationToken)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<TicketDetailDto>.Ok(await tickets.GetAsync(id, cancellationToken)));

    /// <summary>Incidente: TICKETS.CREATE. Solicitud: REQUESTS.CREATE. El tipo lo define la categoria.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Create([FromBody] CreateTicketRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CreateTicketRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var ticket = await tickets.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ApiResponse<TicketDetailDto>.Ok(ticket, $"Ticket {ticket.TicketNumber} creado."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.TicketsUpdate)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Update(int id, [FromBody] UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new UpdateTicketRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<TicketDetailDto>.Ok(await tickets.UpdateAsync(id, request, cancellationToken)));
    }

    /// <summary>
    /// Resolver: TICKETS.RESOLVE. Cerrar: TICKETS.CLOSE (o el solicitante si esta resuelto).
    /// Cancelar: agente o solicitante. Reabrir un resuelto: agente o solicitante. Resto: TICKETS.UPDATE.
    /// </summary>
    [HttpPost("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> ChangeStatus(
        int id,
        [FromBody] ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new ChangeTicketStatusRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<TicketDetailDto>.Ok(await tickets.ChangeStatusAsync(id, request, cancellationToken)));
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Policy = Perms.TicketsAssign)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Assign(int id, [FromBody] AssignTicketRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new AssignTicketRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<TicketDetailDto>.Ok(await tickets.AssignAsync(id, request, cancellationToken), "Ticket asignado."));
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Perms.RequestsApprove)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Approve(int id, [FromBody] ApprovalDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<TicketDetailDto>.Ok(await tickets.ApproveAsync(id, request, cancellationToken), "Solicitud aprobada."));

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Perms.RequestsApprove)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Reject(int id, [FromBody] ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new RejectRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<TicketDetailDto>.Ok(await tickets.RejectAsync(id, request, cancellationToken), "Solicitud rechazada."));
    }

    /// <summary>Los comentarios internos no se devuelven a quien solo accede como solicitante.</summary>
    [HttpGet("{id:int}/comments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TicketCommentDto>>>> GetComments(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<TicketCommentDto>>.Ok(await tickets.GetCommentsAsync(id, cancellationToken)));

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TicketCommentDto>>>> AddComment(
        int id,
        [FromBody] AddTicketCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new AddTicketCommentRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<IReadOnlyList<TicketCommentDto>>.Ok(await tickets.AddCommentAsync(id, request, cancellationToken)));
    }

    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TicketHistoryDto>>>> GetHistory(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<TicketHistoryDto>>.Ok(await tickets.GetHistoryAsync(id, cancellationToken)));
}

[ApiController]
[Route("api/v1/ticket-categories")]
[Authorize]
[Produces("application/json")]
public sealed class TicketCategoriesController(IHelpDeskConfigService config) : ControllerBase
{
    /// <summary>Cualquier usuario autenticado la necesita para crear tickets.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TicketCategoryDto>>>> GetAll([FromQuery] bool? isActive, CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<TicketCategoryDto>>.Ok(await config.GetCategoriesAsync(isActive, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.ConfigurationManage)]
    public async Task<ActionResult<ApiResponse<TicketCategoryDto>>> Create([FromBody] TicketCategoryRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new TicketCategoryRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var category = await config.CreateCategoryAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<TicketCategoryDto>.Ok(category, "Categoria creada."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.ConfigurationManage)]
    public async Task<ActionResult<ApiResponse<TicketCategoryDto>>> Update(int id, [FromBody] TicketCategoryRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new TicketCategoryRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<TicketCategoryDto>.Ok(await config.UpdateCategoryAsync(id, request, cancellationToken)));
    }
}

[ApiController]
[Route("api/v1/sla-policies")]
[Authorize]
[Produces("application/json")]
public sealed class SlaPoliciesController(IHelpDeskConfigService config) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.TicketsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SlaPolicyDto>>>> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<SlaPolicyDto>>.Ok(await config.GetSlaPoliciesAsync(cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.ConfigurationManage)]
    public async Task<ActionResult<ApiResponse<SlaPolicyDto>>> Create([FromBody] SlaPolicyRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new SlaPolicyRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var policy = await config.CreateSlaPolicyAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<SlaPolicyDto>.Ok(policy, "Politica de SLA creada."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.ConfigurationManage)]
    public async Task<ActionResult<ApiResponse<SlaPolicyDto>>> Update(int id, [FromBody] SlaPolicyRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new SlaPolicyRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<SlaPolicyDto>.Ok(await config.UpdateSlaPolicyAsync(id, request, cancellationToken)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Perms.ConfigurationManage)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        await config.DeleteSlaPolicyAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Politica de SLA eliminada."));
    }
}
