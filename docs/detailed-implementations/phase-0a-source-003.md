# 00a — Port source 3 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Api/Contracts/CandidateHypermediaResponses.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Contracts/CandidateHypermediaResponses.cs","encoding":"utf8","sha256":"dea80421ae35f8e85f4a58856e39c64ac94c74773e654b409bf4cadb87386668","parts":1,"part":1} -->

`````csharp
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
`````

## src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs","encoding":"utf8","sha256":"c0bd039a17b410963c76d1bff3f8e6fe854bab30d50eb9f2f25bb905b6f5281c","parts":1,"part":1} -->

`````csharp
using System.Text.Json.Serialization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Audit;

namespace EventBooking.Api.Contracts;

/// <summary>One slot-overview row plus its audit and cancel affordances.</summary>
public sealed record SlotOverviewResourceResponse(
    /// <summary>Gets the stable confirmed slot identifier.</summary>
    Guid ConfirmedSlotId,
    /// <summary>Gets the slot window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the slot window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the slot window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the per-appointment-type capacity rows.</summary>
    IReadOnlyList<SlotCapacityRow> Capacities,
    /// <summary>Gets the aggregate active booking count.</summary>
    int ActiveBookings,
    /// <summary>Gets the audit and cancel affordances for the slot.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one slot-overview row into its hypermedia resource.</summary>
    /// <param name="row">The application slot-overview row to project.</param>
    /// <returns>The API resource with slot-overview links.</returns>
    public static SlotOverviewResourceResponse From(SlotOverviewRow row) =>
        new(row.ConfirmedSlotId, row.Date, row.StartTime, row.EndTime,
            row.Capacities, row.ActiveBookings,
            new Dictionary<string, ApiLink>
            {
                ["audit"] = new($"/api/audit/slot/{row.ConfirmedSlotId}", "GET", "getSlotAuditHistory"),
                ["cancel"] = new($"/api/slots/confirmed/{row.ConfirmedSlotId}", "DELETE", "cancelConfirmedSlot"),
            });
}

/// <summary>Slot-only operations view plus the collection self affordance.</summary>
public sealed record SlotOperationsResourceResponse(
    /// <summary>Gets one row per confirmed slot with capacity and booking counts.</summary>
    IReadOnlyList<SlotOverviewResourceResponse> Slots,
    /// <summary>Gets the collection affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one slot operations view into its hypermedia resource.</summary>
    /// <param name="view">The application slot operations view to project.</param>
    /// <returns>The API resource with operations and row links.</returns>
    public static SlotOperationsResourceResponse From(SlotOperationsView view) =>
        new(view.Slots.Select(SlotOverviewResourceResponse.From).ToList(),
            new Dictionary<string, ApiLink>
            {
                ["self"] = new("/api/slots/operations", "GET", "getSlotOperations"),
                ["board"] = new("/api/slots/board", "GET", "getSlotBoard"),
            });
}

/// <summary>Coordinator dashboards plus entry affordances for related collections.</summary>
public sealed record DashboardResourceResponse(
    /// <summary>Gets the candidates waiting for availability.</summary>
    IReadOnlyList<AwaitingAvailabilityRow> AwaitingAvailability,
    /// <summary>Gets the candidates needing follow-up after no response.</summary>
    IReadOnlyList<NoResponseRow> NoResponse,
    /// <summary>Gets the slot overview rows with capacity and booking counts.</summary>
    IReadOnlyList<SlotOverviewResourceResponse> Slots,
    /// <summary>Gets the latest email delivery state per candidate.</summary>
    IReadOnlyList<CandidateEmailStatusView> EmailStatuses,
    /// <summary>Gets the dashboard self and collection entry affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one dashboards view into its hypermedia resource.</summary>
    /// <param name="view">The application dashboards view to project.</param>
    /// <returns>The API resource with dashboard links.</returns>
    public static DashboardResourceResponse From(DashboardsView view) =>
        new(view.AwaitingAvailability, view.NoResponse,
            view.Slots.Select(SlotOverviewResourceResponse.From).ToList(),
            view.EmailStatuses,
            new Dictionary<string, ApiLink>
            {
                ["self"] = new("/api/dashboards", "GET", "getDashboards"),
                ["candidates"] = new("/api/candidates", "GET", "listCandidates"),
                ["slotOperations"] = new("/api/slots/operations", "GET", "getSlotOperations"),
                ["audit"] = new("/api/audit/search", "GET", "searchAudit"),
            });
}

/// <summary>One audit history row plus a link when it addresses a real candidate or slot route.</summary>
public sealed record AuditHistoryResourceResponse(
    /// <summary>Gets when the audited action was recorded.</summary>
    DateTimeOffset Timestamp,
    /// <summary>Gets the audited entity type name.</summary>
    string EntityType,
    /// <summary>Gets the audited entity identifier.</summary>
    Guid EntityId,
    /// <summary>Gets the audited action name.</summary>
    string Action,
    /// <summary>Gets the actor type name.</summary>
    string ActorType,
    /// <summary>Gets the actor identifier, or null for system actions.</summary>
    string? ActorId,
    /// <summary>Gets the human-readable details, or null when none were recorded.</summary>
    string? Details,
    /// <summary>Gets the history link when the row addresses a real route, otherwise empty.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    private static readonly IReadOnlySet<string> CandidateBucket =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AuditEntityTypes.Candidate, AuditEntityTypes.Invite,
            AuditEntityTypes.Booking, AuditEntityTypes.BookingAppointment,
        };

    /// <summary>Projects one audit history row, linking only to real candidate or slot routes.</summary>
    /// <param name="row">The application audit history row to project.</param>
    /// <returns>The API resource with a history link or an empty link dictionary.</returns>
    public static AuditHistoryResourceResponse From(AuditHistoryRow row)
    {
        IReadOnlyDictionary<string, ApiLink> links = row.EntityType switch
        {
            AuditEntityTypes.ConfirmedSlot => new Dictionary<string, ApiLink>
            {
                ["history"] = new($"/api/audit/slot/{row.EntityId}", "GET", "getSlotAuditHistory"),
            },
            _ when CandidateBucket.Contains(row.EntityType) => new Dictionary<string, ApiLink>
            {
                ["history"] = new($"/api/audit/candidate/{row.EntityId}", "GET", "getCandidateAuditHistory"),
            },
            _ => new Dictionary<string, ApiLink>(),
        };
        return new AuditHistoryResourceResponse(
            row.Timestamp, row.EntityType, row.EntityId, row.Action,
            row.ActorType, row.ActorId, row.Details, links);
    }
}

/// <summary>One audit search page with conditional next-page affordance.</summary>
public sealed record AuditSearchResourceResponse(
    /// <summary>Gets the newest-first audit rows for the requested page.</summary>
    IReadOnlyList<AuditHistoryResourceResponse> Rows,
    /// <summary>Gets the opaque cursor for the following page, or null when exhausted.</summary>
    string? NextCursor,
    /// <summary>Gets the self relation plus next while paging continues.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one audit search page into its hypermedia resource.</summary>
    /// <param name="page">The application search page to project.</param>
    /// <param name="currentQuery">The raw incoming query string, used verbatim for the self href.</param>
    /// <returns>The API resource with search links.</returns>
    public static AuditSearchResourceResponse From(AuditSearchPage page, string currentQuery) =>
        new(page.Rows.Select(AuditHistoryResourceResponse.From).ToList(),
            page.NextCursor,
            StaffResourceLinks.ForAuditSearch(currentQuery, page.NextCursor));
}
`````

