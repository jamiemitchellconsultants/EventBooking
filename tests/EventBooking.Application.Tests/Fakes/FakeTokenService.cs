using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;

namespace EventBooking.Application.Tests.Fakes;

/// <summary>
/// A deterministic stand-in for the real HMAC service in Task 50. Tokens remain readable enough to
/// recover their entity identifiers, while hashes model the opaque values persisted by the system.
/// </summary>
public sealed class FakeTokenService : ITokenService
{
    public IssuedToken Issue(Guid entityId)
    {
        var token = $"token-for-{entityId:N}";
        return new IssuedToken(token, Hash(token));
    }

    public bool TryRead(string? token, out Guid entityId)
    {
        entityId = Guid.Empty;

        if (token is null || !token.StartsWith("token-for-", StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParseExact(token["token-for-".Length..], "N", out entityId);
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
