using EventBooking.Domain.Access;

namespace EventBooking.Api.Auth;

/// <summary>Reads validated staff identity values from the current authenticated principal.</summary>
public interface ICallerAccessor
{
    /// <summary>The signed-in staff user's provider identifier, or null if absent or malformed.</summary>
    Guid? StaffUserId { get; }

    /// <summary>The signed-in staff user's enterprise staff number, or null if absent or malformed.</summary>
    StaffId? StaffId { get; }

    /// <summary>
    /// The signed-in staff user's human-readable name from the token name claim, or null when it
    /// is absent or blank. Presentation data only: it is never authorization-relevant, so no
    /// Require member exists for it.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>The signed-in staff user's identity-provider-assigned roles. Empty when the
    /// `roles` claim is absent; a claim value that does not name a known `Role` is dropped.</summary>
    IReadOnlySet<Role> Roles { get; }

    /// <summary>Returns the provider identifier or throws when the request has no staff identity.</summary>
    Guid RequireStaffUserId();

    /// <summary>Returns the staff number or throws when the request has no valid staff number.</summary>
    StaffId RequireStaffId();
}
