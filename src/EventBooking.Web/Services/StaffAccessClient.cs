using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record StaffAccessProfileDto(Guid StaffUserId, string? StaffId, string? DisplayName,
    IReadOnlyList<string> Roles, Guid? AppointmentTypeId, long Version,
    [property: System.Text.Json.Serialization.JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);
public sealed record SetStaffScopeOutcome(Guid TargetStaffUserId, Guid? AppointmentTypeId, string? DisplacedManagerDisplayName);

public sealed class StaffAccessClient(HttpClient http)
{
    public async Task<ApiOutcome<PageDto<StaffAccessProfileDto>>> ListAsync(CancellationToken ct)
    { using var response = await http.GetAsync("/api/staff-access", ct); return await ApiCall.ReadAsync<PageDto<StaffAccessProfileDto>>(response, ct); }
    public async Task<ApiOutcome<SetStaffScopeOutcome>> SetScopeAsync(Guid staffUserId, Guid? typeId, long version, CancellationToken ct)
    { using var response = await http.PutAsJsonAsync($"/api/staff-access/{staffUserId}/scope", new { appointmentTypeId = typeId, expectedVersion = version }, ct); return await ApiCall.ReadAsync<SetStaffScopeOutcome>(response, ct); }
}
