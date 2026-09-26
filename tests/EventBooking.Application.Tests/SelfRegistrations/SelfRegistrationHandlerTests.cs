using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.SelfRegistrations;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Notifications;
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
    private readonly InMemoryEmailDeliveryRepository _emails = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly AsyncLocalCorrelationContext _correlation = new();

    public SelfRegistrationHandlerTests()
    {
        _correlation.Begin("test-correlation");
    }

    private SubmitSelfRegistrationHandler Handler => new(
        _eventGroups, _attendeeGroups, _events, _locations, _settings, _emails,
        _unitOfWork, _audit, _clock, ProposalFixture.Zones, _correlation);

    private static readonly Guid CabinCrewId = AttendeeGroupIds.CabinCrew;

    [Fact]
    public async Task SubmissionCreatesOnePendingRegistrationAndEmailsItsLink()
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
        var delivery = Assert.Single(_emails.Items);
        Assert.Equal(registration.RequestId, delivery.SelfRegistrationId);
        Assert.Null(delivery.AttendeeId);
        Assert.Equal(EmailTemplate.SelfRegistrationConfirmation, delivery.TemplateName);
    }

    [Fact]
    public void TheReceiptCarriesNoToken()
    {
        Assert.DoesNotContain(typeof(SubmitSelfRegistrationResult).GetProperties(),
            x => x.Name.Contains("Token", StringComparison.Ordinal));
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
        Assert.Equal("not_found", closed.Error.Code);

        var stale = await Handler.HandleAsync(new SubmitSelfRegistrationCommand(
            group.Id, Guid.NewGuid(), CabinCrewId,
            "Robin Public", "robin@example.com"), CancellationToken.None);
        Assert.True(stale.IsFailure);
        Assert.Equal("not_found", stale.Error.Code);
    }

    [Fact]
    public async Task AnInFlightSubmissionIsNeutralAndReEmailsTheExistingRequest()
    {
        var group = SeedOpenGroup();
        var command = new SubmitSelfRegistrationCommand(
            group.Id, group.Events.Single().EventId, CabinCrewId,
            "Robin Public", "robin@example.com");

        var first = await Handler.HandleAsync(command, CancellationToken.None);
        var second = await Handler.HandleAsync(command with { Name = "Robin Other" },
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.RequestId, second.Value.RequestId);
        Assert.Single(_eventGroups.Registrations);
        Assert.Equal(2, _emails.Items.Count);
        Assert.All(_emails.Items,
            x => Assert.Equal(first.Value.RequestId, x.SelfRegistrationId));
    }

    [Fact]
    public async Task ADifferentGroupSelectionRetiresTheOldRequestAndEmailsTheNewOne()
    {
        var group = SeedOpenGroup();
        var second = Domain.EventGroups.EventGroup.Create(Guid.NewGuid(), "Other days", null,
            new Dictionary<Guid, IReadOnlyCollection<Guid>>
            {
                [CabinCrewId] = [AppointmentTypeIds.MedicalCheckUp],
            });
        var eventId = group.Events.Single().EventId;
        second.AddEvent(eventId, [AppointmentTypeIds.MedicalCheckUp],
            new Dictionary<Guid, IReadOnlyCollection<Guid>>
            {
                [CabinCrewId] = [AppointmentTypeIds.MedicalCheckUp],
            }, isFuture: true);
        second.SetOpen(true);
        second.SetEventOpen(eventId, true);
        _eventGroups.Items.Add(second);

        var first = await Handler.HandleAsync(new SubmitSelfRegistrationCommand(
            group.Id, eventId, CabinCrewId, "Robin", "robin@example.com"), CancellationToken.None);
        var other = await Handler.HandleAsync(new SubmitSelfRegistrationCommand(
            second.Id, eventId, CabinCrewId, "Robin", "robin@example.com"), CancellationToken.None);

        Assert.NotEqual(first.Value.RequestId, other.Value.RequestId);
        Assert.Equal(SelfRegistrationStatus.Expired,
            _eventGroups.Registrations.Single(x => x.RequestId == first.Value.RequestId).Status);
        Assert.Equal(second.Id, _eventGroups.Registrations
            .Single(x => x.Status == SelfRegistrationStatus.Pending).EventGroupId);
        Assert.Equal(other.Value.RequestId, _emails.Items.Last().SelfRegistrationId);
    }

    [Fact]
    public async Task ALapsedRequestIsReplacedRatherThanResent()
    {
        var group = SeedOpenGroup();
        var command = new SubmitSelfRegistrationCommand(
            group.Id, group.Events.Single().EventId, CabinCrewId, "Robin", "robin@example.com");
        var first = await Handler.HandleAsync(command, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(25));

        var again = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.NotEqual(first.Value.RequestId, again.Value.RequestId);
        Assert.Equal(again.Value.RequestId, _emails.Items.Last().SelfRegistrationId);
    }

    [Fact]
    public async Task ARepeatWithinTheCooldownQueuesNoExtraEmail()
    {
        var group = SeedOpenGroup();
        var command = new SubmitSelfRegistrationCommand(
            group.Id, group.Events.Single().EventId, CabinCrewId, "Robin", "robin@example.com");
        await Handler.HandleAsync(command, CancellationToken.None);
        _eventGroups.LinkDueAt = _clock.UtcNow;

        var again = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(again.IsSuccess);
        Assert.Single(_emails.Items);
    }

    [Fact]
    public async Task ANewRequestInsideTheCooldownHasItsLinkDeferredNotDropped()
    {
        var group = SeedOpenGroup();
        _eventGroups.LinkDueAt = _clock.UtcNow;

        var result = await Handler.HandleAsync(new SubmitSelfRegistrationCommand(
            group.Id, group.Events.Single().EventId, CabinCrewId, "Robin", "robin@example.com"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var link = Assert.Single(_emails.Items);
        Assert.Equal(_clock.UtcNow.AddSeconds(60), link.NotBefore);
    }

    [Fact]
    public async Task OverlongNameOrEmailIsUnprocessable()
    {
        var group = SeedOpenGroup();
        var eventId = group.Events.Single().EventId;

        var name = await Handler.HandleAsync(new SubmitSelfRegistrationCommand(
            group.Id, eventId, CabinCrewId, new string('n', 201), "robin@example.com"),
            CancellationToken.None);
        var email = await Handler.HandleAsync(new SubmitSelfRegistrationCommand(
            group.Id, eventId, CabinCrewId, "Robin", new string('a', 320) + "@example.com"),
            CancellationToken.None);

        Assert.Equal("validation", name.Error.Code);
        Assert.Equal("validation", email.Error.Code);
        Assert.Empty(_eventGroups.Registrations);
    }

    [Fact]
    public async Task ASubmissionForAFullEventIsRefusedAsCapacityExhausted()
    {
        var group = SeedOpenGroup();
        var eventItem = _events.Items.Single();
        for (var i = 0; i < 5; i++)
            eventItem.ChargeRequiredTypes([AppointmentTypeIds.MedicalCheckUp]);

        var result = await Handler.HandleAsync(new SubmitSelfRegistrationCommand(
            group.Id, eventItem.Id, CabinCrewId, "Robin", "robin@example.com"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.CapacityExhaustedCode, result.Error.Code);
        Assert.Empty(_eventGroups.Registrations);
        Assert.Empty(_emails.Items);
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
