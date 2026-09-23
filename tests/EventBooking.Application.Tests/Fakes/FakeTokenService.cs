using EventBooking.Application.Abstractions;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>
/// A readable stand-in for the HMAC service. It carries the same three values the real token
/// carries — purpose, identifier and version — so a test can forge a stale or mismatched link
/// without reproducing the signature.
/// </summary>
public sealed class FakeTokenService : ITokenService
{
    public string Issue(TokenPurpose purpose, Guid entityId, int version) =>
        $"{Prefix(purpose)}{entityId:N}-v{version}";

    public bool TryRead(string? token, out TokenReference reference)
    {
        reference = default;

        if (token is null)
        {
            return false;
        }

        foreach (var purpose in Enum.GetValues<TokenPurpose>())
        {
            var prefix = Prefix(purpose);
            if (!token.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var parts = token[prefix.Length..].Split("-v");
            if (parts.Length != 2
                || !Guid.TryParseExact(parts[0], "N", out var entityId)
                || !int.TryParse(parts[1], out var version)
                || version < 1)
            {
                return false;
            }

            reference = new TokenReference(purpose, entityId, version);
            return true;
        }

        return false;
    }

    private static string Prefix(TokenPurpose purpose) => $"{purpose.ToString().ToLowerInvariant()}-token-for-";
}
