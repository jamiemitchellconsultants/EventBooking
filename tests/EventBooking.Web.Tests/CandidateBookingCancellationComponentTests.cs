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
public class CandidateBookingCancellationComponentTests : BunitContext
{
    private static readonly Guid CandidateId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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

    private static CandidateBookingDto Booking(Guid id, bool isOriginal, int day) => new(
        id, isOriginal, new DateOnly(2026, 9, day), new TimeOnly(9, 0), new TimeOnly(13, 0));

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<Candidates> cut, string text) =>
        cut.FindAll("button").First(button => button.TextContent.Trim() == text);

    /// <summary>Renders the candidates page with a stubbed bookings list and cancel response.</summary>
    private (IRenderedComponent<Candidates> Cut, StubHandler Handler) RenderWith(
        Queue<IReadOnlyList<CandidateBookingDto>> bookingPages,
        Func<HttpRequestMessage, HttpResponseMessage>? cancel = null)
    {
        this.AddAuthorization().SetAuthorized("Coordinator");
        StubHandler? handler = null;
        handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post
                && path.StartsWith($"/api/candidates/{CandidateId}/bookings/", StringComparison.Ordinal))
            {
                return cancel is not null
                    ? cancel(request)
                    : Json(new CancelCandidateBookingDto(false, false, "Unavailable", null));
            }

            if (path == $"/api/candidates/{CandidateId}/bookings")
            {
                return Json(bookingPages.Count > 0
                    ? bookingPages.Dequeue()
                    : Array.Empty<CandidateBookingDto>());
            }

            if (path == "/api/candidates")
            {
                return Json(new[]
                {
                    new CandidateDto(
                        CandidateId, "Amara Novak", "a.novak@mail.com", null, null, null, false,
                        [], 4, "Booked"),
                });
            }

            if (path == "/api/employee-groups")
            {
                return Json(Array.Empty<object>());
            }

            return Json(new DashboardsDto([], [], [], []));
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        Services.AddSingleton(new CandidatesClient(http));
        Services.AddSingleton(new DashboardsClient(http));
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        return (Render<Candidates>(), handler);
    }

    private static Queue<IReadOnlyList<CandidateBookingDto>> Pages(
        params IReadOnlyList<CandidateBookingDto>[] pages) => new(pages);

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
    public void RecoveryRowOmitsCancelAndRebook()
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
        Assert.Contains(rows[0].QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel & rebook");
        Assert.DoesNotContain(rows[1].QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel & rebook");
        Assert.Contains(rows[1].QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel booking");
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
            $"/api/candidates/{CandidateId}/bookings/{bookingId}/cancel",
            post.RequestUri!.AbsolutePath);
    }

    [Fact]
    public void CancelAndRebookRequiresAConfirmingSecondClick()
    {
        var bookingId = Guid.NewGuid();
        var (cut, handler) = RenderWith(
            Pages([Booking(bookingId, true, 10)], []),
            cancel: _ => Json(new CancelCandidateBookingDto(true, true, "Sent", Guid.NewGuid())));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel & rebook").Click();

        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
        // Arming rebook must not arm the plain cancel on the same row.
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Cancel booking");

        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Booking cancelled; replacement invite sent.", cut.Markup));
    }

    [Fact]
    public void SuccessMessageReportsAnUndeliveredReplacementInvite()
    {
        var bookingId = Guid.NewGuid();
        var (cut, _) = RenderWith(
            Pages([Booking(bookingId, true, 10)], []),
            cancel: _ => Json(new CancelCandidateBookingDto(true, true, "Failed", Guid.NewGuid())));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel & rebook").Click();
        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("replacement invite could not be delivered", cut.Markup));
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
