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
