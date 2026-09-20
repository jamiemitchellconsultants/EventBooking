# 00a — Port source 81 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Web.Tests/CandidateRecoveryComponentTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/CandidateRecoveryComponentTests.cs","encoding":"utf8","sha256":"67857bb2ffb6d610ed06ab89169af625fb6b49c7a1c00e9098669b925d6aa7c6","parts":1,"part":1} -->

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

## tests/EventBooking.Web.Tests/CandidatesClientTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/CandidatesClientTests.cs","encoding":"utf8","sha256":"d4a02fa912836c2c63c4f877a92f48b35ff430342694ec87a02d7eaab8dacb49","parts":1,"part":1} -->

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

## tests/EventBooking.Web.Tests/ConfirmedSlotsClientTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/ConfirmedSlotsClientTests.cs","encoding":"utf8","sha256":"89bd4cfcbec66c8aace0f21bcbb44614869ebc8e024e0af597efb1b2e116228d","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class ConfirmedSlotsClientTests
{
    [Fact]
    public async Task ImportPostsTheCsvToTheNeutralRoute()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new SlotImportOutcomeDto(true, 2, [])),
            },
        };
        var client = new ConfirmedSlotsClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        });

        var result = await client.ImportAsync(
            "date,startTime,DAT,MED,UNI\n2026-09-10,09:00,10,6,8",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ImportedCount);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/confirmed-slots/import", handler.Request.RequestUri!.AbsolutePath);
        Assert.Equal("text/csv", handler.Request.Content!.Headers.ContentType!.MediaType);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public HttpResponseMessage Response { get; init; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }
}
`````

## tests/EventBooking.Web.Tests/ConfirmedSlotsPageTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/ConfirmedSlotsPageTests.cs","encoding":"utf8","sha256":"0b030fa51d5796aa825d06a99e707f0c5e3bf2ec4f3206dae015d58150a66791","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class ConfirmedSlotsPageTests : BunitContext
{
    [Fact]
    public async Task AcceptedImportShowsCountAndUsesNoCandidateClient()
    {
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([]));
        handler.Enqueue("/api/confirmed-slots/import", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SlotImportOutcomeDto(true, 4, [])),
        });
        GivenClients(handler);
        var cut = Render<ConfirmedSlots>();

        await cut.InvokeAsync(() => cut.Instance.ImportCsvForTestingAsync(
            "date,startTime,DAT,MED,UNI\n2026-09-10,09:00,10,6,8"));

        Assert.Contains("4 confirmed slots imported", cut.Markup);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath == "/api/confirmed-slots/import");
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.AbsolutePath.StartsWith("/api/candidates", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectedImportShowsEveryRowError()
    {
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([]));
        handler.Enqueue("/api/confirmed-slots/import", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SlotImportOutcomeDto(
                false,
                0,
                [new SlotImportErrorDto(3, "DAT must be a positive integer.")])),
        });
        GivenClients(handler);
        var cut = Render<ConfirmedSlots>();

        await cut.InvokeAsync(() => cut.Instance.ImportCsvForTestingAsync("bad"));

        Assert.Contains("Nothing was imported", cut.Markup);
        Assert.Contains("Line 3", cut.Markup);
        Assert.Contains("DAT must be a positive integer", cut.Markup);
    }

    [Fact]
    public void RendersSlotSectionWithCancelControls()
    {
        var slotId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([Slot(slotId, activeBookings: 1)]));
        GivenClients(handler);

        var cut = Render<ConfirmedSlots>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#confirmed-slot-operations tbody tr")));
        Assert.Contains("2026-09-10", cut.Markup);
        Assert.Contains("Cancel slot", cut.Markup);
        Assert.Contains("DAT 9/10", cut.Markup);
    }

    [Fact]
    public async Task CancelWithNoBookingsSucceedsOnTheFirstClick()
    {
        var slotId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([Slot(slotId, activeBookings: 0)]));
        handler.Enqueue($"/api/slots/confirmed/{slotId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/slots/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<ConfirmedSlots>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#confirmed-slot-operations tbody tr")));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("No confirmed slots yet", cut.Markup));
        var delete = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Delete);
        Assert.Contains("confirm=false", delete.RequestUri!.Query);
    }

    [Fact]
    public async Task CancelWithBookingsRequiresASecondConfirmClick()
    {
        var slotId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([Slot(slotId, activeBookings: 6)]));
        handler.Enqueue($"/api/slots/confirmed/{slotId}", Conflict(
            "Cancelling this slot will cancel 6 confirmed bookings."));
        handler.Enqueue($"/api/slots/confirmed/{slotId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/slots/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<ConfirmedSlots>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#confirmed-slot-operations tbody tr")));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        Assert.Contains("6 confirmed bookings", cut.Find("[role=alert]").TextContent);
        Assert.Contains("Press Confirm cancel to proceed", cut.Find("[role=alert]").TextContent);
        Assert.Single(cut.FindAll("#confirmed-slot-operations tbody tr"));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("No confirmed slots yet", cut.Markup));
        var deletes = handler.Requests.Where(r => r.Method == HttpMethod.Delete).ToList();
        Assert.Equal(2, deletes.Count);
        Assert.Contains("confirm=false", deletes[0].RequestUri!.Query);
        Assert.Contains("confirm=true", deletes[1].RequestUri!.Query);
    }

    [Fact]
    public async Task TheSlotSectionNeverCallsDashboards()
    {
        var slotId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", OperationsJson([Slot(slotId, activeBookings: 0)]));
        handler.Enqueue($"/api/slots/confirmed/{slotId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/slots/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<ConfirmedSlots>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#confirmed-slot-operations tbody tr")));
        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());
        cut.WaitForAssertion(() => Assert.Contains("No confirmed slots yet", cut.Markup));

        Assert.DoesNotContain(
            handler.Requests,
            r => r.RequestUri!.AbsolutePath.StartsWith("/api/dashboards", StringComparison.Ordinal));
    }

    [Fact]
    public void AForbiddenSlotLoadShowsTheSafeMessage()
    {
        var handler = new RoutingHandler();
        handler.Enqueue("/api/slots/operations", new HttpResponseMessage(HttpStatusCode.Forbidden));
        GivenClients(handler);

        var cut = Render<ConfirmedSlots>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("p.banner.error[role=alert]")));
        Assert.Empty(cut.FindAll("#confirmed-slot-operations"));
    }

    private void GivenClients(RoutingHandler handler)
    {
        Services.AddSingleton(new ConfirmedSlotsClient(NewHttpClient(handler)));
        Services.AddSingleton(new SlotsClient(NewHttpClient(handler)));
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.example.com") };

    private static SlotOperationDto Slot(Guid slotId, int activeBookings) => new(
        slotId,
        new DateOnly(2026, 9, 10),
        new TimeOnly(9, 0),
        new TimeOnly(13, 0),
        [new SlotOperationCapacityDto("DAT", 10, 9)],
        activeBookings);

    private static HttpResponseMessage OperationsJson(IReadOnlyList<SlotOperationDto> slots) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new SlotOperationsDto(slots)) };

    private static HttpResponseMessage Conflict(string detail) =>
        new(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new { title = "conflict", detail, status = 409 }),
        };

    /// <summary>Answers per requested path, in the order each path's responses were enqueued.</summary>
    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Queue<HttpResponseMessage>> _byPath = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        public void Enqueue(string absolutePath, HttpResponseMessage response)
        {
            if (!_byPath.TryGetValue(absolutePath, out var queue))
            {
                queue = new Queue<HttpResponseMessage>();
                _byPath[absolutePath] = queue;
            }

            queue.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            if (!_byPath.TryGetValue(path, out var queue) || queue.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No stubbed response queued for {request.Method} {path}.");
            }

            return Task.FromResult(queue.Dequeue());
        }
    }
}
`````

