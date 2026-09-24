using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests.Contracts;

public sealed class OpenApiClientContractTests
{
    private static readonly (Type Client, string Schema)[] Contracts =
    [
        (typeof(ApiLink), "ApiLink"),
        (typeof(EventTimeDto), "EventTimeResponse"),
        (typeof(MeDto), "CurrentStaffResponse"),
    ];

    [Fact]
    public void ClientContractsExactlyMatchTheirOpenApiSchemas()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SnapshotPath()));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var nullability = new NullabilityInfoContext();

        foreach (var (client, schemaName) in Contracts)
        {
            var schema = schemas.GetProperty(schemaName);
            var properties = schema.GetProperty("properties");
            var clientProperties = client.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                .ToArray();
            var clientNames = clientProperties.Select(JsonName).Order().ToArray();
            var schemaNames = properties.EnumerateObject().Select(x => x.Name).Order().ToArray();
            Assert.Equal(schemaNames, clientNames);

            var required = schema.TryGetProperty("required", out var requiredElement)
                ? requiredElement.EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal)
                : [];
            foreach (var property in clientProperties)
            {
                var name = JsonName(property);
                var clientAllowsNull = property.PropertyType.IsValueType
                    ? Nullable.GetUnderlyingType(property.PropertyType) is not null
                    : nullability.Create(property).ReadState != NullabilityState.NotNull;
                Assert.Equal(clientAllowsNull,
                    AllowsNull(document.RootElement, properties.GetProperty(name)));
                if (!clientAllowsNull) Assert.Contains(name, required);
            }
        }
    }

    [Fact]
    public void EveryListOperationReturnsItemsAndNextCursor()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SnapshotPath()));
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            if (!path.Value.TryGetProperty("get", out var get)
                || !get.TryGetProperty("x-eventbooking-list", out var marker)
                || !marker.GetBoolean())
            {
                continue;
            }

            var schema = get.GetProperty("responses").GetProperty("200")
                .GetProperty("content").GetProperty("application/json").GetProperty("schema");
            var properties = ResolveSchema(document.RootElement, schema).GetProperty("properties");
            Assert.True(properties.TryGetProperty("items", out _), path.Name);
            Assert.True(properties.TryGetProperty("nextCursor", out _), path.Name);
        }
    }

    private static JsonElement ResolveSchema(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference)) return schema;
        return reference.GetString()!.Split('/').Skip(1).Aggregate(root, (value, part) => value.GetProperty(part));
    }

    private static bool AllowsNull(JsonElement root, JsonElement schema)
    {
        if (schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean()) return true;
        if (schema.TryGetProperty("type", out var type))
        {
            if (type.ValueKind == JsonValueKind.String) return type.GetString() == "null";
            if (type.ValueKind == JsonValueKind.Array)
                return type.EnumerateArray().Any(x => x.GetString() == "null");
        }
        if (schema.TryGetProperty("anyOf", out var anyOf))
            return anyOf.EnumerateArray().Any(item => AllowsNull(root, item));
        return schema.TryGetProperty("$ref", out var reference)
            && AllowsNull(root, ResolveSchema(root, schema));
    }

    private static string JsonName(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

    private static string SnapshotPath() => Path.Combine(
        AppContext.BaseDirectory, "Contracts", "openapi-v1.json");
}
