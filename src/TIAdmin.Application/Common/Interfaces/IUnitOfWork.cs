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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}