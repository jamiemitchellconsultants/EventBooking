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
    Guid Id, string Code, string Name, bool IsActive, long Version, bool HasManager,
    string? ManagerDisplayName,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One attendee group with its requirements and member count.</summary>
public sealed record AttendeeGroupResponse(
    Guid Id, string Code, string Name, string Description, bool IsActive, long Version,
    IReadOnlyList<Guid> RequirementTypeIds, int MemberCount,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One event membership with its own publication gate.</summary>
public sealed record EventGroupEventResponse(
    Guid EventId, bool IsOpen,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One attendee-group choice with its public copy.</summary>
public sealed record PublicAttendeeGroupChoiceResponse(
    Guid AttendeeGroupId, string Name, string Description);

/// <summary>One open event choice with its public copy.</summary>
public sealed record PublicEventChoiceResponse(
    Guid EventId, string LocationName, string Address, EventTimeResponse EventTime,
    IReadOnlyList<string> AppointmentTypeCodes);

/// <summary>One open event group with its public choices.</summary>
public sealed record PublicEventGroupResponse(
    Guid Id, string Title, string Description, long Version,
    IReadOnlyList<PublicAttendeeGroupChoiceResponse> AttendeeGroups,
    IReadOnlyList<PublicEventChoiceResponse> Events);

/// <summary>The submitted self-registration request with its confirmation token.</summary>
public sealed record SubmitSelfRegistrationResponse(
    Guid RequestId, Guid EventGroupId, Guid EventId, Guid AttendeeGroupId,
    string Name, string Email, DateTimeOffset ExpiresAt, string ConfirmationToken);

/// <summary>One pending self-registration request's public summary.</summary>
public sealed record SelfRegistrationSummaryResponse(
    Guid EventGroupId, Guid EventId, string EventGroupTitle, string LocationName,
    DateOnly Date, TimeOnly StartTime, string AttendeeGroupName);

/// <summary>The booking one confirmed self-registration request created.</summary>
public sealed record ConfirmSelfRegistrationResponse(Guid BookingId);

/// <summary>One event group with its selected groups and memberships.</summary>
public sealed record EventGroupResponse(
    Guid Id, string Title, string Description, bool IsOpen, long Version,
    IReadOnlyList<Guid> AttendeeGroupIds, IReadOnlyList<Guid> AppointmentTypeIds,
    IReadOnlyList<EventGroupEventResponse> Events,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>The singleton settings row.</summary>
public sealed record SettingsResponse(
    int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount,
    int PendingRegistrationExpiryHours, long Version,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One staff access profile with its read-only roles and scope.</summary>
public sealed record StaffAccessResponse(
    Guid StaffUserId, string? StaffId, string? DisplayName, IReadOnlyList<string> Roles,
    Guid? AppointmentTypeId, long Version,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One event capacity row.</summary>
public sealed record EventCapacityResponse(
    Guid AppointmentTypeId, string Code, string Name, int TotalHeadcount, int RemainingCapacity,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One listed type on a proposal row.</summary>
public sealed record ProposalTypeResponse(string Code, string Name);

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
    bool AcceptedByMe, bool CreatedByMe, IReadOnlyList<ProposalTypeResponse> Types,
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

/// <summary>How many events an invite could offer, and how many it needs.</summary>
public sealed record EligibleEventCountResponse(int Count, int RequiredOptionCount);

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
    Guid EventId, Guid LocationId, string LocationName, EventTimeResponse Time,
    IReadOnlyList<EventCapacityRow> Capacities, int ActiveBookings,
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
    Guid EventId, Guid LocationId, string LocationName, EventTimeResponse Time, string Status,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One roster row with the status actions currently available for it.</summary>
public sealed record WorkspaceRosterRowResponse(
    Guid AppointmentId, string Name, string Email, string ScopeTypeCode,
    string AppointmentStatus, DateTimeOffset? CheckedInAt, long Version,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One appointment's row-local state after a status change.</summary>
public sealed record AppointmentStatusResponse(
    Guid BookingAppointmentId, string Status, DateTimeOffset? CheckedInAt,
    DateTimeOffset? OutcomeAt, long Version,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One invite option: where the event is and when it runs.</summary>
public sealed record InviteOptionResponse(
    Guid EventId, string LocationName, string Address, EventTimeResponse Time);

/// <summary>Attendee-facing invite options plus the confirm affordance.</summary>
public sealed record InviteResponse(
    Guid InviteId, string AttendeeName, IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionResponse> Options, bool IsRecovery,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>The booking confirmation: the booking id plus its manage link secret.</summary>
public sealed record ConfirmBookingResponse(Guid BookingId, string ManageToken);

/// <summary>Attendee-facing managed booking plus the cancel affordance.</summary>
public sealed record ManagedBookingResponse(
    string AttendeeName, string LocationName, string Address, EventTimeResponse Time,
    IReadOnlyList<string> AppointmentTypeNames,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>One audit row.</summary>
public sealed record AuditRowResponse(
    Guid Id, DateTimeOffset Timestamp, string EntityType, Guid EntityId, string Action,
    string ActorType, string? ActorId, string? ActorDisplay, string? Details,
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
    /// <param name="hasManager">Whether a current Manager profile scopes to this type.</param>
    /// <returns>The response body.</returns>
    public static AppointmentTypeResponse AppointmentType(
        AppointmentTypeResult result, IReadOnlySet<string> capabilities, bool hasManager)
    {
        ArgumentNullException.ThrowIfNull(result);
        return AppointmentType(
            result.Id, result.Code, result.Name, result.IsActive, result.Version,
            hasManager, result.ManagerDisplayName, capabilities);
    }

    /// <summary>Projects one appointment-type list row as the full response shape.</summary>
    /// <param name="item">The application row.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static AppointmentTypeResponse AppointmentType(
        AppointmentTypeListItem item, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(item);
        return AppointmentType(
            item.Id, item.Code, item.Name, item.IsActive, item.Version,
            item.HasManager, item.ManagerDisplayName, capabilities);
    }

    private static AppointmentTypeResponse AppointmentType(
        Guid id, string code, string name, bool isActive, long version,
        bool hasManager, string? managerDisplayName, IReadOnlySet<string> capabilities) =>
        new(
            id, code, name, isActive, version, hasManager, managerDisplayName,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listAppointmentTypes", "/api/appointment-types", null),
                new LinkCandidate(
                    "update", "updateAppointmentType", $"/api/appointment-types/{id}",
                    nameof(StaffCapability.ManageReferenceData))));

    /// <summary>Projects one location list row as the full response shape.</summary>
    /// <param name="item">The application row.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static LocationResponse Location(
        LocationListItem item, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new LocationResponse(
            item.Id, item.Code, item.Name, item.Address, item.TimeZoneId,
            item.IsActive, item.Version,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listLocations", "/api/locations", null),
                new LinkCandidate(
                    "update", "updateLocation", $"/api/locations/{item.Id}",
                    nameof(StaffCapability.ManageReferenceData))));
    }

    /// <summary>Projects one attendee-group list row as the full response shape.</summary>
    /// <param name="item">The application row.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static AttendeeGroupResponse AttendeeGroup(
        AttendeeGroupListItem item, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new AttendeeGroupResponse(
            item.Id, item.Code, item.Name, item.Description, item.IsActive, item.Version,
            item.RequirementTypeIds, item.MemberCount,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listAttendeeGroups", "/api/attendee-groups", null),
                new LinkCandidate(
                    "update", "updateAttendeeGroup", $"/api/attendee-groups/{item.Id}",
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
            result.Id, result.Code, result.Name, result.Description, result.IsActive, result.Version,
            result.RequirementTypeIds, result.MemberCount,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listAttendeeGroups", "/api/attendee-groups", null),
                new LinkCandidate(
                    "update", "updateAttendeeGroup", $"/api/attendee-groups/{result.Id}",
                    nameof(StaffCapability.ManageReferenceData))));
    }

    /// <summary>Projects one event group, with mutation links only for holders.</summary>
    /// <param name="result">The application result.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response body.</returns>
    public static EventGroupResponse EventGroup(
        EventBooking.Application.EventGroups.EventGroupResult result,
        IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new EventGroupResponse(
            result.Id, result.Title, result.Description, result.IsOpen, result.Version,
            result.AttendeeGroupIds, result.AppointmentTypeIds,
            [.. result.Events.Select(e => new EventGroupEventResponse(
                e.EventId, e.IsOpen,
                CallerLinks.For(
                    capabilities,
                    new LinkCandidate(
                        "setEventOpen", "setEventGroupEventOpen",
                        $"/api/event-groups/{result.Id}/events/{e.EventId}",
                        nameof(StaffCapability.ManageEventGroups)),
                    new LinkCandidate(
                        "removeEvent", "removeEventGroupEvent",
                        $"/api/event-groups/{result.Id}/events/{e.EventId}",
                        nameof(StaffCapability.ManageEventGroups)))))],
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "getEventGroup", $"/api/event-groups/{result.Id}", null),
                new LinkCandidate(
                    "update", "updateEventGroup", $"/api/event-groups/{result.Id}",
                    nameof(StaffCapability.ManageEventGroups)),
                new LinkCandidate(
                    "addEvent", "addEventGroupEvent", $"/api/event-groups/{result.Id}/events",
                    nameof(StaffCapability.ManageEventGroups))));
    }

    /// <summary>Projects one open event group with its public choices.</summary>
    /// <param name="result">The application result.</param>
    /// <param name="zones">The zone abstraction.</param>
    /// <returns>The response body.</returns>
    public static PublicEventGroupResponse PublicEventGroup(
        EventBooking.Application.SelfRegistrations.PublicEventGroupResult result,
        IEventWindowZones zones)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new PublicEventGroupResponse(
            result.Id, result.Title, result.Description, result.Version,
            [.. result.AttendeeGroups.Select(x =>
                new PublicAttendeeGroupChoiceResponse(x.AttendeeGroupId, x.Name, x.Description))],
            [.. result.Events.Select(x => new PublicEventChoiceResponse(
                x.EventId, x.LocationName, x.Address,
                EventTimeResponse.From(
                    x.Date, x.StartTime, x.DurationMinutes, x.TimeZoneId, zones),
                x.AppointmentTypeCodes))]);
    }

    /// <summary>Projects one submitted self-registration request.</summary>
    /// <param name="result">The application result.</param>
    /// <returns>The response body.</returns>
    public static SubmitSelfRegistrationResponse SubmittedRegistration(
        EventBooking.Application.SelfRegistrations.SubmitSelfRegistrationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new SubmitSelfRegistrationResponse(
            result.RequestId, result.EventGroupId, result.EventId, result.AttendeeGroupId,
            result.Name, result.Email, result.ExpiresAt, result.ConfirmationToken);
    }

    /// <summary>Projects one pending self-registration request's public summary.</summary>
    /// <param name="summary">The application summary.</param>
    /// <returns>The response body.</returns>
    public static SelfRegistrationSummaryResponse SelfRegistrationSummary(
        EventBooking.Application.SelfRegistrations.SelfRegistrationSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new SelfRegistrationSummaryResponse(
            summary.EventGroupId, summary.EventId, summary.EventGroupTitle,
            summary.LocationName, summary.Date, summary.StartTime,
            summary.AttendeeGroupName);
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
            result.PendingRegistrationExpiryHours, result.Version,
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
            view.PendingRegistrationExpiryHours, view.Version,
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

    /// <summary>Projects a scope change, naming any displaced Manager by display name.</summary>
    /// <param name="view">The application view.</param>
    /// <returns>The response body.</returns>
    public static Application.Access.SetStaffScopeOutcome StaffAccessScope(
        StaffAccessMutationView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new Application.Access.SetStaffScopeOutcome(
            view.Profile.StaffUserId, view.Profile.AppointmentTypeId,
            view.FormerManagerDisplayName);
    }

    /// <summary>Projects one event, deriving its time in the location's own zone.</summary>
    /// <param name="view">The read model.</param>
    /// <param name="zones">The zone resolver.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <param name="scopeAppointmentTypeId">The caller's scoped type, or null when unscoped.</param>
    /// <returns>The response body.</returns>
    public static EventResponse Event(
        EventView view, IEventWindowZones zones, IReadOnlySet<string> capabilities,
        Guid? scopeAppointmentTypeId)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new EventResponse(
            view.EventId, view.ProposalId, view.LocationId, view.LocationCode, view.LocationName,
            EventTimeResponse.From(
                view.Date, view.StartTime, view.DurationMinutes, view.TimeZoneId, zones),
            view.Status,
            [.. view.Capacities.Select(c => new EventCapacityResponse(
                c.AppointmentTypeId, c.Code, c.Name, c.TotalHeadcount, c.RemainingCapacity,
                CapacityLinks(view.EventId, c.AppointmentTypeId, capabilities, scopeAppointmentTypeId)))],
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

    private static IReadOnlyDictionary<string, ApiLink> CapacityLinks(
        Guid eventId, Guid appointmentTypeId, IReadOnlySet<string> capabilities,
        Guid? scopeAppointmentTypeId)
    {
        // The handler refuses any row but the caller's own, so only that row advertises
        // the adjust action. Another type's headcount stays visible, never editable.
        if (!capabilities.Contains(nameof(StaffCapability.ManageEventNegotiation))
            || scopeAppointmentTypeId is null
            || scopeAppointmentTypeId != appointmentTypeId)
        {
            return new Dictionary<string, ApiLink>(StringComparer.Ordinal);
        }

        return CallerLinks.For(
            capabilities,
            new LinkCandidate(
                "adjust", "adjustEventCapacity",
                $"/api/events/{eventId}/capacities/{appointmentTypeId}",
                nameof(StaffCapability.ManageEventNegotiation)));
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
        DashboardsView view, IEventWindowZones zones, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new DashboardsResponse(
            new DashboardTabResponse<AwaitingAvailabilityRow>(
                view.AwaitingAvailability.Count, view.AwaitingAvailability.Rows),
            new DashboardTabResponse<NoResponseRow>(
                view.NoResponse.Count, view.NoResponse.Rows),
            new DashboardTabResponse<DashboardEventResponse>(
                view.Events.Count,
                [.. view.Events.Rows.Select(row => DashboardEvent(row, zones, capabilities))]),
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
        EventOverviewRow row, IEventWindowZones zones, IReadOnlySet<string> capabilities) =>
        new(row.EventId, row.LocationId, row.LocationName,
            EventTimeResponse.From(
                row.Date, row.StartTime, row.DurationMinutes, row.TimeZoneId, zones),
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
    /// <param name="actorDisplay">The staff actor's display name, or null.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response row.</returns>
    public static AuditRowResponse AuditRow(
        AuditHistoryRow row, string? actorDisplay, IReadOnlySet<string> capabilities)
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
            row.Id, row.Timestamp, row.EntityType, row.EntityId, row.Action,
            row.ActorType, row.ActorId, actorDisplay, row.Details,
            CallerLinks.For(capabilities, [.. candidates]));
    }

    /// <summary>Projects one workspace event row.</summary>
    /// <param name="view">The application row.</param>
    /// <param name="zones">The zone resolver.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response row.</returns>
    public static WorkspaceEventResponse WorkspaceEvent(
        WorkspaceEventView view, IEventWindowZones zones, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new WorkspaceEventResponse(
            view.EventId, view.LocationId, view.LocationName,
            EventTimeResponse.From(
                view.Date, view.StartTime, view.DurationMinutes, view.TimeZoneId, zones),
            view.Status,
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

    /// <summary>Projects one roster row with the links its state and the event window allow.</summary>
    /// <param name="row">The application row.</param>
    /// <param name="checkInAllowed">Whether the event date is the local date.</param>
    /// <param name="noShowAllowed">Whether the event window has ended locally.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response row.</returns>
    public static WorkspaceRosterRowResponse WorkspaceRosterRow(
        WorkspaceRosterRow row, bool checkInAllowed, bool noShowAllowed,
        IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(row);
        var candidates = new List<LinkCandidate>();
        var href = $"/api/appointment-workspace/appointments/{row.AppointmentId}/status";
        if (string.Equals(row.AppointmentStatus, "Expected", StringComparison.Ordinal))
        {
            if (checkInAllowed)
            {
                candidates.Add(new LinkCandidate(
                    "checkIn", "setAppointmentStatus", href,
                    nameof(StaffCapability.ConductAppointments)));
            }

            if (noShowAllowed)
            {
                candidates.Add(new LinkCandidate(
                    "noShow", "setAppointmentStatus", href,
                    nameof(StaffCapability.ConductAppointments)));
            }
        }
        else if (string.Equals(row.AppointmentStatus, "CheckedIn", StringComparison.Ordinal))
        {
            candidates.Add(new LinkCandidate(
                "complete", "setAppointmentStatus", href,
                nameof(StaffCapability.ConductAppointments)));
            candidates.Add(new LinkCandidate(
                "reopen", "setAppointmentStatus", href,
                nameof(StaffCapability.ConductAppointments)));
        }
        else if (string.Equals(row.AppointmentStatus, "Completed", StringComparison.Ordinal)
            || string.Equals(row.AppointmentStatus, "NoShow", StringComparison.Ordinal))
        {
            // The bounded correction back towards Expected. A NoShow covered by a later
            // recovery still advertises it: the handler refuses with recovery-active,
            // which the caller could not have known from this row.
            candidates.Add(new LinkCandidate(
                "reopen", "setAppointmentStatus", href,
                nameof(StaffCapability.ConductAppointments)));
        }

        return new WorkspaceRosterRowResponse(
            row.AppointmentId, row.Name, row.Email, row.ScopeTypeCode,
            row.AppointmentStatus, row.CheckedInAt, row.Version,
            CallerLinks.For(capabilities, [.. candidates]));
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
    public static InviteResponse Invite(
        InviteView view, IEventWindowZones zones, string token)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new InviteResponse(
            view.InviteId, view.AttendeeName, view.AppointmentTypeNames,
            [.. view.Options.Select(option => new InviteOptionResponse(
                option.EventId, option.LocationName, option.Address,
                EventTimeResponse.From(
                    option.Date, option.StartTime, option.DurationMinutes,
                    option.TimeZoneId, zones)))],
            view.IsRecovery,
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
    public static ManagedBookingResponse ManagedBooking(
        BookingView view, IEventWindowZones zones, string token)
    {
        ArgumentNullException.ThrowIfNull(view);
        var links = new Dictionary<string, ApiLink>(StringComparer.Ordinal);
        if (view.CanCancel)
        {
            links["cancel"] = new(
                $"/api/manage/{token}/cancel", HttpMethods.Post, "cancelManagedBooking");
        }

        return new ManagedBookingResponse(
            view.AttendeeName, view.LocationName, view.Address,
            EventTimeResponse.From(
                view.Date, view.StartTime, view.DurationMinutes, view.TimeZoneId, zones),
            view.AppointmentTypeNames,
            links);
    }

    /// <summary>Projects one proposal.</summary>
    /// <param name="item">The read model.</param>
    /// <param name="zones">The zone resolver.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <param name="scopeAppointmentTypeId">The caller's scoped type, or null when unscoped.</param>
    /// <returns>The response body.</returns>
    public static EventProposalResponse EventProposal(
        EventProposalListItem item, IEventWindowZones zones, IReadOnlySet<string> capabilities,
        Guid? scopeAppointmentTypeId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var candidates = new List<LinkCandidate>
        {
            new("self", "listEventProposals", "/api/event-proposals", null),
        };
        if (string.Equals(item.Status, "Open", StringComparison.Ordinal))
        {
            if (!item.AcceptedByMe)
            {
                candidates.Add(new LinkCandidate(
                    "accept", "recordAcceptance",
                    $"/api/event-proposals/{item.ProposalId}/acceptance",
                    nameof(StaffCapability.ManageEventNegotiation)));
            }
            else
            {
                candidates.Add(new LinkCandidate(
                    "withdrawAcceptance", "withdrawAcceptance",
                    $"/api/event-proposals/{item.ProposalId}/acceptance",
                    nameof(StaffCapability.ManageEventNegotiation)));
            }

            // Withdrawal is judged against the proposing type, so the current Manager
            // inherits it from a predecessor: the scope matches, not the user.
            if (scopeAppointmentTypeId is not null
                && scopeAppointmentTypeId == item.ProposerAppointmentTypeId)
            {
                candidates.Add(new LinkCandidate(
                    "withdraw", "withdrawProposal",
                    $"/api/event-proposals/{item.ProposalId}/withdraw",
                    nameof(StaffCapability.ManageEventNegotiation)));
            }
        }

        return new EventProposalResponse(
            item.ProposalId, item.LocationId, item.LocationCode, item.LocationName,
            EventTimeResponse.From(
                item.Date, item.StartTime, item.DurationMinutes, item.TimeZoneId, zones),
            item.Status, item.ListedTypeCount, item.AcceptedTypeCount, item.MyAcceptedHeadcount,
            item.AcceptedByMe, item.CreatedByMe,
            [.. item.Types.Select(t => new ProposalTypeResponse(t.Code, t.Name))],
            CallerLinks.For(capabilities, [.. candidates]));
    }
}
