using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Invites;

public class TriggerInviteHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ", "recruitment@corp.com");

    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryEmployeeGroupRepository _groups = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Candidate _candidate;

    private TriggerInviteHandler Handler => new(
        _candidates,
        _roles,
        new InviteIssuer(
            _invites, _groups, new EligibleSlotFinder(_slots, _clock), _settings,
            new FakeTokenService(), EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _unitOfWork);

    public TriggerInviteHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));

        var pilots = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _candidate = Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _candidates.Add(_candidate);
    }

    /// <summary>Verifies invite issuance persists the business change and delivery result once each.</summary>
    [Fact]
    public async Task ACoordinatorCanTriggerAnInvite()
    {
        AddThreeSlots();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Invited);
        Assert.Single(_invites.Items);
        Assert.Single(_email.Sent);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task TheSystemCanTriggerWithoutAStaffIdentity()
    {
        AddThreeSlots();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(null, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Invited);
    }

    [Fact]
    public async Task AManagerCannotTriggerAnInvite()
    {
        AddThreeSlots();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Manager, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_invites.Items);
    }

    [Fact]
    public async Task WithoutEnoughSlotsTheCandidateIsFlaggedAndStillSaved()
    {
        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Invited);
        Assert.Equal(CandidateStatus.AwaitingAvailability, _candidate.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReInvitingACandidateWhoNeverRespondedResetsTheRetryCount()
    {
        AddThreeSlots();
        _candidate.MarkInvited();
        _candidate.MarkNoResponse();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.Value.Invited);
        Assert.Equal(0, _invites.Items.Single().RetryCount);
        Assert.Equal(CandidateStatus.Invited, _candidate.Status);
    }

    [Fact]
    public async Task ABookedCandidateCannotBeReInvited()
    {
        AddThreeSlots();
        _candidate.MarkInvited();
        _candidate.MarkBooked();

        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, _candidate.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("This candidate is already booked.", result.Error.Message);
    }

    [Fact]
    public async Task AnUnknownCandidateIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new TriggerInviteCommand(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private void AddThreeSlots()
    {
        foreach (var day in new[] { 10, 12, 14 })
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            _slots.Add(ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
        }
    }
}
