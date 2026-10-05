namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Operations;
using TIAdmin.Application.Validators;
using TIAdmin.Domain.Enums;
using Perms = TIAdmin.Application.Common.Constants.Permissions;

[ApiController]
[Route("api/v1/maintenances")]
[Authorize]
[Produces("application/json")]
public sealed class MaintenancesController(IMaintenanceService maintenances) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.MaintenanceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<MaintenanceDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] MaintenanceType? type,
        [FromQuery] MaintenanceStatus? status,
        [FromQuery] int? assetId,
        [FromQuery] int? technicianId,
        [FromQuery] int? vendorId,
        [FromQuery] DateTime? scheduledFrom,
        [FromQuery] DateTime? scheduledTo,
        [FromQuery] bool? overdue,
        CancellationToken cancellationToken)
    {
        var filter = new MaintenanceFilter(type, status, assetId, technicianId, vendorId, scheduledFrom, scheduledTo, overdue);
        return Ok(ApiResponse<PagedResult<MaintenanceDto>>.Ok(await maintenances.SearchAsync(query, filter, cancellationToken)));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.MaintenanceView)]
    public async Task<ActionResult<ApiResponse<MaintenanceDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<MaintenanceDto>.Ok(await maintenances.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.MaintenanceManage)]
    public async Task<ActionResult<ApiResponse<MaintenanceDto>>> Create([FromBody] MaintenanceRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new MaintenanceRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var created = await maintenances.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse<MaintenanceDto>.Ok(created, $"Mantenimiento {created.Number} creado."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.MaintenanceManage)]
    public async Task<ActionResult<ApiResponse<MaintenanceDto>>> Update(int id, [FromBody] MaintenanceRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new MaintenanceRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<MaintenanceDto>.Ok(await maintenances.UpdateAsync(id, request, cancellationToken)));
    }

    [HttpPost("{id:int}/schedule")]
    [Authorize(Policy = Perms.MaintenanceManage)]
    public async Task<ActionResult<ApiResponse<MaintenanceDto>>> Schedule(int id, [FromBody] ScheduleRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<MaintenanceDto>.Ok(await maintenances.ScheduleAsync(id, request.ScheduledDate, cancellationToken)));

    /// <summary>Si el activo estaba disponible pasa a Maintenance.</summary>
    [HttpPost("{id:int}/start")]
    [Authorize(Policy = Perms.MaintenanceManage)]
    public async Task<ActionResult<ApiResponse<MaintenanceDto>>> Start(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<MaintenanceDto>.Ok(await maintenances.StartAsync(id, cancellationToken)));

    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = Perms.MaintenanceManage)]
    public async Task<ActionResult<ApiResponse<MaintenanceDto>>> Complete(int id, [FromBody] CompleteMaintenanceRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CompleteMaintenanceRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<MaintenanceDto>.Ok(await maintenances.CompleteAsync(id, request, cancellationToken)));
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Perms.MaintenanceManage)]
    public async Task<ActionResult<ApiResponse<MaintenanceDto>>> Cancel(int id, [FromBody] CancelRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CancelRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<MaintenanceDto>.Ok(await maintenances.CancelAsync(id, request.Reason, cancellationToken)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Perms.MaintenanceManage)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        await maintenances.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Mantenimiento eliminado."));
    }

    public sealed record ScheduleRequest(DateTime ScheduledDate);
}

