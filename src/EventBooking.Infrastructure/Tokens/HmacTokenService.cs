using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Tokens;

/// <summary>
/// base64url(purpose ‖ id ‖ version ‖ HMAC-SHA256(key, purpose ‖ id ‖ version)), per design 06.
/// </summary>
public sealed class HmacTokenService : ITokenService
{
    private const int MinimumKeyLength = 32;

    /// <summary>
    /// The placeholder signing key shipped in appsettings.json. It passes the length check,
    /// so it is rejected by value: anyone who can read this repository could forge tokens with it.
    /// </summary>
    private const string PlaceholderSigningKey = "replace-this-with-a-real-secret-of-at-least-32-characters";

    private const int PurposeBytes = 1;
    private const int IdentifierBytes = 16;
    private const int VersionBytes = 4;
    private const int PayloadBytes = PurposeBytes + IdentifierBytes + VersionBytes;
    private const int SignatureBytes = 32;
    private const int TokenBytes = PayloadBytes + SignatureBytes;

    /// <summary>53 bytes encode to 71 unpadded base64url characters.</summary>
    private const int TokenLength = 71;

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

    public string Issue(TokenPurpose purpose, Guid entityId, int version)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1, nameof(version));

        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        Span<byte> token = stackalloc byte[TokenBytes];
        WritePayload(token, (byte)purpose, entityId, version);
        HMACSHA256.HashData(_key, token[..PayloadBytes], token[PayloadBytes..]);

        return ToBase64Url(token);
    }

    public bool TryRead(string? token, out TokenReference reference)
    {
        reference = default;

        if (token is null || token.Length != TokenLength)
        {
            return false;
        }

        Span<byte> decoded = stackalloc byte[TokenBytes];
        if (!TryDecodeBase64Url(token, decoded))
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[SignatureBytes];
        HMACSHA256.HashData(_key, decoded[..PayloadBytes], expected);

        // Constant time: a timing difference here would leak how much of a guess was right.
        if (!CryptographicOperations.FixedTimeEquals(expected, decoded[PayloadBytes..]))
        {
            return false;
        }

        var purpose = (TokenPurpose)decoded[0];
        if (!Enum.IsDefined(purpose))
        {
            return false;
        }

        var version = BinaryPrimitives.ReadInt32BigEndian(
            decoded.Slice(PurposeBytes + IdentifierBytes, VersionBytes));
        if (version < 1)
        {
            return false;
        }

        reference = new TokenReference(
            purpose,
            new Guid(decoded.Slice(PurposeBytes, IdentifierBytes), bigEndian: true),
            version);
        return true;
    }

    private static void WritePayload(Span<byte> destination, byte purpose, Guid entityId, int version)
    {
        destination[0] = purpose;
        entityId.TryWriteBytes(destination.Slice(PurposeBytes, IdentifierBytes), bigEndian: true, out _);
        BinaryPrimitives.WriteInt32BigEndian(
            destination.Slice(PurposeBytes + IdentifierBytes, VersionBytes), version);
    }

    /// <summary>Base64 with the two characters that are unsafe in a URL replaced, and no padding.</summary>
    private static string ToBase64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Decodes only the canonical spelling. Base64 has several encodings of the same bytes once
    /// padding bits are ignored, and accepting them would make one link answer to many URLs.
    /// </summary>
    private static bool TryDecodeBase64Url(string value, Span<byte> destination)
    {
        Span<char> base64 = stackalloc char[TokenLength + 1];
        for (var index = 0; index < value.Length; index++)
        {
            base64[index] = value[index] switch
            {
                '-' => '+',
                '_' => '/',
                var character when IsBase64UrlCharacter(character) => character,
                _ => '\0',
            };

            if (base64[index] == '\0')
            {
                return false;
            }
        }

        base64[TokenLength] = '=';

        return Convert.TryFromBase64Chars(base64, destination, out var written)
            && written == TokenBytes
            && string.Equals(ToBase64Url(destination), value, StringComparison.Ordinal);
    }

    private static bool IsBase64UrlCharacter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
}
