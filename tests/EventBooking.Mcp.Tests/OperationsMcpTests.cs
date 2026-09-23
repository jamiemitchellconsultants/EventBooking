using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class OperationsMcpTests(McpFactory factory)
{
    [Fact]
    public async Task AdminSearchesOperationalAuditWithoutAttendeeRows()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        using var result = await CallResultAsync("search_audit", new { pageSize = 10 });
        Assert.Equal(JsonValueKind.Array, result.RootElement.GetProperty("rows").ValueKind);
        Assert.True(result.RootElement.TryGetProperty("nextCursor", out _));
    }

    [Fact]
    public async Task SearchRejectsMalformedTimestamp()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var payload = await CallAsync("search_audit", new { from = "not-a-timestamp" });
        Assert.True(payload.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task ScopedStaffExportsRosterTextAndFilename()
    {
        var eventId = await McpScenarioSeeder.GivenAppointmentWorkspaceAsync(
            factory, AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        using var result = await CallResultAsync(
            "export_appointment_roster", new { eventId = eventId });
        Assert.StartsWith("roster-drug-&-alcohol-testing-", result.RootElement.GetProperty("fileName").GetString());
        Assert.Contains("Attendee Name,Attendee Email", result.RootElement.GetProperty("csvText").GetString());
    }

    private async Task<JsonDocument> CallResultAsync(string name, object arguments)
    {
        var payload = await CallAsync(name, arguments);
        Assert.False(payload.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean(), payload.GetRawText());
        return JsonDocument.Parse(payload.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!);
    }

    private async Task<JsonElement> CallAsync(string name, object arguments)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = "1", method = "tools/call", @params = new { name, arguments },
            }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }
}
