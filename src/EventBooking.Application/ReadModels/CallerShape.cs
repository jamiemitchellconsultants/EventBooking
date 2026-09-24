namespace EventBooking.Application.ReadModels;

/// <summary>What every Task 20 read model needs to refuse the wrong caller by itself. Resolved once per request from the access profile; Admin-shaped means IsAdmin regardless of scope.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="IsAdmin">Whether the caller holds the Admin role.</param>
/// <param name="Roles">The caller's role names.</param>
public sealed record CallerShape(Guid StaffUserId, bool IsAdmin, IReadOnlySet<string> Roles);
