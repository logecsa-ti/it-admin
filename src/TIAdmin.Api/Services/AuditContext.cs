namespace TIAdmin.Api.Services;

using TIAdmin.Api.Middleware;
using TIAdmin.Application.Common.Interfaces;

/// <summary>
/// Implementacion de IAuditContext basada en HttpContext.
/// Se registra con lifetime Scoped para compartir la peticion.
/// </summary>
public sealed class AuditContext(IHttpContextAccessor httpContextAccessor) : IAuditContext
{
    public string? CorrelationId =>
        httpContextAccessor.HttpContext?.GetCorrelationId();
}
