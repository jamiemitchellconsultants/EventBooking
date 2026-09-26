using EventBooking.Application.Abstractions;
using EventBooking.Application.SelfRegistrations;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Domain.SelfRegistrations;
using EventBooking.Domain.Time;
using EventBooking.TestSupport;

namespace EventBooking.Application.Tests.SelfRegistrations;

public sealed class ViewSelfRegistrationHandlerTests
{
    private static readonly DateTimeOffset BeforeExpiry = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(BeforeExpiry);
    private readonly InMemoryEventGroupRepository _eventGroups = new();
    private readonly InMemoryAttendeeGroupRepository _attendeeGroups = new();
    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryLocationRepository _locations = new();
    private readonly FakeTokenService _tokens = new();

    private Domain.EventGroups.EventGroup _group = null!;
    private Event _eventItem = null!;

    public ViewSelfRegistrationHandlerTests()
    {
        SeedOpenGroup();
    }

    private ViewSelfRegistrationHandler Handler => new(
        _eventGroups, _attendees, _bookings, _attendeeGroups, _events,
        _locations, _tokens, _clock);

    [Fact]
    public async Task ViewReturnsThePublicSummary()
    {
        var token = Submit("Robin Public", "robin@example.com");

        var result = await Handler.HandleAsync(
            new ViewSelfRegistrationQuery(token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_group.Id, result.Value.EventGroupId);
        Assert.Equal(_eventItem.Id, result.Value.EventId);
        Assert.Equal("Open days", result.Value.EventGroupTitle);
        Assert.Equal("London HQ", result.Value.LocationName);
        Assert.Equal(new DateOnly(2026, 12, 10), result.Value.Date);
        Assert.Equal(new TimeOnly(9, 0), result.Value.StartTime);
        Assert.Equal("Cabin Crew", result.Value.AttendeeGroupName);
    }

    [Fact]
    public async Task UnknownTokenIsInvalid()
    {
        var result = await Handler.HandleAsync(
            new ViewSelfRegistrationQuery("not-a-token"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("token-invalid", result.Error.Code);
    }

    [Fact]
    public async Task ExpiredRequestIsExpired()
    {
        var token = Submit("Robin Public", "robin@example.com");
        _eventGroups.Registrations.Single().Expire(BeforeExpiry.AddHours(49));

        var result = await Handler.HandleAsync(
            new ViewSelfRegistrationQuery(token), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("token-expired", result.Error.Code);
    }

    [Fact]
    public async Task ConfirmedRequestWithoutABookingIsInvalid()
    {
        var token = Submit("Robin Public", "robin@example.com");
        var registration = _eventGroups.Registrations.Single();
        registration.Confirm(registration.TokenVersion, BeforeExpiry);

        var result = await Handler.HandleAsync(
            new ViewSelfRegistrationQuery(token), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("token-invalid", result.Error.Code);
    }

    private string Submit(string name, string email)
    {
        var registration = PendingRegistration.Create(
            Guid.NewGuid(), _group.Id, _eventItem.Id, AttendeeGroupIds.CabinCrew,
            name, email, BeforeExpiry, 48);
        _eventGroups.Registrations.Add(registration);
        return _tokens.Issue(
            TokenPurpose.Registration, registration.RequestId, registration.TokenVersion);
    }

    private void SeedOpenGroup()
    {
        var cabinCrew = AttendeeGroup.Create(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew",
            [AppointmentTypeIds.MedicalCheckUp],
            [AppointmentTypeIds.MedicalCheckUp]);
        _attendeeGroups.Items.Add(cabinCrew);
        _locations.Items.Add(Location.Create(
            ProposalFixture.LocationId, "LONDON_HQ", "London HQ", "1 High St",
            ProposalFixture.TimeZoneId, ProposalFixture.Zones));

        var proposal = ProposalFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 12, 10), new TimeOnly(9, 0), 240),
            Guid.NewGuid(),
            AppointmentTypeIds.MedicalCheckUp,
            [AppointmentTypeIds.MedicalCheckUp]);
        foreach (var type in proposal.ListedAppointmentTypeIds) proposal.Accept(type, Guid.NewGuid(), 5);
        _eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Items.Add(_eventItem);

        var requirements = new Dictionary<Guid, IReadOnlyCollection<Guid>>
        {
            [AttendeeGroupIds.CabinCrew] = [AppointmentTypeIds.MedicalCheckUp],
        };
        _group = Domain.EventGroups.EventGroup.Create(
            Guid.NewGuid(), "Open days", null, requirements);
        _group.AddEvent(_eventItem.Id, [AppointmentTypeIds.MedicalCheckUp],
            requirements, isFuture: true);
        _group.SetOpen(true);
        _group.SetEventOpen(_eventItem.Id, true);
        _eventGroups.Items.Add(_group);
    }
}
