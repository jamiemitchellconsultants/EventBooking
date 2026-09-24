using System.Text;
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Application.Common;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Time;

namespace EventBooking.Api.Endpoints;

/// <summary>Accepts one requested appointment status and the last observed version.</summary>
/// <param name="TargetStatus">The requested <c>BookingAppointmentStatus</c> name.</param>
/// <param name="ExpectedVersion">The positive version last observed by the caller.</param>
public sealed record SetAppointmentStatusRequest(string? TargetStatus, long? ExpectedVersion);

/// <summary>Maps the four appointment-workspace routes.</summary>
public static class AppointmentWorkspaceEndpoints
{
    /// <summary>Adds the capability-protected appointment-workspace routes.</summary>
    public static IEndpointRouteBuilder MapAppointmentWorkspaceEndpoints(
        this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/appointment-workspace")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/events", async (
            Guid? locationId,
            ICallerAccessor caller,
            ListWorkspaceEventsHandler handler,
            IEventWindowZones zones,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ListWorkspaceEventsQuery(caller.RequireStaffUserId(), locationId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<WorkspaceEventResponse>(
                [.. result.Value.Select(x => ApiResponses.WorkspaceEvent(x, zones, held))], null));
        })
            .WithAgentMetadata("listWorkspaceEvents")
            .WithEventBookingList()
            .Produces<Page<WorkspaceEventResponse>>(200)
            .ProducesProblem(403);

        group.MapGet("/events/{eventId:guid}", async (
            Guid eventId,
            ICallerAccessor caller,
            GetWorkspaceRosterHandler handler,
            IEventRepository events,
            IClock clock,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetWorkspaceRosterQuery(caller.RequireStaffUserId(), eventId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            // The roster handler has already answered 404 for a missing event, so the
            // window read below cannot fail: it only feeds the link computation.
            var eventItem = await events.GetAsync(eventId, cancellationToken);
            var localNow = clock.NowAtTransitionalLocation;
            var localDate = DateOnly.FromDateTime(localNow.DateTime);
            var localTime = TimeOnly.FromDateTime(localNow.DateTime);
            var checkInAllowed = eventItem!.Window.Date == localDate;
            var noShowAllowed = eventItem.Window.Date < localDate
                || (eventItem.Window.Date == localDate && localTime >= eventItem.Window.EndTime);
            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<WorkspaceRosterRowResponse>(
                [.. result.Value.Select(row => ApiResponses.WorkspaceRosterRow(
                    row, checkInAllowed, noShowAllowed, held))],
                null));
        })
            .WithAgentMetadata("getWorkspaceRoster")
            .WithEventBookingList()
            .Produces<Page<WorkspaceRosterRowResponse>>(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPut("/appointments/{id:guid}/status", async (
            Guid id,
            SetAppointmentStatusRequest? request,
            ICallerAccessor caller,
            UpdateBookingAppointmentStatusHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var expectedVersion = request?.ExpectedVersion ?? 0;
            if (request?.TargetStatus is null
                || !Enum.TryParse<BookingAppointmentStatus>(
                    request.TargetStatus, ignoreCase: false, out var status)
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
                    BookingAppointmentId = id,
                    Status = status,
                    ExpectedVersion = expectedVersion,
                },
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.AppointmentStatus(result.Value, held));
        })
            .WithAgentMetadata("setAppointmentStatus")
            .Produces<AppointmentStatusResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        // The route ends in a literal ".csv", which is how design 05 writes it. The colon-less
        // segment keeps the eventId constraint on the part before it.
        group.MapGet("/events/{eventId:guid}/roster.csv", async (
            Guid eventId,
            ICallerAccessor caller,
            DownloadRosterHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetWorkspaceRosterQuery(caller.RequireStaffUserId(), eventId),
                cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.File(
                    Encoding.UTF8.GetBytes(result.Value),
                    contentType: "text/csv; charset=utf-8",
                    fileDownloadName: $"roster-{eventId:D}.csv");
        })
            .WithAgentMetadata("exportWorkspaceRoster")
            .Produces<string>(200, "text/csv")
            .ProducesProblem(403)
            .ProducesProblem(404);

        return app;
    }
}
