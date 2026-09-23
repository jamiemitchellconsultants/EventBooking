using EventBooking.Domain.Common;

namespace EventBooking.Domain.Slots;

/// <summary>
/// The 4-hour candidate-facing window. Duration is fixed by the domain, so only the date and the
/// start time are ever stored; the end time is always derived.
/// </summary>
public sealed record SlotWindow : IComparable<SlotWindow>
{
    /// <summary>Defines duration for the current use case.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromHours(4);

    /// <summary>Defines slot window for the current use case.</summary>
    /// <param name="date">The date.</param>
    /// <param name="startTime">The start time.</param>
    public SlotWindow(DateOnly date, TimeOnly startTime)
    {
        Guard.Against(
            startTime.ToTimeSpan() + Duration > TimeSpan.FromHours(24),
            "startTime must leave room for the full 4-hour window on the same day.");

        Date = date;
        StartTime = startTime;
    }

    /// <summary>Defines date for the current use case.</summary>
    public DateOnly Date { get; }

    /// <summary>Defines start time for the current use case.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>Defines end time for the current use case.</summary>
    public TimeOnly EndTime => StartTime.Add(Duration);

    /// <summary>Defines starts after for the current use case.</summary>
    /// <param name="today">The today.</param>
    public bool StartsAfter(DateOnly today) => Date > today;

    /// <summary>Defines compare to for the current use case.</summary>
    /// <param name="other">The other.</param>
    public int CompareTo(SlotWindow? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byDate = Date.CompareTo(other.Date);
        return byDate != 0 ? byDate : StartTime.CompareTo(other.StartTime);
    }

    /// <summary>Defines to string for the current use case.</summary>
    public override string ToString() =>
        $"{Date:yyyy-MM-dd} {StartTime:HH\\:mm}-{EndTime:HH\\:mm}";
}
