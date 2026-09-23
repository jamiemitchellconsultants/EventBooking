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
