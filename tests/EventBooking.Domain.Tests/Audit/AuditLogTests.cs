using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Notifications;

namespace EventBooking.Domain.Tests.Audit;

public class AuditLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnEntryRecordsWhoDidWhatToWhatAndWhen()
    {
        var eventId = Guid.NewGuid();
        var actor = Guid.NewGuid().ToString();

        var entry = AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventCancelled,
            ActorType.Staff, actor, Now, "6 bookings voided");

        Assert.Equal("Event", entry.EntityType);
        Assert.Equal(eventId, entry.EntityId);
        Assert.Equal(AuditAction.EventCancelled, entry.Action);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(actor, entry.ActorId);
        Assert.Equal(Now, entry.Timestamp);
        Assert.Equal("6 bookings voided", entry.Details);
    }

    [Fact]
    public void ASystemActorNeedsNoIdentifier()
    {
        var entry = AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteExpired,
            ActorType.System, null, Now, null);

        Assert.Null(entry.ActorId);
        Assert.Null(entry.Details);
    }

    [Fact]
    public void AStaffActorMustBeIdentified()
    {
        var ex = Assert.Throws<DomainException>(
            () => AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated,
                ActorType.Staff, null, Now, null));
        Assert.Equal("actorId must not be blank.", ex.Message);
    }

    [Fact]
    public void AnUnknownEntityTypeIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => AuditLog.Record(
                Guid.NewGuid(), "Sandwich", Guid.NewGuid(), AuditAction.BookingCreated,
                ActorType.System, null, Now, null));
        Assert.Equal("Sandwich is not an audited entity type.", ex.Message);
    }

    [Fact]
    public void AnEmailLogEntryRecordsTheSendAttempt()
    {
        var attendeeId = Guid.NewGuid();

        var entry = EmailLog.Record(
            Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeInvite, Now, EmailStatus.Failed);

        Assert.Equal(attendeeId, entry.AttendeeId);
        Assert.Equal(EmailTemplate.AttendeeInvite, entry.TemplateName);
        Assert.Equal(Now, entry.SentAt);
        Assert.Equal(EmailStatus.Failed, entry.Status);
    }
}
