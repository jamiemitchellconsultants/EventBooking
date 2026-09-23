namespace EventBooking.Api.Auth;

/// <summary>Configurable claim names plus the staff-number pattern, bound from
/// <c>Auth:Claims:StaffId/Name/Roles</c> and <c>Identity:StaffIdPattern</c>.</summary>
public sealed class AuthClaimOptions
{
    /// <summary>Gets or sets the claim carrying the enterprise staff number.</summary>
    public string StaffIdClaim { get; set; } = "staff_id";

    /// <summary>Gets or sets the claim carrying the caller's full name.</summary>
    public string NameClaim { get; set; } = "name";

    /// <summary>Gets or sets the claim carrying identity-provider-assigned roles.</summary>
    public string RolesClaim { get; set; } = "roles";

    /// <summary>Gets or sets the deployment's complete-value staff-number expression.</summary>
    public string StaffIdPattern { get; set; } = Domain.Access.StaffId.DefaultPattern;
}
