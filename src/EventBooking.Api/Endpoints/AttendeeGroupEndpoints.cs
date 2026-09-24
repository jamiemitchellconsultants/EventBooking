using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Api.Pagination;
using EventBooking.Application.Access;
using EventBooking.Application.ReferenceData;

namespace EventBooking.Api.Endpoints;

/// <summary>Maps the three attendee-group routes.</summary>
public static class AttendeeGroupEndpoints
{
    /// <summary>The creation body design 05 names.</summary>
    /// <param name="Code">The canonical code.</param>
    /// <param name="Name">The display name.</param>
    /// <param name="AppointmentTypeIds">The required appointment types.</param>
    public sealed record CreateAttendeeGroupRequest(
        string? Code, string? Name, IReadOnlyList<Guid>? AppointmentTypeIds);

    /// <summary>The update body design 05 names.</summary>
    /// <param name="Name">The display name.</param>
    /// <param name="IsActive">Whether the group stays in use.</param>
    /// <param name="AppointmentTypeIds">The required appointment types.</param>
    /// <param name="ExpectedVersion">The version the caller read.</param>
    public sealed record UpdateAttendeeGroupRequest(
        string? Name, bool IsActive, IReadOnlyList<Guid>? AppointmentTypeIds,
        long ExpectedVersion);

    /// <summary>Maps the attendee-group routes.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapAttendeeGroupEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/attendee-groups")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .RequireRateLimiting(StaffRateLimiterPolicy.PolicyName);

        group.MapGet("/", async (
            bool? includeInactive,
            ListAttendeeGroupsHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ListAttendeeGroupsQuery(includeInactive ?? false), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(new Page<AttendeeGroupListResponse>(
                [.. result.Value.Select(x => AttendeeGroupListResponse.From(x, held))], null));
        })
            .WithAgentMetadata("listAttendeeGroups")
            .Produces<Page<AttendeeGroupListResponse>>(200)
            .ProducesProblem(401)
            .ProducesProblem(403);

        group.MapPost("/", async (
            CreateAttendeeGroupRequest request,
            ICallerAccessor caller,
            CreateAttendeeGroupHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new CreateAttendeeGroupCommand(
                    caller.RequireStaffUserId(), request.Code, request.Name,
                    request.AppointmentTypeIds ?? []),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Created(
                $"/api/attendee-groups/{result.Value.Id}",
                ApiResponses.AttendeeGroup(result.Value, held));
        })
            .WithAgentMetadata("createAttendeeGroup")
            .Produces<AttendeeGroupResponse>(201)
            .ProducesProblem(403)
            .ProducesProblem(409)
            .ProducesProblem(422);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateAttendeeGroupRequest request,
            ICallerAccessor caller,
            UpdateAttendeeGroupHandler handler,
            CallerCapabilities capabilities,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new UpdateAttendeeGroupCommand(
                    caller.RequireStaffUserId(), id, request.Name,
                    request.AppointmentTypeIds, request.IsActive, request.ExpectedVersion),
                cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var held = await capabilities.GetAsync(cancellationToken);
            return Results.Ok(ApiResponses.AttendeeGroup(result.Value, held));
        })
            .WithAgentMetadata("updateAttendeeGroup")
            .Produces<AttendeeGroupResponse>(200)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(422);

        return app;
    }
}

/// <summary>One row of the attendee-group list, with the links its caller may follow.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Code">The canonical code.</param>
/// <param name="Name">The display name.</param>
/// <param name="IsActive">Whether the group is in use.</param>
/// <param name="RequirementTypeIds">The required appointment types.</param>
/// <param name="MemberCount">The member count.</param>
/// <param name="Links">The affordances the caller holds.</param>
public sealed record AttendeeGroupListResponse(
    Guid Id, string Code, string Name, bool IsActive, IReadOnlyList<Guid> RequirementTypeIds,
    int MemberCount,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")]
    IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one list row.</summary>
    /// <param name="item">The application row.</param>
    /// <param name="capabilities">The caller's capabilities.</param>
    /// <returns>The response row.</returns>
    public static AttendeeGroupListResponse From(
        AttendeeGroupListItem item, IReadOnlySet<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new AttendeeGroupListResponse(
            item.Id, item.Code, item.Name, item.IsActive, item.RequirementTypeIds,
            item.MemberCount,
            CallerLinks.For(
                capabilities,
                new LinkCandidate("self", "listAttendeeGroups", "/api/attendee-groups", null),
                new LinkCandidate(
                    "update", "updateAttendeeGroup", $"/api/attendee-groups/{item.Id}",
                    nameof(StaffCapability.ManageReferenceData))));
    }
}
