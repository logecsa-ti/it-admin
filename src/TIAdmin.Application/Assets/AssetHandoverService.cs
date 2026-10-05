namespace TIAdmin.Application.Assets;

using Microsoft.Extensions.Logging;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Exceptions;

public enum HandoverKind
{
    /// <summary>Acta de entrega: TI entrega el equipo al usuario al asignarlo.</summary>
    Delivery,

    /// <summary>Acta de devolucion: el usuario devuelve el equipo a TI.</summary>
    Return
}

/// <summary>Contenido de un acta de entrega o devolucion; las fechas ya vienen en la zona de negocio.</summary>
public sealed record HandoverActa(
    HandoverKind Kind,
    string Number,
    string OrganizationName,
    DateTime Date,
    string AssetCode,
    string AssetName,
    string AssetType,
    string? Brand,
    string? Model,
    string? SerialNumber,
    string? Location,
    string? Department,
    string HolderName,
    string? HolderEmail,
    string DeliveredBy,
    string ReceivedBy,
    string? Condition,
    string? Notes,
    string? Clause,
    DateTime GeneratedAt);

/// <summary>Genera el PDF de un acta. Implementacion en Infrastructure (QuestPDF).</summary>
public interface IHandoverDocumentRenderer
{
    byte[] Render(HandoverActa acta);
}

/// <summary>
/// Actas de entrega y devolucion de equipos (ADR-040). Se archivan como documento del activo al asignar
/// y al devolver; tambien se pueden descargar despues para cualquier asignacion.
/// </summary>
public interface IAssetHandoverService
{
    /// <summary>
    /// Genera el acta y la adjunta al activo. Nunca lanza: la asignacion ya esta guardada y un fallo del PDF
    /// o del almacenamiento no debe revertirla (el acta se puede descargar despues con <see cref="GetAsync"/>).
    /// </summary>
    Task ArchiveAsync(int assignmentId, HandoverKind kind, CancellationToken cancellationToken = default);

    /// <summary>La copia archivada si existe; si no (asignaciones anteriores a esta funcion), la genera al vuelo.</summary>
    Task<DocumentContent> GetAsync(int assetId, int assignmentId, HandoverKind kind, CancellationToken cancellationToken = default);
}

