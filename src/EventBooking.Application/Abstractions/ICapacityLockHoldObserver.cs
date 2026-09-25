// src/EventBooking.Application/Abstractions/ICapacityLockHoldObserver.cs (complete)
namespace EventBooking.Application.Abstractions;

/// <summary>Observes how long a confirmation holds acquired capacity locks.</summary>
public interface ICapacityLockHoldObserver
{
    /// <summary>Records one post-lock interval, after the transaction is released.</summary>
    /// <param name="elapsed">The time from the event capacity lock through release.</param>
    void Record(TimeSpan elapsed);
}
