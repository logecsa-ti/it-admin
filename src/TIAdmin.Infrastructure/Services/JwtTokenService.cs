namespace TIAdmin.Infrastructure.Services;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Infrastructure.Persistence;

/// <summary>
/// Emision y rotacion de tokens JWT con refresh tokens opacos persistidos como hash SHA-256.
/// Nunca se almacena el refresh token en claro (SPECS.md seccion 15.1).
/// </summary>
public sealed class JwtTokenService(
    TIAdminDbContext context,
    IOptions<JwtOptions> jwtOptions,
    IClock clock,
    ICurrentUserService currentUser) : ITokenService
{
    private readonly JwtOptions options = jwtOptions.Value;

    public async Task<TokenPair> CreateTokensAsync(
        AuthenticatedUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var tokens = IssuePair(user, clock.UtcNow);

        context.RefreshTokens.Add(BuildRefreshTokenEntity(user.Id, tokens.RefreshToken));
        await context.SaveChangesAsync(cancellationToken);

        return tokens;
    }

    public async Task<AuthenticatedUser?> ValidateRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var stored = await FindValidTokenAsync(refreshToken, cancellationToken);
        return stored is null ? null : await LoadUserAsync(stored.UserId, cancellationToken);
    }

    public async Task<(TokenPair Tokens, AuthenticatedUser User)?> RotateTokensAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var stored = await FindValidTokenAsync(refreshToken, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var user = await LoadUserAsync(stored.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var now = clock.UtcNow;
        var tokens = IssuePair(user, now);

        // Rotacion: el token usado deja de ser valido y se crea el sustituto
        // en el mismo SaveChanges para que no exista una ventana sin token valido.
        stored.RevokedAt = now;
        stored.RevokedReason = "Rotated";

        context.RefreshTokens.Add(BuildRefreshTokenEntity(user.Id, tokens.RefreshToken));
        await context.SaveChangesAsync(cancellationToken);

        return (tokens, user);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = await FindAsync(refreshToken, cancellationToken);
        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = clock.UtcNow;
        stored.RevokedReason = "Logout";
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var active = await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        if (active.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        foreach (var token in active)
        {
            token.RevokedAt = now;
            token.RevokedReason = "RevokedAll";
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private TokenPair IssuePair(AuthenticatedUser user, DateTime now)
    {
        var accessExpires = now.AddMinutes(options.AccessTokenMinutes);
        var refreshExpires = now.AddDays(options.RefreshTokenDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new(TiClaimTypes.CorrelationId, currentUser.CorrelationId ?? Guid.NewGuid().ToString("N"))
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(user.Permissions.Select(permission => new Claim(TiClaimTypes.Permission, permission)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var accessToken = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: credentials);

        return new TokenPair(
            new JwtSecurityTokenHandler().WriteToken(accessToken),
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            accessExpires,
            refreshExpires);
    }

    private RefreshToken BuildRefreshTokenEntity(int userId, string refreshToken) => new()
    {
        UserId = userId,
        TokenHash = HashToken(refreshToken),
        ExpiresAt = clock.UtcNow.AddDays(options.RefreshTokenDays),
        CreatedAt = clock.UtcNow,
        IpAddress = Truncate(currentUser.IpAddress, 45),
        UserAgent = Truncate(currentUser.UserAgent(), 500)
    };

    private Task<RefreshToken?> FindAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Task.FromResult<RefreshToken?>(null);
        }

        return context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == HashToken(refreshToken), cancellationToken);
    }

    private async Task<RefreshToken?> FindValidTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var stored = await FindAsync(refreshToken, cancellationToken);

        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= clock.UtcNow)
        {
            return null;
        }

        return stored;
    }

    /// <summary>
    /// Carga el usuario y sus permisos efectivos. ApplicationUser pertenece a Infrastructure,
    /// por eso se consulta explicitamente en lugar de navegar desde la entidad de dominio.
    /// </summary>
    private async Task<AuthenticatedUser?> LoadUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roleIds = context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId);

        var permissions = await context.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission.Code)
            .Concat(context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => rp.Permission.Code))
            .Distinct()
            .ToListAsync(cancellationToken);

        var roles = await context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(context.Roles, ur => ur.RoleId, role => role.Id, (ur, role) => role.Name!)
            .ToListAsync(cancellationToken);

        return new AuthenticatedUser(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.FirstName ?? string.Empty,
            user.LastName ?? string.Empty,
            roles,
            permissions);
    }

    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
