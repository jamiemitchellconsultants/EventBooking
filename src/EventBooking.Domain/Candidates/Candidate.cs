using System.Net.Mail;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Candidates;

/// <summary>A person invited to attend appointments, whose requirements derive from one employee group.</summary>
public sealed class Candidate
{
    private readonly List<CandidateRequirement> _requirements = [];

    private Candidate()
    {
        // Required by the persistence layer's constructor binding.
        Name = string.Empty;
        Email = string.Empty;
    }

    /// <summary>Gets the candidate identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the candidate display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the normalized candidate email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the assigned Employee Group, or null during legacy reconciliation.</summary>
    public Guid? EmployeeGroupId { get; private set; }

    /// <summary>Gets where the candidate sits in the invite and booking lifecycle.</summary>
    public CandidateStatus Status { get; private set; } = CandidateStatus.NotYetInvited;

    /// <summary>Gets the materialized appointment types the candidate currently requires.</summary>
    public IReadOnlyList<CandidateRequirement> Requirements => _requirements;

    /// <summary>Gets the identifiers of the appointment types the candidate currently requires.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).ToList();

    /// <summary>Creates a Candidate and derives every requirement from the active mapped group.</summary>
    /// <param name="id">The id.</param>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    /// <param name="employeeGroup">The employee group.</param>
    public static Candidate Create(Guid id, string? name, string? email, EmployeeGroup employeeGroup)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var candidate = new Candidate
        {
            Id = id,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = CandidateStatus.NotYetInvited,
        };

        candidate.AssignEmployeeGroup(employeeGroup);

        return candidate;
    }

    /// <summary>Replaces the candidate name and email after validating both.</summary>
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
    /// <param name="employeeGroup">The employee group.</param>
    public bool AssignEmployeeGroup(EmployeeGroup employeeGroup)
    {
        ArgumentNullException.ThrowIfNull(employeeGroup);
        Guard.Against(!employeeGroup.IsActive, "An inactive employee group cannot be assignment authority.");

        var mapping = employeeGroup.RequiredAppointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An employee group must map at least one appointment type.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        EmployeeGroupId = employeeGroup.Id;

        if (_requirements.Select(r => r.AppointmentTypeId).Order().SequenceEqual(mapping.Order()))
        {
            return false;
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping.Order())
        {
            _requirements.Add(CandidateRequirement.For(Id, appointmentTypeId));
        }

        return true;
    }

    /// <summary>Moves the candidate to Invited from a pre-booking lifecycle state.</summary>
    public void MarkInvited() => TransitionTo(
        CandidateStatus.Invited,
        CandidateStatus.NotYetInvited,
        CandidateStatus.AwaitingAvailability,
        CandidateStatus.Invited,
        CandidateStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves the candidate to AwaitingAvailability from a pre-booking lifecycle state.</summary>
    public void MarkAwaitingAvailability() => TransitionTo(
        CandidateStatus.AwaitingAvailability,
        CandidateStatus.NotYetInvited,
        CandidateStatus.AwaitingAvailability,
        CandidateStatus.Invited,
        CandidateStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves an invited candidate to Booked.</summary>
    public void MarkBooked() => TransitionTo(CandidateStatus.Booked, CandidateStatus.Invited);

    /// <summary>Moves an invited candidate to NoResponseNeedsFollowUp.</summary>
    public void MarkNoResponse() => TransitionTo(
        CandidateStatus.NoResponseNeedsFollowUp,
        CandidateStatus.Invited);

    /// <summary>Returns an invited or booked candidate to NotYetInvited.</summary>
    public void ResetToNotYetInvited() => TransitionTo(
        CandidateStatus.NotYetInvited,
        CandidateStatus.Invited,
        CandidateStatus.Booked);

    /// <summary>Resets an unbooked Candidate after a derived requirement-set change.</summary>
    public void ResetAfterRequirementChange()
    {
        if (Status is CandidateStatus.NotYetInvited)
        {
            return;
        }

        TransitionTo(
            CandidateStatus.NotYetInvited,
            CandidateStatus.AwaitingAvailability,
            CandidateStatus.NoResponseNeedsFollowUp,
            CandidateStatus.Invited);
    }

    private void TransitionTo(CandidateStatus target, params CandidateStatus[] allowedOrigins)
    {
        Guard.Against(
            !allowedOrigins.Contains(Status),
            $"A candidate cannot move from {Status} to {target}.");

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
