namespace Habbak.ERP.Application.Common.Models;

/// <summary>
/// Matches the GetList response contract exactly (00-Frontend-Specs.md, section 6):
/// { items, totalCount, page, pageSize }.
/// </summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

/// <summary>Matches the GetList request contract (00-Frontend-Specs.md, section 6).</summary>
public class ListQuery
{
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? SortBy { get; init; }
    public string SortDir { get; init; } = "asc";
}
