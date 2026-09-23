using System.Security.Cryptography;
using System.Text;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Infrastructure.Tests;

public class HmacTokenServiceTests
{
    private const string SigningKey = "a-signing-key-that-is-long-enough-to-be-safe";

    private static readonly TokenOptions Options =
        new(SigningKey);

    private readonly HmacTokenService _service = new(Options);

    [Fact]
    public void AnIssuedTokenRoundTripsToItsIdentifier()
    {
        var id = Guid.NewGuid();

        var issued = _service.Issue(id);

        Assert.True(_service.TryRead(issued.Token, out var read));
        Assert.Equal(id, read);
    }

    [Fact]
    public void TheStoredValueIsAHashNotTheToken()
    {
        var issued = _service.Issue(Guid.NewGuid());

        Assert.NotEqual(issued.Token, issued.TokenHash);
        Assert.Equal(issued.TokenHash, _service.Hash(issued.Token));
        Assert.Equal(64, issued.TokenHash.Length);
        Assert.DoesNotContain(issued.TokenHash, issued.Token);
    }

    [Fact]
    public void TwoTokensForTheSameIdentifierAreDifferent()
    {
        var id = Guid.NewGuid();

        var first = _service.Issue(id);
        var second = _service.Issue(id);

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.TokenHash, second.TokenHash);
        Assert.True(_service.TryRead(first.Token, out var a));
        Assert.True(_service.TryRead(second.Token, out var b));
        Assert.Equal(a, b);
    }

    [Fact]
    public void ATamperedIdentifierFailsVerification()
    {
        var issued = _service.Issue(Guid.NewGuid());
        var parts = issued.Token.Split('.');
        var forged = $"{Guid.NewGuid():N}.{parts[1]}.{parts[2]}";

        Assert.False(_service.TryRead(forged, out _));
    }

    [Fact]
    public void ATamperedSignatureFailsVerification()
    {
        var issued = _service.Issue(Guid.NewGuid());
        var parts = issued.Token.Split('.');

        Assert.False(_service.TryRead($"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}", out _));
    }

    [Fact]
    public void ATokenSignedWithAnotherKeyIsRejected()
    {
        var other = new HmacTokenService(new TokenOptions("a-completely-different-signing-key-value"));
        var issued = other.Issue(Guid.NewGuid());

        Assert.False(_service.TryRead(issued.Token, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("one-part")]
    [InlineData("two.parts")]
    [InlineData("a.b.c.d")]
    [InlineData("not-a-guid.nonce.signature")]
    public void AMalformedTokenIsRejectedWithoutThrowing(string? token)
    {
        Assert.False(_service.TryRead(token, out var id));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void TheTokenIsUrlSafe()
    {
        var token = _service.Issue(Guid.NewGuid()).Token;

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

    [Fact]
    public void ATokenWithAnUppercaseIdentifierIsRejectedEvenWhenSignedWithTheKnownKey()
    {
        var id = Guid.NewGuid();
        var nonce = "AAAAAAAAAAAAAAAAAAAAAA";
        var token = CreateKnownKeyToken(id.ToString("N").ToUpperInvariant(), nonce);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Theory]
    [InlineData("!AAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAB")]
    public void ATokenWithANonCanonicalNonceIsRejectedEvenWhenSignedWithTheKnownKey(string nonce)
    {
        var token = CreateKnownKeyToken(Guid.NewGuid().ToString("N"), nonce);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Theory]
    [InlineData("invalid-character")]
    [InlineData("padding")]
    [InlineData("wrong-length")]
    public void ATokenWithAMalformedSignatureEncodingIsRejected(string kind)
    {
        var issued = _service.Issue(Guid.NewGuid());
        var parts = issued.Token.Split('.');
        var malformedSignature = kind switch
        {
            "invalid-character" => $"!{parts[2][1..]}",
            "padding" => $"{parts[2][..^1]}=",
            "wrong-length" => parts[2][..^1],
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        Assert.False(_service.TryRead($"{parts[0]}.{parts[1]}.{malformedSignature}", out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Fact]
    public void ATokenWithANonCanonicalSignatureIsRejected()
    {
        var issued = _service.Issue(Guid.NewGuid());
        var finalCharacter = issued.Token[^1];
        const string base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        var alternateCharacter = base64UrlAlphabet[base64UrlAlphabet.IndexOf(finalCharacter) + 1];
        var token = $"{issued.Token[..^1]}{alternateCharacter}";

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Fact]
    public void TokensOutsideTheCanonicalLengthAreRejected()
    {
        var token = CreateKnownKeyToken(Guid.NewGuid().ToString("N"), new string('A', 10_000));

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    private static string CreateKnownKeyToken(string identifier, string nonce)
    {
        var payload = $"{identifier}.{nonce}";
        var signature = ToBase64Url(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(SigningKey),
            Encoding.UTF8.GetBytes(payload)));

        return $"{payload}.{signature}";
    }

    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
