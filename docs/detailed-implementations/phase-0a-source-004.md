# 00a — Port source 4 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Api/Endpoints/CandidateEndpoints.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/CandidateEndpoints.cs","encoding":"utf8","sha256":"ec7c3e495b3eb1814ccc2d4f2c6adbfce9151f86e34dc579c385e6a31015a963","parts":1,"part":1} -->

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

## src/EventBooking.Api/Endpoints/DashboardEndpoints.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/DashboardEndpoints.cs","encoding":"utf8","sha256":"6d508addce4807dc908eeda344080bdb8aaa32eb230db2f21baa53d2792e4776","parts":1,"part":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Dashboards;

namespace EventBooking.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboards", async (
            ICallerAccessor caller,
            GetDashboardsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetDashboardsQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(DashboardResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .WithAgentMetadata("getDashboards")
            .Produces(200)
            .ProducesProblem(403);

        return app;
    }
}
`````

## src/EventBooking.Api/Endpoints/MeEndpoints.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/MeEndpoints.cs","encoding":"utf8","sha256":"f5dcb81101cf81d8d5c5859fb3cc9604cb50c5849564c91be8103c9005286d25","parts":1,"part":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Access;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the authenticated caller self-description route.</summary>
public static class MeEndpoints
{
    /// <summary>Adds the authenticated-only caller route to the application.</summary>
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (
            ICallerAccessor caller,
            MeHandler handler,
            CancellationToken cancellationToken) =>
        {
            var me = await handler.GetAsync(
                caller.RequireStaffUserId(), caller.StaffId, caller.Roles, cancellationToken);
            return Results.Ok(MeResourceResponse.From(
                me.StaffId?.Value,
                me.Roles.Select(role => role.ToString()).ToList(),
                me.AppointmentTypeId,
                me.AppointmentTypeName));
        }).RequireAuthorization(AuthenticationExtensions.AuthenticatedPolicy)
            .WithAgentMetadata("getMyAccess")
            .Produces(200)
            .ProducesProblem(403);

        return app;
    }
}
`````

## src/EventBooking.Api/Endpoints/ResultResponses.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/ResultResponses.cs","encoding":"utf8","sha256":"3b5b64c5c33bc20fdf515f12b2d84e463b5939a3c1ee46e3743373351c940c4d","parts":1,"part":1} -->

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

## src/EventBooking.Api/Endpoints/SlotEndpoints.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/SlotEndpoints.cs","encoding":"utf8","sha256":"125cd4295db147ad164863ea37079851db9bfde3f9e054d845b366ff356f319e","parts":1,"part":1} -->

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

## src/EventBooking.Api/Endpoints/StaffAccessEndpoints.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Endpoints/StaffAccessEndpoints.cs","encoding":"utf8","sha256":"39877a10c42d7f04ab0a2c2ccfd569e89cac014fbec94dcda1f9939e979dc6fa","parts":1,"part":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps staff-access administration routes.</summary>
public static class StaffAccessEndpoints
{
    /// <summary>Accepts one replacement appointment-type scope.</summary>
    public sealed record ReplaceStaffAccessScopeRequest(
        Guid? AppointmentTypeId,
        long ExpectedVersion);

    /// <summary>Returns the provider key resolved from an observed staff number.</summary>
    public sealed record StaffIdentityResponse(Guid StaffUserId);

