namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;
using TIAdmin.Domain.Enums;

/// <summary>
/// Bitacora de auditoria (SPECS.md seccion 18).
/// Es de solo lectura para los usuarios: no existe endpoint para modificarla.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public int? UserId { get; set; }

    /// <summary>
    /// Nombre del usuario en el momento de la operacion (snapshot inmutable,
    /// por si el usuario se renombra o se elimina después).
    /// </summary>
    public string? UserName { get; set; }

    public AuditAction Action { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Estado anterior en formato JSON. Null cuando la entidad es nueva.
    /// </summary>
    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Identificador de correlacion que permite unir la auditoria con los logs
    /// y el error reportado al usuario final (SPECS.md seccion 43).
    /// </summary>
    public string? CorrelationId { get; set; }

    public string Module { get; set; } = string.Empty;

    public bool IsError { get; set; }
}