## src/EventBooking.Api/Contracts/SlotHypermediaResponses.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Contracts/SlotHypermediaResponses.cs","encoding":"utf8","sha256":"39ef63a66194388f66b47063cca4f5d0ef15b65b201278f5c3f08e3c225cd25f","parts":1,"part":1} -->

`````csharp
using System.Text.Json.Serialization;
using EventBooking.Application.Slots;

namespace EventBooking.Api.Contracts;

/// <summary>Central factories for staff-workspace affordances.</summary>
public static class StaffResourceLinks
{
    /// <summary>Builds the self and update relations for the settings resource.</summary>
    /// <returns>The settings affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForSettings() =>
        new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/admin/settings", "GET", "getSettings"),
            ["update"] = new("/api/admin/settings", "PUT", "updateSettings"),
        };

    /// <summary>Builds the replace and clear relations for one staff access profile.</summary>
    /// <param name="staffUserId">The staff identity the relations target.</param>
    /// <param name="version">The observed version, supplied as a body or query input rather than embedded in the href.</param>
    /// <returns>The staff-access affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForStaffAccess(Guid staffUserId, long version) =>
        new Dictionary<string, ApiLink>
        {
            ["replace"] = new($"/api/admin/staff-access/{staffUserId}", "PUT", "replaceStaffAccessScope"),
            ["clear"] = new($"/api/admin/staff-access/{staffUserId}", "DELETE", "clearStaffAccessScope"),
        };

    /// <summary>Builds the entry relations for the slot board collection.</summary>
    /// <returns>The slot board affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForSlotBoard() =>
        new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/slots/board", "GET", "getSlotBoard"),
            ["propose"] = new("/api/slots/proposals", "POST", "proposeSlot"),
            ["import"] = new("/api/confirmed-slots/import", "POST", "importConfirmedSlots"),
        };

    /// <summary>Builds the conditional workflow links for one open slot proposal.</summary>
    /// <param name="proposalId">The proposal identifier the relations target.</param>
    /// <param name="acceptedByMe">Whether the caller already accepted; gates the withdrawAcceptance relation.</param>
    /// <param name="createdByMe">Whether the caller created the proposal; gates the withdrawProposal relation.</param>
    /// <returns>The proposal affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForProposal(Guid proposalId, bool acceptedByMe, bool createdByMe)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["accept"] = new($"/api/slots/proposals/{proposalId}/acceptance", "POST", "acceptProposal"),
        };
        if (acceptedByMe)
        {
            links["withdrawAcceptance"] = new($"/api/slots/proposals/{proposalId}/acceptance", "DELETE", "withdrawAcceptance");
        }

        if (createdByMe)
        {
            links["withdrawProposal"] = new($"/api/slots/proposals/{proposalId}", "DELETE", "withdrawProposal");
        }

        return links;
    }

    /// <summary>Builds the management relations for one confirmed slot.</summary>
    /// <param name="confirmedSlotId">The confirmed slot identifier the relations target.</param>
    /// <returns>The confirmed-slot affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForConfirmedSlot(Guid confirmedSlotId) =>
        new Dictionary<string, ApiLink>
        {
            ["adjustCapacity"] = new($"/api/slots/confirmed/{confirmedSlotId}/capacity", "PUT", "adjustConfirmedSlotCapacity"),
            ["cancel"] = new($"/api/slots/confirmed/{confirmedSlotId}", "DELETE", "cancelConfirmedSlot"),
            ["audit"] = new($"/api/audit/slot/{confirmedSlotId}", "GET", "getSlotAuditHistory"),
            ["appointmentDetail"] = new($"/api/appointment-workspace/slots/{confirmedSlotId}", "GET", "getAppointmentSlot"),
            ["roster"] = new($"/api/appointment-workspace/slots/{confirmedSlotId}/roster", "GET", "exportAppointmentRoster"),
        };

    /// <summary>Builds the self relation from the incoming query plus a next relation while paging continues.</summary>
    /// <param name="currentQuery">The raw incoming query string, used verbatim for the self href.</param>
    /// <param name="nextCursor">The opaque cursor for the following page, or null when exhausted.</param>
    /// <returns>The audit-search affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForAuditSearch(string currentQuery, string? nextCursor)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["self"] = new($"/api/audit/search{currentQuery}", "GET", "searchAudit"),
        };
        if (nextCursor is not null)
        {
            links["next"] = new(BuildAuditNextHref(currentQuery, nextCursor), "GET", "searchAudit");
        }

        return links;
    }

    /// <summary>Builds the detail and roster relations for one appointment-workspace slot.</summary>
    /// <param name="confirmedSlotId">The confirmed slot identifier the relations target.</param>
    /// <returns>The appointment-slot affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForAppointmentSlot(Guid confirmedSlotId) =>
        new Dictionary<string, ApiLink>
        {
            ["detail"] = new($"/api/appointment-workspace/slots/{confirmedSlotId}", "GET", "getAppointmentSlot"),
            ["roster"] = new($"/api/appointment-workspace/slots/{confirmedSlotId}/roster", "GET", "exportAppointmentRoster"),
        };

    /// <summary>Builds the status-update relation for one booking appointment.</summary>
    /// <param name="bookingAppointmentId">The booking appointment identifier the relation targets.</param>
    /// <returns>The booking-appointment affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForBookingAppointment(Guid bookingAppointmentId) =>
        new Dictionary<string, ApiLink>
        {
            ["updateStatus"] = new($"/api/appointment-workspace/appointments/{bookingAppointmentId}/status", "PUT", "updateAppointmentStatus"),
        };

    private static string BuildAuditNextHref(string currentQuery, string nextCursor)
    {
        var parameters = new List<(string Name, string? Value)>();
        var seenCursor = false;
        if (currentQuery.Length > 1)
        {
            foreach (var pair in currentQuery[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var index = pair.IndexOf('=');
                var name = index < 0 ? pair : pair[..index];
                var value = index < 0 ? null : pair[(index + 1)..];
                if (string.Equals(name, "cursor", StringComparison.OrdinalIgnoreCase))
                {
                    seenCursor = true;
                    parameters.Add((name, Uri.EscapeDataString(nextCursor)));
                }
                else
                {
                    parameters.Add((name, value));
                }
            }
        }

        if (!seenCursor)
        {
            parameters.Add(("cursor", Uri.EscapeDataString(nextCursor)));
        }

        return "/api/audit/search?" + string.Join("&", parameters.Select(pair =>
            pair.Value is null ? pair.Name : $"{pair.Name}={pair.Value}"));
    }
}

/// <summary>One open slot proposal plus the caller's conditional workflow operations.</summary>
public sealed record OpenProposalResourceResponse(
    /// <summary>Gets the stable slot proposal identifier.</summary>
    Guid ProposalId,
    /// <summary>Gets the proposal window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the proposal window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the proposal window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the appointment-type names that already accepted this proposal.</summary>
    IReadOnlyList<string> AcceptedByAppointmentTypeNames,
    /// <summary>Gets the caller's accepted headcount, or null when not accepted by the caller.</summary>
    int? MyAcceptedHeadcount,
    /// <summary>Gets whether the caller already accepted this proposal.</summary>
    bool AcceptedByMe,
    /// <summary>Gets whether the caller created this proposal.</summary>
    bool CreatedByMe,
    /// <summary>Gets the conditional workflow operations for the proposal.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one open proposal view into its hypermedia resource.</summary>
    /// <param name="view">The application proposal view to project.</param>
    /// <returns>The API resource with conditional proposal links.</returns>
    public static OpenProposalResourceResponse From(OpenProposalView view) =>
        new(view.ProposalId, view.Date, view.StartTime, view.EndTime,
            view.AcceptedByAppointmentTypeNames, view.MyAcceptedHeadcount,
            view.AcceptedByMe, view.CreatedByMe,
            StaffResourceLinks.ForProposal(view.ProposalId, view.AcceptedByMe, view.CreatedByMe));
}

/// <summary>One confirmed slot plus its management operations.</summary>
public sealed record ManagerConfirmedSlotResourceResponse(
    /// <summary>Gets the stable confirmed slot identifier.</summary>
    Guid ConfirmedSlotId,
    /// <summary>Gets the slot window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the slot window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the slot window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the caller's total headcount on this slot.</summary>
    int MyHeadcount,
    /// <summary>Gets the caller's remaining capacity on this slot.</summary>
    int MyRemainingCapacity,
    /// <summary>Gets the management operations for the confirmed slot.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one confirmed slot view into its hypermedia resource.</summary>
    /// <param name="view">The application confirmed slot view to project.</param>
    /// <returns>The API resource with confirmed-slot links.</returns>
    public static ManagerConfirmedSlotResourceResponse From(ManagerConfirmedSlotView view) =>
        new(view.ConfirmedSlotId, view.Date, view.StartTime, view.EndTime,
            view.MyHeadcount, view.MyRemainingCapacity,
            StaffResourceLinks.ForConfirmedSlot(view.ConfirmedSlotId));
}

/// <summary>Manager-scoped slot board plus board-level affordances.</summary>
public sealed record SlotBoardResourceResponse(
    /// <summary>Gets the open proposals visible to the caller.</summary>
    IReadOnlyList<OpenProposalResourceResponse> OpenProposals,
    /// <summary>Gets the active confirmed slots visible to the caller.</summary>
    IReadOnlyList<ManagerConfirmedSlotResourceResponse> ConfirmedSlots,
    /// <summary>Gets the board-level affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one slot board into its hypermedia resource.</summary>
    /// <param name="board">The application slot board to project.</param>
    /// <returns>The API resource with board and row links.</returns>
    public static SlotBoardResourceResponse From(ManagerSlotBoard board) =>
        new(board.OpenProposals.Select(OpenProposalResourceResponse.From).ToList(),
            board.ConfirmedSlots.Select(ManagerConfirmedSlotResourceResponse.From).ToList(),
            StaffResourceLinks.ForSlotBoard());
}
`````

