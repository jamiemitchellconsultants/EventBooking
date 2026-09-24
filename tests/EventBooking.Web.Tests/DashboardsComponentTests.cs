using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>
/// Component-level regression coverage for the Dashboards page, following up on review findings
/// against Task 69: an invite's success getting blurred by a subsequent refresh failure, and the
/// tablist's missing keyboard behaviour.
/// </summary>
public class DashboardsComponentTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            _responses.Enqueue(respond);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No stubbed response queued for {request.Method} {request.RequestUri}.");
            }

            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private RoutedHandler GivenClients()
    {
        var handler = new RoutedHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton<IDashboardsClient>(new DashboardsClient(http));
        Services.AddSingleton<IAttendeesClient>(new AttendeesClient(http));
        Services.AddSingleton<IAuditClient>(new AuditClient(http));
        return handler;
    }

    private static HttpResponseMessage DashboardJson(DashboardsDto dto) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(dto, options: CamelCase),
    };

    private static HttpResponseMessage InviteJson() => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(
            new InviteOutcomeDto(Guid.NewGuid(), "Invited"), options: CamelCase),
    };

    private static HttpResponseMessage ServerError() => new(HttpStatusCode.InternalServerError)
    {
        Content = JsonContent.Create(new { title = "boom" }, options: CamelCase),
    };

    private static DashboardsDto DashboardWithOneStuckAttendee(Guid attendeeId) => new(
        new DashboardCountedTab<AwaitingAvailabilityDto>(0, []),
        new DashboardCountedTab<NoResponseDto>(1,
        [
            new NoResponseDto(
                attendeeId, "D. Stuck", "d.stuck@mail.com", ["DAT"], DateOnly.FromDateTime(DateTime.UtcNow)),
        ]),
        new DashboardCountedTab<EventOverviewDto>(0, []),
        0,
        0,
        new Dictionary<string, ApiLink>());

    [Fact]
    public async Task ASuccessfulReinviteStaysVisibleEvenWhenTheFollowingRefreshFails()
    {
        var attendeeId = Guid.NewGuid();
        var handler = GivenClients();
        handler.Enqueue(_ => DashboardJson(DashboardWithOneStuckAttendee(attendeeId)));
        handler.Enqueue(_ => InviteJson());
        handler.Enqueue(_ => ServerError());

        var cut = Render<Dashboards>();
        cut.WaitForAssertion(() => Assert.Contains("No response", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("#no-response-tab").Click());
        cut.WaitForAssertion(() => Assert.Contains("Re-invite now", cut.Markup));

        var reinviteButton = cut.Find("button.button-primary");
        await cut.InvokeAsync(() => reinviteButton.Click());

        cut.WaitForAssertion(() =>
        {
            var banner = cut.Find("[role=alert]").TextContent;
            Assert.Contains("Invite sent", banner);
        });
    }

    private static DashboardsDto EmptyDashboard() => new(
        new DashboardCountedTab<AwaitingAvailabilityDto>(0, []),
        new DashboardCountedTab<NoResponseDto>(0, []),
        new DashboardCountedTab<EventOverviewDto>(0, []),
        0,
        0,
        new Dictionary<string, ApiLink>());

    [Fact]
    public void ArrowKeysMoveTheSelectedTabAndWrapAtTheEnds()
    {
        var handler = GivenClients();
        handler.Enqueue(_ => DashboardJson(EmptyDashboard()));

        var cut = Render<Dashboards>();
        cut.WaitForAssertion(() => Assert.Contains("role=\"tablist\"", cut.Markup));

        Assert.Equal("true", cut.Find("#awaiting-tab").GetAttribute("aria-selected"));
        Assert.Equal("false", cut.Find("#no-response-tab").GetAttribute("aria-selected"));
        Assert.Equal("0", cut.Find("#awaiting-tab").GetAttribute("tabindex"));
        Assert.Equal("-1", cut.Find("#no-response-tab").GetAttribute("tabindex"));

        cut.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("#no-response-tab").GetAttribute("aria-selected")));
        Assert.Equal("false", cut.Find("#awaiting-tab").GetAttribute("aria-selected"));

        cut.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("#awaiting-tab").GetAttribute("aria-selected")));

        // Wraps past the first tab to the last one.
        cut.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("#events-tab").GetAttribute("aria-selected")));

        cut.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "Home" });
        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("#awaiting-tab").GetAttribute("aria-selected")));

        cut.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "End" });
        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("#events-tab").GetAttribute("aria-selected")));
    }

    private static DashboardsDto DashboardWithOneAwaitingAttendee(Guid attendeeId) => new(
        new DashboardCountedTab<AwaitingAvailabilityDto>(1,
        [
            new AwaitingAvailabilityDto(
                attendeeId, "A. Waiting", "a.waiting@mail.com", ["DAT"],
                DateOnly.FromDateTime(DateTime.UtcNow), 3),
        ]),
        new DashboardCountedTab<NoResponseDto>(0, []),
        new DashboardCountedTab<EventOverviewDto>(0, []),
        0,
        0,
        new Dictionary<string, ApiLink>());

    /// <summary>Verifies the awaiting-availability tab offers each attendee's history.</summary>
    [Fact]
    public void AwaitingTabRendersHistoryPanelPerRow()
    {
        var handler = GivenClients();
        handler.Enqueue(_ => DashboardJson(DashboardWithOneAwaitingAttendee(Guid.NewGuid())));

        var cut = Render<Dashboards>();

        cut.WaitForAssertion(() => Assert.Contains("A. Waiting", cut.Markup));
        Assert.Single(cut.FindAll("section.audit-history"));
    }

    /// <summary>Verifies the no-response tab offers each attendee's history.</summary>
    [Fact]
    public async Task NoResponseTabRendersHistoryPanelPerRow()
    {
        var handler = GivenClients();
        handler.Enqueue(_ => DashboardJson(DashboardWithOneStuckAttendee(Guid.NewGuid())));

        var cut = Render<Dashboards>();
        cut.WaitForAssertion(() => Assert.Contains("No response", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("#no-response-tab").Click());

        cut.WaitForAssertion(() => Assert.Contains("D. Stuck", cut.Markup));
        Assert.Single(cut.FindAll("section.audit-history"));
    }
}
