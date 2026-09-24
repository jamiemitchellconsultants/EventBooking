using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace EventBooking.Api.Pagination;

/// <summary>
/// The opaque keyset cursor: base64url(payload) + "." + base64url(HMAC-SHA256(cursorKey, payload)).
/// The signature is checked in constant time before the payload is read, so a tampered or
/// forged cursor costs one hash and never reaches a query. There is no offset pagination.
/// </summary>
/// <param name="sharedKey">
/// The configured signing key, shared with the attendee token service. Cursors sign under a
/// key derived from it for this purpose alone, never under the shared key itself.
/// </param>
public sealed class PageCursor(byte[] sharedKey)
{
    private readonly byte[] _signingKey = HMACSHA256.HashData(
        sharedKey, "EventBooking.PageCursor.v1"u8);

    /// <summary>Signs and encodes one sort-key payload.</summary>
    /// <param name="payload">The keyset payload the query produced.</param>
    /// <returns>The opaque cursor.</returns>
    public string Protect(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(_signingKey, bytes);
        return Base64Url.EncodeToString(bytes) + "." + Base64Url.EncodeToString(signature);
    }

    /// <summary>Verifies and decodes one cursor.</summary>
    /// <param name="cursor">The cursor supplied by the caller.</param>
    /// <param name="payload">Receives the payload when the cursor verifies.</param>
    /// <returns>Whether the cursor verified.</returns>
    public bool TryUnprotect(string? cursor, out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrEmpty(cursor))
        {
            return false;
        }

        var separator = cursor.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == cursor.Length - 1)
        {
            return false;
        }

        byte[] bytes;
        byte[] supplied;
        try
        {
            bytes = Base64Url.DecodeFromChars(cursor.AsSpan(0, separator));
            supplied = Base64Url.DecodeFromChars(cursor.AsSpan(separator + 1));
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(_signingKey, bytes);
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            return false;
        }

        payload = Encoding.UTF8.GetString(bytes);
        return true;
    }
}
