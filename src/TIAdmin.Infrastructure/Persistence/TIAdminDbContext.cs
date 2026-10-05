namespace TIAdmin.Infrastructure.Persistence;

using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Domain.Common;
using TIAdmin.Infrastructure.Identity;

/// <summary>
/// Contexto principal de persistencia. Aplica convenciones globales:
/// - Nombres de tablas explicitos en plural PascalCase (ADR-009)
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

    public DbSet<Domain.Entities.Asset> Assets => Set<Domain.Entities.Asset>();

    public DbSet<Domain.Entities.AssetAssignment> AssetAssignments => Set<Domain.Entities.AssetAssignment>();

    public DbSet<Domain.Entities.AssetMovement> AssetMovements => Set<Domain.Entities.AssetMovement>();

    public DbSet<Domain.Entities.Vendor> Vendors => Set<Domain.Entities.Vendor>();

    public DbSet<Domain.Entities.Contract> Contracts => Set<Domain.Entities.Contract>();

    public DbSet<Domain.Entities.Software> Softwares => Set<Domain.Entities.Software>();

    public DbSet<Domain.Entities.SoftwareLicense> SoftwareLicenses => Set<Domain.Entities.SoftwareLicense>();

    public DbSet<Domain.Entities.SoftwareInstallation> SoftwareInstallations => Set<Domain.Entities.SoftwareInstallation>();

    public DbSet<ApplicationPermission> Permissions => Set<ApplicationPermission>();

    public DbSet<ApplicationRolePermission> RolePermissions => Set<ApplicationRolePermission>();

    public DbSet<ApplicationUserPermission> UserPermissions => Set<ApplicationUserPermission>();

    /// <summary>
    /// ADR-005: todas las fechas son UTC. datetime2 no guarda el Kind, asi que al leer se marca
    /// como Utc; sin esto se serializan sin "Z" y el frontend las interpretaria como hora local.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        builder.ApplyGlobalConventions();
    }
}
