using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class EventsClientHeadcountRevisionTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(ResponseFactory(request));
        }
    }

    private static (EventsClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        return (new EventsClient(http), handler);
    }

    [Fact]
    public async Task TheBoardReadsMyCurrentAcceptedHeadcount()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new EventBoardDto(
                [
                    new OpenProposalDto(
                        proposalId,
                        new DateOnly(2026, 9, 10),
                        new TimeOnly(9, 0),
                        new TimeOnly(13, 0),
                        ["Drug & Alcohol Testing"],
                        12,
                        true,
                        false),
                ],
                [])),
        };

        var outcome = await client.GetBoardAsync(CancellationToken.None);

        var proposal = Assert.Single(outcome.Value!.OpenProposals);
        Assert.True(proposal.AcceptedByMe);
        Assert.Equal(12, proposal.MyAcceptedHeadcount);
    }

    [Fact]
    public async Task AcceptAndUpdatePostToTheSameAcceptanceResource()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.AcceptAsync(proposalId, 10, CancellationToken.None);
        await client.AcceptAsync(proposalId, 12, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                $"/api/event-proposals/{proposalId}/acceptance",
                request.RequestUri!.AbsolutePath);
        });
        Assert.Contains("10", await handler.Requests[0].Content!.ReadAsStringAsync());
        Assert.Contains("12", await handler.Requests[1].Content!.ReadAsStringAsync());
    }
}
