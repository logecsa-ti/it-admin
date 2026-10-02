using TIAdmin.Domain.Common;

namespace TIAdmin.Application.Common.Interfaces;

/// <summary>
/// Provee la hora actual en UTC. Permite testear sin depender del reloj del sistema.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }

    DateTime Today { get; }
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

    bool IsAuthenticated { get; }
}
