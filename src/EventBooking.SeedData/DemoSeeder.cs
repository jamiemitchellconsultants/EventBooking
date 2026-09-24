using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Counts the deterministic rows created by one idempotent seed operation.</summary>
public sealed record SeedSummary(
    int LocationsEnsured,
    int AppointmentTypesEnsured,
    int AttendeeGroupsEnsured,
    int IdentitiesEnsured,
    int ProfilesEnsured,
    int EventsEnsured,
    int ProposalsEnsured,
    int AcceptancesApplied,
    int AttendeesEnsured)
{
    public static readonly SeedSummary Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed class SeedException(string message) : Exception(message);

/// <summary>
/// Applies <see cref="DemoSeedSpec"/> through natural-key upserts so every re-run converges:
/// existing rows are matched by code, email or location window and skipped, and a row that
/// exists with a different natural key fails loudly instead of duplicating.
/// Reseeding instead wipes every domain table first, so a database mutated by a demo comes
/// back to exactly the seed state.
/// </summary>
public sealed class DemoSeeder(
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IAttendeeRepository attendees,
    IAttendeeGroupRepository groups,
    SaveAttendeeHandler saveAttendee,
    IUnitOfWork unitOfWork,
    IClock clock,
    IEventWindowZones zones,
    EventBookingDbContext database)
{
    /// <summary>
    /// Gets or sets the verbose progress sink. Defaults to <see cref="TextWriter.Null" />;
    /// the console host assigns <see cref="Console.Out" /> when --verbose is passed.
    /// </summary>
    public TextWriter Progress { get; set; } = TextWriter.Null;

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");

    private static IReadOnlyList<StaffProfileSpec> Staff() => DemoSeedSpec.Staff();

    private static Guid CoordinatorUserId() => DemoSeedSpec.CoordinatorUserId();

    public async Task<SeedSummary> RunAsync(CancellationToken ct)
    {
        var data = DemoSeedSpec.Build();
        var locations = await EnsureLocationsAsync(data, ct);
        var types = await EnsureAppointmentTypesAsync(data, ct);
        var groups = await EnsureAttendeeGroupsAsync(data, ct);
        await EnsureSystemSettingsAsync(ct);
        var identities = await EnsureIdentitiesAsync(ct);
        var profiles = await EnsureProfilesAsync(ct);
        var events = await EnsureEventsAsync(data, ct);
        var proposals = await EnsureOpenProposalsAsync(data, ct);
        var attendees = await EnsureAttendeesAsync(data, ct);
        return new SeedSummary(locations, types, groups, identities, profiles, events, proposals, 0, attendees);
    }

    public async Task<SeedSummary> ReseedAsync(CancellationToken ct)
    {
        await database.Database.ExecuteSqlRawAsync(
            """
            DO $$ DECLARE row record;
            BEGIN
              FOR row IN SELECT tablename FROM pg_tables
                         WHERE schemaname='public' AND tablename <> '__EFMigrationsHistory'
              LOOP EXECUTE 'TRUNCATE TABLE ' || quote_ident(row.tablename) || ' CASCADE'; END LOOP;
            END $$;
            """, ct);
        database.ChangeTracker.Clear();
        return await RunAsync(ct);
    }

    public async Task ReanchorAsync(DemoDataset target, CancellationToken ct)
    {
        var eventIds = target.Events.Select(x => x.Id).ToList();
        var proposalIds = target.OpenProposals.Select(x => x.Id).ToList();
        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        var locationByCode = await database.Locations.AsNoTracking().ToDictionaryAsync(x => x.Code, ct);
        var existingEvents = await database.Events.AsNoTracking()
            .Where(x => eventIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id, x.LocationId, x.ProposalId,
                Date = x.Window.Date, Start = x.Window.StartTime, x.Window.DurationMinutes,
            })
            .ToListAsync(ct);

        foreach (var spec in target.Events)
        {
            var current = existingEvents.SingleOrDefault(x => x.Id == spec.Id);
            if (current is null) continue;
            var locationId = locationByCode[spec.LocationCode].Id;
            if (current.LocationId != locationId || current.Start != spec.StartTime
                || current.DurationMinutes != spec.DurationMinutes || current.ProposalId == Guid.Empty)
                throw new SeedException($"Demo Event {spec.Id} no longer has its seeded identity.");
            var collision = await database.Events.AnyAsync(x => x.Id != spec.Id
                && x.LocationId == locationId && x.Window.Date == spec.Date
                && x.Window.StartTime == spec.StartTime, ct);
            if (collision)
                throw new SeedException($"Cannot reanchor demo Event {spec.Id}; its target window is occupied.");
            if (current.Date == spec.Date) continue;
            var eventsMoved = await database.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "event" SET "date" = {spec.Date} WHERE "id" = {spec.Id}""", ct);
            var proposalsMoved = await database.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "event_proposal" SET "date" = {spec.Date} WHERE "id" = {current.ProposalId}""", ct);
            if (eventsMoved != 1 || proposalsMoved != 1)
                throw new SeedException($"Demo Event {spec.Id} could not be reanchored atomically.");
        }

        var existingProposals = await database.EventProposals.AsNoTracking()
            .Where(x => proposalIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id, x.LocationId, x.Status,
                Date = x.Window.Date, Start = x.Window.StartTime, x.Window.DurationMinutes,
            })
            .ToListAsync(ct);
        foreach (var spec in target.OpenProposals)
        {
            var current = existingProposals.SingleOrDefault(x => x.Id == spec.Id);
            if (current is null) continue;
            var locationId = locationByCode[spec.LocationCode].Id;
            if (current.LocationId != locationId || current.Start != spec.StartTime
                || current.DurationMinutes != spec.DurationMinutes || current.Status != EventProposalStatus.Open)
                throw new SeedException($"Demo EventProposal {spec.Id} no longer has its seeded identity.");
            var collision = await database.EventProposals.AnyAsync(x => x.Id != spec.Id
                && x.LocationId == locationId && x.Window.Date == spec.Date
                && x.Window.StartTime == spec.StartTime, ct);
            if (collision)
                throw new SeedException($"Cannot reanchor demo EventProposal {spec.Id}; its target window is occupied.");
            if (current.Date == spec.Date) continue;
            if (await database.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "event_proposal" SET "date" = {spec.Date} WHERE "id" = {spec.Id}""", ct) != 1)
                throw new SeedException($"Demo EventProposal {spec.Id} could not be reanchored.");
        }

        await transaction.CommitAsync(ct);
        database.ChangeTracker.Clear();
    }

    private async Task EnsureSystemSettingsAsync(CancellationToken ct)
    {
        if (!await database.SystemSettings.AnyAsync(ct))
        {
            database.SystemSettings.Add(SystemSettings.CreateDefault());
            await database.SaveChangesAsync(ct);
        }
    }

    private async Task<int> EnsureLocationsAsync(DemoDataset data, CancellationToken ct)
    {
        var added = 0;
        foreach (var spec in data.Locations)
        {
            var current = await database.Locations.SingleOrDefaultAsync(x => x.Code == spec.Code, ct);
            if (current is null)
            {
                current = Location.Create(spec.Id, spec.Code, spec.Name, spec.Address, spec.TimeZoneId, zones);
                if (!spec.IsActive) current.Deactivate(LocationUsage.None);
                database.Locations.Add(current);
                added++;
            }
            else if (current.Id != spec.Id)
                throw new SeedException($"Location code {spec.Code} belongs to a non-demo identifier.");
        }
        await database.SaveChangesAsync(ct);
        return added;
    }

    private async Task<int> EnsureAppointmentTypesAsync(DemoDataset data, CancellationToken ct)
    {
        var added = 0;
        foreach (var spec in data.AppointmentTypes)
        {
            var current = await database.AppointmentTypes.SingleOrDefaultAsync(x => x.Code == spec.Code, ct);
            if (current is null)
            {
                current = AppointmentType.Create(spec.Id, spec.Code, spec.Name);
                database.AppointmentTypes.Add(current);
                added++;
            }
            else if (current.Id != spec.Id)
                throw new SeedException($"Appointment type code {spec.Code} belongs to a non-demo identifier.");
        }
        await database.SaveChangesAsync(ct);
        return added;
    }

    private async Task<int> EnsureAttendeeGroupsAsync(DemoDataset data, CancellationToken ct)
    {
        var typeByCode = await database.AppointmentTypes.ToDictionaryAsync(x => x.Code, ct);
        var added = 0;
        foreach (var spec in data.AttendeeGroups)
        {
            var current = await database.AttendeeGroups.SingleOrDefaultAsync(x => x.Code == spec.Code, ct);
            if (current is null)
            {
                var ids = spec.TypeCodes.Select(code => typeByCode[code].Id).ToList();
                current = AttendeeGroup.Create(spec.Id, spec.Code, spec.Name, ids, ids);
                if (!spec.IsActive) current.Deactivate(0);
                database.AttendeeGroups.Add(current);
                added++;
            }
            else if (current.Id != spec.Id)
                throw new SeedException($"Attendee group code {spec.Code} belongs to a non-demo identifier.");
        }
        await database.SaveChangesAsync(ct);
        var doc = typeByCode["DOC"];
        if (doc.IsActive)
        {
            doc.Deactivate(AppointmentTypeUsage.None);
            await database.SaveChangesAsync(ct);
        }
        return added;
    }

    private async Task<int> EnsureEventsAsync(DemoDataset data, CancellationToken ct)
    {
        var locationByCode = await database.Locations.ToDictionaryAsync(x => x.Code, ct);
        var added = 0;
        foreach (var spec in data.Events)
        {
            var locationId = locationByCode[spec.LocationCode].Id;
            var current = await database.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == spec.Id, ct);
            if (current is not null)
            {
                if (current.LocationId != locationId || current.Window.Date != spec.Date
                    || current.Window.StartTime != spec.StartTime
                    || current.Window.DurationMinutes != spec.DurationMinutes)
                    throw new SeedException($"Demo Event {spec.Id} exists with a different natural key; use --reanchor.");
                continue;
            }
            if (await database.Events.AnyAsync(x => x.LocationId == locationId
                && x.Window.Date == spec.Date && x.Window.StartTime == spec.StartTime, ct))
                throw new SeedException($"The demo Event target window for {spec.Id} belongs to a non-demo row.");
            DemoEventFactory.CreateEvent(database, spec, data, zones);
            await database.SaveChangesAsync(ct);
            added++;
        }
        return added;
    }

    private async Task<int> EnsureOpenProposalsAsync(DemoDataset data, CancellationToken ct)
    {
        var locationByCode = await database.Locations.ToDictionaryAsync(x => x.Code, ct);
        var added = 0;
        foreach (var spec in data.OpenProposals)
        {
            var locationId = locationByCode[spec.LocationCode].Id;
            var current = await database.EventProposals.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == spec.Id, ct);
            if (current is not null)
            {
                if (current.Status != EventProposalStatus.Open || current.LocationId != locationId
                    || current.Window.Date != spec.Date || current.Window.StartTime != spec.StartTime
                    || current.Window.DurationMinutes != spec.DurationMinutes)
                    throw new SeedException($"Demo EventProposal {spec.Id} exists with a different natural key; use --reanchor.");
                continue;
            }
            if (await database.EventProposals.AnyAsync(x => x.LocationId == locationId
                && x.Window.Date == spec.Date && x.Window.StartTime == spec.StartTime, ct))
                throw new SeedException($"The demo EventProposal target window for {spec.Id} belongs to a non-demo row.");
            DemoEventFactory.CreateProposal(database, spec, data, zones);
            await database.SaveChangesAsync(ct);
            added++;
        }
        return added;
    }

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

    /// <summary>Derives one stable identifier from a attendee email and a seed role name.</summary>
    private static Guid SeedId(string email, string role) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{email}:{role}")));

    private async Task<int> EnsureAttendeesAsync(DemoDataset data, CancellationToken ct)
    {
        var created = 0;
        foreach (var spec in data.Attendees)
        {
            if (await attendees.GetByEmailAsync(spec.Email, ct) is not null) continue;
            var groupSpec = data.AttendeeGroups.Single(x => x.Code == spec.GroupCode);
            if (!groupSpec.IsActive)
                throw new SeedException($"Demo attendee {spec.Email} cannot use inactive group {spec.GroupCode}.");
            var group = await groups.GetByCodeAsync(spec.GroupCode, ct)
                ?? throw new SeedException($"Demo AttendeeGroup {spec.GroupCode} is missing.");
            var result = await saveAttendee.CreateAsync(
                new CreateAttendeeCommand(CoordinatorUserId(), spec.Name, spec.Email, group.Id), ct);
            if (result.IsFailure)
                throw new SeedException($"Creating demo Attendee {spec.Email} failed: {result.Error}.");
            var attendee = await attendees.GetAsync(result.Value, ct)
                ?? throw new SeedException($"Created demo Attendee {spec.Email} cannot be loaded.");
            await BuildJourneyAsync(attendee, group, groupSpec, spec, data, ct);
            created++;
        }
        return created;
    }

    private async Task BuildJourneyAsync(
        Attendee attendee,
        AttendeeGroup group,
        DemoAttendeeGroupSpec groupSpec,
        DemoAttendeeSpec spec,
        DemoDataset data,
        CancellationToken ct)
    {
        var now = clock.UtcNow;
        switch (spec.Status)
        {
            case AttendeeStatus.NotYetInvited:
                return;
            case AttendeeStatus.AwaitingAvailability:
                attendee.MarkAwaitingAvailability(now);
                await unitOfWork.SaveChangesAsync(ct);
                return;
            case AttendeeStatus.Invited:
            case AttendeeStatus.NoResponseNeedsFollowUp:
            {
                var invite = CreateInitialInvite(attendee, group, groupSpec, spec, data, now);
                database.Invites.Add(invite);
                attendee.MarkInvited(now);
                if (spec.Status == AttendeeStatus.NoResponseNeedsFollowUp)
                {
                    invite.MarkExpired();
                    attendee.MarkNoResponse(now);
                }
                await unitOfWork.SaveChangesAsync(ct);
                return;
            }
            case AttendeeStatus.Booked:
                await BuildBookedJourneyAsync(attendee, group, groupSpec, spec, data, now, ct);
                return;
            default:
                throw new SeedException($"Demo AttendeeStatus {spec.Status} is not supported.");
        }
    }

    private async Task BuildBookedJourneyAsync(
        Attendee attendee,
        AttendeeGroup group,
        DemoAttendeeGroupSpec groupSpec,
        DemoAttendeeSpec spec,
        DemoDataset data,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (spec.AppointmentStatuses.Count == 0)
            throw new SeedException($"Booked demo Attendee {spec.Email} needs an appointment status.");
        if (spec.Recovery != DemoRecovery.None
            && spec.AppointmentStatuses[0] != BookingAppointmentStatus.NoShow)
            throw new SeedException($"Recovery demo Attendee {spec.Email} must begin with NoShow.");
        if (spec.Recovery == DemoRecovery.Completed && spec.AppointmentStatuses.Count != 2)
            throw new SeedException($"Completed recovery demo Attendee {spec.Email} needs two statuses.");
        var eligible = EligibleEvents(groupSpec, data);
        if (eligible.Count < 5 && spec.Recovery != DemoRecovery.None)
            throw new SeedException($"Recovery demo Attendee {spec.Email} needs five eligible Events.");
        var initialOptions = eligible.Take(3).ToList();
        var invite = CreateInitialInvite(attendee, group, spec, initialOptions, data, now);
        database.Invites.Add(invite);
        attendee.MarkInvited(now);
        var booking = Booking.Create(
            SeedId(attendee.Email, "initial:booking"), invite, initialOptions[0].Id, now);
        invite.MarkUsed();
        attendee.MarkBooked(now);
        database.Bookings.Add(booking);
        var appointments = group.RequiredAppointmentTypeIds.Order()
            .Select(typeId => BookingAppointment.Create(
                SeedId(attendee.Email, $"initial:appointment:{typeId}"), booking.Id, typeId))
            .ToList();
        database.BookingAppointments.AddRange(appointments);
        ApplyAppointmentStatus(appointments[0], spec.AppointmentStatuses[0], now);

        if (spec.Recovery != DemoRecovery.None)
        {
            var recoveryOptions = eligible.Skip(2).Take(3).ToList();
            var locationByCode = data.Locations.ToDictionary(x => x.Code);
            var originalLocationId = locationByCode[initialOptions[0].LocationCode].Id;
            var otherLocationIds = recoveryOptions.Select(x => locationByCode[x.LocationCode].Id)
                .Where(x => x != originalLocationId).Distinct().ToList();
            var recoveryInvite = Invite.CreateRecovery(
                SeedId(attendee.Email, "recovery:invite"), attendee.Id, booking.Id, now.AddDays(7),
                originalLocationId, otherLocationIds, recoveryOptions.Select(x => x.Id),
                [appointments[0].AppointmentTypeId]);
            database.Invites.Add(recoveryInvite);

            if (spec.Recovery == DemoRecovery.Completed)
            {
                var recovery = Booking.CreateRecovery(
                    SeedId(attendee.Email, "recovery:booking"), recoveryInvite, booking,
                    recoveryOptions[0].Id, now.AddMinutes(5));
                recoveryInvite.MarkUsed();
                database.Bookings.Add(recovery);
                var recoveryAppointment = BookingAppointment.Create(
                    SeedId(attendee.Email, "recovery:appointment"), recovery.Id,
                    appointments[0].AppointmentTypeId);
                database.BookingAppointments.Add(recoveryAppointment);
                ApplyAppointmentStatus(recoveryAppointment, spec.AppointmentStatuses[1], now);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private Invite CreateInitialInvite(
        Attendee attendee,
        AttendeeGroup group,
        DemoAttendeeGroupSpec groupSpec,
        DemoAttendeeSpec spec,
        DemoDataset data,
        DateTimeOffset now) =>
        CreateInitialInvite(attendee, group, spec, EligibleEvents(groupSpec, data).Take(3).ToList(), data, now);

    private Invite CreateInitialInvite(
        Attendee attendee,
        AttendeeGroup group,
        DemoAttendeeSpec spec,
        IReadOnlyList<DemoEventSpec> options,
        DemoDataset data,
        DateTimeOffset now)
    {
        if (options.Count != 3)
            throw new SeedException($"Demo Attendee {spec.Email} needs exactly three eligible Events.");
        var locationByCode = data.Locations.ToDictionary(x => x.Code);
        var locationIds = options.Select(x => locationByCode[x.LocationCode].Id).Distinct().ToList();
        return Invite.CreateInitial(
            SeedId(attendee.Email, "initial:invite"), attendee.Id, now.AddDays(7), locationIds,
            options.Select(x => x.Id), group.RequiredAppointmentTypeIds, 0);
    }

    private static IReadOnlyList<DemoEventSpec> EligibleEvents(
        DemoAttendeeGroupSpec group, DemoDataset data) => data.Events
        .Where(item => group.TypeCodes.All(code => item.TypeCodes.Contains(code)))
        .OrderBy(item => item.Date).ThenBy(item => item.StartTime).ToList();

    private void ApplyAppointmentStatus(
        BookingAppointment appointment, BookingAppointmentStatus status, DateTimeOffset now)
    {
        switch (status)
        {
            case BookingAppointmentStatus.Expected:
                return;
            case BookingAppointmentStatus.CheckedIn:
                appointment.TransitionTo(status, CoordinatorUserId(), now,
                    checkInAllowed: true, noShowAllowed: false);
                return;
            case BookingAppointmentStatus.Completed:
                appointment.TransitionTo(BookingAppointmentStatus.CheckedIn, CoordinatorUserId(), now,
                    checkInAllowed: true, noShowAllowed: false);
                appointment.TransitionTo(status, CoordinatorUserId(), now,
                    checkInAllowed: true, noShowAllowed: false);
                return;
            case BookingAppointmentStatus.NoShow:
                appointment.TransitionTo(status, CoordinatorUserId(), now,
                    checkInAllowed: false, noShowAllowed: true);
                return;
            default:
                throw new SeedException($"Demo BookingAppointmentStatus {status} is not supported.");
        }
    }
}
