namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;

/// <summary>
/// Refresh tokens almacenados como hash (nunca en texto plano) para poder revocarlos.
/// Permite cumplir la revocacion de sesiones de SPECS.md seccion 17.
/// </summary>
public class RefreshToken : Entity
{
    public int UserId { get; set; }

    /// <summary>
    /// SHA-256 en base64 del token. Es la unica forma persistida.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>
    /// Al rotar el refresh token se encadena con el anterior, de modo que un token
    /// ya usado detecta el robo de credenciales (rotacion de token).
    /// </summary>
    public int? ReplacedByTokenId { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
