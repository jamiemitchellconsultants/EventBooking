using System.Net.Mail;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Attendees;

/// <summary>A person invited to attend appointments, whose requirements derive from one attendee group.</summary>
public sealed class Attendee
{
    /// <summary>
    /// Every legal move, one entry per row of design 01's table. Anything absent is a defect
    /// (decision D15), so the set is the rule rather than a comment beside it.
    /// </summary>
    private static readonly HashSet<(AttendeeStatus From, AttendeeStatus To)> Legal =
    [
        (AttendeeStatus.NotYetInvited, AttendeeStatus.Invited),
        (AttendeeStatus.NotYetInvited, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.AwaitingAvailability, AttendeeStatus.Invited),
        (AttendeeStatus.AwaitingAvailability, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.Invited, AttendeeStatus.Invited),
        (AttendeeStatus.Invited, AttendeeStatus.Booked),
        (AttendeeStatus.Invited, AttendeeStatus.NoResponseNeedsFollowUp),
        (AttendeeStatus.Invited, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.Invited),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.NoResponseNeedsFollowUp, AttendeeStatus.NotYetInvited),
        (AttendeeStatus.Booked, AttendeeStatus.Invited),
        (AttendeeStatus.Booked, AttendeeStatus.AwaitingAvailability),
        (AttendeeStatus.Booked, AttendeeStatus.NotYetInvited),
    ];

    private readonly List<AttendeeRequirement> _requirements = [];

    private Attendee()
    {
        // Required by the persistence layer's constructor binding.
        Name = string.Empty;
        Email = string.Empty;
    }

    /// <summary>Gets the attendee identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the attendee display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the normalized attendee email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the required assigned Attendee Group identifier.</summary>
    public Guid AttendeeGroupId { get; private set; }

    /// <summary>Gets where the attendee sits in the invite and booking lifecycle.</summary>
    public AttendeeStatus Status { get; private set; } = AttendeeStatus.NotYetInvited;

    /// <summary>Gets when the status was last written. Stamped on creation and on every change.</summary>
    public DateTimeOffset StatusChangedAt { get; private set; }

    /// <summary>Gets the materialized appointment types the attendee currently requires.</summary>
    public IReadOnlyList<AttendeeRequirement> Requirements => _requirements;

    /// <summary>Gets the identifiers of the appointment types the attendee currently requires.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).ToList();

    /// <summary>Creates a Attendee and derives every requirement from the active mapped group.</summary>
    /// <param name="id">The id.</param>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    /// <param name="attendeeGroup">The attendee group.</param>
    /// <param name="now">The instant the initial status is stamped with.</param>
    public static Attendee Create(
        Guid id, string? name, string? email, AttendeeGroup attendeeGroup, DateTimeOffset now)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var attendee = new Attendee
        {
            Id = id,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = AttendeeStatus.NotYetInvited,
            StatusChangedAt = now,
        };

        attendee.AssignAttendeeGroup(attendeeGroup);

        return attendee;
    }

    /// <summary>Replaces the attendee name and email after validating both.</summary>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    public void UpdateDetails(string? name, string? email)
    {
        // Validate both before mutating either.
        var newName = Guard.NotBlank(name, "name");
        var newEmail = NormaliseEmail(email);

        Name = newName;
        Email = newEmail;
    }

    /// <summary>Assigns a group and derives its complete set; returns whether that set changed.</summary>
    /// <param name="attendeeGroup">The attendee group.</param>
    public bool AssignAttendeeGroup(AttendeeGroup attendeeGroup)
    {
        ArgumentNullException.ThrowIfNull(attendeeGroup);
        Guard.Against(!attendeeGroup.IsActive, "An inactive attendee group cannot be assignment authority.");

        var mapping = attendeeGroup.RequiredAppointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");

        AttendeeGroupId = attendeeGroup.Id;

        if (_requirements.Select(r => r.AppointmentTypeId).Order().SequenceEqual(mapping.Order()))
        {
            return false;
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping.Order())
        {
            _requirements.Add(AttendeeRequirement.For(Id, appointmentTypeId));
        }

        return true;
    }

    /// <summary>Whether design 01's table lists this move. Pure, so a caller can ask before acting.</summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The wanted status.</param>
    public static bool IsLegalTransition(AttendeeStatus from, AttendeeStatus to) =>
        Legal.Contains((from, to));

    /// <summary>Moves the attendee to Invited, from any status the table allows.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkInvited(DateTimeOffset now) => TransitionTo(AttendeeStatus.Invited, now);

    /// <summary>Moves the attendee to AwaitingAvailability, from any status the table allows.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkAwaitingAvailability(DateTimeOffset now) =>
        TransitionTo(AttendeeStatus.AwaitingAvailability, now);

    /// <summary>Moves an invited attendee to Booked.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkBooked(DateTimeOffset now) => TransitionTo(AttendeeStatus.Booked, now);

    /// <summary>Moves an invited attendee to NoResponseNeedsFollowUp.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void MarkNoResponse(DateTimeOffset now) =>
        TransitionTo(AttendeeStatus.NoResponseNeedsFollowUp, now);

    /// <summary>Returns the attendee to NotYetInvited, which the table allows from anywhere.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void ResetToNotYetInvited(DateTimeOffset now) =>
        TransitionTo(AttendeeStatus.NotYetInvited, now);

    /// <summary>Resets an unbooked Attendee after a derived requirement-set change.</summary>
    /// <param name="now">The instant to stamp.</param>
    public void ResetAfterRequirementChange(DateTimeOffset now)
    {
        if (Status is AttendeeStatus.NotYetInvited)
        {
            return;
        }

        // The table allows Booked to NotYetInvited, but only when the attendee themselves cancels.
        // A group reassignment must not silently discard a booking, so it is refused here.
        Guard.Against(
            Status == AttendeeStatus.Booked,
            $"A attendee cannot move from {Status} to {AttendeeStatus.NotYetInvited}.");

        TransitionTo(AttendeeStatus.NotYetInvited, now);
    }

    private void TransitionTo(AttendeeStatus target, DateTimeOffset now)
    {
        Guard.Against(
            !IsLegalTransition(Status, target),
            $"A attendee cannot move from {Status} to {target}.");

        Status = target;
        StatusChangedAt = now;
    }

    private static string NormaliseEmail(string? email)
    {
        var trimmed = email?.Trim() ?? string.Empty;

        var valid =
            trimmed.Length > 0
            && !trimmed.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(trimmed, out var parsed)
            && parsed!.Host.Contains('.');

        Guard.Against(!valid, "email is not a valid email address.");

        return trimmed.ToLowerInvariant();
    }
}
