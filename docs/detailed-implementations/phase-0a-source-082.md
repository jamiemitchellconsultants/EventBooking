# 00a — Port source 82 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Web.Tests/HelpPageTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/HelpPageTests.cs","encoding":"utf8","sha256":"dc46378c4e5fc16839a44a1881708a86535195309c5af3338717e715e87ffca2","parts":1,"part":1} -->

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

## tests/EventBooking.Web.Tests/HomePageTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/HomePageTests.cs","encoding":"utf8","sha256":"e9013cd413e30244b21b4efa3fbe33be934455f526cb667a9ea262f030286ba0","parts":1,"part":1} -->

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

## tests/EventBooking.Web.Tests/MainLayoutTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/MainLayoutTests.cs","encoding":"utf8","sha256":"7ed5b096218b0515a86da71ee57de0a0375ad134409d1a2c0f4c739701d00d78","parts":1,"part":1} -->

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

## tests/EventBooking.Web.Tests/MeClientTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/MeClientTests.cs","encoding":"utf8","sha256":"790f30d2638f826a556b9324f5e976de499ce090bd847518844a5ea501a958ab","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class MeClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Response);
        }
    }

    private static (MeClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new MeClient(http), handler);
    }

    [Fact]
    public async Task TheCallerIsFetchedFromTheMeRoute()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeDto(["Manager", "Coordinator"], Guid.NewGuid(), "Drug & Alcohol Testing")),
        };

        var outcome = await client.GetAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("/api/me", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(["Manager", "Coordinator"], outcome.Value!.Roles);
    }

    [Fact]
    public async Task AnUnassignedCallerGetsAnEmptyRoleSet()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeDto([], null, null)),
        };

        var outcome = await client.GetAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Empty(outcome.Value!.Roles);
    }
}
`````

## tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs","encoding":"utf8","sha256":"9ef1bd43e8838fa27303de43961b48a1fba3f273df3df65f88a2fe7052bbf4c6","parts":1,"part":1} -->

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

## tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs","encoding":"utf8","sha256":"f2a49e041005ab8893ddfbe6a1942261131ee7642f6959768eb1c2a4c248ef0f","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies candidate pages describe durable email outcomes without false promises.</summary>
public class RepairDNotificationComponentTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Booking confirmation failure still displays the usable manage-link recovery.</summary>
    [Fact]
    public void BookPageUsesNeutralCopyWhenConfirmationDeliveryFails()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["DAT"],
            [new InviteOptionDto(
                Guid.NewGuid(),
                new DateOnly(2030, 1, 14),
                new TimeOnly(9, 0),
                new TimeOnly(13, 0),
                "Monday 14 Jan 2030, 09:00-13:00")] )));
        handler.Enqueue(_ => Json(new ConfirmedBookingDto(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "fresh-manage-token",
            "Failed")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
        cut.WaitForAssertion(() => Assert.Contains("Confirm this time", cut.Markup));
        cut.Find("input[type=radio]").Change(true);
        cut.Find("button.booking-page__button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm email delivery", cut.Markup);
            Assert.Contains("fresh-manage-token", cut.Markup);
            Assert.DoesNotContain("is on its way", cut.Markup);
        });
    }

    /// <summary>The confirmed page names the head office the API sent, the same one the confirmation email uses.</summary>
    [Fact]
    public void BookPageShowsTheHeadOfficeAddressReturnedByTheApi()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["DAT"],
            [new InviteOptionDto(
                Guid.NewGuid(),
                new DateOnly(2030, 1, 14),
                new TimeOnly(9, 0),
                new TimeOnly(13, 0),
                "Monday 14 Jan 2030, 09:00-13:00")])));
        handler.Enqueue(_ => Json(new ConfirmedBookingDto(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "fresh-manage-token",
            "Sent",
            "2 Api Street, London")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
        cut.WaitForAssertion(() => Assert.Contains("Confirm this time", cut.Markup));
        cut.Find("input[type=radio]").Change(true);
        cut.Find("button.booking-page__button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Head office: 2 Api Street, London", cut.Markup));
    }

    /// <summary>Booking confirmation pending delivery also uses neutral recovery copy.</summary>
    [Fact]
    public void BookPageUsesNeutralCopyWhenConfirmationDeliveryIsPending()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["DAT"],
            [new InviteOptionDto(
                Guid.NewGuid(),
                new DateOnly(2030, 1, 14),
                new TimeOnly(9, 0),
                new TimeOnly(13, 0),
                "Monday 14 Jan 2030, 09:00-13:00")])));
        handler.Enqueue(_ => Json(new ConfirmedBookingDto(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "fresh-manage-token",
            "Pending")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
        cut.WaitForAssertion(() => Assert.Contains("Confirm this time", cut.Markup));
        cut.Find("input[type=radio]").Change(true);
        cut.Find("button.booking-page__button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm email delivery", cut.Markup);
            Assert.Contains("fresh-manage-token", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
        });
    }

    /// <summary>Cancel/rebook failure displays neutral recovery rather than sent wording.</summary>
    [Fact]
    public void ManagePageUsesNeutralCopyWhenReplacementDeliveryFails()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: true,
            InviteCreated: true,
            DeliveryStatus: "Failed",
            DeliveryId: Guid.NewGuid())));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm delivery", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
            Assert.DoesNotContain("on its way", cut.Markup);
        });
    }

    /// <summary>Cancel/rebook pending delivery displays neutral recovery guidance.</summary>
    [Fact]
    public void ManagePageUsesNeutralCopyWhenReplacementDeliveryIsPending()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: true,
            InviteCreated: true,
            DeliveryStatus: "Pending",
            DeliveryId: Guid.NewGuid())));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm delivery", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
            Assert.DoesNotContain("on its way", cut.Markup);
        });
    }

    /// <summary>A missing replacement invite explains that the team will arrange the next time.</summary>
    [Fact]
    public void ManagePageDistinguishesUnavailableReplacementInvite()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: false,
            InviteCreated: false,
            DeliveryStatus: "Unavailable")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("There are no times available right now", cut.Markup);
            Assert.DoesNotContain("could not confirm delivery", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
        });
    }

    /// <summary>A sent replacement invite is the only state that promises delivery.</summary>
    [Fact]
    public void ManagePagePromisesReplacementDeliveryOnlyWhenSent()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: true,
            InviteCreated: true,
            DeliveryStatus: "Sent",
            DeliveryId: Guid.NewGuid())));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("new invitation with fresh times has been sent", cut.Markup);
            Assert.DoesNotContain("could not confirm delivery", cut.Markup);
        });
    }

    /// <summary>A stale failed delivery remains visible but offers no enabled resend action.</summary>
    [Fact]
    public void CandidatesHideResendWhenServerMarksTheLatestContextStale()
    {
        var candidateId = Guid.NewGuid();
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new List<CandidateDto>
        {
            new(candidateId, "Amara Novak", "a.novak@mail.com", null, null, null, false, [new("DAT", "Drug & Alcohol Testing")], 1, "Not yet invited"),
        }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Slots: [],
            EmailStatuses:
            [new CandidateEmailStatusDto(
                candidateId,
                "Booking confirmation",
                new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero),
                "Failed",
                CanRetry: false)])));
        handler.Enqueue(_ => Json(new List<EmployeeGroupOptionDto>()));
        Services.AddSingleton(new CandidatesClient(NewHttpClient(handler)));
        Services.AddSingleton(new DashboardsClient(NewHttpClient(handler)));
        Services.AddSingleton(new AuditClient(NewHttpClient(handler)));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        var cut = Render<Candidates>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Booking confirmation", cut.Markup);
            Assert.DoesNotContain(">Resend<", cut.Markup);
        });
    }

    /// <summary>Actionable failed and pending deliveries use the retry endpoint, not the invite endpoint.</summary>
    [Theory]
    [InlineData("Failed")]
    [InlineData("Pending")]
    public void CandidatesResendActionUsesTheTemplateAwareRetryEndpoint(string status)
    {
        var candidateId = Guid.NewGuid();
        var candidate = new CandidateDto(
            candidateId, "Amara Novak", "a.novak@mail.com", null, null, null, false, [new("DAT", "Drug & Alcohol Testing")], 1, "Not yet invited");
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new List<CandidateDto> { candidate }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Slots: [],
            EmailStatuses:
            [new CandidateEmailStatusDto(
                candidateId,
                "Booking confirmation",
                new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero),
                status,
                CanRetry: true)])));
        handler.Enqueue(_ => Json(new List<EmployeeGroupOptionDto>()));
        handler.Enqueue(_ => Json(new EmailRetryDto("Sent", Guid.NewGuid())));
        handler.Enqueue(_ => Json(new List<CandidateDto> { candidate }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Slots: [],
            EmailStatuses:
            [new CandidateEmailStatusDto(
                candidateId,
                "Booking confirmation",
                new DateTimeOffset(2026, 9, 7, 10, 1, 0, TimeSpan.Zero),
                "Sent",
                CanRetry: false)])));
        handler.Enqueue(_ => Json(new List<EmployeeGroupOptionDto>()));
        Services.AddSingleton(new CandidatesClient(NewHttpClient(handler)));
        Services.AddSingleton(new DashboardsClient(NewHttpClient(handler)));
        Services.AddSingleton(new AuditClient(NewHttpClient(handler)));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        var cut = Render<Candidates>();
        cut.WaitForAssertion(() => Assert.Contains(">Resend<", cut.Markup));

        cut.FindAll("button").Single(button => button.TextContent == "Resend").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                handler.Requests,
                request => request.Method == HttpMethod.Post
                    && request.Path == $"/api/candidates/{candidateId}/email-retry");
            Assert.DoesNotContain(
                handler.Requests,
                request => request.Method == HttpMethod.Post
                    && request.Path == $"/api/candidates/{candidateId}/invite");
        });
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://api.example.com"),
    };

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value, options: CamelCase),
    };

    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = [];

        /// <summary>Records the method and path of every handled request.</summary>
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];

        /// <summary>Queues one deterministic response for the next page request.</summary>
        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri?.AbsolutePath ?? string.Empty));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No HTTP response was configured.");
            }

            return Task.FromResult(_responses.Dequeue()(request));
        }
    }
}
`````

