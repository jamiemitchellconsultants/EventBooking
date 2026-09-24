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
            return Results.Ok(new Page<AppointmentTypeResponse>(
                [.. result.Value.Select(x => ApiResponses.AppointmentType(x, held))], null));
        })
            .WithAgentMetadata("listAppointmentTypes")
            .WithEventBookingList()
            .Produces<Page<AppointmentTypeResponse>>(200)
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
                ApiResponses.AppointmentType(result.Value, held, hasManager: false));
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
            var managed = await capabilities.ManagerTypeIdsAsync(cancellationToken);
            return Results.Ok(ApiResponses.AppointmentType(
                result.Value, held, managed.Contains(result.Value.Id)));
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
