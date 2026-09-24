// src/EventBooking.Application/Abstractions/IClock.cs (complete)
namespace EventBooking.Application.Abstractions;

/// <summary>The testable source of the current UTC instant.</summary>
public interface IClock
{
    /// <summary>Gets the current UTC instant.</summary>
    DateTimeOffset UtcNow { get; }
}
