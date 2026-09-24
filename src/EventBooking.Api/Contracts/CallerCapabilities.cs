using EventBooking.Api.Auth;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;

namespace EventBooking.Api.Contracts;

/// <summary>
/// The capability names the caller currently holds, resolved once per request. This is
/// presentation, not authorization: it decides which affordances a representation advertises,
/// and every handler still makes its own decision. A caller who ignores `_links` and posts
/// anyway is refused by the handler exactly as before.
/// </summary>
/// <param name="caller">The signed-in staff identity.</param>
/// <param name="profiles">The access profiles.</param>
public sealed class CallerCapabilities(
    ICallerAccessor caller, IStaffAccessProfileRepository profiles)
{
    private IReadOnlySet<string>? _resolved;
    private IReadOnlySet<Guid>? _managedTypeIds;

    /// <summary>Returns the capability names the caller holds, or an empty set.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The capability names.</returns>
    public async Task<IReadOnlySet<string>> GetAsync(CancellationToken ct)
    {
        if (_resolved is not null)
        {
            return _resolved;
        }

        if (caller.StaffUserId is not { } staffUserId)
        {
            return _resolved = new HashSet<string>(StringComparer.Ordinal);
        }

        var profile = await profiles.GetAsync(staffUserId, ct);
        if (profile is null || !profile.IsValid())
        {
            return _resolved = new HashSet<string>(StringComparer.Ordinal);
        }

        var held = new HashSet<string>(StringComparer.Ordinal);
        foreach (var capability in Enum.GetValues<StaffCapability>())
        {
            if (StaffAccessAuthorizer.IsAllowed(profile, capability))
            {
                held.Add(capability.ToString());
            }
        }

        return _resolved = held;
    }

    /// <summary>Returns the caller's own scoped appointment-type identifier, or null.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The scope, or null when the caller is unscoped or unknown.</returns>
    public async Task<Guid?> ScopedAppointmentTypeIdAsync(CancellationToken ct)
    {
        if (caller.StaffUserId is not { } staffUserId)
        {
            return null;
        }

        var profile = await profiles.GetAsync(staffUserId, ct);
        return profile is null || !profile.IsValid() ? null : profile.AppointmentTypeId;
    }

    /// <summary>Returns the appointment-type identifiers with a current Manager profile.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The managed type identifiers, resolved once per request.</returns>
    public async Task<IReadOnlySet<Guid>> ManagerTypeIdsAsync(CancellationToken ct)
    {
        if (_managedTypeIds is not null)
        {
            return _managedTypeIds;
        }

        return _managedTypeIds = (await profiles.ListAsync(ct))
            .Where(profile => profile.IsManager && profile.AppointmentTypeId is not null)
            .Select(profile => profile.AppointmentTypeId!.Value)
            .ToHashSet();
    }
}
