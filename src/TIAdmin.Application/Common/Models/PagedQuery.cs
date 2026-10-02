namespace TIAdmin.Application.Common.Models;

public sealed class PagedQuery
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 200;

    private int _pageSize = DefaultPageSize;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public string? Search { get; set; }

    public string? SortBy { get; set; }

    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;

    public int Skip => (Page - 1) * PageSize;

    public string? NormalizeSearch() =>
        string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}

public enum SortDirection
{
    Ascending = 0,
    Descending = 1
}

public sealed class DateRangeQuery
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}
