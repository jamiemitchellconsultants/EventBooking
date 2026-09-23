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
        Assert.Contains("href=\"/events/negotiate\"", cut.Markup);
        Assert.DoesNotContain("href=\"/attendees\"", cut.Markup);
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
        Assert.Contains("href=\"/attendees\"", cut.Markup);
        Assert.DoesNotContain("href=\"/events\"", cut.Markup);
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
        Assert.DoesNotContain("href=\"/events\"", cut.Markup);
    }

    /// <summary>
    /// Verifies that an authenticated account without a resolved role is described truthfully.
    /// </summary>
    [Fact]
    public void NoRoleHomeShowsTheDedicatedNoRoleState()
    {
        var cut = RenderAuthenticatedHome(new([], null, null));

        Assert.Contains("has not been assigned a role yet", cut.Markup);
        Assert.DoesNotContain("href=\"/events\"", cut.Markup);
        Assert.DoesNotContain("href=\"/attendees\"", cut.Markup);
    }

    /// <summary>
    /// Verifies winter, daylight-saving, and local-date-crossing presentation of stored UTC instants.
    /// </summary>
    [Fact]
    public void TransitionalLocationTimestampPresentationConvertsUtcInstantsWithoutChangingTheInstant()
    {
        var presentation = new TransitionalLocationTimePresentation("Europe/London");

        Assert.Equal("2026-01-15 12:00 +00:00 (Europe/London)",
            presentation.Format(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal("2026-07-15 13:00 +01:00 (Europe/London)",
            presentation.Format(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal("2026-06-02 00:30 +01:00 (Europe/London)",
            presentation.Format(new DateTimeOffset(2026, 6, 1, 23, 30, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// Verifies audit timestamps render as configured transitional-location local time with their offset and zone identifier.
    /// </summary>
    [Fact]
    public void AuditHistoryRendersTransitionalLocationLocalTimestamp()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new List<AuditRowDto>
        {
            new(new DateTimeOffset(2026, 6, 1, 23, 30, 0, TimeSpan.Zero), "Attendee", Guid.NewGuid(), "Updated", "Staff", "staff-1", null),
        }));
        Services.AddSingleton(new AuditClient(NewHttpClient(handler)));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));

        var cut = Render<AuditHistory>(parameters => parameters.Add(p => p.AttendeeId, Guid.NewGuid()));
        cut.Find("details").TriggerEvent("ontoggle", new EventArgs());

        cut.WaitForAssertion(() => Assert.Contains("2026-06-02 00:30 +01:00 (Europe/London)", cut.Markup));
    }

    /// <summary>
    /// Verifies the latest delivery timestamp uses the same configured local-time presentation as audit history.
    /// </summary>
    [Fact]
    public void AttendeesRenderLatestDeliveryInTransitionalLocationLocalTime()
    {
        var attendeeId = Guid.NewGuid();
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new List<AttendeeDto>
        {
            new(attendeeId, "C. Attendee", "attendee@example.com", null, null, null, false, [new("DAT", "Drug & Alcohol Testing")], 3, "Invited"),
        }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Events: [],
            EmailStatuses:
            [new AttendeeEmailStatusDto(
                attendeeId,
                "Invite",
                new DateTimeOffset(2026, 6, 1, 23, 30, 0, TimeSpan.Zero),
                "Sent",
                CanRetry: false)])));
        handler.Enqueue(_ => Json(new List<AttendeeGroupOptionDto>()));
        Services.AddSingleton(new AttendeesClient(NewHttpClient(handler)));
        Services.AddSingleton(new DashboardsClient(NewHttpClient(handler)));
        Services.AddSingleton(new AuditClient(NewHttpClient(handler)));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));

        var cut = Render<Attendees>();

        cut.WaitForAssertion(() => Assert.Contains("Invite 2026-06-02 00:30 +01:00 (Europe/London)", cut.Markup));
    }

    /// <summary>
    /// Verifies a event-board transport exception becomes an alert and restores the page busy state.
    /// </summary>
    [Fact]
    public void EventsReloadTransportExceptionRendersAnAlertAndClearsBusy()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => throw new HttpRequestException("offline"));
        Services.AddSingleton(new EventsClient(NewHttpClient(handler)));

        var cut = Render<EventNegotiation>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find(".event-board").GetAttribute("aria-busy"));
            Assert.Equal("Something went wrong. Please try again.", cut.Find("[role=alert]").TextContent.Trim());
        });
    }

    /// <summary>
    /// Verifies a event file-read exception is recoverable and does not strand the busy flag.
    /// </summary>
    [Fact]
    public async Task EventImportFileReadExceptionRendersAnAlertAndClearsBusy()
    {
        var handler = new RoutedHandler();
        Services.AddSingleton(new EventOperationsClient(NewHttpClient(handler)));
        GivenNoEvents();

        var cut = Render<EventOperations>();

        await cut.InvokeAsync(() => cut.Instance.ImportFileForTestingAsync(new InputFileChangeEventArgs([])));
        cut.Render();

        Assert.Equal("false", cut.Find(".events-page").GetAttribute("aria-busy"));
        Assert.Contains("Something went wrong. Please try again.", cut.Find("[role=alert]").TextContent);
        var input = cut.Find("input[type=file]");
        Assert.Null(input.GetAttribute("disabled"));
        Assert.Equal("Import events CSV", input.GetAttribute("aria-label"));
    }

    /// <summary>
    /// Verifies a event mutation transport failure renders the safe alert and releases the page busy state.
    /// </summary>
    [Fact]
    public async Task EventsMutationTransportExceptionRendersAnAlertAndClearsBusy()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new EventBoardDto([], [])));
        handler.Enqueue(_ => throw new HttpRequestException("offline"));
        Services.AddSingleton(new EventsClient(NewHttpClient(handler)));

        var cut = Render<EventNegotiation>();
        cut.WaitForAssertion(() => Assert.Contains("Submit proposal", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button.button-primary").Click());

        Assert.Equal("false", cut.Find(".event-board").GetAttribute("aria-busy"));
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
    /// Verifies a event import transport failure releases the input's busy state.
    /// </summary>
    [Fact]
    public async Task EventImportTransportExceptionRendersAnAlertAndClearsBusy()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => throw new HttpRequestException("offline"));
        Services.AddSingleton(new EventOperationsClient(NewHttpClient(handler)));
        GivenNoEvents();

        var cut = Render<EventOperations>();

        await cut.InvokeAsync(() => cut.Instance.ImportFileForTestingAsync(new InputFileChangeEventArgs(
            [new BrowserFile("events.csv", "date,startTime,DAT,MED,UNI\n2026-10-01,09:00,1,1,1")] )));
        cut.Render();

        Assert.Equal("false", cut.Find(".events-page").GetAttribute("aria-busy"));
        Assert.Contains("Something went wrong. Please try again.", cut.Find("[role=alert]").TextContent);
        Assert.Null(cut.Find("input[type=file]").GetAttribute("disabled"));
    }

    /// <summary>A second event import cannot start while the first request is in flight.</summary>
    [Fact]
    public async Task EventImportSuppressesReentryUntilTheRequestCompletes()
    {
        var handler = new DeferredImportHandler();
        Services.AddSingleton(new EventOperationsClient(NewHttpClient(handler)));
        GivenNoEvents();
        var cut = Render<EventOperations>();

        var first = cut.InvokeAsync(() => cut.Instance.ImportCsvForTestingAsync("first"));
        await handler.ImportStarted.WaitAsync(TimeSpan.FromSeconds(2));
        var second = cut.InvokeAsync(() => cut.Instance.ImportCsvForTestingAsync("second"));

        Assert.Equal(1, handler.ImportRequestCount);
        handler.CompleteImport();
        await Task.WhenAll(first, second);
        cut.Render();

        Assert.Equal("false", cut.Find(".events-page").GetAttribute("aria-busy"));
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

        /// <summary>Gets the number of event import requests.</summary>
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
            return Json(new EventImportOutcomeDto(true, 1, []));
        }
    }

    /// <summary>
    /// The page lists events on init. These import tests care only about the import card,
    /// so the event list is served from its own always-empty handler rather than the queued one.
    /// </summary>
    private void GivenNoEvents() =>
        Services.AddSingleton(new EventsClient(new HttpClient(new EmptyEventOperationsHandler())
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));

    private sealed class EmptyEventOperationsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new EventOperationsDto([]), options: CamelCase),
            });
    }
}
