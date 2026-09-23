using System.Security.Claims;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Auth;

/// <summary>Reads provider and enterprise staff identifiers from authenticated HTTP claims.</summary>
public sealed class HttpContextCallerAccessor(IHttpContextAccessor accessor, ILogger<HttpContextCallerAccessor> logger) : ICallerAccessor
{
    /// <summary>The claim type a v1 Entra ID token uses.</summary>
    public const string ObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>The claim type a v2 Entra ID token uses.</summary>
    public const string ShortObjectIdClaim = "oid";

    /// <summary>The shared Keycloak and Entra ID claim containing the enterprise staff number.</summary>
    public const string StaffIdClaim = "staff_id";

    /// <summary>The shared Keycloak and Entra ID claim carrying identity-provider-assigned roles.</summary>
    public const string RolesClaim = "roles";

    /// <summary>The shared Keycloak and Entra ID claim carrying the caller's full name.</summary>
    public const string NameClaim = "name";

    /// <summary>Gets the provider identifier from the current authenticated principal.</summary>
    public Guid? StaffUserId => StaffUserIdOf(accessor.HttpContext?.User);

    /// <summary>Gets the validated enterprise staff number from the current principal.</summary>
    public StaffId? StaffId => StaffIdOf(accessor.HttpContext?.User);

    /// <summary>Gets the human-readable name from the current authenticated principal.</summary>
    public string? DisplayName => DisplayNameOf(accessor.HttpContext?.User);

    /// <summary>Gets the recognised roles the current authenticated principal's token carries.</summary>
    public IReadOnlySet<Role> Roles => RolesOf(
        accessor.HttpContext?.User,
        value => logger.LogWarning("Ignoring unknown identity-provider role {Role}.", value));

    /// <inheritdoc />
    public Guid RequireStaffUserId() =>
        StaffUserId ?? throw new InvalidOperationException("The request has no staff identity.");

    /// <inheritdoc />
    public StaffId RequireStaffId() =>
        StaffId ?? throw new InvalidOperationException("The request has no valid staff number.");

    /// <summary>Reads a provider identifier only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The provider identifier, or null when absent or malformed.</returns>
    public static Guid? StaffUserIdOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var value =
                identity.FindFirst(ShortObjectIdClaim)?.Value
                ?? identity.FindFirst(ObjectIdClaim)?.Value;

            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>Reads and validates a staff number only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The canonical staff number, or null when absent or malformed.</returns>
    public static StaffId? StaffIdOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            if (StaffId.TryParse(identity.FindFirst(StaffIdClaim)?.Value, out var staffId))
            {
                return staffId;
            }
        }

        return null;
    }

    /// <summary>Reads a human-readable name only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The name, or null when absent, empty, or whitespace.</returns>
    public static string? DisplayNameOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var value = identity.FindFirst(NameClaim)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Reads and parses every recognised role claim value from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <param name="onRejected">Receives each rejected claim value for warning-level logging.</param>
    /// <returns>The parsed role set; empty when the claim is absent or unauthenticated.</returns>
    public static IReadOnlySet<Role> RolesOf(
        ClaimsPrincipal? principal,
        Action<string>? onRejected = null)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var claims = identity.FindAll(RolesClaim).ToList();
            if (claims.Count == 0)
            {
                continue;
            }

            var roles = new HashSet<Role>();
            foreach (var claim in claims)
            {
                if (Enum.TryParse<Role>(claim.Value, ignoreCase: false, out var role) && Enum.IsDefined(role))
                {
                    roles.Add(role);
                }
                else
                {
                    onRejected?.Invoke(claim.Value);
                }
            }

            return roles;
        }

        return new HashSet<Role>();
    }
}
