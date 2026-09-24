using System.Buffers.Text;
using System.Text;

namespace EventBooking.Application.ReadModels;

/// <summary>
/// The keyset payload every paged read shares: the sort key and the row identifier, base64url
/// encoded and joined by a dot. Task 20a defined this contract for the attendee list; the
/// event and proposal lists need the same thing, so it lives here and the attendee codec
/// forwards to it. It is not a security boundary — Task 21's PageCursor signs it at the API
/// edge, which is where a tampered cursor is refused.
/// </summary>
public static class KeysetCursor
{
    /// <summary>Encodes one row's position.</summary>
    /// <param name="sortKey">The sort key, already formatted for ordinal comparison.</param>
    /// <param name="id">The row identifier that breaks ties on the sort key.</param>
    /// <returns>The opaque payload.</returns>
    public static string Encode(string sortKey, Guid id)
    {
        ArgumentNullException.ThrowIfNull(sortKey);
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(sortKey)) + "." +
            Base64Url.EncodeToString(id.ToByteArray());
    }

    /// <summary>Decodes one row's position.</summary>
    /// <param name="cursor">The opaque payload.</param>
    /// <param name="sortKey">Receives the sort key.</param>
    /// <param name="id">Receives the row identifier.</param>
    /// <returns>Whether the payload decoded.</returns>
    public static bool TryDecode(string? cursor, out string sortKey, out Guid id)
    {
        sortKey = string.Empty;
        id = Guid.Empty;
        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        var separator = cursor.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == cursor.Length - 1)
        {
            return false;
        }

        try
        {
            sortKey = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor.AsSpan(0, separator)));
            var bytes = Base64Url.DecodeFromChars(cursor.AsSpan(separator + 1));
            if (bytes.Length != 16)
            {
                return false;
            }

            id = new Guid(bytes);
            return true;
        }
        catch (FormatException)
        {
            sortKey = string.Empty;
            return false;
        }
    }
}
