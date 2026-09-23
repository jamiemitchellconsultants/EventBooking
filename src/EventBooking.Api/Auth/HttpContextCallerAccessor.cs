using System.Security.Claims;
using EventBooking.Application.Access;
using EventBooking.Domain.Access;
using Microsoft.Extensions.Options;

namespace EventBooking.Api.Auth;

/// <summary>Reads provider and enterprise staff identifiers from authenticated HTTP claims.</summary>
public sealed class HttpContextCallerAccessor(
    IHttpContextAccessor accessor,
    ILogger<HttpContextCallerAccessor> logger,
    IOptions<AuthClaimOptions> claims) : ICallerAccessor
{
    /// <summary>The OIDC subject claim. Specification-fixed, so not configurable.</summary>
    public const string ObjectIdClaim =
        "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>The short form of the same claim.</summary>
    public const string ShortObjectIdClaim = "oid";

    private AuthClaimOptions Claims => claims.Value;

    /// <summary>Gets the provider identifier from the current authenticated principal.</summary>
    public Guid? StaffUserId => StaffUserIdOf(accessor.HttpContext?.User);

    /// <summary>Gets the validated enterprise staff number from the current principal.</summary>
    public StaffId? StaffId =>
        StaffIdOf(accessor.HttpContext?.User, Claims.StaffIdPattern, Claims.StaffIdClaim);

    /// <summary>Gets the human-readable name from the current authenticated principal.</summary>
    public string? DisplayName => DisplayNameOf(accessor.HttpContext?.User, Claims.NameClaim);

    /// <summary>Gets the recognised roles the current authenticated principal's token carries.</summary>
    public IReadOnlySet<Role> Roles => RolesOf(
        accessor.HttpContext?.User,
        value => logger.LogWarning("Ignoring unknown identity-provider role {Role}.", value),
        Claims.RolesClaim);

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
    /// <param name="pattern">The deployment's complete-value validation expression.</param>
    /// <param name="staffIdClaim">The configured staff-number claim name.</param>
    /// <returns>The canonical staff number, or null when absent or malformed.</returns>
    public static StaffId? StaffIdOf(
        ClaimsPrincipal? principal,
        string pattern = EventBooking.Domain.Access.StaffId.DefaultPattern,
        string staffIdClaim = "staff_id")
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            if (StaffId.TryParse(identity.FindFirst(staffIdClaim)?.Value, out var staffId, pattern))
            {
                return staffId;
            }
        }

        return null;
    }

    /// <summary>Reads a human-readable name only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <param name="nameClaim">The configured name claim name.</param>
    /// <returns>The name, or null when absent, empty, or whitespace.</returns>
    public static string? DisplayNameOf(ClaimsPrincipal? principal, string nameClaim = "name")
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var value = identity.FindFirst(nameClaim)?.Value;
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
    /// <param name="rolesClaim">The configured roles claim name.</param>
    /// <returns>The parsed role set; empty when the claim is absent or unauthenticated.</returns>
    public static IReadOnlySet<Role> RolesOf(
        ClaimsPrincipal? principal,
        Action<string>? onRejected = null,
        string rolesClaim = "roles")
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var claims = identity.FindAll(rolesClaim).ToList();
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
