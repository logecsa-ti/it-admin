namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Documento adjunto a una entidad (ADR-006): en la base solo la metadata; el binario vive en
/// IFileStorage bajo <see cref="StoragePath"/>. La baja es logica y conserva el archivo.
/// </summary>
public class Document : AuditableSoftDeletableEntity
{
    public string FileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string MimeType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    /// <summary>SHA-256 en hexadecimal.</summary>
    public string? Checksum { get; set; }

    /// <summary>Asset, Ticket, Contract, License, ChangeRequest, Maintenance, PurchaseRequest, Vendor.</summary>
    public string EntityName { get; set; } = string.Empty;

    public int EntityId { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public int UploadedById { get; set; }
}

/// <summary>Notificacion interna para un usuario.</summary>
public class Notification : Entity
{
    public int UserId { get; set; }

    /// <summary>TicketAssigned, ContractExpiring, LicenseExpiring, ...</summary>
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Ruta relativa del frontend o de la API (p. ej. /tickets/15).</summary>
    public string? Link { get; set; }

    public string? EntityName { get; set; }

    public int? EntityId { get; set; }

    /// <summary>
    /// Clave de deduplicacion para notificaciones periodicas (p. ej. "contract:12:30"): un indice unico
    /// por usuario evita repetir la misma alerta en cada ejecucion del scheduler.
    /// </summary>
    public string? DedupKey { get; set; }

    public bool IsRead { get; private set; }

    public DateTime? ReadAt { get; private set; }

    public DateTime CreatedAt { get; set; }

    public void MarkRead(DateTime now)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAt = now;
    }
}

public enum ExportJobStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

/// <summary>Exportacion asincrona de un reporte grande; el archivo se guarda en IFileStorage.</summary>
public class ExportJob : Entity
{
    public int RequestedById { get; set; }

    public string ReportName { get; set; } = string.Empty;

    public string Format { get; set; } = "xlsx";

    /// <summary>Parametros del reporte serializados (JSON).</summary>
    public string? Parameters { get; set; }

    public ExportJobStatus Status { get; private set; } = ExportJobStatus.Pending;

    public string? FileName { get; private set; }

    public string? StoragePath { get; private set; }

    public int? RowCount { get; private set; }

    public string? Error { get; private set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; private set; }

    public void Start()
    {
        if (Status != ExportJobStatus.Pending)
        {
            throw new ConflictException("EXPORT_ALREADY_STARTED", "La exportacion ya fue procesada.");
        }

        Status = ExportJobStatus.Processing;
    }

    public void Complete(string fileName, string storagePath, int rowCount, DateTime now)
    {
        Status = ExportJobStatus.Completed;
        FileName = fileName;
        StoragePath = storagePath;
        RowCount = rowCount;
        CompletedAt = now;
    }

    public void Fail(string error, DateTime now)
    {
        Status = ExportJobStatus.Failed;
        Error = error.Length > 1000 ? error[..1000] : error;
        CompletedAt = now;
    }
}
