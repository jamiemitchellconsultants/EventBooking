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
            T? value;
            try
            {
                value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
            }
            catch (JsonException)
            {
                value = default;
            }

            if (value is not null) return ApiOutcome<T>.Success(value, status);
            return ApiOutcome<T>.Failure(Unexpected(status));
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

    private static async Task<ApiProblem> ParseAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = document.RootElement;
            // A gateway or proxy can answer with any JSON at all; only an object whose `type`
            // is a string is a problem document this client branches on.
            if (root.ValueKind != JsonValueKind.Object) return Unexpected(status);
            if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                return Unexpected(status) with { Errors = Errors(root) };
            return new(
                type.GetString()!,
                String(root, "title"),
                status,
                String(root, "detail"),
                Errors(root),
                Element(root, "current"), Element(root, "consequence"),
                root.TryGetProperty("minimum", out var minimum)
                    && minimum.ValueKind == JsonValueKind.Number
                    && minimum.TryGetInt32(out var value) ? value : null,
                Element(root, "blocking"));
        }
        catch (JsonException)
        {
            return Unexpected(status);
        }
    }

    private static IReadOnlyList<FieldProblem> Errors(JsonElement root) =>
        root.TryGetProperty("errors", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Object)
                .Select(x => new FieldProblem(
                    String(x, "field"),
                    x.TryGetProperty("line", out var line)
                        && line.ValueKind == JsonValueKind.Number
                        && line.TryGetInt32(out var number) ? number : null,
                    String(x, "code") ?? "invalid",
                    String(x, "message")))
                .ToArray()
            : [];

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static JsonElement? Element(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.Clone() : null;

    private static ApiProblem Unexpected(int status) =>
        new("unexpected", null, status, Generic, [], null, null, null, null);
}
