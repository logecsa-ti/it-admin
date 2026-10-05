namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TIAdmin.Api.Filters;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Validators;
using TIAdmin.Infrastructure.Identity;
using TIAdmin.Infrastructure.Services;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly ITokenService tokenService;
    private readonly IPermissionService permissionService;
    private readonly ICurrentUserService currentUser;
    private readonly IClock clock;
    private readonly JwtOptions jwtOptions;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IPermissionService permissionService,
        ICurrentUserService currentUser,
        IClock clock,
        IOptions<JwtOptions> jwtOptions)
    {
        this.signInManager = signInManager;
        this.userManager = userManager;
        this.tokenService = tokenService;
        this.permissionService = permissionService;
        this.currentUser = currentUser;
        this.clock = clock;
        this.jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// Inicia sesion y devuelve el par de tokens junto con el perfil del usuario.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ValidatableRequest]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var validator = new LoginRequestValidator();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<LoginResponse>.Fail(
                "Los datos de acceso no son validos.",
                validation.Errors.Select(e => new ApiError("VALIDATION_ERROR", e.ErrorMessage))));
        }

        // Mensaje generico: no revela si el usuario existe o la contrasena es incorrecta.
        var invalidCredentials = ApiResponse<LoginResponse>.Fail("Usuario o contrasena incorrectos.", [
            new ApiError("INVALID_CREDENTIALS", "Usuario o contrasena incorrectos.")
        ]);

        var user = await userManager.FindByNameAsync(request.UserName);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(invalidCredentials);
        }

        // CheckPasswordSignInAsync aplica lockout sin emitir la cookie de Identity:
        // la API es stateless y solo autentica con JWT.
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return Unauthorized(ApiResponse<LoginResponse>.Fail("La cuenta esta bloqueada temporalmente.", [
                new ApiError("ACCOUNT_LOCKED", "La cuenta esta bloqueada temporalmente.")
            ]));
        }

        if (!result.Succeeded)
        {
            return Unauthorized(invalidCredentials);
        }

        var authenticated = new AuthenticatedUser(
            user.Id,
            user.UserName!,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            await permissionService.GetRolesAsync(user.Id, cancellationToken),
            await permissionService.GetPermissionsAsync(user.Id, cancellationToken));

        var tokens = await tokenService.CreateTokensAsync(authenticated, cancellationToken);

        user.LastLoginAt = clock.UtcNow;
        await userManager.UpdateAsync(user);

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessTokenExpiresAt,
            tokens.RefreshTokenExpiresAt,
            await BuildProfileAsync(authenticated, cancellationToken)),
            "Inicio de sesion exitoso."));
    }

    /// <summary>
    /// Renueva el access token usando un refresh token valido.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ValidatableRequest]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var validator = new RefreshTokenRequestValidator();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<LoginResponse>.Fail("Refresh token invalido.", [
                new ApiError("VALIDATION_ERROR", "Refresh token invalido.")
            ]));
        }

        var rotated = await tokenService.RotateTokensAsync(request.RefreshToken, cancellationToken);
        if (rotated is null)
        {
            return Unauthorized(ApiResponse<LoginResponse>.Fail("Refresh token invalido o expirado.", [
                new ApiError("INVALID_REFRESH_TOKEN", "Refresh token invalido o expirado.")
            ]));
        }

        var (tokens, authenticated) = rotated.Value;

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessTokenExpiresAt,
            tokens.RefreshTokenExpiresAt,
            await BuildProfileAsync(authenticated, cancellationToken))));
    }

    /// <summary>
    /// Cierra la sesion revocando el refresh token actual.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ValidatableRequest]
    public async Task<ActionResult<ApiResponse>> Logout(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        return Ok(ApiResponse.Ok("Sesion cerrada."));
    }

    /// <summary>
    /// Perfil del usuario autenticado, con roles y permisos efectivos.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> Me(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized(ApiResponse<UserProfileResponse>.Fail("Sesion no valida."));
        }

        var user = await permissionService.GetUserAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Unauthorized(ApiResponse<UserProfileResponse>.Fail("Sesion no valida."));
        }

        return Ok(ApiResponse<UserProfileResponse>.Ok(
            await BuildProfileAsync(user, cancellationToken)));
    }

    /// <summary>
    /// Cambia la contrasena del usuario autenticado.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ValidatableRequest]
    public async Task<ActionResult<ApiResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var validator = new ChangePasswordRequestValidator();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse.Fail("La contrasena no cumple los requisitos.",
                validation.Errors
                    .Select(e => new ApiError("VALIDATION_ERROR", e.ErrorMessage))
                    .ToArray()));
        }

        var userId = currentUser.UserId;
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("Sesion no valida."));
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Unauthorized(ApiResponse.Fail("Sesion no valida."));
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse.Fail("No fue posible cambiar la contrasena.",
                result.Errors
                    .Select(e => new ApiError("CHANGE_PASSWORD_FAILED", e.Description))
                    .ToArray()));
        }

        // Cambiar la contrasena invalida el resto de las sesiones.
        await tokenService.RevokeAllForUserAsync(user.Id, cancellationToken);

        return Ok(ApiResponse.Ok("Contrasena actualizada."));
    }

    private async Task<UserProfileResponse> BuildProfileAsync(
        AuthenticatedUser user,
        CancellationToken cancellationToken)
    {
        var entity = await userManager.FindByIdAsync(user.Id.ToString());

        return new UserProfileResponse(
            user.Id,
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            $"{user.FirstName} {user.LastName}".Trim(),
            entity?.IsActive ?? false,
            entity?.LastLoginAt,
            user.Roles,
            user.Permissions);
    }
}
