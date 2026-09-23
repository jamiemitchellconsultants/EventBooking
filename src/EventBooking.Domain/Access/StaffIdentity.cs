using EventBooking.Domain.Common;

namespace EventBooking.Domain.Access;

/// <summary>
/// The application mirror pairing a provider-assigned staff identity with the immutable enterprise
/// staff number carried by its validated token.
/// </summary>
public sealed class StaffIdentity
{
    private StaffIdentity()
    {
        StaffId = null!;
    }

    /// <summary>Gets the provider-assigned identity used as the authorization aggregate key.</summary>
    public Guid StaffUserId { get; private set; }

    /// <summary>Gets the enterprise staff number learned from the identity provider.</summary>
    public StaffId StaffId { get; private set; }

    /// <summary>
    /// Gets the human-readable name mirrored from the identity provider's token name claim, or
    /// null when no name has been observed. Presentation data only: its absence never blocks a
    /// request and it never affects authorization.
    /// </summary>
    public string? DisplayName { get; private set; }

    /// <summary>Gets the approximate time this identity was most recently recorded.</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Creates the mirror row for a validated identity pair.</summary>
    /// <param name="staffUserId">The non-empty provider-assigned identity.</param>
    /// <param name="staffId">The immutable enterprise staff number.</param>
    /// <param name="displayName">The observed provider name, or null when the token carries none.</param>
    /// <param name="lastSeenAt">The time the pair was observed.</param>
    /// <returns>A valid staff identity mirror.</returns>
    public static StaffIdentity Create(
        Guid staffUserId,
        StaffId staffId,
        string? displayName,
        DateTimeOffset lastSeenAt)
    {
        Guard.Against(staffUserId == Guid.Empty, "staffUserId must not be empty.");
        Guard.Against(staffId is null, "staffId must not be null.");
        return new StaffIdentity
        {
            StaffUserId = staffUserId,
            StaffId = staffId!,
            DisplayName = displayName,
            LastSeenAt = lastSeenAt,
        };
    }

    /// <summary>
    /// Advances the approximate last-observed time without changing either identity, and
    /// overwrites the mirrored name with whatever the latest token carried — including back to
    /// null when it carried no name.
    /// </summary>
    /// <param name="displayName">The observed provider name, or null when the token carries none.</param>
    /// <param name="seenAt">A time no earlier than the current observation.</param>
    public void MarkSeen(string? displayName, DateTimeOffset seenAt)
    {
        Guard.Against(seenAt < LastSeenAt, "lastSeenAt must not move backwards.");
        DisplayName = displayName;
        LastSeenAt = seenAt;
    }
}
