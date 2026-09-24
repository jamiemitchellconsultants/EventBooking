using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.Access;
using EventBooking.Application.ReferenceData;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the three appointment-type routes.</summary>
public static class AppointmentTypeEndpoints
{
    /// <summary>The creation body design 05 names.</summary>
    /// <param name="Code">The canonical code.</param>
    /// <param name="Name">The display name.</param>
    public sealed record CreateAppointmentTypeRequest(string? Code, string? Name);

    /// <summary>The update body design 05 names.</summary>
    /// <param name="Name">The display name.</param>
    /// <param name="IsActive">Whether the type stays in use.</param>
    /// <param name="ExpectedVersion">The version the caller read.</param>
    public sealed record UpdateAppointmentTypeRequest(
        string? Name, bool IsActive, long ExpectedVersion);

    /// <summary>Maps the appointment-type routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapAppointmentTypeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/appointment-types")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            bool? includeInactive,
            ListAppointmentTypesHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ListAppointmentTypesQuery(includeInactive ?? false), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<AppointmentTypeListResponse>(
                [.. result.Value.Select(x => AppointmentTypeListResponse.From(x, held))], null));
        })
            .WithAgentMetadata("listAppointmentTypes")
            .Produces<Page<AppointmentTypeListResponse>>(200)
            .ProducesProblem(401)
            .ProducesProblem(403);

        group.MapPost("/", async (
            CreateAppointmentTypeRequest request,
            ICallerAccessor caller,
            CreateAppointmentTypeHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CreateAppointmentTypeCommand(
                    caller.RequireStaffUserId(), request.Code, request.Name),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Created(
                $"/api/appointment-types/{result.Value.Id}",
                ApiResponses.AppointmentType(result.Value, held));
        })
            .WithAgentMetadata("createAppointmentType")
            .Produces<AppointmentTypeResponse>(201)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateAppointmentTypeRequest request,
            ICallerAccessor caller,
            UpdateAppointmentTypeHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new UpdateAppointmentTypeCommand(
                    caller.RequireStaffUserId(), id, request.Name, request.IsActive,
                    request.ExpectedVersion),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.AppointmentType(result.Value, held));
        })
            .WithAgentMetadata("updateAppointmentType")
            .Produces<AppointmentTypeResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        return app;
    }
}

/// <summary>One row of the appointment-type list, with the links its caller may follow.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Code">The canonical code.</param>
/// <param name="Name">The display name.</param>
/// <param name="IsActive">Whether the type is in use.</param>
/// <param name="ManagerDisplayName">The current Manager's display name.</param>
/// <param name="Links">The affordances the caller holds.</param>
public sealed record AppointmentTypeListResponse(
    Guid Id, string Code, string Name, bool IsActive, string? ManagerDisplayName,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one list row.</summary>
    /// <param name="item">The application row.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response row.</returns>
    public static AppointmentTypeListResponse From(
        AppointmentTypeListItem item, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new AppointmentTypeListResponse(
            item.Id, item.Code, item.Name, item.IsActive, item.ManagerDisplayName,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listAppointmentTypes", "/api/appointment-types", null),
                new LinkCandidate(
                    "update", "updateAppointmentType", $"/api/appointment-types/{item.Id}",
                    nameof(StaffCapability.ManageReferenceData))));
    }
}
