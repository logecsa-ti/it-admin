namespace TIAdmin.Application.Platform;

using Microsoft.Extensions.Options;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Documentos adjuntos (SPECS.md seccion 39, ADR-034). El acceso sigue a la entidad duena:
/// ver exige el permiso de lectura de la entidad; cargar/eliminar, el de gestion de la entidad
/// o DOCUMENTS.MANAGE (con lectura de la entidad). El solicitante de un ticket ve y adjunta en el suyo.
/// </summary>
public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> ListAsync(string entityName, int entityId, CancellationToken cancellationToken = default);

    Task<DocumentDto> UploadAsync(UploadDocumentCommand command, CancellationToken cancellationToken = default);

    Task<DocumentContent> DownloadAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Baja logica: el archivo se conserva como evidencia historica.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class DocumentService(
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    ICurrentUserService currentUser,
    IOptions<StorageOptions> storageOptions,
    IClock clock)
    : IDocumentService
{
    private sealed record EntityRule(string View, string[] Manage, Func<IUnitOfWork, int, CancellationToken, Task<bool>> Exists);

    /// <summary>Entidades que admiten adjuntos (los tickets tienen una regla propia por solicitante).</summary>
    private static readonly Dictionary<string, EntityRule> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Asset"] = new(Permissions.AssetsView, [Permissions.AssetsUpdate], (u, id, ct) => u.Assets.ExistsAsync(id, ct)),
        ["Contract"] = new(Permissions.ContractsView, [Permissions.ContractsManage], (u, id, ct) => u.Contracts.ExistsAsync(id, ct)),
        ["License"] = new(Permissions.LicensesView, [Permissions.LicensesManage], (u, id, ct) => u.Licenses.ExistsAsync(id, ct)),
        ["ChangeRequest"] = new(Permissions.ChangesView, [Permissions.ChangesCreate, Permissions.ChangesManage], (u, id, ct) => u.ChangeRequests.ExistsAsync(id, ct)),
        ["Maintenance"] = new(Permissions.MaintenanceView, [Permissions.MaintenanceManage], (u, id, ct) => u.Maintenances.ExistsAsync(id, ct)),
        ["PurchaseRequest"] = new(Permissions.PurchasesView, [Permissions.PurchasesCreate, Permissions.PurchasesManage], (u, id, ct) => u.PurchaseRequests.ExistsAsync(id, ct)),
        ["Vendor"] = new(Permissions.VendorsView, [Permissions.VendorsManage], (u, id, ct) => u.Vendors.ExistsAsync(id, ct))
    };

    public static IReadOnlyCollection<string> SupportedEntities => [.. Rules.Keys, "Ticket"];

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(string entityName, int entityId, CancellationToken cancellationToken = default)
    {
        var canonical = await EnsureAccessAsync(entityName, entityId, manage: false, cancellationToken);
        return await unitOfWork.Documents.GetByEntityAsync(canonical, entityId, cancellationToken);
    }

    public async Task<DocumentDto> UploadAsync(UploadDocumentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var canonical = await EnsureAccessAsync(command.EntityName, command.EntityId, manage: true, cancellationToken);
        var fileName = SanitizeFileName(command.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var options = storageOptions.Value;

        if (!options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainValidationException("FILE_TYPE_NOT_ALLOWED",
                $"Tipo de archivo no permitido ({extension}). Permitidos: {string.Join(", ", options.AllowedExtensions)}.");
        }

        if (command.Length <= 0)
        {
            throw new DomainValidationException("FILE_EMPTY", "El archivo esta vacio.");
        }

        if (command.Length > options.MaxFileSizeBytes)
        {
            throw new DomainValidationException("FILE_TOO_LARGE",
                $"El archivo excede el maximo de {options.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        // Nombre en disco generado: nunca se usa el nombre del cliente como ruta.
        var now = clock.UtcNow;
        var path = $"{canonical.ToLowerInvariant()}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var stored = await storage.SaveAsync(command.Content, path, cancellationToken);

        if (stored.Size > options.MaxFileSizeBytes)
        {
            await storage.DeleteAsync(path, cancellationToken);
            throw new DomainValidationException("FILE_TOO_LARGE", "El archivo excede el tamano maximo permitido.");
        }

        var document = new Document
        {
            FileName = fileName,
            StoragePath = stored.RelativePath,
            MimeType = string.IsNullOrWhiteSpace(command.MimeType) ? "application/octet-stream" : command.MimeType,
            Size = stored.Size,
            Checksum = stored.Sha256,
            EntityName = canonical,
            EntityId = command.EntityId,
            Description = Normalize(command.Description),
            Category = Normalize(command.Category),
            UploadedById = Me
        };

        try
        {
            await unitOfWork.Documents.AddAsync(document, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Sin metadata no debe quedar un archivo huerfano.
            await storage.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        return (await unitOfWork.Documents.GetDtoAsync(document.Id, cancellationToken))!;
    }

    public async Task<DocumentContent> DownloadAsync(int id, CancellationToken cancellationToken = default)
    {
        var document = await FindAsync(id, cancellationToken);
        await EnsureAccessAsync(document.EntityName, document.EntityId, manage: false, cancellationToken);

        var stream = await storage.OpenReadAsync(document.StoragePath, cancellationToken)
            ?? throw new EntityNotFoundException($"archivo del documento {id}");

        return new DocumentContent(document.FileName, document.MimeType, stream);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var document = await FindAsync(id, cancellationToken);

        // Quien lo cargo puede retirarlo mientras siga viendo la entidad; el documento de otro exige gestion
        // (en tickets, el solicitante no puede borrar lo que adjunto un agente).
        var isUploader = document.UploadedById == Me;
        await EnsureAccessAsync(document.EntityName, document.EntityId, manage: !isUploader, cancellationToken, othersDocument: !isUploader);

        unitOfWork.Documents.Delete(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private int Me => currentUser.UserId ?? throw new UnauthorizedAccessException("Operacion sin usuario autenticado.");

    private bool Has(string permission) => currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private async Task<Document> FindAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.Documents.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Document", id);

    /// <summary>Valida la entidad y el acceso; devuelve el nombre canonico. Sin acceso de lectura responde 404.</summary>
    private async Task<string> EnsureAccessAsync(
        string entityName,
        int entityId,
        bool manage,
        CancellationToken cancellationToken,
        bool othersDocument = false)
    {
        if (string.Equals(entityName, "Ticket", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureTicketAccessAsync(entityId, manage, othersDocument, cancellationToken);
            return "Ticket";
        }

        if (!Rules.TryGetValue(entityName, out var rule))
        {
            throw new DomainValidationException("ENTITY_NOT_SUPPORTED",
                $"No se admiten documentos para {entityName}. Entidades: {string.Join(", ", SupportedEntities)}.");
        }

        var canonical = Rules.Keys.First(k => string.Equals(k, entityName, StringComparison.OrdinalIgnoreCase));

        if (!Has(rule.View))
        {
            throw new UnauthorizedAccessException($"Requiere el permiso {rule.View}.");
        }

        if (!await rule.Exists(unitOfWork, entityId, cancellationToken))
        {
            throw new EntityNotFoundException(canonical, entityId);
        }

        if (manage && !rule.Manage.Any(Has) && !Has(Permissions.DocumentsManage))
        {
            throw new UnauthorizedAccessException("No tiene permiso para gestionar documentos de esta entidad.");
        }

        return canonical;
    }

    private async Task EnsureTicketAccessAsync(int ticketId, bool manage, bool othersDocument, CancellationToken cancellationToken)
    {
        var ticket = await unitOfWork.Tickets.GetByIdAsync(ticketId, cancellationToken) ?? throw new EntityNotFoundException("Ticket", ticketId);
        var isRequester = ticket.RequesterId == Me;
        var canViewAll = Has(ticket.Type == TicketType.Incident ? Permissions.TicketsView : Permissions.RequestsView);

        // Igual que en TicketService (ADR-026): un ticket ajeno sin permiso de ver todos no existe.
        if (!isRequester && !canViewAll)
        {
            throw new EntityNotFoundException("Ticket", ticketId);
        }

        // El solicitante adjunta en su ticket, pero no gestiona documentos de otros.
        var requesterMayManage = isRequester && !othersDocument;
        if (manage && !requesterMayManage && !Has(Permissions.TicketsUpdate) && !Has(Permissions.DocumentsManage))
        {
            throw new UnauthorizedAccessException("No tiene permiso para adjuntar documentos a este ticket.");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.'))
        {
            throw new DomainValidationException("INVALID_FILE_NAME", "El nombre del archivo no es valido.");
        }

        return name.Length > 300 ? name[^300..] : name;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
