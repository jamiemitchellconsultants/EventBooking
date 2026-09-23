using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class SlotsClientCapacityAdjustmentTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public HttpResponseMessage Response { get; set; } =
            new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new AdjustConfirmedCapacityDto(Guid.Empty, 12, 6)),
            };

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }

    private static (SlotsClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        return (new SlotsClient(http), handler);
    }

    [Fact]
    public async Task AdjustingPutsOnlyTheReplacementTotalToTheCapacityResource()
    {
        var (client, handler) = Given();
        var slotId = Guid.NewGuid();

        var result = await client.AdjustCapacityAsync(
            slotId,
            12,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal(
            $"/api/slots/confirmed/{slotId}/capacity",
            handler.Request.RequestUri!.AbsolutePath);
        var body = await handler.Request.Content!.ReadAsStringAsync();
        Assert.Contains("12", body);
        Assert.DoesNotContain("appointmentType", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACapacityConflictReturnsTheActiveBookingCountMessage()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"conflict","detail":"Headcount cannot be lower than the active-booking count of 6.","status":409}""",
                System.Text.Encoding.UTF8,
                "application/problem+json"),
        };

        var result = await client.AdjustCapacityAsync(
            Guid.NewGuid(),
            5,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            "Headcount cannot be lower than the active-booking count of 6.",
            result.ErrorMessage);
    }
}
