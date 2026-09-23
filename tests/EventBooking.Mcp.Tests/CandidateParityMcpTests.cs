using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class CandidateParityMcpTests(McpFactory factory)
{
    [Fact]
    public async Task ReadinessAndBookingListReturnSafeViews()
    {
        var seeded = await McpScenarioSeeder.GivenBookedCandidateAsync(factory);
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        using var readiness = await CallResultAsync("get_candidate_readiness", new { candidateId = seeded.CandidateId });
        Assert.Equal(seeded.CandidateId, readiness.RootElement.GetProperty("candidateId").GetGuid());
        Assert.Equal("AppointmentsOutstanding", readiness.RootElement.GetProperty("code").GetString());
        using var bookings = await CallResultAsync("list_candidate_bookings", new { candidateId = seeded.CandidateId });
        var row = Assert.Single(bookings.RootElement.EnumerateArray());
        Assert.Equal(seeded.BookingId, row.GetProperty("bookingId").GetGuid());
        Assert.DoesNotContain("token", row.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CoordinatorStartsAndCancelsRecovery()
    {
        var seeded = await McpScenarioSeeder.GivenCandidateWithNoShowAsync(factory);
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        using var started = await CallResultAsync("start_recovery_invite", new { candidateId = seeded.CandidateId });
        var inviteId = started.RootElement.GetProperty("inviteId").GetGuid();
        Assert.NotEqual(Guid.Empty, inviteId);
        var cancelled = await CallAsync(
            "cancel_recovery_invite", new { candidateId = seeded.CandidateId, inviteId });
        Assert.False(cancelled.GetProperty("result").TryGetProperty("isError", out var cancelError) && cancelError.GetBoolean(), cancelled.GetRawText());
        Assert.Equal("Recovery invite cancelled.", cancelled.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task CoordinatorCancelsCandidateBooking()
    {
        var seeded = await McpScenarioSeeder.GivenBookedCandidateAsync(factory);
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        using var result = await CallResultAsync("cancel_candidate_booking", new
        {
            candidateId = seeded.CandidateId, bookingId = seeded.BookingId, rebook = false,
        });
        Assert.False(result.RootElement.GetProperty("reinvited").GetBoolean());
    }

    [Fact]
    public async Task AdminCannotReadCandidateReadiness()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        var payload = await CallAsync("get_candidate_readiness", new { candidateId = Guid.NewGuid() });
        Assert.True(payload.GetProperty("result").GetProperty("isError").GetBoolean());
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
