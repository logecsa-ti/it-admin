namespace TIAdmin.Infrastructure.Persistence;

using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Domain.Common;
using TIAdmin.Infrastructure.Identity;

/// <summary>
/// Contexto principal de persistencia. Aplica convenciones globales:
/// - Nombres de tablas en singular PascalCase
/// - Soft delete via query filter (SPECS.md seccion 37)
/// - Timestamps en UTC (SPECS.md seccion 38)
/// </summary>
public class TIAdminDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public TIAdminDbContext(DbContextOptions<TIAdminDbContext> options)
        : base(options)
    {
    }

    public DbSet<Domain.Entities.Department> Departments => Set<Domain.Entities.Department>();

    public DbSet<Domain.Entities.Location> Locations => Set<Domain.Entities.Location>();

    public DbSet<Domain.Entities.AssetType> AssetTypes => Set<Domain.Entities.AssetType>();

    public DbSet<Domain.Entities.SystemConfiguration> SystemConfigurations => Set<Domain.Entities.SystemConfiguration>();

    public DbSet<Domain.Entities.AuditLog> AuditLogs => Set<Domain.Entities.AuditLog>();

    public DbSet<Domain.Entities.RefreshToken> RefreshTokens => Set<Domain.Entities.RefreshToken>();

    public DbSet<ApplicationPermission> Permissions => Set<ApplicationPermission>();

    public DbSet<ApplicationRolePermission> RolePermissions => Set<ApplicationRolePermission>();

    public DbSet<ApplicationUserPermission> UserPermissions => Set<ApplicationUserPermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        builder.ApplyGlobalConventions();
    }
}
