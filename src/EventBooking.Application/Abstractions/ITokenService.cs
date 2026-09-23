namespace EventBooking.Application.Abstractions;

/// <summary>The clear-text token goes in the email link; only its hash is ever stored.</summary>
/// <param name="Token">The token.</param>
/// <param name="TokenHash">The token hash.</param>
public sealed record IssuedToken(string Token, string TokenHash);

/// <summary>Defines itoken service for the current use case.</summary>
public interface ITokenService
{
    /// <summary>Issues a signed, single-use token bound to one invite or booking.</summary>
    /// <param name="entityId">The entity id.</param>
    IssuedToken Issue(Guid entityId);

    /// <summary>Verifies the signature and recovers the identifier. False if the token is malformed
    /// or the signature does not verify.</summary>
    /// <param name="token">The token.</param>
    /// <param name="entityId">The entity id.</param>
    bool TryRead(string? token, out Guid entityId);

    /// <summary>The hash to compare against the stored value once the signature has verified.</summary>
    /// <param name="token">The token.</param>
    string Hash(string token);
}
