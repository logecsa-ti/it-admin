namespace TIAdmin.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TIAdmin.Infrastructure.Identity;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.UserName).HasMaxLength(256).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.EmployeeCode).HasMaxLength(50);
        builder.Property(u => u.JobTitle).HasMaxLength(100);
        builder.Property(u => u.ExternalProvider).HasMaxLength(50);
        builder.Property(u => u.ExternalId).HasMaxLength(128);

        builder.HasIndex(u => u.Email).IsUnique().HasFilter(null);
        builder.HasIndex(u => u.EmployeeCode).IsUnique().HasFilter(null);
        builder.HasIndex(u => u.DepartmentId).HasDatabaseName("IX_Users_DepartmentId");
        builder.HasIndex(u => u.LocationId).HasDatabaseName("IX_Users_LocationId");
        builder.HasIndex(u => u.IsActive).HasDatabaseName("IX_Users_IsActive");
    }
}

public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.NormalizedName).HasMaxLength(256);

        builder.HasIndex(r => r.NormalizedName).IsUnique().HasDatabaseName("IX_Roles_NormalizedName");
    }
}

public class ApplicationPermissionConfiguration : IEntityTypeConfiguration<ApplicationPermission>
{
    public void Configure(EntityTypeBuilder<ApplicationPermission> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Permissions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Module).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Action).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(300).IsRequired();

        builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("IX_Permissions_Code");
    }
}

public class ApplicationRolePermissionConfiguration : IEntityTypeConfiguration<ApplicationRolePermission>
{
    public void Configure(EntityTypeBuilder<ApplicationRolePermission> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RolePermissions");
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.Permissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ApplicationUserPermissionConfiguration : IEntityTypeConfiguration<ApplicationUserPermission>
{
    public void Configure(EntityTypeBuilder<ApplicationUserPermission> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UserPermissions");
        builder.HasKey(up => new { up.UserId, up.PermissionId });

        builder.HasOne(up => up.User)
            .WithMany(u => u.Permissions)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(up => up.Permission)
            .WithMany(p => p.UserPermissions)
            .HasForeignKey(up => up.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
