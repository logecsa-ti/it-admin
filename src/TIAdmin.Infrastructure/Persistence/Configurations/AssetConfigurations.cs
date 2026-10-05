namespace TIAdmin.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TIAdmin.Domain.Entities;
using TIAdmin.Infrastructure.Identity;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Assets");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AssetCode).HasMaxLength(30).IsRequired();
        builder.Property(a => a.SerialNumber).HasMaxLength(100);
        builder.Property(a => a.Name).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(500);
        builder.Property(a => a.Brand).HasMaxLength(100);
        builder.Property(a => a.Model).HasMaxLength(100);
        builder.Property(a => a.PurchaseCost).HasPrecision(18, 2);
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.Status).HasConversion<int>();

        builder.HasIndex(a => a.AssetCode).IsUnique().HasDatabaseName("UX_Assets_AssetCode");
        builder.HasIndex(a => a.SerialNumber).IsUnique().HasDatabaseName("UX_Assets_SerialNumber").HasFilter("[SerialNumber] IS NOT NULL");
        builder.HasIndex(a => a.Status).HasDatabaseName("IX_Assets_Status");
        builder.HasIndex(a => a.AssetTypeId).HasDatabaseName("IX_Assets_AssetTypeId");
        builder.HasIndex(a => a.LocationId).HasDatabaseName("IX_Assets_LocationId");
        builder.HasIndex(a => a.DepartmentId).HasDatabaseName("IX_Assets_DepartmentId");
        builder.HasIndex(a => a.CurrentUserId).HasDatabaseName("IX_Assets_CurrentUserId");
        builder.HasIndex(a => a.WarrantyExpiration).HasDatabaseName("IX_Assets_WarrantyExpiration");

        builder.HasOne(a => a.AssetType)
            .WithMany()
            .HasForeignKey(a => a.AssetTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Location)
            .WithMany()
            .HasForeignKey(a => a.LocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Department)
            .WithMany()
            .HasForeignKey(a => a.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.ParentAsset)
            .WithMany(a => a.Children)
            .HasForeignKey(a => a.ParentAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        // El dominio no conoce ApplicationUser: la FK se declara solo en persistencia.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.CurrentUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    public void Configure(EntityTypeBuilder<AssetAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AssetAssignments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ConditionAtAssignment).HasMaxLength(500);
        builder.Property(a => a.ConditionAtReturn).HasMaxLength(500);
        builder.Property(a => a.Notes).HasMaxLength(1000);

        builder.HasIndex(a => a.AssetId).HasDatabaseName("IX_AssetAssignments_AssetId");
        builder.HasIndex(a => a.UserId).HasDatabaseName("IX_AssetAssignments_UserId");
        builder.HasIndex(a => new { a.AssetId, a.IsActive }).HasDatabaseName("UX_AssetAssignments_AssetId_IsActive")
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        builder.HasOne(a => a.Asset)
            .WithMany()
            .HasForeignKey(a => a.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.AssignedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.ReturnedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}