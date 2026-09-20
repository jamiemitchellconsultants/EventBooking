# 00b — Vocabulary edits 112 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Web.Tests/AttendeesClientTests.cs — 1/1

<!-- vocabulary-file: {"id":380,"oldPath":"tests/EventBooking.Web.Tests/CandidatesClientTests.cs","newPath":"tests/EventBooking.Web.Tests/AttendeesClientTests.cs","beforeSha":"d4a02fa912836c2c63c4f877a92f48b35ff430342694ec87a02d7eaab8dacb49","afterSha":"2a4f831503afc97ebd02fa48ca02ed50c8cab236c37577bc3ccb2d3cc5399d27","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class AttendeesClientTests
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

    private static (AttendeesClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new AttendeesClient(http), handler);
    }

    [Fact]
    public async Task ListingWithoutASearchAsksForEveryAttendee()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<AttendeeDto>()),
        };

        await client.ListAsync(null, null, CancellationToken.None);

        Assert.Equal("/api/attendees", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task ASearchTermIsUrlEncodedIntoTheQueryString()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<AttendeeDto>()),
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
            Content = JsonContent.Create(new List<AttendeeDto>()),
        };

        await client.ListAsync(null, null, CancellationToken.None);

        Assert.Equal("/api/attendees", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task FilteringByStatusIncludesTheStatusValueInTheQueryString()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<AttendeeDto>()),
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
            Content = JsonContent.Create(new List<AttendeeDto>()),
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
            Content = JsonContent.Create(new List<AttendeeGroupOptionDto>()),
        };

        await client.ListGroupsAsync(CancellationToken.None);

        Assert.Equal("/api/attendee-groups", handler.Requests[0].RequestUri!.AbsolutePath);
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
        Assert.Equal($"/api/attendees/{id}", handler.Requests[0].RequestUri!.AbsolutePath);
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
        Assert.Equal($"/api/attendees/{id}", handler.Requests[0].RequestUri!.AbsolutePath);
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
        var csv = "name,email,attendee_group\nAmara Novak,a.novak@mail.com,PILOTS";

        var outcome = await client.ImportAsync(csv, CancellationToken.None);

        Assert.True(outcome.Value!.Accepted);
        Assert.Equal("/api/attendees/import", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(csv, handler.Bodies[0]);
    }

    [Fact]
    public async Task ARejectedImportCarriesEveryRowError()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ImportOutcomeDto(
                false, 0, [new ImportErrorDto(2, "Name is required."), new ImportErrorDto(4, "XYZ is not a known attendee group code.")])),
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

        Assert.Equal($"/api/attendees/{id}/invite", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task StartingRecoveryPostsToTheRecoveryRoute()
    {
        var (client, handler) = Given();
        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { inviteId, appointmentTypeIds = new[] { typeId }, emailSent = true }),
        };

        var outcome = await client.StartRecoveryAsync(attendeeId, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/attendees/{attendeeId}/recovery-invites",
            handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(inviteId, outcome.Value!.InviteId);
        Assert.Equal([typeId], outcome.Value.AppointmentTypeIds);
        Assert.True(outcome.Value.EmailSent);
    }

    [Fact]
    public async Task CancellingRecoveryDeletesTheRecoveryRoute()
    {
        var (client, handler) = Given();
        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

        var outcome = await client.CancelRecoveryAsync(attendeeId, inviteId, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/attendees/{attendeeId}/recovery-invites/{inviteId}",
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
        var startClient = new AttendeesClient(
            new HttpClient(new BlockingHandler()) { BaseAddress = new Uri("https://api.example.com") });
        var cancelClient = new AttendeesClient(
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
        var listContent = new TrackingContent(JsonSerializer.Serialize(new List<AttendeeDto>()));
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
        await client.ImportAsync("name,email,attendee_group", CancellationToken.None);
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
        var attendeeId = Guid.NewGuid();
        handler.Responses.Enqueue(Json("[]"));
        var client = NewAttendeesClient(handler);

        var outcome = await client.GetBookingsAsync(attendeeId, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Empty(outcome.Value!);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal($"/api/attendees/{attendeeId}/bookings", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetBookingsReadsAnOriginalAndARecoveryRow()
    {
        var handler = new StubHandler();
        var originalId = Guid.NewGuid();
        var recoveryId = Guid.NewGuid();
        handler.Responses.Enqueue(Json($$"""
            [
              {"bookingId":"{{originalId}}","isOriginal":true,"eventDate":"2026-09-10",
               "eventStartTime":"09:00:00","eventEndTime":"13:00:00"},
              {"bookingId":"{{recoveryId}}","isOriginal":false,"eventDate":"2026-09-12",
               "eventStartTime":"13:00:00","eventEndTime":"17:00:00"}
            ]
            """));
        var client = NewAttendeesClient(handler);

        var outcome = await client.GetBookingsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(2, outcome.Value!.Count);
        Assert.Equal(originalId, outcome.Value[0].BookingId);
        Assert.True(outcome.Value[0].IsOriginal);
        Assert.Equal(new DateOnly(2026, 9, 10), outcome.Value[0].EventDate);
        Assert.Equal(new TimeOnly(13, 0), outcome.Value[0].EventEndTime);
        Assert.False(outcome.Value[1].IsOriginal);
        Assert.Equal(recoveryId, outcome.Value[1].BookingId);
    }

    [Fact]
    public async Task CancelBookingPostsTheRebookFlagAndReadsTheOutcome()
    {
        var handler = new StubHandler();
        var attendeeId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        handler.Responses.Enqueue(Json(
            """{"reinvited":false,"inviteCreated":false,"deliveryStatus":"Unavailable","deliveryId":null}"""));
        var client = NewAttendeesClient(handler);

        var outcome = await client.CancelBookingAsync(
            attendeeId, bookingId, rebook: false, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.Value!.Reinvited);
        Assert.Equal("Unavailable", outcome.Value.DeliveryStatus);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel",
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
        var client = NewAttendeesClient(handler);

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
        var client = NewAttendeesClient(handler);

        var outcome = await client.CancelBookingAsync(
            Guid.NewGuid(), Guid.NewGuid(), rebook: true, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, outcome.StatusCode);
    }

    private static AttendeesClient NewAttendeesClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
`````

## before — tests/EventBooking.Web.Tests/ConfirmedSlotsClientTests.cs — 1/1

<!-- vocabulary-file: {"id":381,"oldPath":"tests/EventBooking.Web.Tests/ConfirmedSlotsClientTests.cs","newPath":"tests/EventBooking.Web.Tests/EventOperationsClientTests.cs","beforeSha":"89bd4cfcbec66c8aace0f21bcbb44614869ebc8e024e0af597efb1b2e116228d","afterSha":"9cf1052e8d8f7d257bb519529a2a04dd8aa21edfaed9e3a6976885bcd59e55a1","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Web.Tests/EventOperationsClientTests.cs — 1/1

<!-- vocabulary-file: {"id":381,"oldPath":"tests/EventBooking.Web.Tests/ConfirmedSlotsClientTests.cs","newPath":"tests/EventBooking.Web.Tests/EventOperationsClientTests.cs","beforeSha":"89bd4cfcbec66c8aace0f21bcbb44614869ebc8e024e0af597efb1b2e116228d","afterSha":"9cf1052e8d8f7d257bb519529a2a04dd8aa21edfaed9e3a6976885bcd59e55a1","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class EventOperationsClientTests
{
    [Fact]
    public async Task ImportPostsTheCsvToTheNeutralRoute()
    {
        var handler = new RecordingHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new EventImportOutcomeDto(true, 2, [])),
            },
        };
        var client = new EventOperationsClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        });

        var result = await client.ImportAsync(
            "date,startTime,DAT,MED,UNI\n2026-09-10,09:00,10,6,8",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ImportedCount);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/api/events/import", handler.Request.RequestUri!.AbsolutePath);
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

## before — tests/EventBooking.Web.Tests/ConfirmedSlotsPageTests.cs — 1/1

<!-- vocabulary-file: {"id":382,"oldPath":"tests/EventBooking.Web.Tests/ConfirmedSlotsPageTests.cs","newPath":"tests/EventBooking.Web.Tests/EventsPageTests.cs","beforeSha":"0b030fa51d5796aa825d06a99e707f0c5e3bf2ec4f3206dae015d58150a66791","afterSha":"e7c6924c759a8c85cf7899652108d950ee32121da81dc7fcc9d5a9ff863bc2fb","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Web.Tests/EventsPageTests.cs — 1/1

<!-- vocabulary-file: {"id":382,"oldPath":"tests/EventBooking.Web.Tests/ConfirmedSlotsPageTests.cs","newPath":"tests/EventBooking.Web.Tests/EventsPageTests.cs","beforeSha":"0b030fa51d5796aa825d06a99e707f0c5e3bf2ec4f3206dae015d58150a66791","afterSha":"e7c6924c759a8c85cf7899652108d950ee32121da81dc7fcc9d5a9ff863bc2fb","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class EventsPageTests : BunitContext
{
    [Fact]
    public async Task AcceptedImportShowsCountAndUsesNoAttendeeClient()
    {
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", OperationsJson([]));
        handler.Enqueue("/api/events/import", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new EventImportOutcomeDto(true, 4, [])),
        });
        GivenClients(handler);
        var cut = Render<EventOperations>();

        await cut.InvokeAsync(() => cut.Instance.ImportCsvForTestingAsync(
            "date,startTime,DAT,MED,UNI\n2026-09-10,09:00,10,6,8"));

        Assert.Contains("4 events imported", cut.Markup);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath == "/api/events/import");
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.AbsolutePath.StartsWith("/api/attendees", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RejectedImportShowsEveryRowError()
    {
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", OperationsJson([]));
        handler.Enqueue("/api/events/import", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new EventImportOutcomeDto(
                false,
                0,
                [new EventImportErrorDto(3, "DAT must be a positive integer.")])),
        });
        GivenClients(handler);
        var cut = Render<EventOperations>();

        await cut.InvokeAsync(() => cut.Instance.ImportCsvForTestingAsync("bad"));

        Assert.Contains("Nothing was imported", cut.Markup);
        Assert.Contains("Line 3", cut.Markup);
        Assert.Contains("DAT must be a positive integer", cut.Markup);
    }

    [Fact]
    public void RendersEventSectionWithCancelControls()
    {
        var eventId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", OperationsJson([Event(eventId, activeBookings: 1)]));
        GivenClients(handler);

        var cut = Render<EventOperations>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#event-operations tbody tr")));
        Assert.Contains("2026-09-10", cut.Markup);
        Assert.Contains("Cancel event", cut.Markup);
        Assert.Contains("DAT 9/10", cut.Markup);
    }

    [Fact]
    public async Task CancelWithNoBookingsSucceedsOnTheFirstClick()
    {
        var eventId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", OperationsJson([Event(eventId, activeBookings: 0)]));
        handler.Enqueue($"/api/events/{eventId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/events/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<EventOperations>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#event-operations tbody tr")));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("No events yet", cut.Markup));
        var delete = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Delete);
        Assert.Contains("confirm=false", delete.RequestUri!.Query);
    }

    [Fact]
    public async Task CancelWithBookingsRequiresASecondConfirmClick()
    {
        var eventId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", OperationsJson([Event(eventId, activeBookings: 6)]));
        handler.Enqueue($"/api/events/{eventId}", Conflict(
            "Cancelling this event will cancel 6 confirmed bookings."));
        handler.Enqueue($"/api/events/{eventId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/events/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<EventOperations>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#event-operations tbody tr")));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        Assert.Contains("6 confirmed bookings", cut.Find("[role=alert]").TextContent);
        Assert.Contains("Press Confirm cancel to proceed", cut.Find("[role=alert]").TextContent);
        Assert.Single(cut.FindAll("#event-operations tbody tr"));

        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());

        cut.WaitForAssertion(() => Assert.Contains("No events yet", cut.Markup));
        var deletes = handler.Requests.Where(r => r.Method == HttpMethod.Delete).ToList();
        Assert.Equal(2, deletes.Count);
        Assert.Contains("confirm=false", deletes[0].RequestUri!.Query);
        Assert.Contains("confirm=true", deletes[1].RequestUri!.Query);
    }

    [Fact]
    public async Task TheEventSectionNeverCallsDashboards()
    {
        var eventId = Guid.NewGuid();
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", OperationsJson([Event(eventId, activeBookings: 0)]));
        handler.Enqueue($"/api/events/{eventId}", new HttpResponseMessage(HttpStatusCode.NoContent));
        handler.Enqueue("/api/events/operations", OperationsJson([]));
        GivenClients(handler);

        var cut = Render<EventOperations>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#event-operations tbody tr")));
        await cut.InvokeAsync(() => cut.Find("button.button-danger").Click());
        cut.WaitForAssertion(() => Assert.Contains("No events yet", cut.Markup));

        Assert.DoesNotContain(
            handler.Requests,
            r => r.RequestUri!.AbsolutePath.StartsWith("/api/dashboards", StringComparison.Ordinal));
    }

    [Fact]
    public void AForbiddenEventLoadShowsTheSafeMessage()
    {
        var handler = new RoutingHandler();
        handler.Enqueue("/api/events/operations", new HttpResponseMessage(HttpStatusCode.Forbidden));
        GivenClients(handler);

        var cut = Render<EventOperations>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("p.banner.error[role=alert]")));
        Assert.Empty(cut.FindAll("#event-operations"));
    }

    private void GivenClients(RoutingHandler handler)
    {
        Services.AddSingleton(new EventOperationsClient(NewHttpClient(handler)));
        Services.AddSingleton(new EventsClient(NewHttpClient(handler)));
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.example.com") };

    private static EventOperationDto Event(Guid eventId, int activeBookings) => new(
        eventId,
        new DateOnly(2026, 9, 10),
        new TimeOnly(9, 0),
        new TimeOnly(13, 0),
        [new EventOperationCapacityDto("DAT", 10, 9)],
        activeBookings);

    private static HttpResponseMessage OperationsJson(IReadOnlyList<EventOperationDto> events) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new EventOperationsDto(events)) };

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

## before — tests/EventBooking.Web.Tests/DashboardsClientTests.cs — 1/1

<!-- vocabulary-file: {"id":383,"oldPath":"tests/EventBooking.Web.Tests/DashboardsClientTests.cs","newPath":"tests/EventBooking.Web.Tests/DashboardsClientTests.cs","beforeSha":"52a4d516d4a360d57137e2e27fe012dd01df97700fdda9431e8369235fd98fd9","afterSha":"3740b3d1cc946c116cd2d6fe1a07fdb916c109c79c2c03f76d601565ef42e1ed","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Web.Tests/DashboardsClientTests.cs — 1/1

<!-- vocabulary-file: {"id":383,"oldPath":"tests/EventBooking.Web.Tests/DashboardsClientTests.cs","newPath":"tests/EventBooking.Web.Tests/DashboardsClientTests.cs","beforeSha":"52a4d516d4a360d57137e2e27fe012dd01df97700fdda9431e8369235fd98fd9","afterSha":"3740b3d1cc946c116cd2d6fe1a07fdb916c109c79c2c03f76d601565ef42e1ed","side":"after","part":1,"parts":1} -->

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
        Assert.Empty(outcome.Value.Events);
    }

    [Fact]
    public async Task TheDashboardResponseIsDisposedAfterItsBodyIsRead()
    {
        var (client, handler) = Given();
        var content = new TrackingContent("""{"awaitingAvailability":[],"noResponse":[],"events":[]}""");
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
            """{"awaitingAvailability":[],"noResponse":[],"events":[]}""",
            Encoding.UTF8,
            "application/json"),
    };
}
`````
