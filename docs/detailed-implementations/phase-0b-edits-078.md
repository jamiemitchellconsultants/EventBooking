# 00b — Vocabulary edits 78 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs — 1/1

<!-- vocabulary-file: {"id":254,"oldPath":"tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs","beforeSha":"b9489007ac4aa4ebd1c2903d0978c8d13ec832b5d7b375f560499529271589e7","afterSha":"4889700b462f122cd49f0bf0d1ad57ad349394c6985a9de1af272d8d32691b49","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies invite options are topped up to three and shortfalls flag follow-up (Issue #242).</summary>
public class InviteOptionReplacementTests
{
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Candidate _candidate;
    private readonly Invite _invite;
    private readonly string _token;

    private ViewInviteHandler Handler => new(
        _invites, _candidates, _slots, new EligibleSlotFinder(_slots, _clock),
        _audit, _unitOfWork, _tokens, _clock);

    public InviteOptionReplacementTests()
    {
        _candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _candidates.Add(_candidate);

        var slotIds = new[] { AddSlot(10, 9), AddSlot(11, 13), AddSlot(13, 9) };

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        _token = issued.Token;
        _invite = Invite.CreateInitial(
            inviteId, _candidate.Id, issued.TokenHash, _clock.UtcNow.AddDays(4), slotIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(_invite);
        _candidate.MarkInvited();
    }

    /// <summary>A cancelled option is replaced so the candidate still sees three live options.</summary>
    [Fact]
    public async Task View_ReplacesCancelledOption_ToRestoreThreeOptions()
    {
        _slots.Items[1].Cancel();
        var spareId = AddSlot(14, 9);

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Options.Count);
        Assert.Contains(spareId, result.Value.Options.Select(o => o.ConfirmedSlotId));
        Assert.DoesNotContain(
            _slots.Items[1].Id, result.Value.Options.Select(o => o.ConfirmedSlotId));
        Assert.Contains(spareId, _invite.OfferedSlotIds);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    /// <summary>With no replacement available the candidate is flagged for coordinator follow-up.</summary>
    [Fact]
    public async Task View_WithNoReplacementAvailable_FlagsCandidateForFollowUp()
    {
        _slots.Items[1].Cancel();
        _slots.Items[2].Cancel();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Options);
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, _candidate.Status);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    private Guid AddSlot(int day, int hour)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);
        return slot.Id;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs — 1/1

<!-- vocabulary-file: {"id":254,"oldPath":"tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs","beforeSha":"b9489007ac4aa4ebd1c2903d0978c8d13ec832b5d7b375f560499529271589e7","afterSha":"4889700b462f122cd49f0bf0d1ad57ad349394c6985a9de1af272d8d32691b49","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies invite options are topped up to three and shortfalls flag follow-up (Issue #242).</summary>
public class InviteOptionReplacementTests
{
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Attendee _attendee;
    private readonly Invite _invite;
    private readonly string _token;

    private ViewInviteHandler Handler => new(
        _invites, _attendees, _events, new EligibleEventFinder(_events, _clock),
        _audit, _unitOfWork, _tokens, _clock);

    public InviteOptionReplacementTests()
    {
        _attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _attendees.Add(_attendee);

        var eventIds = new[] { AddEvent(10, 9), AddEvent(11, 13), AddEvent(13, 9) };

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        _token = issued.Token;
        _invite = Invite.CreateInitial(
            inviteId, _attendee.Id, issued.TokenHash, _clock.UtcNow.AddDays(4), eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(_invite);
        _attendee.MarkInvited();
    }

    /// <summary>A cancelled option is replaced so the attendee still sees three live options.</summary>
    [Fact]
    public async Task View_ReplacesCancelledOption_ToRestoreThreeOptions()
    {
        _events.Items[1].Cancel();
        var spareId = AddEvent(14, 9);

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Options.Count);
        Assert.Contains(spareId, result.Value.Options.Select(o => o.EventId));
        Assert.DoesNotContain(
            _events.Items[1].Id, result.Value.Options.Select(o => o.EventId));
        Assert.Contains(spareId, _invite.OfferedEventIds);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    /// <summary>With no replacement available the attendee is flagged for coordinator follow-up.</summary>
    [Fact]
    public async Task View_WithNoReplacementAvailable_FlagsAttendeeForFollowUp()
    {
        _events.Items[1].Cancel();
        _events.Items[2].Cancel();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Options);
        Assert.Equal(AttendeeStatus.NoResponseNeedsFollowUp, _attendee.Status);
        Assert.True(_audit.Contains(AuditAction.InviteOptionReplaced));
    }

    private Guid AddEvent(int day, int hour)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem.Id;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs — 1/1

<!-- vocabulary-file: {"id":255,"oldPath":"tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs","beforeSha":"6b6db78603ddcb8de22c702f7758c7fabebf10aacdd666597208b7da13611911","afterSha":"35b4fb4c75b8949fbfdb8f246095a16701873b2bee0f0ed6a902cb7698008a2a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies portal and confirmation treat Invite Requirements as immutable authority.</summary>
public sealed class InviteSnapshotAuthorityTests
{
    /// <summary>The portal displays snapshot names rather than a later Candidate collection.</summary>
    [Fact]
    public async Task ViewInviteUsesPersistedSnapshot()
    {
        var group = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var candidates = new InMemoryCandidateRepository();
        candidates.Add(candidate);
        var slots = new InMemoryConfirmedSlotRepository();
        var options = Enumerable.Range(0, 3).Select(index =>
            ConfirmedSlot.CreateImported(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 10, 10 + index), new TimeOnly(9, 0)),
                new Dictionary<Guid, int>
                {
                    [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                    [AppointmentTypeIds.MedicalCheckUp] = 5,
                    [AppointmentTypeIds.UniformFitting] = 5,
                })).ToList();
        slots.Items.AddRange(options);
        var tokens = new FakeTokenService();
        var issued = tokens.Issue(Guid.NewGuid());
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, issued.TokenHash, DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
            options.Select(slot => slot.Id), [AppointmentTypeIds.MedicalCheckUp], 0);
        var invites = new InMemoryInviteRepository();
        invites.Add(invite);
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));

        var result = await new ViewInviteHandler(
                invites, candidates, slots, new EligibleSlotFinder(slots, clock),
                new RecordingAuditLogger(), new FakeUnitOfWork(), tokens, clock)
            .HandleAsync(new ViewInviteQuery(issued.Token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Medical Check-up"], result.Value.AppointmentTypeNames);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs — 1/1

<!-- vocabulary-file: {"id":255,"oldPath":"tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs","beforeSha":"6b6db78603ddcb8de22c702f7758c7fabebf10aacdd666597208b7da13611911","afterSha":"35b4fb4c75b8949fbfdb8f246095a16701873b2bee0f0ed6a902cb7698008a2a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies portal and confirmation treat Invite Requirements as immutable authority.</summary>
public sealed class InviteSnapshotAuthorityTests
{
    /// <summary>The portal displays snapshot names rather than a later Attendee collection.</summary>
    [Fact]
    public async Task ViewInviteUsesPersistedSnapshot()
    {
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var attendees = new InMemoryAttendeeRepository();
        attendees.Add(attendee);
        var events = new InMemoryEventRepository();
        var options = Enumerable.Range(0, 3).Select(index =>
            Event.CreateImported(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 10, 10 + index), new TimeOnly(9, 0)),
                new Dictionary<Guid, int>
                {
                    [AppointmentTypeIds.DrugAndAlcoholTesting] = 5,
                    [AppointmentTypeIds.MedicalCheckUp] = 5,
                    [AppointmentTypeIds.UniformFitting] = 5,
                })).ToList();
        events.Items.AddRange(options);
        var tokens = new FakeTokenService();
        var issued = tokens.Issue(Guid.NewGuid());
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, issued.TokenHash, DateTimeOffset.Parse("2026-10-01T00:00:00Z"),
            options.Select(eventItem => eventItem.Id), [AppointmentTypeIds.MedicalCheckUp], 0);
        var invites = new InMemoryInviteRepository();
        invites.Add(invite);
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));

        var result = await new ViewInviteHandler(
                invites, attendees, events, new EligibleEventFinder(events, clock),
                new RecordingAuditLogger(), new FakeUnitOfWork(), tokens, clock)
            .HandleAsync(new ViewInviteQuery(issued.Token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Medical Check-up"], result.Value.AppointmentTypeNames);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs — 1/1

<!-- vocabulary-file: {"id":256,"oldPath":"tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs","beforeSha":"cc5e3fba4c1ed641b1ba365488ab51c9bbd1ca8b02493fb1fed0d6dac14a8e23","afterSha":"ea7e70ba0c410211bf3143d3d7b503052534a48e8900afe5a11039d84e931229","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Specifies locked recovery snapshot validation before capacity mutation.</summary>
public sealed class RecoveryBookingLifecycleTests
{
    /// <summary>A snapshot that ceased to be recoverable fails before confirmation.</summary>
    [Fact]
    public void CompletedTypeMakesPendingRecoverySnapshotStale()
    {
        var candidateId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var recoverySlot = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, originalId, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [recoverySlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var attempts = new[]
        {
            new EventBooking.Application.Invites.RecoveryAttempt(
                Guid.NewGuid(), AppointmentTypeIds.MedicalCheckUp,
                BookingAppointmentStatus.NoShow, DateTimeOffset.UtcNow.AddDays(-2)),
            new EventBooking.Application.Invites.RecoveryAttempt(
                Guid.NewGuid(), AppointmentTypeIds.MedicalCheckUp,
                BookingAppointmentStatus.Completed, DateTimeOffset.UtcNow.AddDays(-1)),
        };

        var result = new RecoveryConfirmationValidator().Validate(
            recoveryInvite,
            [AppointmentTypeIds.MedicalCheckUp],
            attempts,
            []);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery_state_changed", result.Error.Code);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs — 1/1

<!-- vocabulary-file: {"id":256,"oldPath":"tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs","beforeSha":"cc5e3fba4c1ed641b1ba365488ab51c9bbd1ca8b02493fb1fed0d6dac14a8e23","afterSha":"ea7e70ba0c410211bf3143d3d7b503052534a48e8900afe5a11039d84e931229","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Specifies locked recovery snapshot validation before capacity mutation.</summary>
public sealed class RecoveryBookingLifecycleTests
{
    /// <summary>A snapshot that ceased to be recoverable fails before confirmation.</summary>
    [Fact]
    public void CompletedTypeMakesPendingRecoverySnapshotStale()
    {
        var attendeeId = Guid.NewGuid();
        var originalId = Guid.NewGuid();
        var recoveryEvent = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, originalId, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [recoveryEvent, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var attempts = new[]
        {
            new EventBooking.Application.Invites.RecoveryAttempt(
                Guid.NewGuid(), AppointmentTypeIds.MedicalCheckUp,
                BookingAppointmentStatus.NoShow, DateTimeOffset.UtcNow.AddDays(-2)),
            new EventBooking.Application.Invites.RecoveryAttempt(
                Guid.NewGuid(), AppointmentTypeIds.MedicalCheckUp,
                BookingAppointmentStatus.Completed, DateTimeOffset.UtcNow.AddDays(-1)),
        };

        var result = new RecoveryConfirmationValidator().Validate(
            recoveryInvite,
            [AppointmentTypeIds.MedicalCheckUp],
            attempts,
            []);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery_state_changed", result.Error.Code);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":257,"oldPath":"tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs","beforeSha":"05fdb5173405d028f2896017091b990eeb3f1b5c27658048ff35507d47e5f665","afterSha":"45574863712b9f45549cb9df4f03bc65c076d801b73a145568bd9a4e6264f082","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Bookings;

public class ViewInviteHandlerTests
{
    private const string InvalidLink = "This booking link is no longer valid.";

    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryCandidateRepository _candidates = new();
    private readonly InMemoryConfirmedSlotRepository _slots = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Candidate _candidate;
    private readonly Invite _invite;
    private readonly string _token;

    private ViewInviteHandler Handler => new(
        _invites, _candidates, _slots, new EligibleSlotFinder(_slots, _clock),
        _audit, _unitOfWork, _tokens, _clock);

    public ViewInviteHandlerTests()
    {
        _candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _candidates.Add(_candidate);

        var slotIds = new[] { AddSlot(10, 9), AddSlot(11, 13), AddSlot(13, 9) };

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        _token = issued.Token;
        _invite = Invite.CreateInitial(
            inviteId, _candidate.Id, issued.TokenHash, _clock.UtcNow.AddDays(4), slotIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(_invite);
        _candidate.MarkInvited();
    }

    [Fact]
    public async Task AValidTokenReturnsTheCandidateTheirTypesAndThreeOptions()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_invite.Id, result.Value.InviteId);
        Assert.Equal("Amara Novak", result.Value.CandidateName);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Uniform Fitting" },
            result.Value.AppointmentTypeNames);
        Assert.Equal(3, result.Value.Options.Count);
    }

    [Fact]
    public async Task OptionsCarryTheirWindowAndADisplayString()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        var first = result.Value.Options[0];
        Assert.Equal(new DateOnly(2026, 9, 10), first.Date);
        Assert.Equal(new TimeOnly(9, 0), first.StartTime);
        Assert.Equal(new TimeOnly(13, 0), first.EndTime);
        Assert.Equal("Thursday 10 Sep 2026, 09:00-13:00", first.Display);
    }

    [Fact]
    public async Task OptionsAreOrderedEarliestFirst()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 13) },
            result.Value.Options.Select(o => o.Date));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task AMalformedTokenGivesTheGenericMessage(string? token)
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(token), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AWellFormedTokenForNoInviteGivesTheSameMessage()
    {
        var result = await Handler.HandleAsync(
            new ViewInviteQuery(_tokens.Issue(Guid.NewGuid()).Token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AnExpiredInviteGivesTheSameMessage()
    {
        _clock.Advance(TimeSpan.FromDays(5));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AUsedInviteGivesTheSameMessage()
    {
        _invite.MarkUsed();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AnOptionWhoseSlotHasBeenCancelledIsNotShown()
    {
        _slots.Items[1].Cancel();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(2, result.Value.Options.Count);
        Assert.DoesNotContain(
            new DateOnly(2026, 9, 11), result.Value.Options.Select(o => o.Date));
    }

    /// <summary>Ensures options on the head-office date or earlier are not projected.</summary>
    [Fact]
    public async Task OptionsOnTodayAndEarlierAreNotShown()
    {
        _invite.RemoveOption(_slots.Items[0].Id);
        _invite.RemoveOption(_slots.Items[1].Id);
        _slots.Items[0].Cancel();
        _slots.Items[1].Cancel();
        _invite.AddOption(AddSlot(3, 9));
        _invite.AddOption(AddSlot(2, 13));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { new DateOnly(2026, 9, 13) }, result.Value.Options.Select(o => o.Date));
    }

    private Guid AddSlot(int day, int hour)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
        _slots.Add(slot);
        return slot.Id;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":257,"oldPath":"tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs","beforeSha":"05fdb5173405d028f2896017091b990eeb3f1b5c27658048ff35507d47e5f665","afterSha":"45574863712b9f45549cb9df4f03bc65c076d801b73a145568bd9a4e6264f082","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

public class ViewInviteHandlerTests
{
    private const string InvalidLink = "This booking link is no longer valid.";

    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Attendee _attendee;
    private readonly Invite _invite;
    private readonly string _token;

    private ViewInviteHandler Handler => new(
        _invites, _attendees, _events, new EligibleEventFinder(_events, _clock),
        _audit, _unitOfWork, _tokens, _clock);

    public ViewInviteHandlerTests()
    {
        _attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _attendees.Add(_attendee);

        var eventIds = new[] { AddEvent(10, 9), AddEvent(11, 13), AddEvent(13, 9) };

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        _token = issued.Token;
        _invite = Invite.CreateInitial(
            inviteId, _attendee.Id, issued.TokenHash, _clock.UtcNow.AddDays(4), eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0);
        _invites.Add(_invite);
        _attendee.MarkInvited();
    }

    [Fact]
    public async Task AValidTokenReturnsTheAttendeeTheirTypesAndThreeOptions()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_invite.Id, result.Value.InviteId);
        Assert.Equal("Amara Novak", result.Value.AttendeeName);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Uniform Fitting" },
            result.Value.AppointmentTypeNames);
        Assert.Equal(3, result.Value.Options.Count);
    }

    [Fact]
    public async Task OptionsCarryTheirWindowAndADisplayString()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        var first = result.Value.Options[0];
        Assert.Equal(new DateOnly(2026, 9, 10), first.Date);
        Assert.Equal(new TimeOnly(9, 0), first.StartTime);
        Assert.Equal(new TimeOnly(13, 0), first.EndTime);
        Assert.Equal("Thursday 10 Sep 2026, 09:00-13:00", first.Display);
    }

    [Fact]
    public async Task OptionsAreOrderedEarliestFirst()
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 13) },
            result.Value.Options.Select(o => o.Date));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task AMalformedTokenGivesTheGenericMessage(string? token)
    {
        var result = await Handler.HandleAsync(new ViewInviteQuery(token), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AWellFormedTokenForNoInviteGivesTheSameMessage()
    {
        var result = await Handler.HandleAsync(
            new ViewInviteQuery(_tokens.Issue(Guid.NewGuid()).Token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AnExpiredInviteGivesTheSameMessage()
    {
        _clock.Advance(TimeSpan.FromDays(5));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AUsedInviteGivesTheSameMessage()
    {
        _invite.MarkUsed();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(InvalidLink, result.Error.Message);
    }

    [Fact]
    public async Task AnOptionWhoseEventHasBeenCancelledIsNotShown()
    {
        _events.Items[1].Cancel();

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.Equal(2, result.Value.Options.Count);
        Assert.DoesNotContain(
            new DateOnly(2026, 9, 11), result.Value.Options.Select(o => o.Date));
    }

    /// <summary>Ensures options on the transitional-location date or earlier are not projected.</summary>
    [Fact]
    public async Task OptionsOnTodayAndEarlierAreNotShown()
    {
        _invite.RemoveOption(_events.Items[0].Id);
        _invite.RemoveOption(_events.Items[1].Id);
        _events.Items[0].Cancel();
        _events.Items[1].Cancel();
        _invite.AddOption(AddEvent(3, 9));
        _invite.AddOption(AddEvent(2, 13));

        var result = await Handler.HandleAsync(new ViewInviteQuery(_token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { new DateOnly(2026, 9, 13) }, result.Value.Options.Select(o => o.Date));
    }

    private Guid AddEvent(int day, int hour)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem.Id;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Candidates/ActiveBookingRequirementTests.cs — 1/1

<!-- vocabulary-file: {"id":258,"oldPath":"tests/EventBooking.Application.Tests/Candidates/ActiveBookingRequirementTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs","beforeSha":"f09a04a03058119a198ca54e74476e7df63a271444f2c64062ef016af86ffd48","afterSha":"d8381a35eb5ad13d6dc05cd0e9c0f380f15100c2ead5836c2a03a5b68030b8ff","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies candidate requirements cannot drift away from an active booking snapshot.</summary>
public sealed class ActiveBookingRequirementTests
{
    private static readonly EmployeeGroup CabinCrew = EmployeeGroup.Define(
        EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly EmployeeGroup GroundTransport = EmployeeGroup.Define(
        EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
        "Ground Transport Services", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly EmployeeGroup Pilots = EmployeeGroup.Define(
        EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);

    /// <summary>Verifies a changed set conflicts before candidate details or requirements mutate.</summary>
    [Fact]
    public async Task ChangedRequirementsAreRejectedBeforeAnyCandidateMutation()
    {
        var (handler, candidate, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateCandidateCommand(
                coordinator,
                candidate.Id,
                "Changed Name",
                "changed@example.com",
                Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("candidate_group_active_booking_conflict", result.Error.Code);
        Assert.Equal(
            "Appointment requirements cannot change while the candidate has an active booking. Cancel and rebook first.",
            result.Error.Message);
        Assert.Equal("Amara Novak", candidate.Name);
        Assert.Equal("amara@example.com", candidate.Email);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting],
            candidate.RequiredAppointmentTypeIds);
    }

    /// <summary>Verifies a set-equivalent group still permits name and email correction.</summary>
    [Fact]
    public async Task SameRequirementSetInAnotherGroupAllowsDetailCorrection()
    {
        var (handler, candidate, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateCandidateCommand(
                coordinator,
                candidate.Id,
                "Amara N. Novak",
                "amara.novak@example.com",
                GroundTransport.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Amara N. Novak", candidate.Name);
        Assert.Equal("amara.novak@example.com", candidate.Email);
    }

    private static (SaveCandidateHandler Handler, Candidate Candidate, Guid Coordinator)
        GivenActiveBooking()
    {
        var coordinator = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var groups = new InMemoryEmployeeGroupRepository();
        groups.Items.AddRange([CabinCrew, GroundTransport, Pilots]);
        var candidates = new InMemoryCandidateRepository();
        var candidate = Candidate.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            CabinCrew);
        candidates.Add(candidate);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(NewBooking(candidate));

        return (
            new SaveCandidateHandler(
                candidates,
                groups,
                new InMemoryInviteRepository(),
                bookings,
                new StaffAccessAuthorizer(profiles),
                new RecordingAuditLogger(),
                new FakeUnitOfWork()),
            candidate,
            coordinator);
    }

    private static Booking NewBooking(Candidate candidate)
    {
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            candidate.Id,
            "invite-token-hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds,
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, slotId, "manage-token-hash", DateTimeOffset.UtcNow);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs — 1/1

<!-- vocabulary-file: {"id":258,"oldPath":"tests/EventBooking.Application.Tests/Candidates/ActiveBookingRequirementTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs","beforeSha":"f09a04a03058119a198ca54e74476e7df63a271444f2c64062ef016af86ffd48","afterSha":"d8381a35eb5ad13d6dc05cd0e9c0f380f15100c2ead5836c2a03a5b68030b8ff","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies attendee requirements cannot drift away from an active booking snapshot.</summary>
public sealed class ActiveBookingRequirementTests
{
    private static readonly AttendeeGroup CabinCrew = AttendeeGroup.Define(
        AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly AttendeeGroup GroundTransport = AttendeeGroup.Define(
        AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
        "Ground Transport Services", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly AttendeeGroup Pilots = AttendeeGroup.Define(
        AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);

    /// <summary>Verifies a changed set conflicts before attendee details or requirements mutate.</summary>
    [Fact]
    public async Task ChangedRequirementsAreRejectedBeforeAnyAttendeeMutation()
    {
        var (handler, attendee, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                coordinator,
                attendee.Id,
                "Changed Name",
                "changed@example.com",
                Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("attendee_group_active_booking_conflict", result.Error.Code);
        Assert.Equal(
            "Appointment requirements cannot change while the attendee has an active booking. Cancel and rebook first.",
            result.Error.Message);
        Assert.Equal("Amara Novak", attendee.Name);
        Assert.Equal("amara@example.com", attendee.Email);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting],
            attendee.RequiredAppointmentTypeIds);
    }

    /// <summary>Verifies a set-equivalent group still permits name and email correction.</summary>
    [Fact]
    public async Task SameRequirementSetInAnotherGroupAllowsDetailCorrection()
    {
        var (handler, attendee, coordinator) = GivenActiveBooking();

        var result = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                coordinator,
                attendee.Id,
                "Amara N. Novak",
                "amara.novak@example.com",
                GroundTransport.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Amara N. Novak", attendee.Name);
        Assert.Equal("amara.novak@example.com", attendee.Email);
    }

    private static (SaveAttendeeHandler Handler, Attendee Attendee, Guid Coordinator)
        GivenActiveBooking()
    {
        var coordinator = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var groups = new InMemoryAttendeeGroupRepository();
        groups.Items.AddRange([CabinCrew, GroundTransport, Pilots]);
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            CabinCrew);
        attendees.Add(attendee);
        var bookings = new InMemoryBookingRepository();
        bookings.Add(NewBooking(attendee));

        return (
            new SaveAttendeeHandler(
                attendees,
                groups,
                new InMemoryInviteRepository(),
                bookings,
                new StaffAccessAuthorizer(profiles),
                new RecordingAuditLogger(),
                new FakeUnitOfWork()),
            attendee,
            coordinator);
    }

    private static Booking NewBooking(Attendee attendee)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "invite-token-hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        return Booking.Create(
            Guid.NewGuid(), invite, eventId, "manage-token-hash", DateTimeOffset.UtcNow);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Candidates/CandidateCsvParserTests.cs — 1/1

<!-- vocabulary-file: {"id":259,"oldPath":"tests/EventBooking.Application.Tests/Candidates/CandidateCsvParserTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/AttendeeCsvParserTests.cs","beforeSha":"590a26bda86c0af10d1fa118b418054326179fe0fa6435d1b162bc4b2d54f142","afterSha":"71bda936a3cc169bdaf0f97a2ab55d3e450e108232a071e788243cf4fcfe9ebc","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Candidates;

namespace EventBooking.Application.Tests.Candidates;

public class CandidateCsvParserTests
{
    private const string Header = "name,email,employee_group";

    [Fact]
    public void AWellFormedFileParsesEveryRow()
    {
        var result = CandidateCsvParser.Parse(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,CABIN_CREW
             B. Chen,b.chen@mail.com,ENGINEERING
             """);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);

        Assert.Equal(2, result.Rows[0].LineNumber);
        Assert.Equal("Amara Novak", result.Rows[0].Name);
        Assert.Equal("a.novak@mail.com", result.Rows[0].Email);
        Assert.Equal("CABIN_CREW", result.Rows[0].EmployeeGroupCode);

        Assert.Equal(3, result.Rows[1].LineNumber);
        Assert.Equal("ENGINEERING", result.Rows[1].EmployeeGroupCode);
    }

    [Fact]
    public void GroupCodesAreCaseInsensitiveAndWhitespaceIsTrimmed()
    {
        var result = CandidateCsvParser.Parse($"{Header}\n  Amara Novak , a.novak@mail.com , pilots ");

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Amara Novak", row.Name);
        Assert.Equal("a.novak@mail.com", row.Email);
        Assert.Equal("PILOTS", row.EmployeeGroupCode);
    }

    [Fact]
    public void BlankLinesAreSkippedWithoutDisturbingLineNumbers()
    {
        var result = CandidateCsvParser.Parse($"{Header}\n\nAmara Novak,a.novak@mail.com,PILOTS\n\n");

        Assert.Empty(result.Errors);
        Assert.Equal(3, Assert.Single(result.Rows).LineNumber);
    }

    [Fact]
    public void AnEmptyFileIsAnError()
    {
        var result = CandidateCsvParser.Parse("");

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(0, error.LineNumber);
        Assert.Equal("The file is empty.", error.Message);
    }

    [Fact]
    public void TheWrongHeaderIsAnError()
    {
        var result = CandidateCsvParser.Parse("name,email,types\nAmara Novak,a.novak@mail.com,PILOTS");

        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
        Assert.Equal(
            "The header line must read exactly: name,email,employee_group",
            error.Message);
    }

    [Fact]
    public void TheOldRequirementHeaderIsAnError()
    {
        var result = CandidateCsvParser.Parse(
            "name,email,appointment_types\nAmara Novak,a.novak@mail.com,DAT;UNI");

        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
    }

    [Fact]
    public void AFileWithOnlyAHeaderIsAnError()
    {
        var result = CandidateCsvParser.Parse(Header);

        var error = Assert.Single(result.Errors);
        Assert.Equal("The file contains no candidate rows.", error.Message);
    }

    [Theory]
    [InlineData("Amara Novak,a.novak@mail.com")]
    [InlineData("Amara Novak,a.novak@mail.com,PILOTS,extra")]
    public void TheWrongNumberOfFieldsIsARowError(string line)
    {
        var result = CandidateCsvParser.Parse($"{Header}\n{line}");

        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("Expected 3 comma-separated fields: name, email, employee_group.", error.Message);
    }

    [Fact]
    public void ABlankNameIsARowError()
    {
        var result = CandidateCsvParser.Parse($"{Header}\n ,a.novak@mail.com,PILOTS");

        Assert.Equal("Name is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ABlankEmailIsARowError()
    {
        var result = CandidateCsvParser.Parse($"{Header}\nAmara Novak, ,PILOTS");

        Assert.Equal("Email is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void NoEmployeeGroupIsARowError()
    {
        var result = CandidateCsvParser.Parse($"{Header}\nAmara Novak,a.novak@mail.com, ");

        Assert.Equal("Employee group is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnknownCodePassesStructuralValidationForTheImportHandler()
    {
        var result = CandidateCsvParser.Parse($"{Header}\nAmara Novak,a.novak@mail.com,UNKNOWN_GROUP");

        Assert.Empty(result.Errors);
        Assert.Equal("UNKNOWN_GROUP", Assert.Single(result.Rows).EmployeeGroupCode);
    }

    [Fact]
    public void ARepeatedEmailInTheFileIsARowErrorOnTheSecondOccurrence()
    {
        var result = CandidateCsvParser.Parse(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             Someone Else,A.NOVAK@mail.com,ENGINEERING
             """);

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("a.novak@mail.com appears more than once in this file.", error.Message);
    }

    [Fact]
    public void EveryBadRowIsReportedNotJustTheFirst()
    {
        var result = CandidateCsvParser.Parse(
            $"""
             {Header}
             ,a.novak@mail.com,PILOTS
             B. Chen,b.chen@mail.com,
             C. Diallo,c.diallo@mail.com,ENGINEERING
             """);

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal([2, 3], result.Errors.Select(e => e.LineNumber));
        Assert.Single(result.Rows);
    }

    [Fact]
    public void WindowsLineEndingsAreHandled()
    {
        var result = CandidateCsvParser.Parse($"{Header}\r\nAmara Novak,a.novak@mail.com,PILOTS\r\n");

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Attendees/AttendeeCsvParserTests.cs — 1/1

<!-- vocabulary-file: {"id":259,"oldPath":"tests/EventBooking.Application.Tests/Candidates/CandidateCsvParserTests.cs","newPath":"tests/EventBooking.Application.Tests/Attendees/AttendeeCsvParserTests.cs","beforeSha":"590a26bda86c0af10d1fa118b418054326179fe0fa6435d1b162bc4b2d54f142","afterSha":"71bda936a3cc169bdaf0f97a2ab55d3e450e108232a071e788243cf4fcfe9ebc","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Attendees;

namespace EventBooking.Application.Tests.Attendees;

public class AttendeeCsvParserTests
{
    private const string Header = "name,email,attendee_group";

    [Fact]
    public void AWellFormedFileParsesEveryRow()
    {
        var result = AttendeeCsvParser.Parse(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,CABIN_CREW
             B. Chen,b.chen@mail.com,ENGINEERING
             """);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);

        Assert.Equal(2, result.Rows[0].LineNumber);
        Assert.Equal("Amara Novak", result.Rows[0].Name);
        Assert.Equal("a.novak@mail.com", result.Rows[0].Email);
        Assert.Equal("CABIN_CREW", result.Rows[0].AttendeeGroupCode);

        Assert.Equal(3, result.Rows[1].LineNumber);
        Assert.Equal("ENGINEERING", result.Rows[1].AttendeeGroupCode);
    }

    [Fact]
    public void GroupCodesAreCaseInsensitiveAndWhitespaceIsTrimmed()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\n  Amara Novak , a.novak@mail.com , pilots ");

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Amara Novak", row.Name);
        Assert.Equal("a.novak@mail.com", row.Email);
        Assert.Equal("PILOTS", row.AttendeeGroupCode);
    }

    [Fact]
    public void BlankLinesAreSkippedWithoutDisturbingLineNumbers()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\n\nAmara Novak,a.novak@mail.com,PILOTS\n\n");

        Assert.Empty(result.Errors);
        Assert.Equal(3, Assert.Single(result.Rows).LineNumber);
    }

    [Fact]
    public void AnEmptyFileIsAnError()
    {
        var result = AttendeeCsvParser.Parse("");

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(0, error.LineNumber);
        Assert.Equal("The file is empty.", error.Message);
    }

    [Fact]
    public void TheWrongHeaderIsAnError()
    {
        var result = AttendeeCsvParser.Parse("name,email,types\nAmara Novak,a.novak@mail.com,PILOTS");

        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
        Assert.Equal(
            "The header line must read exactly: name,email,attendee_group",
            error.Message);
    }

    [Fact]
    public void TheOldRequirementHeaderIsAnError()
    {
        var result = AttendeeCsvParser.Parse(
            "name,email,appointment_types\nAmara Novak,a.novak@mail.com,DAT;UNI");

        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
    }

    [Fact]
    public void AFileWithOnlyAHeaderIsAnError()
    {
        var result = AttendeeCsvParser.Parse(Header);

        var error = Assert.Single(result.Errors);
        Assert.Equal("The file contains no attendee rows.", error.Message);
    }

    [Theory]
    [InlineData("Amara Novak,a.novak@mail.com")]
    [InlineData("Amara Novak,a.novak@mail.com,PILOTS,extra")]
    public void TheWrongNumberOfFieldsIsARowError(string line)
    {
        var result = AttendeeCsvParser.Parse($"{Header}\n{line}");

        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("Expected 3 comma-separated fields: name, email, attendee_group.", error.Message);
    }

    [Fact]
    public void ABlankNameIsARowError()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\n ,a.novak@mail.com,PILOTS");

        Assert.Equal("Name is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ABlankEmailIsARowError()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\nAmara Novak, ,PILOTS");

        Assert.Equal("Email is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void NoAttendeeGroupIsARowError()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\nAmara Novak,a.novak@mail.com, ");

        Assert.Equal("Employee group is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnknownCodePassesStructuralValidationForTheImportHandler()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\nAmara Novak,a.novak@mail.com,UNKNOWN_GROUP");

        Assert.Empty(result.Errors);
        Assert.Equal("UNKNOWN_GROUP", Assert.Single(result.Rows).AttendeeGroupCode);
    }

    [Fact]
    public void ARepeatedEmailInTheFileIsARowErrorOnTheSecondOccurrence()
    {
        var result = AttendeeCsvParser.Parse(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             Someone Else,A.NOVAK@mail.com,ENGINEERING
             """);

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("a.novak@mail.com appears more than once in this file.", error.Message);
    }

    [Fact]
    public void EveryBadRowIsReportedNotJustTheFirst()
    {
        var result = AttendeeCsvParser.Parse(
            $"""
             {Header}
             ,a.novak@mail.com,PILOTS
             B. Chen,b.chen@mail.com,
             C. Diallo,c.diallo@mail.com,ENGINEERING
             """);

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal([2, 3], result.Errors.Select(e => e.LineNumber));
        Assert.Single(result.Rows);
    }

    [Fact]
    public void WindowsLineEndingsAreHandled()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\r\nAmara Novak,a.novak@mail.com,PILOTS\r\n");

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }
}
`````
