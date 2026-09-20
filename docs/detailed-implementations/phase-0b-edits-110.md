# 00b — Vocabulary edits 110 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Web.Tests/CandidateBookingCancellationComponentTests.cs — 1/1

<!-- vocabulary-file: {"id":376,"oldPath":"tests/EventBooking.Web.Tests/CandidateBookingCancellationComponentTests.cs","newPath":"tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs","beforeSha":"0b6a2e1bc631a125db5c4ae871f8e40b6aa6db6708cbc3bfd5d8d63c5dc8a4a8","afterSha":"53ac047434a11e50743e2124a7e37110bb66e507fd3ec2ebe3f130a0ed70062a","side":"before","part":1,"parts":1} -->

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
`````

## after — tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs — 1/1

<!-- vocabulary-file: {"id":376,"oldPath":"tests/EventBooking.Web.Tests/CandidateBookingCancellationComponentTests.cs","newPath":"tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs","beforeSha":"0b6a2e1bc631a125db5c4ae871f8e40b6aa6db6708cbc3bfd5d8d63c5dc8a4a8","afterSha":"53ac047434a11e50743e2124a7e37110bb66e507fd3ec2ebe3f130a0ed70062a","side":"after","part":1,"parts":1} -->

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
                    : Json(new CancelAttendeeBookingDto(false, false, "Unavailable", null));
            }

            if (path == $"/api/attendees/{AttendeeId}/bookings")
            {
                return Json(bookingPages.Count > 0
                    ? bookingPages.Dequeue()
                    : Array.Empty<AttendeeBookingDto>());
            }

            if (path == "/api/attendees")
            {
                return Json(new[]
                {
                    new AttendeeDto(
                        AttendeeId, "Amara Novak", "a.novak@mail.com", null, null, null, false,
                        [], 4, "Booked"),
                });
            }

            if (path == "/api/attendee-groups")
            {
                return Json(Array.Empty<object>());
            }

            return Json(new DashboardsDto([], [], [], []));
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
            $"/api/attendees/{AttendeeId}/bookings/{bookingId}/cancel",
            post.RequestUri!.AbsolutePath);
    }

    [Fact]
    public void CancelAndRebookRequiresAConfirmingSecondClick()
    {
        var bookingId = Guid.NewGuid();
        var (cut, handler) = RenderWith(
            Pages([Booking(bookingId, true, 10)], []),
            cancel: _ => Json(new CancelAttendeeBookingDto(true, true, "Sent", Guid.NewGuid())));
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
            cancel: _ => Json(new CancelAttendeeBookingDto(true, true, "Failed", Guid.NewGuid())));
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
`````

## before — tests/EventBooking.Web.Tests/CandidateLayoutTests.cs — 1/1

<!-- vocabulary-file: {"id":377,"oldPath":"tests/EventBooking.Web.Tests/CandidateLayoutTests.cs","newPath":"tests/EventBooking.Web.Tests/AttendeeLayoutTests.cs","beforeSha":"9e993f431e35e983835bf1d2177455d5b63a068cb2f26ba022cef8d52698acf2","afterSha":"b3f6b8a6ded1b72a06d6ea3c83a71c98c2d0b79a96a4a1d9c81d986f70781e2e","side":"before","part":1,"parts":1} -->

