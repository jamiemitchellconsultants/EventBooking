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
