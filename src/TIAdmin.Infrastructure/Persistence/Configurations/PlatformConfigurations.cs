namespace TIAdmin.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TIAdmin.Domain.Entities;
using TIAdmin.Infrastructure.Identity;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Documents", t => t.HasCheckConstraint("CK_Documents_Size", "[Size] >= 0"));
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName).HasMaxLength(300).IsRequired();
        builder.Property(d => d.StoragePath).HasMaxLength(500).IsRequired();
        builder.Property(d => d.MimeType).HasMaxLength(150).IsRequired();
        builder.Property(d => d.Checksum).HasMaxLength(128);
        builder.Property(d => d.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(500);
        builder.Property(d => d.Category).HasMaxLength(100);

        builder.HasIndex(d => new { d.EntityName, d.EntityId }).HasDatabaseName("IX_Documents_EntityName_EntityId");
        builder.HasIndex(d => d.StoragePath).IsUnique().HasDatabaseName("UX_Documents_StoragePath");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.UploadedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type).HasMaxLength(50).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.Link).HasMaxLength(300);
        builder.Property(n => n.EntityName).HasMaxLength(100);
        builder.Property(n => n.DedupKey).HasMaxLength(150);

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt }).HasDatabaseName("IX_Notifications_UserId_IsRead_CreatedAt");
        // Una alerta periodica se notifica una sola vez por usuario.
        builder.HasIndex(n => new { n.UserId, n.DedupKey }).IsUnique().HasFilter("[DedupKey] IS NOT NULL")
            .HasDatabaseName("UX_Notifications_UserId_DedupKey");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ExportJobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.ReportName).HasMaxLength(50).IsRequired();
        builder.Property(j => j.Format).HasMaxLength(10).IsRequired();
        builder.Property(j => j.Parameters).HasMaxLength(2000);
        builder.Property(j => j.Status).HasConversion<int>();
        builder.Property(j => j.FileName).HasMaxLength(200);
        builder.Property(j => j.StoragePath).HasMaxLength(500);
        builder.Property(j => j.Error).HasMaxLength(1000);

        builder.HasIndex(j => new { j.RequestedById, j.CreatedAt }).HasDatabaseName("IX_ExportJobs_RequestedById_CreatedAt");

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(j => j.RequestedById).OnDelete(DeleteBehavior.Restrict);
    }
}
