using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Bookings;

/// <summary>Defines booking for the current use case.</summary>
public sealed class Booking
{
    private Booking()
    {
        // Required by the persistence layer's constructor binding.
        ManageTokenHash = string.Empty;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines candidate id for the current use case.</summary>
    public Guid CandidateId { get; private set; }

    /// <summary>Defines confirmed slot id for the current use case.</summary>
    public Guid ConfirmedSlotId { get; private set; }

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

    /// <summary>Hash of the single-use token behind the cancel/reschedule link.</summary>
    public string ManageTokenHash { get; private set; }

    /// <summary>Replaces the persisted management-link hash after issuing a fresh raw token.</summary>
    /// <param name="manageTokenHash">The manage token hash.</param>
    public void RotateManageTokenHash(string? manageTokenHash)
    {
        Guard.Against(Status != BookingStatus.Active, "Only an active booking token can be rotated.");
        ManageTokenHash = Guard.NotBlank(manageTokenHash, "manageTokenHash");
    }

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="invite">The invite.</param>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="manageTokenHash">The manage token hash.</param>
    /// <param name="createdAt">The created at.</param>
    public static Booking Create(
        Guid id,
        Invite invite,
        Guid confirmedSlotId,
        string? manageTokenHash,
        DateTimeOffset createdAt)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(invite is null, "invite must be supplied.");
        Guard.Against(invite!.Status != InviteStatus.Pending, "This invite can no longer be used.");
        Guard.Against(
            !invite.Offers(confirmedSlotId),
            "The chosen slot is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            CandidateId = invite.CandidateId,
            ConfirmedSlotId = confirmedSlotId,
            InviteId = invite.Id,
            CreatedAt = createdAt,
            Status = BookingStatus.Active,
            ManageTokenHash = Guard.NotBlank(manageTokenHash, "manageTokenHash"),
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
    /// <param name="confirmedSlotId">The recovery slot offered by the invite.</param>
    /// <param name="manageTokenHash">The management-link hash for the recovery booking.</param>
    /// <param name="createdAt">When the recovery booking is created.</param>
    /// <returns>An active recovery Booking pointing at the original root.</returns>
    public static Booking CreateRecovery(
        Guid id,
        Invite recoveryInvite,
        Booking originalBooking,
        Guid confirmedSlotId,
        string? manageTokenHash,
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
            recoveryInvite.CandidateId != originalBooking.CandidateId,
            "The recovery invite must belong to the original booking candidate.");
        Guard.Against(
            recoveryInvite.Status != InviteStatus.Pending,
            "This invite can no longer be used.");
        Guard.Against(
            !recoveryInvite.Offers(confirmedSlotId),
            "The chosen slot is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            CandidateId = originalBooking.CandidateId,
            ConfirmedSlotId = confirmedSlotId,
            InviteId = recoveryInvite.Id,
            CreatedAt = createdAt,
            Status = BookingStatus.Active,
            RecoveryOfBookingId = originalBooking.Id,
            ManageTokenHash = Guard.NotBlank(manageTokenHash, "manageTokenHash"),
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
