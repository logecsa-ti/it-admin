namespace TIAdmin.Application.Common.Interfaces;

using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;

public interface IDepartmentRepository : IRepository<Department>
{
    /// <summary>
    /// Incluye registros eliminados (soft delete): el indice unico de Code los cubre
    /// y un codigo usado queda reservado para conservar la trazabilidad historica.
    /// </summary>
    Task<bool> ExistsCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Department>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<Department?> GetWithChildrenAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResult<DepartmentDto>> SearchAsync(PagedQuery query, bool? isActive, int? parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si asignar <paramref name="candidateParentId"/> como padre de
    /// <paramref name="departmentId"/> crearia un ciclo en la jerarquia.
    /// </summary>
    Task<bool> WouldCreateCycleAsync(int departmentId, int candidateParentId, CancellationToken cancellationToken = default);
}

public interface ILocationRepository : IRepository<Location>
{
    /// <inheritdoc cref="IDepartmentRepository.ExistsCodeAsync"/>
    Task<bool> ExistsCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Location>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<LocationDto>> SearchAsync(PagedQuery query, bool? isActive, CancellationToken cancellationToken = default);
}
