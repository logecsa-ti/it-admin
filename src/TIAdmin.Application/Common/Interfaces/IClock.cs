namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Domain.Common;

/// <summary>
/// Provee la hora actual en UTC. Permite testear sin depender del reloj del sistema.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }

    /// <summary>Fecha de negocio en la zona horaria de la organizacion (App:TimeZone).</summary>
    DateTime Today { get; }

    /// <summary>Zona horaria de la organizacion (App:TimeZone): horarios laborales del SLA.</summary>
    TimeZoneInfo TimeZone { get; }
}

/// <summary>
/// Abstrae el usuario autenticado actual para que Application no dependa de HttpContext.
/// </summary>
public interface ICurrentUserService
{
    int? UserId { get; }

    string? UserName { get; }

    string? IpAddress { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>User-Agent de la peticion actual, para la auditoria.</summary>
    string? UserAgent();

    /// <summary>CorrelationId de la peticion actual.</summary>
    string? CorrelationId { get; }

    bool IsAuthenticated { get; }
}
