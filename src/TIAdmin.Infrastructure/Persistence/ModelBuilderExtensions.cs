namespace TIAdmin.Infrastructure.Persistence;

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TIAdmin.Domain.Common;

/// <summary>
/// Convenciones globales aplicadas a todas las entidades.
/// </summary>
public static class ModelBuilderExtensions
{
    public static ModelBuilder ApplyGlobalConventions(this ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // Solo entidades de dominio (excluye las de ASP.NET Core Identity)
            if (!typeof(IEntity).IsAssignableFrom(clrType) && !typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                continue;
            }

            if (typeof(IEntity).IsAssignableFrom(clrType))
            {
                var idProperty = entityType.FindProperty("Id");
                if (idProperty is not null)
                {
                    idProperty.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd;
                }
            }

if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
        {
            // SPECS.md seccion 37: las entidades con historico nunca se eliminan fisicamente
            entityType.SetQueryFilter(BuildSoftDeleteFilter((IEntityType)entityType));
        }
        }

        return builder;
    }

    private static LambdaExpression? BuildSoftDeleteFilter(IEntityType entityType)
    {
        var parameter = Expression.Parameter(entityType.ClrType, "e");

        var isDeleted = entityType.FindProperty(nameof(ISoftDeletable.IsDeleted));
        if (isDeleted?.PropertyInfo is null)
        {
            return null;
        }

        var body = Expression.Not(Expression.Property(parameter, isDeleted.PropertyInfo));
        return Expression.Lambda(body, parameter);
    }
}
