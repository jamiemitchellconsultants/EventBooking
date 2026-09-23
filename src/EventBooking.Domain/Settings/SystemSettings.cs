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

    /// <summary>Defines create default for the current use case.</summary>
    public static SystemSettings CreateDefault() =>
        new() { Id = SingletonId, InviteExpiryDays = 4, MaxAutoRetryCount = 2 };

    /// <summary>Defines update for the current use case.</summary>
    /// <param name="inviteExpiryDays">The invite expiry days.</param>
    /// <param name="maxAutoRetryCount">The max auto retry count.</param>
    public void Update(int inviteExpiryDays, int maxAutoRetryCount)
    {
        // Validate both before mutating either, so a rejected update changes nothing.
        var days = Guard.Positive(inviteExpiryDays, "inviteExpiryDays");
        var retries = Guard.NotNegative(maxAutoRetryCount, "maxAutoRetryCount");

        InviteExpiryDays = days;
        MaxAutoRetryCount = retries;
    }
}
