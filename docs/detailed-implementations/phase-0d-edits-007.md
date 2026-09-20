# 00d — Retire direct event import, edits 7 (Task 3b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.SeedData/DemoInvitationSeeder.cs — 1/1

<!-- retirement-file: {"id":19,"file":"src/EventBooking.SeedData/DemoInvitationSeeder.cs","beforeSha":"c64bb47a1c778ceb4f5e2262c42e600062e967ec42fe88863a780397d969388e","afterSha":"097534f49749801b5a74c0cdb6387b4128f9e2f922547f81c449dbce0d3069db","side":"before","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Events;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Issues demo invitations after the database seed, preserving existing attendee journeys.</summary>
/// <param name="database">Reads invitation history without changing it directly.</param>
/// <param name="attendees">Resolves the demo recipients by their seeded email address.</param>
/// <param name="events">Finds already-imported windows without restoring their capacity.</param>
/// <param name="importEvents">Imports missing demo windows under Coordinator authorization.</param>
/// <param name="trigger">Creates initial invitations and sends only after committing.</param>
/// <param name="retry">Retries outstanding messages under the existing claim and token rules.</param>
/// <param name="clock">Determines future transitional-location dates and invitation usability.</param>
public sealed class DemoInvitationSeeder(
    EventBookingDbContext database,
    IAttendeeRepository attendees,
    IEventRepository events,
    ImportEventsHandler importEvents,
    TriggerInviteHandler trigger,
    RetryEmailHandler retry,
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
                // The retry handler selects attendee-wide outstanding work. Do not
                // accidentally retry another invitation/template from a mutated demo.
                var retryTarget = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.AttendeeId == attendee.Id
                        && (e.Status == EmailStatus.Failed || e.Status == EmailStatus.Pending))
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (retryTarget?.Id != previous.Id)
                    throw new SeedException($"Another outstanding delivery needs Coordinator review: {spec.Email}.");
                var retried = await retry.HandleAsync(
                    new RetryEmailCommand(DemoSeedSpec.CoordinatorUserId(), attendee.Id), cancellationToken);
                if (retried.IsFailure)
                    throw new SeedException($"Demo email retry for {spec.Email} failed: {retried.Error}.");
                if (retried.Value.DeliveryStatus != EmailStatus.Sent.ToString())
                    throw DeliveryFailed(spec.Email);
            }
            else
            {
                if (attendee.Status is not AttendeeStatus.NotYetInvited
                    and not AttendeeStatus.AwaitingAvailability)
                    throw new SeedException($"Demo attendee has unexpected invitation state: {spec.Email}.");
                var issued = await trigger.HandleAsync(
                    new TriggerInviteCommand(DemoSeedSpec.CoordinatorUserId(), attendee.Id), cancellationToken);
                if (issued.IsFailure)
                    throw new SeedException($"Demo invitation for {spec.Email} failed: {issued.Error}.");
                if (!issued.Value.Invited)
                    throw new SeedException($"Three future events with capacity are required for {spec.Email}.");
                if (!issued.Value.EmailSent)
                    throw DeliveryFailed(spec.Email);
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
        var csv = "date,startTime,DAT,MED,UNI\n" + string.Join("\n", missing.Select(date =>
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ",11:00,20,20,20"));
        var imported = await importEvents.HandleAsync(
            new ImportEventsCommand(DemoSeedSpec.CoordinatorUserId(), csv), cancellationToken);
        if (imported.IsFailure)
            throw new SeedException($"Demo invitation event import failed: {imported.Error}.");
        if (!imported.Value.Accepted)
            throw new SeedException("Demo invitation event import rejected: "
                + string.Join("; ", imported.Value.Errors.Select(error => error.Message)));
        Report($"Invitation demo events imported: {imported.Value.ImportedCount}.");
    }

    private static SeedException DeliveryFailed(string recipient) => new(
        $"Demo invitation delivery failed for {recipient}. Check Mailpit SMTP and rerun; "
        + "the committed invitation will be retried.");

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");
}
`````

## after — src/EventBooking.SeedData/DemoInvitationSeeder.cs — 1/1

<!-- retirement-file: {"id":19,"file":"src/EventBooking.SeedData/DemoInvitationSeeder.cs","beforeSha":"c64bb47a1c778ceb4f5e2262c42e600062e967ec42fe88863a780397d969388e","afterSha":"097534f49749801b5a74c0cdb6387b4128f9e2f922547f81c449dbce0d3069db","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Events;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Issues demo invitations after the database seed, preserving existing attendee journeys.</summary>
/// <param name="database">Reads invitation history without changing it directly.</param>
/// <param name="attendees">Resolves the demo recipients by their seeded email address.</param>
/// <param name="events">Finds already-imported windows without restoring their capacity.</param>
/// <param name="trigger">Creates initial invitations and sends only after committing.</param>
/// <param name="retry">Retries outstanding messages under the existing claim and token rules.</param>
/// <param name="clock">Determines future transitional-location dates and invitation usability.</param>
public sealed class DemoInvitationSeeder(
    EventBookingDbContext database,
    IAttendeeRepository attendees,
    IEventRepository events,
    TriggerInviteHandler trigger,
    RetryEmailHandler retry,
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
                // The retry handler selects attendee-wide outstanding work. Do not
                // accidentally retry another invitation/template from a mutated demo.
                var retryTarget = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.AttendeeId == attendee.Id
                        && (e.Status == EmailStatus.Failed || e.Status == EmailStatus.Pending))
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (retryTarget?.Id != previous.Id)
                    throw new SeedException($"Another outstanding delivery needs Coordinator review: {spec.Email}.");
                var retried = await retry.HandleAsync(
                    new RetryEmailCommand(DemoSeedSpec.CoordinatorUserId(), attendee.Id), cancellationToken);
                if (retried.IsFailure)
                    throw new SeedException($"Demo email retry for {spec.Email} failed: {retried.Error}.");
                if (retried.Value.DeliveryStatus != EmailStatus.Sent.ToString())
                    throw DeliveryFailed(spec.Email);
            }
            else
            {
                if (attendee.Status is not AttendeeStatus.NotYetInvited
                    and not AttendeeStatus.AwaitingAvailability)
                    throw new SeedException($"Demo attendee has unexpected invitation state: {spec.Email}.");
                var issued = await trigger.HandleAsync(
                    new TriggerInviteCommand(DemoSeedSpec.CoordinatorUserId(), attendee.Id), cancellationToken);
                if (issued.IsFailure)
                    throw new SeedException($"Demo invitation for {spec.Email} failed: {issued.Error}.");
                if (!issued.Value.Invited)
                    throw new SeedException($"Three future events with capacity are required for {spec.Email}.");
                if (!issued.Value.EmailSent)
                    throw DeliveryFailed(spec.Email);
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
            DemoEventFactory.Create(database, Guid.NewGuid(), new EventWindow(date, new TimeOnly(11, 0)),
                AppointmentTypeIds.All.ToDictionary(type => type, _ => 20));
        await database.SaveChangesAsync(cancellationToken);
        Report($"Invitation demo events created: {missing.Count} with accepted proposals.");
    }

    private static SeedException DeliveryFailed(string recipient) => new(
        $"Demo invitation delivery failed for {recipient}. Check Mailpit SMTP and rerun; "
        + "the committed invitation will be retried.");

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");
}
`````

