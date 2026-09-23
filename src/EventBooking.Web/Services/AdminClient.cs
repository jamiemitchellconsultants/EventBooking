using System.Net.Http.Json;
using System.Text;

namespace EventBooking.Web.Services;

/// <summary>One appointment type and the manager assigned to it, when there is one.</summary>
/// <param name="Id">The appointment type identifier.</param>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="ManagerUserId">The assigned manager's provider identity, or null when unassigned.</param>
/// <param name="ManagerStaffId">The manager's enterprise staff number; null until an identity is recorded.</param>
/// <param name="ManagerDisplayName">
/// The manager's name mirrored from the identity provider; null when the identity carries none.
/// </param>
public sealed record AppointmentTypeDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ManagerUserId,
    string? ManagerStaffId = null,
    string? ManagerDisplayName = null);

public sealed record SettingsDto(
    int InviteExpiryDays,
    int MaxAutoRetryCount,
    IReadOnlyList<AppointmentTypeDto> AppointmentTypes);

public sealed class AdminClient(HttpClient http)
{
    public async Task<ApiOutcome<SettingsDto>> GetAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/admin/settings", cancellationToken);
        return await ApiCall.ReadAsync<SettingsDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> UpdateAsync(
        int inviteExpiryDays, int maxAutoRetryCount, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(
            "/api/admin/settings",
            new { InviteExpiryDays = inviteExpiryDays, MaxAutoRetryCount = maxAutoRetryCount },
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }
}
