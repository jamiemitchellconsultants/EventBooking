using System.Text;

namespace EventBooking.Application.ReadModels;

// Cursor payload contract (Task 21 adds the HMAC wrapper). Opaque to callers: the sort
// key plus the row id, base64url-encoded, joined by a dot.
/// <summary>Encodes and decodes the attendee-list keyset cursor payload.</summary>
public static class AttendeeCursor
{
    /// <summary>Encodes one cursor from its sort key and row id.</summary>
    /// <param name="sortKey">The sort key.</param>
    /// <param name="id">The row id.</param>
    public static string Encode(string sortKey, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sortKey}.{id:N}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Decodes one cursor back to its sort key and row id.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="sortKey">The sort key.</param>
    /// <param name="id">The row id.</param>
    public static bool TryDecode(string? cursor, out string sortKey, out Guid id)
    {
        sortKey = string.Empty;
        id = Guid.Empty;
        if (string.IsNullOrEmpty(cursor)) return false;
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var dot = decoded.LastIndexOf('.');
            if (dot < 0 || !Guid.TryParseExact(decoded[(dot + 1)..], "N", out id)) return false;
            sortKey = decoded[..dot];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
