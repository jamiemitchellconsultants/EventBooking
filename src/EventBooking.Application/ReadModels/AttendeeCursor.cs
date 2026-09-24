namespace EventBooking.Application.ReadModels;

/// <summary>The attendee list's keyset cursor. One codec, shared with every other paged read.</summary>
public static class AttendeeCursor
{
    /// <summary>Encodes one attendee row's position.</summary>
    /// <param name="sortKey">The sort key.</param>
    /// <param name="id">The attendee identifier.</param>
    /// <returns>The opaque payload.</returns>
    public static string Encode(string sortKey, Guid id) => KeysetCursor.Encode(sortKey, id);

    /// <summary>Decodes one attendee row's position.</summary>
    /// <param name="cursor">The opaque payload.</param>
    /// <param name="sortKey">Receives the sort key.</param>
    /// <param name="id">Receives the attendee identifier.</param>
    /// <returns>Whether the payload decoded.</returns>
    public static bool TryDecode(string? cursor, out string sortKey, out Guid id) =>
        KeysetCursor.TryDecode(cursor, out sortKey, out id);
}
