namespace TIAdmin.Application.Common.Interfaces;

/// <summary>
/// Agrupa los repositorios sobre un mismo DbContext. El DbContext pertenece al contenedor
/// de DI (scoped), por lo que el UnitOfWork no lo libera.
/// </summary>
public interface IUnitOfWork
{
    IDepartmentRepository Departments { get; }

    ILocationRepository Locations { get; }

    IAssetRepository Assets { get; }

    IAssetAssignmentRepository AssetAssignments { get; }

    IAssetMovementRepository AssetMovements { get; }

    IAssetTypeRepository AssetTypes { get; }

    /// <summary>
    /// Persiste los cambios. Una violacion de indice unico (p. ej. dos asignaciones
    /// simultaneas del mismo activo) se traduce a <see cref="Domain.Exceptions.ConflictException"/>.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
