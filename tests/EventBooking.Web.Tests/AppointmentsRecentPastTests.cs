using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the appointment page groups recently past slots without changing row behavior.</summary>
public sealed class AppointmentsRecentPastTests : BunitContext
{
    private static readonly DateTimeOffset OperationalNow =
        new(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid PastSlotId =
        Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid CurrentSlotId =
        Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Verifies the selector separates recently past slots from current ones.</summary>
    [Fact]
    public void SlotSelectorGroupsRecentPastSeparately()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(MixedSlotList()));
        handler.Enqueue(Ok(CurrentDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
        {
            var groups = cut.FindAll("select[name=confirmed-slot] optgroup");
            Assert.Equal(2, groups.Count);
            Assert.Equal("Recent past", groups[0].GetAttribute("label"));
            Assert.Equal("Current and upcoming", groups[1].GetAttribute("label"));
            Assert.Contains("2026-09-06", groups[0].TextContent);
            Assert.Contains("2026-09-07", groups[1].TextContent);
        });
    }

    /// <summary>Verifies the page opens on the nearest current slot when past slots come first.</summary>
    [Fact]
    public void DefaultsToNearestCurrentSlot()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(MixedSlotList()));
        handler.Enqueue(Ok(CurrentDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith(
            $"/api/appointment-workspace/slots/{CurrentSlotId}",
            handler.Requests[1].Path,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Monday, 07 Sep 2026", cut.Markup);
    }

    /// <summary>Verifies the page falls back to the most recent past slot when nothing is current.</summary>
    [Fact]
    public void FallsBackToMostRecentPastSlot()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(PastOnlySlotList()));
        handler.Enqueue(Ok(PastDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() => Assert.Contains("Past Candidate", cut.Markup));
        Assert.Contains("Sunday, 06 Sep 2026", cut.Markup);
    }

    /// <summary>Verifies a past Expected row offers no-show but no check-in.</summary>
    [Fact]
    public void PastSlotDisablesCheckInAndEnablesNoShow()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(PastOnlySlotList()));
        handler.Enqueue(Ok(PastDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("button[data-action=check-in]").HasAttribute("disabled"));
            Assert.False(cut.Find("button[data-action=no-show]").HasAttribute("disabled"));
            Assert.Contains("Check-in opens on the confirmed-slot date.", cut.Markup);
        });
    }

    private RoutedHandler GivenClient()
    {
        var handler = new RoutedHandler();
        Services.AddSingleton(new AppointmentsClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));
        Services.AddSingleton(new HeadOfficePageClock(
            "Europe/London", () => OperationalNow));
        return handler;
    }

    private static AppointmentWorkspaceSlotListDto MixedSlotList() => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Slots =
        [
            Summary(PastSlotId, new DateOnly(2026, 9, 6)),
            Summary(CurrentSlotId, new DateOnly(2026, 9, 7)),
        ],
    };

    private static AppointmentWorkspaceSlotListDto PastOnlySlotList() => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Slots = [Summary(PastSlotId, new DateOnly(2026, 9, 6))],
    };

    private static AppointmentSlotSummaryDto Summary(Guid slotId, DateOnly date) => new()
    {
        ConfirmedSlotId = slotId,
        Date = date,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Counts = new AppointmentStatusCountsDto
        {
            Expected = 1, CheckedIn = 0, Completed = 0, NoShow = 0,
        },
    };

    private static AppointmentSlotDetailDto CurrentDetail(string status) =>
        Detail(CurrentSlotId, new DateOnly(2026, 9, 7), "Alex Morgan", "alex@example.com", status);

    private static AppointmentSlotDetailDto PastDetail(string status) =>
        Detail(PastSlotId, new DateOnly(2026, 9, 6), "Past Candidate", "past@example.com", status);

    private static AppointmentSlotDetailDto Detail(
        Guid slotId, DateOnly date, string name, string email, string status) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        ConfirmedSlotId = slotId,
        Date = date,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Appointments =
        [
            new BookingAppointmentRowDto
            {
                BookingAppointmentId = Guid.NewGuid(),
                CandidateName = name, CandidateEmail = email,
                Status = status, CheckedInAt = null,
                OutcomeAt = null, Version = 1,
            },
        ],
    };

    private static HttpResponseMessage Ok<T>(T body) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(body, options: CamelCase),
    };

    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _responses = new();
        /// <summary>Gets the requests captured in sending order.</summary>
        public List<(HttpMethod Method, string Path, string Body)> Requests { get; } = [];
        /// <summary>Queues one response for the next request.</summary>
        public void Enqueue(HttpResponseMessage response) =>
            _responses.Enqueue(_ => Task.FromResult(response));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            return await _responses.Dequeue()(cancellationToken);
        }
    }
}
