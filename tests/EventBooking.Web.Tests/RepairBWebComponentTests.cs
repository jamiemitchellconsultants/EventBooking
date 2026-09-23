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
