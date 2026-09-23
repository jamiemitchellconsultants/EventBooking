using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Infrastructure.Tests;

public class HmacTokenServiceTests
{
    private const string SigningKey = "a-signing-key-that-is-long-enough-to-be-safe";

    /// <summary>purpose (1) + identifier (16) + version (4) + HMAC-SHA256 (32), base64url, unpadded.</summary>
    private const int TokenLength = 71;

    private static readonly TokenOptions Options =
        new(SigningKey);

    private readonly HmacTokenService _service = new(Options);

    [Theory]
    [InlineData(TokenPurpose.Book)]
    [InlineData(TokenPurpose.Manage)]
    public void AnIssuedTokenRoundTripsToItsPurposeIdentifierAndVersion(TokenPurpose purpose)
    {
        var id = Guid.NewGuid();

        var token = _service.Issue(purpose, id, 4);

        Assert.True(_service.TryRead(token, out var read));
        Assert.Equal(new TokenReference(purpose, id, 4), read);
    }

    /// <summary>
    /// Determinism is what lets the confirmation page and the confirmation email carry the same
    /// manage link without either of them storing it.
    /// </summary>
    [Fact]
    public void TheSameInputsAlwaysProduceTheSameToken()
    {
        var id = Guid.NewGuid();

        Assert.Equal(
            _service.Issue(TokenPurpose.Manage, id, 1),
            _service.Issue(TokenPurpose.Manage, id, 1));
    }

    [Fact]
    public void ABookTokenIsNotAManageTokenForTheSameIdentifier()
    {
        var id = Guid.NewGuid();

        var book = _service.Issue(TokenPurpose.Book, id, 1);
        var manage = _service.Issue(TokenPurpose.Manage, id, 1);

        Assert.NotEqual(book, manage);
        Assert.True(_service.TryRead(book, out var readBook));
        Assert.Equal(TokenPurpose.Book, readBook.Purpose);
        Assert.True(_service.TryRead(manage, out var readManage));
        Assert.Equal(TokenPurpose.Manage, readManage.Purpose);
    }

    [Fact]
    public void EachVersionOfOneIdentifierIsADifferentToken()
    {
        var id = Guid.NewGuid();

        var first = _service.Issue(TokenPurpose.Book, id, 1);
        var second = _service.Issue(TokenPurpose.Book, id, 2);

        Assert.NotEqual(first, second);
        Assert.True(_service.TryRead(first, out var readFirst));
        Assert.Equal(1, readFirst.Version);
        Assert.True(_service.TryRead(second, out var readSecond));
        Assert.Equal(2, readSecond.Version);
    }

    /// <summary>Nothing derived from the token is stored, so the service offers no hash of it.</summary>
    [Fact]
    public void TheServiceOffersNoWayToDeriveAStoredValueFromAToken()
    {
        Assert.DoesNotContain(
            typeof(ITokenService).GetMethods(),
            method => method.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ANonPositiveVersionIsRefusedAtIssue()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 0));

        Assert.Equal("version", ex.ParamName);
    }

    [Fact]
    public void AnUnknownPurposeIsRefusedAtIssue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.Issue((TokenPurpose)7, Guid.NewGuid(), 1));
    }

    [Fact]
    public void ATokenCarryingANonPositiveVersionIsRejected()
    {
        var token = CreateKnownKeyToken((byte)TokenPurpose.Book, Guid.NewGuid(), 0);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(default, read);
    }

    [Fact]
    public void ATokenCarryingAnUnknownPurposeIsRejected()
    {
        var token = CreateKnownKeyToken(7, Guid.NewGuid(), 1);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(default, read);
    }

    [Fact]
    public void ATamperedTokenFailsVerification()
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);
        var replacement = Alphabet[(Alphabet.IndexOf(token[0], StringComparison.Ordinal) + 1) % Alphabet.Length];

        Assert.False(_service.TryRead($"{replacement}{token[1..]}", out var read));
        Assert.Equal(default, read);
    }

    [Fact]
    public void ATokenSignedWithAnotherKeyIsRejected()
    {
        var other = new HmacTokenService(new TokenOptions("a-completely-different-signing-key-value"));
        var token = other.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);

        Assert.False(_service.TryRead(token, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short")]
    [InlineData("a.b.c")]
    public void AMalformedTokenIsRejectedWithoutThrowing(string? token)
    {
        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(default, read);
    }

    /// <summary>
    /// The final base64url character carries only two significant bits; the other three spellings
    /// decode to the same bytes, and accepting them would make one link answer to four URLs.
    /// </summary>
    [Fact]
    public void ATokenWithANonCanonicalEncodingIsRejected()
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);
        var index = Alphabet.IndexOf(token[^1], StringComparison.Ordinal);
        var alternate = Alphabet[(index & ~0b11) | ((index + 1) & 0b11)];

        Assert.False(_service.TryRead($"{token[..^1]}{alternate}", out _));
    }

    [Theory]
    [InlineData("!")]
    [InlineData("=")]
    public void ATokenWithACharacterOutsideTheBase64UrlAlphabetIsRejected(string character)
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);

        Assert.False(_service.TryRead($"{character}{token[1..]}", out _));
    }

    [Fact]
    public void TokensOutsideTheCanonicalLengthAreRejected()
    {
        Assert.False(_service.TryRead(new string('A', 10_000), out _));
        Assert.False(_service.TryRead(new string('A', TokenLength - 1), out _));
        Assert.False(_service.TryRead(new string('A', TokenLength + 1), out _));
    }

    [Fact]
    public void TheTokenIsUrlSafeAndOfTheCanonicalLength()
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);

        Assert.Equal(TokenLength, token.Length);
        Assert.Equal(token, Uri.EscapeDataString(token));
    }

    [Fact]
    public void AShortSigningKeyIsRejectedAtConstruction()
    {
        var ex = Assert.Throws<ArgumentException>(() => new HmacTokenService(new TokenOptions("too-short")));
        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public void OptionsToStringDoesNotRevealTheSigningKey()
    {
        Assert.DoesNotContain(SigningKey, Options.ToString());
    }

    [Fact]
    public void NullOptionsAreRejectedWithANonSecretMessage()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new HmacTokenService(null!));

        Assert.Equal("options", ex.ParamName);
        Assert.DoesNotContain(SigningKey, ex.Message);
    }

    [Fact]
    public void ARuntimeNullSigningKeyIsRejectedWithANonSecretMessage()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new HmacTokenService(new TokenOptions(null!)));

        Assert.Equal("SigningKey", ex.ParamName);
        Assert.DoesNotContain(SigningKey, ex.Message);
    }

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    private static string CreateKnownKeyToken(byte purpose, Guid id, int version)
    {
        var payload = new byte[21];
        payload[0] = purpose;
        id.TryWriteBytes(payload.AsSpan(1, 16), bigEndian: true, out _);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(17, 4), version);

        var signed = new byte[53];
        payload.CopyTo(signed, 0);
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(SigningKey), payload).CopyTo(signed, 21);

        return Convert.ToBase64String(signed).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
