# 00b — Vocabulary edits 113 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Web.Tests/DashboardsComponentTests.cs — 1/1

<!-- vocabulary-file: {"id":384,"oldPath":"tests/EventBooking.Web.Tests/DashboardsComponentTests.cs","newPath":"tests/EventBooking.Web.Tests/DashboardsComponentTests.cs","beforeSha":"6df5bd269d9ba84ccefa48a17c516b5fddb468f32cfaf9b8150b64b3fe7ed0cb","afterSha":"cd04ab81b22148f49efb4cfd765252bc63cab217117a8cd41cfd27216905e422","side":"before","part":1,"parts":1} -->

`````csharp
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
        Services.AddSingleton(new DashboardsClient(http));
        Services.AddSingleton(new CandidatesClient(http));
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));
        return handler;
    }

    private static HttpResponseMessage DashboardJson(DashboardsDto dto) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(dto, options: CamelCase),
    };

    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    private static HttpResponseMessage ServerError() => new(HttpStatusCode.InternalServerError)
    {
        Content = JsonContent.Create(new { title = "boom" }, options: CamelCase),
    };

    private static DashboardsDto DashboardWithOneStuckCandidate(Guid candidateId) => new(
        AwaitingAvailability: [],
        NoResponse:
        [
            new NoResponseRowDto(
                candidateId, "D. Stuck", "d.stuck@mail.com", ["DAT"], DateOnly.FromDateTime(DateTime.UtcNow)),
        ],
        Slots: [],
        EmailStatuses: []);

    [Fact]
    public async Task ASuccessfulReinviteStaysVisibleEvenWhenTheFollowingRefreshFails()
    {
        var candidateId = Guid.NewGuid();
        var handler = GivenClients();
        handler.Enqueue(_ => DashboardJson(DashboardWithOneStuckCandidate(candidateId)));
        handler.Enqueue(_ => NoContent());
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

    private static DashboardsDto EmptyDashboard() =>
        new(AwaitingAvailability: [], NoResponse: [], Slots: [], EmailStatuses: []);

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
            Assert.Equal("true", cut.Find("#slots-tab").GetAttribute("aria-selected")));

        cut.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "Home" });
        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("#awaiting-tab").GetAttribute("aria-selected")));

        cut.Find("[role=tablist]").KeyDown(new KeyboardEventArgs { Key = "End" });
        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("#slots-tab").GetAttribute("aria-selected")));
    }

    private static DashboardsDto DashboardWithOneAwaitingCandidate(Guid candidateId) => new(
        AwaitingAvailability:
        [
            new AwaitingRowDto(
                candidateId, "A. Waiting", "a.waiting@mail.com", ["DAT"],
                DateOnly.FromDateTime(DateTime.UtcNow), 3),
        ],
        NoResponse: [],
        Slots: [],
        EmailStatuses: []);

    /// <summary>Verifies the awaiting-availability tab offers each candidate's history.</summary>
    [Fact]
    public void AwaitingTabRendersHistoryPanelPerRow()
    {
        var handler = GivenClients();
        handler.Enqueue(_ => DashboardJson(DashboardWithOneAwaitingCandidate(Guid.NewGuid())));

        var cut = Render<Dashboards>();

        cut.WaitForAssertion(() => Assert.Contains("A. Waiting", cut.Markup));
        Assert.Single(cut.FindAll("details.audit-history"));
    }

    /// <summary>Verifies the no-response tab offers each candidate's history.</summary>
    [Fact]
    public async Task NoResponseTabRendersHistoryPanelPerRow()
    {
        var handler = GivenClients();
        handler.Enqueue(_ => DashboardJson(DashboardWithOneStuckCandidate(Guid.NewGuid())));

        var cut = Render<Dashboards>();
        cut.WaitForAssertion(() => Assert.Contains("No response", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("#no-response-tab").Click());

        cut.WaitForAssertion(() => Assert.Contains("D. Stuck", cut.Markup));
        Assert.Single(cut.FindAll("details.audit-history"));
    }
}
`````

## after — tests/EventBooking.Web.Tests/DashboardsComponentTests.cs — 1/1

<!-- vocabulary-file: {"id":384,"oldPath":"tests/EventBooking.Web.Tests/DashboardsComponentTests.cs","newPath":"tests/EventBooking.Web.Tests/DashboardsComponentTests.cs","beforeSha":"6df5bd269d9ba84ccefa48a17c516b5fddb468f32cfaf9b8150b64b3fe7ed0cb","afterSha":"cd04ab81b22148f49efb4cfd765252bc63cab217117a8cd41cfd27216905e422","side":"after","part":1,"parts":1} -->

