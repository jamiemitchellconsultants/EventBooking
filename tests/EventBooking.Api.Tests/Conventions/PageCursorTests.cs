using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using EventBooking.Api.Pagination;

namespace EventBooking.Api.Tests.Conventions;

/// <summary>Pins the opaque cursor: it round-trips, and a tampered one is refused.</summary>
public sealed class PageCursorTests
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes(
        "a-test-signing-key-that-is-long-enough-here");

    [Fact]
    public void ACursorRoundTripsItsPayload()
    {
        var cursor = new PageCursor(Key);

        var protectedValue = cursor.Protect("2026-10-14T09:30:00Z|1f0c…");

        Assert.True(cursor.TryUnprotect(protectedValue, out var payload));
        Assert.Equal("2026-10-14T09:30:00Z|1f0c…", payload);
    }

    /// <summary>
    /// The flipped byte is in the signature, not the payload. Editing the payload would also
    /// break decoding, so that version of this test would pass for the wrong reason.
    /// </summary>
    [Fact]
    public void ATamperedSignatureIsRefused()
    {
        var cursor = new PageCursor(Key);
        var issued = cursor.Protect("page-2");
        var parts = issued.Split('.');
        var signature = Base64Url.DecodeFromChars(parts[1]);
        signature[0] ^= 0xFF;
        var tampered = parts[0] + "." + Base64Url.EncodeToString(signature);

        Assert.False(cursor.TryUnprotect(tampered, out _));
    }

    [Fact]
    public void ACursorFromAnotherKeyIsRefused()
    {
        var issued = new PageCursor(Key).Protect("page-2");
        var other = new PageCursor(RandomNumberGenerator.GetBytes(32));

        Assert.False(other.TryUnprotect(issued, out _));
    }

    /// <summary>
    /// The configured key also signs attendee booking links, so the cursor signs under a key
    /// derived for cursors alone. Otherwise every cursor the API hands out would be an HMAC
    /// under the link key, one payload-format change away from a forged link.
    /// </summary>
    [Fact]
    public void ACursorIsNotSignedWithTheSharedKeyItself()
    {
        var issued = new PageCursor(Key).Protect("page-2");
        var signature = Base64Url.DecodeFromChars(issued.Split('.')[1]);

        Assert.NotEqual(
            HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes("page-2")), signature);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    [InlineData("only-one-part.")]
    public void AMalformedCursorIsRefusedWithoutThrowing(string? value)
    {
        Assert.False(new PageCursor(Key).TryUnprotect(value, out _));
    }

    [Theory]
    [InlineData(null, PageRequest.DefaultLimit)]
    [InlineData(1, 1)]
    [InlineData(200, 200)]
    public void ALimitInRangeBinds(int? limit, int expected)
    {
        Assert.True(PageRequest.TryBind(null, limit, out var request, out var field));
        Assert.Equal(expected, request.Limit);
        Assert.Null(field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    [InlineData(-5)]
    public void ALimitOutsideTheRangeIsAFieldErrorRatherThanASilentClamp(int limit)
    {
        Assert.False(PageRequest.TryBind(null, limit, out _, out var field));
        Assert.Equal("limit", field);
    }
}