    /// <summary>Adds staff-access administration routes to the application.</summary>
    public static IEndpointRouteBuilder MapStaffAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/staff-access")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("", async (
            ICallerAccessor caller,
            StaffAccessHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.ListAsync(
                caller.RequireStaffUserId(), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value.Select(ToResourceResponse).ToList())
                : result.ToResponse();
        })
            .WithAgentMetadata("listStaffAccess")
            .Produces(200)
            .ProducesProblem(403);

        group.MapPut("/{staffUserId:guid}", async (
            Guid staffUserId,
            JsonElement body,
            ICallerAccessor caller,
            StaffAccessHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (body.ValueKind == JsonValueKind.Object
                && body.EnumerateObject().Any(property =>
                    string.Equals(property.Name, "roles", StringComparison.OrdinalIgnoreCase)))
            {
                return Results.BadRequest(
                    new { detail = "Roles are assigned through the identity provider." });
            }

            ReplaceStaffAccessScopeRequest? request;
            try
            {
                request = body.Deserialize<ReplaceStaffAccessScopeRequest>(
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { detail = "The request body is invalid." });
            }

            if (request is null)
            {
                return Results.BadRequest(new { detail = "The request body is invalid." });
            }

            var result = await handler.ReplaceScopeAsync(
                new ReplaceStaffAccessProfileScopeCommand(
                    caller.RequireStaffUserId(),
                    staffUserId,
                    request.AppointmentTypeId,
                    request.ExpectedVersion),
                cancellationToken);

            return result.IsSuccess
                ? Results.Ok(ToMutationResponse(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("replaceStaffAccessScope")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{staffUserId:guid}", async (
            Guid staffUserId,
            long expectedVersion,
            ICallerAccessor caller,
            StaffAccessHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.ClearScopeAsync(
                new ClearStaffAccessProfileScopeCommand(
                    caller.RequireStaffUserId(), staffUserId, expectedVersion),
                cancellationToken)).ToResponse())
            .WithAgentMetadata("clearStaffAccessScope")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        return app;
    }

    private static StaffAccessResourceResponse ToResourceResponse(StaffAccessProfileView view) => new(
        view.StaffUserId,
        view.StaffId?.Value,
        view.Roles.Select(role => role.ToString()).ToList(),
        view.AppointmentTypeId,
        view.AppointmentTypeName,
        view.Version,
        view.DisplayName,
        StaffResourceLinks.ForStaffAccess(view.StaffUserId, view.Version));

    private static StaffAccessMutationResourceResponse ToMutationResponse(StaffAccessMutationView view)
    {
        var profile = ToResourceResponse(view.Profile);
        return new(profile, view.FormerManagerStaffUserId,
            StaffResourceLinks.ForStaffAccess(view.Profile.StaffUserId, view.Profile.Version));
    }
}
`````

## src/EventBooking.Api/EventBooking.Api.csproj — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/EventBooking.Api.csproj","encoding":"utf8","sha256":"8a2e1db19753f37e7564ad99d448be0fdd54309c09f17c730eea852ab1e95d1e","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Application\EventBooking.Application.csproj" />
    <ProjectReference Include="..\EventBooking.Infrastructure\EventBooking.Infrastructure.csproj" />
    <ProjectReference Include="..\EventBooking.Api.Auth\EventBooking.Api.Auth.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" />
  </ItemGroup>

</Project>
`````

## src/EventBooking.Api/EventBookingConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/EventBookingConfiguration.cs","encoding":"utf8","sha256":"6023dfb7da26827ba00e177ec8c2f1c23b147a0396208723184ec87ce96d42da","parts":1,"part":1} -->

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

## src/EventBooking.Api/InviteSweepService.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/InviteSweepService.cs","encoding":"utf8","sha256":"f79720a78c6139067908aa0fe36536570230ff789ad02979993b30a5286f6b6b","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Invites;

namespace EventBooking.Api;

