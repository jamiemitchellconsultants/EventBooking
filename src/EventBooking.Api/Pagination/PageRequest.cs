namespace EventBooking.Api.Pagination;

/// <summary>The bound query string every list endpoint takes.</summary>
/// <param name="Cursor">The opaque cursor, or null for the first page.</param>
/// <param name="Limit">The page size, within the design's bounds.</param>
public sealed record PageRequest(string? Cursor, int Limit)
{
    /// <summary>The page size used when the caller supplies none (design 08).</summary>
    public const int DefaultLimit = 50;

    /// <summary>The smallest page size the API accepts.</summary>
    public const int MinLimit = 1;

    /// <summary>The largest page size the API accepts.</summary>
    public const int MaxLimit = 200;

    /// <summary>
    /// Binds the two query-string values. A limit outside the range is a field error rather
    /// than a silent clamp: a caller asking for 500 rows and quietly getting 200 cannot tell
    /// a short page from the end of the collection.
    /// </summary>
    /// <param name="cursor">The raw cursor value.</param>
    /// <param name="limit">The raw limit value.</param>
    /// <param name="request">Receives the bound request.</param>
    /// <param name="field">Receives the offending field name when binding fails.</param>
    /// <returns>Whether the values bound.</returns>
    public static bool TryBind(string? cursor, int? limit, out PageRequest request, out string? field)
    {
        field = null;
        request = new PageRequest(cursor, DefaultLimit);
        if (limit is null)
        {
            return true;
        }

        if (limit < MinLimit || limit > MaxLimit)
        {
            field = "limit";
            return false;
        }

        request = new PageRequest(cursor, limit.Value);
        return true;
    }
}

/// <summary>The one list envelope: items already projected, and the next cursor or null.</summary>
/// <param name="Items">The page's rows.</param>
/// <param name="NextCursor">The cursor for the following page, or null on the last.</param>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
