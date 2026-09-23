using System.Text.Json.Serialization;
using EventBooking.Application.Events;

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

    /// <summary>Builds the entry relations for the event board collection.</summary>
    /// <returns>The event board affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForEventBoard() =>
        new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/events/board", "GET", "getEventBoard"),
            ["propose"] = new("/api/event-proposals", "POST", "proposeEvent"),
            ["import"] = new("/api/events/import", "POST", "importEvents"),
        };

    /// <summary>Builds the conditional workflow links for one open event proposal.</summary>
    /// <param name="proposalId">The proposal identifier the relations target.</param>
    /// <param name="acceptedByMe">Whether the caller already accepted; gates the withdrawAcceptance relation.</param>
    /// <param name="createdByMe">Whether the caller created the proposal; gates the withdrawProposal relation.</param>
    /// <returns>The proposal affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForProposal(Guid proposalId, bool acceptedByMe, bool createdByMe)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["accept"] = new($"/api/event-proposals/{proposalId}/acceptance", "POST", "acceptProposal"),
        };
        if (acceptedByMe)
        {
            links["withdrawAcceptance"] = new($"/api/event-proposals/{proposalId}/acceptance", "DELETE", "withdrawAcceptance");
        }

        if (createdByMe)
        {
            links["withdrawProposal"] = new($"/api/event-proposals/{proposalId}", "DELETE", "withdrawProposal");
        }

        return links;
    }

    /// <summary>Builds the management relations for one eventItem.</summary>
    /// <param name="eventId">The event identifier the relations target.</param>
    /// <returns>The event affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForEvent(Guid eventId) =>
        new Dictionary<string, ApiLink>
        {
            ["adjustCapacity"] = new($"/api/events/{eventId}/capacity", "PUT", "adjustEventCapacity"),
            ["cancel"] = new($"/api/events/{eventId}", "DELETE", "cancelEvent"),
            ["audit"] = new($"/api/audit/event/{eventId}", "GET", "getEventAuditHistory"),
            ["appointmentDetail"] = new($"/api/appointment-workspace/events/{eventId}", "GET", "getAppointmentEvent"),
            ["roster"] = new($"/api/appointment-workspace/events/{eventId}/roster", "GET", "exportAppointmentRoster"),
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

    /// <summary>Builds the detail and roster relations for one appointment-workspace eventItem.</summary>
    /// <param name="eventId">The event identifier the relations target.</param>
    /// <returns>The appointment-event affordances keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> ForAppointmentEvent(Guid eventId) =>
        new Dictionary<string, ApiLink>
        {
            ["detail"] = new($"/api/appointment-workspace/events/{eventId}", "GET", "getAppointmentEvent"),
            ["roster"] = new($"/api/appointment-workspace/events/{eventId}/roster", "GET", "exportAppointmentRoster"),
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

/// <summary>One open event proposal plus the caller's conditional workflow operations.</summary>
public sealed record OpenProposalResourceResponse(
    /// <summary>Gets the stable event proposal identifier.</summary>
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

/// <summary>One event plus its management operations.</summary>
public sealed record ManagerEventResourceResponse(
    /// <summary>Gets the stable event identifier.</summary>
    Guid EventId,
    /// <summary>Gets the event window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the event window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the event window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the caller's total headcount on this eventItem.</summary>
    int MyHeadcount,
    /// <summary>Gets the caller's remaining capacity on this eventItem.</summary>
    int MyRemainingCapacity,
    /// <summary>Gets the management operations for the eventItem.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one event view into its hypermedia resource.</summary>
    /// <param name="view">The application event view to project.</param>
    /// <returns>The API resource with event links.</returns>
    public static ManagerEventResourceResponse From(ManagerEventView view) =>
        new(view.EventId, view.Date, view.StartTime, view.EndTime,
            view.MyHeadcount, view.MyRemainingCapacity,
            StaffResourceLinks.ForEvent(view.EventId));
}

/// <summary>Manager-scoped event board plus board-level affordances.</summary>
public sealed record EventBoardResourceResponse(
    /// <summary>Gets the open proposals visible to the caller.</summary>
    IReadOnlyList<OpenProposalResourceResponse> OpenProposals,
    /// <summary>Gets the active events visible to the caller.</summary>
    IReadOnlyList<ManagerEventResourceResponse> Events,
    /// <summary>Gets the board-level affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one event board into its hypermedia resource.</summary>
    /// <param name="board">The application event board to project.</param>
    /// <returns>The API resource with board and row links.</returns>
    public static EventBoardResourceResponse From(ManagerEventBoard board) =>
        new(board.OpenProposals.Select(OpenProposalResourceResponse.From).ToList(),
            board.Events.Select(ManagerEventResourceResponse.From).ToList(),
            StaffResourceLinks.ForEventBoard());
}
