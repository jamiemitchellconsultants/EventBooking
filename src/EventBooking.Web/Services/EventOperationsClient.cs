using System.Text;

namespace EventBooking.Web.Services;

public sealed record EventImportErrorDto(int LineNumber, string Message);

public sealed record EventImportOutcomeDto(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<EventImportErrorDto> Errors);

public sealed class EventOperationsClient(HttpClient http)
{
    public async Task<ApiOutcome<EventImportOutcomeDto>> ImportAsync(
        string csv,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        using var response = await http.PostAsync(
            "/api/events/import", content, cancellationToken);
        return await ApiCall.ReadAsync<EventImportOutcomeDto>(response, cancellationToken);
    }
}
