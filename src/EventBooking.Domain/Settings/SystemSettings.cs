using EventBooking.Domain.Common;

namespace EventBooking.Domain.Settings;

/// <summary>Admin-configurable settings. Exactly one row exists, with a constant identifier.</summary>
public sealed class SystemSettings
{
    /// <summary>Defines singleton id for the current use case.</summary>
    public const int SingletonId = 1;

    private SystemSettings()
    {
    }

    /// <summary>Defines id for the current use case.</summary>
    public int Id { get; private set; } = SingletonId;

    /// <summary>Defines invite expiry days for the current use case.</summary>
    public int InviteExpiryDays { get; private set; }

    /// <summary>Defines max auto retry count for the current use case.</summary>
    public int MaxAutoRetryCount { get; private set; }

    /// <summary>How many event options an invitation offers (design 07: 1 to 5, default 3).</summary>
    public int InviteOptionCount { get; private set; }

    /// <summary>The optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Defines create default for the current use case.</summary>
    public static SystemSettings CreateDefault() =>
        new() { Id = SingletonId, InviteExpiryDays = 7, MaxAutoRetryCount = 2, InviteOptionCount = 3, Version = 1 };

    /// <summary>Applies new settings, or refuses them all (FR-1.9; design 08 bounds).</summary>
    /// <param name="inviteExpiryDays">How long an invitation stays usable: 1 to 60.</param>
    /// <param name="maxAutoRetryCount">How many automatic re-issues an invitation gets: 0 to 10.</param>
    /// <param name="inviteOptionCount">How many options an invitation offers: 1 to 5.</param>
    public void Update(int inviteExpiryDays, int maxAutoRetryCount, int inviteOptionCount)
    {
        // Validate everything before mutating anything, so a rejected update changes nothing.
        Guard.Against(
            inviteExpiryDays is < 1 or > 60, "inviteExpiryDays must be between 1 and 60.");
        Guard.Against(
            maxAutoRetryCount is < 0 or > 10, "maxAutoRetryCount must be between 0 and 10.");
        Guard.Against(
            inviteOptionCount is < 1 or > 5, "inviteOptionCount must be between 1 and 5.");

        InviteExpiryDays = inviteExpiryDays;
        MaxAutoRetryCount = maxAutoRetryCount;
        InviteOptionCount = inviteOptionCount;
        Version++;
    }
}