`````csharp
using System.Reflection;
using Bunit;
using EventBooking.Web.Layout;
using EventBooking.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Tests;

/// <summary>
/// Regression coverage for the anonymous candidate shell, which must not expose staff sign-in controls.
/// </summary>
public class CandidateLayoutTests : BunitContext
{
    /// <summary>
    /// Verifies that both candidate routes compile with the dedicated anonymous layout.
    /// </summary>
    [Fact]
    public void CandidatePagesCompileWithTheCandidateLayout()
    {
        Assert.Equal(typeof(CandidateLayout), GetLayoutType(typeof(Book)));
        Assert.Equal(typeof(CandidateLayout), GetLayoutType(typeof(ManageBooking)));
    }

    /// <summary>
    /// Verifies that the candidate shell retains branding and its body without staff authentication controls.
    /// </summary>
    [Fact]
    public void AnonymousCandidateLayoutShowsBrandAndBodyWithoutAuthenticationControls()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderLayout();

        Assert.Equal("EventBooking", cut.Find("header.topbar a.brand").TextContent.Trim());
        Assert.Equal("/", cut.Find("header.topbar a.brand").GetAttribute("href"));
        Assert.Contains("Candidate content", cut.Markup);
        Assert.DoesNotContain("authentication/login", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authentication/logout", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign in", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign out", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static Type? GetLayoutType(Type pageType) =>
        pageType.GetCustomAttribute<LayoutAttribute>()?.LayoutType;

    private IRenderedComponent<CascadingAuthenticationState> RenderLayout()
    {
        RenderFragment layout = builder =>
        {
            builder.OpenComponent<CandidateLayout>(0);
            builder.AddAttribute(1, "Body", (RenderFragment)(body => body.AddContent(0, "Candidate content")));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(layout));
    }
}
`````

## after — tests/EventBooking.Web.Tests/AttendeeLayoutTests.cs — 1/1

<!-- vocabulary-file: {"id":377,"oldPath":"tests/EventBooking.Web.Tests/CandidateLayoutTests.cs","newPath":"tests/EventBooking.Web.Tests/AttendeeLayoutTests.cs","beforeSha":"9e993f431e35e983835bf1d2177455d5b63a068cb2f26ba022cef8d52698acf2","afterSha":"b3f6b8a6ded1b72a06d6ea3c83a71c98c2d0b79a96a4a1d9c81d986f70781e2e","side":"after","part":1,"parts":1} -->

`````csharp
using System.Reflection;
using Bunit;
using EventBooking.Web.Layout;
using EventBooking.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EventBooking.Web.Tests;

/// <summary>
/// Regression coverage for the anonymous attendee shell, which must not expose staff sign-in controls.
/// </summary>
public class AttendeeLayoutTests : BunitContext
{
    /// <summary>
    /// Verifies that both attendee routes compile with the dedicated anonymous layout.
    /// </summary>
    [Fact]
    public void AttendeePagesCompileWithTheAttendeeLayout()
    {
        Assert.Equal(typeof(AttendeeLayout), GetLayoutType(typeof(Book)));
        Assert.Equal(typeof(AttendeeLayout), GetLayoutType(typeof(ManageBooking)));
    }

    /// <summary>
    /// Verifies that the attendee shell retains branding and its body without staff authentication controls.
    /// </summary>
    [Fact]
    public void AnonymousAttendeeLayoutShowsBrandAndBodyWithoutAuthenticationControls()
    {
        this.AddAuthorization().SetNotAuthorized();

        var cut = RenderLayout();

        Assert.Equal("EventBooking", cut.Find("header.topbar a.brand").TextContent.Trim());
        Assert.Equal("/", cut.Find("header.topbar a.brand").GetAttribute("href"));
        Assert.Contains("Attendee content", cut.Markup);
        Assert.DoesNotContain("authentication/login", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authentication/logout", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign in", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sign out", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static Type? GetLayoutType(Type pageType) =>
        pageType.GetCustomAttribute<LayoutAttribute>()?.LayoutType;

    private IRenderedComponent<CascadingAuthenticationState> RenderLayout()
    {
        RenderFragment layout = builder =>
        {
            builder.OpenComponent<AttendeeLayout>(0);
            builder.AddAttribute(1, "Body", (RenderFragment)(body => body.AddContent(0, "Attendee content")));
            builder.CloseComponent();
        };

        return Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent(layout));
    }
}
`````

## before — tests/EventBooking.Web.Tests/CandidatePresentationTests.cs — 1/1

