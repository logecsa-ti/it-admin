namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Licensing;
using TIAdmin.Application.Validators;
using TIAdmin.Application.Vendors;
using TIAdmin.Domain.Enums;
using Perms = TIAdmin.Application.Common.Constants.Permissions;

[ApiController]
[Route("api/v1/software")]
[Authorize]
[Produces("application/json")]
public sealed class SoftwareController(ISoftwareService software) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.SoftwareView)]
    public async Task<ActionResult<ApiResponse<PagedResult<SoftwareDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] bool? isActive,
        [FromQuery] string? category,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<SoftwareDto>>.Ok(
            await software.SearchAsync(query, new SoftwareFilter(isActive, category), cancellationToken)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.SoftwareView)]
    public async Task<ActionResult<ApiResponse<SoftwareDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SoftwareDto>.Ok(await software.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.SoftwareManage)]
    public async Task<ActionResult<ApiResponse<SoftwareDto>>> Create([FromBody] SoftwareRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new SoftwareRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var created = await software.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse<SoftwareDto>.Ok(created, "Software creado."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.SoftwareManage)]
    public async Task<ActionResult<ApiResponse<SoftwareDto>>> Update(int id, [FromBody] SoftwareRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new SoftwareRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<SoftwareDto>.Ok(await software.UpdateAsync(id, request, cancellationToken)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Perms.SoftwareManage)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        await software.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Software eliminado."));
    }
}

[ApiController]
[Route("api/v1/licenses")]
[Authorize]
[Produces("application/json")]
public sealed class LicensesController(ILicenseService licenses) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.LicensesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<LicenseDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] int? softwareId,
        [FromQuery] int? vendorId,
        [FromQuery] LicenseType? licenseType,
        [FromQuery] bool? isActive,
        [FromQuery] DateOnly? expiresBefore,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<LicenseDto>>.Ok(await licenses.SearchAsync(
            query, new LicenseFilter(softwareId, vendorId, licenseType, isActive, expiresBefore), cancellationToken)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.LicensesView)]
    public async Task<ActionResult<ApiResponse<LicenseDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LicenseDto>.Ok(await licenses.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.LicensesManage)]
    public async Task<ActionResult<ApiResponse<LicenseDto>>> Create([FromBody] CreateLicenseRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CreateLicenseRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var license = await licenses.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = license.Id }, ApiResponse<LicenseDto>.Ok(license, "Licencia creada."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.LicensesManage)]
    public async Task<ActionResult<ApiResponse<LicenseDto>>> Update(int id, [FromBody] UpdateLicenseRequest request, CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new UpdateLicenseRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<LicenseDto>.Ok(await licenses.UpdateAsync(id, request, cancellationToken)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Perms.LicensesManage)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        await licenses.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Licencia eliminada."));
    }

    /// <summary>Revela la clave descifrada. Exige LICENSES.MANAGE y queda registrado en la auditoria.</summary>
    [HttpGet("{id:int}/key")]
    [Authorize(Policy = Perms.LicensesManage)]
    public async Task<ActionResult<ApiResponse<LicenseKeyDto>>> GetKey(int id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResponse<LicenseKeyDto>.Ok(await licenses.RevealKeyAsync(id, cancellationToken)));
    }

    [HttpGet("{id:int}/installations")]
    [Authorize(Policy = Perms.LicensesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<InstallationDto>>>> GetInstallations(
        int id,
        [FromQuery] PagedQuery query,
        [FromQuery] bool activeOnly,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<InstallationDto>>.Ok(await licenses.GetInstallationsAsync(id, activeOnly, query, cancellationToken)));

    [HttpPost("{id:int}/installations")]
    [Authorize(Policy = Perms.LicensesManage)]
    public async Task<ActionResult<ApiResponse<InstallationDto>>> Install(
        int id,
        [FromBody] InstallLicenseRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new InstallLicenseRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<InstallationDto>.Ok(await licenses.InstallAsync(id, request, cancellationToken), "Licencia instalada."));
    }

    [HttpDelete("{id:int}/installations/{installationId:int}")]
    [Authorize(Policy = Perms.LicensesManage)]
    public async Task<ActionResult<ApiResponse>> Uninstall(int id, int installationId, CancellationToken cancellationToken)
    {
        await licenses.UninstallAsync(id, installationId, cancellationToken);
        return Ok(ApiResponse.Ok("Licencia desinstalada."));
    }
}

/// <summary>Alertas calculadas al vuelo con los umbrales de SystemConfigurations (Alerts.*).</summary>
[ApiController]
[Route("api/v1/alerts")]
[Authorize]
[Produces("application/json")]
public sealed class AlertsController(ILicenseService licenses, IContractService contracts) : ControllerBase
{
    [HttpGet("licenses")]
    [Authorize(Policy = Perms.LicensesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LicenseAlertDto>>>> Licenses(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<LicenseAlertDto>>.Ok(await licenses.GetAlertsAsync(cancellationToken)));

    [HttpGet("contracts")]
    [Authorize(Policy = Perms.ContractsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ContractAlertDto>>>> Contracts(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<ContractAlertDto>>.Ok(await contracts.GetAlertsAsync(cancellationToken)));
}
