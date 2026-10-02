namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;
using TIAdmin.Domain.Enums;

/// <summary>
/// Parametro configurable del sistema (SPECS.md secciones 22, 25, 53).
/// Los valores por defecto viven en el seed; no se hardcodean en la logica de negocio.
/// </summary>
public class SystemConfiguration : AuditableEntity
{
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? DefaultValue { get; set; }

    public string Group { get; set; } = "General";

    public ConfigurationDataType DataType { get; set; } = ConfigurationDataType.String;

    public string? Description { get; set; }

    /// <summary>
    /// Si es true, el valor se expone al frontend (configuracion publica).
    /// </summary>
    public bool IsPublic { get; set; }

    public bool IsEditable { get; set; } = true;
}
