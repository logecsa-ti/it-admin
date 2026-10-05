namespace TIAdmin.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TIAdmin.Domain.Entities;
using TIAdmin.Infrastructure.Identity;

public class TicketCategoryConfiguration : IEntityTypeConfiguration<TicketCategory>
{
    public void Configure(EntityTypeBuilder<TicketCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TicketCategories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(300);
        builder.Property(c => c.Type).HasConversion<int>();
        builder.Property(c => c.DefaultPriority).HasConversion<int>();

        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("UX_TicketCategories_Code");
        builder.HasIndex(c => c.Name).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_TicketCategories_Name");

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(c => c.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SlaPolicies", t =>
        {
            t.HasCheckConstraint("CK_SlaPolicies_ResponseTime", "[ResponseTimeMinutes] > 0");
            t.HasCheckConstraint("CK_SlaPolicies_ResolutionTime", "[ResolutionTimeMinutes] >= [ResponseTimeMinutes]");
            t.HasCheckConstraint("CK_SlaPolicies_WorkHours", "[WorkEndTime] > [WorkStartTime]");
        });
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Priority).HasConversion<int?>();
        builder.Property(p => p.TicketType).HasConversion<int?>();
        builder.Property(p => p.WorkDays).HasMaxLength(20).IsRequired();
        builder.Ignore(p => p.Schedule);
        builder.Ignore(p => p.Specificity);

        builder.HasIndex(p => p.Name).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_SlaPolicies_Name");
        // A lo sumo una politica predeterminada vigente.
        builder.HasIndex(p => p.IsDefault).IsUnique().HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_SlaPolicies_Default");

        builder.HasOne<TicketCategory>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(p => p.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tickets");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TicketNumber).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).IsRequired();
        builder.Property(t => t.Type).HasConversion<int>();
        builder.Property(t => t.Priority).HasConversion<int>();
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.ApprovalStatus).HasConversion<int?>();
        builder.Property(t => t.ResolutionNotes).HasMaxLength(4000);
        builder.Property(t => t.ApprovalComment).HasMaxLength(500);
        builder.Ignore(t => t.IsOpen);
        builder.Ignore(t => t.IsAwaitingApproval);

        // Evita que dos agentes sobrescriban cambios simultaneos del mismo ticket.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasIndex(t => t.TicketNumber).IsUnique().HasDatabaseName("UX_Tickets_TicketNumber");
        builder.HasIndex(t => t.Status).HasDatabaseName("IX_Tickets_Status");
        builder.HasIndex(t => t.Priority).HasDatabaseName("IX_Tickets_Priority");
        builder.HasIndex(t => t.AssignedToId).HasDatabaseName("IX_Tickets_AssignedToId");
        builder.HasIndex(t => t.RequesterId).HasDatabaseName("IX_Tickets_RequesterId");
        builder.HasIndex(t => t.CreatedAt).HasDatabaseName("IX_Tickets_CreatedAt");
        builder.HasIndex(t => t.DueAtResolution).HasDatabaseName("IX_Tickets_DueAtResolution");
        builder.HasIndex(t => t.CategoryId).HasDatabaseName("IX_Tickets_CategoryId");

        builder.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.ApprovedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(t => t.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(t => t.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SlaPolicy>().WithMany().HasForeignKey(t => t.SlaPolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TicketComments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content).HasMaxLength(4000).IsRequired();
        builder.HasIndex(c => c.TicketId).HasDatabaseName("IX_TicketComments_TicketId");

        builder.HasOne(c => c.Ticket).WithMany().HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TicketStatusHistoryConfiguration : IEntityTypeConfiguration<TicketStatusHistory>
{
    public void Configure(EntityTypeBuilder<TicketStatusHistory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TicketStatusHistory");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus).HasConversion<int?>();
        builder.Property(h => h.ToStatus).HasConversion<int>();
        builder.Property(h => h.UserName).HasMaxLength(256);
        builder.Property(h => h.Comment).HasMaxLength(500);

        builder.HasIndex(h => new { h.TicketId, h.Timestamp }).HasDatabaseName("IX_TicketStatusHistory_TicketId_Timestamp");

        builder.HasOne(h => h.Ticket).WithMany().HasForeignKey(h => h.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