## before — src/EventBooking.SeedData/DemoSeeder.cs — 1/1

<!-- retirement-file: {"id":20,"file":"src/EventBooking.SeedData/DemoSeeder.cs","beforeSha":"c33ab24d5d5d28773e47df32dac20cdfec88adebef56977b7284d41f81becec8","afterSha":"be9d23decdec95ec1d76a33b50dffe691af6910a0db0bf004c72a226f299be7b","side":"before","part":1,"parts":1} -->

`````csharp
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;
using EventBooking.Application.Events;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Counts the deterministic rows created by one idempotent seed operation.</summary>
/// <param name="IdentitiesEnsured">The number of missing identity mirror rows inserted.</param>
/// <param name="ProfilesEnsured">The number of missing staff access profiles inserted.</param>
/// <param name="AgreedEventsImported">The number of agreed appointment events imported.</param>
/// <param name="ProposalsEnsured">The number of missing event proposals inserted.</param>
/// <param name="AcceptancesApplied">The number of proposal acceptances applied.</param>
/// <param name="AttendeesCreated">The number of attendees created.</param>
public sealed record SeedSummary(
    int IdentitiesEnsured,
    int ProfilesEnsured,
    int AgreedEventsImported,
    int ProposalsEnsured,
    int AcceptancesApplied,
    int AttendeesCreated)
{
    /// <summary>Gets the number of missing identity mirror rows inserted.</summary>
    public int IdentitiesEnsured { get; init; } = IdentitiesEnsured;

    /// <summary>Gets the number of missing staff access profiles inserted.</summary>
    public int ProfilesEnsured { get; init; } = ProfilesEnsured;

    /// <summary>Gets the number of agreed appointment events imported.</summary>
    public int AgreedEventsImported { get; init; } = AgreedEventsImported;

    /// <summary>Gets the number of missing event proposals inserted.</summary>
    public int ProposalsEnsured { get; init; } = ProposalsEnsured;

    /// <summary>Gets the number of proposal acceptances applied.</summary>
    public int AcceptancesApplied { get; init; } = AcceptancesApplied;

    /// <summary>Gets the number of attendees created.</summary>
    public int AttendeesCreated { get; init; } = AttendeesCreated;
}

public sealed class SeedException(string message) : Exception(message);

/// <summary>
/// Applies <see cref="DemoSeedSpec"/> through the application handlers so every domain rule
/// is enforced exactly as if staff had entered the data by hand. Re-runnable: existing rows
/// are matched by natural key (role id, event window, attendee email) and skipped, which is
/// also how filled-in headcount placeholders get applied on a later run.
/// Reseeding instead wipes every domain table first, restoring the fixed reference rows,
/// so a database mutated by a demo comes back to exactly the seed state.
/// </summary>
/// <param name="profiles">Provides persistence for application access profiles.</param>
/// <param name="identities">Provides persistence for the identity mirror.</param>
/// <param name="events">Provides persistence for confirmed appointment events.</param>
/// <param name="proposals">Provides persistence for event proposals.</param>
/// <param name="attendees">Provides persistence for attendees.</param>
/// <param name="groups">Resolves demo requirement sets to their Attendee Group.</param>
/// <param name="importEvents">Imports agreed appointment events from the canonical workbook.</param>
/// <param name="proposeEvent">Creates proposed appointment events.</param>
/// <param name="acceptProposal">Accepts proposed appointment events.</param>
/// <param name="saveAttendee">Creates attendees through the application workflow.</param>
/// <param name="unitOfWork">Commits tracked seed changes.</param>
/// <param name="clock">Supplies the observation time for identity mirror rows.</param>
/// <param name="database">Provides destructive reseed access to the application database.</param>
public sealed class DemoSeeder(
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IEventRepository events,
    IEventProposalRepository proposals,
    IAttendeeRepository attendees,
    IAttendeeGroupRepository groups,
    ImportEventsHandler importEvents,
    ProposeEventHandler proposeEvent,
    AcceptProposalHandler acceptProposal,
    SaveAttendeeHandler saveAttendee,
    IUnitOfWork unitOfWork,
    IClock clock,
    EventBookingDbContext database)
{
    /// <summary>
    /// Gets or sets the verbose progress sink. Defaults to <see cref="TextWriter.Null" />;
    /// the console host assigns <see cref="Console.Out" /> when --verbose is passed.
    /// </summary>
    public TextWriter Progress { get; set; } = TextWriter.Null;

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");

    private static IReadOnlyList<StaffProfileSpec> Staff() => DemoSeedSpec.Staff();

    private static IReadOnlyDictionary<Guid, Guid> ManagerForType() => DemoSeedSpec.ManagerForType();

    private static Guid AdminUserId() => DemoSeedSpec.AdminUserId();

    private static Guid CoordinatorUserId() => DemoSeedSpec.CoordinatorUserId();

    public async Task<SeedSummary> RunAsync(CancellationToken cancellationToken)
    {
        Report(
            $"Starting seed: {Staff().Count} staff, " +
            $"{DemoSeedSpec.AgreedEvents().Count} agreed events, " +
            $"{DemoSeedSpec.OpenProposals().Count} proposals, " +
            $"{DemoSeedSpec.Attendees().Count} attendees " +
            $"(anchor {DemoSeedSpec.AnchorDate():yyyy-MM-dd}" +
            $"{(DemoSeedSpec.AnchorOverridden ? ", reanchored" : "")}, " +
            $"today {clock.TodayAtTransitionalLocation:yyyy-MM-dd}).");
        var identitiesEnsured = await EnsureIdentitiesAsync(cancellationToken);
        var profilesEnsured = await EnsureProfilesAsync(cancellationToken);
        var agreedImported = await SeedAgreedEventsAsync(cancellationToken);
        var (proposalsEnsured, acceptancesApplied) = await SeedProposalsAsync(cancellationToken);
        var attendeesCreated = await SeedAttendeesAsync(cancellationToken);
        Report(
            $"Seed run finished: {identitiesEnsured} identities, {profilesEnsured} profiles, " +
            $"{agreedImported} agreed events, {proposalsEnsured} proposals, " +
            $"{acceptancesApplied} acceptances, {attendeesCreated} attendees.");

        return new SeedSummary(
            identitiesEnsured,
            profilesEnsured,
            agreedImported,
            proposalsEnsured,
            acceptancesApplied,
            attendeesCreated);
    }

    public async Task<SeedSummary> ReseedAsync(CancellationToken cancellationToken)
    {
        await WipeAsync(cancellationToken);
        return await RunAsync(cancellationToken);
    }

    private async Task WipeAsync(CancellationToken cancellationToken)
    {
        Report("Wiping domain tables (reseed)...");
        await database.Database.ExecuteSqlRawAsync(
            """
            DO $$
            DECLARE statements CURSOR FOR
                SELECT tablename FROM pg_tables
                WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory';
            BEGIN
                FOR statement IN statements LOOP
                    EXECUTE 'TRUNCATE TABLE ' || quote_ident(statement.tablename) || ' CASCADE;';
                END LOOP;
            END $$;
            """,
            cancellationToken);

        // The truncate above bypasses the change tracker, so drop every stale
        // tracked entity before seeding into the emptied tables.
        database.ChangeTracker.Clear();

        database.AppointmentTypes.AddRange(AppointmentType.CreateFixedSet());
        database.SystemSettings.Add(SystemSettings.CreateDefault());
        database.AttendeeGroups.AddRange(FixedAttendeeGroups());
        await database.SaveChangesAsync(cancellationToken);
        Report("Wipe complete; reference rows restored.");
    }

    /// <summary>
    /// The five change-controlled groups, matching the reference migration exactly. Task 17
    /// replaces demo attendee assignment with explicit groups and journeys.
    /// </summary>
    private static IReadOnlyList<AttendeeGroup> FixedAttendeeGroups() =>
    [
        AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]),
        AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
        AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]),
        AttendeeGroup.Define(
            AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]),
        AttendeeGroup.Define(
            AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
            "Ground Transport Services", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]),
    ];

    private async Task<int> EnsureProfilesAsync(CancellationToken cancellationToken)
    {
        var added = 0;
        var skipped = 0;
        foreach (var spec in Staff())
        {
            if (await profiles.GetAsync(spec.UserId, cancellationToken) is not null)
            {
                skipped++;
                continue;
            }

            profiles.Add(StaffAccessProfile.Create(
                spec.UserId, spec.Roles, spec.AppointmentTypeId));
            added++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        Report($"Staff profiles: {added} created, {skipped} already present.");
        return added;
    }

    private async Task<int> EnsureIdentitiesAsync(CancellationToken cancellationToken)
    {
        var existing = (await identities.ListAsync(cancellationToken))
            .Select(identity => identity.StaffUserId)
            .ToHashSet();
        var added = 0;
        foreach (var spec in Staff())
        {
            if (existing.Contains(spec.UserId))
            {
                continue;
            }

            await identities.UpsertAsync(
                spec.UserId, spec.StaffId, null, clock.UtcNow, cancellationToken);
            added++;
            Report($"Identity ensured: {spec.UserId}.");
        }

        Report($"Staff identities: {added} ensured, {existing.Count} already present.");
        return added;
    }

    private async Task<int> SeedAgreedEventsAsync(CancellationToken cancellationToken)
    {
        var existing = (await events.ListAllAsync(cancellationToken))
            .Select(s => (s.Window.Date, s.Window.StartTime))
            .ToHashSet();

        var missing = DemoSeedSpec.AgreedEvents()
            .Where(s => !existing.Contains((s.Date, s.StartTime)))
            .ToList();

        if (missing.Count == 0)
        {
            Report("Agreed events: none missing.");
            return 0;
        }

        foreach (var eventItem in missing)
        {
            Report($"Agreed event missing, will import: {eventItem.Date:yyyy-MM-dd} {eventItem.StartTime:HH\\:mm}.");
        }

        var csv = "date,startTime,DAT,MED,UNI\n" + string.Join(
            "\n",
            missing.Select(s =>
                $"{s.Date:yyyy-MM-dd},{s.StartTime:HH\\:mm},{s.DatHeadcount},{s.MedHeadcount},{s.UniHeadcount}"));

        var outcome = await importEvents.HandleAsync(
            // Agreed events are deliberately historical (negative day offsets), so the
            // user-facing future-date rule is lifted for this import only.
            new ImportEventsCommand(AdminUserId(), csv, AllowPastDates: true), cancellationToken);
        if (outcome.IsFailure)
        {
            throw new SeedException($"Agreed event import failed: {outcome.Error}.");
        }

        if (!outcome.Value.Accepted)
        {
            var errors = string.Join("; ", outcome.Value.Errors.Select(e => $"line {e.LineNumber}: {e.Message}"));
            throw new SeedException($"Agreed event import rejected: {errors}.");
        }

        Report($"Agreed events: imported {outcome.Value.ImportedCount}.");
        return outcome.Value.ImportedCount;
    }

    private async Task<(int ProposalsEnsured, int AcceptancesApplied)> SeedProposalsAsync(
        CancellationToken cancellationToken)
    {
        var ensured = 0;
        var acceptances = 0;

        foreach (var spec in DemoSeedSpec.OpenProposals())
        {
            var open = await proposals.ListOpenAsync(cancellationToken);
            var match = open.FirstOrDefault(p =>
                p.Window.Date == spec.Date && p.Window.StartTime == spec.StartTime);

            Guid proposalId;
            if (match is not null)
            {
                proposalId = match.Id;
                Report($"Proposal already open: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm}.");
            }
            else
            {
                var confirmedWindows = (await events.ListAllAsync(cancellationToken))
                    .Select(s => (s.Window.Date, s.Window.StartTime))
                    .ToHashSet();
                if (confirmedWindows.Contains((spec.Date, spec.StartTime)))
                {
                    Report($"Proposal already confirmed, skipping: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm}.");
                    continue;
                }

                Report($"Proposing event: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm} (today is {clock.TodayAtTransitionalLocation:yyyy-MM-dd}).");
                var proposed = await proposeEvent.HandleAsync(
                    new ProposeEventCommand(spec.CreatedByManagerUserId, spec.Date, spec.StartTime),
                    cancellationToken);
                if (proposed.IsFailure)
                {
                    throw new SeedException(
                        $"Proposing {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm} failed: {proposed.Error}.");
                }

                proposalId = proposed.Value;
                ensured++;
                Report($"Proposed event: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm}.");
            }

            var headcounts = new Dictionary<Guid, int?>();
            if (spec.DatHeadcount is not null)
            {
                headcounts[AppointmentTypeIds.DrugAndAlcoholTesting] = spec.DatHeadcount;
            }

            if (spec.MedHeadcount is not null)
            {
                headcounts[AppointmentTypeIds.MedicalCheckUp] = spec.MedHeadcount;
            }

            if (spec.UniHeadcount is not null)
            {
                headcounts[AppointmentTypeIds.UniformFitting] = spec.UniHeadcount;
            }

            foreach (var (typeId, headcount) in headcounts)
            {
                var accepted = await acceptProposal.HandleAsync(
                    new AcceptProposalCommand(ManagerForType()[typeId], proposalId, headcount!.Value),
                    cancellationToken);
                if (accepted.IsFailure)
                {
                    throw new SeedException(
                        $"Accepting {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm} failed: {accepted.Error}.");
                }

                acceptances++;
                Report($"Accepted headcount for {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm}.");
            }
        }

        Report($"Proposals: {ensured} newly proposed, {acceptances} headcounts accepted.");
        return (ensured, acceptances);
    }

    /// <summary>Gets the stable journey-event identifier for a deterministic demo window.</summary>
    private static Guid JourneyEventId(string name) => Guid.Parse($"d0000000-0000-0000-0000-{name}");

    private async Task EnsureJourneyEventsAsync(CancellationToken cancellationToken)
    {
        var anchor = DemoSeedSpec.AnchorDate();
        var windows = new (string Suffix, DateOnly Date, TimeOnly Start)[]
        {
            ("000000000001", anchor, new TimeOnly(9, 0)),
            ("000000000002", anchor.AddDays(7), new TimeOnly(9, 0)),
            ("000000000003", anchor, new TimeOnly(13, 0)),
            ("000000000004", anchor.AddDays(-7), new TimeOnly(9, 0)),
            ("000000000005", anchor.AddDays(-14), new TimeOnly(9, 0)),
            ("000000000006", anchor, new TimeOnly(15, 0)),
        };

        var added = 0;
        foreach (var (suffix, date, start) in windows)
        {
            var id = JourneyEventId(suffix);
            if (await events.GetAsync(id, cancellationToken) is not null)
            {
                continue;
            }

            database.Events.Add(Event.CreateImported(
                id,
                new EventWindow(date, start),
                AppointmentTypeIds.All.ToDictionary(typeId => typeId, _ => 20)));
            added++;
            Report($"Journey event created: {date:yyyy-MM-dd} {start:HH\\:mm}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        Report($"Journey events: {added} created, {windows.Length - added} already present.");
    }

    /// <summary>Derives one stable identifier from a attendee email and a seed role name.</summary>
    private static Guid SeedId(string email, string role) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{email}:{role}")));

    private async Task<int> SeedAttendeesAsync(CancellationToken cancellationToken)
    {
        await EnsureJourneyEventsAsync(cancellationToken);

        var created = 0;
        var skipped = 0;
        var position = 0;
        foreach (var spec in DemoSeedSpec.Attendees())
        {
            if (await attendees.GetByEmailAsync(spec.Email, cancellationToken) is not null)
            {
                skipped++;
                position++;
                continue;
            }

            var group = await groups.GetByCodeAsync(spec.AttendeeGroupCode, cancellationToken)
                ?? throw new SeedException(
                    $"Employee group '{spec.AttendeeGroupCode}' for {spec.Email} is not seeded.");

            var result = await saveAttendee.CreateAsync(
                new CreateAttendeeCommand(
                    CoordinatorUserId(), spec.Name, spec.Email, group.Id),
                cancellationToken);
            if (result.IsFailure)
            {
                throw new SeedException($"Creating attendee {spec.Email} failed: {result.Error}.");
            }

            var attendee = await attendees.GetAsync(result.Value, cancellationToken)
                ?? throw new SeedException($"Seeded attendee {spec.Email} is missing.");
            await BuildJourneyAsync(attendee, group, spec.Journey, position, cancellationToken);
            Report($"Attendee created: {spec.Email} ({spec.AttendeeGroupCode}, {spec.Journey}).");

            created++;
            position++;
        }

        Report($"Attendees: {created} created, {skipped} already present.");
        return created;
    }

    /// <summary>Constructs the requested deterministic lifecycle journey through domain factories.</summary>
    private async Task BuildJourneyAsync(
        Attendee attendee,
        AttendeeGroup group,
        DemoAttendeeJourney journey,
        int position,
        CancellationToken cancellationToken)
    {
        var types = group.RequiredAppointmentTypeIds.Order().ToList();
        var coordinator = CoordinatorUserId();
        var now = clock.UtcNow;
        var today = clock.TodayAtTransitionalLocation;

        switch (journey)
        {
            case DemoAttendeeJourney.Unbooked:
                return;
            case DemoAttendeeJourney.Ready:
                {
                    var eventDate = EventDate(JourneyEventId("000000000001"));
                    var appointments = await SeedBookingAsync(
                        attendee, types, JourneyEventId("000000000001"), "initial", now, cancellationToken);
                    foreach (var appointment in appointments)
                    {
                        appointment.TransitionTo(
                            BookingAppointmentStatus.CheckedIn, coordinator, now,
                            checkInAllowed: eventDate == today, noShowAllowed: false);
                        appointment.TransitionTo(
                            BookingAppointmentStatus.Completed, coordinator, now,
                            checkInAllowed: true, noShowAllowed: false);
                    }

                    break;
                }

            case DemoAttendeeJourney.Outstanding:
                {
                    if (position % 2 == 0)
                    {
                        var appointments = await SeedBookingAsync(
                            attendee, types, JourneyEventId("000000000003"), "initial", now, cancellationToken);
                        appointments[0].TransitionTo(
                            BookingAppointmentStatus.CheckedIn, coordinator, now,
                            checkInAllowed: EventDate(JourneyEventId("000000000003")) == today,
                            noShowAllowed: false);
                    }
                    else
                    {
                        await SeedBookingAsync(
                            attendee, types, JourneyEventId("000000000002"), "initial", now, cancellationToken);
                    }

                    break;
                }

            case DemoAttendeeJourney.NoShow:
                {
                    var appointments = await SeedBookingAsync(
                        attendee, types, JourneyEventId("000000000004"), "initial", now, cancellationToken);
                    appointments[0].TransitionTo(
                        BookingAppointmentStatus.NoShow, coordinator, now,
                        checkInAllowed: false,
                        noShowAllowed: EventDate(JourneyEventId("000000000004")) < today);
                    break;
                }

            case DemoAttendeeJourney.RecoveryCompleted:
                {
                    var original = await SeedBookingAsync(
                        attendee, types, JourneyEventId("000000000005"), "initial", now, cancellationToken);
                    var originalBooking = database.Bookings.Single(b =>
                        b.AttendeeId == attendee.Id && b.RecoveryOfBookingId == null);
                    original[0].TransitionTo(
                        BookingAppointmentStatus.NoShow, coordinator, now,
                        checkInAllowed: false,
                        noShowAllowed: EventDate(JourneyEventId("000000000005")) < today);

                    var recoveryEventId = JourneyEventId("000000000006");
                    var recoveryInvite = Invite.CreateRecovery(
                        SeedId(attendee.Email, "recovery:invite"),
                        attendee.Id,
                        originalBooking.Id,
                        $"seed-recovery-{position}",
                        now.AddDays(7),
                        [recoveryEventId, SeedId(attendee.Email, "recovery:spare1"),
                        SeedId(attendee.Email, "recovery:spare2")],
                        [types[0]]);
                    database.Invites.Add(recoveryInvite);
                    var recovery = Booking.CreateRecovery(
                        SeedId(attendee.Email, "recovery:booking"),
                        recoveryInvite,
                        originalBooking,
                        recoveryEventId,
                        $"seed-recovery-manage-{position}",
                        now.AddMinutes(5));
                    recoveryInvite.MarkUsed();
                    database.Bookings.Add(recovery);
                    var recoveryAppointment = BookingAppointment.Create(
                        SeedId(attendee.Email, "recovery:appointment"),
                        recovery.Id,
                        types[0]);
                    recoveryAppointment.TransitionTo(
                        BookingAppointmentStatus.CheckedIn, coordinator, now,
                        checkInAllowed: EventDate(recoveryEventId) == today, noShowAllowed: false);
                    recoveryAppointment.TransitionTo(
                        BookingAppointmentStatus.Completed, coordinator, now,
                        checkInAllowed: true, noShowAllowed: false);
                    database.BookingAppointments.Add(recoveryAppointment);
                    break;
                }

            default:
                throw new SeedException($"Demo journey {journey} is not supported.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private DateOnly EventDate(Guid eventId) =>
        database.Events.Single(eventItem => eventItem.Id == eventId).Window.Date;

    /// <summary>Seeds one used initial invite, booking, and appointment row per required type.</summary>
    /// <returns>The created appointments in requirement order.</returns>
    private async Task<IReadOnlyList<BookingAppointment>> SeedBookingAsync(
        Attendee attendee,
        IReadOnlyList<Guid> types,
        Guid eventId,
        string tag,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var invite = Invite.CreateInitial(
            SeedId(attendee.Email, $"{tag}:invite"),
            attendee.Id,
            $"seed-{tag}-{attendee.Email}",
            createdAt.AddDays(7),
            [eventId, SeedId(attendee.Email, $"{tag}:spare1"), SeedId(attendee.Email, $"{tag}:spare2")],
            types,
            0);
        database.Invites.Add(invite);
        var booking = Booking.Create(
            SeedId(attendee.Email, $"{tag}:booking"),
            invite,
            eventId,
            $"seed-{tag}-manage-{attendee.Email}",
            createdAt);
        invite.MarkUsed();
        database.Bookings.Add(booking);

        var appointments = types
            .Select(typeId => BookingAppointment.Create(
                SeedId(attendee.Email, $"{tag}:appointment:{typeId}"),
                booking.Id,
                typeId))
            .ToList();
        database.BookingAppointments.AddRange(appointments);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return appointments;
    }
}
`````