## src/EventBooking.Api/Dockerfile — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Dockerfile","encoding":"utf8","sha256":"41f57b88ea7a86045e81ce68d65593f915ef07733a46803fdc3cdbf5d9fd9d67","parts":1,"part":1} -->

`````text
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Api/EventBooking.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EventBooking.Api.dll"]
`````

## src/EventBooking.Api/Endpoints/AdminEndpoints.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/AdminEndpoints.cs","encoding":"utf8","sha256":"35befaaff4fcd738eb18c6cce18eea3d778df6241b23cff2f27da411a0efe348","parts":1,"part":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Settings;
using EventBooking.Application.Slots;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Endpoints;

public static class AdminEndpoints
{
    public sealed record UpdateSettingsRequest(int InviteExpiryDays, int MaxAutoRetryCount);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/settings", async (
            ICallerAccessor caller,
            AdminSettingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.GetAsync(caller.RequireStaffUserId(), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(SettingsResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getSettings")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPut("/settings", async (
            UpdateSettingsRequest request,
            ICallerAccessor caller,
            AdminSettingsHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.UpdateAsync(
                new UpdateSettingsCommand(
                    caller.RequireStaffUserId(), request.InviteExpiryDays, request.MaxAutoRetryCount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("updateSettings")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        return app;
    }
}
`````

## src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs","encoding":"utf8","sha256":"088436ea6ee088c8a425af4430731647f040e7d9659f494212f6acf90a5f5f29","parts":1,"part":1} -->

`````csharp
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the anonymous API discovery root.</summary>
public static class ApiDiscoveryEndpoints
{
    /// <summary>Maps the anonymous <c>GET /api</c> entry document.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapApiDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api", () =>
        {
            var links = new Dictionary<string, ApiLink>(StringComparer.Ordinal)
            {
                ["self"] = Link("getApiIndex"),
                ["openapi"] = Link("getOpenApiDocument"),
                ["swagger"] = Link("getSwaggerUi"),
                ["health"] = Link("getHealth"),
                ["me"] = Link("getMyAccess"),
                ["slotBoard"] = Link("getSlotBoard"),
                ["slotOperations"] = Link("getSlotOperations"),
                ["candidates"] = Link("listCandidates"),
                ["employeeGroups"] = Link("listEmployeeGroups"),
                ["settings"] = Link("getSettings"),
                ["staffAccess"] = Link("listStaffAccess"),
                ["dashboards"] = Link("getDashboards"),
                ["audit"] = Link("searchAudit"),
                ["appointments"] = Link("listAppointmentSlots"),
            };
            return Results.Ok(new ApiDiscoveryResponse("EventBooking API", "v1", links));
        }).AllowAnonymous()
            .WithAgentMetadata("getApiIndex")
            .Produces(200);
        return app;
    }

    private static ApiLink Link(string operationId)
    {
        var operation = AgentOperationCatalog.Get(operationId);
        return new ApiLink(operation.Route, operation.Method, operation.OperationId);
    }
}
`````

## src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs","encoding":"utf8","sha256":"76b35ccb0e9652d43fcdbe50cca142ea2fa34c1c2d380c55e5384a82a0d566c7","parts":1,"part":1} -->

`````csharp
using System.Text;
using System.Text.Json.Serialization;
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Appointments;
using EventBooking.Application.Common;
using EventBooking.Domain.Bookings;

namespace EventBooking.Api.Endpoints;

/// <summary>Accepts one requested appointment status and the last observed version.</summary>
public sealed record UpdateBookingAppointmentStatusRequest
{
    /// <summary>Gets the requested `BookingAppointmentStatus` name.</summary>
    public string? Status { get; init; }

