using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.SelfRegistrations;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Domain.SelfRegistrations;
using EventBooking.Domain.Time;
using EventBooking.Application.Tests.Fakes;
using EventBooking.TestSupport;

namespace EventBooking.Application.Tests.SelfRegistrations;

public sealed class SelfRegistrationHandlerTests
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryEventGroupRepository _eventGroups = new();
    private readonly InMemoryAttendeeGroupRepository _attendeeGroups = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryLocationRepository _locations = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly AsyncLocalCorrelationContext _correlation = new();

    public SelfRegistrationHandlerTests()
    {
        _correlation.Begin("test-correlation");
    }

    private SubmitSelfRegistrationHandler Handler => new(
        _eventGroups, _attendeeGroups, _events, _locations, _settings, _tokens,
        _unitOfWork, _audit, _clock, ProposalFixture.Zones, _correlation);

    private static readonly Guid CabinCrewId = AttendeeGroupIds.CabinCrew;

    [Fact]
    public async Task SubmissionCreatesOnePendingRegistrationAndIssuesItsToken()
    {
        var group = SeedOpenGroup();

        var result = await Handler.HandleAsync(new SubmitSelfRegistrationCommand(
            group.Id, group.Events.Single().EventId, CabinCrewId,
            "Robin Public", "Robin@Example.com "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Robin@Example.com", result.Value.Email);
        var registration = _eventGroups.Registrations.Single();
        Assert.Equal("robin@example.com", registration.Email);
        Assert.Equal(SelfRegistrationStatus.Pending, registration.Status);
        Assert.Equal(
            _tokens.Issue(TokenPurpose.Registration, registration.RequestId, registration.TokenVersion),
            result.Value.ConfirmationToken);
    }

    [Fact]
    public async Task SubmissionAgainstClosedGatesOrStaleEventsIsRejected()
    {
        var group = SeedOpenGroup();
        group.SetOpen(false);

        var closed = await Handler.HandleAsync(new SubmitSelfRegistrationCommand(
            group.Id, group.Events.Single().EventId, CabinCrewId,
            "Robin Public", "robin@example.com"), CancellationToken.None);
        Assert.True(closed.IsFailure);
        Assert.Equal("validation", closed.Error.Code);

        var stale = await Handler.HandleAsync(new SubmitSelfRegistrationCommand(
            group.Id, Guid.NewGuid(), CabinCrewId,
            "Robin Public", "robin@example.com"), CancellationToken.None);
        Assert.True(stale.IsFailure);
        Assert.Equal("not_found", stale.Error.Code);
    }

    [Fact]
    public async Task AnIdenticalInFlightSubmissionReturnsTheExistingToken()
    {
        var group = SeedOpenGroup();
        var command = new SubmitSelfRegistrationCommand(
            group.Id, group.Events.Single().EventId, CabinCrewId,
            "Robin Public", "robin@example.com");

        var first = await Handler.HandleAsync(command, CancellationToken.None);
        var second = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.ConfirmationToken, second.Value.ConfirmationToken);
        Assert.Single(_eventGroups.Registrations);
    }

    [Fact]
    public async Task ADifferentSubmissionForTheSameEmailIsUnprocessable()
    {
        var group = SeedOpenGroup();
        var first = new SubmitSelfRegistrationCommand(
            group.Id, group.Events.Single().EventId, CabinCrewId,
            "Robin Public", "robin@example.com");
        var second = first with { Name = "Robin Other" };

        Assert.True((await Handler.HandleAsync(first, CancellationToken.None)).IsSuccess);
        var result = await Handler.HandleAsync(second, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    private Domain.EventGroups.EventGroup SeedOpenGroup()
    {
        var cabinCrew = AttendeeGroup.Create(
            CabinCrewId, "CABIN_CREW", "Cabin Crew",
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
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Items.Add(eventItem);

        var requirements = new Dictionary<Guid, IReadOnlyCollection<Guid>>
        {
            [CabinCrewId] = [AppointmentTypeIds.MedicalCheckUp],
        };
        var group = Domain.EventGroups.EventGroup.Create(
            Guid.NewGuid(), "Open days", null, requirements);
        group.AddEvent(eventItem.Id, [AppointmentTypeIds.MedicalCheckUp],
            requirements, isFuture: true);
        group.SetOpen(true);
        group.SetEventOpen(eventItem.Id, true);
        _eventGroups.Items.Add(group);
        return group;
    }
}
