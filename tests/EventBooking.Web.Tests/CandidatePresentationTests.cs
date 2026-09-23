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
