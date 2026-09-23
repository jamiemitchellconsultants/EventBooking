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