`````csharp
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
        Services.AddSingleton(new DashboardsClient(http));
        Services.AddSingleton(new AttendeesClient(http));
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));
        return handler;
    }

    private static HttpResponseMessage DashboardJson(DashboardsDto dto) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(dto, options: CamelCase),
    };

    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    private static HttpResponseMessage ServerError() => new(HttpStatusCode.InternalServerError)
    {
        Content = JsonContent.Create(new { title = "boom" }, options: CamelCase),
    };

    private static DashboardsDto DashboardWithOneStuckAttendee(Guid attendeeId) => new(
        AwaitingAvailability: [],
        NoResponse:
        [
            new NoResponseRowDto(
                attendeeId, "D. Stuck", "d.stuck@mail.com", ["DAT"], DateOnly.FromDateTime(DateTime.UtcNow)),
        ],
        Events: [],
        EmailStatuses: []);

    [Fact]
    public async Task ASuccessfulReinviteStaysVisibleEvenWhenTheFollowingRefreshFails()
    {
        var attendeeId = Guid.NewGuid();
        var handler = GivenClients();
        handler.Enqueue(_ => DashboardJson(DashboardWithOneStuckAttendee(attendeeId)));
        handler.Enqueue(_ => NoContent());
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

    private static DashboardsDto EmptyDashboard() =>
        new(AwaitingAvailability: [], NoResponse: [], Events: [], EmailStatuses: []);

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
        AwaitingAvailability:
        [
            new AwaitingRowDto(
                attendeeId, "A. Waiting", "a.waiting@mail.com", ["DAT"],
                DateOnly.FromDateTime(DateTime.UtcNow), 3),
        ],
        NoResponse: [],
        Events: [],
        EmailStatuses: []);

    /// <summary>Verifies the awaiting-availability tab offers each attendee's history.</summary>
    [Fact]
    public void AwaitingTabRendersHistoryPanelPerRow()
    {
        var handler = GivenClients();
        handler.Enqueue(_ => DashboardJson(DashboardWithOneAwaitingAttendee(Guid.NewGuid())));

        var cut = Render<Dashboards>();

        cut.WaitForAssertion(() => Assert.Contains("A. Waiting", cut.Markup));
        Assert.Single(cut.FindAll("details.audit-history"));
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
        Assert.Single(cut.FindAll("details.audit-history"));
    }
}
`````

## before — tests/EventBooking.Web.Tests/HelpPageTests.cs — 1/1

<!-- vocabulary-file: {"id":385,"oldPath":"tests/EventBooking.Web.Tests/HelpPageTests.cs","newPath":"tests/EventBooking.Web.Tests/HelpPageTests.cs","beforeSha":"dc46378c4e5fc16839a44a1881708a86535195309c5af3338717e715e87ffca2","afterSha":"2bb045bc78863dc6530eda298ca5ccf40861ea3043aacb05116d210dab3097f5","side":"before","part":1,"parts":1} -->

`````csharp
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the in-app Help page shows the right bundled guide for the caller.</summary>
public class HelpPageTests : BunitContext
{
    [Fact]
    public void SignedOutVisitorSeesTheCandidateGuide()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderHelp(meOutcome: null);

        Assert.Contains("Candidate guide", cut.Markup);
        Assert.Contains("personal links sent to your email", cut.Markup);
    }

    [Fact]
    public void ManagerSeesEveryGuideNotJustTheManagerGuide()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"), 200));

        Assert.Contains("Admin guide", cut.Markup);
        Assert.Contains("Manager guide", cut.Markup);
        Assert.Contains("Appointment staff guide", cut.Markup);
        Assert.Contains("Coordinator guide", cut.Markup);
        Assert.Contains("Candidate guide", cut.Markup);
    }

    [Fact]
    public void MultiGuideStaffGetAJumpLinkToEveryGuide()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"), 200));

        var toc = cut.Find("nav.guide-toc");
        var links = toc.QuerySelectorAll("a");
        Assert.Equal(
            ["/help#admin", "/help#manager", "/help#appointmentstaff", "/help#coordinator", "/help#candidate"],
            links.Select(link => link.GetAttribute("href")));
    }

    [Fact]
    public void GuidesDoNotRepeatTheNowRedundantBackToAllGuidesLink()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"), 200));

        Assert.DoesNotContain("All user guides", cut.Markup);
    }

    [Fact]
    public void CombinedRoleStaffSeeEveryGuide()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager", "Coordinator"], Guid.NewGuid(), "Uniform Fitting"), 200));

        Assert.Contains("Manager guide", cut.Markup);
        Assert.Contains("Coordinator guide", cut.Markup);
        Assert.Contains("Candidate guide", cut.Markup);
    }

    [Fact]
    public void UnassignedStaffAreToldToAskAnAdministrator()
    {
        this.AddAuthorization().SetAuthorized("New Starter");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto([], null, null), 200));

        Assert.Contains("not been assigned a role yet", cut.Markup);
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderHelp(ApiOutcome<MeDto>? meOutcome)
    {
        RenderFragment helpWithMeOutcome = builder =>
        {
            builder.OpenComponent<CascadingValue<ApiOutcome<MeDto>?>>(0);
            builder.AddAttribute(1, "Value", meOutcome);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<Help>(0);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(helpWithMeOutcome));
    }
}
`````

