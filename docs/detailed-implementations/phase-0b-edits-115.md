# 00b — Vocabulary edits 115 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Web.Tests/SlotsClientCapacityAdjustmentTests.cs — 1/1

<!-- vocabulary-file: {"id":391,"oldPath":"tests/EventBooking.Web.Tests/SlotsClientCapacityAdjustmentTests.cs","newPath":"tests/EventBooking.Web.Tests/EventsClientCapacityAdjustmentTests.cs","beforeSha":"412a678535af32b4a2fc0953be40650249b27f1788dd468d28452ef26bb6efdc","afterSha":"02b0bd0562d08243965f11f814bbfbf0974c978d9b34ba7be6ba6b4123bb50da","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — tests/EventBooking.Web.Tests/EventsClientCapacityAdjustmentTests.cs — 1/1

<!-- vocabulary-file: {"id":391,"oldPath":"tests/EventBooking.Web.Tests/SlotsClientCapacityAdjustmentTests.cs","newPath":"tests/EventBooking.Web.Tests/EventsClientCapacityAdjustmentTests.cs","beforeSha":"412a678535af32b4a2fc0953be40650249b27f1788dd468d28452ef26bb6efdc","afterSha":"02b0bd0562d08243965f11f814bbfbf0974c978d9b34ba7be6ba6b4123bb50da","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class EventsClientCapacityAdjustmentTests
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
    public async Task AdjustingPutsOnlyTheReplacementTotalToTheCapacityResource()
    {
        var (client, handler) = Given();
        var eventId = Guid.NewGuid();

        var result = await client.AdjustCapacityAsync(
            eventId,
            12,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal(
            $"/api/events/{eventId}/capacity",
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
`````

## before — tests/EventBooking.Web.Tests/SlotsClientHeadcountRevisionTests.cs — 1/1

<!-- vocabulary-file: {"id":392,"oldPath":"tests/EventBooking.Web.Tests/SlotsClientHeadcountRevisionTests.cs","newPath":"tests/EventBooking.Web.Tests/EventsClientHeadcountRevisionTests.cs","beforeSha":"6ff1802554cc3c452043e0efcf855badd809fa226391692eb1fa7fcf04845895","afterSha":"1c6f4c265d4f293facc8d455bf5896f16cc9dd3cce2c7d245f8a0ae320dfed7a","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class SlotsClientHeadcountRevisionTests
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
    public async Task TheBoardReadsMyCurrentAcceptedHeadcount()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SlotBoardDto(
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
                $"/api/slots/proposals/{proposalId}/acceptance",
                request.RequestUri!.AbsolutePath);
        });
        Assert.Contains("10", await handler.Requests[0].Content!.ReadAsStringAsync());
        Assert.Contains("12", await handler.Requests[1].Content!.ReadAsStringAsync());
    }
}
`````

## after — tests/EventBooking.Web.Tests/EventsClientHeadcountRevisionTests.cs — 1/1

<!-- vocabulary-file: {"id":392,"oldPath":"tests/EventBooking.Web.Tests/SlotsClientHeadcountRevisionTests.cs","newPath":"tests/EventBooking.Web.Tests/EventsClientHeadcountRevisionTests.cs","beforeSha":"6ff1802554cc3c452043e0efcf855badd809fa226391692eb1fa7fcf04845895","afterSha":"1c6f4c265d4f293facc8d455bf5896f16cc9dd3cce2c7d245f8a0ae320dfed7a","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — tests/EventBooking.Web.Tests/SlotsClientTests.cs — 1/1

<!-- vocabulary-file: {"id":393,"oldPath":"tests/EventBooking.Web.Tests/SlotsClientTests.cs","newPath":"tests/EventBooking.Web.Tests/EventsClientTests.cs","beforeSha":"3702763afb81a6b3257ef6291e57e3c32647086876bebc514e49a8a3c7987beb","afterSha":"68c0a987afff007162657f9c4d3c612fced23e747b394a59069ac71bc73ef42e","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class SlotsClientTests
{
    /// <summary>Records what was asked for and answers with whatever the test set up.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public Queue<HttpResponseMessage> Responses { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(
                Responses.Count > 0 ? Responses.Dequeue() : ResponseFactory(request));
        }
    }

    private sealed class TrackingContent(string body) : HttpContent
    {
        public bool WasRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasRead = true;
            return stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = Encoding.UTF8.GetByteCount(body);
            return true;
        }
    }

    private sealed class TrackingResponseMessage : HttpResponseMessage
    {
        private readonly TrackingContent? _trackingContent;

        public TrackingResponseMessage(HttpStatusCode statusCode, TrackingContent? trackingContent = null)
            : base(statusCode)
        {
            _trackingContent = trackingContent;
            if (trackingContent is not null)
            {
                trackingContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Content = trackingContent;
            }
        }

        public bool WasDisposed { get; private set; }

        public bool WasDisposedAfterContentWasRead { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                WasDisposed = true;
                WasDisposedAfterContentWasRead = _trackingContent is null || _trackingContent.WasRead;
            }

            base.Dispose(disposing);
        }
    }

    private static (SlotsClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new SlotsClient(http), handler);
    }

    [Fact]
    public async Task TheBoardIsFetchedFromTheBoardRoute()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SlotBoardDto([], [])),
        };

        var outcome = await client.GetBoardAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("/api/slots/board", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ProposingPostsTheDateAndStartTime()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(Guid.NewGuid()),
        };

        await client.ProposeAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/slots/proposals", request.RequestUri!.AbsolutePath);
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("2026-09-10", body);
        Assert.Contains("09:00", body);
    }

    [Fact]
    public async Task AcceptingPostsTheHeadcountToTheAcceptanceRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.AcceptAsync(proposalId, 10, CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/slots/proposals/{proposalId}/acceptance", request.RequestUri!.AbsolutePath);
        Assert.Contains("10", await request.Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithdrawingAnAcceptanceDeletesTheAcceptanceRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.WithdrawAcceptanceAsync(proposalId, CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/slots/proposals/{proposalId}/acceptance",
            handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task WithdrawingAProposalDeletesTheProposalRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.WithdrawProposalAsync(proposalId, CancellationToken.None);

        Assert.Equal($"/api/slots/proposals/{proposalId}", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CancellingASlotPassesTheConfirmFlagInTheQueryString()
    {
        var (client, handler) = Given();
        var slotId = Guid.NewGuid();

        await client.CancelConfirmedSlotAsync(slotId, true, CancellationToken.None);

        Assert.Equal($"/api/slots/confirmed/{slotId}", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("?confirm=true", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task TheFirstUnconfirmedCancelSurfacesTheWarningText()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"conflict","detail":"Cancelling this slot will cancel 6 confirmed bookings. Affected candidates will be notified and re-invited. Confirm to proceed.","status":409}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.CancelConfirmedSlotAsync(Guid.NewGuid(), false, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("6 confirmed bookings", outcome.ErrorMessage);
    }

    [Fact]
    public async Task EveryResponseIsDisposedOnlyAfterTheClientHasConsumedItsContent()
    {
        var (client, handler) = Given();
        var boardContent = new TrackingContent(JsonSerializer.Serialize(new SlotBoardDto([], [])));
        var proposalContent = new TrackingContent(JsonSerializer.Serialize(Guid.NewGuid()));
        var boardResponse = new TrackingResponseMessage(HttpStatusCode.OK, boardContent);
        var proposalResponse = new TrackingResponseMessage(HttpStatusCode.Created, proposalContent);
        var acceptanceResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var withdrawnAcceptanceResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var withdrawnProposalResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var cancelledSlotResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var responses = new[]
        {
            boardResponse,
            proposalResponse,
            acceptanceResponse,
            withdrawnAcceptanceResponse,
            withdrawnProposalResponse,
            cancelledSlotResponse,
        };

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        var proposalId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        await client.GetBoardAsync(CancellationToken.None);
        await client.ProposeAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), CancellationToken.None);
        await client.AcceptAsync(proposalId, 1, CancellationToken.None);
        await client.WithdrawAcceptanceAsync(proposalId, CancellationToken.None);
        await client.WithdrawProposalAsync(proposalId, CancellationToken.None);
        await client.CancelConfirmedSlotAsync(slotId, false, CancellationToken.None);

        Assert.All(responses, response => Assert.True(response.WasDisposed));
        Assert.True(boardContent.WasRead);
        Assert.True(proposalContent.WasRead);
        Assert.True(boardResponse.WasDisposedAfterContentWasRead);
        Assert.True(proposalResponse.WasDisposedAfterContentWasRead);
    }

    [Fact]
    public async Task GetSlotOperationsCallsTheOperationsRouteAndMapsRows()
    {
        var handler = new StubHandler();
        var slotId = Guid.NewGuid();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SlotOperationsDto(
            [
                new SlotOperationDto(
                    slotId,
                    new DateOnly(2026, 9, 10),
                    new TimeOnly(9, 0),
                    new TimeOnly(13, 0),
                    [new SlotOperationCapacityDto("DAT", 10, 9)],
                    1),
            ])),
        });
        var client = new SlotsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var outcome = await client.GetSlotOperationsAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var slot = Assert.Single(outcome.Value!.Slots);
        Assert.Equal("/api/slots/operations", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(slotId, slot.ConfirmedSlotId);
        Assert.Equal(1, slot.ActiveBookings);
        Assert.Equal("DAT", Assert.Single(slot.Capacities).Code);
    }

    [Fact]
    public async Task GetSlotOperationsSurfacesAForbiddenResponseAsAFailure()
    {
        var handler = new StubHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = new SlotsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var outcome = await client.GetSlotOperationsAsync(CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, outcome.StatusCode);
    }
}
`````

## after — tests/EventBooking.Web.Tests/EventsClientTests.cs — 1/1

<!-- vocabulary-file: {"id":393,"oldPath":"tests/EventBooking.Web.Tests/SlotsClientTests.cs","newPath":"tests/EventBooking.Web.Tests/EventsClientTests.cs","beforeSha":"3702763afb81a6b3257ef6291e57e3c32647086876bebc514e49a8a3c7987beb","afterSha":"68c0a987afff007162657f9c4d3c612fced23e747b394a59069ac71bc73ef42e","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class EventsClientTests
{
    /// <summary>Records what was asked for and answers with whatever the test set up.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public Queue<HttpResponseMessage> Responses { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(
                Responses.Count > 0 ? Responses.Dequeue() : ResponseFactory(request));
        }
    }

    private sealed class TrackingContent(string body) : HttpContent
    {
        public bool WasRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasRead = true;
            return stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = Encoding.UTF8.GetByteCount(body);
            return true;
        }
    }

    private sealed class TrackingResponseMessage : HttpResponseMessage
    {
        private readonly TrackingContent? _trackingContent;

        public TrackingResponseMessage(HttpStatusCode statusCode, TrackingContent? trackingContent = null)
            : base(statusCode)
        {
            _trackingContent = trackingContent;
            if (trackingContent is not null)
            {
                trackingContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Content = trackingContent;
            }
        }

        public bool WasDisposed { get; private set; }

        public bool WasDisposedAfterContentWasRead { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                WasDisposed = true;
                WasDisposedAfterContentWasRead = _trackingContent is null || _trackingContent.WasRead;
            }

            base.Dispose(disposing);
        }
    }

    private static (EventsClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new EventsClient(http), handler);
    }

    [Fact]
    public async Task TheBoardIsFetchedFromTheBoardRoute()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new EventBoardDto([], [])),
        };

        var outcome = await client.GetBoardAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("/api/events/board", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ProposingPostsTheDateAndStartTime()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(Guid.NewGuid()),
        };

        await client.ProposeAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/event-proposals", request.RequestUri!.AbsolutePath);
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("2026-09-10", body);
        Assert.Contains("09:00", body);
    }

    [Fact]
    public async Task AcceptingPostsTheHeadcountToTheAcceptanceRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.AcceptAsync(proposalId, 10, CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/event-proposals/{proposalId}/acceptance", request.RequestUri!.AbsolutePath);
        Assert.Contains("10", await request.Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithdrawingAnAcceptanceDeletesTheAcceptanceRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.WithdrawAcceptanceAsync(proposalId, CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/event-proposals/{proposalId}/acceptance",
            handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task WithdrawingAProposalDeletesTheProposalRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.WithdrawProposalAsync(proposalId, CancellationToken.None);

        Assert.Equal($"/api/event-proposals/{proposalId}", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CancellingAEventPassesTheConfirmFlagInTheQueryString()
    {
        var (client, handler) = Given();
        var eventId = Guid.NewGuid();

        await client.CancelEventAsync(eventId, true, CancellationToken.None);

        Assert.Equal($"/api/events/{eventId}", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("?confirm=true", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task TheFirstUnconfirmedCancelSurfacesTheWarningText()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"conflict","detail":"Cancelling this event will cancel 6 confirmed bookings. Affected attendees will be notified and re-invited. Confirm to proceed.","status":409}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.CancelEventAsync(Guid.NewGuid(), false, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("6 confirmed bookings", outcome.ErrorMessage);
    }

    [Fact]
    public async Task EveryResponseIsDisposedOnlyAfterTheClientHasConsumedItsContent()
    {
        var (client, handler) = Given();
        var boardContent = new TrackingContent(JsonSerializer.Serialize(new EventBoardDto([], [])));
        var proposalContent = new TrackingContent(JsonSerializer.Serialize(Guid.NewGuid()));
        var boardResponse = new TrackingResponseMessage(HttpStatusCode.OK, boardContent);
        var proposalResponse = new TrackingResponseMessage(HttpStatusCode.Created, proposalContent);
        var acceptanceResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var withdrawnAcceptanceResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var withdrawnProposalResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var cancelledEventResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var responses = new[]
        {
            boardResponse,
            proposalResponse,
            acceptanceResponse,
            withdrawnAcceptanceResponse,
            withdrawnProposalResponse,
            cancelledEventResponse,
        };

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        var proposalId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await client.GetBoardAsync(CancellationToken.None);
        await client.ProposeAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), CancellationToken.None);
        await client.AcceptAsync(proposalId, 1, CancellationToken.None);
        await client.WithdrawAcceptanceAsync(proposalId, CancellationToken.None);
        await client.WithdrawProposalAsync(proposalId, CancellationToken.None);
        await client.CancelEventAsync(eventId, false, CancellationToken.None);

        Assert.All(responses, response => Assert.True(response.WasDisposed));
        Assert.True(boardContent.WasRead);
        Assert.True(proposalContent.WasRead);
        Assert.True(boardResponse.WasDisposedAfterContentWasRead);
        Assert.True(proposalResponse.WasDisposedAfterContentWasRead);
    }

    [Fact]
    public async Task GetEventOperationsCallsTheOperationsRouteAndMapsRows()
    {
        var handler = new StubHandler();
        var eventId = Guid.NewGuid();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new EventOperationsDto(
            [
                new EventOperationDto(
                    eventId,
                    new DateOnly(2026, 9, 10),
                    new TimeOnly(9, 0),
                    new TimeOnly(13, 0),
                    [new EventOperationCapacityDto("DAT", 10, 9)],
                    1),
            ])),
        });
        var client = new EventsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var outcome = await client.GetEventOperationsAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var eventItem = Assert.Single(outcome.Value!.Events);
        Assert.Equal("/api/events/operations", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(eventId, eventItem.EventId);
        Assert.Equal(1, eventItem.ActiveBookings);
        Assert.Equal("DAT", Assert.Single(eventItem.Capacities).Code);
    }

    [Fact]
    public async Task GetEventOperationsSurfacesAForbiddenResponseAsAFailure()
    {
        var handler = new StubHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = new EventsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var outcome = await client.GetEventOperationsAsync(CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, outcome.StatusCode);
    }
}
`````

## before — tests/EventBooking.Web.Tests/StaffNavigationTests.cs — 1/1

<!-- vocabulary-file: {"id":394,"oldPath":"tests/EventBooking.Web.Tests/StaffNavigationTests.cs","newPath":"tests/EventBooking.Web.Tests/StaffNavigationTests.cs","beforeSha":"13798a580596891efd3c96be8b7d0925b5f1a88419f78485376c06f7bac2d6fa","afterSha":"a0f1bd8188e65ed755eaf477a363a72bf0540f79f3b18dc1b330478ea877fc48","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the deterministic union of staff navigation links per role combination.</summary>
public class StaffNavigationTests
{
    /// <summary>Gets every valid role shape with its exact expected link union.</summary>
    public static TheoryData<string[], string[]> EveryValidShape => new()
    {
        { ["Admin"], ["/settings", "/staff-access", "/confirmed-slots", "/audit"] },
        { ["Coordinator"], ["/candidates", "/dashboards", "/confirmed-slots", "/audit"] },
        { ["Manager"], ["/slots", "/appointments"] },
        { ["AppointmentStaff"], ["/appointments"] },
        { ["Manager", "Coordinator"], ["/slots", "/appointments", "/candidates", "/dashboards", "/confirmed-slots", "/audit"] },
        { ["Coordinator", "AppointmentStaff"], ["/appointments", "/candidates", "/dashboards", "/confirmed-slots", "/audit"] },
        { ["Manager", "AppointmentStaff"], ["/slots", "/appointments"] },
        { ["Manager", "Coordinator", "AppointmentStaff"], ["/slots", "/appointments", "/candidates", "/dashboards", "/confirmed-slots", "/audit"] },
    };

    /// <summary>Verifies every valid profile shape receives its exact link union.</summary>
    [Theory]
    [MemberData(nameof(EveryValidShape))]
    public void EveryValidProfileShapeGetsItsExactLinkUnion(
        string[] roles,
        string[] expectedRoutes)
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            roles,
            roles.Contains("Manager") || roles.Contains("AppointmentStaff")
                ? Guid.NewGuid()
                : null,
            null));

        Assert.Equal(expectedRoutes, links.Select(link => link.Href));
    }

    /// <summary>Verifies administrators keep only administrative and slot-only links.</summary>
    [Fact]
    public void AdminHasOnlyAdministrativeAndSlotOnlyLinks()
    {
        var links = StaffNavigation.LinksFor(new MeDto(["Admin"], null, null));

        Assert.Equal(
            ["/settings", "/staff-access", "/confirmed-slots", "/audit"],
            links.Select(link => link.Href));
        Assert.DoesNotContain(links, link => link.Href is "/candidates" or "/dashboards");
    }

    /// <summary>Verifies the Staff access link describes scope assignment, since roles come from the identity provider.</summary>
    [Fact]
    public void StaffAccessLinkDescribesScopeAssignmentNotRoleAssignment()
    {
        var staffAccess = StaffNavigation.LinksFor(new MeDto(["Admin"], null, null))
            .Single(link => link.Href == "/staff-access");

        Assert.Equal("Set appointment-type scope", staffAccess.Description);
    }

    /// <summary>Verifies a coordinator-manager keeps the union without duplicates.</summary>
    [Fact]
    public void CoordinatorManagerGetsTheUnionWithoutDuplicates()
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            ["Manager", "Coordinator"],
            Guid.NewGuid(),
            "Medical Check-up"));

        Assert.Equal(
            ["/slots", "/appointments", "/candidates", "/dashboards", "/confirmed-slots", "/audit"],
            links.Select(link => link.Href));
        Assert.Equal(links.Count, links.Select(link => link.Href).Distinct().Count());
    }

    /// <summary>Verifies appointment-only staff see only the appointment workspace.</summary>
    [Fact]
    public void AppointmentStaffHasOnlyTheAppointmentWorkspace()
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            ["AppointmentStaff"], Guid.NewGuid(), "Uniform Fitting"));

        Assert.Equal(["/appointments"], links.Select(link => link.Href));
    }

    /// <summary>Verifies empty roles receive no links.</summary>
    [Fact]
    public void EmptyRolesHaveNoLinks()
    {
        Assert.Empty(StaffNavigation.LinksFor(new MeDto([], null, null)));
    }

    /// <summary>Verifies the audit trail is offered only to the roles that may search it.</summary>
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Coordinator", true)]
    [InlineData("Manager", false)]
    [InlineData("AppointmentStaff", false)]
    public void TheAuditTrailIsOfferedOnlyToRolesThatMaySearchIt(string role, bool expected)
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            [role],
            role is "Manager" or "AppointmentStaff" ? Guid.NewGuid() : null,
            null));

        Assert.Equal(expected, links.Any(link => link.Href == "/audit"));
    }
}
`````

## after — tests/EventBooking.Web.Tests/StaffNavigationTests.cs — 1/1

<!-- vocabulary-file: {"id":394,"oldPath":"tests/EventBooking.Web.Tests/StaffNavigationTests.cs","newPath":"tests/EventBooking.Web.Tests/StaffNavigationTests.cs","beforeSha":"13798a580596891efd3c96be8b7d0925b5f1a88419f78485376c06f7bac2d6fa","afterSha":"a0f1bd8188e65ed755eaf477a363a72bf0540f79f3b18dc1b330478ea877fc48","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the deterministic union of staff navigation links per role combination.</summary>
public class StaffNavigationTests
{
    /// <summary>Gets every valid role shape with its exact expected link union.</summary>
    public static TheoryData<string[], string[]> EveryValidShape => new()
    {
        { ["Admin"], ["/settings", "/staff-access", "/events/operations", "/audit"] },
        { ["Coordinator"], ["/attendees", "/dashboards", "/events/operations", "/audit"] },
        { ["Manager"], ["/events/negotiate", "/appointments"] },
        { ["AppointmentStaff"], ["/appointments"] },
        { ["Manager", "Coordinator"], ["/events/negotiate", "/appointments", "/attendees", "/dashboards", "/events/operations", "/audit"] },
        { ["Coordinator", "AppointmentStaff"], ["/appointments", "/attendees", "/dashboards", "/events/operations", "/audit"] },
        { ["Manager", "AppointmentStaff"], ["/events/negotiate", "/appointments"] },
        { ["Manager", "Coordinator", "AppointmentStaff"], ["/events/negotiate", "/appointments", "/attendees", "/dashboards", "/events/operations", "/audit"] },
    };

    /// <summary>Verifies every valid profile shape receives its exact link union.</summary>
    [Theory]
    [MemberData(nameof(EveryValidShape))]
    public void EveryValidProfileShapeGetsItsExactLinkUnion(
        string[] roles,
        string[] expectedRoutes)
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            roles,
            roles.Contains("Manager") || roles.Contains("AppointmentStaff")
                ? Guid.NewGuid()
                : null,
            null));

        Assert.Equal(expectedRoutes, links.Select(link => link.Href));
    }

    /// <summary>Verifies administrators keep only administrative and event-only links.</summary>
    [Fact]
    public void AdminHasOnlyAdministrativeAndEventOnlyLinks()
    {
        var links = StaffNavigation.LinksFor(new MeDto(["Admin"], null, null));

        Assert.Equal(
            ["/settings", "/staff-access", "/events/operations", "/audit"],
            links.Select(link => link.Href));
        Assert.DoesNotContain(links, link => link.Href is "/attendees" or "/dashboards");
    }

    /// <summary>Verifies the Staff access link describes scope assignment, since roles come from the identity provider.</summary>
    [Fact]
    public void StaffAccessLinkDescribesScopeAssignmentNotRoleAssignment()
    {
        var staffAccess = StaffNavigation.LinksFor(new MeDto(["Admin"], null, null))
            .Single(link => link.Href == "/staff-access");

        Assert.Equal("Set appointment-type scope", staffAccess.Description);
    }

    /// <summary>Verifies a coordinator-manager keeps the union without duplicates.</summary>
    [Fact]
    public void CoordinatorManagerGetsTheUnionWithoutDuplicates()
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            ["Manager", "Coordinator"],
            Guid.NewGuid(),
            "Medical Check-up"));

        Assert.Equal(
            ["/events/negotiate", "/appointments", "/attendees", "/dashboards", "/events/operations", "/audit"],
            links.Select(link => link.Href));
        Assert.Equal(links.Count, links.Select(link => link.Href).Distinct().Count());
    }

    /// <summary>Verifies appointment-only staff see only the appointment workspace.</summary>
    [Fact]
    public void AppointmentStaffHasOnlyTheAppointmentWorkspace()
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            ["AppointmentStaff"], Guid.NewGuid(), "Uniform Fitting"));

        Assert.Equal(["/appointments"], links.Select(link => link.Href));
    }

    /// <summary>Verifies empty roles receive no links.</summary>
    [Fact]
    public void EmptyRolesHaveNoLinks()
    {
        Assert.Empty(StaffNavigation.LinksFor(new MeDto([], null, null)));
    }

    /// <summary>Verifies the audit trail is offered only to the roles that may search it.</summary>
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Coordinator", true)]
    [InlineData("Manager", false)]
    [InlineData("AppointmentStaff", false)]
    public void TheAuditTrailIsOfferedOnlyToRolesThatMaySearchIt(string role, bool expected)
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            [role],
            role is "Manager" or "AppointmentStaff" ? Guid.NewGuid() : null,
            null));

        Assert.Equal(expected, links.Any(link => link.Href == "/audit"));
    }
}
`````