/// <summary>
/// Runs the invite expiry sweep hourly. Expiry cannot wait for a candidate to open a link — the
/// whole point is the candidates who never do.
/// </summary>
public sealed class InviteSweepService(
    IServiceScopeFactory scopes,
    ILogger<InviteSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ExpireInvitesHandler>();

                var summary = await handler.HandleAsync(stoppingToken);

                if (summary.Expired > 0)
                {
                    logger.LogInformation(
                        "Invite sweep: {Expired} expired, {ReIssued} re-issued, {Flagged} flagged.",
                        summary.Expired,
                        summary.ReIssued,
                        summary.FlaggedForFollowUp);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad sweep must not stop every later sweep.
                logger.LogError(ex, "The invite sweep failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
`````

## src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs","encoding":"utf8","sha256":"b39b7c5f6de0d5d007ceaa9fdebfd04fbf53bcbfa553ea87e9e67ce2589736a2","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Api.OpenApi;

/// <summary>Behavior hints shared by OpenAPI and MCP discovery.</summary>
/// <param name="ReadOnly">Whether the operation performs no state change.</param>
/// <param name="Destructive">Whether the operation deletes, cancels, withdraws, clears, replaces, or transitions existing state.</param>
/// <param name="Idempotent">Whether repeating the operation has the same effect as performing it once.</param>
/// <param name="OpenWorld">Whether the operation interacts with arbitrary external entities. Always false for this tool set.</param>
public sealed record AgentHints(
    /// <summary>Gets whether the operation performs no state change.</summary>
    bool ReadOnly,
    /// <summary>Gets whether the operation deletes, cancels, withdraws, clears, replaces, or transitions existing state.</summary>
    bool Destructive,
    /// <summary>Gets whether repeating the operation has the same effect as performing it once.</summary>
    bool Idempotent,
    /// <summary>Gets whether the operation interacts with arbitrary external entities.</summary>
    bool OpenWorld);

/// <summary>One stable HTTP operation and its MCP parity decision.</summary>
/// <param name="OperationId">The unique lower-camel-case OpenAPI operation id.</param>
/// <param name="Method">The uppercase HTTP method of the operation.</param>
/// <param name="Route">The route pattern of the operation.</param>
/// <param name="Tag">The OpenAPI tag grouping the operation.</param>
/// <param name="Summary">The short human-readable summary of the operation.</param>
/// <param name="Description">The longer description stating purpose, authorization, and side effects.</param>
/// <param name="RequiresBearer">Whether the operation requires a staff bearer token.</param>
/// <param name="McpTool">The snake-case MCP tool name, or null when intentionally excluded.</param>
/// <param name="ExclusionReason">The reviewed reason for exclusion, or null when an MCP tool exists.</param>
/// <param name="Hints">The behavior hints shared by OpenAPI and MCP discovery.</param>
public sealed record AgentOperation(
    /// <summary>Gets the unique lower-camel-case OpenAPI operation id.</summary>
    string OperationId,
    /// <summary>Gets the uppercase HTTP method of the operation.</summary>
    string Method,
    /// <summary>Gets the route pattern of the operation.</summary>
    string Route,
    /// <summary>Gets the OpenAPI tag grouping the operation.</summary>
    string Tag,
    /// <summary>Gets the short human-readable summary of the operation.</summary>
    string Summary,
    /// <summary>Gets the longer description stating purpose, authorization, and side effects.</summary>
    string Description,
    /// <summary>Gets whether the operation requires a staff bearer token.</summary>
    bool RequiresBearer,
    /// <summary>Gets the snake-case MCP tool name, or null when intentionally excluded.</summary>
    string? McpTool,
    /// <summary>Gets the reviewed reason for exclusion, or null when an MCP tool exists.</summary>
    string? ExclusionReason,
    /// <summary>Gets the behavior hints shared by OpenAPI and MCP discovery.</summary>
    AgentHints Hints);

/// <summary>Shared registry of HTTP operations and their MCP parity decisions.</summary>
public static class AgentOperationCatalog
{
    /// <summary>Gets every catalogued application operation keyed by operation id.</summary>
    public static IReadOnlyDictionary<string, AgentOperation> All { get; }

    static AgentOperationCatalog()
    {
        var operations = new List<AgentOperation>
        {
            Staff("getMyAccess", HttpMethods.Get, "/api/me", "Identity", "Read the signed-in staff access.", "Reads the signed-in staff identity and capability scope. Requires a staff bearer token.", "get_my_access", Read()),
            Staff("getSlotBoard", HttpMethods.Get, "/api/slots/board", "Slots", "Read the scoped slot board.", "Reads the slot board visible to the signed-in staff member. Requires a staff bearer token.", "slot_board", Read()),
            Staff("getSlotOperations", HttpMethods.Get, "/api/slots/operations", "Slots", "Read the scoped slot operations view.", "Reads the slot operations view visible to the signed-in staff member. Requires a staff bearer token.", "get_slot_operations", Read()),
            Staff("proposeSlot", HttpMethods.Post, "/api/slots/proposals", "Slots", "Propose a slot.", "Creates a slot proposal. Requires a staff bearer token.", "propose_slot", Create()),
            Staff("acceptProposal", HttpMethods.Post, "/api/slots/proposals/{id}/acceptance", "Slots", "Accept a slot proposal.", "Transitions a slot proposal to accepted. Requires a staff bearer token.", "accept_proposal", Transition()),
            Staff("withdrawAcceptance", HttpMethods.Delete, "/api/slots/proposals/{id}/acceptance", "Slots", "Withdraw a slot acceptance.", "Withdraws an accepted slot proposal. Requires a staff bearer token.", "withdraw_acceptance", Delete()),
            Staff("withdrawProposal", HttpMethods.Delete, "/api/slots/proposals/{id}", "Slots", "Withdraw a slot proposal.", "Withdraws a slot proposal. Requires a staff bearer token.", "withdraw_proposal", Delete()),
            Staff("adjustConfirmedSlotCapacity", HttpMethods.Put, "/api/slots/confirmed/{id}/capacity", "Slots", "Adjust confirmed slot capacity.", "Updates the capacity of a confirmed slot. Requires a staff bearer token.", "adjust_slot_capacity", Transition()),
            Staff("cancelConfirmedSlot", HttpMethods.Delete, "/api/slots/confirmed/{id}", "Slots", "Cancel a confirmed slot.", "Cancels a confirmed slot. Requires a staff bearer token.", "cancel_confirmed_slot", Delete()),
            Staff("importConfirmedSlots", HttpMethods.Post, "/api/confirmed-slots/import", "Slots", "Import confirmed slots.", "Imports confirmed slots from CSV. Requires a staff bearer token.", "import_confirmed_slots", Create()),
            Staff("listCandidates", HttpMethods.Get, "/api/candidates", "Candidates", "List candidates.", "Reads the candidate collection. Requires a staff bearer token.", "list_candidates", Read()),
            Staff("createCandidate", HttpMethods.Post, "/api/candidates", "Candidates", "Create a candidate.", "Creates a candidate. Requires a staff bearer token.", "create_candidate", Create()),
            Staff("updateCandidate", HttpMethods.Put, "/api/candidates/{id}", "Candidates", "Update a candidate.", "Updates an existing candidate. Requires a staff bearer token.", "update_candidate", Transition()),
            Staff("deleteCandidate", HttpMethods.Delete, "/api/candidates/{id}", "Candidates", "Delete a candidate.", "Deletes a candidate. Requires a staff bearer token.", "delete_candidate", Delete()),
            Staff("importCandidates", HttpMethods.Post, "/api/candidates/import", "Candidates", "Import candidates.", "Imports candidates from CSV. Requires a staff bearer token.", "import_candidates", Create()),
            Staff("triggerCandidateInvite", HttpMethods.Post, "/api/candidates/{id}/invite", "Candidates", "Trigger a candidate invite.", "Sends a booking invite to a candidate. Requires a staff bearer token.", "trigger_invite", Create()),
            Staff("retryCandidateEmail", HttpMethods.Post, "/api/candidates/{id}/email-retry", "Candidates", "Retry candidate email.", "Retries pending candidate email delivery. Requires a staff bearer token.", "retry_candidate_email", Create()),
            Staff("startRecoveryInvite", HttpMethods.Post, "/api/candidates/{candidateId}/recovery-invites", "Candidates", "Start a recovery invite.", "Starts a recovery invite for a candidate. Requires a staff bearer token.", "start_recovery_invite", Create()),
            Staff("cancelRecoveryInvite", HttpMethods.Delete, "/api/candidates/{candidateId}/recovery-invites/{inviteId}", "Candidates", "Cancel a recovery invite.", "Cancels a pending recovery invite. Requires a staff bearer token.", "cancel_recovery_invite", Delete()),
            Staff("listCandidateBookings", HttpMethods.Get, "/api/candidates/{candidateId}/bookings", "Candidate Booking", "List candidate bookings.", "Reads the active bookings of a candidate. Requires a staff bearer token.", "list_candidate_bookings", Read()),
            Staff("cancelCandidateBooking", HttpMethods.Post, "/api/candidates/{candidateId}/bookings/{bookingId}/cancel", "Candidate Booking", "Cancel a candidate booking.", "Cancels a candidate booking. Requires a staff bearer token.", "cancel_candidate_booking", Delete()),
            Staff("getCandidateReadiness", HttpMethods.Get, "/api/candidates/{candidateId}/readiness", "Candidates", "Read candidate readiness.", "Reads the booking readiness of a candidate. Requires a staff bearer token.", "get_candidate_readiness", Read()),
            Staff("listEmployeeGroups", HttpMethods.Get, "/api/employee-groups", "Employee Groups", "List employee groups.", "Reads the employee group collection. Requires a staff bearer token.", "list_employee_groups", Read()),
            Staff("getSettings", HttpMethods.Get, "/api/admin/settings", "Settings", "Read settings.", "Reads the application settings. Requires a staff bearer token.", "get_settings", Read()),
            Staff("updateSettings", HttpMethods.Put, "/api/admin/settings", "Settings", "Update settings.", "Updates the application settings. Requires a staff bearer token.", "update_settings", Transition()),
            Staff("listStaffAccess", HttpMethods.Get, "/api/admin/staff-access", "Staff Access", "List staff access.", "Reads the staff access collection. Requires a staff bearer token.", "list_staff_access", Read()),
            Staff("replaceStaffAccessScope", HttpMethods.Put, "/api/admin/staff-access/{staffUserId}", "Staff Access", "Replace staff access scope.", "Replaces the access scope of a staff user. Requires a staff bearer token.", "replace_staff_access_scope", Transition()),
            Staff("clearStaffAccessScope", HttpMethods.Delete, "/api/admin/staff-access/{staffUserId}", "Staff Access", "Clear staff access scope.", "Clears the access scope of a staff user. Requires a staff bearer token.", "clear_staff_access_scope", Delete()),
            Staff("getDashboards", HttpMethods.Get, "/api/dashboards", "Dashboards", "Read dashboards.", "Reads the dashboard views visible to the signed-in staff member. Requires a staff bearer token.", "get_dashboards", Read()),
            Staff("getSlotAuditHistory", HttpMethods.Get, "/api/audit/slot/{id}", "Audit", "Read slot audit history.", "Reads the audit history of a slot. Requires a staff bearer token.", "slot_audit_history", Read()),
            Staff("getCandidateAuditHistory", HttpMethods.Get, "/api/audit/candidate/{id}", "Audit", "Read candidate audit history.", "Reads the audit history of a candidate. Requires a staff bearer token.", "candidate_audit_history", Read()),
            Staff("searchAudit", HttpMethods.Get, "/api/audit/search", "Audit", "Search audit events.", "Searches audit events with filters and cursor paging. Requires a staff bearer token.", "search_audit", Read()),
            Staff("listAppointmentSlots", HttpMethods.Get, "/api/appointment-workspace/slots", "Appointment Workspace", "List appointment slots.", "Reads the appointment workspace slots. Requires a staff bearer token.", "appointment_slots", Read()),
            Staff("getAppointmentSlot", HttpMethods.Get, "/api/appointment-workspace/slots/{confirmedSlotId}", "Appointment Workspace", "Read an appointment slot.", "Reads one appointment workspace slot. Requires a staff bearer token.", "appointment_slot_detail", Read()),
            Staff("exportAppointmentRoster", HttpMethods.Get, "/api/appointment-workspace/slots/{confirmedSlotId}/roster", "Appointment Workspace", "Export an appointment roster.", "Exports the appointment roster CSV. Requires a staff bearer token.", "export_appointment_roster", Read()),
            Staff("updateAppointmentStatus", HttpMethods.Put, "/api/appointment-workspace/appointments/{bookingAppointmentId}/status", "Appointment Workspace", "Update appointment status.", "Updates the status of a booking appointment. Requires a staff bearer token.", "update_appointment_status", Transition()),
            Excluded("getApiIndex", HttpMethods.Get, "/api", "Discovery", "Read the API entry document.", "Anonymous API entry document listing principal entry points.", "Transport discovery, not a business capability. MCP has tools/list."),
            Excluded("getOpenApiDocument", HttpMethods.Get, "/openapi/v1.json", "Discovery", "Read the OpenAPI document.", "Anonymous machine-readable API contract.", "Transport discovery, not a business capability."),
            Excluded("getSwaggerUi", HttpMethods.Get, "/swagger", "Discovery", "Open the Swagger UI.", "Anonymous human documentation UI.", "Human documentation UI, not a business capability."),
            Excluded("getHealth", HttpMethods.Get, "/health", "Health", "Read the deployment health probe.", "Anonymous deployment liveness probe.", "Deployment probe, not a staff workflow."),
            Excluded("viewInvite", HttpMethods.Get, "/api/booking/{token}", "Candidate Booking", "View an invite.", "Anonymous candidate token flow.", "Anonymous Candidate token flow; excluded by the approved remote MCP design."),
            Excluded("confirmBooking", HttpMethods.Post, "/api/booking/{token}/confirm", "Candidate Booking", "Confirm a booking.", "Anonymous candidate token flow.", "Anonymous Candidate token flow; excluded by the approved remote MCP design."),
            Excluded("viewManagedBooking", HttpMethods.Get, "/api/booking/manage/{token}", "Candidate Booking", "View a managed booking.", "Anonymous candidate token flow.", "Anonymous Candidate token flow; excluded by the approved remote MCP design."),
            Excluded("cancelManagedBooking", HttpMethods.Post, "/api/booking/manage/{token}/cancel", "Candidate Booking", "Cancel a managed booking.", "Anonymous candidate token flow.", "Anonymous Candidate token flow; excluded by the approved remote MCP design."),
        };

        var byId = new Dictionary<string, AgentOperation>(StringComparer.Ordinal);
        var byRoute = new HashSet<string>(StringComparer.Ordinal);
        var byTool = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            if ((operation.McpTool is null) == (operation.ExclusionReason is null))
            {
                throw new InvalidOperationException($"Operation {operation.OperationId} must set exactly one of McpTool or ExclusionReason.");
            }

            if (!byId.TryAdd(operation.OperationId, operation))
            {
                throw new InvalidOperationException($"Duplicate operation id {operation.OperationId}.");
            }

            if (!byRoute.Add($"{operation.Method} {operation.Route}"))
            {
                throw new InvalidOperationException($"Duplicate method and route {operation.Method} {operation.Route}.");
            }

            if (operation.McpTool is not null && !byTool.Add(operation.McpTool))
            {
                throw new InvalidOperationException($"Duplicate MCP tool {operation.McpTool}.");
            }
        }

        All = byId;
    }

    /// <summary>Gets one required operation or throws for a programming error.</summary>
    /// <param name="operationId">The operation id to look up.</param>
    /// <returns>The catalogued operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the operation id is unknown.</exception>
    public static AgentOperation Get(string operationId) => All[operationId];

    private static AgentOperation Staff(
        string operationId, string method, string route, string tag,
        string summary, string description, string mcpTool, AgentHints hints) =>
        new(operationId, method, route, tag, summary, description, true, mcpTool, null, hints);

    private static AgentOperation Excluded(
        string operationId, string method, string route, string tag,
        string summary, string description, string reason) =>
        new(operationId, method, route, tag, summary, description, false, null, reason, new AgentHints(true, false, true, false));

    private static AgentHints Read() => new(true, false, true, false);

    private static AgentHints Create() => new(false, false, false, false);

    private static AgentHints Transition() => new(false, true, true, false);

    private static AgentHints Delete() => new(false, true, true, false);
}
`````

## src/EventBooking.Api/OpenApi/EndpointMetadataExtensions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/OpenApi/EndpointMetadataExtensions.cs","encoding":"utf8","sha256":"ee79072dd05ef6a8fc2c654e646d674234ea4a801f0df0f1bbfa368806a8d1e7","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Api.OpenApi;

/// <summary>Applies stable agent-facing metadata to Minimal API endpoints.</summary>
public static class EndpointMetadataExtensions
{
    /// <summary>Applies the catalogued operation identity and human description.</summary>
    /// <param name="builder">The endpoint being described.</param>
    /// <param name="operationId">The lower-camel-case operation id in the agent catalog.</param>
    /// <returns>The endpoint builder for chaining.</returns>
    public static RouteHandlerBuilder WithAgentMetadata(
        this RouteHandlerBuilder builder,
        string operationId)
    {
        var operation = AgentOperationCatalog.Get(operationId);
        return builder
            .WithName(operation.OperationId)
            .WithTags(operation.Tag)
            .WithSummary(operation.Summary)
            .WithDescription(operation.Description);
    }
}
`````

## src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs","encoding":"utf8","sha256":"bb145267497bd88e13905e93c24ea810a344ad809318e0d6fc17756f8ab7f920","parts":1,"part":1} -->

`````csharp
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EventBooking.Api.OpenApi;

/// <summary>Registers the first-party OpenAPI document with agent extensions.</summary>
public static class OpenApiConfiguration
{
    /// <summary>Registers the v1 document, security scheme, and agent extensions.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventBookingOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "EventBooking API",
                    Version = "v1",
                    Description = "EventBooking staff API and anonymous Candidate booking links.",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                var name = context.Description.ActionDescriptor.EndpointMetadata
                    .OfType<EndpointNameMetadata>()
                    .FirstOrDefault()?.EndpointName;
                if (name is null || !AgentOperationCatalog.All.TryGetValue(name, out var entry))
                {
                    return Task.CompletedTask;
                }

                if (entry.McpTool is not null)
                {
                    operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    operation.Extensions["x-mcp-tool"] = new JsonNodeExtension(JsonValue.Create(entry.McpTool)!);
                    operation.Extensions["x-agent-hints"] = new JsonNodeExtension(new JsonObject
                    {
                        ["readOnly"] = entry.Hints.ReadOnly,
                        ["destructive"] = entry.Hints.Destructive,
                        ["idempotent"] = entry.Hints.Idempotent,
                        ["openWorld"] = entry.Hints.OpenWorld,
                    });
                }

                if (entry.RequiresBearer)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("bearer")] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }
}
`````

## src/EventBooking.Api/Program.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Program.cs","encoding":"utf8","sha256":"c1ace40de0a90cec9d634746bc9a7c86e234bbd4112d5c91cb16e3c4f3618d7a","parts":1,"part":1} -->

`````csharp
using System.Threading.RateLimiting;
using EventBooking.Api;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using EventBooking.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);


