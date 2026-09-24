using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Notifications;

/// <summary>Retries one failed delivery by staging a fresh pending row for the dispatcher.</summary>
/// <param name="deliveries">The deliveries.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
public sealed class RetryEmailHandler(
    IEmailDeliveryRepository deliveries,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<RetryEmailOutcome>> HandleAsync(
        RetryEmailCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
        if (authorized.IsFailure) return Result<RetryEmailOutcome>.Failure(authorized.Error);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var old = await deliveries.LockForUpdateAsync(command.EmailLogId, ct);
        if (old is null || old.AttendeeId != command.AttendeeId)
            return Result<RetryEmailOutcome>.Failure(Error.NotFound("No such delivery."));
        if (old.Status != EmailStatus.Failed)
            return Result<RetryEmailOutcome>.Failure(
                Error.Conflict($"Only a failed delivery can be retried, not {old.Status}."));

        var fresh = EmailLog.RecordPending(Guid.NewGuid(), old.AttendeeId, old.TemplateName,
            clock.UtcNow, old.InviteId, old.BookingId, old.EventId);
        deliveries.Add(fresh);
        old.MarkResolved(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<RetryEmailOutcome>.Success(new RetryEmailOutcome(fresh.Id));
    }

    /// <summary>Retries the attendee's newest failed delivery, resolving it from the attendee.</summary>
    /// <param name="command">The attendee to retry for.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<RetryEmailOutcome>> HandleAsync(
        RetryNewestEmailCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageAttendees, null, ct);
        if (authorized.IsFailure) return Result<RetryEmailOutcome>.Failure(authorized.Error);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var newest = await deliveries.LockLatestForAttendeeAsync(command.AttendeeId, ct);
        if (newest is null)
            return Result<RetryEmailOutcome>.Failure(
                Error.NotFound("This attendee has no delivery to retry."));
        if (newest.Status != EmailStatus.Failed)
            return Result<RetryEmailOutcome>.Failure(
                Error.Conflict($"Only a failed delivery can be retried, not {newest.Status}."));

        var fresh = EmailLog.RecordPending(Guid.NewGuid(), newest.AttendeeId, newest.TemplateName,
            clock.UtcNow, newest.InviteId, newest.BookingId, newest.EventId);
        deliveries.Add(fresh);
        newest.MarkResolved(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<RetryEmailOutcome>.Success(new RetryEmailOutcome(fresh.Id));
    }
}

/// <summary>Requests a staff-authorized retry of one failed delivery.</summary>
/// <param name="StaffUserId">The coordinator requesting the retry.</param>
/// <param name="AttendeeId">The attendee the delivery belongs to.</param>
/// <param name="EmailLogId">The failed delivery to retry.</param>
public sealed record RetryEmailCommand(Guid StaffUserId, Guid AttendeeId, Guid EmailLogId);

/// <summary>Requests a staff-authorized retry of the attendee's newest failed delivery.</summary>
/// <param name="StaffUserId">The coordinator requesting the retry.</param>
/// <param name="AttendeeId">The attendee the delivery belongs to.</param>
public sealed record RetryNewestEmailCommand(Guid StaffUserId, Guid AttendeeId);

/// <summary>Reports the staged replacement delivery.</summary>
/// <param name="EmailLogId">The new pending delivery identifier.</param>
public sealed record RetryEmailOutcome(Guid EmailLogId);
