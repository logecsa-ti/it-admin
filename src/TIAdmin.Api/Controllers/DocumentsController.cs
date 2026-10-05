namespace TIAdmin.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Platform;

/// <summary>
/// Documentos adjuntos. Solo exige sesion: el servicio aplica los permisos de la entidad duena (ADR-034).
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Authorize]
[Produces("application/json")]
public sealed class DocumentsController(IDocumentService documents) : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentDto>>>> GetByEntity(
        [FromQuery] string entityName,
        [FromQuery] int entityId,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<DocumentDto>>.Ok(await documents.ListAsync(entityName, entityId, cancellationToken)));

    /// <summary>Carga multipart: <c>file</c>, <c>entityName</c>, <c>entityId</c>, opcional <c>description</c> y <c>category</c>.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<DocumentDto>>> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string entityName,
        [FromForm] int entityId,
        [FromForm] string? description,
        [FromForm] string? category,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(ApiResponse.Fail("Adjunte un archivo.", [new ApiError("FILE_REQUIRED", "Adjunte un archivo.")]));
        }

        // El tipo MIME se deriva de la extension, no del encabezado enviado por el cliente.
        var mimeType = ContentTypes.TryGetContentType(file.FileName, out var detected) ? detected : "application/octet-stream";

        await using var content = file.OpenReadStream();
        var document = await documents.UploadAsync(
            new UploadDocumentCommand(entityName, entityId, file.FileName, mimeType, file.Length, content, description, category),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<DocumentDto>.Ok(document, "Documento cargado."));
    }

    [HttpGet("{id:int}/download")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var content = await documents.DownloadAsync(id, cancellationToken);
        return File(content.Content, content.MimeType, content.FileName);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id, CancellationToken cancellationToken)
    {
        await documents.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Documento eliminado."));
    }
}