## before — tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs — 1/1

<!-- vocabulary-file: {"id":395,"oldPath":"tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs","newPath":"tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs","beforeSha":"243011c494022f0634d4eca71c2908e6711f08af3de405e3969bec2d7c548547","afterSha":"e6ef7f973e46632ba5e919e0b3a4797ded9a39217385f9b7f789bedb4bcfdffb","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the role-to-guide mapping bundled from docs/user-guides.</summary>
public class UserGuideCatalogTests
{
    [Fact]
    public void AnyRecognisedRoleGetsEveryGuideIncludingCandidate()
    {
        var guides = UserGuideCatalog.GuidesFor(["Admin"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Candidate"],
            guides.Select(g => g.RoleKey));
    }

    [Fact]
    public void CombinedRolesStillGetEveryGuideNotJustTheirOwn()
    {
        var guides = UserGuideCatalog.GuidesFor(["Coordinator", "Manager", "AppointmentStaff"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Candidate"],
            guides.Select(g => g.RoleKey));
    }

    [Fact]
    public void SingleRoleGetsEveryGuideNotJustItsOwn()
    {
        var guides = UserGuideCatalog.GuidesFor(["Coordinator"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Candidate"],
            guides.Select(g => g.RoleKey));
        Assert.Contains(guides, g => g.RoleKey == "Coordinator" && g.Html.Contains("Candidates"));
    }

    [Fact]
    public void MissingOrUnrecognisedRolesGetNoGuide()
    {
        Assert.Empty(UserGuideCatalog.GuidesFor([]));
        Assert.Empty(UserGuideCatalog.GuidesFor(["SomeFutureRole"]));
    }

    [Fact]
    public void EachGuideRendersMarkdownHeadingsAsHtml()
    {
        var guide = UserGuideCatalog.GuidesFor(["Admin"]).Single(g => g.RoleKey == "Admin");

        Assert.Contains("<h1", guide.Html);
        Assert.Contains("<h2", guide.Html);
    }

    [Fact]
    public void CrossGuideLinksAreRewrittenAwayFromBareMarkdownFilenames()
    {
        var manager = UserGuideCatalog.GuidesFor(["Manager"]).Single(g => g.RoleKey == "Manager");

        Assert.DoesNotContain("appointment-staff-guide.md", manager.Html);
        Assert.DoesNotContain("README.md", manager.Html);
        Assert.DoesNotContain("All user guides", manager.Html);
    }

    [Fact]
    public void CrossGuideAnchorLinksCarryTheHelpPathSoTheyDoNotResolveAgainstBaseHref()
    {
        // Blazor's <base href="/"> resolves a bare "#anchor" against "/" (Home), not against the
        // current route, so every in-page cross-link must be an absolute "/help#..." path.
        var manager = UserGuideCatalog.GuidesFor(["Manager"]).Single(g => g.RoleKey == "Manager");

        Assert.Contains("href=\"/help#appointmentstaff\"", manager.Html);
    }

    [Fact]
    public void CandidateGuideIsAvailableOutsideTheRoleMap()
    {
        var guide = UserGuideCatalog.CandidateGuide();

        Assert.Equal("Candidate", guide.RoleKey);
        Assert.Contains("<h1", guide.Html);
        Assert.DoesNotContain("README.md", guide.Html);
    }
}
`````

## after — tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs — 1/1

<!-- vocabulary-file: {"id":395,"oldPath":"tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs","newPath":"tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs","beforeSha":"243011c494022f0634d4eca71c2908e6711f08af3de405e3969bec2d7c548547","afterSha":"e6ef7f973e46632ba5e919e0b3a4797ded9a39217385f9b7f789bedb4bcfdffb","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the role-to-guide mapping bundled from docs/user-guides.</summary>
public class UserGuideCatalogTests
{
    [Fact]
    public void AnyRecognisedRoleGetsEveryGuideIncludingAttendee()
    {
        var guides = UserGuideCatalog.GuidesFor(["Admin"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Attendee"],
            guides.Select(g => g.RoleKey));
    }

    [Fact]
    public void CombinedRolesStillGetEveryGuideNotJustTheirOwn()
    {
        var guides = UserGuideCatalog.GuidesFor(["Coordinator", "Manager", "AppointmentStaff"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Attendee"],
            guides.Select(g => g.RoleKey));
    }

    [Fact]
    public void SingleRoleGetsEveryGuideNotJustItsOwn()
    {
        var guides = UserGuideCatalog.GuidesFor(["Coordinator"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Attendee"],
            guides.Select(g => g.RoleKey));
        Assert.Contains(guides, g => g.RoleKey == "Coordinator" && g.Html.Contains("Attendees"));
    }

    [Fact]
    public void MissingOrUnrecognisedRolesGetNoGuide()
    {
        Assert.Empty(UserGuideCatalog.GuidesFor([]));
        Assert.Empty(UserGuideCatalog.GuidesFor(["SomeFutureRole"]));
    }

    [Fact]
    public void EachGuideRendersMarkdownHeadingsAsHtml()
    {
        var guide = UserGuideCatalog.GuidesFor(["Admin"]).Single(g => g.RoleKey == "Admin");

        Assert.Contains("<h1", guide.Html);
        Assert.Contains("<h2", guide.Html);
    }

    [Fact]
    public void CrossGuideLinksAreRewrittenAwayFromBareMarkdownFilenames()
    {
        var manager = UserGuideCatalog.GuidesFor(["Manager"]).Single(g => g.RoleKey == "Manager");

        Assert.DoesNotContain("appointment-staff-guide.md", manager.Html);
        Assert.DoesNotContain("README.md", manager.Html);
        Assert.DoesNotContain("All user guides", manager.Html);
    }

    [Fact]
    public void CrossGuideAnchorLinksCarryTheHelpPathSoTheyDoNotResolveAgainstBaseHref()
    {
        // Blazor's <base href="/"> resolves a bare "#anchor" against "/" (Home), not against the
        // current route, so every in-page cross-link must be an absolute "/help#..." path.
        var manager = UserGuideCatalog.GuidesFor(["Manager"]).Single(g => g.RoleKey == "Manager");

        Assert.Contains("href=\"/help#appointmentstaff\"", manager.Html);
    }

    [Fact]
    public void AttendeeGuideIsAvailableOutsideTheRoleMap()
    {
        var guide = UserGuideCatalog.AttendeeGuide();

        Assert.Equal("Attendee", guide.RoleKey);
        Assert.Contains("<h1", guide.Html);
        Assert.DoesNotContain("README.md", guide.Html);
    }
}
`````

## after — tests/EventBooking.Api.Tests/VocabularyContractTests.cs — 1/1

<!-- vocabulary-file: {"id":396,"newPath":"tests/EventBooking.Api.Tests/VocabularyContractTests.cs","beforeSha":null,"afterSha":"f6005e1ea872d5a8e57bc701104454aa258bd6f3f84699d1326d39acc0231f89","side":"after","part":1,"parts":1} -->

`````csharp
using System.Reflection;

namespace EventBooking.Api.Tests;

public sealed class VocabularyContractTests
{
    [Fact]
    public void Public_domain_and_application_contracts_use_canonical_names()
    {
        string[] forbidden = ["Candi" + "date", "Slo" + "t", "Employee" + "Group", "Head" + "Office"];
        var names = new[] { "EventBooking.Domain", "EventBooking.Application" }
            .Select(Assembly.Load)
            .SelectMany(assembly => assembly.GetExportedTypes())
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(member => type.FullName + "." + member.Name).Append(type.FullName!));
        Assert.DoesNotContain(names, name => forbidden.Any(term => name.Contains(term, StringComparison.Ordinal)));
    }
}
`````

## after — tests/EventBooking.Api.Tests/VocabularySurfaceTests.cs — 1/1

<!-- vocabulary-file: {"id":397,"newPath":"tests/EventBooking.Api.Tests/VocabularySurfaceTests.cs","beforeSha":null,"afterSha":"714098069f03d45d6eb6d36f2ad923d2cbfacf3f7df1c4fa5e4a2ad4283e37cb","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class VocabularySurfaceTests(ApiFactory factory)
{
    [Fact]
    public async Task OpenApi_paths_and_schemas_use_canonical_vocabulary()
    {
        using var document = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas")
            .EnumerateObject().Select(p => p.Name);
        string[] retired = ["candi" + "date", "slo" + "t", "employee" + "group", "head" + "office"];
        Assert.DoesNotContain(paths.Concat(schemas), name => retired.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("/api/attendees", paths);
        Assert.Contains("/api/event-proposals", paths);
        Assert.Contains("/api/events/{id}", paths);
    }
}
`````

## after — tests/EventBooking.Mcp.Tests/VocabularyToolTests.cs — 1/1

<!-- vocabulary-file: {"id":398,"newPath":"tests/EventBooking.Mcp.Tests/VocabularyToolTests.cs","beforeSha":null,"afterSha":"c9782e10c69b0ab5c2bf40715aaafbd5b38c982d78f036e59b874007cc384f8e","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class VocabularyToolTests(McpFactory factory)
{
    [Fact]
    public async Task Advertised_tools_use_canonical_vocabulary()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"names\",\"method\":\"tools/list\"}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        using var json = JsonDocument.Parse(data);
        var names = json.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!).ToArray();
        Assert.NotEmpty(names);
        string[] retired = ["candi" + "date", "slo" + "t", "employee" + "group", "head" + "office"];
        Assert.DoesNotContain(names, name => retired.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }
}
`````
