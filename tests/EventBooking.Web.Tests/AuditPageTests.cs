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
        Guid.NewGuid(),
        new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero),
        "Event",
        id ?? Guid.NewGuid(),
        action,
        "Staff",
        "staff-1",
        null,
        $"{action} details",
        new Dictionary<string, ApiLink>());

    private RouteHandler Given(IReadOnlyList<string> roles, Queue<PageDto<AuditRowDto>> pages)
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
                        pages.Count > 0 ? pages.Dequeue() : new PageDto<AuditRowDto>([], null),
                        options: CamelCase),
                });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton<IAuditClient>(new AuditClient(http));
        Services.AddSingleton<IMeClient>(new MeClient(http));
        return handler;
    }

    [Fact]
    public void EntityTypeDropdownAbsentForAdminOnlyCaller()
    {
        Given(["Admin"], new Queue<PageDto<AuditRowDto>>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#audit-entity-type")));
    }

    [Fact]
    public void EntityTypeDropdownPresentForCoordinator()
    {
        Given(["Coordinator"], new Queue<PageDto<AuditRowDto>>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-entity-type")));
    }

    [Fact]
    public void TheNewestPageIsLoadedOnArrival()
    {
        var pages = new Queue<PageDto<AuditRowDto>>();
        pages.Enqueue(new PageDto<AuditRowDto>([Row("EventConfirmed")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Contains("EventConfirmed details", cut.Markup));
    }

    [Fact]
    public void LoadMoreAppendsRatherThanReplaces()
    {
        var pages = new Queue<PageDto<AuditRowDto>>();
        pages.Enqueue(new PageDto<AuditRowDto>([Row("EventConfirmed")], "cursor-1"));
        pages.Enqueue(new PageDto<AuditRowDto>([Row("EventCancelled")], null));
        var handler = Given(["Coordinator"], pages);

        var cut = Render<Audit>();
        cut.WaitForAssertion(() => Assert.Contains("EventConfirmed details", cut.Markup));

        cut.Find("#audit-load-more").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("EventConfirmed details", cut.Markup);
            Assert.Contains("EventCancelled details", cut.Markup);
        });
        Assert.Contains(handler.Requests, uri => uri.Query.Contains("cursor=cursor-1"));
    }

    [Fact]
    public void LoadMoreIsHiddenWhenThePageIsExhausted()
    {
        var pages = new Queue<PageDto<AuditRowDto>>();
        pages.Enqueue(new PageDto<AuditRowDto>([Row("EventConfirmed")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Contains("EventConfirmed details", cut.Markup));
        Assert.Empty(cut.FindAll("#audit-load-more"));
    }

    [Fact]
    public void SearchingAgainReplacesThePreviousResults()
    {
        var pages = new Queue<PageDto<AuditRowDto>>();
        pages.Enqueue(new PageDto<AuditRowDto>([Row("EventConfirmed")], null));
        pages.Enqueue(new PageDto<AuditRowDto>([Row("EventCancelled")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();
        cut.WaitForAssertion(() => Assert.Contains("EventConfirmed details", cut.Markup));

        cut.Find("#audit-search").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("EventCancelled details", cut.Markup);
            Assert.DoesNotContain("EventConfirmed details", cut.Markup);
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
        Services.AddSingleton<IAuditClient>(new AuditClient(http));
        Services.AddSingleton<IMeClient>(new MeClient(http));

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role=alert]")));
    }
    [Fact]
    public void ActionButtonsUseTheSharedButtonStyles()
    {
        var pages = new Queue<PageDto<AuditRowDto>>();
        pages.Enqueue(new PageDto<AuditRowDto>([Row("EventConfirmed")], "cursor-1"));
        Given(["Admin"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-load-more")));
        Assert.Equal(["button", "button-primary"], cut.Find("#audit-search").ClassList);
        Assert.Equal(["button"], cut.Find("#audit-load-more").ClassList);
    }

    [Fact]
    public void EachFilterCaptionIsSeparateFromItsControl()
    {
        Given(["Coordinator"], new Queue<PageDto<AuditRowDto>>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-entity-type")));
        var fields = cut.FindAll(".audit-filters .field");
        Assert.Equal(7, fields.Count);
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
