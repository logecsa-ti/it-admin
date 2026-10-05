namespace TIAdmin.Infrastructure.Persistence.Interceptors;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Domain.Common;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;

/// <summary>
/// Registra en AuditLog las operaciones administrativas criticas (SPECS.md seccion 18).
/// Complementa a AuditSaveChangesInterceptor, que aplica las convenciones de timestamps.
/// Cubre tambien usuarios, roles y asignaciones de Identity (ADR-015).
/// Se apoya en ChangeTracker para capturar valores antes/despues de forma reliable.
/// </summary>
public sealed class AuditTrailInterceptor(
    ICurrentUserService currentUser,
    IClock clock,
    IAuditContext auditContext)
    : SaveChangesInterceptor
{
    /// <summary>
    /// Propiedades que nunca deben quedar registradas en la auditoria.
    /// </summary>
    private static readonly HashSet<string> ExcludedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "TokenHash",
        "NormalizedEmail", "NormalizedUserName", "LicenseKey"
    };

    /// <summary>
    /// Propiedades que cambian como efecto secundario (login, concurrencia, sellos de auditoria).
    /// Un Update que solo toca estas propiedades no se registra: evita un AuditLog por cada login.
    /// </summary>
    private static readonly HashSet<string> NoiseProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "ConcurrencyStamp", "SecurityStamp", "LastLoginAt", "AccessFailedCount", "UpdatedAt", "UpdatedBy"
    };

    /// <summary>
    /// Entidades de Identity auditadas, con el nombre y modulo con que se registran.
    /// El catalogo de permisos (ApplicationPermission) no se audita: lo define el codigo.
    /// </summary>
    private static readonly Dictionary<Type, (string EntityName, string Module)> IdentityEntities = new()
    {
        [typeof(Identity.ApplicationUser)] = ("User", "Users"),
        [typeof(Microsoft.AspNetCore.Identity.IdentityUserRole<int>)] = ("UserRole", "Users"),
        [typeof(Identity.ApplicationUserPermission)] = ("UserPermission", "Users"),
        [typeof(Identity.ApplicationRole)] = ("Role", "Roles"),
        [typeof(Identity.ApplicationRolePermission)] = ("RolePermission", "Roles")
    };

    /// <summary>
    /// Altas pendientes de auditar. Su Id (identity) solo existe despues del INSERT,
    /// por eso se registran en SavedChanges y no en SavingChanges.
    /// </summary>
    private readonly List<EntityEntry> pendingCreates = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is DbContext context)
        {
            WriteAuditLogs(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is DbContext context)
        {
            WriteAuditLogs(context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is DbContext context && AddPendingCreateLogs(context))
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is DbContext context && AddPendingCreateLogs(context))
        {
            context.SaveChanges();
        }

        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        pendingCreates.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        pendingCreates.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>
    /// Agrega los AuditLog de las altas ya persistidas. Devuelve true si hay que guardar de nuevo.
    /// El segundo SaveChanges solo contiene AuditLog, que no se audita, asi que no hay recursion.
    /// </summary>
    private bool AddPendingCreateLogs(DbContext context)
    {
        if (pendingCreates.Count == 0)
        {
            return false;
        }

        var entries = pendingCreates
            .Select(entry =>
            {
                var entityName = GetEntityName(entry);
                return (AuditAction.Create, ResolveModule(entry), entityName,
                    GetEntityId(entry), (string?)null, Serialize(Snapshot(entry, useOriginal: false)));
            })
            .ToList();

        pendingCreates.Clear();
        AddAuditLogs(context, entries);
        return true;
    }

    private void WriteAuditLogs(DbContext context)
    {
        var entries = new List<(AuditAction Action, string Module, string EntityName, string EntityId, string? Old, string? New)>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // El propio AuditLog nunca se audita: evita recursion.
            if (entry.Entity is AuditLog || !ShouldAudit(entry))
            {
                continue;
            }

            var entityName = GetEntityName(entry);
            var module = ResolveModule(entry);

            switch (entry.State)
            {
                case EntityState.Added:
                    pendingCreates.Add(entry);
                    break;

                // UserManager.UpdateAsync marca todo como modificado; solo cuenta lo que realmente cambio.
                case EntityState.Modified when !IsSoftDeletion(entry) && !HasMeaningfulChanges(entry):
                    break;

                // AuditSaveChangesInterceptor ya convirtio el Remove() en soft delete (Modified).
                case EntityState.Modified when IsSoftDeletion(entry):
                    entries.Add((AuditAction.Delete, module, entityName,
                        GetEntityId(entry), Serialize(Snapshot(entry, useOriginal: true)), null));
                    break;

                case EntityState.Modified:
                    entries.Add((AuditAction.Update, module, entityName,
                        GetEntityId(entry),
                        Serialize(Snapshot(entry, useOriginal: true)),
                        Serialize(Snapshot(entry, useOriginal: false))));
                    break;

                case EntityState.Deleted:
                    entries.Add((AuditAction.Delete, module, entityName,
                        GetEntityId(entry), Serialize(Snapshot(entry, useOriginal: true)), null));
                    break;
            }
        }

        AddAuditLogs(context, entries);
    }

    private void AddAuditLogs(
        DbContext context,
        List<(AuditAction Action, string Module, string EntityName, string EntityId, string? Old, string? New)> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;

        foreach (var (action, module, entityName, entityId, old, @new) in entries)
        {
            context.Set<AuditLog>().Add(new AuditLog
            {
                UserId = currentUser.UserId,
                UserName = Truncate(currentUser.UserName, 256),
                Action = action,
                Module = module,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = old,
                NewValues = @new,
                IpAddress = Truncate(currentUser.IpAddress, 45),
                UserAgent = Truncate(currentUser.UserAgent(), 500),
                Timestamp = now,
                CorrelationId = Truncate(auditContext.CorrelationId, 64),
                IsError = false
            });
        }
    }

    /// <summary>
    /// Se auditan las entidades de dominio y las de Identity relevantes para la seguridad
    /// (usuarios, roles y sus asignaciones de roles/permisos).
    /// </summary>
    private static bool ShouldAudit(EntityEntry entry) =>
        entry.Entity is IAuditableEntity or ISoftDeletable
        || IdentityEntities.ContainsKey(entry.Metadata.ClrType);

    private static bool HasMeaningfulChanges(EntityEntry entry) =>
        entry.Properties.Any(p => !NoiseProperties.Contains(p.Metadata.Name)
            && !Equals(p.OriginalValue, p.CurrentValue));

    private static string GetEntityName(EntityEntry entry) =>
        IdentityEntities.TryGetValue(entry.Metadata.ClrType, out var identity)
            ? identity.EntityName
            : entry.Metadata.ClrType.Name;

    private static bool IsSoftDeletion(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDeletable)
        {
            return false;
        }

        var isDeleted = entry.Property(nameof(ISoftDeletable.IsDeleted));
        return isDeleted.CurrentValue is true && isDeleted.OriginalValue is false;
    }

    /// <summary>
    /// Proyecta solo las propiedades escalares relevantes, evitando datos sensibles
    /// y referencias circulares entre entidades.
    /// </summary>
    private static Dictionary<string, object?> Snapshot(EntityEntry entry, bool useOriginal)
    {
        var snapshot = new Dictionary<string, object?>();
        var values = useOriginal ? entry.OriginalValues : entry.CurrentValues;

        foreach (var property in entry.Properties)
        {
            if (!ExcludedProperties.Contains(property.Metadata.Name))
            {
                snapshot[property.Metadata.Name] = values[property.Metadata.Name];
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Id simple, o "RoleId=3;PermissionId=7" para las tablas de union con clave compuesta.
    /// </summary>
    private static string GetEntityId(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties ?? [];
        var value = keyProperties.Count switch
        {
            0 => null,
            1 => entry.Property(keyProperties[0].Name).CurrentValue?.ToString(),
            _ => string.Join(";", keyProperties.Select(p => $"{p.Name}={entry.Property(p.Name).CurrentValue}"))
        };

        return Truncate(value, 64) ?? "0";
    }

    private static string ResolveModule(EntityEntry entry) =>
        IdentityEntities.TryGetValue(entry.Metadata.ClrType, out var identity)
            ? identity.Module
            : ResolveModule(entry.Metadata.ClrType.Name);

    private static string ResolveModule(string entityName) => entityName switch
    {
        var n when n.Contains("Asset", StringComparison.OrdinalIgnoreCase) => "Assets",
        var n when n.Contains("Ticket", StringComparison.OrdinalIgnoreCase) => "Tickets",
        var n when n.Contains("Request", StringComparison.OrdinalIgnoreCase) => "Requests",
        // "License" antes que "Software": SoftwareLicense pertenece al modulo de licencias.
        var n when n.Contains("License", StringComparison.OrdinalIgnoreCase) => "Licenses",
        var n when n.Contains("Installation", StringComparison.OrdinalIgnoreCase) => "Licenses",
        var n when n.Contains("Software", StringComparison.OrdinalIgnoreCase) => "Software",
        var n when n.Contains("Vendor", StringComparison.OrdinalIgnoreCase) => "Vendors",
        var n when n.Contains("Contract", StringComparison.OrdinalIgnoreCase) => "Contracts",
        var n when n.Contains("Maintenance", StringComparison.OrdinalIgnoreCase) => "Maintenance",
        var n when n.Contains("Change", StringComparison.OrdinalIgnoreCase) => "Changes",
        var n when n.Contains("Purchase", StringComparison.OrdinalIgnoreCase) => "Purchases",
        var n when n.Contains("Department", StringComparison.OrdinalIgnoreCase) => "Organization",
        var n when n.Contains("Location", StringComparison.OrdinalIgnoreCase) => "Organization",
        var n when n.Contains("Configuration", StringComparison.OrdinalIgnoreCase) => "Configuration",
        _ => "General"
    };

    private static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(value, JsonSerializerOptions.Default);
        }
        catch (NotSupportedException)
        {
            // Nunca debe fallar una operacion de negocio por un detalle de auditoria.
            return null;
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