const string WebClientCorsPolicy = "web-client";
var allowedWebOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

var (connectionString, headOffice, tokens, email, portal) =
    EventBookingConfiguration.Read(builder.Configuration);

builder.Services.AddEventBookingInfrastructure(connectionString, headOffice, tokens);

var smtpHost = builder.Configuration["Email:Smtp:Host"]
    ?? throw new InvalidOperationException(
        "Email:Smtp:Host is required when Email:Provider is Smtp.");
var smtpPort = int.TryParse(builder.Configuration["Email:Smtp:Port"], out var port)
    ? port
    : throw new InvalidOperationException(
        "Email:Smtp:Port must be a valid integer when Email:Provider is Smtp.");

builder.Services.AddLocalEmailTransport(email, new SmtpOptions(smtpHost, smtpPort));
builder.Services.AddEventBookingApplication(portal);
builder.Services.AddEventBookingAuthentication(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddEventBookingOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebClientCorsPolicy, policy =>
    {
        if (allowedWebOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedWebOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("Content-Disposition");
        }
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Anonymous, token-addressed routes: generous for a real candidate, unattractive for a script.
    // Partitioned by client address so one client's burst (or script) cannot consume the
    // allowance of every other candidate. ForwardedHeadersMiddleware (below) restores the real
    // client address when the app runs behind a proxy or load balancer.
    options.AddPolicy<string, RemoteIpRateLimiterPolicy>(BookingEndpoints.RateLimiterPolicy);
});

