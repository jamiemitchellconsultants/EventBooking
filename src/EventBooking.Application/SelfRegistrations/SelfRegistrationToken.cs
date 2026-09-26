using EventBooking.Application.Abstractions;

namespace EventBooking.Application.SelfRegistrations;

/// <summary>Signs and reads self-registration confirmation tokens through the HMAC service.</summary>
public static class SelfRegistrationToken
{
    /// <summary>Issues the confirmation token for one request and token version.</summary>
    /// <param name="tokens">The token service.</param>
    /// <param name="requestId">The request id.</param>
    /// <param name="tokenVersion">The row's token version.</param>
    public static string Issue(ITokenService tokens, Guid requestId, int tokenVersion)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return tokens.Issue(TokenPurpose.Registration, requestId, tokenVersion);
    }

    /// <summary>Reads a confirmation token, accepting only the registration purpose.</summary>
    /// <param name="tokens">The token service.</param>
    /// <param name="token">The token.</param>
    /// <param name="requestId">The request id, when the token is a registration token.</param>
    /// <param name="tokenVersion">The token version, when the token is a registration token.</param>
    /// <returns>Whether the token is a well-signed registration token.</returns>
    public static bool TryRead(
        ITokenService tokens, string? token, out Guid requestId, out int tokenVersion)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        requestId = Guid.Empty;
        tokenVersion = 0;
        if (!tokens.TryRead(token, out var reference)
            || reference.Purpose != TokenPurpose.Registration)
            return false;
        requestId = reference.EntityId;
        tokenVersion = reference.Version;
        return true;
    }
}
