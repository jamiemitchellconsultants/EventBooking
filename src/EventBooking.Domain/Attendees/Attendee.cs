using System.Net.Mail;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Attendees;

/// <summary>A person invited to attend appointments, whose requirements derive from one attendee group.</summary>
public sealed class Attendee
{
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
    public static Attendee Create(Guid id, string? name, string? email, AttendeeGroup attendeeGroup)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var attendee = new Attendee
        {
            Id = id,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = AttendeeStatus.NotYetInvited,
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

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

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

    /// <summary>Moves the attendee to Invited from a pre-booking lifecycle state.</summary>
    public void MarkInvited() => TransitionTo(
        AttendeeStatus.Invited,
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.Invited,
        AttendeeStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves the attendee to AwaitingAvailability from a pre-booking lifecycle state.</summary>
    public void MarkAwaitingAvailability() => TransitionTo(
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.Invited,
        AttendeeStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves an invited attendee to Booked.</summary>
    public void MarkBooked() => TransitionTo(AttendeeStatus.Booked, AttendeeStatus.Invited);

    /// <summary>Moves an invited attendee to NoResponseNeedsFollowUp.</summary>
    public void MarkNoResponse() => TransitionTo(
        AttendeeStatus.NoResponseNeedsFollowUp,
        AttendeeStatus.Invited);

    /// <summary>Returns an invited or booked attendee to NotYetInvited.</summary>
    public void ResetToNotYetInvited() => TransitionTo(
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.Invited,
        AttendeeStatus.Booked);

    /// <summary>Resets an unbooked Attendee after a derived requirement-set change.</summary>
    public void ResetAfterRequirementChange()
    {
        if (Status is AttendeeStatus.NotYetInvited)
        {
            return;
        }

        TransitionTo(
            AttendeeStatus.NotYetInvited,
            AttendeeStatus.AwaitingAvailability,
            AttendeeStatus.NoResponseNeedsFollowUp,
            AttendeeStatus.Invited);
    }

    private void TransitionTo(AttendeeStatus target, params AttendeeStatus[] allowedOrigins)
    {
        Guard.Against(
            !allowedOrigins.Contains(Status),
            $"A attendee cannot move from {Status} to {target}.");

        Status = target;
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
