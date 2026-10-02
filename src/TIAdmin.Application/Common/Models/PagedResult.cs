namespace TIAdmin.Application.Common.Models;

/// <summary>
/// Resultado paginado estándar (SPECS.md sección 14).
/// </summary>
public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, long totalItems)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = pageSize > 0
            ? (int)Math.Ceiling(totalItems / (double)pageSize)
            : 0;
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public long TotalItems { get; }

    public int TotalPages { get; }

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) =>
        new([], page, pageSize, 0);
}
