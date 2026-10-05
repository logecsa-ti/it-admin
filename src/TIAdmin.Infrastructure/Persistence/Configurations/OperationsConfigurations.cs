namespace TIAdmin.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TIAdmin.Domain.Entities;
using TIAdmin.Infrastructure.Identity;

public class MaintenanceConfiguration : IEntityTypeConfiguration<Maintenance>
{
    public void Configure(EntityTypeBuilder<Maintenance> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Maintenances", t =>
        {
            t.HasCheckConstraint("CK_Maintenances_EstimatedCost", "[EstimatedCost] IS NULL OR [EstimatedCost] >= 0");
            t.HasCheckConstraint("CK_Maintenances_ActualCost", "[ActualCost] IS NULL OR [ActualCost] >= 0");
        });
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Number).HasMaxLength(20).IsRequired();
        builder.Property(m => m.Title).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(4000);
        builder.Property(m => m.Type).HasConversion<int>();
        builder.Property(m => m.Status).HasConversion<int>();
        builder.Property(m => m.EstimatedCost).HasPrecision(18, 2);
        builder.Property(m => m.ActualCost).HasPrecision(18, 2);
        builder.Property(m => m.Findings).HasMaxLength(4000);
        builder.Property(m => m.Actions).HasMaxLength(4000);
        builder.Property(m => m.Recommendations).HasMaxLength(4000);
        builder.Property(m => m.CancellationReason).HasMaxLength(1000);
        builder.Ignore(m => m.IsClosed);
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasIndex(m => m.Number).IsUnique().HasDatabaseName("UX_Maintenances_Number");
        builder.HasIndex(m => m.AssetId).HasDatabaseName("IX_Maintenances_AssetId");
        builder.HasIndex(m => m.Status).HasDatabaseName("IX_Maintenances_Status");
        builder.HasIndex(m => m.ScheduledDate).HasDatabaseName("IX_Maintenances_ScheduledDate");
        builder.HasIndex(m => m.NextDueDate).HasDatabaseName("IX_Maintenances_NextDueDate");
        builder.HasIndex(m => m.Type).HasDatabaseName("IX_Maintenances_Type");

        // Sin navegaciones a principales con soft delete: no ocultar historial de activos dados de baja.
        builder.HasOne<Asset>().WithMany().HasForeignKey(m => m.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vendor>().WithMany().HasForeignKey(m => m.VendorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(m => m.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Contract>().WithMany().HasForeignKey(m => m.ContractId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ChangeRequests");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Number).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).IsRequired();
        builder.Property(c => c.Type).HasConversion<int>();
        builder.Property(c => c.Risk).HasConversion<int>();
        builder.Property(c => c.Impact).HasConversion<int>();
        builder.Property(c => c.Status).HasConversion<int>();
        builder.Property(c => c.ReviewComment).HasMaxLength(1000);
        builder.Property(c => c.RejectionReason).HasMaxLength(1000);
        builder.Property(c => c.ImplementationNotes).HasMaxLength(2000);
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasIndex(c => c.Number).IsUnique().HasDatabaseName("UX_ChangeRequests_Number");
        builder.HasIndex(c => c.Status).HasDatabaseName("IX_ChangeRequests_Status");
        builder.HasIndex(c => c.PlannedDate).HasDatabaseName("IX_ChangeRequests_PlannedDate");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.RequestedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.AssignedToId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.ApprovedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(c => c.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(c => c.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PurchaseRequests", t =>
            t.HasCheckConstraint("CK_PurchaseRequests_EstimatedCost", "[EstimatedCost] >= 0"));
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Number).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4000);
        builder.Property(p => p.Justification).HasMaxLength(4000);
        builder.Property(p => p.Status).HasConversion<int>();
        builder.Property(p => p.EstimatedCost).HasPrecision(18, 2);
        builder.Property(p => p.RejectionReason).HasMaxLength(1000);
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasIndex(p => p.Number).IsUnique().HasDatabaseName("UX_PurchaseRequests_Number");
        builder.HasIndex(p => p.Status).HasDatabaseName("IX_PurchaseRequests_Status");

        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => i.PurchaseRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.RequestedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.ApprovedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vendor>().WithMany().HasForeignKey(p => p.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PurchaseItems", t =>
        {
            t.HasCheckConstraint("CK_PurchaseItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_PurchaseItems_UnitPrice", "[UnitPrice] >= 0");
        });
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Description).HasMaxLength(300).IsRequired();
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        builder.Property(i => i.TotalPrice).HasPrecision(18, 2);
        builder.Property(i => i.Notes).HasMaxLength(500);

        builder.HasOne<AssetType>().WithMany().HasForeignKey(i => i.AssetTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vendor>().WithMany().HasForeignKey(i => i.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}
