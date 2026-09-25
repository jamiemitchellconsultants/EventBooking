using System.Net.Mail;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.SelfRegistrations;

/// <summary>One anonymous request to join an event, awaiting confirmation through its token.</summary>
public sealed class PendingRegistration
{
    private PendingRegistration()
    {
        Name = string.Empty;
        Email = string.Empty;
    }

    /// <summary>Gets the stable request identifier the confirmation token names.</summary>
    public Guid RequestId { get; private set; }

    /// <summary>Gets the owning Event Group identifier.</summary>
    public Guid EventGroupId { get; private set; }

    /// <summary>Gets the requested Event identifier.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Gets the requested Attendee Group identifier.</summary>
    public Guid AttendeeGroupId { get; private set; }

    /// <summary>Gets the requester's name as submitted.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the requester's normalized email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the lifecycle status.</summary>
    public SelfRegistrationStatus Status { get; private set; }

    /// <summary>Gets when the request was submitted.</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>Gets when the pending request stops being confirmable.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets when the request was confirmed, once it is.</summary>
    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Gets when the request reached its terminal state, once it has.</summary>
    public DateTimeOffset? TerminalAt { get; private set; }

    /// <summary>Gets the token version the confirmation link was issued against.</summary>
    public int TokenVersion { get; private set; }

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Creates a pending request expiring after the configured hours.</summary>
    /// <param name="requestId">The new request identifier.</param>
    /// <param name="eventGroupId">The owning event group.</param>
    /// <param name="eventId">The requested event.</param>
    /// <param name="attendeeGroupId">The requested attendee group.</param>
    /// <param name="name">The requester's name.</param>
    /// <param name="email">The requester's email.</param>
    /// <param name="now">The submission instant.</param>
    /// <param name="expiryHours">How long the request stays confirmable.</param>
    public static PendingRegistration Create(
        Guid requestId,
        Guid eventGroupId,
        Guid eventId,
        Guid attendeeGroupId,
        string? name,
        string? email,
        DateTimeOffset now,
        int expiryHours)
    {
        Guard.Against(requestId == Guid.Empty, "requestId must not be empty.");
        Guard.Against(eventGroupId == Guid.Empty, "eventGroupId must not be empty.");
        Guard.Against(eventId == Guid.Empty, "eventId must not be empty.");
        Guard.Against(attendeeGroupId == Guid.Empty, "attendeeGroupId must not be empty.");
        Guard.Against(expiryHours < 1, "expiryHours must be positive.");

        return new PendingRegistration
        {
            RequestId = requestId,
            EventGroupId = eventGroupId,
            EventId = eventId,
            AttendeeGroupId = attendeeGroupId,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = SelfRegistrationStatus.Pending,
            SubmittedAt = now,
            ExpiresAt = now.AddHours(expiryHours),
            TokenVersion = 1,
            Version = 1,
        };
    }

    /// <summary>Confirms a pending request whose token version still matches.</summary>
    /// <param name="tokenVersion">The version the confirmation link names.</param>
    /// <param name="now">The confirmation instant.</param>
    public void Confirm(int tokenVersion, DateTimeOffset now)
    {
        Guard.Against(Status != SelfRegistrationStatus.Pending,
            "Only a pending request can be confirmed.");
        Guard.Against(tokenVersion != TokenVersion,
            "The confirmation link is no longer current.");
        ConfirmedAt = now;
        TerminalAt = now;
        Status = SelfRegistrationStatus.Confirmed;
        Version++;
    }

    /// <summary>Expires a pending request past its confirmation window.</summary>
    /// <param name="now">The current instant.</param>
    /// <returns>Whether the request transitioned.</returns>
    public bool Expire(DateTimeOffset now)
    {
        if (Status != SelfRegistrationStatus.Pending || now < ExpiresAt) return false;
        Status = SelfRegistrationStatus.Expired;
        TerminalAt = now;
        Version++;
        return true;
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