## after — tests/EventBooking.Web.Tests/HelpPageTests.cs — 1/1

<!-- vocabulary-file: {"id":385,"oldPath":"tests/EventBooking.Web.Tests/HelpPageTests.cs","newPath":"tests/EventBooking.Web.Tests/HelpPageTests.cs","beforeSha":"dc46378c4e5fc16839a44a1881708a86535195309c5af3338717e715e87ffca2","afterSha":"2bb045bc78863dc6530eda298ca5ccf40861ea3043aacb05116d210dab3097f5","side":"after","part":1,"parts":1} -->

`````csharp
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the in-app Help page shows the right bundled guide for the caller.</summary>
public class HelpPageTests : BunitContext
{
    [Fact]
    public void SignedOutVisitorSeesTheAttendeeGuide()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderHelp(meOutcome: null);

        Assert.Contains("Attendee guide", cut.Markup);
        Assert.Contains("personal links sent to your email", cut.Markup);
    }

    [Fact]
    public void ManagerSeesEveryGuideNotJustTheManagerGuide()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"), 200));

        Assert.Contains("Admin guide", cut.Markup);
        Assert.Contains("Manager guide", cut.Markup);
        Assert.Contains("Appointment staff guide", cut.Markup);
        Assert.Contains("Coordinator guide", cut.Markup);
        Assert.Contains("Attendee guide", cut.Markup);
    }

    [Fact]
    public void MultiGuideStaffGetAJumpLinkToEveryGuide()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"), 200));

        var toc = cut.Find("nav.guide-toc");
        var links = toc.QuerySelectorAll("a");
        Assert.Equal(
            ["/help#admin", "/help#manager", "/help#appointmentstaff", "/help#coordinator", "/help#attendee"],
            links.Select(link => link.GetAttribute("href")));
    }

    [Fact]
    public void GuidesDoNotRepeatTheNowRedundantBackToAllGuidesLink()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"), 200));

        Assert.DoesNotContain("All user guides", cut.Markup);
    }

    [Fact]
    public void CombinedRoleStaffSeeEveryGuide()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto(["Manager", "Coordinator"], Guid.NewGuid(), "Uniform Fitting"), 200));

        Assert.Contains("Manager guide", cut.Markup);
        Assert.Contains("Coordinator guide", cut.Markup);
        Assert.Contains("Attendee guide", cut.Markup);
    }

    [Fact]
    public void UnassignedStaffAreToldToAskAnAdministrator()
    {
        this.AddAuthorization().SetAuthorized("New Starter");

        var cut = RenderHelp(ApiOutcome<MeDto>.Success(new MeDto([], null, null), 200));

        Assert.Contains("not been assigned a role yet", cut.Markup);
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderHelp(ApiOutcome<MeDto>? meOutcome)
    {
        RenderFragment helpWithMeOutcome = builder =>
        {
            builder.OpenComponent<CascadingValue<ApiOutcome<MeDto>?>>(0);
            builder.AddAttribute(1, "Value", meOutcome);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<Help>(0);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(helpWithMeOutcome));
    }
}
`````

## before — tests/EventBooking.Web.Tests/HomePageTests.cs — 1/1

<!-- vocabulary-file: {"id":386,"oldPath":"tests/EventBooking.Web.Tests/HomePageTests.cs","newPath":"tests/EventBooking.Web.Tests/HomePageTests.cs","beforeSha":"e9013cd413e30244b21b4efa3fbe33be934455f526cb667a9ea262f030286ba0","afterSha":"e9d8f3207bfeb618d912954e60b7d6d514a4ed0c7e2a26cdf15469956f3ed5c4","side":"before","part":1,"parts":1} -->

