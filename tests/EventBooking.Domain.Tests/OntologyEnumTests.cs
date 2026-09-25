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
        Assert.Equal(
            new[] { "Staff", "AttendeeToken", "System", "Anonymous" },
            Enum.GetNames<ActorType>());
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
                "LocationCreated", "LocationUpdated",
                "AppointmentTypeCreated", "AppointmentTypeUpdated",
                "AttendeeGroupCreated", "AttendeeGroupUpdated",
                "SystemSettingsChanged",
                "EventGroupCreated", "EventGroupUpdated", "EventGroupEventChanged",
                "SelfRegistrationRequested", "SelfRegistrationConfirmed", "SelfRegistrationExpired",
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
                "AttendeeReinvite", "SelfRegistrationConfirmation",
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
