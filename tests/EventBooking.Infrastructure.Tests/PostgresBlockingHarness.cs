using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// Shared base for the reference-data blocking suites: a migrated context, seed helpers for
/// locations, proposals, events, groups, members, invites and bookings, the real EF
/// repositories and blocking queries bound to that context, and recording doubles for audit,
/// the unit of work, the clock and the authorizer profiles.
/// </summary>
public abstract class PostgresBlockingHarness(PostgresFixture fixture) : IAsyncLifetime
{
    protected static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    protected static Guid MedId => AppointmentTypeIds.MedicalCheckUp;

    protected EventBookingDbContext Context { get; private set; } = null!;

    protected AttendeeGroupRepository GroupRepository { get; private set; } = null!;

    protected AppointmentTypeRepository TypeRepository { get; private set; } = null!;

    protected AttendeeRepository AttendeeRepository { get; private set; } = null!;

    protected InviteRepository InviteRepository { get; private set; } = null!;

    protected ReferenceDataBlockingQueries Queries { get; private set; } = null!;

    protected HarnessProfiles Profiles { get; private set; } = null!;

    protected HarnessUnitOfWork UnitOfWork { get; private set; } = null!;

    protected HarnessAudit Audit { get; private set; } = null!;

    protected HarnessClock Clock { get; private set; } = null!;

    protected int SaveCount => UnitOfWork.SaveCount;

