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
            Content = JsonContent.Create(new PageDto<AttendeeDto>([], null)),
        };

        await client.ListAsync(null, null, null, null, null, CancellationToken.None);

        Assert.Equal("/api/attendees", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task ASearchTermIsUrlEncodedIntoTheQueryString()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PageDto<AttendeeDto>([], null)),
        };

        await client.ListAsync(null, null, null, "a novak@mail.com", null, CancellationToken.None);

        Assert.Contains("search=a%20novak%40mail.com", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task ListingWithoutAStatusOmitsTheStatusParameter()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PageDto<AttendeeDto>([], null)),
        };

        await client.ListAsync(null, null, null, null, null, CancellationToken.None);

        Assert.Equal("/api/attendees", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task FilteringByStatusIncludesTheStatusValueInTheQueryString()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PageDto<AttendeeDto>([], null)),
        };

        await client.ListAsync("AwaitingAvailability", null, null, null, null, CancellationToken.None);

        Assert.Contains("status=AwaitingAvailability", handler.Requests[0].RequestUri!.Query);
        Assert.DoesNotContain("search=", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task StatusAndSearchCombineIntoOneQueryString()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PageDto<AttendeeDto>([], null)),
        };

        await client.ListAsync("NoResponseNeedsFollowUp", null, null, "a novak@mail.com", null, CancellationToken.None);

        var query = handler.Requests[0].RequestUri!.Query;
        Assert.Contains("status=NoResponseNeedsFollowUp", query);
        Assert.Contains("search=a%20novak%40mail.com", query);
    }

    [Fact]
    public async Task ReferenceDataFansOutToMeLocationsGroupsAndTypes()
    {
        var (client, handler) = Given();
        var groupId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeDto(
                ["Coordinator"], null, null) with
            {
                Links = new Dictionary<string, ApiLink>
                {
                    ["createAttendee"] = new("/api/attendees", "POST", "createAttendee"),
                },
            }),
        });
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PageDto<LocationDto>(
                [new LocationDto(Guid.NewGuid(), "LON", "London HQ", "1 Example St",
                    "Europe/London", true, 1, new Dictionary<string, ApiLink>())], null)),
        });
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PageDto<AttendeeGroupDto>(
                [new AttendeeGroupDto(groupId, "FIELD", "Field staff", "", true, 1,
                    [typeId], 3, new Dictionary<string, ApiLink>())], null)),
        });
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new PageDto<AppointmentTypeDto>(
                [new AppointmentTypeDto(typeId, "MED", "Medical check", true, 1,
                    true, "M. Manager", new Dictionary<string, ApiLink>())], null)),
        });

        var outcome = await client.GetReferenceDataAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(
            ["/api/me", "/api/locations", "/api/attendee-groups", "/api/appointment-types"],
            handler.Requests.Select(request => request.RequestUri!.AbsolutePath));
        Assert.Equal("London HQ", outcome.Value!.Locations[0].Name);
        Assert.Equal("Field staff", outcome.Value.Groups.Single(group => group.Id == groupId).Name);
        Assert.Equal([typeId], outcome.Value.Groups[0].RequirementTypeIds);
        Assert.Equal("MED", outcome.Value.AppointmentTypes.Single(type => type.Id == typeId).Code);
        Assert.True(outcome.Value.CollectionLinks.ContainsKey("createAttendee"));
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

        await client.CreateAsync("Amara Novak", "a.novak@mail.com", groupId,
            IdempotencySubmission.Start(), CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("Amara Novak", handler.Bodies[0]);
        Assert.Contains(groupId.ToString(), handler.Bodies[0]);
        Assert.DoesNotContain("AppointmentTypeIds", handler.Bodies[0]);
        Assert.True(handler.Requests[0].Headers.Contains("Idempotency-Key"));
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
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var outcome = await client.ImportAsync(stream, "attendees.csv",
            IdempotencySubmission.Start(), CancellationToken.None);

        Assert.True(outcome.Value!.Accepted);
        Assert.Equal("/api/attendees/import", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains(csv, handler.Bodies[0]);
        Assert.Contains("attendees.csv", handler.Bodies[0]);
        Assert.StartsWith("multipart/form-data",
            handler.Requests[0].Content!.Headers.ContentType!.MediaType);
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

        using var stream = new MemoryStream("anything"u8.ToArray());

        var outcome = await client.ImportAsync(stream, "bad.csv",
            IdempotencySubmission.Start(), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.Value!.Accepted);
        Assert.Equal(2, outcome.Value.Errors.Count);
        Assert.Equal([2, 4], outcome.Value.Errors.Select(e => e.LineNumber));
    }

    [Fact]
    public async Task InvitingPostsTheChosenLocationsToTheInvitesRoute()
    {
        var (client, handler) = Given();
        var id = Guid.NewGuid();
        var inviteId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new InviteOutcomeDto(inviteId, "Invited")),
        };

        var outcome = await client.InviteAsync(id, [first, second],
            IdempotencySubmission.Start(), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(inviteId, outcome.Value!.InviteId);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal($"/api/attendees/{id}/invites", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains(first.ToString(), handler.Bodies[0]);
        Assert.Contains(second.ToString(), handler.Bodies[0]);
        Assert.True(handler.Requests[0].Headers.Contains("Idempotency-Key"));
    }

    [Fact]
    public async Task InvitingWithoutLocationsSendsANullSelection()
    {
        var (client, handler) = Given();
        var id = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new InviteOutcomeDto(Guid.NewGuid(), "Invited")),
        };

        await client.InviteAsync(id, [], IdempotencySubmission.Start(), CancellationToken.None);

        Assert.Contains("null", handler.Bodies[0]);
    }

    [Fact]
    public async Task CountingEligibleEventsRepeatsOneLocationParameterPerLocation()
    {
        var (client, handler) = Given();
        var id = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new EligibleEventCountDto(2, 3)),
        };

        var outcome = await client.CountEligibleAsync(id, [first, second], CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(2, outcome.Value!.Count);
        Assert.Equal(3, outcome.Value.RequiredOptionCount);
        Assert.Equal($"/api/attendees/{id}/eligible-event-count",
            handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains($"locationIds={first:D}", handler.Requests[0].RequestUri!.Query);
        Assert.Contains($"locationIds={second:D}", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task RetryingEmailPostsWithoutABody()
    {
        var (client, handler) = Given();
        var id = Guid.NewGuid();
        var emailLogId = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new EmailRetryDto(emailLogId)),
        };

        var outcome = await client.RetryEmailAsync(id, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(emailLogId, outcome.Value!.EmailLogId);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal($"/api/attendees/{id}/email-retry", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task ReadingReadinessGetsTheReadinessRoute()
    {
        var (client, handler) = Given();
        var attendeeId = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new AttendeeReadinessDto(
                attendeeId, "AppointmentsOutstanding", "Appointments outstanding",
                [new OutstandingAppointmentTypeDto("MED", "Medical check", true)],
                new Dictionary<string, ApiLink>())),
        };

        var outcome = await client.GetReadinessAsync(attendeeId, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("AppointmentsOutstanding", outcome.Value!.Code);
        Assert.Equal($"/api/attendees/{attendeeId}/readiness",
            handler.Requests[0].RequestUri!.AbsolutePath);
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
            Content = JsonContent.Create(new
            {
                recoveryInviteId = inviteId,
                locationIds = new[] { Guid.NewGuid() },
                recoverableTypeIds = new[] { typeId },
            }),
        };

        var outcome = await client.StartRecoveryAsync(attendeeId, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/attendees/{attendeeId}/recovery-invites",
            handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(inviteId, outcome.Value!.RecoveryInviteId);
        Assert.Equal([typeId], outcome.Value.RecoverableTypeIds);
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
        var listContent = new TrackingContent(JsonSerializer.Serialize(new PageDto<AttendeeDto>([], null)));
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
        await client.ListAsync(null, null, null, null, null, CancellationToken.None);
        await client.CreateAsync("Amara Novak", "a.novak@mail.com", null,
            IdempotencySubmission.Start(), CancellationToken.None);
        await client.UpdateAsync(id, "Amara Novak", "a.novak@mail.com", null, CancellationToken.None);
        await client.DeleteAsync(id, false, CancellationToken.None);
        using var import = new MemoryStream("name,email,attendee_group"u8.ToArray());
        await client.ImportAsync(import, "attendees.csv",
            IdempotencySubmission.Start(), CancellationToken.None);
        await client.InviteAsync(id, [], IdempotencySubmission.Start(), CancellationToken.None);

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
        handler.Responses.Enqueue(Json("""{"items":[],"nextCursor":null}"""));
        var client = NewAttendeesClient(handler);

        var outcome = await client.GetBookingsAsync(attendeeId, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Empty(outcome.Value!.Items);
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
            {"items":[
              {"bookingId":"{{originalId}}","isOriginal":true,"eventDate":"2026-09-10",
               "eventStartTime":"09:00:00","eventEndTime":"13:00:00"},
              {"bookingId":"{{recoveryId}}","isOriginal":false,"eventDate":"2026-09-12",
               "eventStartTime":"13:00:00","eventEndTime":"17:00:00"}
            ],"nextCursor":null}
            """));
        var client = NewAttendeesClient(handler);

        var outcome = await client.GetBookingsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(2, outcome.Value!.Items.Count);
        Assert.Equal(originalId, outcome.Value.Items[0].BookingId);
        Assert.True(outcome.Value.Items[0].IsOriginal);
        Assert.Equal(new DateOnly(2026, 9, 10), outcome.Value.Items[0].EventDate);
        Assert.Equal(new TimeOnly(13, 0), outcome.Value.Items[0].EventEndTime);
        Assert.False(outcome.Value.Items[1].IsOriginal);
        Assert.Equal(recoveryId, outcome.Value.Items[1].BookingId);
    }

    [Fact]
    public async Task CancelBookingPostsTheConfirmFlagAndReadsTheOutcome()
    {
        var handler = new StubHandler();
        var attendeeId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var cancelled = Guid.NewGuid();
        handler.Responses.Enqueue(Json(
            $$"""{"confirmationRequired":false,"activeBookingCount":0,"cancelledBookingId":"{{cancelled}}"}"""));
        var client = NewAttendeesClient(handler);

        var outcome = await client.CancelBookingAsync(
            attendeeId, bookingId, confirm: true, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.Value!.ConfirmationRequired);
        Assert.Equal(cancelled, outcome.Value.CancelledBookingId);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel",
            handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("?confirm=true", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task CancelBookingPreviewReadsTheConsequence()
    {
        var handler = new StubHandler();
        handler.Responses.Enqueue(Json(
            """{"confirmationRequired":true,"activeBookingCount":2,"cancelledBookingId":null}"""));
        var client = NewAttendeesClient(handler);

        var outcome = await client.CancelBookingAsync(
            Guid.NewGuid(), Guid.NewGuid(), confirm: false, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.True(outcome.Value!.ConfirmationRequired);
        Assert.Equal(2, outcome.Value.ActiveBookingCount);
        Assert.Equal("?confirm=false", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task CancelBookingSurfacesAConflictAsAFailure()
    {
        var handler = new StubHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Conflict));
        var client = NewAttendeesClient(handler);

        var outcome = await client.CancelBookingAsync(
            Guid.NewGuid(), Guid.NewGuid(), confirm: true, CancellationToken.None);

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
