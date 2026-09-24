using System.Net.Http.Json;
using System.Text.Json;

namespace EventBooking.Web.Services;

public static class ApiCall
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Generic = "Something went wrong. Please try again.";

    public static async Task<ApiOutcome<T>> ReadAsync<T>(
        HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        if (response.IsSuccessStatusCode && status != (int)System.Net.HttpStatusCode.NoContent)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
            if (value is not null) return ApiOutcome<T>.Success(value, status);
            return ApiOutcome<T>.Failure(
                new ApiProblem("unexpected", null, status, Generic, [], null, null, null, null));
        }
        var problem = await ParseAsync(response, ct);
        return ApiOutcome<T>.Failure(problem with { Status = status });
    }

    public static async Task<ApiOutcome<bool>> ReadNoContentAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return ApiOutcome<bool>.Success(true, (int)response.StatusCode);
        var problem = await ParseAsync(response, ct);
        return ApiOutcome<bool>.Failure(problem with { Status = (int)response.StatusCode });
    }

    public static async Task<ApiOutcome<T>> ReadTextAsync<T>(
        HttpResponseMessage response, Func<string, T> parse, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            return ApiOutcome<T>.Success(parse(text), (int)response.StatusCode);
        }
        var problem = await ParseAsync(response, ct);
        return ApiOutcome<T>.Failure(problem with { Status = (int)response.StatusCode });
    }

    // Compatibility for the predecessor appointments client until Task 26 replaces it.
    public static async Task<ApiOutcome<TextWithHeader>> ReadTextWithHeaderAsync(
        HttpResponseMessage response, string headerName, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            var failure = await ParseAsync(response, cancellationToken);
            return ApiOutcome<TextWithHeader>.Failure(failure.Detail ?? Generic, status, failure.Type);
        }

        // Deliberately not the JSON path: the successful body here is raw text, not JSON.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var header =
            (response.Content.Headers.TryGetValues(headerName, out var contentValues)
                ? contentValues.FirstOrDefault()
                : null)
            ?? (response.Headers.TryGetValues(headerName, out var values)
                ? values.FirstOrDefault()
                : null)
            ?? string.Empty;

        return ApiOutcome<TextWithHeader>.Success(new TextWithHeader(body, header), status);
    }

    private static async Task<ApiProblem> ParseAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = document.RootElement;
            IReadOnlyList<FieldProblem> Errors() => root.TryGetProperty("errors", out var list)
                ? list.EnumerateArray().Select(x => new FieldProblem(
                    x.TryGetProperty("field", out var field) ? field.GetString() : null,
                    x.TryGetProperty("line", out var line) && line.ValueKind == JsonValueKind.Number
                        ? line.GetInt32() : null,
                    x.TryGetProperty("code", out var code) ? code.GetString() ?? "invalid" : "invalid",
                    x.TryGetProperty("message", out var message) ? message.GetString() : null))
                    .ToArray()
                : [];
            JsonElement? Element(string name) => root.TryGetProperty(name, out var value)
                ? value.Clone() : null;
            if (!root.TryGetProperty("type", out var type))
                return new("unexpected", null, (int)response.StatusCode, Generic, Errors(),
                    null, null, null, null);
            return new(
                type.GetString() ?? "unexpected",
                root.TryGetProperty("title", out var title) ? title.GetString() : null,
                (int)response.StatusCode,
                root.TryGetProperty("detail", out var detail) ? detail.GetString() : null,
                Errors(),
                Element("current"), Element("consequence"),
                root.TryGetProperty("minimum", out var minimum) &&
                    minimum.ValueKind == JsonValueKind.Number ? minimum.GetInt32() : null,
                Element("blocking"));
        }
        catch (JsonException)
        {
            return new("unexpected", null, (int)response.StatusCode, Generic, [],
                null, null, null, null);
        }
    }
}

public sealed record TextWithHeader(string Text, string Header);
