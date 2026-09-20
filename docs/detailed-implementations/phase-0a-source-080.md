# 00a — Port source 80 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Web.Tests/AuditClientTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/AuditClientTests.cs","encoding":"utf8","sha256":"d99143ae6d29a3974bb2db35ae2e31e8c8f30839f8380254eae728eb6bbad3a0","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the audit search client builds its query string and reads the page back.</summary>
public class AuditClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }

    private static (AuditClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new AuditClient(http), handler);
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task SearchBuildsQueryStringAndReadsPage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");

        var outcome = await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, "SlotConfirmed", null, null, null, 50),
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.Value);
        Assert.Null(outcome.Value!.NextCursor);
        Assert.Equal("/api/audit/search", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Contains("action=SlotConfirmed", handler.Request.RequestUri.Query);
        Assert.Contains("pageSize=50", handler.Request.RequestUri.Query);
    }

    [Fact]
    public async Task SearchSendsCursorForNextPage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");

        await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, null, null, null, "abc123", 50),
            CancellationToken.None);

        Assert.Contains("cursor=abc123", handler.Request!.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchOmitsAbsentCriteria()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");

        await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, null, null, null, null, 50),
            CancellationToken.None);

        var query = handler.Request!.RequestUri!.Query;
        Assert.DoesNotContain("from=", query);
        Assert.DoesNotContain("to=", query);
        Assert.DoesNotContain("actorType=", query);
        Assert.DoesNotContain("entityType=", query);
        Assert.DoesNotContain("cursor=", query);
    }

    [Fact]
    public async Task SearchSendsBothTimestampBoundsWhenGiven()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

        await client.SearchAsync(
            new AuditSearchFilterDto(from, to, "Staff", null, "abc", "ConfirmedSlot", null, 25),
            CancellationToken.None);

        var query = Uri.UnescapeDataString(handler.Request!.RequestUri!.Query);
        Assert.Contains(from.ToString("O"), query);
        Assert.Contains(to.ToString("O"), query);
        Assert.Contains("actorType=Staff", query);
        Assert.Contains("identifier=abc", query);
        Assert.Contains("entityType=ConfirmedSlot", query);
    }

    [Fact]
    public async Task SearchReadsRowsFromThePage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse(
            """
            {"rows":[{"timestamp":"2026-09-03T12:00:00+00:00","entityType":"ConfirmedSlot",
            "entityId":"11111111-1111-1111-1111-111111111111","action":"SlotConfirmed",
            "actorType":"Staff","actorId":"staff-1","details":"6 headcount"}],"nextCursor":"next"}
            """);

        var outcome = await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, null, null, null, null, 50),
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var row = Assert.Single(outcome.Value!.Rows);
        Assert.Equal("SlotConfirmed", row.Action);
        Assert.Equal("6 headcount", row.Details);
        Assert.Equal("next", outcome.Value.NextCursor);
    }
}
`````

## tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs","encoding":"utf8","sha256":"cbc6d581b39744a69e08727700bfc7bc6f96caf46e8ae194f0df2d293bc7ba12","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Services;
using EventBooking.Web.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>
/// The Task 70 audit panel is loaded lazily on first expand so a table of many slots does not fire
/// one request per row. This proves the lazy load actually happens, and only once.
/// </summary>
public class AuditHistoryComponentTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }

    /// <summary>
    /// Verifies the audit panel issues one lazy request and renders the returned audit row.
    /// </summary>
    [Fact]
    public void TheHistoryIsFetchedOnFirstExpandOnlyAndShowsItsRows()
    {
        var rows = new List<AuditRowDto>
        {
            new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero),
                "ConfirmedSlot", Guid.NewGuid(), "SlotConfirmed", "Staff", "staff-1", "6 headcount"),
        };
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(rows, options: CamelCase),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        var cut = Render<AuditHistory>(parameters => parameters
            .Add(p => p.SlotId, Guid.NewGuid()));

        Assert.Equal(0, handler.RequestCount);

        cut.Find("details").TriggerEvent("ontoggle", new EventArgs());
        cut.WaitForAssertion(() => Assert.Contains("6 headcount", cut.Markup));
        Assert.Equal(1, handler.RequestCount);

        cut.Find("details").TriggerEvent("ontoggle", new EventArgs());
        Assert.Equal(1, handler.RequestCount);
    }
}
`````

