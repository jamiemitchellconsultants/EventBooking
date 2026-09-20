# 00d — Retire direct event import, edits 14 (Task 3b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs — 1/1

<!-- retirement-file: {"id":45,"file":"tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs","beforeSha":"a3f6989b7f2405a790a9b395f278b0e1de2bf928f9b1c1af78212c7363a1bc7a","afterSha":"2def484beaef89e915bea44c76020095e6377fd1b284349593f41617a71420ad","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Audit;

namespace EventBooking.Domain.Tests.Access;

public class StaffAccessAuditVocabularyTests
{
    [Fact]
    public void StaffAccessActionsAppendWithoutRenumberingExistingActions()
    {
        Assert.Equal(16, (int)AuditAction.EventImported);
        Assert.Equal(17, (int)AuditAction.StaffAccessChanged);
        Assert.Equal(18, (int)AuditAction.StaffAccessRemoved);
    }

    [Fact]
    public void StaffAccessProfileIsAnAuditedEntityType()
    {
        Assert.Equal("StaffAccessProfile", AuditEntityTypes.StaffAccessProfile);
        Assert.Contains(AuditEntityTypes.StaffAccessProfile, AuditEntityTypes.All);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs — 1/1

<!-- retirement-file: {"id":45,"file":"tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs","beforeSha":"a3f6989b7f2405a790a9b395f278b0e1de2bf928f9b1c1af78212c7363a1bc7a","afterSha":"2def484beaef89e915bea44c76020095e6377fd1b284349593f41617a71420ad","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Audit;

namespace EventBooking.Domain.Tests.Access;

public class StaffAccessAuditVocabularyTests
{
    [Fact]
    public void StaffAccessActionsAppendWithoutRenumberingExistingActions()
    {
        Assert.Equal(17, (int)AuditAction.StaffAccessChanged);
    }

    [Fact]
    public void StaffAccessProfileIsAnAuditedEntityType()
    {
        Assert.Equal("StaffAccessProfile", AuditEntityTypes.StaffAccessProfile);
        Assert.Contains(AuditEntityTypes.StaffAccessProfile, AuditEntityTypes.All);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentAuditVocabularyTests.cs — 1/1

<!-- retirement-file: {"id":46,"file":"tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentAuditVocabularyTests.cs","beforeSha":"cef00ad92e7f80dc3a145cefe2e8e51ea14c1a3681e21d7ff59860e5a0d3b21a","afterSha":"ca717b2a705ed8c1673c12ea8df8087ce82b0b9026f1c88e9772d35ae902ea4e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Audit;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies stable audit names and numeric values for appointment delivery.</summary>
public sealed class BookingAppointmentAuditVocabularyTests
{
    /// <summary>Verifies appointment actions append after Issue #71 without renumbering history.</summary>
    [Fact]
    public void AppointmentActionsAppendAfterStaffAccessActions()
    {
        Assert.Equal(18, (int)AuditAction.StaffAccessRemoved);
        Assert.Equal(19, (int)AuditAction.AppointmentCheckedIn);
        Assert.Equal(20, (int)AuditAction.AppointmentCompleted);
        Assert.Equal(21, (int)AuditAction.AppointmentMarkedNoShow);
        Assert.Equal(22, (int)AuditAction.AppointmentStatusCorrected);
    }

    /// <summary>Verifies booking appointments can be correlated through the shared audit logger.</summary>
    [Fact]
    public void BookingAppointmentIsAnAuditedEntityType()
    {
        Assert.Equal("BookingAppointment", AuditEntityTypes.BookingAppointment);
        Assert.Single(
            AuditEntityTypes.All,
            value => value == AuditEntityTypes.BookingAppointment);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentAuditVocabularyTests.cs — 1/1

<!-- retirement-file: {"id":46,"file":"tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentAuditVocabularyTests.cs","beforeSha":"cef00ad92e7f80dc3a145cefe2e8e51ea14c1a3681e21d7ff59860e5a0d3b21a","afterSha":"ca717b2a705ed8c1673c12ea8df8087ce82b0b9026f1c88e9772d35ae902ea4e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Audit;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies stable audit names and numeric values for appointment delivery.</summary>
public sealed class BookingAppointmentAuditVocabularyTests
{
    /// <summary>Verifies appointment actions append after Issue #71 without renumbering history.</summary>
    [Fact]
    public void AppointmentActionsAppendAfterStaffAccessActions()
    {
        Assert.Equal(19, (int)AuditAction.AppointmentCheckedIn);
        Assert.Equal(20, (int)AuditAction.AppointmentCompleted);
        Assert.Equal(21, (int)AuditAction.AppointmentMarkedNoShow);
        Assert.Equal(22, (int)AuditAction.AppointmentStatusCorrected);
    }

    /// <summary>Verifies booking appointments can be correlated through the shared audit logger.</summary>
    [Fact]
    public void BookingAppointmentIsAnAuditedEntityType()
    {
        Assert.Equal("BookingAppointment", AuditEntityTypes.BookingAppointment);
        Assert.Single(
            AuditEntityTypes.All,
            value => value == AuditEntityTypes.BookingAppointment);
    }
}
`````

## before — tests/EventBooking.Domain.Tests/Events/EventImportTests.cs — 1/1

<!-- retirement-file: {"id":47,"file":"tests/EventBooking.Domain.Tests/Events/EventImportTests.cs","beforeSha":"3d5e133abbe6e84d7d84670ea28682eeeddb8bdfc2cdaf96f9afdbeb109c3d15","afterSha":null,"side":"before","part":1,"parts":1} -->

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

## before — tests/EventBooking.Domain.Tests/OntologyEnumTests.cs — 1/1

<!-- retirement-file: {"id":48,"file":"tests/EventBooking.Domain.Tests/OntologyEnumTests.cs","beforeSha":"5f2be5a404ffabf7580e92375f0e0776b9cc98a69f6d22354e7883449d832adc","afterSha":"28118fd7a7e3c09e80a2f2a60af63def11cd8ee67a025ac4b43be74b0a8b01d3","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Domain.Tests/OntologyEnumTests.cs — 1/1

<!-- retirement-file: {"id":48,"file":"tests/EventBooking.Domain.Tests/OntologyEnumTests.cs","beforeSha":"5f2be5a404ffabf7580e92375f0e0776b9cc98a69f6d22354e7883449d832adc","afterSha":"28118fd7a7e3c09e80a2f2a60af63def11cd8ee67a025ac4b43be74b0a8b01d3","side":"after","part":1,"parts":1} -->

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
                "BookingCreated", "BookingCancelled", "CapacityAdjusted",
                "StaffAccessChanged",
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

## before — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs — 1/1

<!-- retirement-file: {"id":49,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","beforeSha":"1370b2e57d6adaa1e0b274aefb71a3656eac1dd42fa82ce67a92f4a023ff27a3","afterSha":"b66e333808c4f3c80ef576739625139ffd58a5e85e394b1f5671bbcfb17384b3","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies workspace projections are scoped and contain only operational data.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies event counts retain recent-past rows and exclude older, cancelled, inactive-booking, and other-type rows.</summary>
    [Fact]
    public async Task EventListContainsOnlyRetainedActiveScopedAppointments()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var future = AddEvent(write, new DateOnly(2026, 9, 8), cancelled: false);
            var recentPast = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            var cancelled = AddEvent(write, new DateOnly(2026, 9, 9), cancelled: true);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.CheckedIn, false);
            AddBooking(write, current, "Priya Shah", "priya@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Other Type", "other@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            AddBooking(write, future, "Future Attendee", "future@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, recentPast, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, cancelled, "Cancelled Event", "event@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Cancelled Booking", "booking@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, true);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal("Drug & Alcohol Testing", result.AppointmentTypeName);
        Assert.Equal(3, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 9, 6), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
        Assert.Equal(1, result.Events[1].Counts.Expected);
        Assert.Equal(1, result.Events[1].Counts.CheckedIn);
        Assert.Equal(0, result.Events[1].Counts.Completed);
        Assert.Equal(0, result.Events[1].Counts.NoShow);
        Assert.Equal(new DateOnly(2026, 9, 8), result.Events[2].Date);
    }

    /// <summary>Verifies event detail returns only same-type active rows ordered for staff use.</summary>
    [Fact]
    public async Task SelectedEventReturnsOnlyMinimumScopedAttendeeRows()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Zara Young", "zara@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Completed, false);
            AddBooking(write, eventItem, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Drug & Alcohol Testing", detail!.AppointmentTypeName);
        Assert.Equal(eventId, detail.EventId);
        Assert.Collection(
            detail.Appointments,
            row =>
            {
                Assert.Equal("Alex Morgan", row.AttendeeName);
                Assert.Equal("alex@example.com", row.AttendeeEmail);
                Assert.Equal(BookingAppointmentStatus.Expected, row.Status);
            },
            row =>
            {
                Assert.Equal("Zara Young", row.AttendeeName);
                Assert.Equal(BookingAppointmentStatus.Completed, row.Status);
                Assert.NotNull(row.CheckedInAt);
                Assert.NotNull(row.OutcomeAt);
            });
    }

    /// <summary>Verifies a event that has no row in trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task SelectedEventOutsideTrustedScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null(await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = Event.CreateImported(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.Cancel();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        var staff = Guid.NewGuid();
        var checkIn = new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero);
        if (status is BookingAppointmentStatus.CheckedIn or BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.CheckedIn, staff, checkIn, true, false);
        }
        if (status == BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.Completed, staff, checkIn.AddHours(1), false, false);
        }
        if (status == BookingAppointmentStatus.NoShow)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.NoShow, staff, checkIn.AddHours(4), false, true);
        }

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs — 1/1

<!-- retirement-file: {"id":49,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs","beforeSha":"1370b2e57d6adaa1e0b274aefb71a3656eac1dd42fa82ce67a92f4a023ff27a3","afterSha":"b66e333808c4f3c80ef576739625139ffd58a5e85e394b1f5671bbcfb17384b3","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies workspace projections are scoped and contain only operational data.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies event counts retain recent-past rows and exclude older, cancelled, inactive-booking, and other-type rows.</summary>
    [Fact]
    public async Task EventListContainsOnlyRetainedActiveScopedAppointments()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var future = AddEvent(write, new DateOnly(2026, 9, 8), cancelled: false);
            var recentPast = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            var cancelled = AddEvent(write, new DateOnly(2026, 9, 9), cancelled: true);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.CheckedIn, false);
            AddBooking(write, current, "Priya Shah", "priya@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Other Type", "other@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            AddBooking(write, future, "Future Attendee", "future@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, recentPast, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, cancelled, "Cancelled Event", "event@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Cancelled Booking", "booking@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, true);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal("Drug & Alcohol Testing", result.AppointmentTypeName);
        Assert.Equal(3, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 9, 6), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
        Assert.Equal(1, result.Events[1].Counts.Expected);
        Assert.Equal(1, result.Events[1].Counts.CheckedIn);
        Assert.Equal(0, result.Events[1].Counts.Completed);
        Assert.Equal(0, result.Events[1].Counts.NoShow);
        Assert.Equal(new DateOnly(2026, 9, 8), result.Events[2].Date);
    }

    /// <summary>Verifies event detail returns only same-type active rows ordered for staff use.</summary>
    [Fact]
    public async Task SelectedEventReturnsOnlyMinimumScopedAttendeeRows()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Zara Young", "zara@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Completed, false);
            AddBooking(write, eventItem, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Drug & Alcohol Testing", detail!.AppointmentTypeName);
        Assert.Equal(eventId, detail.EventId);
        Assert.Collection(
            detail.Appointments,
            row =>
            {
                Assert.Equal("Alex Morgan", row.AttendeeName);
                Assert.Equal("alex@example.com", row.AttendeeEmail);
                Assert.Equal(BookingAppointmentStatus.Expected, row.Status);
            },
            row =>
            {
                Assert.Equal("Zara Young", row.AttendeeName);
                Assert.Equal(BookingAppointmentStatus.Completed, row.Status);
                Assert.NotNull(row.CheckedInAt);
                Assert.NotNull(row.OutcomeAt);
            });
    }

    /// <summary>Verifies a event that has no row in trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task SelectedEventOutsideTrustedScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null(await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.Cancel();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        var staff = Guid.NewGuid();
        var checkIn = new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero);
        if (status is BookingAppointmentStatus.CheckedIn or BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.CheckedIn, staff, checkIn, true, false);
        }
        if (status == BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.Completed, staff, checkIn.AddHours(1), false, false);
        }
        if (status == BookingAppointmentStatus.NoShow)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.NoShow, staff, checkIn.AddHours(4), false, true);
        }

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs — 1/1

<!-- retirement-file: {"id":50,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","beforeSha":"4e692ae9cb5760932b73451f8c93bcdf833baa5cdaa0cbf460df19224d56f5ad","afterSha":"916d9c214947e3f5dbdb7de2979d486665d6385d4d5ac9aaa7ecc3f5df22d023","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the workspace retains recently past events for late outcome recording.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceRecentPastTests(PostgresFixture fixture)
{
    /// <summary>Verifies the event list includes 7-days-past events and excludes 8-days-past events.</summary>
    [Fact]
    public async Task EventListRetainsSevenDaysPastAndExcludesOlderEvents()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var sevenDaysPast = AddEvent(write, new DateOnly(2026, 8, 31), cancelled: false);
            var eightDaysPast = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, sevenDaysPast, "Recent Past", "recent@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eightDaysPast, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal(2, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
    }

    /// <summary>Verifies event detail loads a recently past event inside trusted scope.</summary>
    [Fact]
    public async Task SelectedEventLoadsRecentlyPastEventInScope()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(eventId, detail!.EventId);
        Assert.Equal(new DateOnly(2026, 9, 6), detail.Date);
        Assert.Single(detail.Appointments);
    }

    /// <summary>Verifies event detail returns null for events older than the allowance or outside scope.</summary>
    [Fact]
    public async Task SelectedEventTooOldOrOutOfScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid tooOldId;
        Guid wrongScopeId;
        await using (var write = fixture.NewContext())
        {
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            tooOldId = tooOld.Id;
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            var wrongScope = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            wrongScopeId = wrongScope.Id;
            AddBooking(write, wrongScope, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AppointmentWorkspaceQueries(read, new TestClock());
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, tooOldId, CancellationToken.None));
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, wrongScopeId, CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = Event.CreateImported(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.Cancel();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs — 1/1

<!-- retirement-file: {"id":50,"file":"tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs","beforeSha":"4e692ae9cb5760932b73451f8c93bcdf833baa5cdaa0cbf460df19224d56f5ad","afterSha":"916d9c214947e3f5dbdb7de2979d486665d6385d4d5ac9aaa7ecc3f5df22d023","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the workspace retains recently past events for late outcome recording.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceRecentPastTests(PostgresFixture fixture)
{
    /// <summary>Verifies the event list includes 7-days-past events and excludes 8-days-past events.</summary>
    [Fact]
    public async Task EventListRetainsSevenDaysPastAndExcludesOlderEvents()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddEvent(write, new DateOnly(2026, 9, 7), cancelled: false);
            var sevenDaysPast = AddEvent(write, new DateOnly(2026, 8, 31), cancelled: false);
            var eightDaysPast = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, sevenDaysPast, "Recent Past", "recent@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eightDaysPast, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListEventsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal(2, result.Events.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Events[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Events[1].Date);
    }

    /// <summary>Verifies event detail loads a recently past event inside trusted scope.</summary>
    [Fact]
    public async Task SelectedEventLoadsRecentlyPastEventInScope()
    {
        await fixture.ResetAsync();
        Guid eventId;
        await using (var write = fixture.NewContext())
        {
            var eventItem = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            eventId = eventItem.Id;
            AddBooking(write, eventItem, "Past Attendee", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            eventId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(eventId, detail!.EventId);
        Assert.Equal(new DateOnly(2026, 9, 6), detail.Date);
        Assert.Single(detail.Appointments);
    }

    /// <summary>Verifies event detail returns null for events older than the allowance or outside scope.</summary>
    [Fact]
    public async Task SelectedEventTooOldOrOutOfScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid tooOldId;
        Guid wrongScopeId;
        await using (var write = fixture.NewContext())
        {
            var tooOld = AddEvent(write, new DateOnly(2026, 8, 30), cancelled: false);
            tooOldId = tooOld.Id;
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            var wrongScope = AddEvent(write, new DateOnly(2026, 9, 6), cancelled: false);
            wrongScopeId = wrongScope.Id;
            AddBooking(write, wrongScope, "Medical Attendee", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AppointmentWorkspaceQueries(read, new TestClock());
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, tooOldId, CancellationToken.None));
        Assert.Null(await queries.GetEventAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, wrongScopeId, CancellationToken.None));
    }

    private static Event AddEvent(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(date, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            eventItem.Cancel();
        }

        context.Events.Add(eventItem);
        return eventItem;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        Event eventItem,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.AttendeeGroups.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{attendee.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{attendee.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);

        context.Attendees.Add(attendee);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning transitional-location today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtTransitionalLocation => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtTransitionalLocation => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
    }
}
`````
