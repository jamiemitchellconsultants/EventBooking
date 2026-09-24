using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the browser client uses only the three minimum-data workspace routes.</summary>
public sealed class AppointmentsClientTests
{
    /// <summary>Verifies event-list retrieval and deserialization.</summary>
    [Fact]
    public async Task ListEventsGetsTheWorkspaceCollection()
    {
        var (client, handler) = Given(JsonContent.Create(new AppointmentWorkspaceEventListDto
        {
            AppointmentTypeName = "Uniform Fitting",
            Events = [],
        }));

        var result = await client.ListEventsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Uniform Fitting", result.Value!.AppointmentTypeName);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/api/appointment-workspace/events", handler.Request.RequestUri!.AbsolutePath);
    }

    /// <summary>Verifies selected-event retrieval targets only its route identifier.</summary>
    [Fact]
    public async Task GetEventUsesTheEventRoute()
    {
        var eventId = Guid.NewGuid();
        var (client, handler) = Given(JsonContent.Create(new AppointmentEventDetailDto
        {
            AppointmentTypeName = "Uniform Fitting",
            EventId = eventId,
            Date = new DateOnly(2026, 9, 7),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(13, 0),
            Appointments = [],
        }));

        Assert.True((await client.GetEventAsync(eventId, CancellationToken.None)).IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal(
            $"/api/appointment-workspace/events/{eventId}",
            handler.Request.RequestUri!.AbsolutePath);
    }

    /// <summary>Verifies updates send status and version without scope or attendee identifiers.</summary>
    [Fact]
    public async Task UpdateSendsOnlyStatusAndExpectedVersion()
    {
        var appointmentId = Guid.NewGuid();
        var (client, handler) = Given(JsonContent.Create(new BookingAppointmentUpdateDto
        {
            BookingAppointmentId = appointmentId,
            Status = "CheckedIn",
            CheckedInAt = DateTimeOffset.Parse("2026-09-07T08:05:00Z"),
            OutcomeAt = null,
            Version = 2,
        }));

        var result = await client.UpdateStatusAsync(
            appointmentId, "CheckedIn", 1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal(
            $"/api/appointment-workspace/appointments/{appointmentId}/status",
            handler.Request.RequestUri!.AbsolutePath);
        var body = await handler.Request.Content!.ReadAsStringAsync();
        Assert.Contains("\"status\":\"CheckedIn\"", body);
        Assert.Contains("\"expectedVersion\":1", body);
        Assert.DoesNotContain("appointmentType", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attendee", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bookingId", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies problem details preserve the appointment concurrency discriminator.</summary>
    [Fact]
    public async Task ConflictReturnsItsSafeDetail()
    {
        var (client, handler) = Given(new StringContent(
            """{"type":"appointment_version_conflict","title":"appointment_version_conflict","detail":"This appointment changed. Refresh and try again.","status":409}""",
            System.Text.Encoding.UTF8, "application/problem+json"));
        handler.StatusCode = HttpStatusCode.Conflict;

        var result = await client.UpdateStatusAsync(
            Guid.NewGuid(), "Completed", 2, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("appointment_version_conflict", result.ErrorCode);
        Assert.Equal("This appointment changed. Refresh and try again.", result.ErrorMessage);
    }


    /// <summary>Verifies the roster download returns raw CSV plus the server-suggested filename.</summary>
    [Fact]
    public async Task GetRosterReturnsCsvContentAndFilename()
    {
        var eventId = Guid.NewGuid();
        var csv = "Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At\n";
        var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = "roster-medical-check-up-2026-09-15-0930.csv",
        };
        var (client, handler) = Given(content);

        var result = await client.GetRosterAsync(eventId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(csv, result.Value!.Content);
        Assert.Equal("roster-medical-check-up-2026-09-15-0930.csv", result.Value.FileName);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal(
            $"/api/appointment-workspace/events/{eventId}/roster",
            handler.Request.RequestUri!.AbsolutePath);
    }

    /// <summary>Verifies a quoted filename is unwrapped rather than passed through with quotes.</summary>
    [Fact]
    public async Task GetRosterUnwrapsAQuotedFilename()
    {
        var content = new StringContent("Attendee Name\n", Encoding.UTF8, "text/csv");
        content.Headers.TryAddWithoutValidation(
            "Content-Disposition", "attachment; filename=\"roster-uniform-fitting-2026-09-15-0930.csv\"");
        var (client, _) = Given(content);

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("roster-uniform-fitting-2026-09-15-0930.csv", result.Value!.FileName);
    }

    /// <summary>Verifies the starred filename is preferred when the header carries both.</summary>
    [Fact]
    public async Task GetRosterPrefersTheStarredFilename()
    {
        var content = new StringContent("Attendee Name\n", Encoding.UTF8, "text/csv");
        content.Headers.TryAddWithoutValidation(
            "Content-Disposition",
            "attachment; filename=fallback.csv; filename*=UTF-8''roster-drug-%26-alcohol-testing-2026-09-15-0930.csv");
        var (client, _) = Given(content);

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("roster-drug-&-alcohol-testing-2026-09-15-0930.csv", result.Value!.FileName);
    }

    /// <summary>Verifies a response with no disposition header still yields a usable filename.</summary>
    [Fact]
    public async Task GetRosterFallsBackToADefaultFilename()
    {
        var (client, _) = Given(new StringContent("Attendee Name\n", Encoding.UTF8, "text/csv"));

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("roster.csv", result.Value!.FileName);
    }

    /// <summary>Verifies a refused download maps through the shared problem-details failure shape.</summary>
    [Fact]
    public async Task GetRosterMapsFailureThroughProblemDetails()
    {
        var handler = new StubHandler
        {
            Content = JsonContent.Create(new { type = "forbidden", title = "forbidden", detail = "You do not have permission to do that.", status = 403 }),
            StatusCode = HttpStatusCode.Forbidden,
        };
        var client = new AppointmentsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var result = await client.GetRosterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("You do not have permission to do that.", result.ErrorMessage);
    }

    private static (AppointmentsClient Client, StubHandler Handler) Given(HttpContent content)
    {
        var handler = new StubHandler { Content = content };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new AppointmentsClient(http), handler);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        /// <summary>Gets the last captured request.</summary>
        public HttpRequestMessage? Request { get; private set; }
        /// <summary>Gets the response content returned by the stub.</summary>
        public HttpContent Content { get; init; } = JsonContent.Create(new { });
        /// <summary>Gets or sets the response status returned by the stub.</summary>
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(StatusCode) { Content = Content });
        }
    }
}