`````csharp
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the landing page links out to the in-app Help guide.</summary>
public class HomePageTests : BunitContext
{
    [Fact]
    public void AssignedStaffSeeAHelpCardAmongTheirWorkspaceLinks()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");

        var cut = RenderHome(ApiOutcome<MeDto>.Success(new MeDto(["Coordinator"], null, null), 200));

        Assert.Contains("href=\"/help\"", cut.Markup);
    }

    [Fact]
    public void UnassignedStaffSeeNoHelpCard()
    {
        this.AddAuthorization().SetAuthorized("New Starter");

        var cut = RenderHome(ApiOutcome<MeDto>.Success(new MeDto([], null, null), 200));

        Assert.DoesNotContain("href=\"/help\"", cut.Markup);
    }

    [Fact]
    public void SignedOutVisitorsCanReachTheCandidateGuide()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderHome(meOutcome: null);

        var link = cut.Find("a[href='/help']");
        Assert.Equal("Read the candidate guide", link.TextContent.Trim());
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderHome(ApiOutcome<MeDto>? meOutcome)
    {
        RenderFragment homeWithMeOutcome = builder =>
        {
            builder.OpenComponent<CascadingValue<ApiOutcome<MeDto>?>>(0);
            builder.AddAttribute(1, "Value", meOutcome);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<Home>(0);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(homeWithMeOutcome));
    }
}
`````

## after — tests/EventBooking.Web.Tests/HomePageTests.cs — 1/1

<!-- vocabulary-file: {"id":386,"oldPath":"tests/EventBooking.Web.Tests/HomePageTests.cs","newPath":"tests/EventBooking.Web.Tests/HomePageTests.cs","beforeSha":"e9013cd413e30244b21b4efa3fbe33be934455f526cb667a9ea262f030286ba0","afterSha":"e9d8f3207bfeb618d912954e60b7d6d514a4ed0c7e2a26cdf15469956f3ed5c4","side":"after","part":1,"parts":1} -->

`````csharp
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the landing page links out to the in-app Help guide.</summary>
public class HomePageTests : BunitContext
{
    [Fact]
    public void AssignedStaffSeeAHelpCardAmongTheirWorkspaceLinks()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");

        var cut = RenderHome(ApiOutcome<MeDto>.Success(new MeDto(["Coordinator"], null, null), 200));

        Assert.Contains("href=\"/help\"", cut.Markup);
    }

    [Fact]
    public void UnassignedStaffSeeNoHelpCard()
    {
        this.AddAuthorization().SetAuthorized("New Starter");

        var cut = RenderHome(ApiOutcome<MeDto>.Success(new MeDto([], null, null), 200));

        Assert.DoesNotContain("href=\"/help\"", cut.Markup);
    }

    [Fact]
    public void SignedOutVisitorsCanReachTheAttendeeGuide()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderHome(meOutcome: null);

        var link = cut.Find("a[href='/help']");
        Assert.Equal("Read the attendee guide", link.TextContent.Trim());
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderHome(ApiOutcome<MeDto>? meOutcome)
    {
        RenderFragment homeWithMeOutcome = builder =>
        {
            builder.OpenComponent<CascadingValue<ApiOutcome<MeDto>?>>(0);
            builder.AddAttribute(1, "Value", meOutcome);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<Home>(0);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(homeWithMeOutcome));
    }
}
`````

## before — tests/EventBooking.Web.Tests/MainLayoutTests.cs — 1/1

