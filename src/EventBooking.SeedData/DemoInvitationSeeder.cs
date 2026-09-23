using System.Globalization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Slots;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Issues demo invitations after the database seed, preserving existing candidate journeys.</summary>
/// <param name="database">Reads invitation history without changing it directly.</param>
/// <param name="candidates">Resolves the demo recipients by their seeded email address.</param>
/// <param name="slots">Finds already-imported windows without restoring their capacity.</param>
/// <param name="importSlots">Imports missing demo windows under Coordinator authorization.</param>
/// <param name="trigger">Creates initial invitations and sends only after committing.</param>
/// <param name="retry">Retries outstanding messages under the existing claim and token rules.</param>
/// <param name="clock">Determines future head-office dates and invitation usability.</param>
public sealed class DemoInvitationSeeder(
    EventBookingDbContext database,
    ICandidateRepository candidates,
    IConfirmedSlotRepository slots,
    ImportConfirmedSlotsHandler importSlots,
    TriggerInviteHandler trigger,
    RetryEmailHandler retry,
    IClock clock)
{
    /// <summary>Gets or sets a progress sink; messages omit raw tokens, URLs and email bodies.</summary>
    public TextWriter Progress { get; set; } = TextWriter.Null;

    /// <summary>Imports demo availability and sends missing or recoverable demo invitations.</summary>
    /// <param name="cancellationToken">Cancels persistence and provider calls.</param>
    /// <returns>The count of messages successfully sent or retried during this run.</returns>
    /// <exception cref="SeedException">Dates, candidate state, issuance or delivery prevent completion.</exception>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await EnsureSlotsAsync(cancellationToken);
        var recipients = DemoSeedSpec.Candidates()
            .Where(c => c.Journey == DemoCandidateJourney.Unbooked)
            .GroupBy(c => c.EmployeeGroupCode)
            .Select(g => g.First()).ToList();
        if (recipients.Count != 5)
            throw new SeedException("Demo invitations require one Unbooked example per Employee Group.");
        var sent = 0;
        foreach (var spec in recipients)
        {
            var candidate = await candidates.GetByEmailAsync(spec.Email, cancellationToken)
                ?? throw new SeedException($"Seed candidate is missing: {spec.Email}.");
            if (candidate.Status is CandidateStatus.Booked)
            {
                Report($"Preserved existing journey: {spec.Email}.");
                continue;
            }
            if (await database.Invites.AnyAsync(i => i.CandidateId == candidate.Id, cancellationToken))
            {
                var pending = await database.Invites.AsNoTracking().SingleOrDefaultAsync(
                    i => i.CandidateId == candidate.Id && i.Status == InviteStatus.Pending
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
                    .Where(e => e.CandidateId == candidate.Id && e.InviteId == pending.Id
                        && e.TemplateName == EmailTemplate.CandidateInvite && e.Status != EmailStatus.Resolved)
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (previous is null)
                    throw new SeedException($"Pending demo invitation has no matching delivery: {spec.Email}.");
                if (previous.Status is not EmailStatus.Failed and not EmailStatus.Pending)
                {
                    Report($"Invitation already delivered: {spec.Email}.");
                    continue;
                }
                // The retry handler selects candidate-wide outstanding work. Do not
                // accidentally retry another invitation/template from a mutated demo.
                var retryTarget = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.CandidateId == candidate.Id
                        && (e.Status == EmailStatus.Failed || e.Status == EmailStatus.Pending))
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (retryTarget?.Id != previous.Id)
                    throw new SeedException($"Another outstanding delivery needs Coordinator review: {spec.Email}.");
                var retried = await retry.HandleAsync(
                    new RetryEmailCommand(DemoSeedSpec.CoordinatorUserId(), candidate.Id), cancellationToken);
                if (retried.IsFailure)
                    throw new SeedException($"Demo email retry for {spec.Email} failed: {retried.Error}.");
                if (retried.Value.DeliveryStatus != EmailStatus.Sent.ToString())
                    throw DeliveryFailed(spec.Email);
            }
            else
            {
                if (candidate.Status is not CandidateStatus.NotYetInvited
                    and not CandidateStatus.AwaitingAvailability)
                    throw new SeedException($"Demo candidate has unexpected invitation state: {spec.Email}.");
                var issued = await trigger.HandleAsync(
                    new TriggerInviteCommand(DemoSeedSpec.CoordinatorUserId(), candidate.Id), cancellationToken);
                if (issued.IsFailure)
                    throw new SeedException($"Demo invitation for {spec.Email} failed: {issued.Error}.");
                if (!issued.Value.Invited)
                    throw new SeedException($"Three future slots with capacity are required for {spec.Email}.");
                if (!issued.Value.EmailSent)
                    throw DeliveryFailed(spec.Email);
            }
            sent++;
            Report($"Invitation delivered: {spec.Email}.");
        }
        return sent;
    }

    private async Task EnsureSlotsAsync(CancellationToken cancellationToken)
    {
        var dates = new[] { 3, 6, 9 }.Select(offset => DemoSeedSpec.AnchorDate().AddDays(offset)).ToArray();
        if (dates.Any(date => date <= clock.TodayAtHeadOffice))
            throw new SeedException("Demo invitation dates are stale. Use --reanchor for a fresh dataset; "
                + "use --reseed --reanchor only when deliberately resetting the demo.");
        var existing = (await slots.ListAllAsync(cancellationToken))
            .Select(slot => (slot.Window.Date, slot.Window.StartTime)).ToHashSet();
        var missing = dates.Where(date => !existing.Contains((date, new TimeOnly(11, 0)))).ToList();
        if (missing.Count == 0) return;
        var csv = "date,startTime,DAT,MED,UNI\n" + string.Join("\n", missing.Select(date =>
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ",11:00,20,20,20"));
        var imported = await importSlots.HandleAsync(
            new ImportConfirmedSlotsCommand(DemoSeedSpec.CoordinatorUserId(), csv), cancellationToken);
        if (imported.IsFailure)
            throw new SeedException($"Demo invitation slot import failed: {imported.Error}.");
        if (!imported.Value.Accepted)
            throw new SeedException("Demo invitation slot import rejected: "
                + string.Join("; ", imported.Value.Errors.Select(error => error.Message)));
        Report($"Invitation demo slots imported: {imported.Value.ImportedCount}.");
    }

    private static SeedException DeliveryFailed(string recipient) => new(
        $"Demo invitation delivery failed for {recipient}. Check Mailpit SMTP and rerun; "
        + "the committed invitation will be retried.");

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");
}
