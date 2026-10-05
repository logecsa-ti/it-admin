namespace TIAdmin.Application.Common.Models;

using TIAdmin.Domain.Entities;

public record DocumentDto(
    int Id,
    string FileName,
    string MimeType,
    long Size,
    string? Checksum,
    string EntityName,
    int EntityId,
    string? Description,
    string? Category,
    int UploadedById,
    string UploadedByName,
    DateTime UploadedAt);

/// <summary>Carga de un documento; el contenido llega como stream desde el controller (multipart).</summary>
public record UploadDocumentCommand(
    string EntityName,
    int EntityId,
    string FileName,
    string MimeType,
    long Length,
    Stream Content,
    string? Description,
    string? Category);

public record DocumentContent(string FileName, string MimeType, Stream Content);

public record NotificationDto(
    int Id,
    string Type,
    string Title,
    string Message,
    string? Link,
    string? EntityName,
    int? EntityId,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);

/// <summary>Tabla generica para exportar/importar (encabezados + filas de texto/valores).</summary>
public sealed record TabularData(string Title, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows);

public record ExportJobDto(
    int Id,
    string ReportName,
    string Format,
    ExportJobStatus Status,
    int? RowCount,
    string? Error,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? DownloadUrl);

/// <summary>
/// Resultado de una exportacion: el archivo listo (sincronica) o el trabajo encolado (asincronica,
/// cuando supera Exports.AsyncThreshold filas).
/// </summary>
public record ExportResult(DocumentContent? File, ExportJobDto? Job);

public record ImportError(int Row, string Field, string Message);

/// <summary>
/// Resultado de una importacion: si hay errores no se importa nada (todo o nada); con
/// <paramref name="DryRun"/> solo se valida.
/// </summary>
public record ImportResult(int TotalRows, int Imported, bool DryRun, IReadOnlyList<ImportError> Errors);