## tests/EventBooking.Web.Tests/AuditPageTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/AuditPageTests.cs","encoding":"utf8","sha256":"0a7db81b03cd7bca946ffc03ad1445ec3de7b26444b0f454a806fc20392edb71","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the cross-cutting audit page filters, paginates, and respects the caller's bucket.</summary>
public class AuditPageTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class RouteHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private static AuditRowDto Row(string action, Guid? id = null) => new(
        new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero),
        "ConfirmedSlot",
        id ?? Guid.NewGuid(),
        action,
        "Staff",
        "staff-1",
        $"{action} details");

    private RouteHandler Given(IReadOnlyList<string> roles, Queue<AuditSearchPageDto> pages)
    {
        var handler = new RouteHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/me"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new MeDto(roles, null, null), options: CamelCase),
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        pages.Count > 0 ? pages.Dequeue() : new AuditSearchPageDto([], null),
                        options: CamelCase),
                });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new MeClient(http));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));
        return handler;
    }

    [Fact]
    public void EntityTypeDropdownAbsentForAdminOnlyCaller()
    {
        Given(["Admin"], new Queue<AuditSearchPageDto>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#audit-entity-type")));
    }

    [Fact]
    public void EntityTypeDropdownPresentForCoordinator()
    {
        Given(["Coordinator"], new Queue<AuditSearchPageDto>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-entity-type")));
    }

    [Fact]
    public void TheNewestPageIsLoadedOnArrival()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("SlotConfirmed")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Contains("SlotConfirmed details", cut.Markup));
    }

    [Fact]
    public void LoadMoreAppendsRatherThanReplaces()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("SlotConfirmed")], "cursor-1"));
        pages.Enqueue(new AuditSearchPageDto([Row("SlotCancelled")], null));
        var handler = Given(["Coordinator"], pages);

        var cut = Render<Audit>();
        cut.WaitForAssertion(() => Assert.Contains("SlotConfirmed details", cut.Markup));

        cut.Find("#audit-load-more").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("SlotConfirmed details", cut.Markup);
            Assert.Contains("SlotCancelled details", cut.Markup);
        });
        Assert.Contains(handler.Requests, uri => uri.Query.Contains("cursor=cursor-1"));
    }

    [Fact]
    public void LoadMoreIsHiddenWhenThePageIsExhausted()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("SlotConfirmed")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Contains("SlotConfirmed details", cut.Markup));
        Assert.Empty(cut.FindAll("#audit-load-more"));
    }

    [Fact]
    public void SearchingAgainReplacesThePreviousResults()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("SlotConfirmed")], null));
        pages.Enqueue(new AuditSearchPageDto([Row("SlotCancelled")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();
        cut.WaitForAssertion(() => Assert.Contains("SlotConfirmed details", cut.Markup));

        cut.Find("#audit-search").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("SlotCancelled details", cut.Markup);
            Assert.DoesNotContain("SlotConfirmed details", cut.Markup);
        });
    }

    [Fact]
    public void AFailedSearchShowsTheSafeErrorMessage()
    {
        var handler = new RouteHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/me"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new MeDto(["Coordinator"], null, null), options: CamelCase),
                }
                : new HttpResponseMessage(HttpStatusCode.Forbidden));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new MeClient(http));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role=alert]")));
    }
    [Fact]
    public void ActionButtonsUseTheSharedButtonStyles()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("SlotConfirmed")], "cursor-1"));
        Given(["Admin"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-load-more")));
        Assert.Equal(["button", "button-primary"], cut.Find("#audit-search").ClassList);
        Assert.Equal(["button"], cut.Find("#audit-load-more").ClassList);
    }

    [Fact]
    public void EachFilterCaptionIsSeparateFromItsControl()
    {
        Given(["Coordinator"], new Queue<AuditSearchPageDto>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-entity-type")));
        var fields = cut.FindAll(".audit-filters .field");
        Assert.Equal(6, fields.Count);
        Assert.All(fields, field =>
        {
            var label = field.QuerySelector("label.field-label");
            Assert.NotNull(label);
            var control = field.QuerySelector("input, select");
            Assert.NotNull(control);
            Assert.Equal(control!.Id, label!.GetAttribute("for"));
        });
    }
}
`````

## tests/EventBooking.Web.Tests/BookingClientTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/BookingClientTests.cs","encoding":"utf8","sha256":"d69a1a3ecb2225f6982860748d551126dc2caea27657474619c9bb4828568089","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class BookingClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public Queue<HttpResponseMessage> Responses { get; } = [];

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.NoContent);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return Responses.Count > 0 ? Responses.Dequeue() : Response;
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

    private static (BookingClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new BookingClient(http), handler);
    }

    [Fact]
    public async Task TheInviteIsFetchedByTokenAndTheTokenIsEscaped()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new InviteDto(Guid.NewGuid(), "Amara Novak", ["Uniform Fitting"], [])),
        };

        await client.GetInviteAsync("abc.def/ghi", CancellationToken.None);

        Assert.Equal("/api/booking/abc.def%2Fghi", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task AnExpiredLinkComesBackAsTheGenericMessage()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                """{"title":"not_found","detail":"This booking link is no longer valid.","status":404}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.GetInviteAsync("anything", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(404, outcome.StatusCode);
        Assert.Equal("This booking link is no longer valid.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task ConfirmingPostsTheChosenSlot()
    {
        var (client, handler) = Given();
        var slotId = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ConfirmedBookingDto(
                Guid.NewGuid(), new DateOnly(2026, 9, 11), new TimeOnly(13, 0), new TimeOnly(17, 0), "manage-token")),
        };

        var outcome = await client.ConfirmAsync("tok", slotId, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/booking/tok/confirm", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains(slotId.ToString(), handler.Bodies[0]);
        Assert.Equal("manage-token", outcome.Value!.ManageToken);
    }

    [Fact]
    public async Task AnOptionThatFilledUpComesBackAsAConflictWithItsMessage()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"conflict","detail":"That time filled up while you were choosing. Please pick from the updated options.","status":409}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.ConfirmAsync("tok", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(409, outcome.StatusCode);
        Assert.Contains("filled up", outcome.ErrorMessage);
    }

    [Fact]
    public async Task TheBookingIsFetchedFromTheManageRoute()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new BookingDto(
                new DateOnly(2026, 9, 11), new TimeOnly(13, 0), new TimeOnly(17, 0),
                "Friday 11 Sep 2026, 13:00-17:00", "Amara Novak")),
        };

        var outcome = await client.GetBookingAsync("mtok", CancellationToken.None);

        Assert.Equal("/api/booking/manage/mtok", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("Amara Novak", outcome.Value!.CandidateName);
    }

    [Fact]
    public async Task CancellingSendsTheRebookFlag()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CancelOutcomeDto(true)),
        };

        var outcome = await client.CancelAsync("mtok", true, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/booking/manage/mtok/cancel", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("true", handler.Bodies[0]);
        Assert.True(outcome.Value!.Reinvited);
    }

    [Fact]
    public async Task CancellingWithoutAvailableSlotsReportsThatNoInviteWasSent()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CancelOutcomeDto(false)),
        };

        var outcome = await client.CancelAsync("mtok", true, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.Value!.Reinvited);
    }

    [Fact]
    public async Task EveryResponseIsDisposedAfterEachCandidateOperation()
    {
        var (client, handler) = Given();
        var inviteResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"inviteId":"00000000-0000-0000-0000-000000000001","candidateName":"Amara Novak","appointmentTypeNames":["Uniform Fitting"],"options":[]}"""));
        var confirmedResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"bookingId":"00000000-0000-0000-0000-000000000002","date":"2026-09-11","startTime":"13:00:00","endTime":"17:00:00","manageToken":"manage-token"}"""));
        var bookingResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"date":"2026-09-11","startTime":"13:00:00","endTime":"17:00:00","display":"Friday 11 Sep 2026","candidateName":"Amara Novak"}"""));
        var cancelResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"reinvited":false}"""));
        var responses = new[] { inviteResponse, confirmedResponse, bookingResponse, cancelResponse };

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        await client.GetInviteAsync("invite", CancellationToken.None);
        await client.ConfirmAsync("invite", Guid.NewGuid(), CancellationToken.None);
        await client.GetBookingAsync("manage", CancellationToken.None);
        await client.CancelAsync("manage", false, CancellationToken.None);

        Assert.All(responses, response => Assert.True(response.WasDisposed));
        Assert.True(inviteResponse.WasDisposedAfterContentWasRead);
        Assert.True(confirmedResponse.WasDisposedAfterContentWasRead);
        Assert.True(bookingResponse.WasDisposedAfterContentWasRead);
        Assert.True(cancelResponse.WasDisposedAfterContentWasRead);
    }
}
`````

## tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs","encoding":"utf8","sha256":"481b8a6efbb948498b93020dd690d7fcb0f268d8133afc286a9147d379a3bfba","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the book page distinguishes recovery visits with singular/plural copy.</summary>
public class BookRecoveryHeadingTests : BunitContext
{
    [Fact]
    public void InitialInviteKeepsTheChooseATimeHeading()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(), "Amara Novak", ["Medical Check-Up"], [Option()], IsRecovery: false));

        cut.WaitForAssertion(() =>
            Assert.Equal("Choose a time", cut.Find("h1").TextContent.Trim()));
    }

    [Fact]
    public void RecoveryInviteUsesTheSingularMissedAppointmentHeading()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(), "Amara Novak", ["Medical Check-Up"], [Option()], IsRecovery: true));

        cut.WaitForAssertion(() =>
            Assert.Equal(
                "Choose a new time for your missed appointment",
                cut.Find("h1").TextContent.Trim()));
    }

    [Fact]
    public void RecoveryInviteUsesThePluralHeadingForTwoTypes()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["Medical Check-Up", "Uniform Fitting"],
            [Option()],
            IsRecovery: true));

        cut.WaitForAssertion(() =>
            Assert.Equal(
                "Choose a new time for your missed appointments",
                cut.Find("h1").TextContent.Trim()));
    }

    private IRenderedComponent<Book> RenderBook(InviteDto invite)
    {
        var handler = new StubInviteHandler(invite);
        Services.AddSingleton(
            new BookingClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        return Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
    }

    private static InviteOptionDto Option() =>
        new(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00");

    private sealed class StubInviteHandler(InviteDto invite) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(invite),
            });
    }
}
`````

## tests/EventBooking.Web.Tests/CandidateBookingCancellationComponentTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/CandidateBookingCancellationComponentTests.cs","encoding":"utf8","sha256":"0b6a2e1bc631a125db5c4ae871f8e40b6aa6db6708cbc3bfd5d8d63c5dc8a4a8","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the coordinator booking cell: its summary, confirmations, and outcome copy.</summary>
public class CandidateBookingCancellationComponentTests : BunitContext
{
    private static readonly Guid CandidateId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(object? value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Encoding.UTF8,
            "application/json"),
    };

    private static CandidateBookingDto Booking(Guid id, bool isOriginal, int day) => new(
        id, isOriginal, new DateOnly(2026, 9, day), new TimeOnly(9, 0), new TimeOnly(13, 0));

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<Candidates> cut, string text) =>
        cut.FindAll("button").First(button => button.TextContent.Trim() == text);

    /// <summary>Renders the candidates page with a stubbed bookings list and cancel response.</summary>
    private (IRenderedComponent<Candidates> Cut, StubHandler Handler) RenderWith(
        Queue<IReadOnlyList<CandidateBookingDto>> bookingPages,
        Func<HttpRequestMessage, HttpResponseMessage>? cancel = null)
    {
        this.AddAuthorization().SetAuthorized("Coordinator");
        StubHandler? handler = null;
        handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post
                && path.StartsWith($"/api/candidates/{CandidateId}/bookings/", StringComparison.Ordinal))
            {
                return cancel is not null
                    ? cancel(request)
                    : Json(new CancelCandidateBookingDto(false, false, "Unavailable", null));
            }

            if (path == $"/api/candidates/{CandidateId}/bookings")
            {
                return Json(bookingPages.Count > 0
                    ? bookingPages.Dequeue()
                    : Array.Empty<CandidateBookingDto>());
            }

            if (path == "/api/candidates")
            {
                return Json(new[]
                {
                    new CandidateDto(
                        CandidateId, "Amara Novak", "a.novak@mail.com", null, null, null, false,
                        [], 4, "Booked"),
                });
            }

            if (path == "/api/employee-groups")
            {
                return Json(Array.Empty<object>());
            }

            return Json(new DashboardsDto([], [], [], []));
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        Services.AddSingleton(new CandidatesClient(http));
        Services.AddSingleton(new DashboardsClient(http));
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        return (Render<Candidates>(), handler);
    }

    private static Queue<IReadOnlyList<CandidateBookingDto>> Pages(
        params IReadOnlyList<CandidateBookingDto>[] pages) => new(pages);

    [Fact]
    public void BookingCellReportsNoActiveBookings()
    {
        var (cut, _) = RenderWith(Pages([]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Contains("No active bookings", cut.Markup));
        Assert.Empty(cut.FindAll("ul.booking-list li"));
    }

    [Fact]
    public void BookingCellSummarizesOneActiveBooking()
    {
        var (cut, _) = RenderWith(Pages([Booking(Guid.NewGuid(), true, 10)]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));
        Assert.Contains("1 active booking", cut.Markup);
        Assert.Contains("10 Sep 2026", cut.Markup);
    }

    [Fact]
    public void BookingCellSummarizesTwoActiveBookings()
    {
        var (cut, _) = RenderWith(Pages(
        [
            Booking(Guid.NewGuid(), true, 10),
            Booking(Guid.NewGuid(), false, 12),
        ]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("ul.booking-list li").Count));
        Assert.Contains("2 active bookings", cut.Markup);
    }

    [Fact]
    public void RecoveryRowOmitsCancelAndRebook()
    {
        var (cut, _) = RenderWith(Pages(
        [
            Booking(Guid.NewGuid(), true, 10),
            Booking(Guid.NewGuid(), false, 12),
        ]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("ul.booking-list li").Count));
        var rows = cut.FindAll("ul.booking-list li");
        Assert.Contains(rows[0].QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel & rebook");
        Assert.DoesNotContain(rows[1].QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel & rebook");
        Assert.Contains(rows[1].QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel booking");
        Assert.Contains("(recovery)", rows[1].TextContent);
    }

    [Fact]
    public void CancelBookingRequiresAConfirmingSecondClick()
    {
        var bookingId = Guid.NewGuid();
        var (cut, handler) = RenderWith(Pages([Booking(bookingId, true, 10)], []));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel booking").Click();

        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);

        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() => Assert.Contains("Booking cancelled.", cut.Markup));
        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal(
            $"/api/candidates/{CandidateId}/bookings/{bookingId}/cancel",
            post.RequestUri!.AbsolutePath);
    }

    [Fact]
    public void CancelAndRebookRequiresAConfirmingSecondClick()
    {
        var bookingId = Guid.NewGuid();
        var (cut, handler) = RenderWith(
            Pages([Booking(bookingId, true, 10)], []),
            cancel: _ => Json(new CancelCandidateBookingDto(true, true, "Sent", Guid.NewGuid())));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel & rebook").Click();

        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
        // Arming rebook must not arm the plain cancel on the same row.
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Cancel booking");

        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Booking cancelled; replacement invite sent.", cut.Markup));
    }

    [Fact]
    public void SuccessMessageReportsAnUndeliveredReplacementInvite()
    {
        var bookingId = Guid.NewGuid();
        var (cut, _) = RenderWith(
            Pages([Booking(bookingId, true, 10)], []),
            cancel: _ => Json(new CancelCandidateBookingDto(true, true, "Failed", Guid.NewGuid())));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel & rebook").Click();
        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("replacement invite could not be delivered", cut.Markup));
    }

    [Fact]
    public void AFailedCancellationShowsTheSafeMessage()
    {
        var bookingId = Guid.NewGuid();
        var (cut, _) = RenderWith(
            Pages([Booking(bookingId, false, 12)]),
            cancel: _ => new HttpResponseMessage(HttpStatusCode.Conflict));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel booking").Click();
        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("span.booking-error[role=alert]")));
    }
}
`````

