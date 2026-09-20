# 00b — Vocabulary edits 111 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Web.Tests/CandidateRecoveryComponentTests.cs — 1/1

<!-- vocabulary-file: {"id":379,"oldPath":"tests/EventBooking.Web.Tests/CandidateRecoveryComponentTests.cs","newPath":"tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs","beforeSha":"67857bb2ffb6d610ed06ab89169af625fb6b49c7a1c00e9098669b925d6aa7c6","afterSha":"f282156cf95fa14ce4130c5357466aeb64f5c6970c8c082cff8765612a6b10a5","side":"before","part":1,"parts":1} -->

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

/// <summary>Verifies recovery start/cancel visibility, exact copy, and outcomes on the candidate page.</summary>
public class CandidateRecoveryComponentTests : BunitContext
{
    [Fact]
    public void ArrangeButtonIsHiddenWhenNothingIsRecoverable()
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(
            candidateId,
            Outstanding("Appointments outstanding", [("DAT", "Drug & Alcohol Testing", false)]),
            start: null,
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("ul.readiness-types li")));

        Assert.DoesNotContain(
            cut.FindAll("button"),
            button => button.TextContent.Trim() == "Arrange missed appointments");
    }

    [Fact]
    public void ArrangeStartsRecoveryAndShowsTheSingularOutcome()
    {
        var candidateId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();
        var requests = new List<HttpRequestMessage>();
        var cut = RenderCandidates(
            candidateId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: request =>
            {
                requests.Add(request);
                return OutcomeJson(inviteId, [Guid.NewGuid()], emailSent: true);
            },
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Recovery started for 1 missed appointment: Medical Check-Up. Email sent.",
                cut.Find("span.recovery-outcome").TextContent));
        Assert.Equal(
            $"/api/candidates/{candidateId}/recovery-invites",
            requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        FindButton(cut, "Cancel recovery");
    }

    [Fact]
    public void OutcomeUsesThePluralForTwoRecoverableTypes()
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(
            candidateId,
            Outstanding("Appointments outstanding", [
                ("MED", "Medical Check-Up", true),
                ("UNI", "Uniform Fitting", true),
            ]),
            start: _ => OutcomeJson(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], emailSent: true),
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Recovery started for 2 missed appointments: Medical Check-Up, Uniform Fitting. Email sent.",
                cut.Find("span.recovery-outcome").TextContent));
    }

    [Fact]
    public void OutcomeNamesTheFailedDelivery()
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(
            candidateId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: _ => OutcomeJson(Guid.NewGuid(), [Guid.NewGuid()], emailSent: false),
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Recovery started for 1 missed appointment: Medical Check-Up, but the email could not be sent.",
                cut.Find("span.recovery-outcome").TextContent));
    }

    [Fact]
    public void AwaitingAvailabilityKeepsTheCandidateUnbooked()
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(
            candidateId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: _ => OutcomeJson(Guid.Empty, [Guid.NewGuid()], emailSent: false),
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "No appointments are available yet for 1 missed appointment: Medical Check-Up.",
                cut.Find("span.recovery-outcome").TextContent));
        Assert.DoesNotContain(
            cut.FindAll("button"),
            button => button.TextContent.Trim() == "Cancel recovery");
    }

    [Fact]
    public void FailedStartShowsAnAlertWithoutAnOutcome()
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(
            candidateId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: _ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """{"title":"recovery_not_available","status":409}""",
                    Encoding.UTF8,
                    "application/problem+json"),
            },
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find("span.recovery-error[role=alert]")));
        Assert.Empty(cut.FindAll("span.recovery-outcome"));
    }

    [Fact]
    public void CancelRequiresConfirmationBeforeDeleting()
    {
        var candidateId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();
        var deletes = new List<HttpRequestMessage>();
        var cut = RenderCandidates(
            candidateId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: _ => OutcomeJson(inviteId, [Guid.NewGuid()], emailSent: true),
            cancel: request =>
            {
                deletes.Add(request);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Cancel recovery"));

        FindButton(cut, "Cancel recovery").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Confirm cancel"));
        Assert.Empty(deletes);

        FindButton(cut, "Confirm cancel").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("Recovery cancelled.", cut.Find("span.recovery-outcome").TextContent));
        Assert.Equal(
            $"/api/candidates/{candidateId}/recovery-invites/{inviteId}",
            deletes[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, deletes[0].Method);
    }

    private static CandidateReadinessDto Outstanding(
        string display, (string Code, string Name, bool IsRecoverable)[] types) =>
        new(
            Guid.NewGuid(),
            "AppointmentsOutstanding",
            display,
            types
                .Select(type => new OutstandingAppointmentTypeDto(type.Code, type.Name, type.IsRecoverable))
                .ToList());

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<Candidates> cut, string text) =>
        cut.FindAll("button").First(button => button.TextContent.Trim() == text);

    private IRenderedComponent<Candidates> RenderCandidates(
        Guid candidateId,
        CandidateReadinessDto readiness,
        Func<HttpRequestMessage, HttpResponseMessage>? start,
        Func<HttpRequestMessage, HttpResponseMessage>? cancel)
    {
        this.AddAuthorization().SetAuthorized("Coordinator");
        var stub = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post
                && path == $"/api/candidates/{candidateId}/recovery-invites"
                && start is not null)
            {
                return Task.FromResult(start(request));
            }

            if (request.Method == HttpMethod.Delete
                && path.StartsWith($"/api/candidates/{candidateId}/recovery-invites/", StringComparison.Ordinal)
                && cancel is not null)
            {
                return Task.FromResult(cancel(request));
            }

            if (path.EndsWith("/readiness", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(readiness));
            }

            if (path == "/api/candidates")
            {
                return Task.FromResult(Json(new[]
                {
                    new CandidateDto(
                        candidateId, "Amara Novak", "a.novak@mail.com", null, null, null, false,
                        [], 1, "Not yet invited"),
                }));
            }

            if (path == "/api/employee-groups")
            {
                return Task.FromResult(Json(Array.Empty<object>()));
            }

            return Task.FromResult(Json(new DashboardsDto([], [], [], [])));
        });
        Services.AddSingleton(
            new CandidatesClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(
            new DashboardsClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));
        Services.AddSingleton(
            new AuditClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));

        return Render<Candidates>();
    }

    private static HttpResponseMessage OutcomeJson(Guid inviteId, Guid[] typeIds, bool emailSent) =>
        Json(new { inviteId, appointmentTypeIds = typeIds, emailSent });

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

    /// <summary>Verifies each candidate row offers its own audit history panel.</summary>
    [Fact]
    public void CandidatesPageRendersHistoryPanelPerRow()
    {
        var cut = RenderCandidates(
            Guid.NewGuid(),
            Outstanding("Appointments outstanding", [("DAT", "Drug & Alcohol Testing", false)]),
            start: null,
            cancel: null);

        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        Assert.Single(cut.FindAll("details.audit-history"));
    }
}
`````

## after — tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs — 1/1

<!-- vocabulary-file: {"id":379,"oldPath":"tests/EventBooking.Web.Tests/CandidateRecoveryComponentTests.cs","newPath":"tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs","beforeSha":"67857bb2ffb6d610ed06ab89169af625fb6b49c7a1c00e9098669b925d6aa7c6","afterSha":"f282156cf95fa14ce4130c5357466aeb64f5c6970c8c082cff8765612a6b10a5","side":"after","part":1,"parts":1} -->

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

/// <summary>Verifies recovery start/cancel visibility, exact copy, and outcomes on the attendee page.</summary>
public class AttendeeRecoveryComponentTests : BunitContext
{
    [Fact]
    public void ArrangeButtonIsHiddenWhenNothingIsRecoverable()
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [("DAT", "Drug & Alcohol Testing", false)]),
            start: null,
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("ul.readiness-types li")));

        Assert.DoesNotContain(
            cut.FindAll("button"),
            button => button.TextContent.Trim() == "Arrange missed appointments");
    }

    [Fact]
    public void ArrangeStartsRecoveryAndShowsTheSingularOutcome()
    {
        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();
        var requests = new List<HttpRequestMessage>();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: request =>
            {
                requests.Add(request);
                return OutcomeJson(inviteId, [Guid.NewGuid()], emailSent: true);
            },
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Recovery started for 1 missed appointment: Medical Check-Up. Email sent.",
                cut.Find("span.recovery-outcome").TextContent));
        Assert.Equal(
            $"/api/attendees/{attendeeId}/recovery-invites",
            requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        FindButton(cut, "Cancel recovery");
    }

    [Fact]
    public void OutcomeUsesThePluralForTwoRecoverableTypes()
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [
                ("MED", "Medical Check-Up", true),
                ("UNI", "Uniform Fitting", true),
            ]),
            start: _ => OutcomeJson(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], emailSent: true),
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Recovery started for 2 missed appointments: Medical Check-Up, Uniform Fitting. Email sent.",
                cut.Find("span.recovery-outcome").TextContent));
    }

    [Fact]
    public void OutcomeNamesTheFailedDelivery()
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: _ => OutcomeJson(Guid.NewGuid(), [Guid.NewGuid()], emailSent: false),
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Recovery started for 1 missed appointment: Medical Check-Up, but the email could not be sent.",
                cut.Find("span.recovery-outcome").TextContent));
    }

    [Fact]
    public void AwaitingAvailabilityKeepsTheAttendeeUnbooked()
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: _ => OutcomeJson(Guid.Empty, [Guid.NewGuid()], emailSent: false),
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "No appointments are available yet for 1 missed appointment: Medical Check-Up.",
                cut.Find("span.recovery-outcome").TextContent));
        Assert.DoesNotContain(
            cut.FindAll("button"),
            button => button.TextContent.Trim() == "Cancel recovery");
    }

    [Fact]
    public void FailedStartShowsAnAlertWithoutAnOutcome()
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: _ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """{"title":"recovery_not_available","status":409}""",
                    Encoding.UTF8,
                    "application/problem+json"),
            },
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find("span.recovery-error[role=alert]")));
        Assert.Empty(cut.FindAll("span.recovery-outcome"));
    }

    [Fact]
    public void CancelRequiresConfirmationBeforeDeleting()
    {
        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();
        var deletes = new List<HttpRequestMessage>();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: _ => OutcomeJson(inviteId, [Guid.NewGuid()], emailSent: true),
            cancel: request =>
            {
                deletes.Add(request);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Cancel recovery"));

        FindButton(cut, "Cancel recovery").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Confirm cancel"));
        Assert.Empty(deletes);

        FindButton(cut, "Confirm cancel").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("Recovery cancelled.", cut.Find("span.recovery-outcome").TextContent));
        Assert.Equal(
            $"/api/attendees/{attendeeId}/recovery-invites/{inviteId}",
            deletes[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, deletes[0].Method);
    }

    private static AttendeeReadinessDto Outstanding(
        string display, (string Code, string Name, bool IsRecoverable)[] types) =>
        new(
            Guid.NewGuid(),
            "AppointmentsOutstanding",
            display,
            types
                .Select(type => new OutstandingAppointmentTypeDto(type.Code, type.Name, type.IsRecoverable))
                .ToList());

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<Attendees> cut, string text) =>
        cut.FindAll("button").First(button => button.TextContent.Trim() == text);

    private IRenderedComponent<Attendees> RenderAttendees(
        Guid attendeeId,
        AttendeeReadinessDto readiness,
        Func<HttpRequestMessage, HttpResponseMessage>? start,
        Func<HttpRequestMessage, HttpResponseMessage>? cancel)
    {
        this.AddAuthorization().SetAuthorized("Coordinator");
        var stub = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post
                && path == $"/api/attendees/{attendeeId}/recovery-invites"
                && start is not null)
            {
                return Task.FromResult(start(request));
            }

            if (request.Method == HttpMethod.Delete
                && path.StartsWith($"/api/attendees/{attendeeId}/recovery-invites/", StringComparison.Ordinal)
                && cancel is not null)
            {
                return Task.FromResult(cancel(request));
            }

            if (path.EndsWith("/readiness", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(readiness));
            }

            if (path == "/api/attendees")
            {
                return Task.FromResult(Json(new[]
                {
                    new AttendeeDto(
                        attendeeId, "Amara Novak", "a.novak@mail.com", null, null, null, false,
                        [], 1, "Not yet invited"),
                }));
            }

            if (path == "/api/attendee-groups")
            {
                return Task.FromResult(Json(Array.Empty<object>()));
            }

            return Task.FromResult(Json(new DashboardsDto([], [], [], [])));
        });
        Services.AddSingleton(
            new AttendeesClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(
            new DashboardsClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));
        Services.AddSingleton(
            new AuditClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));

        return Render<Attendees>();
    }

    private static HttpResponseMessage OutcomeJson(Guid inviteId, Guid[] typeIds, bool emailSent) =>
        Json(new { inviteId, appointmentTypeIds = typeIds, emailSent });

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

    /// <summary>Verifies each attendee row offers its own audit history panel.</summary>
    [Fact]
    public void AttendeesPageRendersHistoryPanelPerRow()
    {
        var cut = RenderAttendees(
            Guid.NewGuid(),
            Outstanding("Appointments outstanding", [("DAT", "Drug & Alcohol Testing", false)]),
            start: null,
            cancel: null);

        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        Assert.Single(cut.FindAll("details.audit-history"));
    }
}
`````

