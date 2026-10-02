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

            var entityName = entry.Metadata.ClrType.Name;

            switch (entry.State)
            {
                case EntityState.Added:
                    entries.Add((AuditAction.Create, ResolveModule(entityName), entityName,
                        GetEntityId(entry), null, Serialize(Snapshot(entry, useOriginal: false))));
                    break;

                case EntityState.Modified:
                    entries.Add((AuditAction.Update, ResolveModule(entityName), entityName,
                        GetEntityId(entry),
                        Serialize(Snapshot(entry, useOriginal: true)),
                        Serialize(Snapshot(entry, useOriginal: false))));
                    break;

                case EntityState.Deleted:
                    entries.Add((AuditAction.Delete, ResolveModule(entityName), entityName,
                        GetEntityId(entry), Serialize(Snapshot(entry, useOriginal: true)), null));
                    break;
            }
        }

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
    /// Solo se auditan entidades de dominio. Las de Identity se auditan de forma explicita
    /// en los endpoints de administracion de usuarios, no en cada operacion.
    /// </summary>
    private static bool ShouldAudit(EntityEntry entry) =>
        typeof(IAuditableEntity).IsAssignableFrom(entry.Entity.GetType())
        || typeof(ISoftDeletable).IsAssignableFrom(entry.Entity.GetType());

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

    private static string GetEntityId(EntityEntry entry)
    {
        var idProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
        return Truncate(idProperty?.CurrentValue?.ToString(), 64) ?? "0";
    }

    private static string ResolveModule(string entityName) => entityName switch
    {
        var n when n.Contains("Asset", StringComparison.OrdinalIgnoreCase) => "Assets",
        var n when n.Contains("Ticket", StringComparison.OrdinalIgnoreCase) => "Tickets",
        var n when n.Contains("Request", StringComparison.OrdinalIgnoreCase) => "Requests",
        var n when n.Contains("Software", StringComparison.OrdinalIgnoreCase) => "Software",
        var n when n.Contains("License", StringComparison.OrdinalIgnoreCase) => "Licenses",
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
