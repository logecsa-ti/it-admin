namespace TIAdmin.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TIAdmin.Domain.Entities;
using TIAdmin.Infrastructure.Identity;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Vendors", t => t.HasCheckConstraint("CK_Vendors_Rating", "[Rating] IS NULL OR ([Rating] >= 0 AND [Rating] <= 5)"));
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Code).HasMaxLength(20);
        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.TaxId).HasMaxLength(50);
        builder.Property(v => v.ContactName).HasMaxLength(150);
        builder.Property(v => v.Email).HasMaxLength(200);
        builder.Property(v => v.Phone).HasMaxLength(30);
        builder.Property(v => v.Address).HasMaxLength(300);
        builder.Property(v => v.City).HasMaxLength(100);
        builder.Property(v => v.Country).HasMaxLength(100);
        builder.Property(v => v.Website).HasMaxLength(200);
        builder.Property(v => v.Status).HasConversion<int>();
        builder.Property(v => v.Notes).HasMaxLength(1000);
        builder.Property(v => v.Rating).HasPrecision(3, 2);

        // Un proveedor eliminado libera su nombre (a diferencia de los codigos, ADR-013).
        builder.HasIndex(v => v.Name).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_Vendors_Name");
        builder.HasIndex(v => v.TaxId).HasDatabaseName("IX_Vendors_TaxId");
        builder.HasIndex(v => v.Status).HasDatabaseName("IX_Vendors_Status");
    }
}

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Contracts", t =>
        {
            t.HasCheckConstraint("CK_Contracts_EndDate", "[EndDate] > [StartDate]");
            t.HasCheckConstraint("CK_Contracts_Value", "[Value] IS NULL OR [Value] >= 0");
        });
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Number).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Type).HasConversion<int>();
        builder.Property(c => c.Status).HasConversion<int>();
        builder.Property(c => c.Value).HasPrecision(18, 2);
        builder.Property(c => c.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("USD");
        builder.Property(c => c.RenewalNoticeDays).HasDefaultValue(30);
        builder.Property(c => c.Notes).HasMaxLength(1000);

        builder.HasIndex(c => c.Number).IsUnique().HasDatabaseName("UX_Contracts_Number");
        builder.HasIndex(c => c.EndDate).HasDatabaseName("IX_Contracts_EndDate");
        builder.HasIndex(c => new { c.VendorId, c.Status }).HasDatabaseName("IX_Contracts_VendorId_Status");

        builder.HasOne(c => c.Vendor)
            .WithMany()
            .HasForeignKey(c => c.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.ResponsibleUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Contract>()
            .WithMany()
            .HasForeignKey(c => c.RenewedFromContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SoftwareConfiguration : IEntityTypeConfiguration<Software>
{
    public void Configure(EntityTypeBuilder<Software> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Softwares");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Version).HasMaxLength(50);
        builder.Property(s => s.Publisher).HasMaxLength(150);
        builder.Property(s => s.Category).HasMaxLength(100);
        builder.Property(s => s.Description).HasMaxLength(500);

        builder.HasIndex(s => new { s.Name, s.Version }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_Softwares_Name_Version");
        builder.HasIndex(s => s.Category).HasDatabaseName("IX_Softwares_Category");
    }
}

public class SoftwareLicenseConfiguration : IEntityTypeConfiguration<SoftwareLicense>
{
    public void Configure(EntityTypeBuilder<SoftwareLicense> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SoftwareLicenses", t =>
        {
            t.HasCheckConstraint("CK_SoftwareLicenses_Quantity", "[Quantity] >= 0");
            t.HasCheckConstraint("CK_SoftwareLicenses_UsedQuantity", "[UsedQuantity] >= 0 AND [UsedQuantity] <= [Quantity]");
        });
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name).HasMaxLength(150).IsRequired();
        builder.Property(l => l.LicenseType).HasConversion<int>();
        // Texto cifrado (Data Protection): mas largo que la clave original.
        builder.Property(l => l.LicenseKey).HasMaxLength(1000);
        builder.Property(l => l.Cost).HasPrecision(18, 2);
        builder.Property(l => l.Notes).HasMaxLength(1000);
        builder.Ignore(l => l.AvailableQuantity);

        // Concurrencia optimista: evita que dos instalaciones simultaneas ocupen el mismo ultimo puesto.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasIndex(l => l.ExpirationDate).HasDatabaseName("IX_SoftwareLicenses_ExpirationDate");
        builder.HasIndex(l => l.SoftwareId).HasDatabaseName("IX_SoftwareLicenses_SoftwareId");
        builder.HasIndex(l => l.VendorId).HasDatabaseName("IX_SoftwareLicenses_VendorId");

        builder.HasOne(l => l.Software)
            .WithMany()
            .HasForeignKey(l => l.SoftwareId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Vendor)
            .WithMany()
            .HasForeignKey(l => l.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Contract)
            .WithMany()
            .HasForeignKey(l => l.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SoftwareInstallationConfiguration : IEntityTypeConfiguration<SoftwareInstallation>
{
    public void Configure(EntityTypeBuilder<SoftwareInstallation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SoftwareInstallations");
        builder.HasKey(i => i.Id);

        builder.HasIndex(i => new { i.LicenseId, i.AssetId })
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("UX_SoftwareInstallations_LicenseId_AssetId_Active");
        builder.HasIndex(i => i.AssetId).HasDatabaseName("IX_SoftwareInstallations_AssetId");

        builder.HasOne(i => i.License)
            .WithMany()
            .HasForeignKey(i => i.LicenseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Asset)
            .WithMany()
            .HasForeignKey(i => i.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
