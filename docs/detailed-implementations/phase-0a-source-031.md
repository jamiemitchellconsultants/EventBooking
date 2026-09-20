# 00a — Port source 31 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.SeedData/DemoSeeder.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.SeedData/DemoSeeder.cs","encoding":"utf8","sha256":"1febbeaf3197c97759fef8a01450852c355350adeda8735322b2a7109567ee57","parts":1,"part":1} -->

`````csharp
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Candidates;
using EventBooking.Application.Slots;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Counts the deterministic rows created by one idempotent seed operation.</summary>
/// <param name="IdentitiesEnsured">The number of missing identity mirror rows inserted.</param>
/// <param name="ProfilesEnsured">The number of missing staff access profiles inserted.</param>
/// <param name="AgreedSlotsImported">The number of agreed appointment slots imported.</param>
/// <param name="ProposalsEnsured">The number of missing slot proposals inserted.</param>
/// <param name="AcceptancesApplied">The number of proposal acceptances applied.</param>
/// <param name="CandidatesCreated">The number of candidates created.</param>
public sealed record SeedSummary(
    int IdentitiesEnsured,
    int ProfilesEnsured,
    int AgreedSlotsImported,
    int ProposalsEnsured,
    int AcceptancesApplied,
    int CandidatesCreated)
{
    /// <summary>Gets the number of missing identity mirror rows inserted.</summary>
    public int IdentitiesEnsured { get; init; } = IdentitiesEnsured;

    /// <summary>Gets the number of missing staff access profiles inserted.</summary>
    public int ProfilesEnsured { get; init; } = ProfilesEnsured;

    /// <summary>Gets the number of agreed appointment slots imported.</summary>
    public int AgreedSlotsImported { get; init; } = AgreedSlotsImported;

    /// <summary>Gets the number of missing slot proposals inserted.</summary>
    public int ProposalsEnsured { get; init; } = ProposalsEnsured;

    /// <summary>Gets the number of proposal acceptances applied.</summary>
    public int AcceptancesApplied { get; init; } = AcceptancesApplied;

    /// <summary>Gets the number of candidates created.</summary>
    public int CandidatesCreated { get; init; } = CandidatesCreated;
}

public sealed class SeedException(string message) : Exception(message);

/// <summary>
/// Applies <see cref="DemoSeedSpec"/> through the application handlers so every domain rule
/// is enforced exactly as if staff had entered the data by hand. Re-runnable: existing rows
/// are matched by natural key (role id, slot window, candidate email) and skipped, which is
/// also how filled-in headcount placeholders get applied on a later run.
/// Reseeding instead wipes every domain table first, restoring the fixed reference rows,
/// so a database mutated by a demo comes back to exactly the seed state.
/// </summary>
/// <param name="profiles">Provides persistence for application access profiles.</param>
/// <param name="identities">Provides persistence for the identity mirror.</param>
/// <param name="confirmedSlots">Provides persistence for confirmed appointment slots.</param>
/// <param name="proposals">Provides persistence for slot proposals.</param>
/// <param name="candidates">Provides persistence for candidates.</param>
/// <param name="groups">Resolves demo requirement sets to their Employee Group.</param>
/// <param name="importSlots">Imports agreed appointment slots from the canonical workbook.</param>
/// <param name="proposeSlot">Creates proposed appointment slots.</param>
/// <param name="acceptProposal">Accepts proposed appointment slots.</param>
/// <param name="saveCandidate">Creates candidates through the application workflow.</param>
/// <param name="unitOfWork">Commits tracked seed changes.</param>
/// <param name="clock">Supplies the observation time for identity mirror rows.</param>
/// <param name="database">Provides destructive reseed access to the application database.</param>
public sealed class DemoSeeder(
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IConfirmedSlotRepository confirmedSlots,
    ISlotProposalRepository proposals,
    ICandidateRepository candidates,
    IEmployeeGroupRepository groups,
    ImportConfirmedSlotsHandler importSlots,
    ProposeSlotHandler proposeSlot,
    AcceptProposalHandler acceptProposal,
    SaveCandidateHandler saveCandidate,
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
            $"{DemoSeedSpec.AgreedSlots().Count} agreed slots, " +
            $"{DemoSeedSpec.OpenProposals().Count} proposals, " +
            $"{DemoSeedSpec.Candidates().Count} candidates " +
            $"(anchor {DemoSeedSpec.AnchorDate():yyyy-MM-dd}" +
            $"{(DemoSeedSpec.AnchorOverridden ? ", reanchored" : "")}, " +
            $"today {clock.TodayAtHeadOffice:yyyy-MM-dd}).");
        var identitiesEnsured = await EnsureIdentitiesAsync(cancellationToken);
        var profilesEnsured = await EnsureProfilesAsync(cancellationToken);
        var agreedImported = await SeedAgreedSlotsAsync(cancellationToken);
        var (proposalsEnsured, acceptancesApplied) = await SeedProposalsAsync(cancellationToken);
        var candidatesCreated = await SeedCandidatesAsync(cancellationToken);
        Report(
            $"Seed run finished: {identitiesEnsured} identities, {profilesEnsured} profiles, " +
            $"{agreedImported} agreed slots, {proposalsEnsured} proposals, " +
            $"{acceptancesApplied} acceptances, {candidatesCreated} candidates.");

        return new SeedSummary(
            identitiesEnsured,
            profilesEnsured,
            agreedImported,
            proposalsEnsured,
            acceptancesApplied,
            candidatesCreated);
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
        database.EmployeeGroups.AddRange(FixedEmployeeGroups());
        await database.SaveChangesAsync(cancellationToken);
        Report("Wipe complete; reference rows restored.");
    }

    /// <summary>
    /// The five change-controlled groups, matching the reference migration exactly. Task 17
    /// replaces demo candidate assignment with explicit groups and journeys.
    /// </summary>
    private static IReadOnlyList<EmployeeGroup> FixedEmployeeGroups() =>
    [
        EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]),
        EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
        EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]),
        EmployeeGroup.Define(
            EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]),
        EmployeeGroup.Define(
            EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
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

    private async Task<int> SeedAgreedSlotsAsync(CancellationToken cancellationToken)
    {
        var existing = (await confirmedSlots.ListAllAsync(cancellationToken))
            .Select(s => (s.Window.Date, s.Window.StartTime))
            .ToHashSet();

        var missing = DemoSeedSpec.AgreedSlots()
            .Where(s => !existing.Contains((s.Date, s.StartTime)))
            .ToList();

        if (missing.Count == 0)
        {
            Report("Agreed slots: none missing.");
            return 0;
        }

        foreach (var slot in missing)
        {
            Report($"Agreed slot missing, will import: {slot.Date:yyyy-MM-dd} {slot.StartTime:HH\\:mm}.");
        }

        var csv = "date,startTime,DAT,MED,UNI\n" + string.Join(
            "\n",
            missing.Select(s =>
                $"{s.Date:yyyy-MM-dd},{s.StartTime:HH\\:mm},{s.DatHeadcount},{s.MedHeadcount},{s.UniHeadcount}"));

        var outcome = await importSlots.HandleAsync(
            // Agreed slots are deliberately historical (negative day offsets), so the
            // user-facing future-date rule is lifted for this import only.
            new ImportConfirmedSlotsCommand(AdminUserId(), csv, AllowPastDates: true), cancellationToken);
        if (outcome.IsFailure)
        {
            throw new SeedException($"Agreed slot import failed: {outcome.Error}.");
        }

        if (!outcome.Value.Accepted)
        {
            var errors = string.Join("; ", outcome.Value.Errors.Select(e => $"line {e.LineNumber}: {e.Message}"));
            throw new SeedException($"Agreed slot import rejected: {errors}.");
        }

        Report($"Agreed slots: imported {outcome.Value.ImportedCount}.");
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
                var confirmedWindows = (await confirmedSlots.ListAllAsync(cancellationToken))
                    .Select(s => (s.Window.Date, s.Window.StartTime))
                    .ToHashSet();
                if (confirmedWindows.Contains((spec.Date, spec.StartTime)))
                {
                    Report($"Proposal already confirmed, skipping: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm}.");
                    continue;
                }

                Report($"Proposing slot: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm} (today is {clock.TodayAtHeadOffice:yyyy-MM-dd}).");
                var proposed = await proposeSlot.HandleAsync(
                    new ProposeSlotCommand(spec.CreatedByManagerUserId, spec.Date, spec.StartTime),
                    cancellationToken);
                if (proposed.IsFailure)
                {
                    throw new SeedException(
                        $"Proposing {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm} failed: {proposed.Error}.");
                }

                proposalId = proposed.Value;
                ensured++;
                Report($"Proposed slot: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm}.");
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

    /// <summary>Gets the stable journey-slot identifier for a deterministic demo window.</summary>
    private static Guid JourneySlotId(string name) => Guid.Parse($"d0000000-0000-0000-0000-{name}");

    private async Task EnsureJourneySlotsAsync(CancellationToken cancellationToken)
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
            var id = JourneySlotId(suffix);
            if (await confirmedSlots.GetAsync(id, cancellationToken) is not null)
            {
                continue;
            }

            database.ConfirmedSlots.Add(ConfirmedSlot.CreateImported(
                id,
                new SlotWindow(date, start),
                AppointmentTypeIds.All.ToDictionary(typeId => typeId, _ => 20)));
            added++;
            Report($"Journey slot created: {date:yyyy-MM-dd} {start:HH\\:mm}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        Report($"Journey slots: {added} created, {windows.Length - added} already present.");
    }

    /// <summary>Derives one stable identifier from a candidate email and a seed role name.</summary>
    private static Guid SeedId(string email, string role) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{email}:{role}")));

    private async Task<int> SeedCandidatesAsync(CancellationToken cancellationToken)
    {
        await EnsureJourneySlotsAsync(cancellationToken);

        var created = 0;
        var skipped = 0;
        var position = 0;
        foreach (var spec in DemoSeedSpec.Candidates())
        {
            if (await candidates.GetByEmailAsync(spec.Email, cancellationToken) is not null)
            {
                skipped++;
                position++;
                continue;
            }

            var group = await groups.GetByCodeAsync(spec.EmployeeGroupCode, cancellationToken)
                ?? throw new SeedException(
                    $"Employee group '{spec.EmployeeGroupCode}' for {spec.Email} is not seeded.");

            var result = await saveCandidate.CreateAsync(
                new CreateCandidateCommand(
                    CoordinatorUserId(), spec.Name, spec.Email, group.Id),
                cancellationToken);
            if (result.IsFailure)
            {
                throw new SeedException($"Creating candidate {spec.Email} failed: {result.Error}.");
            }

            var candidate = await candidates.GetAsync(result.Value, cancellationToken)
                ?? throw new SeedException($"Seeded candidate {spec.Email} is missing.");
            await BuildJourneyAsync(candidate, group, spec.Journey, position, cancellationToken);
            Report($"Candidate created: {spec.Email} ({spec.EmployeeGroupCode}, {spec.Journey}).");

            created++;
            position++;
        }

        Report($"Candidates: {created} created, {skipped} already present.");
        return created;
    }

    /// <summary>Constructs the requested deterministic lifecycle journey through domain factories.</summary>
    private async Task BuildJourneyAsync(
        Candidate candidate,
        EmployeeGroup group,
        DemoCandidateJourney journey,
        int position,
        CancellationToken cancellationToken)
    {
        var types = group.RequiredAppointmentTypeIds.Order().ToList();
        var coordinator = CoordinatorUserId();
        var now = clock.UtcNow;
        var today = clock.TodayAtHeadOffice;

        switch (journey)
        {
            case DemoCandidateJourney.Unbooked:
                return;
            case DemoCandidateJourney.Ready:
                {
                    var slotDate = SlotDate(JourneySlotId("000000000001"));
                    var appointments = await SeedBookingAsync(
                        candidate, types, JourneySlotId("000000000001"), "initial", now, cancellationToken);
                    foreach (var appointment in appointments)
                    {
                        appointment.TransitionTo(
                            BookingAppointmentStatus.CheckedIn, coordinator, now,
                            checkInAllowed: slotDate == today, noShowAllowed: false);
                        appointment.TransitionTo(
                            BookingAppointmentStatus.Completed, coordinator, now,
                            checkInAllowed: true, noShowAllowed: false);
                    }

                    break;
                }

            case DemoCandidateJourney.Outstanding:
                {
                    if (position % 2 == 0)
                    {
                        var appointments = await SeedBookingAsync(
                            candidate, types, JourneySlotId("000000000003"), "initial", now, cancellationToken);
                        appointments[0].TransitionTo(
                            BookingAppointmentStatus.CheckedIn, coordinator, now,
                            checkInAllowed: SlotDate(JourneySlotId("000000000003")) == today,
                            noShowAllowed: false);
                    }
                    else
                    {
                        await SeedBookingAsync(
                            candidate, types, JourneySlotId("000000000002"), "initial", now, cancellationToken);
                    }

                    break;
                }

            case DemoCandidateJourney.NoShow:
                {
                    var appointments = await SeedBookingAsync(
                        candidate, types, JourneySlotId("000000000004"), "initial", now, cancellationToken);
                    appointments[0].TransitionTo(
                        BookingAppointmentStatus.NoShow, coordinator, now,
                        checkInAllowed: false,
                        noShowAllowed: SlotDate(JourneySlotId("000000000004")) < today);
                    break;
                }

            case DemoCandidateJourney.RecoveryCompleted:
                {
                    var original = await SeedBookingAsync(
                        candidate, types, JourneySlotId("000000000005"), "initial", now, cancellationToken);
                    var originalBooking = database.Bookings.Single(b =>
                        b.CandidateId == candidate.Id && b.RecoveryOfBookingId == null);
                    original[0].TransitionTo(
                        BookingAppointmentStatus.NoShow, coordinator, now,
                        checkInAllowed: false,
                        noShowAllowed: SlotDate(JourneySlotId("000000000005")) < today);

                    var recoverySlotId = JourneySlotId("000000000006");
                    var recoveryInvite = Invite.CreateRecovery(
                        SeedId(candidate.Email, "recovery:invite"),
                        candidate.Id,
                        originalBooking.Id,
                        $"seed-recovery-{position}",
                        now.AddDays(7),
                        [recoverySlotId, SeedId(candidate.Email, "recovery:spare1"),
                        SeedId(candidate.Email, "recovery:spare2")],
                        [types[0]]);
                    database.Invites.Add(recoveryInvite);
                    var recovery = Booking.CreateRecovery(
                        SeedId(candidate.Email, "recovery:booking"),
                        recoveryInvite,
                        originalBooking,
                        recoverySlotId,
                        $"seed-recovery-manage-{position}",
                        now.AddMinutes(5));
                    recoveryInvite.MarkUsed();
                    database.Bookings.Add(recovery);
                    var recoveryAppointment = BookingAppointment.Create(
                        SeedId(candidate.Email, "recovery:appointment"),
                        recovery.Id,
                        types[0]);
                    recoveryAppointment.TransitionTo(
                        BookingAppointmentStatus.CheckedIn, coordinator, now,
                        checkInAllowed: SlotDate(recoverySlotId) == today, noShowAllowed: false);
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

    private DateOnly SlotDate(Guid slotId) =>
        database.ConfirmedSlots.Single(slot => slot.Id == slotId).Window.Date;

    /// <summary>Seeds one used initial invite, booking, and appointment row per required type.</summary>
    /// <returns>The created appointments in requirement order.</returns>
    private async Task<IReadOnlyList<BookingAppointment>> SeedBookingAsync(
        Candidate candidate,
        IReadOnlyList<Guid> types,
        Guid slotId,
        string tag,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var invite = Invite.CreateInitial(
            SeedId(candidate.Email, $"{tag}:invite"),
            candidate.Id,
            $"seed-{tag}-{candidate.Email}",
            createdAt.AddDays(7),
            [slotId, SeedId(candidate.Email, $"{tag}:spare1"), SeedId(candidate.Email, $"{tag}:spare2")],
            types,
            0);
        database.Invites.Add(invite);
        var booking = Booking.Create(
            SeedId(candidate.Email, $"{tag}:booking"),
            invite,
            slotId,
            $"seed-{tag}-manage-{candidate.Email}",
            createdAt);
        invite.MarkUsed();
        database.Bookings.Add(booking);

        var appointments = types
            .Select(typeId => BookingAppointment.Create(
                SeedId(candidate.Email, $"{tag}:appointment:{typeId}"),
                booking.Id,
                typeId))
            .ToList();
        database.BookingAppointments.AddRange(appointments);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return appointments;
    }
}
`````

