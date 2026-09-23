using System.Text.Json.Serialization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Audit;

namespace EventBooking.Api.Contracts;

/// <summary>One event-overview row plus its audit and cancel affordances.</summary>
public sealed record EventOverviewResourceResponse(
    /// <summary>Gets the stable event identifier.</summary>
    Guid EventId,
    /// <summary>Gets the event window calendar date.</summary>
    DateOnly Date,
    /// <summary>Gets the start of the event window.</summary>
    TimeOnly StartTime,
    /// <summary>Gets the end of the event window.</summary>
    TimeOnly EndTime,
    /// <summary>Gets the per-appointment-type capacity rows.</summary>
    IReadOnlyList<EventCapacityRow> Capacities,
    /// <summary>Gets the aggregate active booking count.</summary>
    int ActiveBookings,
    /// <summary>Gets the audit and cancel affordances for the eventItem.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one event-overview row into its hypermedia resource.</summary>
    /// <param name="row">The application event-overview row to project.</param>
    /// <returns>The API resource with event-overview links.</returns>
    public static EventOverviewResourceResponse From(EventOverviewRow row) =>
        new(row.EventId, row.Date, row.StartTime, row.EndTime,
            row.Capacities, row.ActiveBookings,
            new Dictionary<string, ApiLink>
            {
                ["audit"] = new($"/api/audit/event/{row.EventId}", "GET", "getEventAuditHistory"),
                ["cancel"] = new($"/api/events/{row.EventId}", "DELETE", "cancelEvent"),
            });
}

/// <summary>Event-only operations view plus the collection self affordance.</summary>
public sealed record EventOperationsResourceResponse(
    /// <summary>Gets one row per event with capacity and booking counts.</summary>
    IReadOnlyList<EventOverviewResourceResponse> Events,
    /// <summary>Gets the collection affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one event operations view into its hypermedia resource.</summary>
    /// <param name="view">The application event operations view to project.</param>
    /// <returns>The API resource with operations and row links.</returns>
    public static EventOperationsResourceResponse From(EventOperationsView view) =>
        new(view.Events.Select(EventOverviewResourceResponse.From).ToList(),
            new Dictionary<string, ApiLink>
            {
                ["self"] = new("/api/events/operations", "GET", "getEventOperations"),
                ["board"] = new("/api/events/board", "GET", "getEventBoard"),
            });
}

/// <summary>Coordinator dashboards plus entry affordances for related collections.</summary>
public sealed record DashboardResourceResponse(
    /// <summary>Gets the attendees waiting for availability.</summary>
    IReadOnlyList<AwaitingAvailabilityRow> AwaitingAvailability,
    /// <summary>Gets the attendees needing follow-up after no response.</summary>
    IReadOnlyList<NoResponseRow> NoResponse,
    /// <summary>Gets the event overview rows with capacity and booking counts.</summary>
    IReadOnlyList<EventOverviewResourceResponse> Events,
    /// <summary>Gets the latest email delivery state per attendee.</summary>
    IReadOnlyList<AttendeeEmailStatusView> EmailStatuses,
    /// <summary>Gets the dashboard self and collection entry affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one dashboards view into its hypermedia resource.</summary>
    /// <param name="view">The application dashboards view to project.</param>
    /// <returns>The API resource with dashboard links.</returns>
    public static DashboardResourceResponse From(DashboardsView view) =>
        new(view.AwaitingAvailability, view.NoResponse,
            view.Events.Select(EventOverviewResourceResponse.From).ToList(),
            view.EmailStatuses,
            new Dictionary<string, ApiLink>
            {
                ["self"] = new("/api/dashboards", "GET", "getDashboards"),
                ["attendees"] = new("/api/attendees", "GET", "listAttendees"),
                ["eventOperations"] = new("/api/events/operations", "GET", "getEventOperations"),
                ["audit"] = new("/api/audit/search", "GET", "searchAudit"),
            });
}

/// <summary>One audit history row plus a link when it addresses a real attendee or event route.</summary>
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
    private static readonly IReadOnlySet<string> AttendeeBucket =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AuditEntityTypes.Attendee, AuditEntityTypes.Invite,
            AuditEntityTypes.Booking, AuditEntityTypes.BookingAppointment,
        };

    /// <summary>Projects one audit history row, linking only to real attendee or event routes.</summary>
    /// <param name="row">The application audit history row to project.</param>
    /// <returns>The API resource with a history link or an empty link dictionary.</returns>
    public static AuditHistoryResourceResponse From(AuditHistoryRow row)
    {
        IReadOnlyDictionary<string, ApiLink> links = row.EntityType switch
        {
            AuditEntityTypes.Event => new Dictionary<string, ApiLink>
            {
                ["history"] = new($"/api/audit/event/{row.EntityId}", "GET", "getEventAuditHistory"),
            },
            _ when AttendeeBucket.Contains(row.EntityType) => new Dictionary<string, ApiLink>
            {
                ["history"] = new($"/api/audit/attendee/{row.EntityId}", "GET", "getAttendeeAuditHistory"),
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
