using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the book page distinguishes recovery visits with singular/plural copy.</summary>
public class BookRecoveryHeadingTests : BunitContext
{
    [Fact]
    public void InitialInviteKeepsTheChooseATimeHeading()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(), "Amara Novak", ["Medical Check-Up"], [Option()], IsRecovery: false));

        cut.WaitForAssertion(() =>
            Assert.Equal("Choose a time", cut.Find("h1").TextContent.Trim()));
    }

    [Fact]
    public void RecoveryInviteUsesTheSingularMissedAppointmentHeading()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(), "Amara Novak", ["Medical Check-Up"], [Option()], IsRecovery: true));

        cut.WaitForAssertion(() =>
            Assert.Equal(
                "Choose a new time for your missed appointment",
                cut.Find("h1").TextContent.Trim()));
    }

    [Fact]
    public void RecoveryInviteUsesThePluralHeadingForTwoTypes()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["Medical Check-Up", "Uniform Fitting"],
            [Option()],
            IsRecovery: true));

        cut.WaitForAssertion(() =>
            Assert.Equal(
                "Choose a new time for your missed appointments",
                cut.Find("h1").TextContent.Trim()));
    }

    private IRenderedComponent<Book> RenderBook(InviteDto invite)
    {
        var handler = new StubInviteHandler(invite);
        Services.AddSingleton(
            new BookingClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        return Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
    }

    private static InviteOptionDto Option() =>
        new(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00");

    private sealed class StubInviteHandler(InviteDto invite) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(invite),
            });
    }
}