    /// <summary>Gets the positive version last observed by the caller.</summary>
    public long? ExpectedVersion { get; init; }
}

/// <summary>Maps the three authenticated appointment-workspace routes.</summary>
public static class AppointmentWorkspaceEndpoints
{
    /// <summary>Adds the capability-protected appointment-workspace routes.</summary>
    public static IEndpointRouteBuilder MapAppointmentWorkspaceEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/appointment-workspace")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/slots", async (
            ICallerAccessor caller,
            GetAppointmentWorkspaceHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.ListSlotsAsync(
                caller.RequireStaffUserId(), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(AppointmentWorkspaceSlotListResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("listAppointmentSlots")
            .Produces(200)
            .ProducesProblem(403);

        group.MapGet("/slots/{confirmedSlotId:guid}", async (
            Guid confirmedSlotId,
            ICallerAccessor caller,
            GetAppointmentWorkspaceHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.GetSlotAsync(
                caller.RequireStaffUserId(), confirmedSlotId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(AppointmentSlotDetailResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getAppointmentSlot")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/slots/{confirmedSlotId:guid}/roster", async (
            Guid confirmedSlotId,
            ICallerAccessor caller,
            GetAppointmentWorkspaceHandler handler,
            AppointmentRosterCsvFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            // The same read the JSON detail route performs, so authorization, scoping, and the
            // not-found-for-out-of-scope behaviour are identical by construction.
            var result = await handler.GetSlotAsync(
                caller.RequireStaffUserId(), confirmedSlotId, cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var roster = formatter.Format(result.Value);
            return Results.File(
                Encoding.UTF8.GetBytes(roster.CsvText),
                contentType: "text/csv; charset=utf-8",
                fileDownloadName: roster.FileName);
        })
            .WithAgentMetadata("exportAppointmentRoster")
            .Produces<string>(200, "text/csv")
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPut("/appointments/{bookingAppointmentId:guid}/status", async (
            Guid bookingAppointmentId,
            UpdateBookingAppointmentStatusRequest? request,
            ICallerAccessor caller,
            UpdateBookingAppointmentStatusHandler handler,
            CancellationToken cancellationToken) =>
        {
            var expectedVersion = request?.ExpectedVersion ?? 0;
            if (request?.Status is null
                || !Enum.TryParse<BookingAppointmentStatus>(
                    request.Status, ignoreCase: false, out var status)
                || !Enum.IsDefined(status)
                || expectedVersion <= 0)
            {
                return Result<BookingAppointmentUpdateView>.Failure(
                    Error.Validation("A recognised status and positive expectedVersion are required."))
                    .ToResponse();
            }

            var result = await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = caller.RequireStaffUserId(),
                    BookingAppointmentId = bookingAppointmentId,
                    Status = status,
                    ExpectedVersion = expectedVersion,
                },
                cancellationToken);
            return result.IsSuccess
                ? Results.Ok(BookingAppointmentUpdateResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("updateAppointmentStatus")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        return app;
    }
}

/// <summary>Counts scoped booking appointments in each operational state.</summary>
public sealed record AppointmentStatusCountsResponse
{
    /// <summary>Gets candidates booked but not checked in for this appointment.</summary>
    public required int Expected { get; init; }

    /// <summary>Gets candidates checked in for this appointment.</summary>
    public required int CheckedIn { get; init; }

    /// <summary>Gets required appointments completed after check-in.</summary>
    public required int Completed { get; init; }

    /// <summary>Gets candidates recorded as not attending this required appointment.</summary>
    public required int NoShow { get; init; }

    internal static AppointmentStatusCountsResponse From(AppointmentStatusCounts counts) => new()
    {
        Expected = counts.Expected,
        CheckedIn = counts.CheckedIn,
        Completed = counts.Completed,
        NoShow = counts.NoShow,
    };
}

/// <summary>Describes one selectable active slot without candidate rows.</summary>
public sealed record AppointmentSlotSummaryResponse
{
    /// <summary>Gets the confirmed slot identifier.</summary>
    public required Guid ConfirmedSlotId { get; init; }

    /// <summary>Gets the slot's head-office calendar date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }

    /// <summary>Gets scoped counts grouped by independent operational state.</summary>
    public required AppointmentStatusCountsResponse Counts { get; init; }

    /// <summary>Gets the detail and roster affordances for the slot.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static AppointmentSlotSummaryResponse From(AppointmentSlotSummary summary) => new()
    {
        ConfirmedSlotId = summary.ConfirmedSlotId,
        Date = summary.Date,
        StartTime = summary.StartTime,
        EndTime = summary.EndTime,
        Counts = AppointmentStatusCountsResponse.From(summary.Counts),
        Links = StaffResourceLinks.ForAppointmentSlot(summary.ConfirmedSlotId),
    };
}

/// <summary>Returns the trusted appointment-type name and its selectable active slots.</summary>
public sealed record AppointmentWorkspaceSlotListResponse
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets current and upcoming active slots containing scoped active bookings.</summary>
    public required IReadOnlyList<AppointmentSlotSummaryResponse> Slots { get; init; }

    /// <summary>Gets the collection self affordance.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static AppointmentWorkspaceSlotListResponse From(AppointmentWorkspaceSlotList list) => new()
    {
        AppointmentTypeName = list.AppointmentTypeName,
        Slots = list.Slots.Select(AppointmentSlotSummaryResponse.From).ToList(),
        Links = new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/appointment-workspace/slots", "GET", "listAppointmentSlots"),
        },
    };
}

/// <summary>Contains only the fields needed to identify and conduct one booked appointment.</summary>
public sealed record BookingAppointmentRowResponse
{
    /// <summary>Gets the stable booking-appointment command identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }

    /// <summary>Gets the candidate name used for primary human identification.</summary>
    public required string CandidateName { get; init; }

    /// <summary>Gets the candidate email used for secondary human identification.</summary>
    public required string CandidateEmail { get; init; }

    /// <summary>Gets this appointment's independent operational status name.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when staff checked the candidate in, or null until check-in.</summary>
    public DateTimeOffset? CheckedInAt { get; init; }

    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public DateTimeOffset? OutcomeAt { get; init; }

    /// <summary>Gets the positive concurrency version required by a status command.</summary>
    public required long Version { get; init; }

    /// <summary>Gets the status-update affordance for the appointment.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static BookingAppointmentRowResponse From(BookingAppointmentRow row) => new()
    {
        BookingAppointmentId = row.BookingAppointmentId,
        CandidateName = row.CandidateName,
        CandidateEmail = row.CandidateEmail,
        Status = row.Status.ToString(),
        CheckedInAt = row.CheckedInAt,
        OutcomeAt = row.OutcomeAt,
        Version = row.Version,
        Links = StaffResourceLinks.ForBookingAppointment(row.BookingAppointmentId),
    };
}

/// <summary>Returns one scoped active slot and only its minimum-data operational rows.</summary>
public sealed record AppointmentSlotDetailResponse
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets the selected confirmed slot identifier.</summary>
    public required Guid ConfirmedSlotId { get; init; }

    /// <summary>Gets the slot's head-office calendar date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }

    /// <summary>Gets scoped active-booking appointment rows ordered for staff identification.</summary>
    public required IReadOnlyList<BookingAppointmentRowResponse> Appointments { get; init; }

    /// <summary>Gets the detail self and roster affordances.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static AppointmentSlotDetailResponse From(AppointmentSlotDetail detail) => new()
    {
        AppointmentTypeName = detail.AppointmentTypeName,
        ConfirmedSlotId = detail.ConfirmedSlotId,
        Date = detail.Date,
        StartTime = detail.StartTime,
        EndTime = detail.EndTime,
        Appointments = detail.Appointments.Select(BookingAppointmentRowResponse.From).ToList(),
        Links = new Dictionary<string, ApiLink>
        {
            ["self"] = new($"/api/appointment-workspace/slots/{detail.ConfirmedSlotId}", "GET", "getAppointmentSlot"),
            ["roster"] = new($"/api/appointment-workspace/slots/{detail.ConfirmedSlotId}/roster", "GET", "exportAppointmentRoster"),
        },
    };
}

/// <summary>Returns only the changed appointment row state needed by the workspace.</summary>
public sealed record BookingAppointmentUpdateResponse
{
    /// <summary>Gets the stable booking-appointment identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }

    /// <summary>Gets this appointment's current independent operational status name.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when staff checked the candidate in, or null until check-in.</summary>
    public DateTimeOffset? CheckedInAt { get; init; }

    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public DateTimeOffset? OutcomeAt { get; init; }

    /// <summary>Gets the current positive concurrency version.</summary>
    public required long Version { get; init; }

    /// <summary>Gets the status-update affordance for the appointment.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static BookingAppointmentUpdateResponse From(BookingAppointmentUpdateView view) => new()
    {
        BookingAppointmentId = view.BookingAppointmentId,
        Status = view.Status.ToString(),
        CheckedInAt = view.CheckedInAt,
        OutcomeAt = view.OutcomeAt,
        Version = view.Version,
        Links = StaffResourceLinks.ForBookingAppointment(view.BookingAppointmentId),
    };
}
`````

## src/EventBooking.Api/Endpoints/AuditEndpoints.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/AuditEndpoints.cs","encoding":"utf8","sha256":"3f2ad5f91db5d1a55ed51c4f2701e0df333d1991d028f35aa13a78916d0c0089","parts":1,"part":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Audit;

namespace EventBooking.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/slot/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            GetAuditHistoryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAuditHistoryQuery(
                    caller.RequireStaffUserId(), AuditEntityTypes.ConfirmedSlot, id),
                cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value.Select(AuditHistoryResourceResponse.From).ToList())
                : result.ToResponse();
        })
            .WithAgentMetadata("getSlotAuditHistory")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/candidate/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            GetAuditHistoryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAuditHistoryQuery(caller.RequireStaffUserId(), null, id), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value.Select(AuditHistoryResourceResponse.From).ToList())
                : result.ToResponse();
        })
            .WithAgentMetadata("getCandidateAuditHistory")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/search", async (
            string? from,
            string? to,
            string? actorType,
            string? action,
            string? identifier,
            string? entityType,
            string? cursor,
            int? pageSize,
            HttpRequest request,
            ICallerAccessor caller,
            GetAuditSearchHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!AuditInputParser.TryParseBound(from, out var fromBound))
            {
                return Invalid(nameof(from));
            }

            if (!AuditInputParser.TryParseBound(to, out var toBound))
            {
                return Invalid(nameof(to));
            }

            var result = await handler.HandleAsync(
                new GetAuditSearchQuery(
                    caller.RequireStaffUserId(),
                    fromBound,
                    toBound,
                    actorType,
                    action,
                    identifier,
                    entityType,
                    cursor,
                    pageSize ?? 50),
                cancellationToken);
            return result.IsSuccess
                ? Results.Ok(AuditSearchResourceResponse.From(
                    result.Value, request.QueryString.Value ?? string.Empty))
                : result.ToResponse();
        })
            .WithAgentMetadata("searchAudit")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403);

        return app;
    }

    private static IResult Invalid(string parameter) =>
        Result<AuditSearchPage>
            .Failure(Error.Validation($"The {parameter} bound is not a valid timestamp."))
            .ToResponse();
}
`````

## src/EventBooking.Api/Endpoints/AuditInputParser.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/AuditInputParser.cs","encoding":"utf8","sha256":"f697cbfb7af1dbe43bbbaabfd4446f89b611d65e2a2e1aa8e31a921079d913a5","parts":1,"part":1} -->

`````csharp
using System.Globalization;

namespace EventBooking.Api.Endpoints;

/// <summary>Shares invariant optional-timestamp parsing between HTTP and MCP surfaces.</summary>
public static class AuditInputParser
{
    /// <summary>Parses an optional invariant timestamp; blank is a successful null.</summary>
    /// <param name="value">The raw bound text, or null when the bound is absent.</param>
    /// <param name="bound">The parsed bound, or null when absent.</param>
    /// <returns>True when absent or a valid timestamp; false otherwise.</returns>
    public static bool TryParseBound(string? value, out DateTimeOffset? bound)
    {
        bound = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        bound = parsed;
        return true;
    }
}
`````

## src/EventBooking.Api/Endpoints/BookingEndpoints.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/BookingEndpoints.cs","encoding":"utf8","sha256":"71f42620ae49f7bc7a1fb7b027a49d897a0edcc2a980dd1860c49da3897dd09c","parts":1,"part":1} -->

`````csharp
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Bookings;

namespace EventBooking.Api.Endpoints;

public static class BookingEndpoints
{
    /// <summary>Named so Task 58's registration and these routes cannot drift apart.</summary>
    public const string RateLimiterPolicy = "candidate-links";

    public sealed record ConfirmBookingRequest(Guid ConfirmedSlotId);

    public sealed record CancelBookingRequest(bool Rebook);

    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/booking")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiterPolicy);

        group.MapGet("/{token}", async (
            string token,
            ViewInviteHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ViewInviteQuery(token), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(InviteResourceResponse.From(result.Value, token));
        })
            .WithAgentMetadata("viewInvite")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(429);

        group.MapPost("/{token}/confirm", async (
            string token,
            ConfirmBookingRequest request,
            ConfirmBookingHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ConfirmBookingCommand(token, request.ConfirmedSlotId), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("confirmBooking")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(429);

        group.MapGet("/manage/{token}", async (
            string token,
            ViewBookingHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ViewBookingQuery(token), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(ManagedBookingResourceResponse.From(result.Value, token));
        })
            .WithAgentMetadata("viewManagedBooking")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(429);

        group.MapPost("/manage/{token}/cancel", async (
            string token,
            CancelBookingRequest request,
            CancelBookingHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelBookingCommand(token, request.Rebook), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelManagedBooking")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(429);

        return app;
    }
}
`````
