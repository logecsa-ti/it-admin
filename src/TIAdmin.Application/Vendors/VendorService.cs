namespace TIAdmin.Application.Vendors;

using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Exceptions;

/// <summary>Proveedores (SPECS.md seccion 24).</summary>
public interface IVendorService
{
    Task<PagedResult<VendorDto>> SearchAsync(PagedQuery query, VendorFilter filter, CancellationToken cancellationToken = default);

    Task<VendorDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<VendorDto> CreateAsync(VendorRequest request, CancellationToken cancellationToken = default);

    Task<VendorDto> UpdateAsync(int id, VendorRequest request, CancellationToken cancellationToken = default);

    /// <summary>Baja logica. No se permite con contratos abiertos (Draft o Active vigentes).</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class VendorService(IUnitOfWork unitOfWork, IClock clock) : IVendorService
{
    public Task<PagedResult<VendorDto>> SearchAsync(PagedQuery query, VendorFilter filter, CancellationToken cancellationToken = default) =>
        unitOfWork.Vendors.SearchAsync(query, filter, cancellationToken);

    public async Task<VendorDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        ToDto(await FindAsync(id, cancellationToken));

    public async Task<VendorDto> CreateAsync(VendorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, null, cancellationToken);

        var vendor = new Vendor();
        Apply(vendor, request);

        await unitOfWork.Vendors.AddAsync(vendor, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(vendor);
    }

    public async Task<VendorDto> UpdateAsync(int id, VendorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vendor = await FindAsync(id, cancellationToken);
        await EnsureNameAvailableAsync(request.Name.Trim(), id, cancellationToken);

        Apply(vendor, request);
        unitOfWork.Vendors.Update(vendor);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(vendor);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var vendor = await FindAsync(id, cancellationToken);

        if (await unitOfWork.Vendors.HasOpenContractsAsync(id, DateOnly.FromDateTime(clock.Today), cancellationToken))
        {
            throw new ConflictException("VENDOR_HAS_OPEN_CONTRACTS",
                "No se puede eliminar un proveedor con contratos vigentes o en borrador.");
        }

        unitOfWork.Vendors.Delete(vendor);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Vendor> FindAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.Vendors.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Vendor", id);

    private async Task EnsureNameAvailableAsync(string name, int? excludeId, CancellationToken cancellationToken)
    {
        if (await unitOfWork.Vendors.ExistsNameAsync(name, excludeId, cancellationToken))
        {
            throw new ConflictException("VENDOR_NAME_ALREADY_EXISTS", $"Ya existe un proveedor llamado {name}.");
        }
    }

    private static void Apply(Vendor vendor, VendorRequest request)
    {
        vendor.Code = Normalize(request.Code)?.ToUpperInvariant();
        vendor.Name = request.Name.Trim();
        vendor.TaxId = Normalize(request.TaxId);
        vendor.ContactName = Normalize(request.ContactName);
        vendor.Email = Normalize(request.Email);
        vendor.Phone = Normalize(request.Phone);
        vendor.Address = Normalize(request.Address);
        vendor.City = Normalize(request.City);
        vendor.Country = Normalize(request.Country);
        vendor.Website = Normalize(request.Website);
        vendor.Status = request.Status;
        vendor.Notes = Normalize(request.Notes);
        vendor.Rating = request.Rating;
    }

    internal static VendorDto ToDto(Vendor v) => new(
        v.Id, v.Code, v.Name, v.TaxId, v.ContactName, v.Email, v.Phone, v.Address, v.City, v.Country,
        v.Website, v.Status, v.Notes, v.Rating);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
