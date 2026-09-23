using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record MeDto(
    IReadOnlyList<string> Roles,
    Guid? AppointmentTypeId,
    string? AppointmentTypeName);

public sealed class MeClient(HttpClient http)
{
    public async Task<ApiOutcome<MeDto>> GetAsync(CancellationToken cancellationToken)
    {
        var response = await http.GetAsync("/api/me", cancellationToken);
        return await ApiCall.ReadAsync<MeDto>(response, cancellationToken);
    }
}