## before — tests/EventBooking.Web.Tests/CandidatesClientTests.cs — 1/1

<!-- vocabulary-file: {"id":380,"oldPath":"tests/EventBooking.Web.Tests/CandidatesClientTests.cs","newPath":"tests/EventBooking.Web.Tests/AttendeesClientTests.cs","beforeSha":"d4a02fa912836c2c63c4f877a92f48b35ff430342694ec87a02d7eaab8dacb49","afterSha":"2a4f831503afc97ebd02fa48ca02ed50c8cab236c37577bc3ccb2d3cc5399d27","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class CandidatesClientTests
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

    private static (CandidatesClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new CandidatesClient(http), handler);
    }

    [Fact]
    public async Task ListingWithoutASearchAsksForEveryCandidate()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<CandidateDto>()),
        };

        await client.ListAsync(null, null, CancellationToken.None);

        Assert.Equal("/api/candidates", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task ASearchTermIsUrlEncodedIntoTheQueryString()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<CandidateDto>()),
        };

        await client.ListAsync(null, "a novak@mail.com", CancellationToken.None);

        Assert.Contains("search=a%20novak%40mail.com", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task ListingWithoutAStatusOmitsTheStatusParameter()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<CandidateDto>()),
        };

        await client.ListAsync(null, null, CancellationToken.None);

        Assert.Equal("/api/candidates", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task FilteringByStatusIncludesTheStatusValueInTheQueryString()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<CandidateDto>()),
        };

        await client.ListAsync(2, null, CancellationToken.None);

        Assert.Contains("status=2", handler.Requests[0].RequestUri!.Query);
        Assert.DoesNotContain("search=", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task StatusAndSearchCombineIntoOneQueryString()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<CandidateDto>()),
        };

        await client.ListAsync(5, "a novak@mail.com", CancellationToken.None);

        var query = handler.Requests[0].RequestUri!.Query;
        Assert.Contains("status=5", query);
        Assert.Contains("search=a%20novak%40mail.com", query);
    }

    [Fact]
    public async Task ListingGroupsAsksForTheReferenceRoute()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<EmployeeGroupOptionDto>()),
        };

        await client.ListGroupsAsync(CancellationToken.None);

        Assert.Equal("/api/employee-groups", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CreatingPostsTheNameEmailAndGroup()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(Guid.NewGuid()),
        };
        var groupId = Guid.NewGuid();

        await client.CreateAsync("Amara Novak", "a.novak@mail.com", groupId, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("Amara Novak", handler.Bodies[0]);
        Assert.Contains(groupId.ToString(), handler.Bodies[0]);
        Assert.DoesNotContain("AppointmentTypeIds", handler.Bodies[0]);
    }

    [Fact]
    public async Task UpdatingPutsTheNameEmailAndGroup()
    {
        var (client, handler) = Given();
        var id = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await client.UpdateAsync(id, "Amara Novak", "a.novak@mail.com", groupId, CancellationToken.None);

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal($"/api/candidates/{id}", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("Amara Novak", handler.Bodies[0]);
        Assert.Contains(groupId.ToString(), handler.Bodies[0]);
    }

    [Fact]
    public async Task DeletingPassesTheConfirmFlag()
    {
        var (client, handler) = Given();
        var id = Guid.NewGuid();

        await client.DeleteAsync(id, true, CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal($"/api/candidates/{id}", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("?confirm=true", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task ImportingPostsTheCsvAsTheBody()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ImportOutcomeDto(true, 2, [])),
        };
        var csv = "name,email,employee_group\nAmara Novak,a.novak@mail.com,PILOTS";

        var outcome = await client.ImportAsync(csv, CancellationToken.None);

        Assert.True(outcome.Value!.Accepted);
        Assert.Equal("/api/candidates/import", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(csv, handler.Bodies[0]);
    }

    [Fact]
    public async Task ARejectedImportCarriesEveryRowError()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ImportOutcomeDto(
                false, 0, [new ImportErrorDto(2, "Name is required."), new ImportErrorDto(4, "XYZ is not a known employee group code.")])),
        };

        var outcome = await client.ImportAsync("anything", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.Value!.Accepted);
        Assert.Equal(2, outcome.Value.Errors.Count);
        Assert.Equal([2, 4], outcome.Value.Errors.Select(e => e.LineNumber));
    }

    [Fact]
    public async Task TriggeringAnInvitePostsToTheInviteRoute()
    {
        var (client, handler) = Given();
        var id = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { Invited = true }),
        };

        await client.TriggerInviteAsync(id, CancellationToken.None);

        Assert.Equal($"/api/candidates/{id}/invite", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task StartingRecoveryPostsToTheRecoveryRoute()
    {
        var (client, handler) = Given();
        var candidateId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { inviteId, appointmentTypeIds = new[] { typeId }, emailSent = true }),
        };

        var outcome = await client.StartRecoveryAsync(candidateId, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/candidates/{candidateId}/recovery-invites",
            handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(inviteId, outcome.Value!.InviteId);
        Assert.Equal([typeId], outcome.Value.AppointmentTypeIds);
        Assert.True(outcome.Value.EmailSent);
    }

    [Fact]
    public async Task CancellingRecoveryDeletesTheRecoveryRoute()
    {
        var (client, handler) = Given();
        var candidateId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

        var outcome = await client.CancelRecoveryAsync(candidateId, inviteId, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/candidates/{candidateId}/recovery-invites/{inviteId}",
            handler.Requests[0].RequestUri!.AbsolutePath);
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    [Fact]
    public async Task RecoveryCallsPropagateTheCancellationToken()
    {
        using var source = new CancellationTokenSource();
        source.CancelAfter(TimeSpan.FromMilliseconds(100));
        var startClient = new CandidatesClient(
            new HttpClient(new BlockingHandler()) { BaseAddress = new Uri("https://api.example.com") });
        var cancelClient = new CandidatesClient(
            new HttpClient(new BlockingHandler()) { BaseAddress = new Uri("https://api.example.com") });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            startClient.StartRecoveryAsync(Guid.NewGuid(), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cancelClient.CancelRecoveryAsync(Guid.NewGuid(), Guid.NewGuid(), source.Token));
    }

    [Fact]
    public async Task EveryResponseIsDisposedOnlyAfterTheClientHasConsumedItsContent()
    {
        var (client, handler) = Given();
        var listContent = new TrackingContent(JsonSerializer.Serialize(new List<CandidateDto>()));
        var createContent = new TrackingContent(JsonSerializer.Serialize(Guid.NewGuid()));
        var importContent = new TrackingContent(JsonSerializer.Serialize(new ImportOutcomeDto(true, 1, [])));
        var listResponse = new TrackingResponseMessage(HttpStatusCode.OK, listContent);
        var createResponse = new TrackingResponseMessage(HttpStatusCode.Created, createContent);
        var updateResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var deleteResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var importResponse = new TrackingResponseMessage(HttpStatusCode.OK, importContent);
        var inviteResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var responses = new[]
        {
            listResponse,
            createResponse,
            updateResponse,
            deleteResponse,
            importResponse,
            inviteResponse,
        };

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        var id = Guid.NewGuid();
        await client.ListAsync(null, null, CancellationToken.None);
        await client.CreateAsync("Amara Novak", "a.novak@mail.com", null, CancellationToken.None);
        await client.UpdateAsync(id, "Amara Novak", "a.novak@mail.com", null, CancellationToken.None);
        await client.DeleteAsync(id, false, CancellationToken.None);
        await client.ImportAsync("name,email,employee_group", CancellationToken.None);
        await client.TriggerInviteAsync(id, CancellationToken.None);

        Assert.All(responses, response => Assert.True(response.WasDisposed));
        Assert.True(listContent.WasRead);
        Assert.True(createContent.WasRead);
        Assert.True(importContent.WasRead);
        Assert.True(listResponse.WasDisposedAfterContentWasRead);
        Assert.True(createResponse.WasDisposedAfterContentWasRead);
        Assert.True(importResponse.WasDisposedAfterContentWasRead);
    }

    [Fact]
    public async Task GetBookingsCallsTheBookingsRouteAndReadsAnEmptyList()
    {
        var handler = new StubHandler();
        var candidateId = Guid.NewGuid();
        handler.Responses.Enqueue(Json("[]"));
        var client = NewCandidatesClient(handler);

        var outcome = await client.GetBookingsAsync(candidateId, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Empty(outcome.Value!);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal($"/api/candidates/{candidateId}/bookings", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetBookingsReadsAnOriginalAndARecoveryRow()
    {
        var handler = new StubHandler();
        var originalId = Guid.NewGuid();
        var recoveryId = Guid.NewGuid();
        handler.Responses.Enqueue(Json($$"""
            [
              {"bookingId":"{{originalId}}","isOriginal":true,"slotDate":"2026-09-10",
               "slotStartTime":"09:00:00","slotEndTime":"13:00:00"},
              {"bookingId":"{{recoveryId}}","isOriginal":false,"slotDate":"2026-09-12",
               "slotStartTime":"13:00:00","slotEndTime":"17:00:00"}
            ]
            """));
        var client = NewCandidatesClient(handler);

        var outcome = await client.GetBookingsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(2, outcome.Value!.Count);
        Assert.Equal(originalId, outcome.Value[0].BookingId);
        Assert.True(outcome.Value[0].IsOriginal);
        Assert.Equal(new DateOnly(2026, 9, 10), outcome.Value[0].SlotDate);
        Assert.Equal(new TimeOnly(13, 0), outcome.Value[0].SlotEndTime);
        Assert.False(outcome.Value[1].IsOriginal);
        Assert.Equal(recoveryId, outcome.Value[1].BookingId);
    }

    [Fact]
    public async Task CancelBookingPostsTheRebookFlagAndReadsTheOutcome()
    {
        var handler = new StubHandler();
        var candidateId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        handler.Responses.Enqueue(Json(
            """{"reinvited":false,"inviteCreated":false,"deliveryStatus":"Unavailable","deliveryId":null}"""));
        var client = NewCandidatesClient(handler);

        var outcome = await client.CancelBookingAsync(
            candidateId, bookingId, rebook: false, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.Value!.Reinvited);
        Assert.Equal("Unavailable", outcome.Value.DeliveryStatus);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel",
            handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("\"rebook\":false", handler.Bodies[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelAndRebookReportsTheReplacementDelivery()
    {
        var handler = new StubHandler();
        var deliveryId = Guid.NewGuid();
        handler.Responses.Enqueue(Json(
            $$"""{"reinvited":true,"inviteCreated":true,"deliveryStatus":"Sent","deliveryId":"{{deliveryId}}"}"""));
        var client = NewCandidatesClient(handler);

        var outcome = await client.CancelBookingAsync(
            Guid.NewGuid(), Guid.NewGuid(), rebook: true, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.True(outcome.Value!.Reinvited);
        Assert.Equal("Sent", outcome.Value.DeliveryStatus);
        Assert.Equal(deliveryId, outcome.Value.DeliveryId);
        Assert.Contains("\"rebook\":true", handler.Bodies[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelBookingSurfacesAConflictAsAFailure()
    {
        var handler = new StubHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Conflict));
        var client = NewCandidatesClient(handler);

        var outcome = await client.CancelBookingAsync(
            Guid.NewGuid(), Guid.NewGuid(), rebook: true, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, outcome.StatusCode);
    }

    private static CandidatesClient NewCandidatesClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
`````
