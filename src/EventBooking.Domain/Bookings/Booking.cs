using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Bookings;

/// <summary>Defines booking for the current use case.</summary>
public sealed class Booking
{
    /// <summary>The version every new booking's manage link is signed against.</summary>
    public const int InitialManageTokenVersion = 1;

    private Booking()
    {
        // Required by the persistence layer's constructor binding.
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines attendee id for the current use case.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Defines invite id for the current use case.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Defines created at for the current use case.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public BookingStatus Status { get; private set; } = BookingStatus.Active;

    /// <summary>Gets the original active Booking ID, or null for the journey root.</summary>
    public Guid? RecoveryOfBookingId { get; private set; }

    /// <summary>Gets whether this Booking is the original journey root.</summary>
    public bool IsOriginal => RecoveryOfBookingId is null;

    /// <summary>
    /// The version the cancel/reschedule link is signed against. Only the counter is stored; the
    /// token is reproduced from it, which is how the confirmation page and the confirmation email
    /// carry the same link (design 06).
    /// </summary>
    public int ManageTokenVersion { get; private set; } = InitialManageTokenVersion;

    /// <summary>Revokes every outstanding manage link for this booking by moving to the next version.</summary>
    public void RotateManageToken()
    {
        Guard.Against(Status != BookingStatus.Active, "Only an active booking token can be rotated.");
        ManageTokenVersion++;
    }

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="invite">The invite.</param>
    /// <param name="eventId">The event id.</param>
    /// <param name="createdAt">The created at.</param>
    public static Booking Create(
        Guid id,
        Invite invite,
        Guid eventId,
        DateTimeOffset createdAt)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(invite is null, "invite must be supplied.");
        Guard.Against(invite!.Status != InviteStatus.Pending, "This invite can no longer be used.");
        Guard.Against(
            !invite.Offers(eventId),
            "The chosen eventItem is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            AttendeeId = invite.AttendeeId,
            EventId = eventId,
            InviteId = invite.Id,
            CreatedAt = createdAt,
            Status = BookingStatus.Active,
        };
    }

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == BookingStatus.Cancelled, "This booking has already been cancelled.");
        Status = BookingStatus.Cancelled;
    }

    /// <summary>Creates a recovery Booking directly linked to the original Booking.</summary>
    /// <param name="id">The stable recovery booking identifier.</param>
    /// <param name="recoveryInvite">The pending recovery invite issued for the original Booking.</param>
    /// <param name="originalBooking">The active original journey root being recovered.</param>
    /// <param name="eventId">The recovery event offered by the invite.</param>
    /// <param name="createdAt">When the recovery booking is created.</param>
    /// <returns>An active recovery Booking pointing at the original root.</returns>
    public static Booking CreateRecovery(
        Guid id,
        Invite recoveryInvite,
        Booking originalBooking,
        Guid eventId,
        DateTimeOffset createdAt)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(recoveryInvite is null, "recoveryInvite must be supplied.");
        Guard.Against(originalBooking is null, "originalBooking must be supplied.");
        Guard.Against(!originalBooking!.IsOriginal, "A recovery booking cannot point at another recovery.");
        Guard.Against(
            originalBooking.Status != BookingStatus.Active,
            "A recovery booking requires an active original booking.");
        Guard.Against(
            recoveryInvite!.RecoveryOfBookingId != originalBooking.Id,
            "The recovery invite must point at the supplied original booking.");
        Guard.Against(
            recoveryInvite.AttendeeId != originalBooking.AttendeeId,
            "The recovery invite must belong to the original booking attendee.");
        Guard.Against(
            recoveryInvite.Status != InviteStatus.Pending,
            "This invite can no longer be used.");
        Guard.Against(
            !recoveryInvite.Offers(eventId),
            "The chosen eventItem is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            AttendeeId = originalBooking.AttendeeId,
            EventId = eventId,
            InviteId = recoveryInvite.Id,
            CreatedAt = createdAt,
            Status = BookingStatus.Active,
            RecoveryOfBookingId = originalBooking.Id,
        };
    }

    /// <summary>Concludes an Active recovery Booking after all of its appointments are terminal.</summary>
    public void Conclude()
    {
        Guard.Against(IsOriginal, "Only a recovery booking can conclude.");
        Guard.Against(Status != BookingStatus.Active, "Only an active recovery booking can conclude.");
        Status = BookingStatus.Concluded;
    }

    /// <summary>Reopens a Concluded recovery Booking after an allowed outcome correction.</summary>
    public void Reopen()
    {
        Guard.Against(IsOriginal, "Only a recovery booking can reopen.");
        Guard.Against(Status != BookingStatus.Concluded, "Only a concluded recovery booking can reopen.");
        Status = BookingStatus.Active;
    }
}
