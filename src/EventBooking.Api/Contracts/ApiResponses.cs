using System.Text.Json.Serialization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Attendees;
using EventBooking.Application.Bookings;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Events;
using EventBooking.Application.Negotiation;
using EventBooking.Application.Recovery;
using EventBooking.Application.ReferenceData;
using EventBooking.Application.Settings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Time;

namespace EventBooking.Api.Contracts;

/// <summary>The response records design 05 names, in one file.</summary>
public sealed record LocationResponse(
    Guid Id, string Code, string Name, string Address, string TimeZoneId, bool IsActive,
    long Version,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One appointment type with its current Manager.</summary>
public sealed record AppointmentTypeResponse(
    Guid Id, string Code, string Name, bool IsActive, long Version, string? ManagerDisplayName,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One attendee group with its requirements and member count.</summary>
public sealed record AttendeeGroupResponse(
    Guid Id, string Code, string Name, bool IsActive, long Version,
    IReadOnlyList<Guid> RequirementTypeIds, int MemberCount,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>The singleton settings row.</summary>
public sealed record SettingsResponse(
    int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount, long Version,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One staff access profile with its read-only roles and scope.</summary>
public sealed record StaffAccessResponse(
    Guid StaffUserId, string? StaffId, string? DisplayName, IReadOnlyList<string> Roles,
    Guid? AppointmentTypeId, long Version,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>A scope change, naming the Manager it displaced, if any.</summary>
public sealed record StaffAccessScopeResponse(
    StaffAccessResponse Profile, Guid? DisplacedManagerStaffUserId,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One event capacity row.</summary>
public sealed record EventCapacityResponse(
    Guid AppointmentTypeId, string Code, string Name, int TotalHeadcount, int RemainingCapacity);

/// <summary>One event with its capacities.</summary>
public sealed record EventResponse(
    Guid Id, Guid ProposalId, Guid LocationId, string LocationCode, string LocationName,
    EventTimeResponse Time, string Status, IReadOnlyList<EventCapacityResponse> Capacities,
    int ActiveBookings,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One listed proposal with the caller's relationship to it.</summary>
public sealed record EventProposalResponse(
    Guid Id, Guid LocationId, string LocationCode, string LocationName, EventTimeResponse Time,
    string Status, int ListedTypeCount, int AcceptedTypeCount, int? MyAcceptedHeadcount,
    bool AcceptedByMe, bool CreatedByMe,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One attendee row with its workflow links.</summary>
public sealed record AttendeeResponse(
    Guid AttendeeId, string Name, string Email, string Status, string StatusDisplay,
    string GroupCode, string Readiness, IReadOnlyList<string> RequiredTypeCodes,
    string? LatestDeliveryStatus, string Cursor, Guid? LatestDeliveryId,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One active booking a Coordinator may cancel.</summary>
public sealed record AttendeeBookingResponse(
    Guid BookingId, bool IsOriginal, DateOnly EventDate, TimeOnly EventStartTime,
    TimeOnly EventEndTime,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Minimum canonical detail for one incomplete appointment type.</summary>
public sealed record OutstandingAppointmentTypeResponse(
    string Code, string Name, bool IsRecoverable);

/// <summary>Coordinator-facing readiness for one attendee.</summary>
public sealed record AttendeeReadinessResponse(
    Guid AttendeeId, string Code, string Display,
    IReadOnlyList<OutstandingAppointmentTypeResponse> OutstandingAppointmentTypes,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Coordinator-facing delivery outcome for one started recovery invite.</summary>
public sealed record StartRecoveryResponse(
    Guid RecoveryInviteId, IReadOnlyList<Guid> LocationIds,
    IReadOnlyList<Guid> RecoverableTypeIds,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Coordinator-facing outcome of cancelling one attendee booking.</summary>
public sealed record CancelAttendeeBookingResponse(
    bool ConfirmationRequired, int ActiveBookingCount, Guid? CancelledBookingId,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One dashboard tab with its row count.</summary>
public sealed record DashboardTabResponse<T>(int Count, IReadOnlyList<T> Rows);

/// <summary>One events-tab row with its audit and cancel links.</summary>
public sealed record DashboardEventResponse(
    Guid EventId, Guid LocationId, string LocationName, DateOnly Date, TimeOnly StartTime,
    TimeOnly EndTime, IReadOnlyList<EventCapacityRow> Capacities, int ActiveBookings,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Coordinator dashboards plus entry links for related collections.</summary>
public sealed record DashboardsResponse(
    DashboardTabResponse<AwaitingAvailabilityRow> AwaitingAvailability,
    DashboardTabResponse<NoResponseRow> NoResponse,
    DashboardTabResponse<DashboardEventResponse> Events,
    int FailedEmails, int PendingEmails,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One workspace event row with its roster link.</summary>
public sealed record WorkspaceEventResponse(
    Guid EventId, Guid LocationId, string LocationName, DateOnly Date,
    TimeOnly StartTime, TimeOnly EndTime, string ZoneAbbreviation, string Status,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One appointment's row-local state after a status change.</summary>
public sealed record AppointmentStatusResponse(
    Guid BookingAppointmentId, string Status, DateTimeOffset? CheckedInAt,
    DateTimeOffset? OutcomeAt, long Version,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Attendee-facing invite options plus the confirm affordance.</summary>
public sealed record InviteResponse(
    Guid InviteId, string AttendeeName, IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionView> Options, bool IsRecovery,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Attendee-facing managed booking plus the cancel affordance.</summary>
public sealed record ManagedBookingResponse(
    DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Display, string AttendeeName,
    bool CanCancel,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One audit row.</summary>
public sealed record AuditRowResponse(
    DateTimeOffset Timestamp, string EntityType, Guid EntityId, string Action, string ActorType,
    string? ActorId, string? Details,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>Projects application results onto design 05's response bodies.</summary>
public static class ApiResponses
{
    /// <summary>Projects one location, with the links its caller may follow.</summary>
    /// <param name="result">The application result.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static LocationResponse Location(
        LocationResult result, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new LocationResponse(
            result.Id, result.Code, result.Name, result.Address, result.TimeZoneId,
            result.IsActive, result.Version,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listLocations", "/api/locations", null),
                new LinkCandidate(
                    "update", "updateLocation", $"/api/locations/{result.Id}",
                    nameof(StaffCapability.ManageReferenceData))));
    }

    /// <summary>Projects one appointment type.</summary>
    /// <param name="result">The application result.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static AppointmentTypeResponse AppointmentType(
        AppointmentTypeResult result, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new AppointmentTypeResponse(
            result.Id, result.Code, result.Name, result.IsActive, result.Version,
            result.ManagerDisplayName,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listAppointmentTypes", "/api/appointment-types", null),
                new LinkCandidate(
                    "update", "updateAppointmentType", $"/api/appointment-types/{result.Id}",
                    nameof(StaffCapability.ManageReferenceData))));
    }

    /// <summary>Projects one attendee group.</summary>
    /// <param name="result">The application result.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static AttendeeGroupResponse AttendeeGroup(
        AttendeeGroupResult result, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new AttendeeGroupResponse(
            result.Id, result.Code, result.Name, result.IsActive, result.Version,
            result.RequirementTypeIds, result.MemberCount,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listAttendeeGroups", "/api/attendee-groups", null),
                new LinkCandidate(
                    "update", "updateAttendeeGroup", $"/api/attendee-groups/{result.Id}",
                    nameof(StaffCapability.ManageReferenceData))));
    }

    /// <summary>Projects the settings row.</summary>
    /// <param name="result">The application result.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static SettingsResponse Settings(
        SystemSettingsResult result, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new SettingsResponse(
            result.InviteExpiryDays, result.MaxAutoRetryCount, result.InviteOptionCount,
            result.Version,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "getSettings", "/api/settings", null),
                new LinkCandidate(
                    "update", "updateSettings", "/api/settings",
                    nameof(StaffCapability.ManageSettings))));
    }

    /// <summary>Projects the settings row read.</summary>
    /// <param name="view">The application view.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static SettingsResponse Settings(
        SettingsView view, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new SettingsResponse(
            view.InviteExpiryDays, view.MaxAutoRetryCount, view.InviteOptionCount,
            view.Version,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "getSettings", "/api/settings", null),
                new LinkCandidate(
                    "update", "updateSettings", "/api/settings",
                    nameof(StaffCapability.ManageSettings))));
    }

    /// <summary>Projects one staff access profile.</summary>
    /// <param name="view">The application view.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static StaffAccessResponse StaffAccess(
        StaffAccessProfileView view, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new StaffAccessResponse(
            view.StaffUserId, view.StaffId?.Value, view.DisplayName,
            [.. view.Roles.Select(role => role.ToString())],
            view.AppointmentTypeId, view.Version,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listStaffAccess", "/api/staff-access", null),
                new LinkCandidate(
                    "setScope", "setStaffAccessScope",
                    $"/api/staff-access/{view.StaffUserId}/scope",
                    nameof(StaffCapability.ManageStaffAccess))));
    }

    /// <summary>Projects a scope change, naming any displaced Manager.</summary>
    /// <param name="view">The application view.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static StaffAccessScopeResponse StaffAccessScope(
        StaffAccessMutationView view, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(view);
        var profile = StaffAccess(view.Profile, capabilities);
        return new StaffAccessScopeResponse(
            profile, view.FormerManagerStaffUserId, profile.Links);
    }

    /// <summary>Projects one event, deriving its time in the location's own zone.</summary>
    /// <param name="view">The read model.</param>
    /// <param name="zones">The zone resolver.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static EventResponse Event(
        EventView view, IEventWindowZones zones, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new EventResponse(
            view.EventId, view.ProposalId, view.LocationId, view.LocationCode, view.LocationName,
            EventTimeResponse.From(
                view.Date, view.StartTime, view.DurationMinutes, view.TimeZoneId, zones),
            view.Status,
            [.. view.Capacities.Select(c => new EventCapacityResponse(
                c.AppointmentTypeId, c.Code, c.Name, c.TotalHeadcount, c.RemainingCapacity))],
            view.ActiveBookings,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "getEvent", $"/api/events/{view.EventId}", null),
                new LinkCandidate(
                    "cancel", "cancelEvent", $"/api/events/{view.EventId}/cancel",
                    nameof(StaffCapability.CancelEvent)),
                new LinkCandidate(
                    "roster", "getWorkspaceRoster",
                    $"/api/appointment-workspace/events/{view.EventId}",
                    nameof(StaffCapability.ConductAppointments))));
    }

    /// <summary>Projects one attendee row. The row cursor is signed by the endpoint.</summary>
    /// <param name="item">The application row.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response row.</returns>
    public static AttendeeResponse Attendee(
        AttendeeListItem item, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(item);
        var status = Enum.Parse<AttendeeStatus>(item.Status);
        return new AttendeeResponse(
            item.AttendeeId, item.Name, item.Email, item.Status, AttendeeStatusDisplay(status),
            item.GroupCode, item.Readiness, item.RequiredTypeCodes, item.LatestDeliveryStatus,
            item.Cursor, item.LatestDeliveryId,
            AttendeeLinks(item.AttendeeId, status, capabilities));
    }

    /// <summary>The Coordinator-facing wording for each attendee status.</summary>
    /// <param name="status">The status.</param>
    public static string AttendeeStatusDisplay(AttendeeStatus status) => status switch
    {
        AttendeeStatus.NotYetInvited => "Not yet invited",
        AttendeeStatus.AwaitingAvailability => "Awaiting availability",
        AttendeeStatus.Invited => "Invited (pending response)",
        AttendeeStatus.Booked => "Booked",
        AttendeeStatus.NoResponseNeedsFollowUp => "No response - needs follow-up",
        _ => status.ToString(),
    };

    /// <summary>Projects one active booking with its cancellation link.</summary>
    /// <param name="attendeeId">The owning attendee.</param>
    /// <param name="row">The application row.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response row.</returns>
    public static AttendeeBookingResponse AttendeeBooking(
        Guid attendeeId, AttendeeBookingSummary row, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new AttendeeBookingResponse(
            row.BookingId, row.IsOriginal, row.EventDate, row.EventStartTime, row.EventEndTime,
            CallerLinks.For(
                capabilities,
                new LinkCandidate(
                    "cancel", "cancelAttendeeBooking",
                    $"/api/attendees/{attendeeId}/bookings/{row.BookingId}/cancel",
                    nameof(StaffCapability.ManageAttendees))));
    }

    /// <summary>Projects one attendee's readiness.</summary>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="readiness">The application readiness.</param>
    /// <param name="display">The Coordinator-facing explanation.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static AttendeeReadinessResponse AttendeeReadiness(
        Guid attendeeId, Application.Attendees.AttendeeReadiness readiness, string display,
        IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        var candidates = new List<LinkCandidate>
        {
            new("bookings", "listAttendeeBookings", $"/api/attendees/{attendeeId}/bookings",
                nameof(StaffCapability.ManageAttendees)),
            new("readiness", "getAttendeeReadiness", $"/api/attendees/{attendeeId}/readiness",
                nameof(StaffCapability.ViewAttendeeDashboards)),
        };
        if (readiness.OutstandingAppointmentTypes.Any(type => type.IsRecoverable))
        {
            candidates.Add(new LinkCandidate(
                "startRecovery", "startRecoveryInvite",
                $"/api/attendees/{attendeeId}/recovery-invites",
                nameof(StaffCapability.ManageAttendees)));
        }

        return new AttendeeReadinessResponse(
            readiness.AttendeeId, readiness.Code.ToString(), display,
            [.. readiness.OutstandingAppointmentTypes.Select(type =>
                new OutstandingAppointmentTypeResponse(type.Code, type.Name, type.IsRecoverable))],
            CallerLinks.For(capabilities, [.. candidates]));
    }

    /// <summary>Projects one started recovery invite. No follow-up route exists for a recovery
    /// invite itself, so the link map is honestly empty.</summary>
    /// <param name="outcome">The application outcome.</param>
    /// <returns>The response body.</returns>
    public static StartRecoveryResponse RecoveryStarted(StartRecoveryOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new StartRecoveryResponse(
            outcome.RecoveryInviteId, outcome.LocationIds, outcome.RecoverableTypeIds,
            new Dictionary<string, ApiLink>(StringComparer.Ordinal));
    }

    /// <summary>Projects a confirmed booking cancellation.</summary>
    /// <param name="attendeeId">The attendee.</param>
    /// <param name="outcome">The application outcome.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static CancelAttendeeBookingResponse BookingCancelled(
        Guid attendeeId, CoordinatorCancelOutcome outcome, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new CancelAttendeeBookingResponse(
            outcome.ConfirmationRequired, outcome.ActiveBookingCount, outcome.CancelledBookingId,
            AttendeeLinks(attendeeId, AttendeeStatus.Booked, capabilities));
    }

    private static IReadOnlyDictionary<string, ApiLink> AttendeeLinks(
        Guid attendeeId, AttendeeStatus status, IReadOnlySet<string> capabilities)
    {
        var candidates = new List<LinkCandidate>
        {
            new("bookings", "listAttendeeBookings", $"/api/attendees/{attendeeId}/bookings",
                nameof(StaffCapability.ManageAttendees)),
            new("readiness", "getAttendeeReadiness", $"/api/attendees/{attendeeId}/readiness",
                nameof(StaffCapability.ViewAttendeeDashboards)),
            new("audit", "getAttendeeAuditHistory", $"/api/audit/attendees/{attendeeId}",
                nameof(StaffCapability.ViewAttendeeAudit)),
            new("update", "updateAttendee", $"/api/attendees/{attendeeId}",
                nameof(StaffCapability.ManageAttendees)),
            new("delete", "deleteAttendee", $"/api/attendees/{attendeeId}",
                nameof(StaffCapability.ManageAttendees)),
            new("emailRetry", "retryAttendeeEmail", $"/api/attendees/{attendeeId}/email-retry",
                nameof(StaffCapability.ManageAttendees)),
        };
        if (status is AttendeeStatus.NotYetInvited or AttendeeStatus.AwaitingAvailability
            or AttendeeStatus.NoResponseNeedsFollowUp)
        {
            candidates.Add(new LinkCandidate(
                "invite", "inviteAttendee", $"/api/attendees/{attendeeId}/invites",
                nameof(StaffCapability.ManageAttendees)));
        }

        return CallerLinks.For(capabilities, [.. candidates]);
    }

    /// <summary>Projects the dashboards view with entry links.</summary>
    /// <param name="view">The application view.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static DashboardsResponse Dashboards(
        DashboardsView view, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new DashboardsResponse(
            new DashboardTabResponse<AwaitingAvailabilityRow>(
                view.AwaitingAvailability.Count, view.AwaitingAvailability.Rows),
            new DashboardTabResponse<NoResponseRow>(
                view.NoResponse.Count, view.NoResponse.Rows),
            new DashboardTabResponse<DashboardEventResponse>(
                view.Events.Count,
                [.. view.Events.Rows.Select(row => DashboardEvent(row, capabilities))]),
            view.FailedEmails,
            view.PendingEmails,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "getDashboards", "/api/dashboards", null),
                new LinkCandidate(
                    "attendees", "listAttendees", "/api/attendees",
                    nameof(StaffCapability.ManageAttendees)),
                new LinkCandidate("events", "listEvents", "/api/events", null),
                new LinkCandidate("audit", "searchAudit", "/api/audit", null)));
    }

    private static DashboardEventResponse DashboardEvent(
        EventOverviewRow row, IReadOnlySet<string> capabilities) =>
        new(row.EventId, row.LocationId, row.LocationName,
            row.Date, row.StartTime, row.EndTime,
            row.Capacities, row.ActiveBookings,
            CallerLinks.For(
                capabilities,
                new LinkCandidate(
                    "audit", "getEventAuditHistory", $"/api/audit/events/{row.EventId}",
                    nameof(StaffCapability.ViewEventAudit)),
                new LinkCandidate(
                    "cancel", "cancelEvent", $"/api/events/{row.EventId}/cancel",
                    nameof(StaffCapability.CancelEvent))));

    /// <summary>Projects one audit row, linking only to real attendee or event routes.</summary>
    /// <param name="row">The application row.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response row.</returns>
    public static AuditRowResponse AuditRow(
        AuditHistoryRow row, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(row);
        var candidates = row.EntityType switch
        {
            AuditEntityTypes.Event => (IReadOnlyList<LinkCandidate>)
            [
                new LinkCandidate(
                    "history", "getEventAuditHistory", $"/api/audit/events/{row.EntityId}",
                    nameof(StaffCapability.ViewEventAudit)),
            ],
            AuditEntityTypes.Attendee or AuditEntityTypes.Invite or AuditEntityTypes.Booking
                or AuditEntityTypes.BookingAppointment => (IReadOnlyList<LinkCandidate>)
            [
                new LinkCandidate(
                    "history", "getAttendeeAuditHistory", $"/api/audit/attendees/{row.EntityId}",
                    nameof(StaffCapability.ViewAttendeeAudit)),
            ],
            _ => [],
        };
        return new AuditRowResponse(
            row.Timestamp, row.EntityType, row.EntityId, row.Action,
            row.ActorType, row.ActorId, row.Details,
            CallerLinks.For(capabilities, [.. candidates]));
    }

    /// <summary>Projects one workspace event row.</summary>
    /// <param name="view">The application row.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response row.</returns>
    public static WorkspaceEventResponse WorkspaceEvent(
        WorkspaceEventView view, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new WorkspaceEventResponse(
            view.EventId, view.LocationId, view.LocationName, view.Date,
            view.StartTime, view.EndTime, view.ZoneAbbreviation, view.Status,
            CallerLinks.For(
                capabilities,
                new LinkCandidate(
                    "roster", "getWorkspaceRoster",
                    $"/api/appointment-workspace/events/{view.EventId}",
                    nameof(StaffCapability.ConductAppointments)),
                new LinkCandidate(
                    "rosterCsv", "exportWorkspaceRoster",
                    $"/api/appointment-workspace/events/{view.EventId}/roster.csv",
                    nameof(StaffCapability.ConductAppointments))));
    }

    /// <summary>Projects one appointment's row-local state with its update affordance.</summary>
    /// <param name="view">The application view.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static AppointmentStatusResponse AppointmentStatus(
        BookingAppointmentUpdateView view, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new AppointmentStatusResponse(
            view.BookingAppointmentId, view.Status.ToString(), view.CheckedInAt,
            view.OutcomeAt, view.Version,
            CallerLinks.For(
                capabilities,
                new LinkCandidate(
                    "updateStatus", "setAppointmentStatus",
                    $"/api/appointment-workspace/appointments/{view.BookingAppointmentId}/status",
                    nameof(StaffCapability.ConductAppointments))));
    }

    /// <summary>Projects one invite view with its confirm link.</summary>
    /// <param name="view">The application view.</param>
    /// <param name="token">The raw invite token, embedded only in the confirm href.</param>
    /// <returns>The response body.</returns>
    public static InviteResponse Invite(InviteView view, string token)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new InviteResponse(
            view.InviteId, view.AttendeeName, view.AppointmentTypeNames,
            view.Options, view.IsRecovery,
            new Dictionary<string, ApiLink>(StringComparer.Ordinal)
            {
                ["confirm"] = new(
                    $"/api/booking/{token}/confirm", HttpMethods.Post, "confirmBooking"),
            });
    }

    /// <summary>Projects one booking view with its cancel link.</summary>
    /// <param name="view">The application view.</param>
    /// <param name="token">The raw manage token, embedded only in the cancel href.</param>
    /// <returns>The response body.</returns>
    public static ManagedBookingResponse ManagedBooking(BookingView view, string token)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ManagedBookingResponse(
            view.Date, view.StartTime, view.EndTime, view.Display, view.AttendeeName,
            view.CanCancel,
            new Dictionary<string, ApiLink>(StringComparer.Ordinal)
            {
                ["cancel"] = new(
                    $"/api/manage/{token}/cancel", HttpMethods.Post, "cancelManagedBooking"),
            });
    }

    /// <summary>Projects one proposal.</summary>
    /// <param name="item">The read model.</param>
    /// <param name="zones">The zone resolver.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static EventProposalResponse EventProposal(
        EventProposalListItem item, IEventWindowZones zones, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new EventProposalResponse(
            item.ProposalId, item.LocationId, item.LocationCode, item.LocationName,
            EventTimeResponse.From(
                item.Date, item.StartTime, item.DurationMinutes, item.TimeZoneId, zones),
            item.Status, item.ListedTypeCount, item.AcceptedTypeCount, item.MyAcceptedHeadcount,
            item.AcceptedByMe, item.CreatedByMe,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listEventProposals", "/api/event-proposals", null),
                new LinkCandidate(
                    "accept", "recordAcceptance",
                    $"/api/event-proposals/{item.ProposalId}/acceptance",
                    nameof(StaffCapability.ManageEventNegotiation)),
                new LinkCandidate(
                    "withdraw", "withdrawProposal",
                    $"/api/event-proposals/{item.ProposalId}/withdraw",
                    nameof(StaffCapability.ManageEventNegotiation))));
    }
}