## src/EventBooking.SeedData/DemoSeedSpec.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.SeedData/DemoSeedSpec.cs","encoding":"utf8","sha256":"29a7c06109e485291124b201658d17e474a012db587aaae45b01fb028affeacd","parts":1,"part":1} -->

`````csharp
using System.Reflection;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.SeedData;

/// <summary>The deterministic lifecycle state constructed for a demo Candidate.</summary>
public enum DemoCandidateJourney
{
    /// <summary>The Candidate has a group but no Booking.</summary>
    Unbooked = 1,
    /// <summary>Every current requirement has a Completed attempt.</summary>
    Ready = 2,
    /// <summary>At least one current requirement is Expected or CheckedIn.</summary>
    Outstanding = 3,
    /// <summary>At least one current requirement has a recoverable NoShow.</summary>
    NoShow = 4,
    /// <summary>A recovery Booking has completed a previously missed requirement.</summary>
    RecoveryCompleted = 5,
}

/// <summary>One deterministic Candidate seed assignment and requested demo journey.</summary>
/// <param name="Name">The demo Candidate full name.</param>
/// <param name="Email">The demo Candidate email address.</param>
/// <param name="EmployeeGroupCode">The canonical Employee Group code.</param>
/// <param name="Journey">The deterministic lifecycle state to construct.</param>
public sealed record CandidateSpec(
    string Name,
    string Email,
    string EmployeeGroupCode,
    DemoCandidateJourney Journey);

public sealed record AgreedSlotSpec(DateOnly Date, TimeOnly StartTime, int DatHeadcount, int MedHeadcount, int UniHeadcount);

public sealed record OpenProposalSpec(
    DateOnly Date,
    TimeOnly StartTime,
    Guid CreatedByManagerUserId,
    int? DatHeadcount,
    int? MedHeadcount,
    int? UniHeadcount);



/// <summary>One deterministic demo access profile and its provider identity fields.</summary>
/// <param name="Username">The unique Keycloak username created for the demo identity.</param>
/// <param name="UserId">The stable identity-provider object identifier.</param>
/// <param name="StaffId">The canonical HR-issued staff number.</param>
/// <param name="Roles">The identity-provider roles mirrored for the staff user.</param>
/// <param name="AppointmentTypeId">The optional EventBooking-owned appointment-type scope.</param>
public sealed record StaffProfileSpec(
    string Username,
    Guid UserId,
    StaffId StaffId,
    IReadOnlyList<Role> Roles,
    Guid? AppointmentTypeId)
{
    /// <summary>Gets the unique Keycloak username created for the demo identity.</summary>
    public string Username { get; init; } = Username;

    /// <summary>Gets the stable identity-provider object identifier.</summary>
    public Guid UserId { get; init; } = UserId;

    /// <summary>Gets the canonical HR-issued staff number.</summary>
    public StaffId StaffId { get; init; } = StaffId;

    /// <summary>Gets the identity-provider roles mirrored for the staff user.</summary>
    public IReadOnlyList<Role> Roles { get; init; } = Roles;

    /// <summary>Gets the EventBooking-owned appointment-type scope, or null when unscoped.</summary>
    public Guid? AppointmentTypeId { get; init; } = AppointmentTypeId;
}

/// <summary>
/// The demo dataset, loaded from the embedded demo-seed.json so it can be edited without
/// touching code. Day offsets resolve against the file's anchor date, so every run matches
/// the same windows and re-runs only ever fill in newly edited placeholders.
/// </summary>
public static class DemoSeedSpec
{
    private static readonly Lazy<SeedDocument> Document = new(Load);

    private static DateOnly? AnchorOverride;

    /// <summary>
    /// Gets the fixed calendar date against which the demo dataset's day offsets resolve.
    /// </summary>
    public static DateOnly AnchorDate() => AnchorOverride ?? Document.Value.Anchor;

    /// <summary>Gets whether a run override replaced the file anchor.</summary>
    public static bool AnchorOverridden => AnchorOverride.HasValue;

    /// <summary>
    /// Overrides the file anchor for this run (the --reanchor option), or restores file
    /// behavior with null. Applies to agreed slots, proposals, and journey windows alike.
    /// </summary>
    public static void OverrideAnchor(DateOnly? anchor) => AnchorOverride = anchor;

    public static IReadOnlyList<StaffProfileSpec> Staff() => Document.Value.Staff;

    public static Guid AdminUserId() =>
        Document.Value.Staff.Single(profile => profile.Roles.SequenceEqual([Role.Admin])).UserId;

    public static Guid CoordinatorUserId() =>
        Document.Value.Staff.Single(profile =>
            profile.Roles.Contains(Role.Coordinator)
            && !profile.Roles.Contains(Role.Manager)
            && !profile.Roles.Contains(Role.AppointmentStaff)).UserId;

    public static IReadOnlyDictionary<Guid, Guid> ManagerForType() =>
        Document.Value.Staff
            .Where(profile => profile.Roles.Contains(Role.Manager))
            .ToDictionary(profile => profile.AppointmentTypeId!.Value, profile => profile.UserId);

    public static IReadOnlyList<AgreedSlotSpec> AgreedSlots() =>
        Document.Value.AgreedSlots
            .Select(s => new AgreedSlotSpec(
                AnchorDate().AddDays(s.DaysOffset), ParseStartTime(s.StartTime),
                s.DatHeadcount, s.MedHeadcount, s.UniHeadcount))
            .ToList();

    public static IReadOnlyList<OpenProposalSpec> OpenProposals() =>
        Document.Value.OpenProposals
            .Select(p => new OpenProposalSpec(
                AnchorDate().AddDays(p.DaysOffset), ParseStartTime(p.StartTime),
                ManagerId(p.CreatedBy), p.DatHeadcount, p.MedHeadcount, p.UniHeadcount))
            .ToList();

    public static IReadOnlyList<CandidateSpec> Candidates() =>
        Document.Value.Candidates
            .Select(c => new CandidateSpec(
                c.Name, c.Email, GroupCode(c.EmployeeGroup), Journey(c.Journey)))
            .ToList();

    private static string GroupCode(string code) =>
        EmployeeGroupIds.TryFromCode(code, out var id)
            ? EmployeeGroupIds.CodeOf(id)
            : throw new SeedException($"Unknown employee group code '{code}' in demo-seed.json.");

    private static DemoCandidateJourney Journey(string value) =>
        Enum.TryParse<DemoCandidateJourney>(value, ignoreCase: true, out var journey)
            && Enum.IsDefined(journey)
            ? journey
            : throw new SeedException(
                $"Journey '{value}' in demo-seed.json must be one of Unbooked, Ready, Outstanding, NoShow, or RecoveryCompleted.");

    private static Guid ManagerId(string username) =>
        Document.Value.StaffByUsername.TryGetValue(username, out var userId)
            ? userId
            : throw new SeedException($"Unknown staff username '{username}' in demo-seed.json.");

    private static Guid TypeId(string code) =>
        AppointmentTypeIds.TryFromCode(code, out var id)
            ? id
            : throw new SeedException($"Unknown appointment type code '{code}' in demo-seed.json.");

    private static TimeOnly ParseStartTime(string value) =>
        TimeOnly.TryParseExact(
            value, "HH:mm", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var startTime)
            ? startTime
            : throw new SeedException($"Start time '{value}' in demo-seed.json must read HH:mm.");

    private static SeedDocument Load()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EventBooking.SeedData.demo-seed.json")
            ?? throw new SeedException("Embedded demo-seed.json is missing.");

        var document = JsonSerializer.Deserialize<SeedFile>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new SeedException("demo-seed.json is empty.");

        var staffById = new HashSet<Guid>();
        var staffIds = new HashSet<StaffId>();
        var staff = document.Staff.Select(row =>
        {
            if (!Guid.TryParse(row.UserId, out var userId) || userId == Guid.Empty)
            {
                throw new SeedException($"Staff entry '{row.Username}' has an invalid userId.");
            }

            if (!staffById.Add(userId))
            {
                throw new SeedException($"Duplicate staff userId '{row.UserId}'.");
            }

            var parsedRoles = row.Roles.Select(roleName =>
            {
                if (!Enum.TryParse<Role>(roleName, ignoreCase: true, out var role)
                    || !Enum.IsDefined(role))
                {
                    throw new SeedException(
                        $"Staff entry '{row.Username}' has an unknown role '{roleName}'.");
                }

                return role;
            }).ToList();

            Guid? appointmentTypeId = row.AppointmentType is null
                ? null
                : TypeId(row.AppointmentType);

            try
            {
                var staffId = new StaffId(row.StaffId);
                if (!staffIds.Add(staffId))
                {
                    throw new SeedException($"Duplicate staffId '{row.StaffId}'.");
                }

                var validated = StaffAccessProfile.Create(
                    userId, parsedRoles, appointmentTypeId);
                return new StaffRow(
                    row.Username,
                    new StaffProfileSpec(
                        row.Username,
                        userId,
                        staffId,
                        validated.Roles.OrderBy(role => role).ToList(),
                        validated.AppointmentTypeId));
            }
            catch (DomainException exception)
            {
                throw new SeedException(
                    $"Staff entry '{row.Username}' is invalid: {exception.Message}");
            }
        }).ToList();

        if (!DateOnly.TryParseExact(
                document.AnchorDate, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var anchor))
        {
            throw new SeedException("anchorDate in demo-seed.json must read yyyy-MM-dd.");
        }

        return new SeedDocument(
            anchor,
            staff.Select(s => s.Assignment).ToList(),
            staff.ToDictionary(s => s.Username, s => s.Assignment.UserId),
            document.AgreedSlots,
            document.OpenProposals,
            document.Candidates);
    }

    private sealed record StaffRow(string Username, StaffProfileSpec Assignment);

    private sealed record SeedDocument(
        DateOnly Anchor,
        IReadOnlyList<StaffProfileSpec> Staff,
        IReadOnlyDictionary<string, Guid> StaffByUsername,
        IReadOnlyList<AgreedSlotRow> AgreedSlots,
        IReadOnlyList<OpenProposalRow> OpenProposals,
        IReadOnlyList<CandidateRow> Candidates);

    private sealed record SeedFile(
        string AnchorDate,
        List<StaffRowFile> Staff,
        List<AgreedSlotRow> AgreedSlots,
        List<OpenProposalRow> OpenProposals,
        List<CandidateRow> Candidates);

    private sealed record StaffRowFile(
        string Username,
        string UserId,
        string StaffId,
        List<string> Roles,
        string? AppointmentType);

    private sealed record AgreedSlotRow(
        int DaysOffset, string StartTime, int DatHeadcount, int MedHeadcount, int UniHeadcount);

    private sealed record OpenProposalRow(
        int DaysOffset, string StartTime, string CreatedBy,
        int? DatHeadcount, int? MedHeadcount, int? UniHeadcount);

    private sealed record CandidateRow(string Name, string Email, string EmployeeGroup, string Journey);
}
`````