## tests/EventBooking.Web.Tests/CandidateLayoutTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/CandidateLayoutTests.cs","encoding":"utf8","sha256":"9e993f431e35e983835bf1d2177455d5b63a068cb2f26ba022cef8d52698acf2","parts":1,"part":1} -->

`````csharp
using System.Reflection;
using Bunit;
using EventBooking.Web.Layout;
using EventBooking.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Tests;

/// <summary>
/// Regression coverage for the anonymous candidate shell, which must not expose staff sign-in controls.
/// </summary>
public class CandidateLayoutTests : BunitContext
{
    /// <summary>
    /// Verifies that both candidate routes compile with the dedicated anonymous layout.
    /// </summary>
    [Fact]
    public void CandidatePagesCompileWithTheCandidateLayout()
    {
        Assert.Equal(typeof(CandidateLayout), GetLayoutType(typeof(Book)));
        Assert.Equal(typeof(CandidateLayout), GetLayoutType(typeof(ManageBooking)));
    }

    /// <summary>
    /// Verifies that the candidate shell retains branding and its body without staff authentication controls.
    /// </summary>
    [Fact]
    public void AnonymousCandidateLayoutShowsBrandAndBodyWithoutAuthenticationControls()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderLayout();

        Assert.Equal("EventBooking", cut.Find("header.topbar a.brand").TextContent.Trim());
        Assert.Equal("/", cut.Find("header.topbar a.brand").GetAttribute("href"));
        Assert.Contains("Candidate content", cut.Markup);
        Assert.DoesNotContain("authentication/login", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authentication/logout", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign in", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign out", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static Type? GetLayoutType(Type pageType) =>
        pageType.GetCustomAttribute<LayoutAttribute>()?.LayoutType;

    private IRenderedComponent<CascadingAuthenticationState> RenderLayout()
    {
        RenderFragment layout = builder =>
        {
            builder.OpenComponent<CandidateLayout>(0);
            builder.AddAttribute(1, "Body", (RenderFragment)(body => body.AddContent(0, "Candidate content")));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(layout));
    }
}
`````