[ApiController]
[Route("api/v1/changes")]
[Authorize]
[Produces("application/json")]
public sealed class ChangesController(IChangeService changes) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.ChangesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ChangeRequestDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] ChangeType? type,
        [FromQuery] ChangeStatus? status,
        [FromQuery] ChangeRisk? risk,
        [FromQuery] int? requestedById,
        [FromQuery] int? assignedToId,
        [FromQuery] bool? assignedToMe,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<ChangeRequestDto>>.Ok(await changes.SearchAsync(
            query, new ChangeFilter(type, status, risk, requestedById, assignedToId, assignedToMe), cancellationToken)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.ChangesView)]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.ChangesCreate)]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> Create([FromBody] ChangeRequestBody request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new ChangeRequestBodyValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var created = await changes.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse<ChangeRequestDto>.Ok(created, $"Cambio {created.Number} creado."));
    }

    /// <summary>Solo en borrador, por el solicitante o CHANGES.MANAGE.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.ChangesCreate)]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> Update(int id, [FromBody] ChangeRequestBody request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new ChangeRequestBodyValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.UpdateAsync(id, request, cancellationToken)));
    }

    /// <summary>Un cambio estandar queda aprobado; normal y de emergencia requieren plan de rollback.</summary>
    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = Perms.ChangesCreate)]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> Submit(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.SubmitAsync(id, cancellationToken)));

    [HttpPost("{id:int}/review")]
    [Authorize(Policy = Perms.ChangesReview)]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> StartReview(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.StartReviewAsync(id, cancellationToken)));

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Perms.ChangesReview)]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> Approve(int id, [FromBody] ReviewDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.ApproveAsync(id, request.Comment, cancellationToken), "Cambio aprobado."));

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Perms.ChangesReview)]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> Reject(int id, [FromBody] CancelRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CancelRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.RejectAsync(id, request.Reason, cancellationToken), "Cambio rechazado."));
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Policy = Perms.ChangesManage)]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> Assign(int id, [FromBody] AssignUserRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new AssignUserRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.AssignAsync(id, request.UserId, cancellationToken)));
    }

    /// <summary>Implementacion: el responsable asignado o CHANGES.MANAGE.</summary>
    [HttpPost("{id:int}/start")]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> Start(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.StartImplementationAsync(id, cancellationToken)));

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> Complete(int id, [FromBody] ImplementationNotesRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.CompleteAsync(id, request.Notes, cancellationToken)));

    [HttpPost("{id:int}/rollback")]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> RollBack(int id, [FromBody] CancelRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CancelRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.RollBackAsync(id, request.Reason, cancellationToken)));
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = Perms.ChangesManage)]
    public async Task<ActionResult<ApiResponse<ChangeRequestDto>>> Close(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ChangeRequestDto>.Ok(await changes.CloseAsync(id, cancellationToken)));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Perms.ChangesCreate)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        await changes.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Cambio eliminado."));
    }
}

[ApiController]
[Route("api/v1/purchases")]
[Authorize]
[Produces("application/json")]
public sealed class PurchasesController(IPurchaseService purchases) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.PurchasesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<PurchaseRequestDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] PurchaseStatus? status,
        [FromQuery] int? requestedById,
        [FromQuery] int? departmentId,
        [FromQuery] int? vendorId,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<PurchaseRequestDto>>.Ok(await purchases.SearchAsync(
            query, new PurchaseFilter(status, requestedById, departmentId, vendorId), cancellationToken)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.PurchasesView)]
    public async Task<ActionResult<ApiResponse<PurchaseRequestDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PurchaseRequestDto>.Ok(await purchases.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.PurchasesCreate)]
    public async Task<ActionResult<ApiResponse<PurchaseRequestDto>>> Create([FromBody] PurchaseRequestBody request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new PurchaseRequestBodyValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var created = await purchases.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse<PurchaseRequestDto>.Ok(created, $"Solicitud {created.Number} creada."));
    }

    /// <summary>Solo en borrador, por el solicitante o PURCHASES.MANAGE. Las partidas se reemplazan.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.PurchasesCreate)]
    public async Task<ActionResult<ApiResponse<PurchaseRequestDto>>> Update(int id, [FromBody] PurchaseRequestBody request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new PurchaseRequestBodyValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<PurchaseRequestDto>.Ok(await purchases.UpdateAsync(id, request, cancellationToken)));
    }

    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = Perms.PurchasesCreate)]
    public async Task<ActionResult<ApiResponse<PurchaseRequestDto>>> Submit(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PurchaseRequestDto>.Ok(await purchases.SubmitAsync(id, cancellationToken)));

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Perms.PurchasesApprove)]
    public async Task<ActionResult<ApiResponse<PurchaseRequestDto>>> Approve(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PurchaseRequestDto>.Ok(await purchases.ApproveAsync(id, cancellationToken), "Compra aprobada."));

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Perms.PurchasesApprove)]
    public async Task<ActionResult<ApiResponse<PurchaseRequestDto>>> Reject(int id, [FromBody] CancelRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CancelRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<PurchaseRequestDto>.Ok(await purchases.RejectAsync(id, request.Reason, cancellationToken), "Compra rechazada."));
    }

    [HttpPost("{id:int}/order")]
    [Authorize(Policy = Perms.PurchasesManage)]
    public async Task<ActionResult<ApiResponse<PurchaseRequestDto>>> Order(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PurchaseRequestDto>.Ok(await purchases.MarkOrderedAsync(id, cancellationToken)));

    [HttpPost("{id:int}/receive")]
    [Authorize(Policy = Perms.PurchasesManage)]
    public async Task<ActionResult<ApiResponse<PurchaseRequestDto>>> Receive(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PurchaseRequestDto>.Ok(await purchases.MarkReceivedAsync(id, cancellationToken)));

    /// <summary>El solicitante cancela hasta el envio a aprobacion; despues, PURCHASES.MANAGE.</summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Perms.PurchasesCreate)]
    public async Task<ActionResult<ApiResponse<PurchaseRequestDto>>> Cancel(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PurchaseRequestDto>.Ok(await purchases.CancelAsync(id, cancellationToken)));
}
