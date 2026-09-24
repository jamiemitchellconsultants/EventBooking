using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies booking confirmation delivery outcomes at the HTTP boundary.</summary>
[Collection("api")]
public class ConfirmBookingEndpointTests(ApiFactory factory)
{
    /// <summary>A attendee can confirm one of the offered events.</summary>
    [Fact]
    public async Task AAttendeeCanConfirmOneOfTheirOptions()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[1];

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen.EventId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        Assert.NotEqual(Guid.Empty, outcome!.BookingId);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
    }

    /// <summary>Confirmation stages the confirmation email for the outbox instead of sending it.</summary>
    [Fact]
    public async Task AConfirmationStagesItsEmailAsPending()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = view!.Options[0].EventId });
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(outcome!.ManageToken));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var staged = await context.EmailLogs.SingleAsync(e => e.BookingId == outcome.BookingId);
        Assert.Equal(EmailTemplate.BookingConfirmation, staged.TemplateName);
        Assert.Equal(EmailStatus.Pending, staged.Status);
    }

    /// <summary>The same confirmation link cannot be consumed twice.</summary>
    [Fact]
    public async Task TheSameLinkCannotBeUsedTwice()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[0].EventId;

        var first = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });
        var second = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("already-confirmed", await second.Content.ReadAsStringAsync());
    }

    /// <summary>A event absent from the invitation is rejected as a bad request.</summary>
    [Fact]
    public async Task ChoosingAEventThatWasNeverOfferedIsRejected()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = invite.UnofferedEventId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private sealed record ConfirmResponse(Guid BookingId, string ManageToken);

    private async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var offeredEventIds = new List<Guid>();

        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            offeredEventIds.Add(eventId);
        }

        var unofferedProposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0), 240), Guid.NewGuid());
        unofferedProposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        unofferedProposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        unofferedProposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var unofferedEventId = Guid.NewGuid();
        context.EventProposals.Add(unofferedProposal);
        context.Events.Add(Event.CreateFrom(unofferedEventId, unofferedProposal));

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            $"{Guid.NewGuid():N}@mail.com",
            pilots,
            ProposalFixture.Now);
        attendee.MarkInvited(ProposalFixture.Now);
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            [ProposalFixture.LocationId],
            offeredEventIds,
            attendee.RequiredAppointmentTypeIds,
            0));
        await context.SaveChangesAsync();

        return new InviteFixture(issued, unofferedEventId);
    }

    private sealed record InviteFixture(string Token, Guid UnofferedEventId);
}
