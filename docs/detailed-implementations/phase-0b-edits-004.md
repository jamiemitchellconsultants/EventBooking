# 00b — Vocabulary edits 4 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":12,"oldPath":"src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs","beforeSha":"088436ea6ee088c8a425af4430731647f040e7d9659f494212f6acf90a5f5f29","afterSha":"07fbacedd058bb2b7d2ad4ecb0295aa58bcba15ed8f72659eb857bb5202773b9","side":"after","part":1,"parts":1} -->

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
                ["eventBoard"] = Link("getEventBoard"),
                ["eventOperations"] = Link("getEventOperations"),
                ["attendees"] = Link("listAttendees"),
                ["attendeeGroups"] = Link("listAttendeeGroups"),
                ["settings"] = Link("getSettings"),
                ["staffAccess"] = Link("listStaffAccess"),
                ["dashboards"] = Link("getDashboards"),
                ["audit"] = Link("searchAudit"),
                ["appointments"] = Link("listAppointmentEvents"),
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

## before — src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":13,"oldPath":"src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs","beforeSha":"76b35ccb0e9652d43fcdbe50cca142ea2fa34c1c2d380c55e5384a82a0d566c7","afterSha":"b0088409dce5f7f0703c1c33c3a5b654058f755a69a052e4b459e3fb8c50d6ae","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":13,"oldPath":"src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs","beforeSha":"76b35ccb0e9652d43fcdbe50cca142ea2fa34c1c2d380c55e5384a82a0d566c7","afterSha":"b0088409dce5f7f0703c1c33c3a5b654058f755a69a052e4b459e3fb8c50d6ae","side":"after","part":1,"parts":1} -->

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

        group.MapGet("/events", async (
            ICallerAccessor caller,
            GetAppointmentWorkspaceHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.ListEventsAsync(
                caller.RequireStaffUserId(), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(AppointmentWorkspaceEventListResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("listAppointmentEvents")
            .Produces(200)
            .ProducesProblem(403);

        group.MapGet("/events/{eventId:guid}", async (
            Guid eventId,
            ICallerAccessor caller,
            GetAppointmentWorkspaceHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.GetEventAsync(
                caller.RequireStaffUserId(), eventId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(AppointmentEventDetailResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getAppointmentEvent")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/events/{eventId:guid}/roster", async (
            Guid eventId,
            ICallerAccessor caller,
            GetAppointmentWorkspaceHandler handler,
            AppointmentRosterCsvFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            // The same read the JSON detail route performs, so authorization, scoping, and the
            // not-found-for-out-of-scope behaviour are identical by construction.
            var result = await handler.GetEventAsync(
                caller.RequireStaffUserId(), eventId, cancellationToken);
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
    /// <summary>Gets attendees booked but not checked in for this appointment.</summary>
    public required int Expected { get; init; }

    /// <summary>Gets attendees checked in for this appointment.</summary>
    public required int CheckedIn { get; init; }

    /// <summary>Gets required appointments completed after check-in.</summary>
    public required int Completed { get; init; }

    /// <summary>Gets attendees recorded as not attending this required appointment.</summary>
    public required int NoShow { get; init; }

    internal static AppointmentStatusCountsResponse From(AppointmentStatusCounts counts) => new()
    {
        Expected = counts.Expected,
        CheckedIn = counts.CheckedIn,
        Completed = counts.Completed,
        NoShow = counts.NoShow,
    };
}

/// <summary>Describes one selectable active event without attendee rows.</summary>
public sealed record AppointmentEventSummaryResponse
{
    /// <summary>Gets the event identifier.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the event's transitional-location calendar date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }

    /// <summary>Gets scoped counts grouped by independent operational state.</summary>
    public required AppointmentStatusCountsResponse Counts { get; init; }

    /// <summary>Gets the detail and roster affordances for the eventItem.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static AppointmentEventSummaryResponse From(AppointmentEventSummary summary) => new()
    {
        EventId = summary.EventId,
        Date = summary.Date,
        StartTime = summary.StartTime,
        EndTime = summary.EndTime,
        Counts = AppointmentStatusCountsResponse.From(summary.Counts),
        Links = StaffResourceLinks.ForAppointmentEvent(summary.EventId),
    };
}

/// <summary>Returns the trusted appointment-type name and its selectable active events.</summary>
public sealed record AppointmentWorkspaceEventListResponse
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets current and upcoming active events containing scoped active bookings.</summary>
    public required IReadOnlyList<AppointmentEventSummaryResponse> Events { get; init; }

    /// <summary>Gets the collection self affordance.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static AppointmentWorkspaceEventListResponse From(AppointmentWorkspaceEventList list) => new()
    {
        AppointmentTypeName = list.AppointmentTypeName,
        Events = list.Events.Select(AppointmentEventSummaryResponse.From).ToList(),
        Links = new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/appointment-workspace/events", "GET", "listAppointmentEvents"),
        },
    };
}

/// <summary>Contains only the fields needed to identify and conduct one booked appointment.</summary>
public sealed record BookingAppointmentRowResponse
{
    /// <summary>Gets the stable booking-appointment command identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }

    /// <summary>Gets the attendee name used for primary human identification.</summary>
    public required string AttendeeName { get; init; }

    /// <summary>Gets the attendee email used for secondary human identification.</summary>
    public required string AttendeeEmail { get; init; }

    /// <summary>Gets this appointment's independent operational status name.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when staff checked the attendee in, or null until check-in.</summary>
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
        AttendeeName = row.AttendeeName,
        AttendeeEmail = row.AttendeeEmail,
        Status = row.Status.ToString(),
        CheckedInAt = row.CheckedInAt,
        OutcomeAt = row.OutcomeAt,
        Version = row.Version,
        Links = StaffResourceLinks.ForBookingAppointment(row.BookingAppointmentId),
    };
}

/// <summary>Returns one scoped active event and only its minimum-data operational rows.</summary>
public sealed record AppointmentEventDetailResponse
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets the selected event identifier.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the event's transitional-location calendar date.</summary>
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

    internal static AppointmentEventDetailResponse From(AppointmentEventDetail detail) => new()
    {
        AppointmentTypeName = detail.AppointmentTypeName,
        EventId = detail.EventId,
        Date = detail.Date,
        StartTime = detail.StartTime,
        EndTime = detail.EndTime,
        Appointments = detail.Appointments.Select(BookingAppointmentRowResponse.From).ToList(),
        Links = new Dictionary<string, ApiLink>
        {
            ["self"] = new($"/api/appointment-workspace/events/{detail.EventId}", "GET", "getAppointmentEvent"),
            ["roster"] = new($"/api/appointment-workspace/events/{detail.EventId}/roster", "GET", "exportAppointmentRoster"),
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

    /// <summary>Gets when staff checked the attendee in, or null until check-in.</summary>
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

## before — src/EventBooking.Api/Endpoints/AuditEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":14,"oldPath":"src/EventBooking.Api/Endpoints/AuditEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/AuditEndpoints.cs","beforeSha":"3f2ad5f91db5d1a55ed51c4f2701e0df333d1991d028f35aa13a78916d0c0089","afterSha":"f17117f006fc2ac35fa71be85482c31d9a70858dd84c59861c4768e3e794fb20","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Api/Endpoints/AuditEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":14,"oldPath":"src/EventBooking.Api/Endpoints/AuditEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/AuditEndpoints.cs","beforeSha":"3f2ad5f91db5d1a55ed51c4f2701e0df333d1991d028f35aa13a78916d0c0089","afterSha":"f17117f006fc2ac35fa71be85482c31d9a70858dd84c59861c4768e3e794fb20","side":"after","part":1,"parts":1} -->

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

        group.MapGet("/event/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            GetAuditHistoryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAuditHistoryQuery(
                    caller.RequireStaffUserId(), AuditEntityTypes.Event, id),
                cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value.Select(AuditHistoryResourceResponse.From).ToList())
                : result.ToResponse();
        })
            .WithAgentMetadata("getEventAuditHistory")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/attendee/{id:guid}", async (
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
            .WithAgentMetadata("getAttendeeAuditHistory")
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

## before — src/EventBooking.Api/Endpoints/BookingEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":15,"oldPath":"src/EventBooking.Api/Endpoints/BookingEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/BookingEndpoints.cs","beforeSha":"71f42620ae49f7bc7a1fb7b027a49d897a0edcc2a980dd1860c49da3897dd09c","afterSha":"84a27d1959f89cb2d1c514354e5d50d21f306c242dcb2d5d96b36daf406d2922","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Api/Endpoints/BookingEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":15,"oldPath":"src/EventBooking.Api/Endpoints/BookingEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/BookingEndpoints.cs","beforeSha":"71f42620ae49f7bc7a1fb7b027a49d897a0edcc2a980dd1860c49da3897dd09c","afterSha":"84a27d1959f89cb2d1c514354e5d50d21f306c242dcb2d5d96b36daf406d2922","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Bookings;

namespace EventBooking.Api.Endpoints;

public static class BookingEndpoints
{
    /// <summary>Named so Task 58's registration and these routes cannot drift apart.</summary>
    public const string RateLimiterPolicy = "attendee-links";

    public sealed record ConfirmBookingRequest(Guid EventId);

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
                new ConfirmBookingCommand(token, request.EventId), cancellationToken))
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
