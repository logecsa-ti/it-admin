namespace TIAdmin.Infrastructure.Persistence.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Domain.Common;

/// <summary>
/// Intercepta SaveChanges para aplicar convenciones de auditoria de forma centralizada:
/// - CreatedAt / UpdatedAt en UTC
/// - CreatedBy / UpdatedBy desde el usuario actual
/// - Soft delete en lugar de borrado fisico
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUserService currentUser, IClock clock)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        ApplyConventions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        ApplyConventions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyConventions(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // SPECS.md seccion 37: un Remove() sobre una entidad con historico se convierte en soft delete.
            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable softDeletable })
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAt = now;
                softDeletable.DeletedBy = userId;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.Entity is IAuditableEntity auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.CreatedAt = now;
                        auditable.CreatedBy = userId;
                        break;

                    case EntityState.Modified:
                        auditable.UpdatedAt = now;
                        auditable.UpdatedBy = userId;
                        break;
                }
            }
        }
    }
}
