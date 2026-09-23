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
            DemoEventFactory.Create(database, Guid.NewGuid(), new EventWindow(item.Date, item.StartTime, 240),
                new Dictionary<Guid, int>
                {
                    [AppointmentTypeIds.DrugAndAlcoholTesting] = item.DatHeadcount,
                    [AppointmentTypeIds.MedicalCheckUp] = item.MedHeadcount,
                    [AppointmentTypeIds.UniformFitting] = item.UniHeadcount,
                },
                clock.UtcNow);
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
                new EventWindow(date, start, 240),
                AppointmentTypeIds.All.ToDictionary(typeId => typeId, _ => 20),
                clock.UtcNow);
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
                    $"Attendee group '{spec.AttendeeGroupCode}' for {spec.Email} is not seeded.");

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
                        TransitionalLocation.Id,
                        null,
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
            [TransitionalLocation.Id],
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