public sealed class AssetHandoverService(
    IUnitOfWork unitOfWork,
    IUserDirectory userDirectory,
    ICurrentUserService currentUser,
    IFileStorage storage,
    ISystemSettings settings,
    IHandoverDocumentRenderer renderer,
    IClock clock,
    ILogger<AssetHandoverService> logger)
    : IAssetHandoverService
{
    public const string DeliveryClauseKey = "Assets.Handover.DeliveryClause";
    public const string ReturnClauseKey = "Assets.Handover.ReturnClause";
    public const string DeliveryCategory = "ActaEntrega";
    public const string ReturnCategory = "ActaDevolucion";
    private const string PdfMimeType = "application/pdf";

    public async Task ArchiveAsync(int assignmentId, HandoverKind kind, CancellationToken cancellationToken = default)
    {
        string? path = null;
        try
        {
            var (assignment, acta) = await BuildAsync(assignmentId, kind, cancellationToken);
            var content = renderer.Render(acta);

            var now = clock.UtcNow;
            path = $"asset/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.pdf";
            using var stream = new MemoryStream(content, writable: false);
            var stored = await storage.SaveAsync(stream, path, cancellationToken);

            await unitOfWork.Documents.AddAsync(new Document
            {
                FileName = FileName(acta),
                StoragePath = stored.RelativePath,
                MimeType = PdfMimeType,
                Size = stored.Size,
                Checksum = stored.Sha256,
                EntityName = "Asset",
                EntityId = assignment.AssetId,
                Description = $"{Title(kind)} {acta.Number}",
                Category = Category(kind),
                UploadedById = currentUser.UserId ?? assignment.AssignedById
            }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "No se pudo archivar el acta {Kind} de la asignacion {AssignmentId}.", kind, assignmentId);
            if (path is not null)
            {
                await DeleteOrphanAsync(path);
            }
        }
    }

    public async Task<DocumentContent> GetAsync(int assetId, int assignmentId, HandoverKind kind, CancellationToken cancellationToken = default)
    {
        var (assignment, acta) = await BuildAsync(assignmentId, kind, cancellationToken);
        if (assignment.AssetId != assetId)
        {
            throw new EntityNotFoundException("AssetAssignment", assignmentId);
        }

        var fileName = FileName(acta);
        var archived = (await unitOfWork.Documents.GetByEntityAsync("Asset", assetId, cancellationToken))
            .FirstOrDefault(d => d.Category == Category(kind) && d.FileName == fileName);
        if (archived is not null && await unitOfWork.Documents.GetByIdAsync(archived.Id, cancellationToken) is { } document
            && await storage.OpenReadAsync(document.StoragePath, cancellationToken) is { } stream)
        {
            return new DocumentContent(fileName, PdfMimeType, stream);
        }

        return new DocumentContent(fileName, PdfMimeType, new MemoryStream(renderer.Render(acta), writable: false));
    }

    /// <summary>Sin metadata no debe quedar un archivo huerfano; si tampoco se puede borrar, solo se registra.</summary>
    private async Task DeleteOrphanAsync(string path)
    {
        try
        {
            await storage.DeleteAsync(path, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "No se pudo eliminar el archivo huerfano {Path}.", path);
        }
    }

    private async Task<(AssetAssignment Assignment, HandoverActa Acta)> BuildAsync(
        int assignmentId,
        HandoverKind kind,
        CancellationToken cancellationToken)
    {
        var assignment = await unitOfWork.AssetAssignments.GetByIdAsync(assignmentId, cancellationToken)
            ?? throw new EntityNotFoundException("AssetAssignment", assignmentId);
        if (kind == HandoverKind.Return && assignment.ReturnDate is null)
        {
            throw new ConflictException("ASSIGNMENT_NOT_RETURNED", "La asignacion sigue vigente: no tiene acta de devolucion.");
        }

        var asset = await unitOfWork.Assets.GetDetailAsync(assignment.AssetId, cancellationToken)
            ?? throw new EntityNotFoundException("Asset", assignment.AssetId);
        var holder = await userDirectory.FindAsync(assignment.UserId, cancellationToken);
        var assignedBy = await userDirectory.FindAsync(assignment.AssignedById, cancellationToken);
        var returnedBy = assignment.ReturnedById is { } id ? await userDirectory.FindAsync(id, cancellationToken) : null;

        var holderName = NameOf(holder, assignment.UserId);
        var utcDate = kind == HandoverKind.Delivery ? assignment.AssignmentDate : assignment.ReturnDate!.Value;
        var date = TimeZoneInfo.ConvertTimeFromUtc(utcDate, clock.TimeZone);
        var prefix = kind == HandoverKind.Delivery ? "ENT" : "DEV";

        var acta = new HandoverActa(
            kind,
            $"{prefix}-{date:yyyy}-{assignment.Id:000000}",
            await settings.GetStringAsync("App.Name", "TI Admin", cancellationToken),
            date,
            asset.AssetCode,
            asset.Name,
            asset.AssetTypeName,
            asset.Brand,
            asset.Model,
            asset.SerialNumber,
            asset.LocationName,
            asset.DepartmentName,
            holderName,
            holder?.Email,
            kind == HandoverKind.Delivery ? NameOf(assignedBy, assignment.AssignedById) : holderName,
            kind == HandoverKind.Delivery ? holderName : NameOf(returnedBy, assignment.ReturnedById ?? 0),
            kind == HandoverKind.Delivery ? assignment.ConditionAtAssignment : assignment.ConditionAtReturn,
            kind == HandoverKind.Delivery ? assignment.Notes : null,
            await ClauseAsync(kind, cancellationToken),
            TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, clock.TimeZone));

        return (assignment, acta);
    }

    private async Task<string?> ClauseAsync(HandoverKind kind, CancellationToken cancellationToken)
    {
        var clause = await settings.GetStringAsync(kind == HandoverKind.Delivery ? DeliveryClauseKey : ReturnClauseKey, string.Empty, cancellationToken);
        return string.IsNullOrWhiteSpace(clause) ? null : clause.Trim();
    }

    private static string NameOf(UserReference? user, int id) =>
        user is null ? $"Usuario #{id}" : string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;

    private static string Title(HandoverKind kind) => kind == HandoverKind.Delivery ? "Acta de entrega" : "Acta de devolucion";

    private static string Category(HandoverKind kind) => kind == HandoverKind.Delivery ? DeliveryCategory : ReturnCategory;

    private static string FileName(HandoverActa acta) =>
        $"Acta-{(acta.Kind == HandoverKind.Delivery ? "entrega" : "devolucion")}-{acta.Number}.pdf";
}
