using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Recovery;

/// <summary>
/// Concludes one recovery booking whose appointments have all reached an outcome. Driven
/// by the Task 19 sweep, not by a person, so it demands no capability and audits as
/// System.
/// </summary>
/// <param name="bookings">The bookings.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class ConcludeRecoveryHandler(
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Handles the command.</summary>
    /// <param name="bookingId">The recovery booking to conclude.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result> HandleAsync(Guid bookingId, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        var booking = await bookings.LockForUpdateAsync(bookingId, ct);
        if (booking is null) return Result.Failure(Error.NotFound("No such booking."));

        // Re-validated under the lock because the sweep listed this row without one.
        if (booking.IsOriginal)
            return Result.Failure(
                Error.Validation("Only a recovery booking is concluded this way."));
        if (booking.Status != BookingStatus.Active)
            return Result.Failure(
                Error.Validation($"The booking is {booking.Status} and cannot be concluded."));

        var rows = await appointments.ListForBookingAsync(booking.Id, ct);
        if (rows.Count == 0 || rows.Any(a =>
                a.Status is not (BookingAppointmentStatus.Completed
                    or BookingAppointmentStatus.NoShow)))
            return Result.Failure(Error.Validation("No appointments remain open."));

        try
        {
            booking.Conclude();
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.Booking,
            booking.Id,
            AuditAction.RecoveryBookingConcluded,
            ActorType.System,
            null,
            null);

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success();
    }
}
