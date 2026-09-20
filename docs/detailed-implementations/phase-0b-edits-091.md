# 00b — Vocabulary edits 91 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Domain.Tests/Invites/InviteTests.cs — 1/1

<!-- vocabulary-file: {"id":310,"oldPath":"tests/EventBooking.Domain.Tests/Invites/InviteTests.cs","newPath":"tests/EventBooking.Domain.Tests/Invites/InviteTests.cs","beforeSha":"74b496c6098f24acef6b7aef47cba988fdddbccd10aec1487c37efffbe281354","afterSha":"92141fb780fe16ed1a2d551764117e8fdf0212795733d7dab4ffce6fdb519184","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

public class InviteTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid EventB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid EventC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid EventD = Guid.Parse("50000004-0000-0000-0000-000000000004");

    private static Invite NewInvite(int retryCount = 0) =>
        Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "hash-of-the-token", Now.AddDays(4),
            [EventA, EventB, EventC], [AppointmentTypeIds.DrugAndAlcoholTesting], retryCount);

    [Fact]
    public void ANewInviteIsPendingWithThreeOptions()
    {
        var invite = NewInvite();

        Assert.Equal(InviteStatus.Pending, invite.Status);
        Assert.Equal(3, invite.Options.Count);
        Assert.Equal(Invite.RequiredOptionCount, invite.Options.Count);
        Assert.Equal([EventA, EventB, EventC], invite.OfferedEventIds);
        Assert.Equal(0, invite.RetryCount);
        Assert.Equal("hash-of-the-token", invite.TokenHash);
    }

    [Fact]
    public void EveryOptionBelongsToTheInvite()
    {
        var invite = NewInvite();

        Assert.All(invite.Options, o => Assert.Equal(invite.Id, o.InviteId));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void AnInviteMustOfferExactlyThreeOptions(int optionCount)
    {
        var events = new[] { EventA, EventB, EventC, EventD }.Take(optionCount);

        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                events, [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("An invite must offer exactly 3 event options.", ex.Message);
    }

    [Fact]
    public void TheSameEventCannotBeOfferedTwice()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                [EventA, EventA, EventB], [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("An invite cannot offer the same event twice.", ex.Message);
    }

    [Fact]
    public void AnInviteWithoutATokenHashIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "  ", Now.AddDays(4),
                [EventA, EventB, EventC], [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("tokenHash must not be blank.", ex.Message);
    }

    [Fact]
    public void ANegativeRetryCountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                [EventA, EventB, EventC], [AppointmentTypeIds.DrugAndAlcoholTesting], -1));
    }

    [Fact]
    public void APendingInviteIsUsableUntilItExpires()
    {
        var invite = NewInvite();

        Assert.True(invite.IsUsableAt(Now));
        Assert.True(invite.IsUsableAt(Now.AddDays(4).AddSeconds(-1)));
        Assert.False(invite.IsUsableAt(Now.AddDays(4)));
        Assert.False(invite.IsUsableAt(Now.AddDays(5)));
    }

    [Fact]
    public void AUsedInviteIsNeverUsableAgain()
    {
        var invite = NewInvite();
        invite.MarkUsed();

        Assert.Equal(InviteStatus.Used, invite.Status);
        Assert.False(invite.IsUsableAt(Now));
    }

    [Fact]
    public void OnlyAPendingInviteCanBeUsedExpiredOrSuperseded()
    {
        var used = NewInvite();
        used.MarkUsed();
        Assert.Throws<DomainException>(() => used.MarkExpired());
        Assert.Throws<DomainException>(() => used.MarkSuperseded());
        Assert.Throws<DomainException>(() => used.MarkUsed());

        var expired = NewInvite();
        expired.MarkExpired();
        Assert.Equal(InviteStatus.Expired, expired.Status);
        Assert.Throws<DomainException>(() => expired.MarkUsed());

        var superseded = NewInvite();
        superseded.MarkSuperseded();
        Assert.Equal(InviteStatus.Superseded, superseded.Status);
    }

    [Fact]
    public void AnOptionThatFilledUpIsDroppedAndAReplacementRestoresThree()
    {
        var invite = NewInvite();

        invite.RemoveOption(EventB);
        Assert.Equal(2, invite.Options.Count);
        Assert.False(invite.Offers(EventB));

        invite.AddOption(EventD);
        Assert.Equal(3, invite.Options.Count);
        Assert.True(invite.Offers(EventD));
    }

    [Fact]
    public void AFourthOptionIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(() => invite.AddOption(EventD));
        Assert.Equal("An invite cannot offer more than 3 event options.", ex.Message);
    }

    [Fact]
    public void AddingAnOptionAlreadyOfferedIsRejected()
    {
        var invite = NewInvite();
        invite.RemoveOption(EventB);

        var ex = Assert.Throws<DomainException>(() => invite.AddOption(EventA));
        Assert.Equal("An invite cannot offer the same event twice.", ex.Message);
    }

    [Fact]
    public void RemovingAnOptionThatWasNotOfferedIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(() => invite.RemoveOption(EventD));
        Assert.Equal("This invite does not offer that eventItem.", ex.Message);
    }

    [Fact]
    public void OptionsCanOnlyChangeWhileTheInviteIsPending()
    {
        var invite = NewInvite();
        invite.MarkUsed();

        Assert.Throws<DomainException>(() => invite.RemoveOption(EventA));
        Assert.Throws<DomainException>(() => invite.AddOption(EventD));
    }

    [Fact]
    public void TheRetryCountIsCarriedForwardByTheCaller()
    {
        var invite = NewInvite(retryCount: 2);

        Assert.Equal(2, invite.RetryCount);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/OntologyEnumTests.cs — 1/1

<!-- vocabulary-file: {"id":311,"oldPath":"tests/EventBooking.Domain.Tests/OntologyEnumTests.cs","newPath":"tests/EventBooking.Domain.Tests/OntologyEnumTests.cs","beforeSha":"5f794cd9644e8506e24039f0e30f88740bb4f0326906dda5b0b56a3c6106de88","afterSha":"5f2be5a404ffabf7580e92375f0e0776b9cc98a69f6d22354e7883449d832adc","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests;

/// <summary>
/// Locks every enum to the member list docs/ontology.md declares. Changing an enum without
/// changing the ontology fails here, which is the point.
/// </summary>
public class OntologyEnumTests
{
    /// <summary>Checks the role vocabulary against the ontology.</summary>
    [Fact]
    public void RoleMatchesTheOntology()
    {
        Assert.Equal(
            new[] { "Manager", "Coordinator", "Admin", "AppointmentStaff" },
            Enum.GetNames<Role>());
    }

    /// <summary>Checks the candidate lifecycle vocabulary against the ontology.</summary>
    [Fact]
    public void CandidateStatusMatchesTheOntology()
    {
        Assert.Equal(
            new[]
            {
                "NotYetInvited", "AwaitingAvailability", "Invited", "Booked",
                "NoResponseNeedsFollowUp",
            },
            Enum.GetNames<CandidateStatus>());
    }

    /// <summary>Checks the slot-proposal vocabulary against the ontology.</summary>
    [Fact]
    public void SlotProposalStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Open", "Withdrawn", "Confirmed" }, Enum.GetNames<SlotProposalStatus>());
    }

    /// <summary>Checks the confirmed-slot vocabulary against the ontology.</summary>
    [Fact]
    public void ConfirmedSlotStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Active", "Cancelled" }, Enum.GetNames<ConfirmedSlotStatus>());
    }

    /// <summary>Checks the invite vocabulary against the ontology.</summary>
    [Fact]
    public void InviteStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Pending", "Used", "Expired", "Superseded", "Cancelled" }, Enum.GetNames<InviteStatus>());
    }

    /// <summary>Checks the booking vocabulary against the ontology.</summary>
    [Fact]
    public void BookingStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Active", "Cancelled", "Concluded" }, Enum.GetNames<BookingStatus>());
    }

    /// <summary>Checks the audit actor vocabulary against the ontology.</summary>
    [Fact]
    public void ActorTypeMatchesTheOntology()
    {
        Assert.Equal(new[] { "Staff", "CandidateToken", "System" }, Enum.GetNames<ActorType>());
    }

    /// <summary>Checks the audit action vocabulary against the ontology.</summary>
    [Fact]
    public void AuditActionMatchesTheOntology()
    {
        Assert.Equal(
            new[]
            {
                "ProposalCreated", "ProposalWithdrawn", "AcceptanceRecorded", "AcceptanceWithdrawn",
                "SlotConfirmed", "SlotCancelled", "CapacityDecremented", "CapacityIncremented",
                "InviteCreated", "InviteSent", "InviteExpired", "InviteOptionReplaced",
                "BookingCreated", "BookingCancelled", "CapacityAdjusted", "SlotImported",
                "StaffAccessChanged", "StaffAccessRemoved",
                "AppointmentCheckedIn", "AppointmentCompleted",
                "AppointmentMarkedNoShow", "AppointmentStatusCorrected",
                "EmployeeGroupAssigned", "EmployeeGroupChanged",
                "RecoveryInviteCreated", "RecoveryInviteCancelled",
                "RecoveryBookingCreated", "RecoveryBookingConcluded", "StaffRolesSynced",
                "CandidateDeleted",
            },
            Enum.GetNames<AuditAction>());
    }

    /// <summary>Verifies booking-appointment statuses remain synchronized with the ontology.</summary>
    [Fact]
    public void BookingAppointmentStatusMatchesTheOntology()
    {
        Assert.Equal(
            new[] { "Expected", "CheckedIn", "Completed", "NoShow" },
            Enum.GetNames<BookingAppointmentStatus>());
    }

    /// <summary>Checks the email-template vocabulary against the ontology.</summary>
    [Fact]
    public void EmailTemplateMatchesTheOntology()
    {
        Assert.Equal(
            new[]
            {
                "CandidateInvite", "BookingConfirmation", "SlotCancelledRebookingNeeded",
                "CandidateReinvite",
            },
            Enum.GetNames<EmailTemplate>());
    }

    /// <summary>Checks the durable email delivery vocabulary against the ontology.</summary>
    [Fact]
    public void EmailStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Sent", "Failed", "Pending", "Resolved" }, Enum.GetNames<EmailStatus>());
    }

    /// <summary>Ensures every persisted enum has a deliberate non-zero value.</summary>
    [Fact]
    public void NoEnumMemberUsesTheDefaultZeroValue()
    {
        // A zero member would be indistinguishable from an unset integer column.
        Assert.DoesNotContain(0, Enum.GetValues<Role>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<CandidateStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<SlotProposalStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<ConfirmedSlotStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<InviteStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<BookingStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<BookingAppointmentStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<ActorType>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<AuditAction>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<EmailTemplate>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<EmailStatus>().Cast<int>());
    }
}
`````

## after — tests/EventBooking.Domain.Tests/OntologyEnumTests.cs — 1/1

<!-- vocabulary-file: {"id":311,"oldPath":"tests/EventBooking.Domain.Tests/OntologyEnumTests.cs","newPath":"tests/EventBooking.Domain.Tests/OntologyEnumTests.cs","beforeSha":"5f794cd9644e8506e24039f0e30f88740bb4f0326906dda5b0b56a3c6106de88","afterSha":"5f2be5a404ffabf7580e92375f0e0776b9cc98a69f6d22354e7883449d832adc","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests;

/// <summary>
/// Locks every enum to the member list docs/ontology.md declares. Changing an enum without
/// changing the ontology fails here, which is the point.
/// </summary>
public class OntologyEnumTests
{
    /// <summary>Checks the role vocabulary against the ontology.</summary>
    [Fact]
    public void RoleMatchesTheOntology()
    {
        Assert.Equal(
            new[] { "Manager", "Coordinator", "Admin", "AppointmentStaff" },
            Enum.GetNames<Role>());
    }

    /// <summary>Checks the attendee lifecycle vocabulary against the ontology.</summary>
    [Fact]
    public void AttendeeStatusMatchesTheOntology()
    {
        Assert.Equal(
            new[]
            {
                "NotYetInvited", "AwaitingAvailability", "Invited", "Booked",
                "NoResponseNeedsFollowUp",
            },
            Enum.GetNames<AttendeeStatus>());
    }

    /// <summary>Checks the event-proposal vocabulary against the ontology.</summary>
    [Fact]
    public void EventProposalStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Open", "Withdrawn", "Confirmed" }, Enum.GetNames<EventProposalStatus>());
    }

    /// <summary>Checks the event vocabulary against the ontology.</summary>
    [Fact]
    public void EventStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Active", "Cancelled" }, Enum.GetNames<EventStatus>());
    }

    /// <summary>Checks the invite vocabulary against the ontology.</summary>
    [Fact]
    public void InviteStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Pending", "Used", "Expired", "Superseded", "Cancelled" }, Enum.GetNames<InviteStatus>());
    }

    /// <summary>Checks the booking vocabulary against the ontology.</summary>
    [Fact]
    public void BookingStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Active", "Cancelled", "Concluded" }, Enum.GetNames<BookingStatus>());
    }

    /// <summary>Checks the audit actor vocabulary against the ontology.</summary>
    [Fact]
    public void ActorTypeMatchesTheOntology()
    {
        Assert.Equal(new[] { "Staff", "AttendeeToken", "System" }, Enum.GetNames<ActorType>());
    }

    /// <summary>Checks the audit action vocabulary against the ontology.</summary>
    [Fact]
    public void AuditActionMatchesTheOntology()
    {
        Assert.Equal(
            new[]
            {
                "ProposalCreated", "ProposalWithdrawn", "AcceptanceRecorded", "AcceptanceWithdrawn",
                "EventConfirmed", "EventCancelled", "CapacityDecremented", "CapacityIncremented",
                "InviteCreated", "InviteSent", "InviteExpired", "InviteOptionReplaced",
                "BookingCreated", "BookingCancelled", "CapacityAdjusted", "EventImported",
                "StaffAccessChanged", "StaffAccessRemoved",
                "AppointmentCheckedIn", "AppointmentCompleted",
                "AppointmentMarkedNoShow", "AppointmentStatusCorrected",
                "AttendeeGroupAssigned", "AttendeeGroupReassigned",
                "RecoveryInviteCreated", "RecoveryInviteCancelled",
                "RecoveryBookingCreated", "RecoveryBookingConcluded", "StaffRolesSynced",
                "AttendeeDeleted",
            },
            Enum.GetNames<AuditAction>());
    }

    /// <summary>Verifies booking-appointment statuses remain synchronized with the ontology.</summary>
    [Fact]
    public void BookingAppointmentStatusMatchesTheOntology()
    {
        Assert.Equal(
            new[] { "Expected", "CheckedIn", "Completed", "NoShow" },
            Enum.GetNames<BookingAppointmentStatus>());
    }

    /// <summary>Checks the email-template vocabulary against the ontology.</summary>
    [Fact]
    public void EmailTemplateMatchesTheOntology()
    {
        Assert.Equal(
            new[]
            {
                "AttendeeInvite", "BookingConfirmation", "EventCancelledRebookingNeeded",
                "AttendeeReinvite",
            },
            Enum.GetNames<EmailTemplate>());
    }

    /// <summary>Checks the durable email delivery vocabulary against the ontology.</summary>
    [Fact]
    public void EmailStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Sent", "Failed", "Pending", "Resolved" }, Enum.GetNames<EmailStatus>());
    }

    /// <summary>Ensures every persisted enum has a deliberate non-zero value.</summary>
    [Fact]
    public void NoEnumMemberUsesTheDefaultZeroValue()
    {
        // A zero member would be indistinguishable from an unset integer column.
        Assert.DoesNotContain(0, Enum.GetValues<Role>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<AttendeeStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<EventProposalStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<EventStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<InviteStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<BookingStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<BookingAppointmentStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<ActorType>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<AuditAction>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<EmailTemplate>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<EmailStatus>().Cast<int>());
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotCancellationTests.cs — 1/1

<!-- vocabulary-file: {"id":312,"oldPath":"tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotCancellationTests.cs","newPath":"tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs","beforeSha":"6609d93db88d174a9231c2ba2132b8ef05998b421ffd2bc268a66df22678c7bb","afterSha":"f03dccfeef3344b486383b1f186ad08856971c45fc9388cef205f1ef7d09c034","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ConfirmedSlotCancellationTests
{
    private static ConfirmedSlot ActiveSlot()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void CancellingMarksTheSlotCancelled()
    {
        var slot = ActiveSlot();

        slot.Cancel();

        Assert.Equal(ConfirmedSlotStatus.Cancelled, slot.Status);
    }

    [Fact]
    public void ACancelledSlotOffersNoSpareCapacityEvenWhenItsCountersAreFull()
    {
        var slot = ActiveSlot();
        Assert.True(slot.HasSpareCapacityForAll(AppointmentTypeIds.All));

        slot.Cancel();

        Assert.False(slot.HasSpareCapacityForAll(AppointmentTypeIds.All));
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var slot = ActiveSlot();
        slot.Cancel();

        var ex = Assert.Throws<DomainException>(() => slot.Cancel());
        Assert.Equal("This slot has already been cancelled.", ex.Message);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs — 1/1

<!-- vocabulary-file: {"id":312,"oldPath":"tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotCancellationTests.cs","newPath":"tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs","beforeSha":"6609d93db88d174a9231c2ba2132b8ef05998b421ffd2bc268a66df22678c7bb","afterSha":"f03dccfeef3344b486383b1f186ad08856971c45fc9388cef205f1ef7d09c034","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventCancellationTests
{
    private static Event ActiveEvent()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void CancellingMarksTheEventCancelled()
    {
        var eventItem = ActiveEvent();

        eventItem.Cancel();

        Assert.Equal(EventStatus.Cancelled, eventItem.Status);
    }

    [Fact]
    public void ACancelledEventOffersNoSpareCapacityEvenWhenItsCountersAreFull()
    {
        var eventItem = ActiveEvent();
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));

        eventItem.Cancel();

        Assert.False(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var eventItem = ActiveEvent();
        eventItem.Cancel();

        var ex = Assert.Throws<DomainException>(() => eventItem.Cancel());
        Assert.Equal("This event has already been cancelled.", ex.Message);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotImportTests.cs — 1/1

<!-- vocabulary-file: {"id":313,"oldPath":"tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotImportTests.cs","newPath":"tests/EventBooking.Domain.Tests/Events/EventImportTests.cs","beforeSha":"9f58ec736f48b877d04aeea8166a71c1bfd5d1875388b8dbf31836175deea748","afterSha":"3d5e133abbe6e84d7d84670ea28682eeeddb8bdfc2cdaf96f9afdbeb109c3d15","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ConfirmedSlotImportTests
{
    private static readonly SlotWindow Window = new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

    private static Dictionary<Guid, int> FullHeadcounts(int dat = 10, int med = 6, int uni = 8) => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = dat,
        [AppointmentTypeIds.MedicalCheckUp] = med,
        [AppointmentTypeIds.UniformFitting] = uni,
    };

    [Fact]
    public void AnImportedSlotHasNoProposalAndIsActive()
    {
        var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts());

        Assert.Null(slot.ProposalId);
        Assert.Equal(Window, slot.Window);
        Assert.Equal(ConfirmedSlotStatus.Active, slot.Status);
    }

    [Fact]
    public void EachAppointmentTypeGetsItsOwnHeadcountAsBothTotalAndRemaining()
    {
        var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts(10, 6, 8));

        Assert.Equal(3, slot.Capacities.Count);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void AMissingAppointmentTypeIsRejected()
    {
        var incomplete = new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 6,
        };

        Assert.Throws<DomainException>(
            () => ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, incomplete));
    }

    [Fact]
    public void AnExtraAppointmentTypeIsRejected()
    {
        var headcounts = FullHeadcounts();
        headcounts.Add(Guid.NewGuid(), 4);

        Assert.Throws<DomainException>(
            () => ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, headcounts));
    }

    [Fact]
    public void ANonPositiveHeadcountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts(dat: 0)));
    }

    [Fact]
    public void TwoImportedSlotsAreIndependentEntities()
    {
        var first = ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts());
        var second = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0)), FullHeadcounts());

        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(first.ProposalId);
        Assert.Null(second.ProposalId);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventImportTests.cs — 1/1

<!-- vocabulary-file: {"id":313,"oldPath":"tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotImportTests.cs","newPath":"tests/EventBooking.Domain.Tests/Events/EventImportTests.cs","beforeSha":"9f58ec736f48b877d04aeea8166a71c1bfd5d1875388b8dbf31836175deea748","afterSha":"3d5e133abbe6e84d7d84670ea28682eeeddb8bdfc2cdaf96f9afdbeb109c3d15","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventImportTests
{
    private static readonly EventWindow Window = new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

    private static Dictionary<Guid, int> FullHeadcounts(int dat = 10, int med = 6, int uni = 8) => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = dat,
        [AppointmentTypeIds.MedicalCheckUp] = med,
        [AppointmentTypeIds.UniformFitting] = uni,
    };

    [Fact]
    public void AnImportedEventHasNoProposalAndIsActive()
    {
        var eventItem = Event.CreateImported(Guid.NewGuid(), Window, FullHeadcounts());

        Assert.Null(eventItem.ProposalId);
        Assert.Equal(Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
    }

    [Fact]
    public void EachAppointmentTypeGetsItsOwnHeadcountAsBothTotalAndRemaining()
    {
        var eventItem = Event.CreateImported(Guid.NewGuid(), Window, FullHeadcounts(10, 6, 8));

        Assert.Equal(3, eventItem.Capacities.Count);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void AMissingAppointmentTypeIsRejected()
    {
        var incomplete = new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 6,
        };

        Assert.Throws<DomainException>(
            () => Event.CreateImported(Guid.NewGuid(), Window, incomplete));
    }

    [Fact]
    public void AnExtraAppointmentTypeIsRejected()
    {
        var headcounts = FullHeadcounts();
        headcounts.Add(Guid.NewGuid(), 4);

        Assert.Throws<DomainException>(
            () => Event.CreateImported(Guid.NewGuid(), Window, headcounts));
    }

    [Fact]
    public void ANonPositiveHeadcountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => Event.CreateImported(Guid.NewGuid(), Window, FullHeadcounts(dat: 0)));
    }

    [Fact]
    public void TwoImportedEventsAreIndependentEntities()
    {
        var first = Event.CreateImported(Guid.NewGuid(), Window, FullHeadcounts());
        var second = Event.CreateImported(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0)), FullHeadcounts());

        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(first.ProposalId);
        Assert.Null(second.ProposalId);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotTests.cs — 1/1

<!-- vocabulary-file: {"id":314,"oldPath":"tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotTests.cs","newPath":"tests/EventBooking.Domain.Tests/Events/EventTests.cs","beforeSha":"bae5346589b445248440be01473130a0286031a46dd72da6f373f03cb8e6a63b","afterSha":"19afa88eb3d03fcd2d05a26c0091add6b05077fe5f8909f7017b6cc2debc84f4","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ConfirmedSlotTests
{
    private static SlotProposal FullyAcceptedProposal(
        int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void ConfirmingCarriesTheWindowAndMarksTheProposalConfirmed()
    {
        var proposal = FullyAcceptedProposal();

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(proposal.Id, slot.ProposalId);
        Assert.Equal(proposal.Window, slot.Window);
        Assert.Equal(ConfirmedSlotStatus.Active, slot.Status);
        Assert.Equal(SlotProposalStatus.Confirmed, proposal.Status);
    }

    [Fact]
    public void ConfirmingCreatesOneCapacityCounterPerAppointmentType()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Equal(3, slot.Capacities.Count);
        Assert.Equal(
            AppointmentTypeIds.All.OrderBy(id => id),
            slot.Capacities.Select(c => c.AppointmentTypeId).OrderBy(id => id));
    }

    [Fact]
    public void EachCounterStartsAtTheHeadcountItsManagerAccepted()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal(10, 6, 8));

        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void EveryCounterBelongsToTheSlotThatOwnsIt()
    {
        var id = Guid.NewGuid();

        var slot = ConfirmedSlot.CreateFrom(id, FullyAcceptedProposal());

        Assert.All(slot.Capacities, c => Assert.Equal(id, c.ConfirmedSlotId));
    }

    [Fact]
    public void APartlyAcceptedProposalCannotBeConfirmed()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);

        var ex = Assert.Throws<DomainException>(() => ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
        Assert.Equal("A proposal can only be confirmed once all 3 managers have accepted it.", ex.Message);
    }

    [Fact]
    public void AProposalCannotBeConfirmedTwice()
    {
        var proposal = FullyAcceptedProposal();
        ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Throws<DomainException>(() => ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
    }

    [Fact]
    public void CapacityForAnUnknownAppointmentTypeIsRejected()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Throws<DomainException>(() => slot.CapacityFor(Guid.NewGuid()));
    }

    [Fact]
    public void SpareCapacityIsCheckedAcrossEveryRequiredType()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.True(slot.HasSpareCapacityForAll(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        Assert.True(slot.HasSpareCapacityForAll(AppointmentTypeIds.All));
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/EventTests.cs — 1/1

<!-- vocabulary-file: {"id":314,"oldPath":"tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotTests.cs","newPath":"tests/EventBooking.Domain.Tests/Events/EventTests.cs","beforeSha":"bae5346589b445248440be01473130a0286031a46dd72da6f373f03cb8e6a63b","afterSha":"19afa88eb3d03fcd2d05a26c0091add6b05077fe5f8909f7017b6cc2debc84f4","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class EventTests
{
    private static EventProposal FullyAcceptedProposal(
        int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void ConfirmingCarriesTheWindowAndMarksTheProposalConfirmed()
    {
        var proposal = FullyAcceptedProposal();

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(proposal.Id, eventItem.ProposalId);
        Assert.Equal(proposal.Window, eventItem.Window);
        Assert.Equal(EventStatus.Active, eventItem.Status);
        Assert.Equal(EventProposalStatus.Confirmed, proposal.Status);
    }

    [Fact]
    public void ConfirmingCreatesOneCapacityCounterPerAppointmentType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Equal(3, eventItem.Capacities.Count);
        Assert.Equal(
            AppointmentTypeIds.All.OrderBy(id => id),
            eventItem.Capacities.Select(c => c.AppointmentTypeId).OrderBy(id => id));
    }

    [Fact]
    public void EachCounterStartsAtTheHeadcountItsManagerAccepted()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal(10, 6, 8));

        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void EveryCounterBelongsToTheEventThatOwnsIt()
    {
        var id = Guid.NewGuid();

        var eventItem = Event.CreateFrom(id, FullyAcceptedProposal());

        Assert.All(eventItem.Capacities, c => Assert.Equal(id, c.EventId));
    }

    [Fact]
    public void APartlyAcceptedProposalCannotBeConfirmed()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);

        var ex = Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
        Assert.Equal("A proposal can only be confirmed once all 3 managers have accepted it.", ex.Message);
    }

    [Fact]
    public void AProposalCannotBeConfirmedTwice()
    {
        var proposal = FullyAcceptedProposal();
        Event.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Throws<DomainException>(() => Event.CreateFrom(Guid.NewGuid(), proposal));
    }

    [Fact]
    public void CapacityForAnUnknownAppointmentTypeIsRejected()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Throws<DomainException>(() => eventItem.CapacityFor(Guid.NewGuid()));
    }

    [Fact]
    public void SpareCapacityIsCheckedAcrossEveryRequiredType()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.True(eventItem.HasSpareCapacityForAll(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        Assert.True(eventItem.HasSpareCapacityForAll(AppointmentTypeIds.All));
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Slots/ProposalAcceptanceHeadcountRevisionTests.cs — 1/1

<!-- vocabulary-file: {"id":315,"oldPath":"tests/EventBooking.Domain.Tests/Slots/ProposalAcceptanceHeadcountRevisionTests.cs","newPath":"tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs","beforeSha":"60f7ee50ac59ad93f8a6e21d5d1535f72526294ab59afdd760cfb998ed224ffd","afterSha":"a25264d1cd3bb63721b0c003f673a70eff65c452d1ccd4c01cb280804c8f9541","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ProposalAcceptanceHeadcountRevisionTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerDrugAndAlcoholManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");
    private static readonly Guid MedicalManager =
        Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager =
        Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private static SlotProposal NewProposal() =>
        SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);

    [Fact]
    public void TheSameManagerRevisesTheExistingAcceptanceInPlace()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        var original = Assert.Single(proposal.Acceptances);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Same(original, Assert.Single(proposal.Acceptances));
        Assert.Equal(12, original.Headcount);
    }

    [Fact]
    public void ResubmittingTheCurrentHeadcountReportsNoChange()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        Assert.False(changed);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AReplacementManagerRevisesTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, FormerDrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Equal(12, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void AnInvalidRevisionLeavesTheCurrentHeadcountUntouched(int headcount)
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, headcount));

        Assert.Equal("headcount must be greater than zero.", ex.Message);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AWithdrawnProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Withdraw(DrugAndAlcoholManager);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.MedicalCheckUp, MedicalManager, 8));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(6, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AConfirmedProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(
            10,
            proposal.Acceptances.Single(acceptance =>
                acceptance.AppointmentTypeId
                == AppointmentTypeIds.DrugAndAlcoholTesting).Headcount);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs — 1/1

<!-- vocabulary-file: {"id":315,"oldPath":"tests/EventBooking.Domain.Tests/Slots/ProposalAcceptanceHeadcountRevisionTests.cs","newPath":"tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs","beforeSha":"60f7ee50ac59ad93f8a6e21d5d1535f72526294ab59afdd760cfb998ed224ffd","afterSha":"a25264d1cd3bb63721b0c003f673a70eff65c452d1ccd4c01cb280804c8f9541","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Domain.Tests.Events;

public class ProposalAcceptanceHeadcountRevisionTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerDrugAndAlcoholManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");
    private static readonly Guid MedicalManager =
        Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager =
        Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private static EventProposal NewProposal() =>
        EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);

    [Fact]
    public void TheSameManagerRevisesTheExistingAcceptanceInPlace()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        var original = Assert.Single(proposal.Acceptances);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Same(original, Assert.Single(proposal.Acceptances));
        Assert.Equal(12, original.Headcount);
    }

    [Fact]
    public void ResubmittingTheCurrentHeadcountReportsNoChange()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        Assert.False(changed);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AReplacementManagerRevisesTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, FormerDrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Equal(12, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void AnInvalidRevisionLeavesTheCurrentHeadcountUntouched(int headcount)
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, headcount));

        Assert.Equal("headcount must be greater than zero.", ex.Message);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AWithdrawnProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Withdraw(DrugAndAlcoholManager);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.MedicalCheckUp, MedicalManager, 8));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(6, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AConfirmedProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        Event.CreateFrom(Guid.NewGuid(), proposal);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(
            10,
            proposal.Acceptances.Single(acceptance =>
                acceptance.AppointmentTypeId
                == AppointmentTypeIds.DrugAndAlcoholTesting).Headcount);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Slots/SlotCapacityHeadcountAdjustmentTests.cs — 1/1

<!-- vocabulary-file: {"id":316,"oldPath":"tests/EventBooking.Domain.Tests/Slots/SlotCapacityHeadcountAdjustmentTests.cs","newPath":"tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs","beforeSha":"4da0600456a57374a015f496d362ca53ec1a0e06681b539e37e1bc2ba2331296","afterSha":"6aa700403be4d45ffa18517652992401abc7cb190c765b65a95b147cd10afc11","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class SlotCapacityHeadcountAdjustmentTests
{
    private static SlotCapacity CapacityOf(int totalHeadcount, int occupied = 0)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);

        var capacity = ConfirmedSlot
            .CreateFrom(Guid.NewGuid(), proposal)
            .CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting);

        for (var index = 0; index < occupied; index++)
        {
            capacity.Decrement();
        }

        return capacity;
    }

    [Fact]
    public void IncreasingTheTotalIncreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(12);

        Assert.True(changed);
        Assert.Equal(12, capacity.TotalHeadcount);
        Assert.Equal(6, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void DecreasingTheTotalDecreasesRemainingByTheSameDelta()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(8);

        Assert.True(changed);
        Assert.Equal(8, capacity.TotalHeadcount);
        Assert.Equal(2, capacity.RemainingCapacity);
        Assert.Equal(6, capacity.OccupiedCapacity);
    }

    [Fact]
    public void TheTotalMayEqualOccupiedCapacity()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        capacity.AdjustTotalHeadcount(6);

        Assert.Equal(6, capacity.TotalHeadcount);
        Assert.Equal(0, capacity.RemainingCapacity);
        Assert.False(capacity.HasSpare);
    }

    [Fact]
    public void ATotalBelowOccupiedCapacityIsRejectedWithoutMutation()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(5));

        Assert.Equal(
            "totalHeadcount cannot be lower than occupied capacity.",
            exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ATotalMustRemainPositive(int totalHeadcount)
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 2);

        var exception = Assert.Throws<DomainException>(
            () => capacity.AdjustTotalHeadcount(totalHeadcount));

        Assert.Equal("totalHeadcount must be greater than zero.", exception.Message);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(8, capacity.RemainingCapacity);
    }

    [Fact]
    public void ResubmittingTheCurrentTotalReportsNoChange()
    {
        var capacity = CapacityOf(totalHeadcount: 10, occupied: 6);

        var changed = capacity.AdjustTotalHeadcount(10);

        Assert.False(changed);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }
}
`````