<!-- vocabulary-file: {"id":387,"oldPath":"tests/EventBooking.Web.Tests/MainLayoutTests.cs","newPath":"tests/EventBooking.Web.Tests/MainLayoutTests.cs","beforeSha":"7ed5b096218b0515a86da71ee57de0a0375ad134409d1a2c0f4c739701d00d78","afterSha":"83e5263f769c290653db64100ff44e30d3a80fe8b69e1661870214310ba15f29","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Web.Layout;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class MainLayoutTests : BunitContext
{
    [Fact]
    public void AnonymousTopbarShowsSignInLink()
    {
        this.AddAuthorization().SetNotAuthorized();
        RegisterMe(new MeDto([], null, null));

        var cut = RenderTopbar();

        var link = cut.Find("header.topbar a[href='authentication/login']");
        Assert.Equal("Sign in", link.TextContent.Trim());
        Assert.DoesNotContain("staff-nav", cut.Markup);
    }

    [Fact]
    public void AuthenticatedTopbarSignsOutThroughInPageNavigation()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");
        RegisterMe(new MeDto([], null, null));

        var cut = RenderTopbar();

        cut.Find("header.topbar button").Click();

        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        Assert.EndsWith("authentication/logout", navigation.Uri);
        Assert.DoesNotContain("authentication/login", cut.Markup);
    }

    [Fact]
    public void AuthenticatedTopbarShowsOnlyTheCallersPermittedNavLinks()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");
        RegisterMe(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"));

        var cut = RenderTopbar();

        cut.WaitForAssertion(() => Assert.Contains("staff-nav", cut.Markup));
        Assert.Contains("href=\"/slots\"", cut.Markup);
        Assert.DoesNotContain("href=\"/candidates\"", cut.Markup);
    }

    [Fact]
    public void AnyAssignedRoleGetsAHelpLinkToTheirGuide()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");
        RegisterMe(new MeDto(["Coordinator"], null, null));

        var cut = RenderTopbar();

        cut.WaitForAssertion(() => Assert.Contains("href=\"/help\"", cut.Markup));
    }

    [Fact]
    public void UnassignedStaffSeeNoNavBar()
    {
        this.AddAuthorization().SetAuthorized("New Starter");
        RegisterMe(new MeDto([], null, null));

        var cut = RenderTopbar();

        Assert.DoesNotContain("staff-nav", cut.Markup);
    }

    private void RegisterMe(MeDto me)
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(me),
        });
        Services.AddSingleton(new MeClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderTopbar()
    {
        RenderFragment layout = builder =>
        {
            builder.OpenComponent<MainLayout>(0);
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(
            parameters => parameters.AddChildContent(layout));
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
`````

## after — tests/EventBooking.Web.Tests/MainLayoutTests.cs — 1/1

<!-- vocabulary-file: {"id":387,"oldPath":"tests/EventBooking.Web.Tests/MainLayoutTests.cs","newPath":"tests/EventBooking.Web.Tests/MainLayoutTests.cs","beforeSha":"7ed5b096218b0515a86da71ee57de0a0375ad134409d1a2c0f4c739701d00d78","afterSha":"83e5263f769c290653db64100ff44e30d3a80fe8b69e1661870214310ba15f29","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Web.Layout;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class MainLayoutTests : BunitContext
{
    [Fact]
    public void AnonymousTopbarShowsSignInLink()
    {
        this.AddAuthorization().SetNotAuthorized();
        RegisterMe(new MeDto([], null, null));

        var cut = RenderTopbar();

        var link = cut.Find("header.topbar a[href='authentication/login']");
        Assert.Equal("Sign in", link.TextContent.Trim());
        Assert.DoesNotContain("staff-nav", cut.Markup);
    }

    [Fact]
    public void AuthenticatedTopbarSignsOutThroughInPageNavigation()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");
        RegisterMe(new MeDto([], null, null));

        var cut = RenderTopbar();

        cut.Find("header.topbar button").Click();

        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        Assert.EndsWith("authentication/logout", navigation.Uri);
        Assert.DoesNotContain("authentication/login", cut.Markup);
    }

    [Fact]
    public void AuthenticatedTopbarShowsOnlyTheCallersPermittedNavLinks()
    {
        this.AddAuthorization().SetAuthorized("Manny Manager");
        RegisterMe(new MeDto(["Manager"], Guid.NewGuid(), "Medical Check-up"));

        var cut = RenderTopbar();

        cut.WaitForAssertion(() => Assert.Contains("staff-nav", cut.Markup));
        Assert.Contains("href=\"/events/negotiate\"", cut.Markup);
        Assert.DoesNotContain("href=\"/attendees\"", cut.Markup);
    }

    [Fact]
    public void AnyAssignedRoleGetsAHelpLinkToTheirGuide()
    {
        this.AddAuthorization().SetAuthorized("Cory Coordinator");
        RegisterMe(new MeDto(["Coordinator"], null, null));

        var cut = RenderTopbar();

        cut.WaitForAssertion(() => Assert.Contains("href=\"/help\"", cut.Markup));
    }

    [Fact]
    public void UnassignedStaffSeeNoNavBar()
    {
        this.AddAuthorization().SetAuthorized("New Starter");
        RegisterMe(new MeDto([], null, null));

        var cut = RenderTopbar();

        Assert.DoesNotContain("staff-nav", cut.Markup);
    }

    private void RegisterMe(MeDto me)
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(me),
        });
        Services.AddSingleton(new MeClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderTopbar()
    {
        RenderFragment layout = builder =>
        {
            builder.OpenComponent<MainLayout>(0);
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(
            parameters => parameters.AddChildContent(layout));
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
`````

## before — tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs — 1/1

<!-- vocabulary-file: {"id":388,"oldPath":"tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs","newPath":"tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs","beforeSha":"9ef1bd43e8838fa27303de43961b48a1fba3f273df3df65f88a2fe7052bbf4c6","afterSha":"2374abf04010a5e667b79222ed0e177eaf78996deafc1e197cad9e72b8cc4e67","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using EventBooking.Web.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>
/// Component regressions for the staff authority, local-time presentation, and recoverable busy states.
/// </summary>
public class RepairBWebComponentTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Verifies that a manager can see both its resolved role and appointment-type scope.
    /// </summary>
    [Fact]
    public void ManagerHomeShowsRoleAndAppointmentType()
    {
        var cut = RenderAuthenticatedHome(new(["Manager"], Guid.NewGuid(), "Medical Check-up"));

        Assert.Contains("Roles: Manager", cut.Markup);
        Assert.Contains("Appointment type: Medical Check-up", cut.Markup);
        Assert.Contains("href=\"/slots\"", cut.Markup);
        Assert.DoesNotContain("href=\"/candidates\"", cut.Markup);
    }

    /// <summary>
    /// Verifies that a coordinator sees its resolved role and only coordinator links.
    /// </summary>
    [Fact]
    public void CoordinatorHomeShowsRoleAndCoordinatorLinks()
    {
        var cut = RenderAuthenticatedHome(new(["Coordinator"], null, null));

        Assert.Contains("Roles: Coordinator", cut.Markup);
        Assert.DoesNotContain("Appointment type:", cut.Markup);
        Assert.Contains("href=\"/candidates\"", cut.Markup);
        Assert.DoesNotContain("href=\"/slots\"", cut.Markup);
        Assert.DoesNotContain("href=\"/settings\"", cut.Markup);
    }

    /// <summary>
    /// Verifies that an administrator sees its resolved role and only administrator links.
    /// </summary>
    [Fact]
    public void AdminHomeShowsRoleAndAdministratorLinks()
    {
        var cut = RenderAuthenticatedHome(new(["Admin"], null, null));

        Assert.Contains("Roles: Admin", cut.Markup);
        Assert.Contains("href=\"/settings\"", cut.Markup);
        Assert.DoesNotContain("href=\"/slots\"", cut.Markup);
    }

    /// <summary>
    /// Verifies that an authenticated account without a resolved role is described truthfully.
    /// </summary>
    [Fact]
    public void NoRoleHomeShowsTheDedicatedNoRoleState()
    {
        var cut = RenderAuthenticatedHome(new([], null, null));

        Assert.Contains("has not been assigned a role yet", cut.Markup);
        Assert.DoesNotContain("href=\"/slots\"", cut.Markup);
        Assert.DoesNotContain("href=\"/candidates\"", cut.Markup);
    }

    /// <summary>
    /// Verifies winter, daylight-saving, and local-date-crossing presentation of stored UTC instants.
    /// </summary>
    [Fact]
    public void HeadOfficeTimestampPresentationConvertsUtcInstantsWithoutChangingTheInstant()
    {
        var presentation = new HeadOfficeTimePresentation("Europe/London");

        Assert.Equal("2026-01-15 12:00 +00:00 (Europe/London)",
            presentation.Format(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal("2026-07-15 13:00 +01:00 (Europe/London)",
            presentation.Format(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal("2026-06-02 00:30 +01:00 (Europe/London)",
            presentation.Format(new DateTimeOffset(2026, 6, 1, 23, 30, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// Verifies audit timestamps render as configured head-office local time with their offset and zone identifier.
    /// </summary>
    [Fact]
    public void AuditHistoryRendersHeadOfficeLocalTimestamp()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new List<AuditRowDto>
        {
            new(new DateTimeOffset(2026, 6, 1, 23, 30, 0, TimeSpan.Zero), "Candidate", Guid.NewGuid(), "Updated", "Staff", "staff-1", null),
        }));
        Services.AddSingleton(new AuditClient(NewHttpClient(handler)));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        var cut = Render<AuditHistory>(parameters => parameters.Add(p => p.CandidateId, Guid.NewGuid()));
        cut.Find("details").TriggerEvent("ontoggle", new EventArgs());

        cut.WaitForAssertion(() => Assert.Contains("2026-06-02 00:30 +01:00 (Europe/London)", cut.Markup));
    }

    /// <summary>
    /// Verifies the latest delivery timestamp uses the same configured local-time presentation as audit history.
    /// </summary>
    [Fact]
    public void CandidatesRenderLatestDeliveryInHeadOfficeLocalTime()
    {
        var candidateId = Guid.NewGuid();
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new List<CandidateDto>
        {
            new(candidateId, "C. Candidate", "candidate@example.com", null, null, null, false, [new("DAT", "Drug & Alcohol Testing")], 3, "Invited"),
        }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Slots: [],
            EmailStatuses:
            [new CandidateEmailStatusDto(
                candidateId,
                "Invite",
                new DateTimeOffset(2026, 6, 1, 23, 30, 0, TimeSpan.Zero),
                "Sent",
                CanRetry: false)])));
        handler.Enqueue(_ => Json(new List<EmployeeGroupOptionDto>()));
        Services.AddSingleton(new CandidatesClient(NewHttpClient(handler)));
        Services.AddSingleton(new DashboardsClient(NewHttpClient(handler)));
        Services.AddSingleton(new AuditClient(NewHttpClient(handler)));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        var cut = Render<Candidates>();

        cut.WaitForAssertion(() => Assert.Contains("Invite 2026-06-02 00:30 +01:00 (Europe/London)", cut.Markup));
    }

    /// <summary>
    /// Verifies a slot-board transport exception becomes an alert and restores the page busy state.
    /// </summary>
    [Fact]
    public void SlotsReloadTransportExceptionRendersAnAlertAndClearsBusy()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => throw new HttpRequestException("offline"));
        Services.AddSingleton(new SlotsClient(NewHttpClient(handler)));

        var cut = Render<Slots>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find(".slot-board").GetAttribute("aria-busy"));
            Assert.Equal("Something went wrong. Please try again.", cut.Find("[role=alert]").TextContent.Trim());
        });
    }

    /// <summary>
    /// Verifies a confirmed-slot file-read exception is recoverable and does not strand the busy flag.
    /// </summary>
    [Fact]
    public async Task ConfirmedSlotImportFileReadExceptionRendersAnAlertAndClearsBusy()
    {
        var handler = new RoutedHandler();
        Services.AddSingleton(new ConfirmedSlotsClient(NewHttpClient(handler)));
        GivenNoConfirmedSlots();

        var cut = Render<ConfirmedSlots>();

        await cut.InvokeAsync(() => cut.Instance.ImportFileForTestingAsync(new InputFileChangeEventArgs([])));
        cut.Render();

        Assert.Equal("false", cut.Find(".confirmed-slots-page").GetAttribute("aria-busy"));
        Assert.Contains("Something went wrong. Please try again.", cut.Find("[role=alert]").TextContent);
        var input = cut.Find("input[type=file]");
        Assert.Null(input.GetAttribute("disabled"));
        Assert.Equal("Import confirmed slots CSV", input.GetAttribute("aria-label"));
    }

    /// <summary>
    /// Verifies a slot mutation transport failure renders the safe alert and releases the page busy state.
    /// </summary>
    [Fact]
    public async Task SlotsMutationTransportExceptionRendersAnAlertAndClearsBusy()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new SlotBoardDto([], [])));
        handler.Enqueue(_ => throw new HttpRequestException("offline"));
        Services.AddSingleton(new SlotsClient(NewHttpClient(handler)));

        var cut = Render<Slots>();
        cut.WaitForAssertion(() => Assert.Contains("Submit proposal", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button.button-primary").Click());

        Assert.Equal("false", cut.Find(".slot-board").GetAttribute("aria-busy"));
        Assert.Equal("Something went wrong. Please try again.", cut.Find("[role=alert]").TextContent.Trim());
    }

    /// <summary>
    /// Verifies a settings save transport failure renders the safe alert and releases the page busy state.
    /// </summary>
    [Fact]
    public async Task SettingsSaveTransportExceptionRendersAnAlertAndClearsBusy()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(SettingsWithManager(Guid.NewGuid())));
        handler.Enqueue(_ => throw new HttpRequestException("offline"));
        Services.AddSingleton(new AdminClient(NewHttpClient(handler)));

        var cut = Render<Settings>();
        cut.WaitForAssertion(() => Assert.Contains("Save changes", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button.button-primary").Click());

        Assert.Equal("false", cut.Find(".settings-page").GetAttribute("aria-busy"));
        Assert.Contains("Something went wrong. Please try again.", cut.Find("[role=alert]").TextContent);
    }

    /// <summary>
    /// Verifies a confirmed-slot import transport failure releases the input's busy state.
    /// </summary>
    [Fact]
    public async Task ConfirmedSlotImportTransportExceptionRendersAnAlertAndClearsBusy()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => throw new HttpRequestException("offline"));
        Services.AddSingleton(new ConfirmedSlotsClient(NewHttpClient(handler)));
        GivenNoConfirmedSlots();

        var cut = Render<ConfirmedSlots>();

        await cut.InvokeAsync(() => cut.Instance.ImportFileForTestingAsync(new InputFileChangeEventArgs(
            [new BrowserFile("slots.csv", "date,startTime,DAT,MED,UNI\n2026-10-01,09:00,1,1,1")] )));
        cut.Render();

        Assert.Equal("false", cut.Find(".confirmed-slots-page").GetAttribute("aria-busy"));
        Assert.Contains("Something went wrong. Please try again.", cut.Find("[role=alert]").TextContent);
        Assert.Null(cut.Find("input[type=file]").GetAttribute("disabled"));
    }

    /// <summary>A second confirmed-slot import cannot start while the first request is in flight.</summary>
    [Fact]
    public async Task ConfirmedSlotImportSuppressesReentryUntilTheRequestCompletes()
    {
        var handler = new DeferredImportHandler();
        Services.AddSingleton(new ConfirmedSlotsClient(NewHttpClient(handler)));
        GivenNoConfirmedSlots();
        var cut = Render<ConfirmedSlots>();

        var first = cut.InvokeAsync(() => cut.Instance.ImportCsvForTestingAsync("first"));
        await handler.ImportStarted.WaitAsync(TimeSpan.FromSeconds(2));
        var second = cut.InvokeAsync(() => cut.Instance.ImportCsvForTestingAsync("second"));

        Assert.Equal(1, handler.ImportRequestCount);
        handler.CompleteImport();
        await Task.WhenAll(first, second);
        cut.Render();

        Assert.Equal("false", cut.Find(".confirmed-slots-page").GetAttribute("aria-busy"));
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderAuthenticatedHome(MeDto me)
    {
        this.AddAuthorization().SetAuthorized("Staff Member");
        var outcome = ApiOutcome<MeDto>.Success(me, 200);

        RenderFragment page = builder =>
        {
            builder.OpenComponent<CascadingValue<ApiOutcome<MeDto>>>(0);
            builder.AddAttribute(1, "Value", outcome);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<Home>(0);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        };

        var cut = Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(page));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Loading…", cut.Markup));
        return cut;
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://api.example.com"),
    };

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value, options: CamelCase),
    };

    private static SettingsDto SettingsWithManager(Guid appointmentTypeId) => new(
        4,
        2,
        [new AppointmentTypeDto(appointmentTypeId, "DAT", "Drug & Alcohol Testing", null)]);

    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = [];

        /// <summary>
        /// Queues the next HTTP response observed by the rendered component.
        /// </summary>
        /// <param name="response">Builds the response for the next outgoing request.</param>
        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No HTTP response was configured for this component action.");
            }

            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private sealed class BrowserFile(string name, string content) : IBrowserFile
    {
        /// <summary>
        /// Gets the browser-provided file name used by the upload component.
        /// </summary>
        public string Name => name;

        /// <summary>
        /// Gets a deterministic modification instant for this in-memory upload fixture.
        /// </summary>
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        /// <summary>
        /// Gets the UTF-8 byte length of the fixture's CSV content.
        /// </summary>
        public long Size => Encoding.UTF8.GetByteCount(content);

        /// <summary>
        /// Gets the CSV media type supplied to the file input.
        /// </summary>
        public string ContentType => "text/csv";

        /// <summary>
        /// Opens the in-memory CSV content for the component upload path.
        /// </summary>
        /// <param name="maxAllowedSize">The maximum permitted input size requested by the component.</param>
        /// <param name="cancellationToken">Cancels opening the in-memory stream.</param>
        /// <returns>A readable stream containing the fixture's CSV content.</returns>
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private sealed class DeferredImportHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _importStarted = new();
        private readonly TaskCompletionSource _importCompleted = new();

        /// <summary>Gets the number of confirmed-slot import requests.</summary>
        public int ImportRequestCount { get; private set; }

        /// <summary>Completes when the first import request reaches the transport.</summary>
        public Task ImportStarted => _importStarted.Task;

        /// <summary>Releases the deferred import request.</summary>
        public void CompleteImport() => _importCompleted.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ImportRequestCount++;
            _importStarted.TrySetResult();
            await _importCompleted.Task.WaitAsync(cancellationToken);
            return Json(new SlotImportOutcomeDto(true, 1, []));
        }
    }

    /// <summary>
    /// The page lists confirmed slots on init. These import tests care only about the import card,
    /// so the slot list is served from its own always-empty handler rather than the queued one.
    /// </summary>
    private void GivenNoConfirmedSlots() =>
        Services.AddSingleton(new SlotsClient(new HttpClient(new EmptySlotOperationsHandler())
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));

    private sealed class EmptySlotOperationsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new SlotOperationsDto([]), options: CamelCase),
            });
    }
}
`````
