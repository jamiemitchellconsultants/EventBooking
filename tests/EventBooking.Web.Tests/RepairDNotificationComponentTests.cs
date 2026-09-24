using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies attendee pages describe durable email outcomes without false promises.</summary>
public class RepairDNotificationComponentTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Booking confirmation shows the chosen window without promising delivery.</summary>
    [Fact]
    public void BookPageShowsTheChosenWindowWithoutPromisingDelivery()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["DAT"],
            [new InviteOptionDto(
                Guid.NewGuid(),
                "London HQ",
                "1 Example St",
                TestContractFactory.EventTimeAt(new DateOnly(2030, 1, 14), new TimeOnly(9, 0)))],
            false,
            new Dictionary<string, ApiLink>())));
        handler.Enqueue(_ => Json(new ConfirmBookingOutcomeDto(
            Guid.NewGuid(),
            "fresh-manage-token")));
        Services.AddSingleton<IBookingClient>(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        var cut = Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
        cut.WaitForAssertion(() => Assert.Contains("Confirm this time", cut.Markup));
        cut.Find("input[type=radio]").Change(true);
        cut.Find("button.booking-page__button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("confirmation email will follow shortly", cut.Markup);
            Assert.Contains("fresh-manage-token", cut.Markup);
            Assert.Contains("14 Jan 2030", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
        });
    }

    /// <summary>A rebooked cancellation reports the fresh invitation as on its way.</summary>
    [Fact]
    public void ManagePageReportsARebookAsOnItsWay()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new ManagedBookingDto(
            "Amara Novak",
            "London HQ",
            "1 Example St",
            TestContractFactory.EventTimeAt(new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
            ["Medical check"],
            new Dictionary<string, ApiLink>
            {
                ["cancel"] = new("/api/manage/manage-token/cancel", "POST", "cancelManagedBooking"),
            })));
        handler.Enqueue(_ => Json(new CancelBookingOutcomeDto("reinvited", Guid.NewGuid())));
        Services.AddSingleton<IBookingClient>(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("fresh times is on its way", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
        });
    }

    /// <summary>A still-pending recovery points back at the open invitation link.</summary>
    [Fact]
    public void ManagePagePointsAtThePendingInviteWhenOneIsAlreadyOpen()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new ManagedBookingDto(
            "Amara Novak",
            "London HQ",
            "1 Example St",
            TestContractFactory.EventTimeAt(new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
            ["Medical check"],
            new Dictionary<string, ApiLink>
            {
                ["cancel"] = new("/api/manage/manage-token/cancel", "POST", "cancelManagedBooking"),
            })));
        handler.Enqueue(_ => Json(new CancelBookingOutcomeDto("reinvitePending", Guid.NewGuid())));
        Services.AddSingleton<IBookingClient>(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        var cut = Render<ManageBooking>(parameters => parameters.Add(page => page.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("Cancel and choose a new time", cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel and choose")).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("already have a pending invitation", cut.Markup);
            Assert.DoesNotContain("has been sent", cut.Markup);
        });
    }

    /// <summary>A missing replacement invite explains that the team will arrange the next time.</summary>
    [Fact]
    public void ManagePageDistinguishesUnavailableReplacementInvite()
    {
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new ManagedBookingDto(
            "Amara Novak",
            "London HQ",
            "1 Example St",
            TestContractFactory.EventTimeAt(new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
            ["Medical check"],
            new Dictionary<string, ApiLink>
            {
                ["cancel"] = new("/api/manage/manage-token/cancel", "POST", "cancelManagedBooking"),
            })));
        handler.Enqueue(_ => Json(new CancelBookingOutcomeDto("noEligibleEvents", null)));
        Services.AddSingleton<IBookingClient>(new BookingClient(NewHttpClient(handler)));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

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

    /// <summary>The delivery column shows the list row's latest status with no resend action.</summary>
    [Fact]
    public void AttendeesShowDeliveryStatusWithoutResendAction()
    {
        var attendeeId = Guid.NewGuid();
        var handler = new RoutedHandler();
        handler.Enqueue(_ => Json(new MeDto(["Coordinator"], null, null)));
        handler.Enqueue(_ => Json(new PageDto<LocationDto>([], null)));
        handler.Enqueue(_ => Json(new PageDto<AttendeeGroupDto>([], null)));
        handler.Enqueue(_ => Json(new PageDto<AppointmentTypeDto>([], null)));
        handler.Enqueue(_ => Json(new PageDto<AttendeeDto>(
            [new AttendeeDto(
                attendeeId, "Amara Novak", "a.novak@mail.com", "NotYetInvited",
                "Not yet invited", "MED", "NoActiveBooking", [], "Failed", "cursor",
                null, new Dictionary<string, ApiLink>())],
            null)));
        Services.AddSingleton<IAttendeesClient>(new AttendeesClient(NewHttpClient(handler)));
        Services.AddSingleton<IAuditClient>(new AuditClient(NewHttpClient(handler)));

        var cut = Render<Attendees>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed", cut.Markup);
            Assert.DoesNotContain(">Resend<", cut.Markup);
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
