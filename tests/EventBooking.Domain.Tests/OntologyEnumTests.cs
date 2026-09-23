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
