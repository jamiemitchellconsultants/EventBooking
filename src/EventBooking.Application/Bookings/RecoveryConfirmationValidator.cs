using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Bookings;

/// <summary>Revalidates a Pending recovery Invite snapshot against locked journey state.</summary>
public sealed class RecoveryConfirmationValidator
{
    /// <summary>Revalidates a Pending recovery Invite snapshot against locked journey state.</summary>
    /// <param name="invite">The Pending recovery Invite being confirmed.</param>
    /// <param name="currentRequirementTypeIds">The attendee's current derived requirement set.</param>
    /// <param name="attempts">Non-cancelled attempts across the journey, in any order.</param>
    /// <param name="typesAlreadyCoveredByAnotherRecovery">Types another recovery already covers.</param>
    /// <returns>The revalidated snapshot, or a stale-snapshot failure.</returns>
    public Result<IReadOnlyList<Guid>> Validate(
        Invite invite,
        IReadOnlyCollection<Guid> currentRequirementTypeIds,
        IReadOnlyCollection<RecoveryAttempt> attempts,
        IReadOnlyCollection<Guid> typesAlreadyCoveredByAnotherRecovery)
    {
        if (invite.RecoveryOfBookingId is null)
        {
            return Result<IReadOnlyList<Guid>>.Failure(Error.RecoveryStateChanged(
                "The invite is not a recovery invite."));
        }

        var selected = new RecoveryRequirementSelector().Select(
            currentRequirementTypeIds, attempts, typesAlreadyCoveredByAnotherRecovery);

        if (!selected.SequenceEqual(invite.RequiredAppointmentTypeIds.Order()))
        {
            return Result<IReadOnlyList<Guid>>.Failure(Error.RecoveryStateChanged(
                "The recovery snapshot no longer matches current eligibility."));
        }

        return Result<IReadOnlyList<Guid>>.Success(selected);
    }

    /// <summary>Builds non-cancelled journey attempts for the selector, in any order.</summary>
    /// <param name="journey">The original and direct recovery Bookings.</param>
    /// <param name="rows">The appointments owned by that journey.</param>
    /// <returns>One attempt per appointment outside cancelled Bookings.</returns>
    public static IReadOnlyList<RecoveryAttempt> BuildAttempts(
        IReadOnlyList<Booking> journey,
        IReadOnlyList<BookingAppointment> rows)
    {
        var createdByBooking = journey.ToDictionary(booking => booking.Id);
        var cancelled = journey
            .Where(booking => booking.Status == BookingStatus.Cancelled)
            .Select(booking => booking.Id)
            .ToHashSet();

        return rows
            .Where(row => !cancelled.Contains(row.BookingId) && createdByBooking.ContainsKey(row.BookingId))
            .Select(row => new RecoveryAttempt(
                row.Id,
                row.AppointmentTypeId,
                row.Status,
                createdByBooking[row.BookingId].CreatedAt))
            .ToList();
    }
}
