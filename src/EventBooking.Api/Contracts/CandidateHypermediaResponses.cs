using System.Text.Json.Serialization;
using EventBooking.Application.Bookings;
using EventBooking.Application.Candidates;
using EventBooking.Domain.Candidates;

namespace EventBooking.Api.Contracts;

/// <summary>Candidate list data plus safe follow-up operations.</summary>
public sealed record CandidateResourceResponse(
    /// <summary>Gets the stable Candidate identifier.</summary>
    Guid CandidateId,
    /// <summary>Gets the Candidate full name.</summary>
    string Name,
    /// <summary>Gets the Candidate contact email address.</summary>
    string Email,
    /// <summary>Gets the assigned Employee Group identifier, when assigned.</summary>
    Guid? EmployeeGroupId,
    /// <summary>Gets the canonical Employee Group code, when assigned.</summary>
    string? EmployeeGroupCode,
    /// <summary>Gets the Employee Group display name, when assigned.</summary>
    string? EmployeeGroupName,
    /// <summary>Gets whether the Candidate needs legacy Employee Group reconciliation.</summary>
    bool RequiresEmployeeGroupReconciliation,
    /// <summary>Gets the Appointment Types currently required by the Candidate.</summary>
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes,
    /// <summary>Gets where the Candidate sits in the invite and booking lifecycle.</summary>
    CandidateStatus Status,
    /// <summary>Gets the Coordinator-facing wording for the Candidate status.</summary>
    string StatusDisplay,
    /// <summary>Gets the safe follow-up operations for the Candidate.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one list item into its hypermedia resource.</summary>
    /// <param name="item">The application list item to project.</param>
    /// <returns>The API resource with Candidate workflow links.</returns>
    public static CandidateResourceResponse From(CandidateListItem item) =>
        new(item.CandidateId, item.Name, item.Email, item.EmployeeGroupId,
            item.EmployeeGroupCode, item.EmployeeGroupName,
            item.RequiresEmployeeGroupReconciliation, item.RequiredAppointmentTypes,
            item.Status, item.StatusDisplay,
            CandidateLinks.ForCandidate(item.CandidateId, item.Status));
}