## tests/EventBooking.Web.Tests/CandidatePresentationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/CandidatePresentationTests.cs","encoding":"utf8","sha256":"7058ea58827b469df32ed5cfcfdc9f93c914266d3c926ee9f0f095bfadbf419f","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Domain.Candidates;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class CandidatePresentationTests : BunitContext
{
    [Theory]
    [InlineData(CandidateStatus.NotYetInvited, "status-new")]
    [InlineData(CandidateStatus.AwaitingAvailability, "status-warning")]
    [InlineData(CandidateStatus.Invited, "status-neutral")]
    [InlineData(CandidateStatus.Booked, "status-success")]
    [InlineData(CandidateStatus.NoResponseNeedsFollowUp, "status-warning")]
    public void EachCanonicalCandidateStatusGetsItsWireframeStyle(
        CandidateStatus status,
        string expectedCssClass)
    {
        Assert.Equal(expectedCssClass, CandidatePresentation.StatusCssClass((int)status));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void UnknownRawStatusValuesAreNeutral(int rawStatus)
    {
        Assert.Equal("status-neutral", CandidatePresentation.StatusCssClass(rawStatus));
    }

    [Fact]
    public async Task ABusyPageIgnoresANewFileEventBeforeReadingItsFile()
    {
        var page = new Candidates { IsBusyForTesting = true };
        var noFileEvent = new InputFileChangeEventArgs([]);

        await page.OnFileChosenAsync(noFileEvent);

        Assert.True(page.IsBusyForTesting);
        Assert.Null(page.ErrorForTesting);
    }

    [Theory]
    [InlineData("Ready", "status-success")]
    [InlineData("EmployeeGroupUnassigned", "status-warning")]
    [InlineData("NoActiveBooking", "status-neutral")]
    [InlineData("RequirementSnapshotMismatch", "status-warning")]
    [InlineData("AppointmentsOutstanding", "status-warning")]
    [InlineData("UnknownFutureCode", "status-neutral")]
    public void EachReadinessCodeGetsItsBadgeStyle(string code, string expectedCssClass)
    {
        Assert.Equal(expectedCssClass, CandidatePresentation.ReadinessCssClass(code));
    }

    [Theory]
    [InlineData("Ready", "✓")]
    [InlineData("EmployeeGroupUnassigned", "!")]
    [InlineData("NoActiveBooking", "○")]
    [InlineData("RequirementSnapshotMismatch", "≠")]
    [InlineData("AppointmentsOutstanding", "•")]
    [InlineData("UnknownFutureCode", "?")]
    public void EachReadinessCodeGetsItsBadgeGlyph(string code, string expectedIcon)
    {
        Assert.Equal(expectedIcon, CandidatePresentation.ReadinessIcon(code));
    }

    [Theory]
    [InlineData("Ready", "status-success", "✓", "Ready to book")]
    [InlineData("EmployeeGroupUnassigned", "status-warning", "!", "Needs employee group")]
    [InlineData("NoActiveBooking", "status-neutral", "○", "No active booking")]
    [InlineData("RequirementSnapshotMismatch", "status-warning", "≠", "Requirements changed")]
    [InlineData("AppointmentsOutstanding", "status-warning", "•", "Appointments outstanding")]
    public void EachReadinessCodeRendersBadgeTextAndStyle(
        string code, string cssClass, string icon, string display)
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(candidateId, _ => Task.FromResult(ReadinessJson(
            new CandidateReadinessDto(candidateId, code, display, []))));

        cut.Find("button.readiness-badge").Click();

        cut.WaitForAssertion(() =>
        {
            var badge = cut.Find("button.readiness-badge");
            Assert.Contains(cssClass, badge.ClassList);
            Assert.Contains(icon, badge.TextContent);
            Assert.Contains(display, badge.TextContent);
            Assert.Equal("true", badge.GetAttribute("aria-expanded"));
        });
        Assert.Contains(display, cut.Find("div.readiness-detail").TextContent);
    }

    [Fact]
    public void OutstandingTypesRenderAsAListWithRecoverability()
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(candidateId, _ => Task.FromResult(ReadinessJson(
            new CandidateReadinessDto(
                candidateId,
                "AppointmentsOutstanding",
                "Appointments outstanding",
                [
                    new OutstandingAppointmentTypeDto("DAT", "Drug & Alcohol Testing", false),
                    new OutstandingAppointmentTypeDto("MED", "Medical Check-Up", true),
                ]))));

        cut.Find("button.readiness-badge").Click();

        cut.WaitForAssertion(() =>
        {
            var items = cut.FindAll("ul.readiness-types li");
            Assert.Equal(2, items.Count);
            Assert.Contains("Drug & Alcohol Testing", items[0].TextContent);
            Assert.Contains("Medical Check-Up (recoverable)", items[1].TextContent);
        });
    }

    [Fact]
    public void ReadinessLoadingAnnouncesPolitely()
    {
        var candidateId = Guid.NewGuid();
        var gate = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = RenderCandidates(candidateId, _ => gate.Task);

        cut.Find("button.readiness-badge").Click();

        cut.WaitForAssertion(() =>
        {
            var loading = cut.Find("span.readiness-loading");
            Assert.Equal("polite", loading.GetAttribute("aria-live"));
            Assert.Contains("Loading readiness", loading.TextContent);
        });

        gate.SetResult(ReadinessJson(
            new CandidateReadinessDto(candidateId, "Ready", "Ready to book", [])));
        cut.WaitForAssertion(() =>
            Assert.Contains("Ready to book", cut.Find("button.readiness-badge").TextContent));
    }

    [Fact]
    public void ReadinessFailureIsRetryable()
    {
        var candidateId = Guid.NewGuid();
        var attempts = 0;
        var cut = RenderCandidates(candidateId, _ =>
        {
            attempts++;
            return Task.FromResult(attempts == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : ReadinessJson(
                    new CandidateReadinessDto(candidateId, "Ready", "Ready to book", [])));
        });

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("span.readiness-error[role=alert]")));
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Try again").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Ready to book", cut.Find("button.readiness-badge").TextContent));
    }

    [Fact]
    public void ReadinessBadgeIsAKeyboardOperableButton()
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(candidateId, _ => Task.FromResult(ReadinessJson(
            new CandidateReadinessDto(candidateId, "Ready", "Ready to book", []))));

        var badge = cut.Find("button.readiness-badge");

        Assert.Equal("BUTTON", badge.TagName);
        Assert.Null(badge.GetAttribute("tabindex"));
        Assert.Equal("false", badge.GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task ABusyPageIgnoresReadinessToggle()
    {
        var page = new Candidates { IsBusyForTesting = true };
        var candidateId = Guid.NewGuid();

        await page.ToggleReadinessForTestingAsync(candidateId);

        Assert.True(page.IsBusyForTesting);
        Assert.False(page.IsReadinessExpandedForTesting(candidateId));
        Assert.Null(page.ReadinessForTesting(candidateId));
        Assert.Null(page.ReadinessErrorForTesting(candidateId));
    }

    private IRenderedComponent<Candidates> RenderCandidates(
        Guid candidateId,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        this.AddAuthorization().SetAuthorized("Coordinator");
        var stub = new StubHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/readiness", StringComparison.Ordinal))
            {
                return await respond(request);
            }

            if (path == "/api/candidates")
            {
                return Json(new[]
                {
                    new CandidateDto(
                        candidateId, "Amara Novak", "a.novak@mail.com", null, null, null, false,
                        [], 1, "Not yet invited"),
                });
            }

            if (path == "/api/employee-groups")
            {
                return Json(Array.Empty<object>());
            }

            return Json(new DashboardsDto([], [], [], []));
        });
        Services.AddSingleton(
            new CandidatesClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(
            new DashboardsClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(
            new AuditClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        return Render<Candidates>();
    }

    private static HttpResponseMessage ReadinessJson(CandidateReadinessDto dto) => Json(dto);

    private static HttpResponseMessage Json(object? value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                Encoding.UTF8,
                "application/json"),
        };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            respond(request);
    }
}
`````
