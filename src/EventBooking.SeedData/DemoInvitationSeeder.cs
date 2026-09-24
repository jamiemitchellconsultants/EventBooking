using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Events;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Issues demo invitations after the database seed, preserving existing attendee journeys.</summary>
/// <param name="database">Reads invitation history without changing it directly.</param>
/// <param name="attendees">Resolves the demo recipients by their seeded email address.</param>
/// <param name="events">Finds already-imported windows without restoring their capacity.</param>
/// <param name="trigger">Issues initial invitations; sending goes through the dispatcher.</param>
/// <param name="retry">Restages failed messages for the dispatcher.</param>
/// <param name="flush">Sends staged messages inline so the seed reports real delivery.</param>
/// <param name="clock">Determines future transitional-location dates and invitation usability.</param>
public sealed class DemoInvitationSeeder(
    EventBookingDbContext database,
    IAttendeeRepository attendees,
    IEventRepository events,
    InviteAttendeeHandler trigger,
    RetryEmailHandler retry,
    OutboxDispatcher flush,
    IClock clock)
{
    /// <summary>Gets or sets a progress sink; messages omit raw tokens, URLs and email bodies.</summary>
    public TextWriter Progress { get; set; } = TextWriter.Null;

    /// <summary>Imports demo availability and sends missing or recoverable demo invitations.</summary>
    /// <param name="cancellationToken">Cancels persistence and provider calls.</param>
    /// <returns>The count of messages successfully sent or retried during this run.</returns>
    /// <exception cref="SeedException">Dates, attendee state, issuance or delivery prevent completion.</exception>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await EnsureEventsAsync(cancellationToken);
        var recipients = DemoSeedSpec.Attendees()
            .Where(c => c.Journey == DemoAttendeeJourney.Unbooked)
            .GroupBy(c => c.AttendeeGroupCode)
            .Select(g => g.First()).ToList();
        if (recipients.Count != 5)
            throw new SeedException("Demo invitations require one Unbooked example per Attendee Group.");
        var sent = 0;
        foreach (var spec in recipients)
        {
            var attendee = await attendees.GetByEmailAsync(spec.Email, cancellationToken)
                ?? throw new SeedException($"Seed attendee is missing: {spec.Email}.");
            if (attendee.Status is AttendeeStatus.Booked)
            {
                Report($"Preserved existing journey: {spec.Email}.");
                continue;
            }
            if (await database.Invites.AnyAsync(i => i.AttendeeId == attendee.Id, cancellationToken))
            {
                var pending = await database.Invites.AsNoTracking().SingleOrDefaultAsync(
                    i => i.AttendeeId == attendee.Id && i.Status == InviteStatus.Pending
                        && i.RecoveryOfBookingId == null, cancellationToken);
                if (pending is null || !pending.IsUsableAt(clock.UtcNow))
                {
                    Report($"Preserved invitation history: {spec.Email}; use Coordinator actions or an explicit reseed.");
                    continue;
                }
                // Scope to the current Invite: an unresolved delivery for an older,
                // superseded Invite must not hide a successful Coordinator replacement.
                // Resolved attempts no longer describe the effective delivery outcome.
                var previous = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.AttendeeId == attendee.Id && e.InviteId == pending.Id
                        && e.TemplateName == EmailTemplate.AttendeeInvite && e.Status != EmailStatus.Resolved)
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (previous is null)
                    throw new SeedException($"Pending demo invitation has no matching delivery: {spec.Email}.");
                if (previous.Status is not EmailStatus.Failed and not EmailStatus.Pending)
                {
                    Report($"Invitation already delivered: {spec.Email}.");
                    continue;
                }
                // Do not accidentally touch another invitation/template from a
                // mutated demo: the attendee's latest outstanding row must be this one.
                var retryTarget = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.AttendeeId == attendee.Id
                        && (e.Status == EmailStatus.Failed || e.Status == EmailStatus.Pending))
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (retryTarget?.Id != previous.Id)
                    throw new SeedException($"Another outstanding delivery needs Coordinator review: {spec.Email}.");
                if (previous.Status == EmailStatus.Failed)
                {
                    var retried = await retry.HandleAsync(
                        new RetryEmailCommand(
                            DemoSeedSpec.CoordinatorUserId(), attendee.Id, previous.Id),
                        cancellationToken);
                    if (retried.IsFailure)
                        throw new SeedException($"Demo email retry for {spec.Email} failed: {retried.Error}.");
                    await RequireSentAsync(retried.Value.EmailLogId, spec.Email, cancellationToken);
                }
                else
                {
                    // A pending row held under a live claim belongs to a running
                    // dispatcher; anything older is flushed directly, since a pending
                    // row is already the delivery and needs no retry row.
                    if (previous.ClaimedAt is not null
                        && clock.UtcNow - previous.ClaimedAt.Value < ClaimQuery.ClaimLease)
                        throw new SeedException(
                            $"Demo invitation is already being delivered for {spec.Email}. Rerun later.");
                    await RequireSentAsync(previous.Id, spec.Email, cancellationToken);
                }
            }
            else
            {
                if (attendee.Status is not AttendeeStatus.NotYetInvited
                    and not AttendeeStatus.AwaitingAvailability)
                    throw new SeedException($"Demo attendee has unexpected invitation state: {spec.Email}.");
                var issued = await trigger.HandleAsync(
                    new InviteAttendeeCommand(
                        DemoSeedSpec.CoordinatorUserId(), attendee.Id,
                        [TransitionalLocation.Id]),
                    cancellationToken);
                if (issued.IsFailure)
                    throw new SeedException($"Demo invitation for {spec.Email} failed: {issued.Error}.");
                // Issuing only stages the email; the dispatcher sends the staged
                // delivery. A fresh attendee has exactly one pending delivery.
                var staged = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.AttendeeId == attendee.Id
                        && e.TemplateName == EmailTemplate.AttendeeInvite
                        && e.Status == EmailStatus.Pending)
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (staged is null)
                    throw new SeedException($"Demo invitation staged no delivery: {spec.Email}.");
                await RequireSentAsync(staged.Id, spec.Email, cancellationToken);
            }
            sent++;
            Report($"Invitation delivered: {spec.Email}.");
        }
        return sent;
    }

    private async Task EnsureEventsAsync(CancellationToken cancellationToken)
    {
        var dates = new[] { 3, 6, 9 }.Select(offset => DemoSeedSpec.AnchorDate().AddDays(offset)).ToArray();
        if (dates.Any(date => date <= clock.TodayAtTransitionalLocation))
            throw new SeedException("Demo invitation dates are stale. Use --reanchor for a fresh dataset; "
                + "use --reseed --reanchor only when deliberately resetting the demo.");
        var existing = (await events.ListAllAsync(cancellationToken))
            .Select(eventItem => (eventItem.Window.Date, eventItem.Window.StartTime)).ToHashSet();
        var missing = dates.Where(date => !existing.Contains((date, new TimeOnly(11, 0)))).ToList();
        if (missing.Count == 0) return;
        foreach (var date in missing)
            DemoEventFactory.Create(database, Guid.NewGuid(), new EventWindow(date, new TimeOnly(11, 0), 240),
                AppointmentTypeIds.All.ToDictionary(type => type, _ => 20), clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);
        Report($"Invitation demo events created: {missing.Count} with accepted proposals.");
    }

    private async Task RequireSentAsync(Guid deliveryId, string email, CancellationToken ct)
    {
        await flush.DispatchOnceAsync(ct);
        var status = await database.EmailLogs.AsNoTracking()
            .Where(e => e.Id == deliveryId).Select(e => e.Status).SingleAsync(ct);
        if (status != EmailStatus.Sent)
            throw DeliveryFailed(email);
    }

    private static SeedException DeliveryFailed(string recipient) => new(
        $"Demo invitation delivery failed for {recipient}. Check Mailpit SMTP and rerun; "
        + "the committed invitation will be retried.");

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");
}
