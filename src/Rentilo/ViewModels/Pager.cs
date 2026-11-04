namespace Rentilo.ViewModels;

/// <summary>Pagination state. Route values for other filters are kept in <see cref="RouteValues"/>.</summary>
public record Pager(int Page, int PageSize, int TotalCount, IDictionary<string, string?> RouteValues)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public Dictionary<string, string?> For(int page) =>
        new(RouteValues.Where(kv => !string.IsNullOrEmpty(kv.Value))) { ["page"] = page.ToString() };
}
