namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;

/// <summary>
/// Almacenamiento de archivos (ADR-006): local en desarrollo, Blob/S3 a futuro. Las rutas son relativas
/// a la raiz del almacenamiento y nunca pueden salir de ella.
/// </summary>
public interface IFileStorage
{
    /// <summary>Guarda el contenido y devuelve su tamano y SHA-256 (hex).</summary>
    Task<StoredFile> SaveAsync(Stream content, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Null si el archivo no existe.</summary>
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Verifica que la raiz exista y admita escritura (health check).</summary>
    Task<bool> CanWriteAsync(CancellationToken cancellationToken = default);
}

public record StoredFile(string RelativePath, long Size, string Sha256);

/// <summary>Correo saliente. En desarrollo (Email:Enabled=false) solo se registra en el log.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Cola en memoria: el envio ocurre en segundo plano y un fallo SMTP no afecta la operacion de negocio.</summary>
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);
}

public record EmailMessage(IReadOnlyList<string> To, string Subject, string HtmlBody);

/// <summary>Cola de exportaciones asincronas (ids de ExportJob).</summary>
public interface IExportQueue
{
    void Enqueue(int exportJobId);
}

/// <summary>Escritura de tablas a CSV o Excel.</summary>
public interface ITabularFileWriter
{
    /// <summary>"csv" o "xlsx".</summary>
    bool Supports(string format);

    Task WriteAsync(TabularData data, string format, Stream output, CancellationToken cancellationToken = default);
}

/// <summary>Lectura de CSV o Excel (primera hoja); la primera fila son encabezados.</summary>
public interface ITabularFileReader
{
    Task<TabularData> ReadAsync(Stream input, string format, CancellationToken cancellationToken = default);
}

public interface IDocumentRepository : IRepository<Document>
{
    Task<IReadOnlyList<DocumentDto>> GetByEntityAsync(string entityName, int entityId, CancellationToken cancellationToken = default);

    Task<DocumentDto?> GetDtoAsync(int id, CancellationToken cancellationToken = default);
}

public interface INotificationRepository
{
    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);

    /// <summary>Claves de deduplicacion ya notificadas a esos usuarios (entre las indicadas).</summary>
    Task<HashSet<(int UserId, string DedupKey)>> GetExistingDedupKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    Task<PagedResult<NotificationDto>> GetForUserAsync(int userId, bool unreadOnly, PagedQuery query, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Con seguimiento de cambios.</summary>
    Task<Notification?> GetForUserAsync(int userId, int notificationId, CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(int userId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Deja de seguir notificaciones que no pudieron guardarse (no deben reintentarse en el siguiente SaveChanges).</summary>
    void Discard(IEnumerable<Notification> notifications);
}

public interface IExportJobRepository
{
    Task AddAsync(ExportJob job, CancellationToken cancellationToken = default);

    /// <summary>Con seguimiento de cambios.</summary>
    Task<ExportJob?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Trabajos pendientes (para reencolarlos al reiniciar la aplicacion).</summary>
    Task<IReadOnlyList<int>> GetPendingIdsAsync(CancellationToken cancellationToken = default);
}
