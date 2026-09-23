using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Recovery;
using EventBooking.Domain.Attendees;
using System.Text;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps authorized attendee and delivery-management endpoints.</summary>
public static class AttendeeEndpoints
{
    private const int MaxImportBytes = 1_048_576;
    private const int MaxImportDataRows = 10_000;

    /// <summary>Attendee fields accepted by create and update operations.</summary>
    public sealed record SaveAttendeeRequest(string? Name, string? Email, Guid? AttendeeGroupId);

    public sealed record InviteAttendeeRequest(IReadOnlyList<Guid> LocationIds);

    /// <summary>Registers attendee CRUD, invite, and template-aware retry routes.</summary>
    public static IEndpointRouteBuilder MapAttendeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendees")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/", async (
            AttendeeStatus? status,
            string? search,
            ICallerAccessor caller,
            ListAttendeesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ListAttendeesQuery(caller.RequireStaffUserId(), status, search), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(result.Value.Select(AttendeeResourceResponse.From).ToList());
        })
            .WithAgentMetadata("listAttendees")
            .Produces(200)
            .ProducesProblem(403);

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
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

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
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

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
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/import", async (
            HttpRequest httpRequest,
            ICallerAccessor caller,
            ImportAttendeesHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!HasCsvContentType(httpRequest.ContentType))
            {
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            if (httpRequest.ContentLength > MaxImportBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var csv = await ReadAtMostAsync(httpRequest.Body, cancellationToken);
            if (csv is null)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            if (CountDataRows(csv) > MaxImportDataRows)
            {
                return Result.Failure(Error.Validation(
                    $"The import contains more than {MaxImportDataRows:N0} data rows.")).ToResponse();
            }

            return (await handler.HandleAsync(
                new ImportAttendeesCommand(caller.RequireStaffUserId(), csv), cancellationToken))
                .ToResponse();
        })
            .WithAgentMetadata("importAttendees")
            .Accepts<string>("text/csv")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .Produces(413)
            .Produces(415);

        group.MapPost("/{id:guid}/invite", async (
            Guid id,
            InviteAttendeeRequest request,
            ICallerAccessor caller,
            InviteAttendeeHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new InviteAttendeeCommand(caller.RequireStaffUserId(), id, request?.LocationIds ?? []),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("triggerAttendeeInvite")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{id:guid}/email-retry", async (
            Guid id,
            ICallerAccessor caller,
            RetryEmailHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new RetryEmailCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("retryAttendeeEmail")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{attendeeId:guid}/recovery-invites", async (
            Guid attendeeId,
            Guid[]? locationIds,
            ICallerAccessor caller,
            StartRecoveryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new StartRecoveryCommand(
                    caller.RequireStaffUserId(), attendeeId, locationIds?.ToList() ?? []),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var outcome = result.Value;
            return Results.Ok(new StartRecoveryResourceResponse(
                outcome.RecoveryInviteId,
                outcome.LocationIds,
                outcome.RecoverableTypeIds,
                new Dictionary<string, ApiLink>()));
        })
            .WithAgentMetadata("startRecoveryInvite")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{attendeeId:guid}/recovery-invites/{inviteId:guid}", async (
            Guid attendeeId,
            Guid inviteId,
            ICallerAccessor caller,
            CancelRecoveryInviteHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelRecoveryInviteCommand(
                     caller.RequireStaffUserId(), inviteId),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelRecoveryInvite")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{attendeeId:guid}/bookings/{bookingId:guid}/cancel", async (
            Guid attendeeId,
            Guid bookingId,
            bool? confirm,
            ICallerAccessor caller,
            CancelBookingByCoordinatorHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CancelBookingByCoordinatorCommand(
                    caller.RequireStaffUserId(), attendeeId, bookingId, confirm ?? false),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var outcome = result.Value;
            return Results.Ok(new CancelAttendeeBookingResourceResponse(
                outcome.ConfirmationRequired,
                outcome.ActiveBookingCount,
                outcome.CancelledBookingId,
                AttendeeLinks.ForAttendee(attendeeId, AttendeeStatus.Booked)));
        })
            .WithAgentMetadata("cancelAttendeeBooking")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapGet("/{attendeeId:guid}/bookings", async (
            Guid attendeeId,
            ICallerAccessor caller,
            GetAttendeeBookingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAttendeeBookingsQuery(caller.RequireStaffUserId(), attendeeId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(result.Value
                .Select(row => AttendeeBookingResourceResponse.From(attendeeId, row))
                .ToList());
        })
            .WithAgentMetadata("listAttendeeBookings")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/{attendeeId:guid}/readiness", async (
            Guid attendeeId,
            ICallerAccessor caller,
            GetAttendeeReadinessHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetAttendeeReadinessQuery(caller.RequireStaffUserId(), attendeeId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var readiness = result.Value;
            return Results.Ok(new AttendeeReadinessResourceResponse(
                readiness.AttendeeId,
                readiness.Code.ToString(),
                DisplayFor(readiness.Code),
                readiness.OutstandingAppointmentTypes
                    .Select(type => new OutstandingAppointmentTypeResourceResponse(
                        type.Code,
                        type.Name,
                        type.IsRecoverable))
                    .ToList(),
                AttendeeLinks.ForReadiness(
                    attendeeId,
                    readiness.OutstandingAppointmentTypes.Any(type => type.IsRecoverable))));
        })
            .WithAgentMetadata("getAttendeeReadiness")
            .Produces(200)
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

    /// <summary>Registers the read-only Attendee Group reference route.</summary>
    public static IEndpointRouteBuilder MapAttendeeGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendee-groups")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/", async (
            ICallerAccessor caller,
            ListAttendeeGroupsHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ListAttendeeGroupsQuery(caller.RequireStaffUserId()), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("listAttendeeGroups")
            .Produces(200)
            .ProducesProblem(403);

        return app;
    }

    private static bool HasCsvContentType(string? contentType) =>
        string.Equals(
            contentType?.Split(';', 2)[0].Trim(),
            "text/csv",
            StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> ReadAtMostAsync(Stream body, CancellationToken cancellationToken)
    {
        var chunk = new byte[81_920];
        await using var buffer = new MemoryStream();

        while (true)
        {
            var read = await body.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxImportBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static int CountDataRows(string csv)
    {
        using var reader = new StringReader(csv);
        _ = reader.ReadLine();

        var count = 0;
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line) && ++count > MaxImportDataRows)
            {
                return count;
            }
        }

        return count;
    }
}
