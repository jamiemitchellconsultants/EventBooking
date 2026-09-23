using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Invites;

public class TriggerInviteHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Attendee _attendee;

    private TriggerInviteHandler Handler => new(
        _attendees,
        _roles,
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _unitOfWork);

    public TriggerInviteHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));

        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _attendees.Add(_attendee);
    }

    /// <summary>Verifies invite issuance persists the business change and delivery result once each.</summary>
    [Fact]
    public async Task ACoordinatorCanTriggerAnInvite()
    {
        AddThreeEvents();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Invited);
        Assert.Single(_invites.Items);
        Assert.Single(_email.Sent);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task TheSystemCanTriggerWithoutAStaffIdentity()
    {
        AddThreeEvents();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(null, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Invited);
    }

    [Fact]
    public async Task AManagerCannotTriggerAnInvite()
    {
        AddThreeEvents();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Manager, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_invites.Items);
    }

    [Fact]
    public async Task WithoutEnoughEventsTheAttendeeIsFlaggedAndStillSaved()
    {
        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Invited);
        Assert.Equal(AttendeeStatus.AwaitingAvailability, _attendee.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReInvitingAAttendeeWhoNeverRespondedResetsTheRetryCount()
    {
        AddThreeEvents();
        _attendee.MarkInvited();
        _attendee.MarkNoResponse();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.Value.Invited);
        Assert.Equal(0, _invites.Items.Single().RetryCount);
        Assert.Equal(AttendeeStatus.Invited, _attendee.Status);
    }

    [Fact]
    public async Task ABookedAttendeeCannotBeReInvited()
    {
        AddThreeEvents();
        _attendee.MarkInvited();
        _attendee.MarkBooked();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _attendee.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("This attendee is already booked.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private void AddThreeEvents()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
        }
    }
}
