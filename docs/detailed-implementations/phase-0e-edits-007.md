# 00e — Require an attendee group, edits 7 (Task 3c)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs — 1/1

<!-- retirement-file: {"id":19,"file":"tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs","beforeSha":"53ac047434a11e50743e2124a7e37110bb66e507fd3ec2ebe3f130a0ed70062a","afterSha":"5b5777156e9fe807ce2eef1119981b4aa7cbc0532aa0d5c20869b473a3c74024","side":"after","part":1,"parts":1} -->

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
                        AttendeeId, "Amara Novak", "a.novak@mail.com", Guid.NewGuid(), "MED", "Medical",
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

## before — tests/EventBooking.Web.Tests/AttendeePresentationTests.cs — 1/1

<!-- retirement-file: {"id":20,"file":"tests/EventBooking.Web.Tests/AttendeePresentationTests.cs","beforeSha":"2d571a0178d2f2dd058f48dcc9566c9a16ce607fae18b1f07a5ac78713959b3c","afterSha":"78313cf9bfd668cbab8dc14121fb00f93e3a1a289f574a20b86543f97e1a576f","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Web.Tests/AttendeePresentationTests.cs — 1/1

<!-- retirement-file: {"id":20,"file":"tests/EventBooking.Web.Tests/AttendeePresentationTests.cs","beforeSha":"2d571a0178d2f2dd058f48dcc9566c9a16ce607fae18b1f07a5ac78713959b3c","afterSha":"78313cf9bfd668cbab8dc14121fb00f93e3a1a289f574a20b86543f97e1a576f","side":"after","part":1,"parts":1} -->

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
                        attendeeId, "Amara Novak", "a.novak@mail.com", Guid.NewGuid(), "MED", "Medical",
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

## before — tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs — 1/1

<!-- retirement-file: {"id":21,"file":"tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs","beforeSha":"f282156cf95fa14ce4130c5357466aeb64f5c6970c8c082cff8765612a6b10a5","afterSha":"7cf66e0e9488334b746972473fddbe9831ad017ee70c0a98e6f8d2987a52359e","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs — 1/1

<!-- retirement-file: {"id":21,"file":"tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs","beforeSha":"f282156cf95fa14ce4130c5357466aeb64f5c6970c8c082cff8765612a6b10a5","afterSha":"7cf66e0e9488334b746972473fddbe9831ad017ee70c0a98e6f8d2987a52359e","side":"after","part":1,"parts":1} -->

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
                        attendeeId, "Amara Novak", "a.novak@mail.com", Guid.NewGuid(), "MED", "Medical",
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
