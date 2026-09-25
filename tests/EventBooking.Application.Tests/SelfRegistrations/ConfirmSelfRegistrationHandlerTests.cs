using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.SelfRegistrations;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.SelfRegistrations;
using EventBooking.Domain.Time;
using EventBooking.TestSupport;

namespace EventBooking.Application.Tests.SelfRegistrations;

public sealed class ConfirmSelfRegistrationHandlerTests
{
    private static readonly DateTimeOffset BeforeExpiry = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(BeforeExpiry);
    private readonly InMemoryEventGroupRepository _eventGroups = new();
    private readonly InMemoryAttendeeGroupRepository _attendeeGroups = new();
    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryEventRepository _events;
    private readonly InMemoryEventCapacityRepository _capacities;
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryLocationRepository _locations = new();
    private readonly InMemoryAppointmentTypeRepository _types = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryEmailDeliveryRepository _emails = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly AsyncLocalCorrelationContext _correlation = new();

    private Domain.EventGroups.EventGroup _group = null!;
    private Event _eventItem = null!;

    public ConfirmSelfRegistrationHandlerTests()
    {
        _events = new InMemoryEventRepository();
        _capacities = new InMemoryEventCapacityRepository(_events);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _correlation.Begin("confirm-correlation");
        SeedOpenGroup();
    }

    private ConfirmSelfRegistrationHandler Handler => new(
        _eventGroups, _attendees, _invites, _attendeeGroups, _events, _capacities,
        _bookings, _appointments, _locations, _settings, _emails, _tokens,
        _unitOfWork, _audit, _clock, ProposalFixture.Zones, _correlation);

    [Fact]
    public async Task ConfirmBooksEveryRequiredTypeAtomicallyAndStagesTheEmail()
    {
        var token = Submit("Robin Public", "robin@example.com");

        var result = await Handler.HandleAsync(
            new ConfirmSelfRegistrationCommand(token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var booking = Assert.Single(_bookings.Items);
        Assert.Equal(result.Value.BookingId, booking.Id);
        Assert.Equal(BookingStatus.Active, booking.Status);
        Assert.Single(_appointments.Items);
        var registration = _eventGroups.Registrations.Single();
        Assert.Equal(SelfRegistrationStatus.Confirmed, registration.Status);
        var delivery = Assert.Single(_emails.Items);
        Assert.Equal(EmailTemplate.SelfRegistrationConfirmation, delivery.TemplateName);
        Assert.Equal(booking.Id, delivery.BookingId);
        Assert.Equal("confirm-correlation", delivery.CorrelationId);
        var attendee = Assert.Single(_attendees.Items);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.Equal(AttendeeGroupIds.CabinCrew, attendee.AttendeeGroupId);
        Assert.Contains(_audit.Entries,
            e => e.Action == AuditAction.SelfRegistrationConfirmed
                && e.ActorType == ActorType.Anonymous);
    }

    [Fact]
    public async Task ReconfirmingTheSameTokenIsAnAlreadyConfirmedConflict()
    {
        var token = Submit("Robin Public", "robin@example.com");
        Assert.True((await Handler.HandleAsync(
            new ConfirmSelfRegistrationCommand(token), CancellationToken.None)).IsSuccess);

        var again = await Handler.HandleAsync(
            new ConfirmSelfRegistrationCommand(token), CancellationToken.None);

        Assert.True(again.IsFailure);
        Assert.Equal("already-confirmed", again.Error.Code);
        Assert.Single(_bookings.Items);
    }

    [Fact]
    public async Task StaleVersionConfirmsNothing()
    {
        Submit("Robin Public", "robin@example.com");
        var registration = _eventGroups.Registrations.Single();
        var stale = _tokens.Issue(
            TokenPurpose.Registration, registration.RequestId, registration.TokenVersion + 1);

        var result = await Handler.HandleAsync(
            new ConfirmSelfRegistrationCommand(stale), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_bookings.Items);
        Assert.Empty(_emails.Items);
        Assert.Single(_eventGroups.Registrations);
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
