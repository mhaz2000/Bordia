namespace BuildingBlocks.Domain.Pagination;

/// <summary>
/// Parameters for paginated queries.
/// </summary>
public class PaginationParams
{
    /// <summary>
    /// 1-based page number. Defaults to 1.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Number of items per page. Defaults to 20, maximum 100.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Computes the number of items to skip for the current page.
    /// </summary>
    public int Skip => (Page - 1) * Take;

    /// <summary>
    /// The actual page size to use, clamped between 1 and 100.
    /// </summary>
    public int Take => Math.Clamp(PageSize, 1, 100);
}
