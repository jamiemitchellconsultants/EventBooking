using EventBooking.Api.Pagination;
using EventBooking.Application.Common;

namespace EventBooking.Mcp.Tools;

/// <summary>
/// The REST cursor on the tool surface: verified on the way in, signed on the way out. A tool
/// that passed the raw keyset payload through would accept a cursor REST refuses, hand out
/// one REST cannot read, and let an unverified payload reach the query.
/// </summary>
internal static class McpCursors
{
    /// <summary>Verifies a caller's cursor and returns its payload, or refuses as REST does.</summary>
    /// <param name="cursors">The signer.</param>
    /// <param name="cursor">The caller's cursor, or null for the first page.</param>
    /// <returns>The keyset payload, or null for the first page.</returns>
    internal static string? Unwrap(this PageCursor cursors, string? cursor)
    {
        if (cursor is null)
        {
            return null;
        }

        return cursors.TryUnprotect(cursor, out var payload)
            ? payload
            : throw McpErrors.ToMcpException(Error.Validation("That cursor is not valid."));
    }

    /// <summary>Signs a keyset payload, passing a null (the last page) through.</summary>
    /// <param name="cursors">The signer.</param>
    /// <param name="payload">The keyset payload, or null.</param>
    /// <returns>The opaque cursor, or null.</returns>
    internal static string? Wrap(this PageCursor cursors, string? payload) =>
        payload is null ? null : cursors.Protect(payload);
}