## src/EventBooking.SeedData/EventBooking.SeedData.csproj — 1/1

<!-- port-file: {"path":"src/EventBooking.SeedData/EventBooking.SeedData.csproj","encoding":"utf8","sha256":"4432276e1940d801c3e1555739c6b91f173c1986d7074f7a1562940f72e38262","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Application\EventBooking.Application.csproj" />
    <ProjectReference Include="..\EventBooking.Infrastructure\EventBooking.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="demo-seed.json" />
  </ItemGroup>

</Project>
`````

## src/EventBooking.SeedData/KeycloakSeeder.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.SeedData/KeycloakSeeder.cs","encoding":"utf8","sha256":"e659347cc2e2eed9162c04e596996a8053ae442ae20f21a2da691b2f1cf39757","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.SeedData;

/// <summary>Counts Keycloak objects changed by one idempotent convergence.</summary>
/// <param name="RolesCreated">The missing EventBooking realm roles created.</param>
/// <param name="MapperWrites">The missing or drifted `roles` mappers written.</param>
/// <param name="UsersCreated">The missing demo users created.</param>
/// <param name="RoleMappingWrites">The add/remove role-mapping requests made.</param>
public sealed record KeycloakSeedSummary(
    int RolesCreated,
    int MapperWrites,
    int UsersCreated,
    int RoleMappingWrites);

/// <summary>Converges the Keycloak-owned half of the deterministic demo staff seed.</summary>
public sealed class KeycloakSeeder
{
    private static readonly string[] BusinessRoleNames = Enum.GetNames<Role>();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient http;
    private readonly KeycloakSeedOptions options;

    /// <summary>Creates a Keycloak demo seeder over the supplied HTTP transport.</summary>
    /// <param name="http">The transport used for token and Admin API calls.</param>
    /// <param name="options">Validated Keycloak seed settings.</param>
    public KeycloakSeeder(HttpClient http, KeycloakSeedOptions options)
    {
        this.http = http;
        this.options = options;
        this.http.BaseAddress = options.BaseUrl;
    }

    /// <summary>Deletes the configured realm if it exists, then recreates it from a realm export
    /// document. Intended only for a --reseed run: this discards every user, role, and client in
    /// the realm, demo or not.</summary>
    /// <param name="realmExportJson">The full realm representation to import after deletion.</param>
    /// <param name="cancellationToken">Cancels Keycloak network operations.</param>
    /// <exception cref="SeedException">Keycloak rejects the delete or the recreate request.</exception>
    public async Task ResetRealmAsync(string realmExportJson, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var realmPath = $"admin/realms/{Escape(options.Realm)}";
        using var deleteResponse = await http.DeleteAsync(realmPath, cancellationToken);
        if (deleteResponse.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(deleteResponse, realmPath, cancellationToken);
        }

        await SendAsync(
            HttpMethod.Post,
            "admin/realms",
            new StringContent(realmExportJson, System.Text.Encoding.UTF8, "application/json"),
            cancellationToken);
    }

    /// <summary>Ensures realm roles, mapper, demo users, and exact business-role mappings.</summary>
    /// <param name="staff">The canonical demo identity rows.</param>
    /// <param name="cancellationToken">Cancels Keycloak network operations.</param>
    /// <returns>Counts of objects changed by this convergence.</returns>
    /// <exception cref="SeedException">Keycloak rejects a request or identity keys conflict.</exception>
    public async Task<KeycloakSeedSummary> EnsureAsync(
        IReadOnlyList<StaffProfileSpec> staff,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var roleWrites = 0;
        var mapperWrites = 0;
        var userWrites = 0;
        var mappingWrites = 0;
        var roles = new Dictionary<string, RoleRepresentation>(StringComparer.Ordinal);

        foreach (var name in BusinessRoleNames)
        {
            var path = $"admin/realms/{Escape(options.Realm)}/roles/{Escape(name)}";
            var response = await http.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await SendAsync(
                    HttpMethod.Post,
                    $"admin/realms/{Escape(options.Realm)}/roles",
                    JsonContent.Create(new { name, description = $"EventBooking {name}" }),
                    cancellationToken);
                roleWrites++;
                response = await http.GetAsync(path, cancellationToken);
            }

            await EnsureSuccessAsync(response, path, cancellationToken);
            roles[name] = (await response.Content.ReadFromJsonAsync<RoleRepresentation>(
                Json, cancellationToken))
                ?? throw new SeedException($"Keycloak returned an empty role '{name}'.");
        }

        var clientPath =
            $"admin/realms/{Escape(options.Realm)}/clients?clientId=eventbooking-web";
        var clients = await GetAsync<List<ClientRepresentation>>(clientPath, cancellationToken);
        var client = clients.SingleOrDefault(value => value.ClientId == "eventbooking-web")
            ?? throw new SeedException("Keycloak client 'eventbooking-web' does not exist.");
        var mapperPath =
            $"admin/realms/{Escape(options.Realm)}/clients/{Escape(client.Id)}/protocol-mappers/models";
        var mappers = await GetAsync<List<MapperRepresentation>>(mapperPath, cancellationToken);
        var currentMapper = mappers.SingleOrDefault(value => value.Name == "roles");
        var desiredMapper = DesiredMapper(currentMapper?.Id);
        if (currentMapper is null)
        {
            await SendAsync(HttpMethod.Post, mapperPath, JsonContent.Create(desiredMapper, options: Json), cancellationToken);
            mapperWrites++;
        }
        else if (!MapperMatches(currentMapper))
        {
            await SendAsync(
                HttpMethod.Put,
                $"{mapperPath}/{Escape(currentMapper.Id!)}",
                JsonContent.Create(desiredMapper, options: Json),
                cancellationToken);
            mapperWrites++;
        }

        foreach (var person in staff)
        {
            var userPath =
                $"admin/realms/{Escape(options.Realm)}/users?username={Escape(person.Username)}&exact=true&briefRepresentation=false";
            var matches = await GetAsync<List<UserRepresentation>>(userPath, cancellationToken);
            if (matches.Count > 1)
            {
                throw new SeedException(
                    $"Keycloak returned multiple exact users for '{person.Username}'.");
            }

            if (matches.Count == 0)
            {
                await SendAsync(
                    HttpMethod.Post,
                    $"admin/realms/{Escape(options.Realm)}/users",
                    JsonContent.Create(new
                    {
                        id = person.UserId,
                        username = person.Username,
                        enabled = true,
                        attributes = new Dictionary<string, string[]>
                        {
                            ["staffId"] = [person.StaffId.Value],
                        },
                        credentials = new[]
                        {
                            new { type = "password", value = options.DemoPassword, temporary = false },
                        },
                    }, options: Json),
                    cancellationToken);
                userWrites++;
                matches = await GetAsync<List<UserRepresentation>>(userPath, cancellationToken);
                if (matches.Count != 1)
                {
                    throw new SeedException(
                        $"Keycloak did not return the newly created user '{person.Username}'.");
                }

                ValidateIdentity(matches[0], person);
            }
            else
            {
                ValidateIdentity(matches[0], person);
            }

            var id = person.UserId.ToString();
            var mappingsPath =
                $"admin/realms/{Escape(options.Realm)}/users/{Escape(id)}/role-mappings/realm";
            var currentMappings = await GetAsync<List<RoleRepresentation>>(
                mappingsPath, cancellationToken);
            var currentBusiness = currentMappings
                .Where(value => BusinessRoleNames.Contains(value.Name, StringComparer.Ordinal))
                .ToDictionary(value => value.Name, StringComparer.Ordinal);
            var desiredNames = person.Roles.Select(value => value.ToString())
                .ToHashSet(StringComparer.Ordinal);
            var missing = desiredNames.Except(currentBusiness.Keys)
                .Select(name => roles[name]).ToArray();
            var extra = currentBusiness.Keys.Except(desiredNames)
                .Select(name => currentBusiness[name]).ToArray();

            if (missing.Length > 0)
            {
                await SendAsync(HttpMethod.Post, mappingsPath,
                    JsonContent.Create(missing, options: Json), cancellationToken);
                mappingWrites++;
            }
            if (extra.Length > 0)
            {
                await SendAsync(HttpMethod.Delete, mappingsPath,
                    JsonContent.Create(extra, options: Json), cancellationToken);
                mappingWrites++;
            }
        }

        return new KeycloakSeedSummary(roleWrites, mapperWrites, userWrites, mappingWrites);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var path = $"realms/{Escape(options.AdminRealm)}/protocol/openid-connect/token";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = options.AdminUsername,
            ["password"] = options.AdminPassword,
        });
        using var response = await http.PostAsync(path, content, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new SeedException("Keycloak token response omitted access_token.");
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken))
            ?? throw new SeedException($"Keycloak returned an empty response for '{path}'.");
    }

    private async Task SendAsync(
        HttpMethod method,
        string path,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, path, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new SeedException(
            $"Keycloak request '{path}' failed with {(int)response.StatusCode}: {body}");
    }

    private static void ValidateIdentity(UserRepresentation actual, StaffProfileSpec expected)
    {
        if (!Guid.TryParse(actual.Id, out var actualId) || actualId != expected.UserId)
        {
            throw new SeedException(
                $"Keycloak user '{expected.Username}' has a conflicting provider identifier.");
        }

        if (actual.Attributes is null
            || !actual.Attributes.TryGetValue("staffId", out var values)
            || values.Count != 1
            || !string.Equals(values[0], expected.StaffId.Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new SeedException(
                $"Keycloak user '{expected.Username}' has a conflicting staffId attribute.");
        }
    }

    private static MapperRepresentation DesiredMapper(string? id) => new()
    {
        Id = id,
        Name = "roles",
        Protocol = "openid-connect",
        ProtocolMapper = "oidc-usermodel-realm-role-mapper",
        ConsentRequired = false,
        Config = new Dictionary<string, string>
        {
            ["multivalued"] = "true",
            ["claim.name"] = "roles",
            ["jsonType.label"] = "String",
            ["id.token.claim"] = "true",
            ["access.token.claim"] = "true",
        },
    };

    private static bool MapperMatches(MapperRepresentation mapper)
    {
        var desired = DesiredMapper(mapper.Id);
        return mapper.Protocol == desired.Protocol
            && mapper.ProtocolMapper == desired.ProtocolMapper
            && mapper.ConsentRequired == desired.ConsentRequired
            && desired.Config.All(pair =>
                mapper.Config.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private sealed record RoleRepresentation(string Id, string Name);
    private sealed record ClientRepresentation(string Id, string ClientId);
    private sealed record UserRepresentation(
        string Id,
        string Username,
        Dictionary<string, List<string>>? Attributes);
    private sealed class MapperRepresentation
    {
        public string? Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Protocol { get; init; } = string.Empty;
        public string ProtocolMapper { get; init; } = string.Empty;
        public bool ConsentRequired { get; init; }
        public Dictionary<string, string> Config { get; init; } = [];
    }
}
`````

## src/EventBooking.SeedData/KeycloakSeedOptions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.SeedData/KeycloakSeedOptions.cs","encoding":"utf8","sha256":"aa7980756993c0bf28b351c437f07d27e91a4079fdd23d8bafbfeae2d32d1a55","parts":1,"part":1} -->

`````csharp
namespace EventBooking.SeedData;

/// <summary>Validated settings for one opt-in Keycloak demo-seed run.</summary>
public sealed class KeycloakSeedOptions
{
    private static readonly string[] RequiredNames =
    [
        "Keycloak__BaseUrl",
        "Keycloak__AdminUsername",
        "Keycloak__AdminPassword",
        "Keycloak__DemoPassword",
    ];

    private KeycloakSeedOptions(
        Uri baseUrl,
        string realm,
        string adminRealm,
        string adminUsername,
        string adminPassword,
        string demoPassword,
        string? realmExportPath)
    {
        BaseUrl = baseUrl;
        Realm = realm;
        AdminRealm = adminRealm;
        AdminUsername = adminUsername;
        AdminPassword = adminPassword;
        DemoPassword = demoPassword;
        RealmExportPath = realmExportPath;
    }

    /// <summary>Gets the Keycloak origin used for token and Admin API calls.</summary>
    public Uri BaseUrl { get; }

    /// <summary>Gets the realm whose EventBooking demo identities are converged.</summary>
    public string Realm { get; }

    /// <summary>Gets the realm used to authenticate the Keycloak administrator.</summary>
    public string AdminRealm { get; }

    /// <summary>Gets the Keycloak administrator username; never written to progress output.</summary>
    public string AdminUsername { get; }

    /// <summary>Gets the Keycloak administrator password; never written to progress output.</summary>
    public string AdminPassword { get; }

    /// <summary>Gets the non-production password assigned only when a demo user is created.</summary>
    public string DemoPassword { get; }

    /// <summary>Gets the path to a realm export JSON file, required only for a --reseed realm
    /// delete-and-recreate; unused by ordinary convergence.</summary>
    public string? RealmExportPath { get; }

    /// <summary>Reads Keycloak settings from process environment variables.</summary>
    public static KeycloakSeedOptions? FromEnvironment() =>
        From(Environment.GetEnvironmentVariable);

    /// <summary>
    /// Returns null when every Keycloak setting is absent and otherwise validates the complete set.
    /// </summary>
    /// <param name="readSetting">Reads one setting by its environment-variable name.</param>
    /// <returns>Validated settings, or null when Keycloak seeding is disabled.</returns>
    /// <exception cref="SeedException">A required setting is absent or the base URL is invalid.</exception>
    public static KeycloakSeedOptions? From(Func<string, string?> readSetting)
    {
        ArgumentNullException.ThrowIfNull(readSetting);

        var values = RequiredNames.ToDictionary(name => name, readSetting);
        values["Keycloak__Realm"] = readSetting("Keycloak__Realm");
        values["Keycloak__AdminRealm"] = readSetting("Keycloak__AdminRealm");
        var realmExportPath = readSetting("Keycloak__RealmExportPath");

        if (values.Values.All(string.IsNullOrWhiteSpace) && string.IsNullOrWhiteSpace(realmExportPath))
        {
            return null;
        }

        foreach (var name in RequiredNames)
        {
            if (string.IsNullOrWhiteSpace(values[name]))
            {
                throw new SeedException($"Keycloak seed setting '{name}' is required.");
            }
        }

        if (!Uri.TryCreate(values["Keycloak__BaseUrl"], UriKind.Absolute, out var baseUrl)
            || (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new SeedException(
                "Keycloak seed setting 'Keycloak__BaseUrl' must be an absolute HTTP(S) URL.");
        }

        return new KeycloakSeedOptions(
            baseUrl,
            string.IsNullOrWhiteSpace(values["Keycloak__Realm"])
                ? "eventbooking"
                : values["Keycloak__Realm"]!,
            string.IsNullOrWhiteSpace(values["Keycloak__AdminRealm"])
                ? "master"
                : values["Keycloak__AdminRealm"]!,
            values["Keycloak__AdminUsername"]!,
            values["Keycloak__AdminPassword"]!,
            values["Keycloak__DemoPassword"]!,
            string.IsNullOrWhiteSpace(realmExportPath) ? null : realmExportPath);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"KeycloakSeedOptions {{ BaseUrl = {BaseUrl}, Realm = {Realm}, AdminRealm = {AdminRealm}, AdminUsername = [REDACTED], AdminPassword = [REDACTED], DemoPassword = [REDACTED], RealmExportPath = {RealmExportPath ?? "(none)"} }}";
}
`````

## src/EventBooking.SeedData/KeycloakSeedStep.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.SeedData/KeycloakSeedStep.cs","encoding":"utf8","sha256":"9d6cdc43d967064146611b56aaa38be0b06ea2db2497cb07dc9ae9631975027d","parts":1,"part":1} -->

`````csharp
namespace EventBooking.SeedData;

/// <summary>Runs optional Keycloak convergence, and a --reseed realm delete-and-recreate, at the
/// seed-host boundary.</summary>
/// <param name="readSetting">Reads one environment-style setting by name.</param>
/// <param name="createHttpClient">Creates HTTP transport only for a configured provider run.</param>
/// <param name="readFile">Reads a realm export file's full contents, only for a --reseed run.</param>
public sealed class KeycloakSeedStep(
    Func<string, string?> readSetting,
    Func<HttpClient> createHttpClient,
    Func<string, string>? readFile = null)
{
    /// <summary>Runs provider convergence unless all seed work or Keycloak itself is disabled. When
    /// <paramref name="reseed"/> is also requested, first deletes and recreates the configured realm
    /// from <c>Keycloak__RealmExportPath</c> before converging demo users into it.</summary>
    /// <param name="skipSeed">Whether the host is in migrations-only mode.</param>
    /// <param name="reseed">Whether the database is also being fully reseeded.</param>
    /// <param name="staff">The canonical provider and database identity rows.</param>
    /// <param name="cancellationToken">Cancels provider requests.</param>
    /// <returns>The provider change summary, or null when no provider work was attempted.</returns>
    /// <exception cref="SeedException">
    /// A reseed is requested but <c>Keycloak__RealmExportPath</c> is not set, or Keycloak rejects
    /// the realm delete, recreate, or convergence requests.
    /// </exception>
    public async Task<KeycloakSeedSummary?> RunAsync(
        bool skipSeed,
        bool reseed,
        IReadOnlyList<StaffProfileSpec> staff,
        CancellationToken cancellationToken)
    {
        if (skipSeed)
        {
            return null;
        }

        var options = KeycloakSeedOptions.From(readSetting);
        if (options is null)
        {
            return null;
        }

        if (reseed && string.IsNullOrWhiteSpace(options.RealmExportPath))
        {
            throw new SeedException(
                "Keycloak seed setting 'Keycloak__RealmExportPath' is required for --reseed.");
        }

        using var http = createHttpClient();
        var seeder = new KeycloakSeeder(http, options);

        if (reseed)
        {
            var reader = readFile ?? File.ReadAllText;
            var realmExportJson = reader(options.RealmExportPath!);
            await seeder.ResetRealmAsync(realmExportJson, cancellationToken);
        }

        return await seeder.EnsureAsync(staff, cancellationToken);
    }
}
`````
