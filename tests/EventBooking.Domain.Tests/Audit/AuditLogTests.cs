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
        var slotId = Guid.NewGuid();
        var actor = Guid.NewGuid().ToString();

        var entry = AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotCancelled,
            ActorType.Staff, actor, Now, "6 bookings voided");

        Assert.Equal("ConfirmedSlot", entry.EntityType);
        Assert.Equal(slotId, entry.EntityId);
        Assert.Equal(AuditAction.SlotCancelled, entry.Action);
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
        var candidateId = Guid.NewGuid();

        var entry = EmailLog.Record(
            Guid.NewGuid(), candidateId, EmailTemplate.CandidateInvite, Now, EmailStatus.Failed);

        Assert.Equal(candidateId, entry.CandidateId);
        Assert.Equal(EmailTemplate.CandidateInvite, entry.TemplateName);
        Assert.Equal(Now, entry.SentAt);
        Assert.Equal(EmailStatus.Failed, entry.Status);
    }
}
