using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Domain.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace EventBooking.Api.Auth;

/// <summary>Signed in, and known to this application as a member of staff.</summary>
public sealed class StaffRequirement : IAuthorizationRequirement;

/// <summary>Requires both validated identity claims and an application-owned access profile.</summary>
public sealed class StaffRequirementHandler(
    ICallerAccessor caller,
    IStaffAccessProfileRepository profiles,
    SyncStaffAccessProfileRolesHandler sync) : AuthorizationHandler<StaffRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        StaffRequirement requirement)
    {
        var staffUserId = caller.StaffUserId;
        if (staffUserId is null || caller.StaffId is null)
        {
            return;
        }

        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;

        // Identity-provider role changes take effect on every staff request, not only when
        // /api/me is called: the stored profile is reconciled with the token's roles first,
        // so a reduced role set cannot linger for REST or MCP callers that never call /api/me.
        // The sync is a cheap no-op read when nothing changed, and refuses to leave the
        // application without its last Admin (see the IdP-sourced roles design).
        var profile = await sync.SyncAsync(staffUserId.Value, caller.Roles, cancellationToken)
            ?? await profiles.GetAsync(staffUserId.Value, cancellationToken);
        if (profile is not null && profile.IsValid())
        {
            context.Succeed(requirement);
        }
    }
}
