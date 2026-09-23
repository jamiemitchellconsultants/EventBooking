using System.Text;

namespace EventBooking.Web.Services;

public sealed record SlotImportErrorDto(int LineNumber, string Message);

public sealed record SlotImportOutcomeDto(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<SlotImportErrorDto> Errors);

public sealed class ConfirmedSlotsClient(HttpClient http)
{
    public async Task<ApiOutcome<SlotImportOutcomeDto>> ImportAsync(
        string csv,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        using var response = await http.PostAsync(
            "/api/confirmed-slots/import", content, cancellationToken);
        return await ApiCall.ReadAsync<SlotImportOutcomeDto>(response, cancellationToken);
    }
}
