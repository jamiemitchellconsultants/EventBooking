using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Tests.Bookings;

public sealed class BookingFixture
{
    public static readonly DateTimeOffset Now = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    public InMemoryAttendeeRepository Attendees = new();
    public InMemoryInviteRepository Invites = new();
    public InMemoryEventRepository Events = new();
    public InMemoryEventCapacityRepository Capacities = null!;
    public InMemoryBookingRepository Bookings = new();
    public InMemoryBookingAppointmentRepository Appointments = null!;
    public InMemoryLocationRepository Locations = new();
    public InMemoryAppointmentTypeRepository Types = new();
    public InMemoryAttendeeGroupRepository Groups = new();
    public InMemorySystemSettingsRepository Settings = new();
    public InMemoryEmailDeliveryRepository Emails = new();
    public InMemoryStaffAccessProfileRepository Profiles = new();
    public FakeTokenService Tokens = new();
    public FakeUnitOfWork UnitOfWork = new();
    public RecordingAuditLogger Audit = new();
    public FakeClock Clock = new(Now);
    public StubInviteEligibility Eligibility = new();

    public Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    public Guid EventId;
    public Guid AttendeeId;
    public Dictionary<string, Guid> TypeIds = new();

    public static BookingFixture Create()
    {
        var fixture = new BookingFixture();
        fixture.Capacities = new InMemoryEventCapacityRepository(fixture.Events);
        fixture.Appointments = new InMemoryBookingAppointmentRepository(fixture.Bookings);
        fixture.Types.Items.Clear();
        foreach (var code in new[] { "MED", "FIT", "IND" })
        {
            var type = AppointmentType.Create(Guid.NewGuid(), code, code);
            fixture.Types.Items.Add(type);
            fixture.TypeIds[code] = type.Id;
        }

        var location = Location.Create(Guid.NewGuid(), "LONDON_HQ", "London HQ", "1 High St",
            "Europe/London", BookingTestZones.Instance);
        fixture.Locations.Items.Add(location);

        var group = AttendeeGroup.Create(Guid.NewGuid(), "NHS", "NHS staff",
            fixture.TypeIds.Values.ToList(), fixture.TypeIds.Values.ToList());
        fixture.Groups.Items.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amy", "amy@example.invalid", group, Now);
        fixture.Attendees.Items.Add(attendee);
        fixture.AttendeeId = attendee.Id;
        fixture.Profiles.Add(StaffAccessProfile.Create(fixture.Coordinator, Role.Coordinator, null));

        var proposal = EventProposal.Propose(Guid.NewGuid(), location.Id, true, "Europe/London",
            new EventWindow(new DateOnly(2026, 11, 4), new TimeOnly(9, 30), 90),
            BookingTestZones.Instance, Now,
            fixture.TypeIds.Values.Select(id => new ProposableAppointmentType(id, id.ToString(), true, true)).ToList(),
            fixture.TypeIds["MED"], fixture.Coordinator, 10);
        foreach (var code in new[] { "FIT", "IND" })
            proposal.Accept(fixture.TypeIds[code], fixture.Coordinator, 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        fixture.Events.Items.Add(eventItem);
        fixture.EventId = eventItem.Id;
        fixture.Eligibility.EligibleInOrder = [eventItem.Id];
        fixture.Settings.Settings.Update(7, 2, 1);
        return fixture;
    }

    public string BookTokenFor(Guid inviteId, int version) =>
        Tokens.Issue(TokenPurpose.Book, inviteId, version);

    public string ManageTokenFor(Guid bookingId, int version) =>
        Tokens.Issue(TokenPurpose.Manage, bookingId, version);

    private int _subGroups;

    // A second attendee needing exactly the named types, invited at the fixture event.
    // Returns the attendee id and the pending invite id.
    public (Guid AttendeeId, Guid InviteId) InviteAttendee(params string[] codes)
    {
        var ids = codes.Select(c => TypeIds[c]).ToList();
        var group = AttendeeGroup.Create(Guid.NewGuid(), $"SUB{_subGroups:D3}", "Sub group",
            ids, TypeIds.Values.ToList());
        Groups.Items.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), "Sub", $"sub{_subGroups}@example.invalid", group, Now);
        _subGroups++;
        Attendees.Items.Add(attendee);
        var issuer = new InviteIssuer(Invites, Settings, Emails, Audit, Clock, Eligibility);
        var result = issuer.IssueInitialAsync(attendee, Locations.Items.Select(l => l.Id).ToList(),
            Domain.Notifications.EmailTemplate.AttendeeInvite, Domain.Audit.ActorType.Staff,
            Coordinator.ToString(), CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(result.IsSuccess);
        return (attendee.Id, result.Value.InviteId);
    }

    public int RemainingFor(string code) =>
        Events.Items.Single().CapacityFor(TypeIds[code]).RemainingCapacity;

    public void Occupy(string code, int count)
    {
        var row = Events.Items.Single().CapacityFor(TypeIds[code]);
        for (var i = 0; i < count; i++) row.Decrement();
    }
}

public sealed class StubInviteEligibility : IEventEligibilityQuery
{
    public IReadOnlyList<Guid> EligibleInOrder { get; set; } = [];
    public Task<IReadOnlyList<Guid>> FindEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        int count, DateTimeOffset asOf, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(EligibleInOrder
            .Where(id => !excludeEventIds.Contains(id)).Take(count).ToList());
    public Task<int> CountEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        DateTimeOffset asOf, CancellationToken cancellationToken) =>
        Task.FromResult(EligibleInOrder.Count(id => !excludeEventIds.Contains(id)));
}

public sealed class BookingTestZones : IEventWindowZones
{
    public static readonly BookingTestZones Instance = new();
    public bool IsKnownZone(string timeZoneId) => timeZoneId is "Europe/London";
    public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
        LocalTimeValidity.Unique;
    public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
        new(date.ToDateTime(time), TimeSpan.Zero);
    public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
        DateOnly.FromDateTime(instant.UtcDateTime);
    public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "GMT";
}
