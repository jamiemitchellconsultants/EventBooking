using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;
using EventBooking.Domain.SelfRegistrations;

namespace EventBooking.Application.SelfRegistrations;

/// <summary>
/// Expires lapsed pending requests and purges terminal ones past retention, with their
/// confirmation email rows. Personal data (name, email) lives only on the request row,
/// so the purge removes it; attendees, bookings and audit rows stay.
/// </summary>
/// <param name="groups">The event groups.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit logger.</param>
/// <param name="clock">The clock.</param>
public sealed class SelfRegistrationMaintenance(
    IEventGroupRepository groups,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Whether a request has been terminal for at least thirty days.</summary>
    /// <param name="request">The request.</param>
    /// <param name="now">The current instant.</param>
    public static bool IsDueForDeletion(PendingRegistration request, DateTimeOffset now) =>
        (request.Status is SelfRegistrationStatus.Confirmed or SelfRegistrationStatus.Expired)
        && request.TerminalAt is { } terminal && terminal.AddDays(30) <= now;

    /// <summary>Runs one maintenance pass: expire, then purge batch by batch.</summary>
    /// <param name="ct">The cancellation token.</param>
    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        foreach (var request in await groups.ListExpiredPendingAsync(now, ct))
        {
            if (request.Expire(now))
                audit.Record(AuditEntityTypes.SelfRegistration, request.RequestId,
                    AuditAction.SelfRegistrationExpired, ActorType.System, null,
                    $"status {request.Status}");
        }

        await unitOfWork.SaveChangesAsync(ct);

        // Each batch is only staged by the repository; saving it is what lets the next read
        // see the remaining rows instead of the same ones again.
        while (await groups.DeleteTerminalBeforeAsync(now.AddDays(-30), ct) > 0)
            await unitOfWork.SaveChangesAsync(ct);
    }
}
