using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>The caller's self view, or a no-role explanation when nothing usable synced.</summary>
/// <param name="DisplayName">The human-readable name, or null when the identity carries none.</param>
/// <param name="StaffId">The canonical staff number, or null when unusable.</param>
/// <param name="Roles">The token role names in enum order.</param>
/// <param name="ScopeAppointmentTypeId">The profile scope, or null when unscoped.</param>
/// <param name="Capabilities">The capability names the profile grants.</param>
/// <param name="Problem">The no-role explanation, or null for a full view.</param>
public sealed record StaffMeView(
    string? DisplayName, string? StaffId, IReadOnlyList<string> Roles,
    Guid? ScopeAppointmentTypeId, IReadOnlyList<string> Capabilities, string? Problem);

/// <summary>Builds the caller's self view. Never writes: the recorder middleware owns identity
/// refresh, and role sync runs in the pipeline before this handler is reached. The single
/// repository parameter is deliberate: without the identity or unit-of-work ports the handler
/// cannot write even by accident.</summary>
/// <param name="profiles">The profiles.</param>
public sealed class MeHandler(IStaffAccessProfileRepository profiles)
{
    /// <summary>Handles the parsed token model the Api endpoint adapts the caller to.</summary>
    /// <param name="staffUserId">The provider user identifier.</param>
    /// <param name="staffIdValue">The untrusted staff-number claim value.</param>
    /// <param name="displayName">The human-readable name claim value.</param>
    /// <param name="roles">The token roles.</param>
    /// <param name="staffIdPattern">The deployment's staff-number expression.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<StaffMeView>> HandleAsync(
        Guid staffUserId, string? staffIdValue, string? displayName,
        IReadOnlySet<Role> roles, string staffIdPattern, CancellationToken ct)
    {
        if (staffIdValue is null
            || !StaffId.TryParse(staffIdValue, out var staffId, staffIdPattern))
            return Result<StaffMeView>.Success(new StaffMeView(
                displayName, null, OrderedNames(roles), null, [],
                "No usable staff_id: sign in with an identity carrying one."));

        var profile = await profiles.GetAsync(staffUserId, ct);
        if (profile is null)
            return Result<StaffMeView>.Success(new StaffMeView(
                displayName, staffId!.Value, OrderedNames(roles), null, [],
                "No staff profile: no roles have been synced for this identity."));
        if (!profile.IsValid())
            return Result<StaffMeView>.Success(new StaffMeView(
                displayName, staffId!.Value, OrderedNames(roles), null, [],
                "Staff profile is not valid: ask an administrator to review it."));

        var capabilities = CapabilityMatrix.Grants
            .Where(g => profile.HasRole(Enum.Parse<Role>(g.Role))
                && (!g.NeedsScope || profile.AppointmentTypeId is not null))
            .Select(g => g.Capability)
            .Distinct()
            .ToList();
        return Result<StaffMeView>.Success(new StaffMeView(
            displayName, staffId!.Value, OrderedNames(roles),
            profile.AppointmentTypeId, capabilities, null));
    }

    private static IReadOnlyList<string> OrderedNames(IReadOnlySet<Role> roles) =>
        roles.OrderBy(role => role).Select(role => role.ToString()).ToList();
}