builder.Services.AddHostedService<InviteSweepService>();

var app = builder.Build();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "EventBooking API v1";
    options.SwaggerEndpoint("/openapi/v1.json", "EventBooking API v1");
    options.DisplayOperationId();
    options.HeadContent = "<link rel=\"alternate\" type=\"application/json\" href=\"/openapi/v1.json\" />";
});
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});
app.UseCors(WebClientCorsPolicy);
app.UseAuthentication();
app.UseMiddleware<StaffIdentityRecorder>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().WithAgentMetadata("getHealth");
app.MapApiDiscoveryEndpoints();

app.MapSlotEndpoints();
app.MapCandidateEndpoints();
app.MapEmployeeGroupEndpoints();
app.MapAdminEndpoints();
app.MapStaffAccessEndpoints();
app.MapMeEndpoints();
app.MapBookingEndpoints();
app.MapDashboardEndpoints();
app.MapAuditEndpoints();
app.MapAppointmentWorkspaceEndpoints();

app.Run();

/// <summary>Named so the integration test factory can start this host.</summary>
public partial class Program;
`````

## src/EventBooking.Api/Properties/launchSettings.json — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Properties/launchSettings.json","encoding":"utf8","sha256":"d2e93936b073c2db9905ee9f2724750604121a140055c7db4497ad095996959b","parts":1,"part":1} -->

`````text
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:5001;http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:5001;http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
`````
