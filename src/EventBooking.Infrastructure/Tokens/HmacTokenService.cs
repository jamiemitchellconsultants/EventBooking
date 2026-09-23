using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Tokens;

public sealed class HmacTokenService : ITokenService
{
    private const int MinimumKeyLength = 32;

    /// <summary>
    /// The placeholder signing key shipped in appsettings.json. It passes the length check,
    /// so it is rejected by value: anyone who can read this repository could forge tokens with it.
    /// </summary>
    private const string PlaceholderSigningKey = "replace-this-with-a-real-secret-of-at-least-32-characters";
    private const int NonceBytes = 16;
    private const int GuidLength = 32;
    private const int NonceLength = 22;
    private const int SignatureBytes = 32;
    private const int SignatureLength = 43;
    private const int PayloadLength = GuidLength + 1 + NonceLength;
    private const int TokenLength = PayloadLength + 1 + SignatureLength;

    private readonly byte[] _key;

    public HmacTokenService(TokenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.SigningKey, nameof(options.SigningKey));

        if (options.SigningKey.Length < MinimumKeyLength)
        {
            throw new ArgumentException(
                $"The token signing key must be at least {MinimumKeyLength} characters.",
                nameof(options));
        }

        if (string.Equals(options.SigningKey, PlaceholderSigningKey, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The token signing key is still the placeholder from appsettings.json. Configure Tokens:SigningKey.",
                nameof(options));
        }

        _key = Encoding.UTF8.GetBytes(options.SigningKey);
    }

    public IssuedToken Issue(Guid entityId)
    {
        var nonce = ToBase64Url(RandomNumberGenerator.GetBytes(NonceBytes));
        var payload = $"{entityId:N}.{nonce}";
        var token = $"{payload}.{Sign(payload)}";

        return new IssuedToken(token, Hash(token));
    }

    public bool TryRead(string? token, out Guid entityId)
    {
        entityId = Guid.Empty;

        if (token is null || token.Length != TokenLength)
        {
            return false;
        }

        if (token[GuidLength] != '.' || token[PayloadLength] != '.')
        {
            return false;
        }

        var identifier = token[..GuidLength];
        if (!Guid.TryParseExact(identifier, "N", out var id) ||
            !string.Equals(identifier, id.ToString("N"), StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryDecodeBase64Url(token.AsSpan(GuidLength + 1, NonceLength), NonceBytes, out _) ||
            !TryDecodeBase64Url(token.AsSpan(PayloadLength + 1, SignatureLength), SignatureBytes, out var supplied))
        {
            return false;
        }

        var expected = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(token[..PayloadLength]));

        // Constant time: a timing difference here would leak how much of a guess was right.
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            return false;
        }

        entityId = id;
        return true;
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private string Sign(string payload) =>
        ToBase64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));

    /// <summary>Base64 with the two characters that are unsafe in a URL replaced, and no padding.</summary>
    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecodeBase64Url(ReadOnlySpan<char> value, int expectedByteLength, out byte[] decoded)
    {
        decoded = Array.Empty<byte>();

        if (!IsBase64Url(value))
        {
            return false;
        }

        try
        {
            var base64 = value.ToString().Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            decoded = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return false;
        }

        return decoded.Length == expectedByteLength &&
            value.SequenceEqual(ToBase64Url(decoded).AsSpan());
    }

    private static bool IsBase64UrlCharacter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';

    private static bool IsBase64Url(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!IsBase64UrlCharacter(character))
            {
                return false;
            }
        }

        return true;
    }
}
