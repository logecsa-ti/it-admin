namespace TIAdmin.Application.Licensing;

using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Exceptions;
using SoftwareEntity = TIAdmin.Domain.Entities.Software;

/// <summary>Catalogo de software (SPECS.md seccion 23).</summary>
public interface ISoftwareService
{
    Task<PagedResult<SoftwareDto>> SearchAsync(PagedQuery query, SoftwareFilter filter, CancellationToken cancellationToken = default);

    Task<SoftwareDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<SoftwareDto> CreateAsync(SoftwareRequest request, CancellationToken cancellationToken = default);

    Task<SoftwareDto> UpdateAsync(int id, SoftwareRequest request, CancellationToken cancellationToken = default);

    /// <summary>Baja logica. No se permite con licencias registradas (desactivar en su lugar).</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class SoftwareService(IUnitOfWork unitOfWork) : ISoftwareService
{
    public Task<PagedResult<SoftwareDto>> SearchAsync(PagedQuery query, SoftwareFilter filter, CancellationToken cancellationToken = default) =>
        unitOfWork.Software.SearchAsync(query, filter, cancellationToken);

    public async Task<SoftwareDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await unitOfWork.Software.GetDtoAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Software", id);

    public async Task<SoftwareDto> CreateAsync(SoftwareRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var software = new SoftwareEntity();
        Apply(software, request);
        await EnsureUniqueAsync(software, null, cancellationToken);

        await unitOfWork.Software.AddAsync(software, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(software.Id, cancellationToken);
    }

    public async Task<SoftwareDto> UpdateAsync(int id, SoftwareRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var software = await FindAsync(id, cancellationToken);
        Apply(software, request);
        await EnsureUniqueAsync(software, id, cancellationToken);

        unitOfWork.Software.Update(software);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var software = await FindAsync(id, cancellationToken);
        if (await unitOfWork.Software.HasLicensesAsync(id, cancellationToken))
        {
            throw new ConflictException("SOFTWARE_HAS_LICENSES",
                "No se puede eliminar un software con licencias registradas; desactivelo en su lugar.");
        }

        unitOfWork.Software.Delete(software);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<SoftwareEntity> FindAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.Software.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Software", id);

    private async Task EnsureUniqueAsync(SoftwareEntity software, int? excludeId, CancellationToken cancellationToken)
    {
        if (await unitOfWork.Software.ExistsNameVersionAsync(software.Name, software.Version, excludeId, cancellationToken))
        {
            var label = software.Version is null ? software.Name : $"{software.Name} {software.Version}";
            throw new ConflictException("SOFTWARE_ALREADY_EXISTS", $"El software {label} ya existe en el catalogo.");
        }
    }

    private static void Apply(SoftwareEntity software, SoftwareRequest request)
    {
        software.Name = request.Name.Trim();
        software.Version = Normalize(request.Version);
        software.Publisher = Normalize(request.Publisher);
        software.Category = Normalize(request.Category);
        software.Description = Normalize(request.Description);
        software.IsActive = request.IsActive;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
