namespace TIAdmin.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Infrastructure.Persistence;

public abstract class BaseRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, TIAdmin.Domain.Common.IEntity
{
    protected TIAdminDbContext Context { get; }
    protected DbSet<TEntity> Set { get; }

    protected BaseRepository(TIAdminDbContext context)
    {
        Context = context;
        Set = context.Set<TEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await Set.FindAsync([id], cancellationToken);

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking().ToListAsync(cancellationToken);

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual void Update(TEntity entity) => Set.Update(entity);

    /// <summary>
    /// Para entidades <see cref="TIAdmin.Domain.Common.ISoftDeletable"/> el interceptor
    /// convierte el borrado en soft delete.
    /// </summary>
    public virtual void Delete(TEntity entity) => Set.Remove(entity);

    public virtual async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        await Set.AnyAsync(e => e.Id == id, cancellationToken);

    protected static async Task<PagedResult<TDto>> ToPagedResultAsync<TDto>(
        IQueryable<TDto> query,
        PagedQuery paging,
        CancellationToken cancellationToken)
    {
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.Skip(paging.Skip).Take(paging.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<TDto>(items, paging.Page, paging.PageSize, total);
    }
}

public class DepartmentRepository : BaseRepository<Department>, IDepartmentRepository
{
    public DepartmentRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = Set.IgnoreQueryFilters().Where(d => d.Code == code);
        if (excludeId.HasValue)
        {
            query = query.Where(d => d.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Department>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

    public async Task<Department?> GetWithChildrenAsync(int id, CancellationToken cancellationToken = default) =>
        await Set
            .Include(d => d.Children)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<PagedResult<DepartmentDto>> SearchAsync(
        PagedQuery query,
        bool? isActive,
        int? parentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var departments = Set.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            departments = departments.Where(d => d.Code.Contains(search) || d.Name.Contains(search));
        }

        if (isActive.HasValue)
        {
            departments = departments.Where(d => d.IsActive == isActive.Value);
        }

        if (parentId.HasValue)
        {
            departments = departments.Where(d => d.ParentId == parentId.Value);
        }

        var descending = query.SortDirection == SortDirection.Descending;
        departments = query.SortBy?.ToLowerInvariant() switch
        {
            "code" => descending ? departments.OrderByDescending(d => d.Code) : departments.OrderBy(d => d.Code),
            "createdat" => descending ? departments.OrderByDescending(d => d.CreatedAt) : departments.OrderBy(d => d.CreatedAt),
            _ => descending ? departments.OrderByDescending(d => d.Name) : departments.OrderBy(d => d.Name)
        };

        var projected = departments.Select(d =>
            new DepartmentDto(d.Id, d.Code, d.Name, d.Description, d.ManagerId, d.ParentId, d.IsActive));

        return await ToPagedResultAsync(projected, query, cancellationToken);
    }

    public async Task<bool> WouldCreateCycleAsync(int departmentId, int candidateParentId, CancellationToken cancellationToken = default)
    {
        // La jerarquia es poco profunda: se recorre hacia arriba desde el candidato.
        var visited = new HashSet<int>();
        int? current = candidateParentId;

        while (current.HasValue)
        {
            if (current.Value == departmentId || !visited.Add(current.Value))
            {
                return true;
            }

            var currentId = current.Value;
            current = await Set.AsNoTracking()
                .Where(d => d.Id == currentId)
                .Select(d => d.ParentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return false;
    }
}

public class LocationRepository : BaseRepository<Location>, ILocationRepository
{
    public LocationRepository(TIAdminDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = Set.IgnoreQueryFilters().Where(l => l.Code == code);
        if (excludeId.HasValue)
        {
            query = query.Where(l => l.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Location>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<LocationDto>> SearchAsync(
        PagedQuery query,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var locations = Set.AsNoTracking();

        if (query.NormalizeSearch() is { } search)
        {
            locations = locations.Where(l => l.Code.Contains(search) || l.Name.Contains(search)
                || (l.City != null && l.City.Contains(search)));
        }

        if (isActive.HasValue)
        {
            locations = locations.Where(l => l.IsActive == isActive.Value);
        }

        var descending = query.SortDirection == SortDirection.Descending;
        locations = query.SortBy?.ToLowerInvariant() switch
        {
            "code" => descending ? locations.OrderByDescending(l => l.Code) : locations.OrderBy(l => l.Code),
            "city" => descending ? locations.OrderByDescending(l => l.City) : locations.OrderBy(l => l.City),
            "createdat" => descending ? locations.OrderByDescending(l => l.CreatedAt) : locations.OrderBy(l => l.CreatedAt),
            _ => descending ? locations.OrderByDescending(l => l.Name) : locations.OrderBy(l => l.Name)
        };

        var projected = locations.Select(l =>
            new LocationDto(l.Id, l.Code, l.Name, l.Address, l.City, l.Country, l.IsActive));

        return await ToPagedResultAsync(projected, query, cancellationToken);
    }
}