## tests/EventBooking.Web.Tests/DashboardsClientTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/DashboardsClientTests.cs","encoding":"utf8","sha256":"52a4d516d4a360d57137e2e27fe012dd01df97700fdda9431e8369235fd98fd9","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class DashboardsClientTests
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

    private sealed class TrackingResponseMessage(HttpStatusCode statusCode, TrackingContent content)
        : HttpResponseMessage(statusCode)
    {
        public bool WasDisposed { get; private set; }

        public bool WasDisposedAfterContentWasRead { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                WasDisposed = true;
                WasDisposedAfterContentWasRead = content.WasRead;
            }

            base.Dispose(disposing);
        }
    }

    private static (DashboardsClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new DashboardsClient(http), handler);
    }

    [Fact]
    public async Task TheThreeDashboardViewsAreFetchedFromTheirSingleRoute()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse();

        var outcome = await client.GetAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/api/dashboards", handler.Request.RequestUri!.AbsolutePath);
        Assert.Empty(outcome.Value!.AwaitingAvailability);
        Assert.Empty(outcome.Value.NoResponse);
        Assert.Empty(outcome.Value.Slots);
    }

    [Fact]
    public async Task TheDashboardResponseIsDisposedAfterItsBodyIsRead()
    {
        var (client, handler) = Given();
        var content = new TrackingContent("""{"awaitingAvailability":[],"noResponse":[],"slots":[]}""");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = new TrackingResponseMessage(HttpStatusCode.OK, content) { Content = content };
        handler.Response = response;

        await client.GetAsync(CancellationToken.None);

        Assert.True(response.WasDisposed);
        Assert.True(response.WasDisposedAfterContentWasRead);
    }

    private static HttpResponseMessage JsonResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            """{"awaitingAvailability":[],"noResponse":[],"slots":[]}""",
            Encoding.UTF8,
            "application/json"),
    };
}
`````

## tests/EventBooking.Web.Tests/DashboardsComponentTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/DashboardsComponentTests.cs","encoding":"utf8","sha256":"6df5bd269d9ba84ccefa48a17c516b5fddb468f32cfaf9b8150b64b3fe7ed0cb","parts":1,"part":1} -->

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

## tests/EventBooking.Web.Tests/EventBooking.Web.Tests.csproj — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/EventBooking.Web.Tests.csproj","encoding":"utf8","sha256":"2d53b16bebca151c884e494fabf3420ba2c94e65f74d53a76a764f1522440e77","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="bunit" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\EventBooking.Web\EventBooking.Web.csproj" />
    <ProjectReference Include="..\..\src\EventBooking.Domain\EventBooking.Domain.csproj" />
  </ItemGroup>

</Project>
`````
