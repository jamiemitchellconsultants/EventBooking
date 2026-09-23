using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies candidate pages describe durable email outcomes without false promises.</summary>
public class RepairDNotificationComponentTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Booking confirmation failure still displays the usable manage-link recovery.</summary>
    [Fact]
    public void BookPageUsesNeutralCopyWhenConfirmationDeliveryFails()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["DAT"],
            [new InviteOptionDto(
                Guid.NewGuid(),
                new DateOnly(2030, 1, 14),
                new TimeOnly(9, 0),
                new TimeOnly(13, 0),
                "Monday 14 Jan 2030, 09:00-13:00")] )));
        handler.Enqueue(_ => Json(new ConfirmedBookingDto(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "fresh-manage-token",
            "Failed")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
        cut.WaitForAssertion(() => Assert.Contains("Confirm this time", cut.Markup));
        cut.Find("input[type=radio]").Change(true);
        cut.Find("button.booking-page__button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm email delivery", cut.Markup);
            Assert.Contains("fresh-manage-token", cut.Markup);
            Assert.DoesNotContain("is on its way", cut.Markup);
        });
    }

    /// <summary>The confirmed page names the head office the API sent, the same one the confirmation email uses.</summary>
    [Fact]
    public void BookPageShowsTheHeadOfficeAddressReturnedByTheApi()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["DAT"],
            [new InviteOptionDto(
                Guid.NewGuid(),
                new DateOnly(2030, 1, 14),
                new TimeOnly(9, 0),
                new TimeOnly(13, 0),
                "Monday 14 Jan 2030, 09:00-13:00")])));
        handler.Enqueue(_ => Json(new ConfirmedBookingDto(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "fresh-manage-token",
            "Sent",
            "2 Api Street, London")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
        cut.WaitForAssertion(() => Assert.Contains("Confirm this time", cut.Markup));
        cut.Find("input[type=radio]").Change(true);
        cut.Find("button.booking-page__button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Head office: 2 Api Street, London", cut.Markup));
    }

    /// <summary>Booking confirmation pending delivery also uses neutral recovery copy.</summary>
    [Fact]
    public void BookPageUsesNeutralCopyWhenConfirmationDeliveryIsPending()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["DAT"],
            [new InviteOptionDto(
                Guid.NewGuid(),
                new DateOnly(2030, 1, 14),
                new TimeOnly(9, 0),
                new TimeOnly(13, 0),
                "Monday 14 Jan 2030, 09:00-13:00")])));
        handler.Enqueue(_ => Json(new ConfirmedBookingDto(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "fresh-manage-token",
            "Pending")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
        cut.WaitForAssertion(() => Assert.Contains("Confirm this time", cut.Markup));
        cut.Find("input[type=radio]").Change(true);
        cut.Find("button.booking-page__button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm email delivery", cut.Markup);
            Assert.Contains("fresh-manage-token", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
        });
    }

    /// <summary>Cancel/rebook failure displays neutral recovery rather than sent wording.</summary>
    [Fact]
    public void ManagePageUsesNeutralCopyWhenReplacementDeliveryFails()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: true,
            InviteCreated: true,
            DeliveryStatus: "Failed",
            DeliveryId: Guid.NewGuid())));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm delivery", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
            Assert.DoesNotContain("on its way", cut.Markup);
        });
    }

    /// <summary>Cancel/rebook pending delivery displays neutral recovery guidance.</summary>
    [Fact]
    public void ManagePageUsesNeutralCopyWhenReplacementDeliveryIsPending()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: true,
            InviteCreated: true,
            DeliveryStatus: "Pending",
            DeliveryId: Guid.NewGuid())));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("could not confirm delivery", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
            Assert.DoesNotContain("on its way", cut.Markup);
        });
    }

    /// <summary>A missing replacement invite explains that the team will arrange the next time.</summary>
    [Fact]
    public void ManagePageDistinguishesUnavailableReplacementInvite()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: false,
            InviteCreated: false,
            DeliveryStatus: "Unavailable")));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("There are no times available right now", cut.Markup);
            Assert.DoesNotContain("could not confirm delivery", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
        });
    }

    /// <summary>A sent replacement invite is the only state that promises delivery.</summary>
    [Fact]
    public void ManagePagePromisesReplacementDeliveryOnlyWhenSent()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new BookingDto(
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00",
            "Amara Novak")));
        handler.Enqueue(_ => Json(new CancelOutcomeDto(
            Reinvited: true,
            InviteCreated: true,
            DeliveryStatus: "Sent",
            DeliveryId: Guid.NewGuid())));
        Services.AddSingleton(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("new invitation with fresh times has been sent", cut.Markup);
            Assert.DoesNotContain("could not confirm delivery", cut.Markup);
        });
    }

    /// <summary>A stale failed delivery remains visible but offers no enabled resend action.</summary>
    [Fact]
    public void CandidatesHideResendWhenServerMarksTheLatestContextStale()
    {
        var candidateId = Guid.NewGuid();
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new List<CandidateDto>
        {
            new(candidateId, "Amara Novak", "a.novak@mail.com", null, null, null, false, [new("DAT", "Drug & Alcohol Testing")], 1, "Not yet invited"),
        }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Slots: [],
            EmailStatuses:
            [new CandidateEmailStatusDto(
                candidateId,
                "Booking confirmation",
                new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero),
                "Failed",
                CanRetry: false)])));
        handler.Enqueue(_ => Json(new List<EmployeeGroupOptionDto>()));
        Services.AddSingleton(new CandidatesClient(NewHttpClient(handler)));
        Services.AddSingleton(new DashboardsClient(NewHttpClient(handler)));
        Services.AddSingleton(new AuditClient(NewHttpClient(handler)));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        var cut = Render<Candidates>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Booking confirmation", cut.Markup);
            Assert.DoesNotContain(">Resend<", cut.Markup);
        });
    }

    /// <summary>Actionable failed and pending deliveries use the retry endpoint, not the invite endpoint.</summary>
    [Theory]
    [InlineData("Failed")]
    [InlineData("Pending")]
    public void CandidatesResendActionUsesTheTemplateAwareRetryEndpoint(string status)
    {
        var candidateId = Guid.NewGuid();
        var candidate = new CandidateDto(
            candidateId, "Amara Novak", "a.novak@mail.com", null, null, null, false, [new("DAT", "Drug & Alcohol Testing")], 1, "Not yet invited");
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new List<CandidateDto> { candidate }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Slots: [],
            EmailStatuses:
            [new CandidateEmailStatusDto(
                candidateId,
                "Booking confirmation",
                new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero),
                status,
                CanRetry: true)])));
        handler.Enqueue(_ => Json(new List<EmployeeGroupOptionDto>()));
        handler.Enqueue(_ => Json(new EmailRetryDto("Sent", Guid.NewGuid())));
        handler.Enqueue(_ => Json(new List<CandidateDto> { candidate }));
        handler.Enqueue(_ => Json(new DashboardsDto(
            AwaitingAvailability: [],
            NoResponse: [],
            Slots: [],
            EmailStatuses:
            [new CandidateEmailStatusDto(
                candidateId,
                "Booking confirmation",
                new DateTimeOffset(2026, 9, 7, 10, 1, 0, TimeSpan.Zero),
                "Sent",
                CanRetry: false)])));
        handler.Enqueue(_ => Json(new List<EmployeeGroupOptionDto>()));
        Services.AddSingleton(new CandidatesClient(NewHttpClient(handler)));
        Services.AddSingleton(new DashboardsClient(NewHttpClient(handler)));
        Services.AddSingleton(new AuditClient(NewHttpClient(handler)));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        var cut = Render<Candidates>();
        cut.WaitForAssertion(() => Assert.Contains(">Resend<", cut.Markup));

        cut.FindAll("button").Single(button => button.TextContent == "Resend").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                handler.Requests,
                request => request.Method == HttpMethod.Post
                    && request.Path == $"/api/candidates/{candidateId}/email-retry");
            Assert.DoesNotContain(
                handler.Requests,
                request => request.Method == HttpMethod.Post
                    && request.Path == $"/api/candidates/{candidateId}/invite");
        });
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://api.example.com"),
    };

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value, options: CamelCase),
    };

    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = [];

        /// <summary>Records the method and path of every handled request.</summary>
        public List<(HttpMethod Method, string Path)> Requests { get; } = [];

        /// <summary>Queues one deterministic response for the next page request.</summary>
        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri?.AbsolutePath ?? string.Empty));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No HTTP response was configured.");
            }

            return Task.FromResult(_responses.Dequeue()(request));
        }
    }
}
