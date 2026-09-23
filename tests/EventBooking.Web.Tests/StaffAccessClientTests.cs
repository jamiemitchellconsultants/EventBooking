using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class StaffAccessClientTests
{
    [Fact]
    public async Task ListReplaceScopeAndClearScopeUseTheScopeOnlyContract()
    {
        var target = Guid.NewGuid();
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create<IReadOnlyList<StaffAccessProfileDto>>([]),
        });
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new StaffAccessMutationDto(
                new StaffAccessProfileDto(
                    target, ["Manager"], Guid.NewGuid(), "Medical Check-up", 2),
                null)),
        });
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new StaffAccessClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        });

        await client.ListAsync(CancellationToken.None);
        await client.ReplaceScopeAsync(target, Guid.NewGuid(), 1, CancellationToken.None);
        await client.ClearScopeAsync(target, 2, CancellationToken.None);

        Assert.Collection(
            handler.Requests,
            request => Assert.Equal("/api/admin/staff-access", request.RequestUri!.PathAndQuery),
            request =>
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.Equal($"/api/admin/staff-access/{target}", request.RequestUri!.PathAndQuery);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Delete, request.Method);
                Assert.Equal(
                    $"/api/admin/staff-access/{target}?expectedVersion=2",
                    request.RequestUri!.PathAndQuery);
            });
        Assert.DoesNotContain("roles", handler.Bodies[1]);
        Assert.Contains("\"expectedVersion\":1", handler.Bodies[1]);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        public Queue<HttpResponseMessage> Responses { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return Responses.Dequeue();
        }
    }
}