    private int _proposalStarts;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        Context = fixture.NewContext();
        GroupRepository = new AttendeeGroupRepository(Context);
        TypeRepository = new AppointmentTypeRepository(Context);
        AttendeeRepository = new AttendeeRepository(Context);
        InviteRepository = new InviteRepository(Context);
        Queries = new ReferenceDataBlockingQueries(Context);
        Profiles = new HarnessProfiles();
        UnitOfWork = new HarnessUnitOfWork(Context);
        Audit = new HarnessAudit();
        Clock = new HarnessClock();
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
    }

    protected async Task<Location> SeedLocationAsync(string code, string timeZoneId)
    {
        var location = Location.Create(
            Guid.NewGuid(), code, $"{code} name", $"{code} address", timeZoneId,
            new NodaTimeEventWindowZones());
        Context.Locations.Add(location);
        await Context.SaveChangesAsync();
        return location;
    }

    protected async Task<EventProposal> SeedOpenProposalAsync(Guid locationId)
    {
        // Successive proposals take successive half-hour windows: two open proposals at one
        // location may not share a local window.
        var start = new TimeOnly(9, 0).Add(TimeSpan.FromMinutes(30 * _proposalStarts++));
        var proposal = EventProposal.Propose(
            Guid.NewGuid(),
            locationId,
            locationIsActive: true,
            "Europe/London",
            new EventWindow(new DateOnly(2027, 3, 10), start, 240),
            new NodaTimeEventWindowZones(),
            Now,
            [
                new(AppointmentTypeIds.DrugAndAlcoholTesting, "DAT", true, true),
                new(AppointmentTypeIds.MedicalCheckUp, "MED", true, true),
                new(AppointmentTypeIds.UniformFitting, "UNI", true, true),
            ],
            AppointmentTypeIds.DrugAndAlcoholTesting,
            Guid.NewGuid(),
            headcount: 1);
        Context.EventProposals.Add(proposal);
        await Context.SaveChangesAsync();
        return proposal;
    }

    protected async Task<Event> SeedFutureEventAsync(Guid locationId)
    {
        var proposal = await SeedOpenProposalAsync(locationId);
        var manager = Guid.NewGuid();
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, manager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, manager, 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, manager, 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        Context.Events.Add(eventItem);
        await Context.SaveChangesAsync();
        return eventItem;
    }

    // Proposals only offer canonical appointment types, so the usage seed counts MED. The
    // attendee_group table survives fixture resets, so the caller's group expectation must be
    // a delta over the pre-seed count, never an absolute number.
    protected async Task<(Guid TypeId, Location Location)> SeedTypeWithProposalEventAndGroupAsync()
    {
        var location = await SeedLocationAsync("TYPETOWN", "Europe/London");
        var manager = Guid.NewGuid();

        var open = EventProposal.Propose(
            Guid.NewGuid(),
            location.Id,
            locationIsActive: true,
            "Europe/London",
            new EventWindow(new DateOnly(2027, 4, 10), new TimeOnly(9, 0), 240),
            new NodaTimeEventWindowZones(),
            Now,
            [new(MedId, "MED", true, true)],
            MedId,
            manager,
            headcount: 1);
        Context.EventProposals.Add(open);
        await Context.SaveChangesAsync();

        var listed = EventProposal.Propose(
            Guid.NewGuid(),
            location.Id,
            locationIsActive: true,
            "Europe/London",
            new EventWindow(new DateOnly(2027, 5, 10), new TimeOnly(9, 0), 240),
            new NodaTimeEventWindowZones(),
            Now,
            [new(MedId, "MED", true, true)],
            MedId,
            manager,
            headcount: 1);
        Context.EventProposals.Add(listed);
        await Context.SaveChangesAsync();
        listed.Accept(MedId, manager, 10);
        Context.Events.Add(Event.CreateFrom(Guid.NewGuid(), listed));

        var group = AttendeeGroup.Create(
            Guid.NewGuid(), $"MAPPED_{Guid.NewGuid():N}".ToUpperInvariant(), "Mapped group",
            [MedId], AppointmentTypeIds.All);
        Context.AttendeeGroups.Add(group);

        await Context.SaveChangesAsync();

        return (MedId, location);
    }

    protected async Task<AppointmentType> AddTypeAsync(string code, string name)
    {
        var type = AppointmentType.Create(Guid.NewGuid(), code, name);
        Context.AppointmentTypes.Add(type);
        await Context.SaveChangesAsync();
        return type;
    }

    protected async Task<AttendeeGroup> SeedGroupWithTwoMembersAsync(bool blockActiveBooking)
    {
        // The attendee_group table survives fixture resets: every seed takes a unique
        // code or the second test to seed collides with the first.
        var group = AttendeeGroup.Create(
            Guid.NewGuid(), $"NHS_{Guid.NewGuid():N}".ToUpperInvariant(), "NHS staff",
            [MedId], AppointmentTypeIds.All);
        Context.AttendeeGroups.Add(group);

        // Fixed ids in ascending name order: members read back ordered by id, so the
        // member holding the pending invite is always first.
        var first = Attendee.Create(
            Guid.Parse("00000000-0000-0000-0000-000000000001"), "Amy", "amy@example.invalid", group, Now);
        var second = Attendee.Create(
            Guid.Parse("00000000-0000-0000-0000-000000000002"), "Bo", "bo@example.invalid", group, Now);
        Context.Attendees.AddRange(first, second);

        var options = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        Context.Invites.Add(Invite.CreateInitial(
            Guid.NewGuid(), first.Id, Now.AddDays(7), [Guid.NewGuid()], options, [MedId], 0));

        if (blockActiveBooking)
        {
            var bookingInvite = Invite.CreateInitial(
                Guid.NewGuid(), second.Id, Now.AddDays(7), [Guid.NewGuid()], options, [MedId], 0);
            var booking = Booking.Create(Guid.NewGuid(), bookingInvite, options[0], Now);
            bookingInvite.MarkUsed();
            Context.Invites.Add(bookingInvite);
            Context.Bookings.Add(booking);
        }

        await Context.SaveChangesAsync();
        return group;
    }

    protected async Task<IReadOnlyList<Attendee>> MembersOfAsync(Guid groupId)
    {
        await using var read = fixture.NewContext();
        return await read.Attendees
            .Include(a => a.Requirements)
            .Where(a => a.AttendeeGroupId == groupId)
            .OrderBy(a => a.Id)
            .ToListAsync();
    }

    protected async Task<Invite> InviteForAsync(Guid attendeeId)
    {
        await using var read = fixture.NewContext();
        return await read.Invites.SingleAsync(i => i.AttendeeId == attendeeId);
    }

    protected async Task<long> GroupVersionAsync(Guid groupId)
    {
        await using var read = fixture.NewContext();
        return (await read.AttendeeGroups.SingleAsync(g => g.Id == groupId)).Version;
    }

    protected sealed class HarnessProfiles : IStaffAccessProfileRepository, IStaffAccessAuthorizer
    {
        private readonly List<StaffAccessProfile> _items = [];

        public void Add(StaffAccessProfile profile) => _items.Add(profile);

        public void Remove(StaffAccessProfile profile) => _items.Remove(profile);

        public Task<StaffAccessProfile?> GetAsync(Guid staffUserId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.SingleOrDefault(p => p.StaffUserId == staffUserId));

        public Task<IReadOnlyList<StaffAccessProfile>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccessProfile>>(_items.ToList());

        public Task<IReadOnlyList<StaffAccessProfile>> LockAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccessProfile>>(_items.ToList());

        public Task<Result<StaffAccessContext>> AuthorizeAsync(
            Guid staffUserId,
            StaffCapability capability,
            Guid? requiredAppointmentTypeId,
            CancellationToken cancellationToken) =>
            new StaffAccessAuthorizer(this).AuthorizeAsync(
                staffUserId, capability, requiredAppointmentTypeId, cancellationToken);
    }

    protected sealed class HarnessUnitOfWork(EventBookingDbContext context) : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            var saved = await context.SaveChangesAsync(cancellationToken);
            SaveCount++;
            return saved;
        }

        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ITransactionScope>(new NullScope());

        private sealed class NullScope : ITransactionScope
        {
            public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    protected sealed class HarnessAudit : IAuditLogger
    {
        public List<(string EntityType, Guid EntityId, AuditAction Action)> Entries { get; } = [];

        public void Record(
            string entityType,
            Guid entityId,
            AuditAction action,
            ActorType actorType,
            string? actorId,
            string? details = null) =>
            Entries.Add((entityType, entityId, action));
    }

    protected sealed class HarnessClock : IClock
    {
        public DateTimeOffset UtcNow => Now;

        public DateTimeOffset NowAtTransitionalLocation => Now;

        public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(Now.UtcDateTime);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
            instant.ToUniversalTime();
    }
}