/// <summary>Coordinator-facing delivery outcome for one started recovery invite.</summary>
public sealed record StartRecoveryResourceResponse(
    /// <summary>Gets the new recovery Invite identifier, or empty when awaiting availability.</summary>
    Guid InviteId,
    /// <summary>Gets the recoverable snapshot offered, or awaiting availability.</summary>
    IReadOnlyList<Guid> AppointmentTypeIds,
    /// <summary>Gets whether the post-commit provider attempt completed successfully.</summary>
    bool EmailSent,
    /// <summary>Gets the safe follow-up operations for the recovery invite.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Coordinator-facing outcome of cancelling one candidate booking.</summary>
public sealed record CancelCandidateBookingResourceResponse(
    /// <summary>Gets whether a replacement Invite was created for the Candidate.</summary>
    bool Reinvited,
    /// <summary>Gets the explicit replacement-invite creation state.</summary>
    bool InviteCreated,
    /// <summary>Gets the provider outcome, or Unavailable when no replacement Invite exists.</summary>
    string? DeliveryStatus,
    /// <summary>Gets the durable replacement delivery identifier, when one was staged.</summary>
    Guid? DeliveryId,
    /// <summary>Gets the safe follow-up operations for the Candidate.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One active Booking a Coordinator may cancel; carries no management token.</summary>
public sealed record CandidateBookingResourceResponse(
    /// <summary>Gets the Booking identifier used to target a cancellation.</summary>
    Guid BookingId,
    /// <summary>Gets whether this is the original Booking rather than an active recovery Booking.</summary>
    bool IsOriginal,
    /// <summary>Gets the date of the Confirmed Slot window the Booking holds.</summary>
    DateOnly SlotDate,
    /// <summary>Gets the start of the Confirmed Slot window the Booking holds.</summary>
    TimeOnly SlotStartTime,
    /// <summary>Gets the end of the Confirmed Slot window the Booking holds.</summary>
    TimeOnly SlotEndTime,
    /// <summary>Gets the safe follow-up operations for the Booking.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one booking summary into its hypermedia resource.</summary>
    /// <param name="candidateId">The owning Candidate identifier.</param>
    /// <param name="row">The application booking summary to project.</param>
    /// <returns>The API resource with the cancellation link.</returns>
    public static CandidateBookingResourceResponse From(Guid candidateId, CandidateBookingSummary row) =>
        new(row.BookingId, row.IsOriginal, row.SlotDate, row.SlotStartTime, row.SlotEndTime,
            CandidateLinks.ForBooking(candidateId, row.BookingId, row.IsOriginal));
}

/// <summary>Minimum canonical detail for one incomplete appointment type.</summary>
public sealed record OutstandingAppointmentTypeResourceResponse(
    /// <summary>Gets the canonical Appointment Type code.</summary>
    string Code,
    /// <summary>Gets the canonical Appointment Type name.</summary>
    string Name,
    /// <summary>Gets whether recovery can currently be started for this type.</summary>
    bool IsRecoverable);

/// <summary>Coordinator-facing readiness for one candidate.</summary>
public sealed record CandidateReadinessResourceResponse(
    /// <summary>Gets the stable Candidate identifier.</summary>
    Guid CandidateId,
    /// <summary>Gets the stable machine-readable readiness reason.</summary>
    string Code,
    /// <summary>Gets the Coordinator-facing explanation.</summary>
    string Display,
    /// <summary>Gets minimum canonical detail for incomplete current Appointment Types.</summary>
    IReadOnlyList<OutstandingAppointmentTypeResourceResponse> OutstandingAppointmentTypes,
    /// <summary>Gets the safe follow-up operations for the Candidate.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Candidate-facing invite options plus the confirm affordance.</summary>
public sealed record InviteResourceResponse(
    /// <summary>Gets the Invite identifier shown to the Candidate.</summary>
    Guid InviteId,
    /// <summary>Gets the Candidate name shown on the invite.</summary>
    string CandidateName,
    /// <summary>Gets the Appointment Type names offered by the invite.</summary>
    IReadOnlyList<string> AppointmentTypeNames,
    /// <summary>Gets the usable future appointment options.</summary>
    IReadOnlyList<InviteOptionView> Options,
    /// <summary>Gets whether the invite is a missed-appointment recovery.</summary>
    bool IsRecovery,
    /// <summary>Gets the safe follow-up operations for the invite token.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one invite view into its hypermedia resource.</summary>
    /// <param name="view">The application invite view to project.</param>
    /// <param name="token">The raw invite token used only to build the confirm link.</param>
    /// <returns>The API resource with the confirm link.</returns>
    public static InviteResourceResponse From(InviteView view, string token) =>
        new(view.InviteId, view.CandidateName, view.AppointmentTypeNames,
            view.Options, view.IsRecovery, CandidateLinks.ForInviteToken(token));
}

/// <summary>Candidate-facing managed booking plus the cancel affordance.</summary>
public sealed record ManagedBookingResourceResponse(
    /// <summary>Gets the date of the Confirmed Slot window the Booking holds.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the Confirmed Slot window the Booking holds.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the Confirmed Slot window the Booking holds.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the Candidate-facing window description.</summary>
    string Display,
    /// <summary>Gets the Candidate name shown on the booking.</summary>
    string CandidateName,
    /// <summary>Gets the safe follow-up operations for the manage token.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one booking view into its hypermedia resource.</summary>
    /// <param name="view">The application booking view to project.</param>
    /// <param name="token">The raw manage token used only to build the cancel link.</param>
    /// <returns>The API resource with the cancel link.</returns>
    public static ManagedBookingResourceResponse From(BookingView view, string token) =>
        new(view.Date, view.StartTime, view.EndTime, view.Display,
            view.CandidateName, CandidateLinks.ForManageToken(token));
}

/// <summary>Builds Candidate relations from stable operation IDs.</summary>
public static class CandidateLinks
{
    /// <summary>Builds the workflow links for one Candidate list resource.</summary>
    /// <param name="candidateId">The stable Candidate identifier.</param>
    /// <param name="status">Where the Candidate sits in the invite and booking lifecycle.</param>
    /// <returns>The safe follow-up operations keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForCandidate(Guid candidateId, CandidateStatus status)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["bookings"] = new($"/api/candidates/{candidateId}/bookings", "GET", "listCandidateBookings"),
            ["readiness"] = new($"/api/candidates/{candidateId}/readiness", "GET", "getCandidateReadiness"),
            ["audit"] = new($"/api/audit/candidate/{candidateId}", "GET", "getCandidateAuditHistory"),
            ["update"] = new($"/api/candidates/{candidateId}", "PUT", "updateCandidate"),
            ["delete"] = new($"/api/candidates/{candidateId}", "DELETE", "deleteCandidate"),
            ["emailRetry"] = new($"/api/candidates/{candidateId}/email-retry", "POST", "retryCandidateEmail"),
        };
        if (status is CandidateStatus.NotYetInvited or CandidateStatus.AwaitingAvailability
            or CandidateStatus.NoResponseNeedsFollowUp)
        {
            links["invite"] = new($"/api/candidates/{candidateId}/invite", "POST", "triggerCandidateInvite");
        }

        return links;
    }

    /// <summary>Builds the cancellation link for one Candidate booking.</summary>
    /// <param name="candidateId">The owning Candidate identifier.</param>
    /// <param name="bookingId">The Booking identifier used to target a cancellation.</param>
    /// <param name="isOriginal">Whether this is the original Booking; described in the cancel request, not the URL.</param>
    /// <returns>The cancel relation for the booking.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForBooking(Guid candidateId, Guid bookingId, bool isOriginal) =>
        new Dictionary<string, ApiLink>
        {
            ["cancel"] = new($"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", "POST", "cancelCandidateBooking"),
        };

    /// <summary>Builds the workflow links for one readiness response.</summary>
    /// <param name="candidateId">The stable Candidate identifier.</param>
    /// <param name="hasRecoverableType">Whether any outstanding type can currently be recovered.</param>
    /// <returns>The bookings and readiness relations, plus startRecovery when recoverable.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForReadiness(Guid candidateId, bool hasRecoverableType)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["bookings"] = new($"/api/candidates/{candidateId}/bookings", "GET", "listCandidateBookings"),
            ["readiness"] = new($"/api/candidates/{candidateId}/readiness", "GET", "getCandidateReadiness"),
        };
        if (hasRecoverableType)
        {
            links["startRecovery"] = new($"/api/candidates/{candidateId}/recovery-invites", "POST", "startRecoveryInvite");
        }

        return links;
    }

    /// <summary>Builds the confirm link for one anonymous invite token.</summary>
    /// <param name="token">The raw invite token embedded only in the confirm href.</param>
    /// <returns>The confirm relation for the invite.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForInviteToken(string token) =>
        new Dictionary<string, ApiLink>
        {
            ["confirm"] = new($"/api/booking/{Uri.EscapeDataString(token)}/confirm", "POST", "confirmBooking"),
        };

    /// <summary>Builds the cancel link for one anonymous manage token.</summary>
    /// <param name="token">The raw manage token embedded only in the cancel href.</param>
    /// <returns>The cancel relation for the managed booking.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForManageToken(string token) =>
        new Dictionary<string, ApiLink>
        {
            ["cancel"] = new($"/api/booking/manage/{Uri.EscapeDataString(token)}/cancel", "POST", "cancelManagedBooking"),
        };
}
