# 00b — Vocabulary edits 5 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Api/Endpoints/CandidateEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":16,"oldPath":"src/EventBooking.Api/Endpoints/CandidateEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs","beforeSha":"ec7c3e495b3eb1814ccc2d4f2c6adbfce9151f86e34dc579c385e6a31015a963","afterSha":"75370e684349a157997a5f93724224ca005b9d53d19e9adbaf6c4ef43178b8c0","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Bookings;
using EventBooking.Application.Candidates;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Candidates;
using System.Text;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps authorized candidate and delivery-management endpoints.</summary>
public static class CandidateEndpoints
{
    private const int MaxImportBytes = 1_048_576;
    private const int MaxImportDataRows = 10_000;

    /// <summary>Candidate fields accepted by create and update operations.</summary>
    public sealed record SaveCandidateRequest(string? Name, string? Email, Guid? EmployeeGroupId);

    /// <summary>The coordinator's choice when cancelling one candidate booking.</summary>
    /// <param name="Rebook">
    /// Whether to issue a replacement invite. Valid only for an original booking; requesting it
    /// for a recovery booking is refused as a conflict.
    /// </param>
    public sealed record CancelCandidateBookingRequest(bool Rebook);

    /// <summary>Registers candidate CRUD, invite, and template-aware retry routes.</summary>
    public static IEndpointRouteBuilder MapCandidateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/candidates")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/", async (
            CandidateStatus? status,
            string? search,
            ICallerAccessor caller,
            ListCandidatesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ListCandidatesQuery(caller.RequireStaffUserId(), status, search), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(result.Value.Select(CandidateResourceResponse.From).ToList());
        })
            .WithAgentMetadata("listCandidates")
            .Produces(200)
            .ProducesProblem(403);

        group.MapPost("/", async (
            SaveCandidateRequest request,
            ICallerAccessor caller,
            SaveCandidateHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.CreateAsync(
                new CreateCandidateCommand(
                    caller.RequireStaffUserId(), request.Name, request.Email, request.EmployeeGroupId),
                cancellationToken))
                .ToCreated(id => $"/api/candidates/{id}"))
            .WithAgentMetadata("createCandidate")
            .Produces<Guid>(201)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        group.MapPut("/{id:guid}", async (
            Guid id,
            SaveCandidateRequest request,
            ICallerAccessor caller,
            SaveCandidateHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.UpdateAsync(
                new UpdateCandidateCommand(
                    caller.RequireStaffUserId(), id, request.Name, request.Email, request.EmployeeGroupId),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("updateCandidate")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            DeleteCandidateHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new DeleteCandidateCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("deleteCandidate")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/import", async (
            HttpRequest httpRequest,
            ICallerAccessor caller,
            ImportCandidatesHandler handler,
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
                new ImportCandidatesCommand(caller.RequireStaffUserId(), csv), cancellationToken))
                .ToResponse();
        })
            .WithAgentMetadata("importCandidates")
            .Accepts<string>("text/csv")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .Produces(413)
            .Produces(415);

        group.MapPost("/{id:guid}/invite", async (
            Guid id,
            ICallerAccessor caller,
            TriggerInviteHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new TriggerInviteCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("triggerCandidateInvite")
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
            .WithAgentMetadata("retryCandidateEmail")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{candidateId:guid}/recovery-invites", async (
            Guid candidateId,
            ICallerAccessor caller,
            StartRecoveryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new StartRecoveryCommand(caller.RequireStaffUserId(), candidateId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var outcome = result.Value;
            return Results.Ok(new StartRecoveryResourceResponse(
                outcome.InviteId,
                outcome.AppointmentTypeIds,
                outcome.EmailSent,
                new Dictionary<string, ApiLink>()));
        })
            .WithAgentMetadata("startRecoveryInvite")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{candidateId:guid}/recovery-invites/{inviteId:guid}", async (
            Guid candidateId,
            Guid inviteId,
            ICallerAccessor caller,
            CancelRecoveryInviteHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelRecoveryInviteCommand(
                     caller.RequireStaffUserId(), candidateId, inviteId),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelRecoveryInvite")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPost("/{candidateId:guid}/bookings/{bookingId:guid}/cancel", async (
            Guid candidateId,
            Guid bookingId,
            CancelCandidateBookingRequest request,
            ICallerAccessor caller,
            CancelCandidateBookingHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CancelCandidateBookingCommand(
                    caller.RequireStaffUserId(), candidateId, bookingId, request.Rebook),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var outcome = result.Value;
            return Results.Ok(new CancelCandidateBookingResourceResponse(
                outcome.Reinvited,
                outcome.InviteCreated,
                outcome.DeliveryStatus,
                outcome.DeliveryId,
                CandidateLinks.ForCandidate(candidateId, CandidateStatus.Booked)));
        })
            .WithAgentMetadata("cancelCandidateBooking")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapGet("/{candidateId:guid}/bookings", async (
            Guid candidateId,
            ICallerAccessor caller,
            GetCandidateBookingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetCandidateBookingsQuery(caller.RequireStaffUserId(), candidateId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            return Results.Ok(result.Value
                .Select(row => CandidateBookingResourceResponse.From(candidateId, row))
                .ToList());
        })
            .WithAgentMetadata("listCandidateBookings")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/{candidateId:guid}/readiness", async (
            Guid candidateId,
            ICallerAccessor caller,
            GetCandidateReadinessHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetCandidateReadinessQuery(caller.RequireStaffUserId(), candidateId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var readiness = result.Value;
            return Results.Ok(new CandidateReadinessResourceResponse(
                readiness.CandidateId,
                readiness.Code.ToString(),
                DisplayFor(readiness.Code),
                readiness.OutstandingAppointmentTypes
                    .Select(type => new OutstandingAppointmentTypeResourceResponse(
                        type.Code,
                        type.Name,
                        type.IsRecoverable))
                    .ToList(),
                CandidateLinks.ForReadiness(
                    candidateId,
                    readiness.OutstandingAppointmentTypes.Any(type => type.IsRecoverable))));
        })
            .WithAgentMetadata("getCandidateReadiness")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        return app;
    }

    /// <summary>Gets the Coordinator-facing display wording for a readiness code.</summary>
    /// <param name="code">The readiness code to describe.</param>
    /// <returns>The display wording shared by REST and MCP transports.</returns>
    public static string DisplayForTool(CandidateReadinessCode code) => DisplayFor(code);

    private static string DisplayFor(CandidateReadinessCode code) => code switch
    {
        CandidateReadinessCode.Ready => "Ready",
        CandidateReadinessCode.EmployeeGroupUnassigned => "Needs employee group",
        CandidateReadinessCode.NoActiveBooking => "No active booking",
        CandidateReadinessCode.RequirementSnapshotMismatch => "Requirements changed",
        CandidateReadinessCode.AppointmentsOutstanding => "Appointments outstanding",
        _ => code.ToString(),
    };

    /// <summary>Registers the read-only Employee Group reference route.</summary>
    public static IEndpointRouteBuilder MapEmployeeGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/employee-groups")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/", async (
            ICallerAccessor caller,
            ListEmployeeGroupsHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ListEmployeeGroupsQuery(caller.RequireStaffUserId()), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("listEmployeeGroups")
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
`````

## after — src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":16,"oldPath":"src/EventBooking.Api/Endpoints/CandidateEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs","beforeSha":"ec7c3e495b3eb1814ccc2d4f2c6adbfce9151f86e34dc579c385e6a31015a963","afterSha":"75370e684349a157997a5f93724224ca005b9d53d19e9adbaf6c4ef43178b8c0","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
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

    /// <summary>The coordinator's choice when cancelling one attendee booking.</summary>
    /// <param name="Rebook">
    /// Whether to issue a replacement invite. Valid only for an original booking; requesting it
    /// for a recovery booking is refused as a conflict.
    /// </param>
    public sealed record CancelAttendeeBookingRequest(bool Rebook);

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
            ICallerAccessor caller,
            TriggerInviteHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new TriggerInviteCommand(caller.RequireStaffUserId(), id), cancellationToken))
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
            ICallerAccessor caller,
            StartRecoveryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new StartRecoveryCommand(caller.RequireStaffUserId(), attendeeId),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var outcome = result.Value;
            return Results.Ok(new StartRecoveryResourceResponse(
                outcome.InviteId,
                outcome.AppointmentTypeIds,
                outcome.EmailSent,
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
                     caller.RequireStaffUserId(), attendeeId, inviteId),
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
            CancelAttendeeBookingRequest request,
            ICallerAccessor caller,
            CancelAttendeeBookingHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CancelAttendeeBookingCommand(
                    caller.RequireStaffUserId(), attendeeId, bookingId, request.Rebook),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var outcome = result.Value;
            return Results.Ok(new CancelAttendeeBookingResourceResponse(
                outcome.Reinvited,
                outcome.InviteCreated,
                outcome.DeliveryStatus,
                outcome.DeliveryId,
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
        AttendeeReadinessCode.AttendeeGroupUnassigned => "Needs attendee group",
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
`````

## before — src/EventBooking.Api/Endpoints/ResultResponses.cs — 1/1

<!-- vocabulary-file: {"id":17,"oldPath":"src/EventBooking.Api/Endpoints/ResultResponses.cs","newPath":"src/EventBooking.Api/Endpoints/ResultResponses.cs","beforeSha":"3b5b64c5c33bc20fdf515f12b2d84e463b5939a3c1ee46e3743373351c940c4d","afterSha":"937f172d465e394c852e365168d84c934d2eaa45662045837a751fcd226e4a34","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps application results to the API's HTTP response contract.</summary>
public static class ResultResponses
{
    /// <summary>Returns the HTTP status associated with a machine-readable application error code.</summary>
    public static int StatusCodeFor(string errorCode) => errorCode switch
    {
        "validation" => StatusCodes.Status400BadRequest,
        "employee_group_required" => StatusCodes.Status400BadRequest,
        "employee_group_unknown" => StatusCodes.Status400BadRequest,
        "employee_group_inactive" => StatusCodes.Status400BadRequest,
        "employee_group_unmapped" => StatusCodes.Status409Conflict,
        "forbidden" => StatusCodes.Status403Forbidden,
        "not_found" => StatusCodes.Status404NotFound,
        "conflict" => StatusCodes.Status409Conflict,
        Error.AppointmentVersionConflictCode => StatusCodes.Status409Conflict,
        Error.CandidateGroupActiveBookingConflictCode => StatusCodes.Status409Conflict,
        Error.CandidateReconciliationRequiredCode => StatusCodes.Status409Conflict,
        Error.CandidateRequirementSnapshotMismatchCode => StatusCodes.Status409Conflict,
        Error.RecoveryNotAvailableCode => StatusCodes.Status409Conflict,
        Error.RecoveryAlreadyPendingCode => StatusCodes.Status409Conflict,
        Error.RecoveryStateChangedCode => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Returns no content for success or problem details for failure.</summary>
    public static IResult ToResponse(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    /// <summary>Returns the result value for success or problem details for failure.</summary>
    public static IResult ToResponse<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    /// <summary>Returns a created result for success or problem details for failure.</summary>
    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess
            ? Results.Created(location(result.Value), result.Value)
            : Problem(result.Error);

    private static IResult Problem(Error error) =>
        Results.Problem(
            detail: error.Message,
            statusCode: StatusCodeFor(error.Code),
            title: error.Code);
}
`````

## after — src/EventBooking.Api/Endpoints/ResultResponses.cs — 1/1

<!-- vocabulary-file: {"id":17,"oldPath":"src/EventBooking.Api/Endpoints/ResultResponses.cs","newPath":"src/EventBooking.Api/Endpoints/ResultResponses.cs","beforeSha":"3b5b64c5c33bc20fdf515f12b2d84e463b5939a3c1ee46e3743373351c940c4d","afterSha":"937f172d465e394c852e365168d84c934d2eaa45662045837a751fcd226e4a34","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps application results to the API's HTTP response contract.</summary>
public static class ResultResponses
{
    /// <summary>Returns the HTTP status associated with a machine-readable application error code.</summary>
    public static int StatusCodeFor(string errorCode) => errorCode switch
    {
        "validation" => StatusCodes.Status400BadRequest,
        "attendee_group_required" => StatusCodes.Status400BadRequest,
        "attendee_group_unknown" => StatusCodes.Status400BadRequest,
        "attendee_group_inactive" => StatusCodes.Status400BadRequest,
        "attendee_group_unmapped" => StatusCodes.Status409Conflict,
        "forbidden" => StatusCodes.Status403Forbidden,
        "not_found" => StatusCodes.Status404NotFound,
        "conflict" => StatusCodes.Status409Conflict,
        Error.AppointmentVersionConflictCode => StatusCodes.Status409Conflict,
        Error.AttendeeGroupActiveBookingConflictCode => StatusCodes.Status409Conflict,
        Error.AttendeeReconciliationRequiredCode => StatusCodes.Status409Conflict,
        Error.AttendeeRequirementSnapshotMismatchCode => StatusCodes.Status409Conflict,
        Error.RecoveryNotAvailableCode => StatusCodes.Status409Conflict,
        Error.RecoveryAlreadyPendingCode => StatusCodes.Status409Conflict,
        Error.RecoveryStateChangedCode => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Returns no content for success or problem details for failure.</summary>
    public static IResult ToResponse(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    /// <summary>Returns the result value for success or problem details for failure.</summary>
    public static IResult ToResponse<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    /// <summary>Returns a created result for success or problem details for failure.</summary>
    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess
            ? Results.Created(location(result.Value), result.Value)
            : Problem(result.Error);

    private static IResult Problem(Error error) =>
        Results.Problem(
            detail: error.Message,
            statusCode: StatusCodeFor(error.Code),
            title: error.Code);
}
`````

## before — src/EventBooking.Api/Endpoints/SlotEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":18,"oldPath":"src/EventBooking.Api/Endpoints/SlotEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/EventEndpoints.cs","beforeSha":"125cd4295db147ad164863ea37079851db9bfde3f9e054d845b366ff356f319e","afterSha":"71e9b33a96c28ae157a4a11924c978d38ca7e1ea2f4d3d4dcf3d90fbe95ff4f4","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Slots;

namespace EventBooking.Api.Endpoints;

public static class SlotEndpoints
{
    public sealed record ProposeSlotRequest(DateOnly Date, TimeOnly StartTime);

    public sealed record AcceptProposalRequest(int Headcount);

    public sealed record AdjustConfirmedSlotCapacityRequest(int TotalHeadcount);

    public static IEndpointRouteBuilder MapSlotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/slots").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/board", async (
            ICallerAccessor caller,
            GetManagerSlotBoardHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetManagerSlotBoardQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(SlotBoardResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getSlotBoard")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/operations", async (
            ICallerAccessor caller,
            GetSlotOperationsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetSlotOperationsQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(SlotOperationsResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getSlotOperations")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPost("/proposals", async (
            ProposeSlotRequest request,
            ICallerAccessor caller,
            ProposeSlotHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ProposeSlotCommand(caller.RequireStaffUserId(), request.Date, request.StartTime),
                cancellationToken))
                .ToCreated(id => $"/api/slots/proposals/{id}"))
            .WithAgentMetadata("proposeSlot")
            .Produces<Guid>(201)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        group.MapPost("/proposals/{id:guid}/acceptance", async (
            Guid id,
            AcceptProposalRequest request,
            ICallerAccessor caller,
            AcceptProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AcceptProposalCommand(caller.RequireStaffUserId(), id, request.Headcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("acceptProposal")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/proposals/{id:guid}/acceptance", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawAcceptanceHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawAcceptance")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/proposals/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawProposalCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawProposal")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPut("/confirmed/{id:guid}/capacity", async (
            Guid id,
            AdjustConfirmedSlotCapacityRequest request,
            ICallerAccessor caller,
            AdjustConfirmedSlotCapacityHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AdjustConfirmedSlotCapacityCommand(
                    caller.RequireStaffUserId(),
                    id,
                    request.TotalHeadcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("adjustConfirmedSlotCapacity")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/confirmed/{id:guid}", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            CancelConfirmedSlotHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelConfirmedSlotCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelConfirmedSlot")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        app.MapPost("/api/confirmed-slots/import", async (
            HttpRequest request,
            ICallerAccessor caller,
            ImportConfirmedSlotsHandler handler,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            return (await handler.HandleAsync(
                new ImportConfirmedSlotsCommand(caller.RequireStaffUserId(), csv),
                cancellationToken)).ToResponse();
        }).RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .WithAgentMetadata("importConfirmedSlots")
            .Accepts<string>("text/csv")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(413)
            .ProducesProblem(415);

        return app;
    }
}
`````

## after — src/EventBooking.Api/Endpoints/EventEndpoints.cs — 1/1

<!-- vocabulary-file: {"id":18,"oldPath":"src/EventBooking.Api/Endpoints/SlotEndpoints.cs","newPath":"src/EventBooking.Api/Endpoints/EventEndpoints.cs","beforeSha":"125cd4295db147ad164863ea37079851db9bfde3f9e054d845b366ff356f319e","afterSha":"71e9b33a96c28ae157a4a11924c978d38ca7e1ea2f4d3d4dcf3d90fbe95ff4f4","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Events;

namespace EventBooking.Api.Endpoints;

public static class EventEndpoints
{
    public sealed record ProposeEventRequest(DateOnly Date, TimeOnly StartTime);

    public sealed record AcceptProposalRequest(int Headcount);

    public sealed record AdjustEventCapacityRequest(int TotalHeadcount);

    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").RequireAuthorization(AuthenticationExtensions.StaffPolicy);
        var proposals = app.MapGroup("/api/event-proposals").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/board", async (
            ICallerAccessor caller,
            GetManagerEventBoardHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetManagerEventBoardQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(EventBoardResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getEventBoard")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/operations", async (
            ICallerAccessor caller,
            GetEventOperationsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetEventOperationsQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(EventOperationsResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getEventOperations")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        proposals.MapPost("", async (
            ProposeEventRequest request,
            ICallerAccessor caller,
            ProposeEventHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ProposeEventCommand(caller.RequireStaffUserId(), request.Date, request.StartTime),
                cancellationToken))
                .ToCreated(id => $"/api/event-proposals/{id}"))
            .WithAgentMetadata("proposeEvent")
            .Produces<Guid>(201)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        proposals.MapPost("/{id:guid}/acceptance", async (
            Guid id,
            AcceptProposalRequest request,
            ICallerAccessor caller,
            AcceptProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AcceptProposalCommand(caller.RequireStaffUserId(), id, request.Headcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("acceptProposal")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        proposals.MapDelete("/{id:guid}/acceptance", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawAcceptanceHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawAcceptance")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        proposals.MapDelete("/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawProposalCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawProposal")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPut("/{id:guid}/capacity", async (
            Guid id,
            AdjustEventCapacityRequest request,
            ICallerAccessor caller,
            AdjustEventCapacityHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AdjustEventCapacityCommand(
                    caller.RequireStaffUserId(),
                    id,
                    request.TotalHeadcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("adjustEventCapacity")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            CancelEventHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelEventCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelEvent")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        app.MapPost("/api/events/import", async (
            HttpRequest request,
            ICallerAccessor caller,
            ImportEventsHandler handler,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            return (await handler.HandleAsync(
                new ImportEventsCommand(caller.RequireStaffUserId(), csv),
                cancellationToken)).ToResponse();
        }).RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .WithAgentMetadata("importEvents")
            .Accepts<string>("text/csv")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(413)
            .ProducesProblem(415);

        return app;
    }
}
`````

## before — src/EventBooking.Api/EventBookingConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":19,"oldPath":"src/EventBooking.Api/EventBookingConfiguration.cs","newPath":"src/EventBooking.Api/EventBookingConfiguration.cs","beforeSha":"6023dfb7da26827ba00e177ec8c2f1c23b147a0396208723184ec87ce96d42da","afterSha":"847e8a0df3f1009f95163bb237a7e953247e4967974a463d3236d671726b4c72","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Api;

public static class EventBookingConfiguration
{
    /// <summary>
    /// Reads and validates the configuration required to start the EventBooking API.
    /// </summary>
    public static (
        string ConnectionString,
        HeadOfficeOptions HeadOffice,
        TokenOptions Tokens,
        EmailOptions Email,
        CandidatePortalOptions Portal) Read(IConfiguration configuration)
    {
        var missing = new List<string>();

        string Required(string key)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(key);
                return string.Empty;
            }

            return value;
        }

        var connectionString = Required("ConnectionStrings:EventBooking");
        var timeZone = Required("HeadOffice:TimeZoneId");
        var address = Required("HeadOffice:Address");
        var signingKey = Required("Tokens:SigningKey");
        var fromAddress = Required("Email:FromAddress");
        var fromName = Required("Email:FromName");
        var emailProviderRaw = Required("Email:Provider");
        var authProviderRaw = Required("Auth:Provider");
        var baseUrl = Required("Portal:BaseUrl");
        var coordinatorContact = Required("Portal:CoordinatorContact");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "The following configuration values are missing: " + string.Join(", ", missing));
        }

        // Checked after the missing-key check, not folded into it: a key that is present but
        // holds an unrecognised value is a different failure from a key that was never set.
        if (emailProviderRaw != "Smtp")
        {
            throw new InvalidOperationException(
                $"Email:Provider must be 'Smtp', but was '{emailProviderRaw}'.");
        }

        var emailProvider = EmailProvider.Smtp;

        if (authProviderRaw != "Local")
        {
            throw new InvalidOperationException(
                $"Auth:Provider must be 'Local', but was '{authProviderRaw}'.");
        }

        return (
            connectionString,
            new HeadOfficeOptions(timeZone),
            new TokenOptions(signingKey),
            new EmailOptions(fromAddress, fromName, emailProvider),
            new CandidatePortalOptions(baseUrl, address, coordinatorContact));
    }
}
`````

## after — src/EventBooking.Api/EventBookingConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":19,"oldPath":"src/EventBooking.Api/EventBookingConfiguration.cs","newPath":"src/EventBooking.Api/EventBookingConfiguration.cs","beforeSha":"6023dfb7da26827ba00e177ec8c2f1c23b147a0396208723184ec87ce96d42da","afterSha":"847e8a0df3f1009f95163bb237a7e953247e4967974a463d3236d671726b4c72","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Api;

public static class EventBookingConfiguration
{
    /// <summary>
    /// Reads and validates the configuration required to start the EventBooking API.
    /// </summary>
    public static (
        string ConnectionString,
        TransitionalLocationOptions TransitionalLocation,
        TokenOptions Tokens,
        EmailOptions Email,
        AttendeePortalOptions Portal) Read(IConfiguration configuration)
    {
        var missing = new List<string>();

        string Required(string key)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(key);
                return string.Empty;
            }

            return value;
        }

        var connectionString = Required("ConnectionStrings:EventBooking");
        var timeZone = Required("TransitionalLocation:TimeZoneId");
        var address = Required("TransitionalLocation:Address");
        var signingKey = Required("Tokens:SigningKey");
        var fromAddress = Required("Email:FromAddress");
        var fromName = Required("Email:FromName");
        var emailProviderRaw = Required("Email:Provider");
        var authProviderRaw = Required("Auth:Provider");
        var baseUrl = Required("Portal:BaseUrl");
        var coordinatorContact = Required("Portal:CoordinatorContact");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "The following configuration values are missing: " + string.Join(", ", missing));
        }

        // Checked after the missing-key check, not folded into it: a key that is present but
        // holds an unrecognised value is a different failure from a key that was never set.
        if (emailProviderRaw != "Smtp")
        {
            throw new InvalidOperationException(
                $"Email:Provider must be 'Smtp', but was '{emailProviderRaw}'.");
        }

        var emailProvider = EmailProvider.Smtp;

        if (authProviderRaw != "Local")
        {
            throw new InvalidOperationException(
                $"Auth:Provider must be 'Local', but was '{authProviderRaw}'.");
        }

        return (
            connectionString,
            new TransitionalLocationOptions(timeZone),
            new TokenOptions(signingKey),
            new EmailOptions(fromAddress, fromName, emailProvider),
            new AttendeePortalOptions(baseUrl, address, coordinatorContact));
    }
}
`````
