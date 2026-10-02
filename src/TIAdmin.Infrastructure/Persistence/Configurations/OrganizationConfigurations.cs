namespace TIAdmin.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TIAdmin.Domain.Entities;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Departments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Code).HasMaxLength(20).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(150).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(500);

        builder.HasIndex(d => d.Code).IsUnique().HasDatabaseName("IX_Departments_Code");
        builder.HasIndex(d => d.Name).HasDatabaseName("IX_Departments_Name");
        builder.HasIndex(d => d.ManagerId).HasDatabaseName("IX_Departments_ManagerId");

        builder.HasOne(d => d.Parent)
            .WithMany(d => d.Children)
            .HasForeignKey(d => d.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Locations");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Name).HasMaxLength(150).IsRequired();
        builder.Property(l => l.Address).HasMaxLength(300);
        builder.Property(l => l.City).HasMaxLength(100);
        builder.Property(l => l.Country).HasMaxLength(100);

        builder.HasIndex(l => l.Code).IsUnique().HasDatabaseName("IX_Locations_Code");
        builder.HasIndex(l => l.Name).HasDatabaseName("IX_Locations_Name");
    }
}

public class AssetTypeConfiguration : IEntityTypeConfiguration<AssetType>
{
    public void Configure(EntityTypeBuilder<AssetType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AssetTypes");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Code).HasMaxLength(30).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(300);

        builder.HasIndex(a => a.Code).IsUnique().HasDatabaseName("IX_AssetTypes_Code");
        builder.HasIndex(a => a.Name).IsUnique().HasDatabaseName("IX_AssetTypes_Name");
    }
}
