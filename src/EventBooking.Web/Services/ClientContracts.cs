using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventBooking.Web.Services;

public sealed record ApiLink(string Href, string Method, string OperationId);
public sealed record PageDto<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record FieldProblem(string? Field, int? Line, string Code, string? Message);
public sealed record ApiProblem(
    string Type, string? Title, int Status, string? Detail,
    IReadOnlyList<FieldProblem> Errors,
    JsonElement? Current, JsonElement? Consequence, int? Minimum, JsonElement? Blocking)
{
    public static ApiProblem Validation(IReadOnlyDictionary<string, string[]> errors) => new(
        "validation-failed", "Validation failed", 422, null,
        errors.SelectMany(pair => pair.Value.Select(message =>
            new FieldProblem(pair.Key, null, "invalid", message))).ToArray(),
        null, null, null, null);

    public static ApiProblem FromSlug(
        string slug, string detail,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        extensions ??= new Dictionary<string, object?>();
        extensions.TryGetValue("current", out var current);
        extensions.TryGetValue("consequence", out var consequence);
        extensions.TryGetValue("blocking", out var blocking);
        var minimum = extensions.TryGetValue("minimum", out var rawMinimum)
            ? Convert.ToInt32(rawMinimum, System.Globalization.CultureInfo.InvariantCulture)
            : (int?)null;
        return new(slug, "Request refused", 409, detail, [],
            Element(current), Element(consequence), minimum, Element(blocking));
    }

    private static JsonElement? Element(object? value) =>
        value is null ? null : JsonSerializer.SerializeToElement(value);
}
public sealed record EventTimeDto(
    DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    DateTimeOffset StartLocal, DateTimeOffset EndLocal,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    string TimeZoneId, string ZoneAbbreviation);

[method: JsonConstructor]
public sealed record MeDto(
    string? DisplayName, string? StaffId, IReadOnlyList<string> Roles,
    Guid? ScopeAppointmentTypeId, string? ScopeAppointmentTypeCode,
    string? ScopeAppointmentTypeName, IReadOnlyList<string> Capabilities, string? Problem,
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    public MeDto(
        IReadOnlyList<string> roles, Guid? appointmentTypeId, string? appointmentTypeName)
        : this(null, null, roles, appointmentTypeId, null, appointmentTypeName, [], null,
            new Dictionary<string, ApiLink>()) { }

    // Compatibility aliases are client-only and remain excluded from the wire contract.
    [JsonIgnore] public Guid? AppointmentTypeId => ScopeAppointmentTypeId;
    [JsonIgnore] public string? AppointmentTypeName => ScopeAppointmentTypeName;
}

public static class LinkRelations
{
    public static bool Allows(this IReadOnlyDictionary<string, ApiLink> links, string relation) =>
        links.ContainsKey(relation);
}
