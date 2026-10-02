namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Application.Common.Models;

public sealed record AuthenticatedUser(
    int Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, DateTime RefreshTokenExpiresAt);

/// <summary>
/// Emision de tokens JWT. Vive en Infrastructure porque depende de la libreria de JWT.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Emite un par de tokens y persiste el refresh token hasheado en un solo SaveChanges.
    /// </summary>
    Task<TokenPair> CreateTokensAsync(AuthenticatedUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida el refresh token y devuelve el usuario vigente, o null si es invalido/expirado/revocado.
    /// </summary>
    Task<AuthenticatedUser?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rota el refresh token: valida el actual, lo revoca y emite un par nuevo de forma atomica.
    /// Devuelve null si el refresh token no es valido.
    /// </summary>
    Task<(TokenPair Tokens, AuthenticatedUser User)?> RotateTokensAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca un refresh token (logout). Idempotente.
    /// </summary>
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca todos los refresh tokens de un usuario.
    /// </summary>
    Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resuelve los permisos efectivos de un usuario: union de los permisos de sus roles
/// y los permisos asignados directamente (SPECS.md seccion 15.2).
/// </summary>
public interface IPermissionService
{
    Task<IReadOnlyCollection<string>> GetPermissionsAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetRolesAsync(int userId, CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> GetUserAsync(int userId, CancellationToken cancellationToken = default);
}
