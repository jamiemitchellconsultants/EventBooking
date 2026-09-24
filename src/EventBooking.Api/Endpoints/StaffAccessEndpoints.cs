using System.Text.Json;
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.Access;

namespace EventBooking.Api.Endpoints;

/// <summary>
/// Maps the two staff-access routes. There is deliberately no create, no delete and no role
/// edit (FR-10.5): roles live in the identity provider, and EventBooking owns only the scope.
/// </summary>
public static class StaffAccessEndpoints
{
    /// <summary>The scope body design 05 names.</summary>
    /// <param name="AppointmentTypeId">The type to scope to, or null to clear.</param>
    /// <param name="ExpectedVersion">The version the caller read.</param>
    public sealed record SetScopeRequest(Guid? AppointmentTypeId, long ExpectedVersion);

    /// <summary>Maps the staff-access routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapStaffAccessEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/staff-access")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            ICallerAccessor caller,
            StaffAccessHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.ListAsync(
                caller.RequireStaffUserId(), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<StaffAccessResponse>(
                [.. result.Value.Select(x => ApiResponses.StaffAccess(x, held))], null));
        })
            .WithAgentMetadata("listStaffAccess")
            .Produces<Page<StaffAccessResponse>>(200)
            .ProducesProblem(403);

        group.MapPut("/{staffUserId:guid}/scope", async (
            Guid staffUserId,
            JsonElement body,
            ICallerAccessor caller,
            StaffAccessHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            // Roles live in the identity provider (FR-10.5): a body smuggling a roles field
            // is rejected outright rather than silently stripped.
            if (body.ValueKind == JsonValueKind.Object
                && body.EnumerateObject().Any(property =>
                    string.Equals(property.Name, "roles", StringComparison.OrdinalIgnoreCase)))
            {
                return Results.BadRequest(
                    new { detail = "Roles are assigned through the identity provider." });
            }

            SetScopeRequest? request;
            try
            {
                request = body.Deserialize<SetScopeRequest>(
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

            // One route, two handler operations: the body's null clears and a value replaces.
            // Choosing by the body's shape is translation, not a rule — both refusals below
            // are the handler's.
            if (request.AppointmentTypeId is null)
            {
                return (await handler.ClearScopeAsync(
                    new ClearStaffAccessProfileScopeCommand(
                        caller.RequireStaffUserId(), staffUserId, request.ExpectedVersion),
                    cancellationToken)).ToResponse();
            }

            var result = await handler.ReplaceScopeAsync(
                new ReplaceStaffAccessProfileScopeCommand(
                    caller.RequireStaffUserId(), staffUserId, request.AppointmentTypeId,
                    request.ExpectedVersion),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.StaffAccessScope(result.Value, held));
        })
            .WithAgentMetadata("setStaffAccessScope")
            .Produces<StaffAccessScopeResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        return app;
    }
}
