namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Validators;
using TIAdmin.Application.Vendors;
using TIAdmin.Domain.Enums;
using Perms = TIAdmin.Application.Common.Constants.Permissions;

[ApiController]
[Route("api/v1/vendors")]
[Authorize]
[Produces("application/json")]
public sealed class VendorsController(IVendorService vendors) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.VendorsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<VendorDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] VendorStatus? status,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<VendorDto>>.Ok(await vendors.SearchAsync(query, new VendorFilter(status), cancellationToken)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.VendorsView)]
    public async Task<ActionResult<ApiResponse<VendorDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<VendorDto>.Ok(await vendors.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.VendorsManage)]
    public async Task<ActionResult<ApiResponse<VendorDto>>> Create([FromBody] VendorRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new VendorRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var vendor = await vendors.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = vendor.Id }, ApiResponse<VendorDto>.Ok(vendor, "Proveedor creado."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.VendorsManage)]
    public async Task<ActionResult<ApiResponse<VendorDto>>> Update(int id, [FromBody] VendorRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new VendorRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<VendorDto>.Ok(await vendors.UpdateAsync(id, request, cancellationToken)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Perms.VendorsManage)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        await vendors.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Proveedor eliminado."));
    }
}

[ApiController]
[Route("api/v1/contracts")]
[Authorize]
[Produces("application/json")]
public sealed class ContractsController(IContractService contracts) : ControllerBase
{
    /// <summary><c>status</c> filtra por estado efectivo: Active, Expiring, Expired, Draft, Terminated, Renewed.</summary>
    [HttpGet]
    [Authorize(Policy = Perms.ContractsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ContractDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] int? vendorId,
        [FromQuery] ContractType? type,
        [FromQuery] ContractStatus? status,
        [FromQuery] DateOnly? endsBefore,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<ContractDto>>.Ok(
            await contracts.SearchAsync(query, new ContractFilter(vendorId, type, status, endsBefore), cancellationToken)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.ContractsView)]
    public async Task<ActionResult<ApiResponse<ContractDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ContractDto>.Ok(await contracts.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.ContractsManage)]
    public async Task<ActionResult<ApiResponse<ContractDto>>> Create([FromBody] CreateContractRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CreateContractRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var contract = await contracts.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = contract.Id }, ApiResponse<ContractDto>.Ok(contract, "Contrato creado."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.ContractsManage)]
    public async Task<ActionResult<ApiResponse<ContractDto>>> Update(int id, [FromBody] UpdateContractRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new UpdateContractRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<ContractDto>.Ok(await contracts.UpdateAsync(id, request, cancellationToken)));
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = Perms.ContractsManage)]
    public async Task<ActionResult<ApiResponse<ContractDto>>> Activate(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ContractDto>.Ok(await contracts.ActivateAsync(id, cancellationToken), "Contrato activado."));

    [HttpPost("{id:int}/terminate")]
    [Authorize(Policy = Perms.ContractsManage)]
    public async Task<ActionResult<ApiResponse<ContractDto>>> Terminate(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ContractDto>.Ok(await contracts.TerminateAsync(id, cancellationToken), "Contrato terminado."));

    /// <summary>Crea el contrato sucesor; el actual queda como Renewed. Devuelve el sucesor.</summary>
    [HttpPost("{id:int}/renew")]
    [Authorize(Policy = Perms.ContractsManage)]
    public async Task<ActionResult<ApiResponse<ContractDto>>> Renew(int id, [FromBody] RenewContractRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new RenewContractRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var successor = await contracts.RenewAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = successor.Id }, ApiResponse<ContractDto>.Ok(successor, "Contrato renovado."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Perms.ContractsManage)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        await contracts.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Contrato eliminado."));
    }
}
