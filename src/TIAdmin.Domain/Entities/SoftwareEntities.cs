namespace TIAdmin.Domain.Entities;

using TIAdmin.Domain.Common;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>Producto del catalogo de software (SPECS.md seccion 23).</summary>
public class Software : AuditableSoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string? Publisher { get; set; }

    public string? Category { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Licencia adquirida. <see cref="UsedQuantity"/> refleja las instalaciones activas y nunca
/// supera <see cref="Quantity"/> (sin sobreasignacion). <see cref="LicenseKey"/> guarda el
/// valor cifrado: nunca se registra en logs ni en la auditoria.
/// </summary>
public class SoftwareLicense : AuditableSoftDeletableEntity
{
    public int SoftwareId { get; set; }

    public int? VendorId { get; set; }

    public int? ContractId { get; set; }

    public string Name { get; set; } = string.Empty;

    public LicenseType LicenseType { get; set; }

    /// <summary>Clave cifrada (ISecretProtector). Null si la licencia no tiene clave.</summary>
    public string? LicenseKey { get; set; }

    public int Quantity { get; private set; }

    public int UsedQuantity { get; private set; }

    public DateOnly? PurchaseDate { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    public decimal? Cost { get; set; }

    public DateOnly? SupportEndDate { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public Software? Software { get; set; }

    public Vendor? Vendor { get; set; }

    public Contract? Contract { get; set; }

    public int AvailableQuantity => Quantity - UsedQuantity;

    public bool IsExpired(DateOnly today) => ExpirationDate is { } expiration && expiration < today;

    public void SetQuantity(int quantity)
    {
        if (quantity < 0)
        {
            throw new DomainValidationException("INVALID_LICENSE_QUANTITY", "La cantidad de licencias no puede ser negativa.");
        }

        if (quantity < UsedQuantity)
        {
            throw new ConflictException("LICENSE_QUANTITY_BELOW_USAGE",
                $"La licencia tiene {UsedQuantity} instalaciones activas; desinstale antes de reducir la cantidad a {quantity}.");
        }

        Quantity = quantity;
    }

    /// <summary>
    /// Ocupa un puesto de la licencia en un activo. <paramref name="today"/> es la fecha de negocio
    /// (zona de la organizacion) contra la que se evalua el vencimiento.
    /// </summary>
    public SoftwareInstallation Install(int assetId, int? userId, DateTime now, DateOnly today)
    {
        if (!IsActive)
        {
            throw new ConflictException("LICENSE_INACTIVE", "La licencia esta inactiva.");
        }

        if (IsExpired(today))
        {
            throw new ConflictException("LICENSE_EXPIRED", "La licencia esta vencida.");
        }

        if (UsedQuantity >= Quantity)
        {
            throw new ConflictException("LICENSE_EXHAUSTED",
                $"No hay puestos disponibles ({UsedQuantity}/{Quantity} en uso).");
        }

        UsedQuantity++;
        return new SoftwareInstallation
        {
            LicenseId = Id,
            License = this,
            AssetId = assetId,
            UserId = userId,
            InstalledAt = now,
            IsActive = true
        };
    }

    /// <summary>Libera el puesto. La instalacion queda en el historial con su fecha de baja.</summary>
    public void Uninstall(SoftwareInstallation installation, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(installation);

        if (!installation.IsActive || installation.LicenseId != Id)
        {
            throw new ConflictException("INSTALLATION_NOT_ACTIVE", "La instalacion no esta activa en esta licencia.");
        }

        installation.Close(now);
        UsedQuantity--;
    }

    public void EnsureCanBeDeleted()
    {
        if (UsedQuantity > 0)
        {
            throw new ConflictException("LICENSE_IN_USE", "No se puede eliminar una licencia con instalaciones activas.");
        }
    }
}

/// <summary>Puesto de licencia ocupado por un activo. Una vez desinstalado no vuelve a cambiar.</summary>
public class SoftwareInstallation : AuditableEntity
{
    public int LicenseId { get; set; }

    public int AssetId { get; set; }

    public int? UserId { get; set; }

    public DateTime InstalledAt { get; set; }

    public DateTime? UninstalledAt { get; private set; }

    public bool IsActive { get; set; } = true;

    public SoftwareLicense? License { get; set; }

    public Asset? Asset { get; set; }

    internal void Close(DateTime now)
    {
        IsActive = false;
        UninstalledAt = now;
    }
}
