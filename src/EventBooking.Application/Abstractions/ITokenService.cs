namespace EventBooking.Application.Abstractions;

/// <summary>What an attendee link is for. Part of the signed payload, so a book token can never
/// be replayed as a manage token for the same identifier.</summary>
public enum TokenPurpose
{
    /// <summary>The link that opens an invite's options.</summary>
    Book = 1,

    /// <summary>The link that views, cancels or reschedules a booking.</summary>
    Manage = 2,

    /// <summary>The link that confirms an anonymous self-registration request.</summary>
    Registration = 3,
}

/// <summary>What a valid token names: the row to load and the version it must still be on.</summary>
/// <param name="Purpose">The purpose.</param>
/// <param name="EntityId">The invite or booking identifier.</param>
/// <param name="Version">The token version the link was issued against.</param>
public readonly record struct TokenReference(TokenPurpose Purpose, Guid EntityId, int Version);

/// <summary>
/// Deterministic attendee links (design 06). The token is reproducible from the row, so the
/// confirmation page and the confirmation email share one link and nothing derived from the token
/// is ever stored: the row keeps only the version counter that a link is revoked by incrementing.
/// </summary>
public interface ITokenService
{
    /// <summary>Issues the link for one purpose, identifier and version.</summary>
    /// <param name="purpose">The purpose.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="version">The current version counter on the row; one or more.</param>
    string Issue(TokenPurpose purpose, Guid entityId, int version);

    /// <summary>
    /// Verifies the signature in constant time and recovers what the token names, before any
    /// database access. False if the token is malformed or the signature does not verify.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="reference">What the token names, when it verifies.</param>
    bool TryRead(string? token, out TokenReference reference);
}
