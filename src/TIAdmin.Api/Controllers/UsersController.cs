namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Validators;
using Perms = TIAdmin.Application.Common.Constants.Permissions;

[ApiController]
[Route("api/v1/users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController(IUserManagementService users, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Perms.UsersView)]
    public async Task<ActionResult<ApiResponse<PagedResult<UserListItemDto>>>> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] bool? isActive,
        [FromQuery] int? departmentId,
        [FromQuery] int? locationId,
        [FromQuery] string? role,
        CancellationToken cancellationToken)
    {
        var page = await users.SearchAsync(query, new UserFilter(isActive, departmentId, locationId, role), cancellationToken);
        return Ok(ApiResponse<PagedResult<UserListItemDto>>.Ok(page));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Perms.UsersView)]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<UserDetailDto>.Ok(await users.GetAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Perms.UsersCreate)]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new CreateUserRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var user = await users.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ApiResponse<UserDetailDto>.Ok(user, "Usuario creado."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Perms.UsersUpdate)]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> Update(
        int id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new UpdateUserRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<UserDetailDto>.Ok(await users.UpdateAsync(id, request, cancellationToken)));
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = Perms.UsersDisable)]
    public async Task<ActionResult<ApiResponse>> Deactivate(int id, CancellationToken cancellationToken)
    {
        await users.SetActiveAsync(id, isActive: false, currentUser.UserId, cancellationToken);
        return Ok(ApiResponse.Ok("Usuario desactivado."));
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = Perms.UsersDisable)]
    public async Task<ActionResult<ApiResponse>> Activate(int id, CancellationToken cancellationToken)
    {
        await users.SetActiveAsync(id, isActive: true, currentUser.UserId, cancellationToken);
        return Ok(ApiResponse.Ok("Usuario activado."));
    }

    /// <summary>Reemplaza los roles del usuario. Asignar roles es escalar privilegios: exige ROLES.MANAGE.</summary>
    [HttpPut("{id:int}/roles")]
    [Authorize(Policy = Perms.RolesManage)]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> SetRoles(
        int id,
        [FromBody] AssignRolesRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new AssignRolesRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<UserDetailDto>.Ok(await users.SetRolesAsync(id, request.Roles, cancellationToken)));
    }

    /// <summary>Reemplaza los permisos directos (adicionales a los de sus roles). Exige ROLES.MANAGE.</summary>
    [HttpPut("{id:int}/permissions")]
    [Authorize(Policy = Perms.RolesManage)]
    public async Task<ActionResult<ApiResponse<UserDetailDto>>> SetPermissions(
        int id,
        [FromBody] AssignPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        if (await this.ValidateAsync(new AssignPermissionsRequestValidator(), request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return Ok(ApiResponse<UserDetailDto>.Ok(await users.SetDirectPermissionsAsync(id, request.Permissions, cancellationToken)));
    }
}
