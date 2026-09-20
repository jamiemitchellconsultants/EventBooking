# 00d — Retire direct event import, edits 8 (Task 3b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.SeedData/DemoSeeder.cs — 1/1

<!-- retirement-file: {"id":20,"file":"src/EventBooking.SeedData/DemoSeeder.cs","beforeSha":"c33ab24d5d5d28773e47df32dac20cdfec88adebef56977b7284d41f81becec8","afterSha":"be9d23decdec95ec1d76a33b50dffe691af6910a0db0bf004c72a226f299be7b","side":"after","part":1,"parts":1} -->

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
            Report($"Agreed event missing, will create with an accepted proposal: {eventItem.Date:yyyy-MM-dd} {eventItem.StartTime:HH\\:mm}.");
        }

        foreach (var item in missing)
        {
            DemoEventFactory.Create(database, Guid.NewGuid(), new EventWindow(item.Date, item.StartTime),
                new Dictionary<Guid, int>
                {
                    [AppointmentTypeIds.DrugAndAlcoholTesting] = item.DatHeadcount,
                    [AppointmentTypeIds.MedicalCheckUp] = item.MedHeadcount,
                    [AppointmentTypeIds.UniformFitting] = item.UniHeadcount,
                });
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        Report($"Agreed events: created {missing.Count} with accepted proposals.");
        return missing.Count;
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

            DemoEventFactory.Create(database,
                id,
                new EventWindow(date, start),
                AppointmentTypeIds.All.ToDictionary(typeId => typeId, _ => 20));
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

## before — src/EventBooking.Web/Pages/Audit.razor — 1/1

<!-- retirement-file: {"id":21,"file":"src/EventBooking.Web/Pages/Audit.razor","beforeSha":"4f9b04878a6d724c0eca51ac7fef918937a281f970023a2ae28e22996cc7d02a","afterSha":"d79305702d571409d264301956fd9187064c47ada9d0e0edca877081efe11d0f","side":"before","part":1,"parts":1} -->

`````text
@page "/audit"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AuditClient Audits
@inject MeClient Me
@inject TransitionalLocationTimePresentation TimePresentation

<PageTitle>Audit trail</PageTitle>

<section class="page audit-page" aria-labelledby="audit-heading">
    <div class="page-header">
        <div>
            <span class="eyebrow">Assurance</span>
            <h1 id="audit-heading">Audit trail</h1>
            <p>Search what changed, who changed it, and when.</p>
        </div>
    </div>

    <div class="card">
        <div class="card-body audit-filters" aria-label="Audit search filters">
            <div class="field">
                <label class="field-label" for="audit-from">From</label>
                <input id="audit-from" type="date" @bind="_from"
                       title="Only shows changes recorded on or after this date." />
            </div>
            <div class="field">
                <label class="field-label" for="audit-to">To</label>
                <input id="audit-to" type="date" @bind="_to"
                       title="Only shows changes recorded on or before this date." />
            </div>
            <div class="field">
                <label class="field-label" for="audit-actor-type">Actor</label>
                <select id="audit-actor-type" @bind="_actorType"
                        title="Who caused the change: a staff member, a attendee's link, or the system.">
                    <option value="">Any actor</option>
                    @foreach (var actorType in ActorTypes)
                    {
                        <option value="@actorType">@actorType</option>
                    }
                </select>
            </div>
            <div class="field">
                <label class="field-label" for="audit-action">Action</label>
                <select id="audit-action" @bind="_action"
                        title="The recorded change to look for.">
                    <option value="">Any action</option>
                    @foreach (var action in Actions)
                    {
                        <option value="@action">@action</option>
                    }
                </select>
            </div>
            <div class="field">
                <label class="field-label" for="audit-identifier">Identifier</label>
                <input id="audit-identifier" @bind="_identifier" placeholder="Entity or actor id"
                       title="Matches an audited entity identifier or an actor identifier exactly." />
            </div>
            @if (_showEntityType)
            {
                <div class="field">
                    <label class="field-label" for="audit-entity-type">Entity</label>
                    <select id="audit-entity-type" @bind="_entityType"
                            title="Narrows the search to one kind of audited entity.">
                        <option value="">All entities</option>
                        @foreach (var entityType in EntityTypes)
                        {
                            <option value="@entityType">@entityType</option>
                        }
                    </select>
                </div>
            }
            <button id="audit-search" type="button" class="button button-primary" @onclick="SearchAsync" disabled="@_busy">
                Search
            </button>
        </div>
    </div>

    @if (_error is not null)
    {
        <p class="error" role="alert">@_error</p>
    }
    else if (_rows.Count == 0 && !_busy)
    {
        <p>Nothing matches these filters.</p>
    }

    @if (_rows.Count > 0)
    {
        <div class="table-wrap">
            <table>
                <thead>
                    <tr>
                        <th scope="col">When</th>
                        <th scope="col">What</th>
                        <th scope="col">Who</th>
                        <th scope="col">Details</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var row in _rows)
                    {
                        <tr>
                            <td data-label="When">@TimePresentation.Format(row.Timestamp)</td>
                            <td data-label="What">@row.EntityType @row.Action</td>
                            <td data-label="Who">@row.ActorType @row.ActorId</td>
                            <td data-label="Details">@row.Details</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }

    @if (_nextCursor is not null)
    {
        <button id="audit-load-more" type="button" class="button" @onclick="LoadMoreAsync" disabled="@_busy">
            Load more
        </button>
    }
</section>

@code {
    // The web app deliberately does not reference the domain assembly, so the server's enum names
    // are repeated here as plain strings; the API rejects anything it does not recognise.
    private static readonly string[] ActorTypes = ["Staff", "AttendeeToken", "System"];

    private static readonly string[] Actions =
    [
        "ProposalCreated", "ProposalWithdrawn", "AcceptanceRecorded", "AcceptanceWithdrawn",
        "EventConfirmed", "EventCancelled", "CapacityDecremented", "CapacityIncremented",
        "InviteCreated", "InviteSent", "InviteExpired", "InviteOptionReplaced",
        "BookingCreated", "BookingCancelled", "CapacityAdjusted", "EventImported",
        "StaffAccessChanged", "StaffAccessRemoved", "AppointmentCheckedIn", "AppointmentCompleted",
        "AppointmentMarkedNoShow", "AppointmentStatusCorrected", "AttendeeGroupAssigned",
        "AttendeeGroupReassigned", "RecoveryInviteCreated", "RecoveryInviteCancelled",
        "RecoveryBookingCreated", "RecoveryBookingConcluded", "StaffRolesSynced",
    ];

    private static readonly string[] EntityTypes =
    [
        "EventProposal", "Event", "Invite", "Booking",
        "StaffAccessProfile", "BookingAppointment", "Attendee",
    ];

    private const int PageSize = 50;

    private readonly List<AuditRowDto> _rows = [];
    private DateTime? _from;
    private DateTime? _to;
    private string? _actorType;
    private string? _action;
    private string? _identifier;
    private string? _entityType;
    private string? _nextCursor;
    private string? _error;
    private bool _busy;
    private bool _showEntityType;

    protected override async Task OnInitializedAsync()
    {
        var me = await Me.GetAsync(CancellationToken.None);
        _showEntityType = me.Value?.Roles.Contains("Coordinator") == true;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _rows.Clear();
        _nextCursor = null;
        await LoadMoreAsync();
    }

    private async Task LoadMoreAsync()
    {
        _busy = true;
        _error = null;
        try
        {
            var outcome = await Audits.SearchAsync(
                new AuditSearchFilterDto(
                    ToOffset(_from),
                    ToOffset(_to),
                    EmptyToNull(_actorType),
                    EmptyToNull(_action),
                    EmptyToNull(_identifier),
                    EmptyToNull(_entityType),
                    _nextCursor,
                    PageSize),
                CancellationToken.None);

            if (outcome is { IsSuccess: true, Value: not null })
            {
                _rows.AddRange(outcome.Value.Rows);
                _nextCursor = outcome.Value.NextCursor;
            }
            else
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
`````

## after — src/EventBooking.Web/Pages/Audit.razor — 1/1

<!-- retirement-file: {"id":21,"file":"src/EventBooking.Web/Pages/Audit.razor","beforeSha":"4f9b04878a6d724c0eca51ac7fef918937a281f970023a2ae28e22996cc7d02a","afterSha":"d79305702d571409d264301956fd9187064c47ada9d0e0edca877081efe11d0f","side":"after","part":1,"parts":1} -->

`````text
@page "/audit"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AuditClient Audits
@inject MeClient Me
@inject TransitionalLocationTimePresentation TimePresentation

<PageTitle>Audit trail</PageTitle>

<section class="page audit-page" aria-labelledby="audit-heading">
    <div class="page-header">
        <div>
            <span class="eyebrow">Assurance</span>
            <h1 id="audit-heading">Audit trail</h1>
            <p>Search what changed, who changed it, and when.</p>
        </div>
    </div>

    <div class="card">
        <div class="card-body audit-filters" aria-label="Audit search filters">
            <div class="field">
                <label class="field-label" for="audit-from">From</label>
                <input id="audit-from" type="date" @bind="_from"
                       title="Only shows changes recorded on or after this date." />
            </div>
            <div class="field">
                <label class="field-label" for="audit-to">To</label>
                <input id="audit-to" type="date" @bind="_to"
                       title="Only shows changes recorded on or before this date." />
            </div>
            <div class="field">
                <label class="field-label" for="audit-actor-type">Actor</label>
                <select id="audit-actor-type" @bind="_actorType"
                        title="Who caused the change: a staff member, a attendee's link, or the system.">
                    <option value="">Any actor</option>
                    @foreach (var actorType in ActorTypes)
                    {
                        <option value="@actorType">@actorType</option>
                    }
                </select>
            </div>
            <div class="field">
                <label class="field-label" for="audit-action">Action</label>
                <select id="audit-action" @bind="_action"
                        title="The recorded change to look for.">
                    <option value="">Any action</option>
                    @foreach (var action in Actions)
                    {
                        <option value="@action">@action</option>
                    }
                </select>
            </div>
            <div class="field">
                <label class="field-label" for="audit-identifier">Identifier</label>
                <input id="audit-identifier" @bind="_identifier" placeholder="Entity or actor id"
                       title="Matches an audited entity identifier or an actor identifier exactly." />
            </div>
            @if (_showEntityType)
            {
                <div class="field">
                    <label class="field-label" for="audit-entity-type">Entity</label>
                    <select id="audit-entity-type" @bind="_entityType"
                            title="Narrows the search to one kind of audited entity.">
                        <option value="">All entities</option>
                        @foreach (var entityType in EntityTypes)
                        {
                            <option value="@entityType">@entityType</option>
                        }
                    </select>
                </div>
            }
            <button id="audit-search" type="button" class="button button-primary" @onclick="SearchAsync" disabled="@_busy">
                Search
            </button>
        </div>
    </div>

    @if (_error is not null)
    {
        <p class="error" role="alert">@_error</p>
    }
    else if (_rows.Count == 0 && !_busy)
    {
        <p>Nothing matches these filters.</p>
    }

    @if (_rows.Count > 0)
    {
        <div class="table-wrap">
            <table>
                <thead>
                    <tr>
                        <th scope="col">When</th>
                        <th scope="col">What</th>
                        <th scope="col">Who</th>
                        <th scope="col">Details</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var row in _rows)
                    {
                        <tr>
                            <td data-label="When">@TimePresentation.Format(row.Timestamp)</td>
                            <td data-label="What">@row.EntityType @row.Action</td>
                            <td data-label="Who">@row.ActorType @row.ActorId</td>
                            <td data-label="Details">@row.Details</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }

    @if (_nextCursor is not null)
    {
        <button id="audit-load-more" type="button" class="button" @onclick="LoadMoreAsync" disabled="@_busy">
            Load more
        </button>
    }
</section>

@code {
    // The web app deliberately does not reference the domain assembly, so the server's enum names
    // are repeated here as plain strings; the API rejects anything it does not recognise.
    private static readonly string[] ActorTypes = ["Staff", "AttendeeToken", "System"];

    private static readonly string[] Actions =
    [
        "ProposalCreated", "ProposalWithdrawn", "AcceptanceRecorded", "AcceptanceWithdrawn",
        "EventConfirmed", "EventCancelled", "CapacityDecremented", "CapacityIncremented",
        "InviteCreated", "InviteSent", "InviteExpired", "InviteOptionReplaced",
        "BookingCreated", "BookingCancelled", "CapacityAdjusted",
        "StaffAccessChanged", "AppointmentCheckedIn", "AppointmentCompleted",
        "AppointmentMarkedNoShow", "AppointmentStatusCorrected", "AttendeeGroupAssigned",
        "AttendeeGroupReassigned", "RecoveryInviteCreated", "RecoveryInviteCancelled",
        "RecoveryBookingCreated", "RecoveryBookingConcluded", "StaffRolesSynced",
    ];

    private static readonly string[] EntityTypes =
    [
        "EventProposal", "Event", "Invite", "Booking",
        "StaffAccessProfile", "BookingAppointment", "Attendee",
    ];

    private const int PageSize = 50;

    private readonly List<AuditRowDto> _rows = [];
    private DateTime? _from;
    private DateTime? _to;
    private string? _actorType;
    private string? _action;
    private string? _identifier;
    private string? _entityType;
    private string? _nextCursor;
    private string? _error;
    private bool _busy;
    private bool _showEntityType;

    protected override async Task OnInitializedAsync()
    {
        var me = await Me.GetAsync(CancellationToken.None);
        _showEntityType = me.Value?.Roles.Contains("Coordinator") == true;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _rows.Clear();
        _nextCursor = null;
        await LoadMoreAsync();
    }

    private async Task LoadMoreAsync()
    {
        _busy = true;
        _error = null;
        try
        {
            var outcome = await Audits.SearchAsync(
                new AuditSearchFilterDto(
                    ToOffset(_from),
                    ToOffset(_to),
                    EmptyToNull(_actorType),
                    EmptyToNull(_action),
                    EmptyToNull(_identifier),
                    EmptyToNull(_entityType),
                    _nextCursor,
                    PageSize),
                CancellationToken.None);

            if (outcome is { IsSuccess: true, Value: not null })
            {
                _rows.AddRange(outcome.Value.Rows);
                _nextCursor = outcome.Value.NextCursor;
            }
            else
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
`````

## before — src/EventBooking.Web/Pages/EventOperations.razor — 1/1

<!-- retirement-file: {"id":22,"file":"src/EventBooking.Web/Pages/EventOperations.razor","beforeSha":"f7043ae4369a1e0e8124d04d93c96fe033a3afe32efcba9e1477eb5409078487","afterSha":"c2cc5dd945fe9fa1f78184a261016cfd8b8ab57bf7eac70e0fea507a284c3373","side":"before","part":1,"parts":1} -->

`````text
@page "/events/operations"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject EventOperationsClient EventImportsApi
@inject EventsClient EventsApi

<PageTitle>Events</PageTitle>

<section class="page events-page" aria-labelledby="events-heading" aria-busy="@(_busy ? "true" : "false")">
    <div class="page-header">
        <div>
            <span class="eyebrow">Bulk import</span>
            <h1 id="events-heading">Events</h1>
            <p>Bring in windows that were already agreed away from the negotiation board.</p>
        </div>
    </div>

    <div class="card">
        <div class="card-heading">
            <h2>
                Import already agreed events
                <span class="tip" tabindex="0" role="note"
                      aria-label="Imported events skip the negotiation board: each row lands as a confirmed window with the headcount given per appointment type."
                      data-tip="Imported events skip the negotiation board: each row lands as a confirmed window with the headcount given per appointment type."></span>
            </h2>
        </div>
        <div class="card-body import-card">
            <p class="hint">
                Upload a CSV to add events agreed outside the system. The required header is
                <code>date,startTime,DAT,MED,UNI</code>. The whole file is accepted or rejected.
            </p>
            <InputFile OnChange="OnEventFileChosenAsync" accept=".csv" disabled="@_busy"
                       aria-label="Import events CSV" />

            @if (_busy)
            {
                <p class="hint" role="status">Importing events…</p>
            }
            @if (_error is not null)
            {
                <p class="banner error" role="alert">@_error</p>
            }
            @if (_importMessage is not null)
            {
                <p class="banner saved" role="status">@_importMessage</p>
            }
            @if (_importErrors.Count > 0)
            {
                <div class="banner error" role="alert">
                    <p><strong>Nothing was imported.</strong> Fix the file and try again.</p>
                    <ul class="import-errors">
                        @foreach (var error in _importErrors)
                        {
                            <li><strong>Line @error.LineNumber:</strong> @error.Message</li>
                        }
                    </ul>
                </div>
            }
        </div>
    </div>

    <div class="card">
        <div class="card-heading">
            <h2>
                Cancel a event
                <span class="tip" tabindex="0" role="note"
                      aria-label="Cancelling a window releases every place it holds. Any attendee booked into it is notified and re-invited."
                      data-tip="Cancelling a window releases every place it holds. Any attendee booked into it is notified and re-invited."></span>
            </h2>
        </div>
        <div class="card-body">
            @if (_eventsLoading)
            {
                <p class="hint" role="status">Loading events…</p>
            }
            else if (_events is null || _events.Count == 0)
            {
                <p class="hint" role="status">No events yet.</p>
            }
            else
            {
                <div class="table-wrap">
                    <table id="event-operations">
                        <thead>
                            <tr>
                                <th scope="col">Date</th>
                                <th scope="col">Window</th>
                                <th scope="col" title="Places left over the total headcount each appointment type accepted.">Capacity by type</th>
                                <th scope="col" title="Attendees currently booked into this window.">Active bookings</th>
                                <th scope="col" class="actions-column">Cancel</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var eventItem in _events)
                            {
                                <tr @key="eventItem.EventId">
                                    <td data-label="Date">@eventItem.Date.ToString("yyyy-MM-dd")</td>
                                    <td data-label="Window">@eventItem.StartTime.ToString("HH\\:mm")–@eventItem.EndTime.ToString("HH\\:mm")</td>
                                    <td data-label="Capacity by type">
                                        <div class="chip-row">
                                            @foreach (var capacity in eventItem.Capacities)
                                            {
                                                <span class="chip">@capacity.Code @capacity.RemainingCapacity/@capacity.TotalHeadcount</span>
                                            }
                                        </div>
                                    </td>
                                    <td data-label="Active bookings">@eventItem.ActiveBookings</td>
                                    <td data-label="Cancel">
                                        <button class="button button-danger button-small"
                                                @onclick="() => CancelEventAsync(eventItem.EventId)" disabled="@_busy">
                                            @(_cancelAwaitingConfirmation == eventItem.EventId ? "Confirm cancel" : "Cancel event")
                                        </button>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }

            @if (_eventsError is not null)
            {
                <p class="banner error" role="alert">@_eventsError</p>
            }
        </div>
    </div>
</section>

@code {
    private bool _busy;
    private string? _error;
    private string? _importMessage;
    private List<EventImportErrorDto> _importErrors = [];

    private List<EventOperationDto>? _events;
    private bool _eventsLoading = true;
    private string? _eventsError;
    private Guid? _cancelAwaitingConfirmation;

    protected override Task OnInitializedAsync() => ReloadEventsAsync();

    internal Task ReloadEventsForTestingAsync() => ReloadEventsAsync();

    internal Task CancelEventForTestingAsync(Guid eventId) => CancelEventAsync(eventId);

    private async Task ReloadEventsAsync()
    {
        _eventsLoading = true;
        try
        {
            var outcome = await EventsApi.GetEventOperationsAsync(CancellationToken.None);
            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _eventsError = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
                return;
            }

            _events = outcome.Value.Events.ToList();
            _eventsError = null;
        }
        catch (Exception)
        {
            _eventsError = "Something went wrong. Please try again.";
        }
        finally
        {
            _eventsLoading = false;
            StateHasChanged();
        }
    }

    // Two-stage: the first click asks without authorizing the cascade, so a event holding bookings
    // comes back 409 and the button becomes the confirmation.
    private async Task CancelEventAsync(Guid eventId)
    {
        if (_busy)
        {
            return;
        }

        var confirm = _cancelAwaitingConfirmation == eventId;
        _busy = true;
        try
        {
            var outcome = await EventsApi.CancelEventAsync(eventId, confirm, CancellationToken.None);
            if (!outcome.IsSuccess && outcome.StatusCode == 409 && !confirm)
            {
                _cancelAwaitingConfirmation = eventId;
                _eventsError = $"{outcome.ErrorMessage} Press Confirm cancel to proceed.";
                return;
            }

            _cancelAwaitingConfirmation = null;
            _eventsError = outcome.ErrorMessage;
            if (outcome.IsSuccess)
            {
                await ReloadEventsAsync();
            }
        }
        catch (Exception)
        {
            _eventsError = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

    private async Task OnEventFileChosenAsync(InputFileChangeEventArgs args)
    {
        await ImportAsync(async () =>
        {
            using var stream = args.File.OpenReadStream(1024 * 1024);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        });
    }

    internal Task ImportCsvForTestingAsync(string csv) =>
        ImportAsync(() => Task.FromResult(csv));

    internal Task ImportFileForTestingAsync(InputFileChangeEventArgs args) =>
        OnEventFileChosenAsync(args);

    private async Task ImportAsync(Func<Task<string>> readCsv)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _error = null;
        _importMessage = null;
        _importErrors = [];
        try
        {
            var csv = await readCsv();
            var outcome = await EventImportsApi.ImportAsync(csv, CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
                return;
            }

            if (outcome.Value!.Accepted)
            {
                _importMessage = $"{outcome.Value.ImportedCount} events imported.";
            }
            else
            {
                _importErrors = outcome.Value.Errors.ToList();
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }
}
`````
