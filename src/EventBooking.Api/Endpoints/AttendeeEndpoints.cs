using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.Attendees;
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Recovery;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the thirteen attendee routes.</summary>
public static class AttendeeEndpoints
{
    /// <summary>Attendee fields accepted by create and update operations.</summary>
    public sealed record SaveAttendeeRequest(string? Name, string? Email, Guid? AttendeeGroupId);

    /// <summary>Invite options; a missing body or empty list means every active location.</summary>
    public sealed record InviteAttendeeRequest(IReadOnlyList<Guid>? LocationIds);

    /// <summary>Recovery locations added to the original booking's own.</summary>
    public sealed record AdditionalLocationIdsRequest(IReadOnlyList<Guid>? AdditionalLocationIds);

    /// <summary>Registers the attendee routes.</summary>
    public static IEndpointRouteBuilder MapAttendeeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/attendees")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            string? status,
            Guid? groupId,
            string? readiness,
            string? search,
            string? cursor,
            int? limit,
            ICallerAccessor caller,
            ListAttendeesHandler handler,
            PageCursor cursors,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            if (!PageRequest.TryBind(cursor, limit, out var page, out var field))
            {
                return ResultResponses.ValidationFailed(
                    field!, "out-of-range", "Limit must be between 1 and 200.");
            }

            string? inner = null;
            if (page.Cursor is not null && !cursors.TryUnprotect(page.Cursor, out inner!))
            {
                return ResultResponses.ValidationFailed(
                    "cursor", "cursor-invalid", "That cursor is not valid.");
            }

