namespace TIAdmin.Application.Common.Interfaces;

/// <summary>
/// Expone el CorrelationId de la peticion actual para poder persistirlo en la auditoria.
/// Implementado en la API para no acoplar Application a HttpContext.
/// </summary>
public interface IAuditContext
{
    string? CorrelationId { get; }
}
