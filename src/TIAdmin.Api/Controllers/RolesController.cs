namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Validators;
using Perms = TIAdmin.Application.Common.Constants.Permissions;

[ApiController]
[Route("api/v1/roles")]
[Authorize]
[Produces("application/json")]
public sealed class RolesController(IRoleManagementService roles) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.RolesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleDto>>>> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<RoleDto>>.Ok(await roles.GetAllAsync(cancellationToken)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.RolesView)]
    public async Task<ActionResult<ApiResponse<RoleDetailDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<RoleDetailDto>.Ok(await roles.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.RolesManage)]
    public async Task<ActionResult<ApiResponse<RoleDetailDto>>> Create(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await new CreateRoleRequestValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse.Fail("Los datos proporcionados no son validos.",
                validation.Errors.Select(e => new ApiError("VALIDATION_ERROR", e.ErrorMessage)).ToArray()));
        }

        var role = await roles.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, ApiResponse<RoleDetailDto>.Ok(role, "Rol creado."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.RolesManage)]
    public async Task<ActionResult<ApiResponse<RoleDetailDto>>> Update(
        int id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await new UpdateRoleRequestValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse.Fail("Los datos proporcionados no son validos.",
                validation.Errors.Select(e => new ApiError("VALIDATION_ERROR", e.ErrorMessage)).ToArray()));
        }

        return Ok(ApiResponse<RoleDetailDto>.Ok(await roles.UpdateAsync(id, request, cancellationToken)));
    }

    /// <summary>Reemplaza el conjunto completo de permisos del rol.</summary>
    [HttpPut("{id:int}/permissions")]
    [Authorize(Policy = Perms.RolesManage)]
    public async Task<ActionResult<ApiResponse<RoleDetailDto>>> SetPermissions(
        int id,
        [FromBody] AssignPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await new AssignPermissionsRequestValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse.Fail("Los datos proporcionados no son validos.",
                validation.Errors.Select(e => new ApiError("VALIDATION_ERROR", e.ErrorMessage)).ToArray()));
        }

        return Ok(ApiResponse<RoleDetailDto>.Ok(await roles.SetPermissionsAsync(id, request.Permissions, cancellationToken)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Perms.RolesManage)]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        await roles.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Rol eliminado."));
    }
}

[ApiController]
[Route("api/v1/permissions")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionsController(IRoleManagementService roles) : ControllerBase
{
    /// <summary>Catalogo de permisos disponibles para construir roles.</summary>
    [HttpGet]
    [Authorize(Policy = Perms.RolesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PermissionDto>>>> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<PermissionDto>>.Ok(await roles.GetPermissionCatalogAsync(cancellationToken)));
}