            var result = await handler.HandleAsync(
                new ListAttendeesQuery(
                    caller.RequireStaffUserId(),
                    inner,
                    page.Limit,
                    status,
                    groupId,
                    readiness,
                    search),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<AttendeeResponse>(
                [.. result.Value.Items.Select(x =>
                {
                    // Each row's own cursor is signed too: a client that pages from a row it is
                    // looking at must not be handed an unsigned key it could edit.
                    var projected = ApiResponses.Attendee(x, held);
                    return projected with { Cursor = cursors.Protect(projected.Cursor) };
                })],
                result.Value.NextCursor is null ? null : cursors.Protect(result.Value.NextCursor)));
        })
            .WithAgentMetadata("listAttendees")
            .Produces<Page<AttendeeResponse>>(200)
            .ProducesProblem(403)
            .ProducesProblem(422);

        group.MapPost("/", async (
            SaveAttendeeRequest request,
            ICallerAccessor caller,
            SaveAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.CreateAsync(
                new CreateAttendeeCommand(
                    caller.RequireStaffUserId(), request.Name, request.Email, request.AttendeeGroupId),
                cancellationToken))
                .ToCreated(id => $"/api/attendees/{id}"))
            .WithAgentMetadata("createAttendee")
            .Produces<Guid>(201)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapPut("/{id:guid}", async (
            Guid id,
            SaveAttendeeRequest request,
            ICallerAccessor caller,
            SaveAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.UpdateAsync(
                new UpdateAttendeeCommand(
                    caller.RequireStaffUserId(), id, request.Name, request.Email, request.AttendeeGroupId),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("updateAttendee")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        // Two-step delete. The handler reports the consequence as a failure carrying its count,
        // so the endpoint renders Task 21's confirmation body from it and decides nothing.
        group.MapDelete("/{id:guid}", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            DeleteAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new DeleteAttendeeCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("deleteAttendee")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        // Multipart, per design 05. The handler takes the file's text, so the endpoint's whole
        // job is to find the part, hold it to the 1 MB and 1000-row bounds before handing it
        // over, and decode it as UTF-8.
        group.MapPost("/import", async (
            HttpRequest request,
            ICallerAccessor caller,
            ImportAttendeesHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return ResultResponses.ValidationFailed(
                    "file", "multipart-required", "Upload the CSV as a multipart form file.");
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null)
            {
                return ResultResponses.ValidationFailed(
                    "file", "file-required", "A CSV file is required.");
            }

            if (file.Length > ImportAttendeesHandler.MaxBytes)
            {
                return ResultResponses.ValidationFailed(
                    "file", "file-too-large", "The file must be 1 MB or smaller.");
            }

            await using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            var result = await handler.HandleAsync(
                new ImportAttendeesCommand(caller.RequireStaffUserId(), csv), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            // A rejected file is a normal outcome to the handler but a 422 to design 05,
            // whose catalogue covers CSV row errors with their line numbers.
            if (!result.Value.Accepted)
            {
                return ResultResponses.ValidationFailed(
                    "The import file failed validation.",
                    [.. result.Value.Errors.Select(e =>
                        new ProblemError(null, e.LineNumber, "invalid-row", e.Message))]);
            }

            return Results.Ok(result.Value);
        })
            .WithAgentMetadata("importAttendees")
            .DisableAntiforgery()
            .Produces<AttendeeImportOutcome>(200)
            .ProducesProblem(403)
            .ProducesProblem(422);

        group.MapGet("/{id:guid}/eligible-event-count", async (
            Guid id,
            Guid[] locationIds,
            ICallerAccessor caller,
            CountEligibleEventsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CountEligibleEventsQuery(caller.RequireStaffUserId(), id, locationIds),
                cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.Ok(new { count = result.Value });
        })
            .WithAgentMetadata("countEligibleEvents")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(422);

        group.MapPost("/{id:guid}/invites", async (
            Guid id,
            InviteAttendeeRequest? request,
            ICallerAccessor caller,
            InviteAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new InviteAttendeeCommand(
                    caller.RequireStaffUserId(), id, request?.LocationIds ?? []),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("inviteAttendee")
            .Produces<InviteAttendeeOutcome>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapPost("/{id:guid}/recovery-invites", async (
            Guid id,
            AdditionalLocationIdsRequest? request,
            ICallerAccessor caller,
            StartRecoveryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new StartRecoveryCommand(
                    caller.RequireStaffUserId(), id, request?.AdditionalLocationIds ?? []),
                cancellationToken);
            return result.IsFailure
                ? result.ToResponse()
                : Results.Ok(ApiResponses.RecoveryStarted(result.Value));
        })
            .WithAgentMetadata("startRecoveryInvite")
            .Produces<StartRecoveryResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapDelete("/{id:guid}/recovery-invites/{inviteId:guid}", async (
            Guid id,
            Guid inviteId,
            ICallerAccessor caller,
            CancelRecoveryInviteHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelRecoveryInviteCommand(caller.RequireStaffUserId(), id, inviteId),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelRecoveryInvite")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapGet("/{id:guid}/bookings", async (
            Guid id,
            ICallerAccessor caller,
            GetAttendeeBookingsHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAttendeeBookingsQuery(caller.RequireStaffUserId(), id),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<AttendeeBookingResponse>(
                [.. result.Value.Select(row => ApiResponses.AttendeeBooking(id, row, held))],
                null));
        })
            .WithAgentMetadata("listAttendeeBookings")
            .Produces<Page<AttendeeBookingResponse>>(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        // Two-step booking cancellation. Here the handler reports the consequence as a SUCCESS
        // carrying ConfirmationRequired, so the translation is the endpoint's — a rendering
        // decision, not a business one, and the only place in this file where a success
        // becomes a 409.
        group.MapPost("/{id:guid}/bookings/{bookingId:guid}/cancel", async (
            Guid id,
            Guid bookingId,
            bool? confirm,
            ICallerAccessor caller,
            CancelBookingByCoordinatorHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CancelBookingByCoordinatorCommand(
                    caller.RequireStaffUserId(), id, bookingId, confirm ?? false),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            if (result.Value.ConfirmationRequired)
            {
                return ResultResponses.ConfirmationRequired(
                    "Cancelling this booking will release its place.",
                    new Dictionary<string, long>
                    {
                        ["activeBookings"] = result.Value.ActiveBookingCount,
                    });
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.BookingCancelled(id, result.Value, held));
        })
            .WithAgentMetadata("cancelAttendeeBooking")
            .Produces<CancelAttendeeBookingResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{id:guid}/email-retry", async (
            Guid id,
            ICallerAccessor caller,
            RetryEmailHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new RetryNewestEmailCommand(caller.RequireStaffUserId(), id),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("retryAttendeeEmail")
            .Produces<RetryEmailOutcome>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapGet("/{id:guid}/readiness", async (
            Guid id,
            ICallerAccessor caller,
            GetAttendeeReadinessHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAttendeeReadinessQuery(caller.RequireStaffUserId(), id),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.AttendeeReadiness(
                id, result.Value, DisplayFor(result.Value.Code), held));
        })
            .WithAgentMetadata("getAttendeeReadiness")
            .Produces<AttendeeReadinessResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        return app;
    }

    /// <summary>Gets the Coordinator-facing display wording for a readiness code.</summary>
    /// <param name="code">The readiness code to describe.</param>
    /// <returns>The display wording shared by REST and MCP transports.</returns>
    public static string DisplayForTool(AttendeeReadinessCode code) => DisplayFor(code);

    private static string DisplayFor(AttendeeReadinessCode code) => code switch
    {
        AttendeeReadinessCode.Ready => "Ready",
        AttendeeReadinessCode.NoActiveBooking => "No active booking",
        AttendeeReadinessCode.RequirementSnapshotMismatch => "Requirements changed",
        AttendeeReadinessCode.AppointmentsOutstanding => "Appointments outstanding",
        _ => code.ToString(),
    };

}
