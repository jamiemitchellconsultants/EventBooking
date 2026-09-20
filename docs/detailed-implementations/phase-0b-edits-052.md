# 00b — Vocabulary edits 52 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.SeedData/DemoSeedSpec.cs — 1/1

<!-- vocabulary-file: {"id":182,"oldPath":"src/EventBooking.SeedData/DemoSeedSpec.cs","newPath":"src/EventBooking.SeedData/DemoSeedSpec.cs","beforeSha":"29a7c06109e485291124b201658d17e474a012db587aaae45b01fb028affeacd","afterSha":"e84cc5428d0f3adda53323a6c52264abe66b9e31740c76744f76af08774247e0","side":"after","part":1,"parts":1} -->

`````csharp
using System.Reflection;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.SeedData;

/// <summary>The deterministic lifecycle state constructed for a demo Attendee.</summary>
public enum DemoAttendeeJourney
{
    /// <summary>The Attendee has a group but no Booking.</summary>
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

/// <summary>One deterministic Attendee seed assignment and requested demo journey.</summary>
/// <param name="Name">The demo Attendee full name.</param>
/// <param name="Email">The demo Attendee email address.</param>
/// <param name="AttendeeGroupCode">The canonical Attendee Group code.</param>
/// <param name="Journey">The deterministic lifecycle state to construct.</param>
public sealed record AttendeeSpec(
    string Name,
    string Email,
    string AttendeeGroupCode,
    DemoAttendeeJourney Journey);

public sealed record AgreedEventSpec(DateOnly Date, TimeOnly StartTime, int DatHeadcount, int MedHeadcount, int UniHeadcount);

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
    /// behavior with null. Applies to agreed events, proposals, and journey windows alike.
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

    public static IReadOnlyList<AgreedEventSpec> AgreedEvents() =>
        Document.Value.AgreedEvents
            .Select(s => new AgreedEventSpec(
                AnchorDate().AddDays(s.DaysOffset), ParseStartTime(s.StartTime),
                s.DatHeadcount, s.MedHeadcount, s.UniHeadcount))
            .ToList();

    public static IReadOnlyList<OpenProposalSpec> OpenProposals() =>
        Document.Value.OpenProposals
            .Select(p => new OpenProposalSpec(
                AnchorDate().AddDays(p.DaysOffset), ParseStartTime(p.StartTime),
                ManagerId(p.CreatedBy), p.DatHeadcount, p.MedHeadcount, p.UniHeadcount))
            .ToList();

    public static IReadOnlyList<AttendeeSpec> Attendees() =>
        Document.Value.Attendees
            .Select(c => new AttendeeSpec(
                c.Name, c.Email, GroupCode(c.AttendeeGroup), Journey(c.Journey)))
            .ToList();

    private static string GroupCode(string code) =>
        AttendeeGroupIds.TryFromCode(code, out var id)
            ? AttendeeGroupIds.CodeOf(id)
            : throw new SeedException($"Unknown attendee group code '{code}' in demo-seed.json.");

    private static DemoAttendeeJourney Journey(string value) =>
        Enum.TryParse<DemoAttendeeJourney>(value, ignoreCase: true, out var journey)
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
            document.AgreedEvents,
            document.OpenProposals,
            document.Attendees);
    }

    private sealed record StaffRow(string Username, StaffProfileSpec Assignment);

    private sealed record SeedDocument(
        DateOnly Anchor,
        IReadOnlyList<StaffProfileSpec> Staff,
        IReadOnlyDictionary<string, Guid> StaffByUsername,
        IReadOnlyList<AgreedEventRow> AgreedEvents,
        IReadOnlyList<OpenProposalRow> OpenProposals,
        IReadOnlyList<AttendeeRow> Attendees);

    private sealed record SeedFile(
        string AnchorDate,
        List<StaffRowFile> Staff,
        List<AgreedEventRow> AgreedEvents,
        List<OpenProposalRow> OpenProposals,
        List<AttendeeRow> Attendees);

    private sealed record StaffRowFile(
        string Username,
        string UserId,
        string StaffId,
        List<string> Roles,
        string? AppointmentType);

    private sealed record AgreedEventRow(
        int DaysOffset, string StartTime, int DatHeadcount, int MedHeadcount, int UniHeadcount);

    private sealed record OpenProposalRow(
        int DaysOffset, string StartTime, string CreatedBy,
        int? DatHeadcount, int? MedHeadcount, int? UniHeadcount);

    private sealed record AttendeeRow(string Name, string Email, string AttendeeGroup, string Journey);
}
`````

## before — src/EventBooking.SeedData/DemoSeeder.cs — 1/1

<!-- vocabulary-file: {"id":183,"oldPath":"src/EventBooking.SeedData/DemoSeeder.cs","newPath":"src/EventBooking.SeedData/DemoSeeder.cs","beforeSha":"1febbeaf3197c97759fef8a01450852c355350adeda8735322b2a7109567ee57","afterSha":"c33ab24d5d5d28773e47df32dac20cdfec88adebef56977b7284d41f81becec8","side":"before","part":1,"parts":1} -->

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