<!-- vocabulary-file: {"id":378,"oldPath":"tests/EventBooking.Web.Tests/CandidatePresentationTests.cs","newPath":"tests/EventBooking.Web.Tests/AttendeePresentationTests.cs","beforeSha":"7058ea58827b469df32ed5cfcfdc9f93c914266d3c926ee9f0f095bfadbf419f","afterSha":"2d571a0178d2f2dd058f48dcc9566c9a16ce607fae18b1f07a5ac78713959b3c","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Domain.Candidates;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class CandidatePresentationTests : BunitContext
{
    [Theory]
    [InlineData(CandidateStatus.NotYetInvited, "status-new")]
    [InlineData(CandidateStatus.AwaitingAvailability, "status-warning")]
    [InlineData(CandidateStatus.Invited, "status-neutral")]
    [InlineData(CandidateStatus.Booked, "status-success")]
    [InlineData(CandidateStatus.NoResponseNeedsFollowUp, "status-warning")]
    public void EachCanonicalCandidateStatusGetsItsWireframeStyle(
        CandidateStatus status,
        string expectedCssClass)
    {
        Assert.Equal(expectedCssClass, CandidatePresentation.StatusCssClass((int)status));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void UnknownRawStatusValuesAreNeutral(int rawStatus)
    {
        Assert.Equal("status-neutral", CandidatePresentation.StatusCssClass(rawStatus));
    }

    [Fact]
    public async Task ABusyPageIgnoresANewFileEventBeforeReadingItsFile()
    {
        var page = new Candidates { IsBusyForTesting = true };
        var noFileEvent = new InputFileChangeEventArgs([]);

        await page.OnFileChosenAsync(noFileEvent);

        Assert.True(page.IsBusyForTesting);
        Assert.Null(page.ErrorForTesting);
    }

    [Theory]
    [InlineData("Ready", "status-success")]
    [InlineData("EmployeeGroupUnassigned", "status-warning")]
    [InlineData("NoActiveBooking", "status-neutral")]
    [InlineData("RequirementSnapshotMismatch", "status-warning")]
    [InlineData("AppointmentsOutstanding", "status-warning")]
    [InlineData("UnknownFutureCode", "status-neutral")]
    public void EachReadinessCodeGetsItsBadgeStyle(string code, string expectedCssClass)
    {
        Assert.Equal(expectedCssClass, CandidatePresentation.ReadinessCssClass(code));
    }

    [Theory]
    [InlineData("Ready", "✓")]
    [InlineData("EmployeeGroupUnassigned", "!")]
    [InlineData("NoActiveBooking", "○")]
    [InlineData("RequirementSnapshotMismatch", "≠")]
    [InlineData("AppointmentsOutstanding", "•")]
    [InlineData("UnknownFutureCode", "?")]
    public void EachReadinessCodeGetsItsBadgeGlyph(string code, string expectedIcon)
    {
        Assert.Equal(expectedIcon, CandidatePresentation.ReadinessIcon(code));
    }

    [Theory]
    [InlineData("Ready", "status-success", "✓", "Ready to book")]
    [InlineData("EmployeeGroupUnassigned", "status-warning", "!", "Needs employee group")]
    [InlineData("NoActiveBooking", "status-neutral", "○", "No active booking")]
    [InlineData("RequirementSnapshotMismatch", "status-warning", "≠", "Requirements changed")]
    [InlineData("AppointmentsOutstanding", "status-warning", "•", "Appointments outstanding")]
    public void EachReadinessCodeRendersBadgeTextAndStyle(
        string code, string cssClass, string icon, string display)
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(candidateId, _ => Task.FromResult(ReadinessJson(
            new CandidateReadinessDto(candidateId, code, display, []))));

        cut.Find("button.readiness-badge").Click();

        cut.WaitForAssertion(() =>
        {
            var badge = cut.Find("button.readiness-badge");
            Assert.Contains(cssClass, badge.ClassList);
            Assert.Contains(icon, badge.TextContent);
            Assert.Contains(display, badge.TextContent);
            Assert.Equal("true", badge.GetAttribute("aria-expanded"));
        });
        Assert.Contains(display, cut.Find("div.readiness-detail").TextContent);
    }

    [Fact]
    public void OutstandingTypesRenderAsAListWithRecoverability()
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(candidateId, _ => Task.FromResult(ReadinessJson(
            new CandidateReadinessDto(
                candidateId,
                "AppointmentsOutstanding",
                "Appointments outstanding",
                [
                    new OutstandingAppointmentTypeDto("DAT", "Drug & Alcohol Testing", false),
                    new OutstandingAppointmentTypeDto("MED", "Medical Check-Up", true),
                ]))));

        cut.Find("button.readiness-badge").Click();

        cut.WaitForAssertion(() =>
        {
            var items = cut.FindAll("ul.readiness-types li");
            Assert.Equal(2, items.Count);
            Assert.Contains("Drug & Alcohol Testing", items[0].TextContent);
            Assert.Contains("Medical Check-Up (recoverable)", items[1].TextContent);
        });
    }

    [Fact]
    public void ReadinessLoadingAnnouncesPolitely()
    {
        var candidateId = Guid.NewGuid();
        var gate = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = RenderCandidates(candidateId, _ => gate.Task);

        cut.Find("button.readiness-badge").Click();

        cut.WaitForAssertion(() =>
        {
            var loading = cut.Find("span.readiness-loading");
            Assert.Equal("polite", loading.GetAttribute("aria-live"));
            Assert.Contains("Loading readiness", loading.TextContent);
        });

        gate.SetResult(ReadinessJson(
            new CandidateReadinessDto(candidateId, "Ready", "Ready to book", [])));
        cut.WaitForAssertion(() =>
            Assert.Contains("Ready to book", cut.Find("button.readiness-badge").TextContent));
    }

    [Fact]
    public void ReadinessFailureIsRetryable()
    {
        var candidateId = Guid.NewGuid();
        var attempts = 0;
        var cut = RenderCandidates(candidateId, _ =>
        {
            attempts++;
            return Task.FromResult(attempts == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : ReadinessJson(
                    new CandidateReadinessDto(candidateId, "Ready", "Ready to book", [])));
        });

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("span.readiness-error[role=alert]")));
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Try again").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Ready to book", cut.Find("button.readiness-badge").TextContent));
    }

    [Fact]
    public void ReadinessBadgeIsAKeyboardOperableButton()
    {
        var candidateId = Guid.NewGuid();
        var cut = RenderCandidates(candidateId, _ => Task.FromResult(ReadinessJson(
            new CandidateReadinessDto(candidateId, "Ready", "Ready to book", []))));

        var badge = cut.Find("button.readiness-badge");

        Assert.Equal("BUTTON", badge.TagName);
        Assert.Null(badge.GetAttribute("tabindex"));
        Assert.Equal("false", badge.GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task ABusyPageIgnoresReadinessToggle()
    {
        var page = new Candidates { IsBusyForTesting = true };
        var candidateId = Guid.NewGuid();

        await page.ToggleReadinessForTestingAsync(candidateId);

        Assert.True(page.IsBusyForTesting);
        Assert.False(page.IsReadinessExpandedForTesting(candidateId));
        Assert.Null(page.ReadinessForTesting(candidateId));
        Assert.Null(page.ReadinessErrorForTesting(candidateId));
    }

    private IRenderedComponent<Candidates> RenderCandidates(
        Guid candidateId,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        this.AddAuthorization().SetAuthorized("Coordinator");
        var stub = new StubHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/readiness", StringComparison.Ordinal))
            {
                return await respond(request);
            }

            if (path == "/api/candidates")
            {
                return Json(new[]
                {
                    new CandidateDto(
                        candidateId, "Amara Novak", "a.novak@mail.com", null, null, null, false,
                        [], 1, "Not yet invited"),
                });
            }

            if (path == "/api/employee-groups")
            {
                return Json(Array.Empty<object>());
            }

            return Json(new DashboardsDto([], [], [], []));
        });
        Services.AddSingleton(
            new CandidatesClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(
            new DashboardsClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(
            new AuditClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        return Render<Candidates>();
    }

    private static HttpResponseMessage ReadinessJson(CandidateReadinessDto dto) => Json(dto);

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
}
`````

## after — tests/EventBooking.Web.Tests/AttendeePresentationTests.cs — 1/1

<!-- vocabulary-file: {"id":378,"oldPath":"tests/EventBooking.Web.Tests/CandidatePresentationTests.cs","newPath":"tests/EventBooking.Web.Tests/AttendeePresentationTests.cs","beforeSha":"7058ea58827b469df32ed5cfcfdc9f93c914266d3c926ee9f0f095bfadbf419f","afterSha":"2d571a0178d2f2dd058f48dcc9566c9a16ce607fae18b1f07a5ac78713959b3c","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Domain.Attendees;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class AttendeePresentationTests : BunitContext
{
    [Theory]
    [InlineData(AttendeeStatus.NotYetInvited, "status-new")]
    [InlineData(AttendeeStatus.AwaitingAvailability, "status-warning")]
    [InlineData(AttendeeStatus.Invited, "status-neutral")]
    [InlineData(AttendeeStatus.Booked, "status-success")]
    [InlineData(AttendeeStatus.NoResponseNeedsFollowUp, "status-warning")]
    public void EachCanonicalAttendeeStatusGetsItsWireframeStyle(
        AttendeeStatus status,
        string expectedCssClass)
    {
        Assert.Equal(expectedCssClass, AttendeePresentation.StatusCssClass((int)status));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void UnknownRawStatusValuesAreNeutral(int rawStatus)
    {
        Assert.Equal("status-neutral", AttendeePresentation.StatusCssClass(rawStatus));
    }

    [Fact]
    public async Task ABusyPageIgnoresANewFileEventBeforeReadingItsFile()
    {
        var page = new Attendees { IsBusyForTesting = true };
        var noFileEvent = new InputFileChangeEventArgs([]);

        await page.OnFileChosenAsync(noFileEvent);

        Assert.True(page.IsBusyForTesting);
        Assert.Null(page.ErrorForTesting);
    }

    [Theory]
    [InlineData("Ready", "status-success")]
    [InlineData("AttendeeGroupUnassigned", "status-warning")]
    [InlineData("NoActiveBooking", "status-neutral")]
    [InlineData("RequirementSnapshotMismatch", "status-warning")]
    [InlineData("AppointmentsOutstanding", "status-warning")]
    [InlineData("UnknownFutureCode", "status-neutral")]
    public void EachReadinessCodeGetsItsBadgeStyle(string code, string expectedCssClass)
    {
        Assert.Equal(expectedCssClass, AttendeePresentation.ReadinessCssClass(code));
    }

    [Theory]
    [InlineData("Ready", "✓")]
    [InlineData("AttendeeGroupUnassigned", "!")]
    [InlineData("NoActiveBooking", "○")]
    [InlineData("RequirementSnapshotMismatch", "≠")]
    [InlineData("AppointmentsOutstanding", "•")]
    [InlineData("UnknownFutureCode", "?")]
    public void EachReadinessCodeGetsItsBadgeGlyph(string code, string expectedIcon)
    {
        Assert.Equal(expectedIcon, AttendeePresentation.ReadinessIcon(code));
    }

    [Theory]
    [InlineData("Ready", "status-success", "✓", "Ready to book")]
    [InlineData("AttendeeGroupUnassigned", "status-warning", "!", "Needs attendee group")]
    [InlineData("NoActiveBooking", "status-neutral", "○", "No active booking")]
    [InlineData("RequirementSnapshotMismatch", "status-warning", "≠", "Requirements changed")]
    [InlineData("AppointmentsOutstanding", "status-warning", "•", "Appointments outstanding")]
    public void EachReadinessCodeRendersBadgeTextAndStyle(
        string code, string cssClass, string icon, string display)
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(attendeeId, _ => Task.FromResult(ReadinessJson(
            new AttendeeReadinessDto(attendeeId, code, display, []))));

        cut.Find("button.readiness-badge").Click();

        cut.WaitForAssertion(() =>
        {
            var badge = cut.Find("button.readiness-badge");
            Assert.Contains(cssClass, badge.ClassList);
            Assert.Contains(icon, badge.TextContent);
            Assert.Contains(display, badge.TextContent);
            Assert.Equal("true", badge.GetAttribute("aria-expanded"));
        });
        Assert.Contains(display, cut.Find("div.readiness-detail").TextContent);
    }

    [Fact]
    public void OutstandingTypesRenderAsAListWithRecoverability()
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(attendeeId, _ => Task.FromResult(ReadinessJson(
            new AttendeeReadinessDto(
                attendeeId,
                "AppointmentsOutstanding",
                "Appointments outstanding",
                [
                    new OutstandingAppointmentTypeDto("DAT", "Drug & Alcohol Testing", false),
                    new OutstandingAppointmentTypeDto("MED", "Medical Check-Up", true),
                ]))));

        cut.Find("button.readiness-badge").Click();

        cut.WaitForAssertion(() =>
        {
            var items = cut.FindAll("ul.readiness-types li");
            Assert.Equal(2, items.Count);
            Assert.Contains("Drug & Alcohol Testing", items[0].TextContent);
            Assert.Contains("Medical Check-Up (recoverable)", items[1].TextContent);
        });
    }

    [Fact]
    public void ReadinessLoadingAnnouncesPolitely()
    {
        var attendeeId = Guid.NewGuid();
        var gate = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = RenderAttendees(attendeeId, _ => gate.Task);

        cut.Find("button.readiness-badge").Click();

        cut.WaitForAssertion(() =>
        {
            var loading = cut.Find("span.readiness-loading");
            Assert.Equal("polite", loading.GetAttribute("aria-live"));
            Assert.Contains("Loading readiness", loading.TextContent);
        });

        gate.SetResult(ReadinessJson(
            new AttendeeReadinessDto(attendeeId, "Ready", "Ready to book", [])));
        cut.WaitForAssertion(() =>
            Assert.Contains("Ready to book", cut.Find("button.readiness-badge").TextContent));
    }

    [Fact]
    public void ReadinessFailureIsRetryable()
    {
        var attendeeId = Guid.NewGuid();
        var attempts = 0;
        var cut = RenderAttendees(attendeeId, _ =>
        {
            attempts++;
            return Task.FromResult(attempts == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : ReadinessJson(
                    new AttendeeReadinessDto(attendeeId, "Ready", "Ready to book", [])));
        });

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("span.readiness-error[role=alert]")));
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Try again").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Ready to book", cut.Find("button.readiness-badge").TextContent));
    }

    [Fact]
    public void ReadinessBadgeIsAKeyboardOperableButton()
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(attendeeId, _ => Task.FromResult(ReadinessJson(
            new AttendeeReadinessDto(attendeeId, "Ready", "Ready to book", []))));

        var badge = cut.Find("button.readiness-badge");

        Assert.Equal("BUTTON", badge.TagName);
        Assert.Null(badge.GetAttribute("tabindex"));
        Assert.Equal("false", badge.GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task ABusyPageIgnoresReadinessToggle()
    {
        var page = new Attendees { IsBusyForTesting = true };
        var attendeeId = Guid.NewGuid();

        await page.ToggleReadinessForTestingAsync(attendeeId);

        Assert.True(page.IsBusyForTesting);
        Assert.False(page.IsReadinessExpandedForTesting(attendeeId));
        Assert.Null(page.ReadinessForTesting(attendeeId));
        Assert.Null(page.ReadinessErrorForTesting(attendeeId));
    }

    private IRenderedComponent<Attendees> RenderAttendees(
        Guid attendeeId,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        this.AddAuthorization().SetAuthorized("Coordinator");
        var stub = new StubHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/readiness", StringComparison.Ordinal))
            {
                return await respond(request);
            }

            if (path == "/api/attendees")
            {
                return Json(new[]
                {
                    new AttendeeDto(
                        attendeeId, "Amara Novak", "a.novak@mail.com", null, null, null, false,
                        [], 1, "Not yet invited"),
                });
            }

            if (path == "/api/attendee-groups")
            {
                return Json(Array.Empty<object>());
            }

            return Json(new DashboardsDto([], [], [], []));
        });
        Services.AddSingleton(
            new AttendeesClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(
            new DashboardsClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(
            new AuditClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));

        return Render<Attendees>();
    }

    private static HttpResponseMessage ReadinessJson(AttendeeReadinessDto dto) => Json(dto);

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
}
`````