## tests/EventBooking.Web.Tests/SettingsTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/SettingsTests.cs","encoding":"utf8","sha256":"fb33e7e8d4c55294b82ba425bb0965608656a8a92b430b24372e76010dc831da","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class SettingsTests : BunitContext
{
    [Fact]
    public void SettingsContainsOnlyFixedTypesAndInvitationTiming()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SettingsDto(
                4,
                2,
                [new AppointmentTypeDto(
                    AppointmentTypeIds.DrugAndAlcoholTesting,
                    "DAT",
                    "Drug & Alcohol Testing",
                    Guid.NewGuid())])),
        });
        Services.AddSingleton(new AdminClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));

        var cut = Render<Settings>();

        cut.WaitForAssertion(() => Assert.Contains("Drug & Alcohol Testing", cut.Find("table").TextContent));
        Assert.Contains("System settings", cut.Markup);
        Assert.DoesNotContain("Assign manager", cut.Markup);
        Assert.DoesNotContain("Import Confirmed Slots", cut.Markup);
        Assert.Single(handler.Requests);
        Assert.Equal("/api/admin/settings", handler.Requests[0].RequestUri!.AbsolutePath);
    }


    /// <summary>Verifies the manager column prefers the name and always keeps the staff number.</summary>
    [Theory]
    [InlineData("Dana Datson", "U000002", "Dana Datson (U000002)")]
    [InlineData(null, "U000002", "U000002")]
    public void ManagerColumnRendersNameStates(string? displayName, string? staffId, string expected)
    {
        var cut = RenderWithManager(new AppointmentTypeDto(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            "DAT",
            "Drug & Alcohol Testing",
            Guid.NewGuid(),
            staffId,
            displayName));

        cut.WaitForAssertion(() => Assert.Contains(expected, ManagerCell(cut)));
    }

    /// <summary>Verifies an appointment type with no manager reads as unassigned.</summary>
    [Fact]
    public void ManagerColumnRendersUnassigned()
    {
        var cut = RenderWithManager(new AppointmentTypeDto(
            AppointmentTypeIds.DrugAndAlcoholTesting, "DAT", "Drug & Alcohol Testing", null));

        cut.WaitForAssertion(() => Assert.Contains("Unassigned", ManagerCell(cut)));
    }

    /// <summary>Verifies an assigned manager with no recorded identity reads as pending sync.</summary>
    [Fact]
    public void ManagerColumnRendersPendingSync()
    {
        var cut = RenderWithManager(new AppointmentTypeDto(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            "DAT",
            "Drug & Alcohol Testing",
            Guid.NewGuid()));

        cut.WaitForAssertion(() =>
            Assert.Contains("Assigned (pending identity sync)", ManagerCell(cut)));
    }

    private static string ManagerCell(IRenderedComponent<Settings> cut) =>
        cut.Find("td[data-label='Manager identifier']").TextContent;

    private IRenderedComponent<Settings> RenderWithManager(AppointmentTypeDto type)
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SettingsDto(4, 2, [type])),
        });
        Services.AddSingleton(new AdminClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));

        return Render<Settings>();
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(response);
        }
    }
}
`````

## tests/EventBooking.Web.Tests/SlotsClientCapacityAdjustmentTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/SlotsClientCapacityAdjustmentTests.cs","encoding":"utf8","sha256":"412a678535af32b4a2fc0953be40650249b27f1788dd468d28452ef26bb6efdc","parts":1,"part":1} -->

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
