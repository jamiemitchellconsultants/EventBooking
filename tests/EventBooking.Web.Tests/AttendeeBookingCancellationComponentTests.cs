using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the coordinator booking cell: its summary, confirmations, and outcome copy.</summary>
public class AttendeeBookingCancellationComponentTests : BunitContext
{
    private static readonly Guid AttendeeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(object? value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Encoding.UTF8,
            "application/json"),
    };

    private static AttendeeBookingDto Booking(Guid id, bool isOriginal, int day) => new(
        id, isOriginal, new DateOnly(2026, 9, day), new TimeOnly(9, 0), new TimeOnly(13, 0));

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<Attendees> cut, string text) =>
        cut.FindAll("button").First(button => button.TextContent.Trim() == text);

    /// <summary>Renders the attendees page with a stubbed bookings list and cancel response.</summary>
    private (IRenderedComponent<Attendees> Cut, StubHandler Handler) RenderWith(
        Queue<IReadOnlyList<AttendeeBookingDto>> bookingPages,
        Func<HttpRequestMessage, HttpResponseMessage>? cancel = null)
    {
        this.AddAuthorization().SetAuthorized("Coordinator");
        StubHandler? handler = null;
        handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post
                && path.StartsWith($"/api/attendees/{AttendeeId}/bookings/", StringComparison.Ordinal))
            {
                return cancel is not null
                    ? cancel(request)
                    : Json(new CancelAttendeeBookingDto(false, 0, Guid.NewGuid()));
            }

            if (path == $"/api/attendees/{AttendeeId}/bookings")
            {
                return Json(bookingPages.Count > 0
                    ? bookingPages.Dequeue()
                    : Array.Empty<AttendeeBookingDto>());
            }

            if (path == "/api/attendees")
            {
                return Json(new AttendeeListDto(
                    [
                        new AttendeeDto(
                            AttendeeId, "Amara Novak", "a.novak@mail.com", "Booked",
                            "Booked", "MED", "Booked", [], null, "cursor"),
                    ],
                    null));
            }

            if (path == "/api/attendee-groups")
            {
                return Json(Array.Empty<object>());
            }

            return Json(new DashboardsDto(
                new DashboardTabDto<AwaitingRowDto>(0, []),
                new DashboardTabDto<NoResponseRowDto>(0, []),
                new DashboardTabDto<EventRowDto>(0, []),
                0,
                0));
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        Services.AddSingleton(new AttendeesClient(http));
        Services.AddSingleton(new DashboardsClient(http));
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));

        return (Render<Attendees>(), handler);
    }

    private static Queue<IReadOnlyList<AttendeeBookingDto>> Pages(
        params IReadOnlyList<AttendeeBookingDto>[] pages) => new(pages);

    [Fact]
    public void BookingCellReportsNoActiveBookings()
    {
        var (cut, _) = RenderWith(Pages([]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Contains("No active bookings", cut.Markup));
        Assert.Empty(cut.FindAll("ul.booking-list li"));
    }

    [Fact]
    public void BookingCellSummarizesOneActiveBooking()
    {
        var (cut, _) = RenderWith(Pages([Booking(Guid.NewGuid(), true, 10)]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));
        Assert.Contains("1 active booking", cut.Markup);
        Assert.Contains("10 Sep 2026", cut.Markup);
    }

    [Fact]
    public void BookingCellSummarizesTwoActiveBookings()
    {
        var (cut, _) = RenderWith(Pages(
        [
            Booking(Guid.NewGuid(), true, 10),
            Booking(Guid.NewGuid(), false, 12),
        ]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("ul.booking-list li").Count));
        Assert.Contains("2 active bookings", cut.Markup);
    }

    [Fact]
    public void EveryRowOffersCancelOnly()
    {
        var (cut, _) = RenderWith(Pages(
        [
            Booking(Guid.NewGuid(), true, 10),
            Booking(Guid.NewGuid(), false, 12),
        ]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("ul.booking-list li").Count));
        var rows = cut.FindAll("ul.booking-list li");
        Assert.All(rows, row =>
        {
            Assert.Contains(row.QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel booking");
            Assert.DoesNotContain(
                row.QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel & rebook");
        });
        Assert.Contains("(recovery)", rows[1].TextContent);
    }

    [Fact]
    public void CancelBookingRequiresAConfirmingSecondClick()
    {
        var bookingId = Guid.NewGuid();
        var (cut, handler) = RenderWith(Pages([Booking(bookingId, true, 10)], []));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel booking").Click();

        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);

        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() => Assert.Contains("Booking cancelled.", cut.Markup));
        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal(
            $"/api/attendees/{AttendeeId}/bookings/{bookingId}/cancel",
            post.RequestUri!.AbsolutePath);
        Assert.Equal("?confirm=true", post.RequestUri!.Query);
    }

    [Fact]
    public void AFailedCancellationShowsTheSafeMessage()
    {
        var bookingId = Guid.NewGuid();
        var (cut, _) = RenderWith(
            Pages([Booking(bookingId, false, 12)]),
            cancel: _ => new HttpResponseMessage(HttpStatusCode.Conflict));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel booking").Click();
        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("span.booking-error[role=alert]")));
    }
}
