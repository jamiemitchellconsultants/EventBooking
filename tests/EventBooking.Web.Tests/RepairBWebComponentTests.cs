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
        Assert.Contains("href=\"/admin/settings\"", cut.Markup);
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
    /// Verifies audit timestamps render as UTC so entries from every location compare directly.
    /// </summary>
    [Fact]
    public void AuditHistoryRendersUtcTimestamp()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new PageDto<AuditRowDto>(
        [
            new(Guid.NewGuid(), new DateTimeOffset(2026, 6, 1, 23, 30, 0, TimeSpan.Zero), "Attendee", Guid.NewGuid(), "Updated", "Staff", "staff-1", null, null, new Dictionary<string, ApiLink>()),
        ], null)));
        Services.AddSingleton<IAuditClient>(new AuditClient(NewHttpClient(handler)));

        var cut = Render<AuditHistory>(parameters => parameters
            .Add(p => p.EntityKind, AuditEntityKind.Attendee)
            .Add(p => p.EntityId, Guid.NewGuid()));
        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Contains("2026-06-01 23:30 UTC", cut.Markup));
    }

    /// <summary>
    /// Verifies the delivery column shows the list row's latest delivery status.
    /// </summary>
    [Fact]
    public void AttendeesRenderLatestDeliveryStatus()
    {
        var attendeeId = Guid.NewGuid();
        var handler = new RoutedHandler();
        EnqueueReferenceData(handler);
        handler.Enqueue(_ => Json(new PageDto<AttendeeDto>(
            [new AttendeeDto(
                attendeeId, "C. Attendee", "attendee@example.com", "Invited",
                "Invited (pending response)", "MED", "NoActiveBooking", ["MED"], "Sent", "cursor",
                null, new Dictionary<string, ApiLink>())],
            null)));
        Services.AddSingleton<IAttendeesClient>(new AttendeesClient(NewHttpClient(handler)));
        Services.AddSingleton<IAuditClient>(new AuditClient(NewHttpClient(handler)));

        var cut = Render<Attendees>();

        cut.WaitForAssertion(() => Assert.Contains("Sent", cut.Markup));
    }

    /// <summary>
    /// Verifies a failed delivery offers Resend, and Resend retries that exact delivery.
    /// </summary>
    [Fact]
    public void AttendeesResendRetriesTheFailedDelivery()
    {
        var attendeeId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var page = new PageDto<AttendeeDto>(
            [new AttendeeDto(
                attendeeId, "C. Attendee", "attendee@example.com", "Invited",
                "Invited (pending response)", "MED", "NoActiveBooking", ["MED"], "Failed", "cursor",
                deliveryId, new Dictionary<string, ApiLink>
                {
                    ["emailRetry"] = new($"/api/attendees/{attendeeId}/email-retry", "POST", "retryEmail"),
                })],
            null);
        string? retried = null;
        var handler = new RoutedHandler();
        EnqueueReferenceData(handler);
        handler.Enqueue(_ => Json(page));
        handler.Enqueue(request =>
        {
            retried = request.RequestUri!.PathAndQuery;
            return Json(new EmailRetryDto(Guid.NewGuid()));
        });
        EnqueueReferenceData(handler);
        handler.Enqueue(_ => Json(page));
        Services.AddSingleton<IAttendeesClient>(new AttendeesClient(NewHttpClient(handler)));
        Services.AddSingleton<IAuditClient>(new AuditClient(NewHttpClient(handler)));

        var cut = Render<Attendees>();
        cut.WaitForAssertion(() => Assert.Contains("Failed", cut.Markup));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Resend").Click();

        cut.WaitForAssertion(() => Assert.Equal(
            $"/api/attendees/{attendeeId}/email-retry", retried));
    }

    /// <summary>
    /// Verifies a event-board transport exception becomes an alert and restores the page busy state.
    /// </summary>
    [Fact]
    public void EventsReloadTransportExceptionRendersAnAlertAndClearsBusy()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => throw new HttpRequestException("offline"));
        Services.AddSingleton<IEventsClient>(new EventsClient(NewHttpClient(handler)));

        var cut = Render<EventNegotiation>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find(".event-board").GetAttribute("aria-busy"));
            Assert.Contains("Something went wrong. Please try again.", cut.Find("[role=alert]").TextContent);
        });
    }

    /// <summary>
    /// Verifies a event mutation transport failure renders the safe alert and releases the page busy state.
    /// </summary>
    [Fact]
    public async Task EventsMutationTransportExceptionRendersAnAlertAndClearsBusy()
    {
        // The negotiation board loads reference data, proposals and events before the
        // proposal dialog can submit; the failure below lands on the submit call.
        var scopeType = Guid.NewGuid();
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Raw("""
            {"displayName":"Manny Manager","staffId":"M1","roles":["Manager"],"scopeAppointmentTypeId":"SCOPE","scopeAppointmentTypeCode":"MED","scopeAppointmentTypeName":"Medical check","capabilities":[],"problem":null,"_links":{"proposeEvent":{"href":"/api/event-proposals","method":"POST","operationId":"proposeEvent"}}}
            """.Replace("SCOPE", scopeType.ToString())));
        handler.Enqueue(_ => Raw("""
            {"items":[{"id":"10000000-0000-0000-0000-000000000001","code":"LON","name":"London HQ","address":"1 Example St","timeZoneId":"Europe/London","isActive":true,"version":1,"_links":{}}],"nextCursor":null}
            """));
        handler.Enqueue(_ => Raw("""
            {"items":[{"id":"SCOPE","code":"MED","name":"Medical check","isActive":true,"version":1,"hasManager":true,"managerDisplayName":"Manny Manager","_links":{}}],"nextCursor":null}
            """.Replace("SCOPE", scopeType.ToString())));
        handler.Enqueue(_ => Raw("""{"items":[],"nextCursor":null}"""));
        handler.Enqueue(_ => Raw("""{"items":[],"nextCursor":null}"""));
        handler.Enqueue(_ => throw new HttpRequestException("offline"));
        Services.AddSingleton<IEventsClient>(new EventsClient(NewHttpClient(handler)));

        var cut = Render<EventNegotiation>();
        cut.WaitForAssertion(() => Assert.Contains("Propose event", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("[data-action='propose']").Click());
        await cut.InvokeAsync(() => cut.Find("[data-action='submit-proposal']").Click());

        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find(".event-board").GetAttribute("aria-busy")));
        Assert.Contains("Something went wrong. Please try again.", cut.Find("[role=alert]").TextContent);
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

    private static void EnqueueReferenceData(RoutedHandler handler)
    {
        handler.Enqueue(_ => Json(new MeDto(["Coordinator"], null, null)));
        handler.Enqueue(_ => Json(new PageDto<LocationDto>([], null)));
        handler.Enqueue(_ => Json(new PageDto<AttendeeGroupDto>([], null)));
        handler.Enqueue(_ => Json(new PageDto<AppointmentTypeDto>([], null)));
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://api.example.com"),
    };

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value, options: CamelCase),
    };

    private static HttpResponseMessage Raw(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private static SettingsDto SettingsWithManager(Guid appointmentTypeId) => new(
        4,
        2,
        3,
        1,
        new Dictionary<string, ApiLink>());

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

}
