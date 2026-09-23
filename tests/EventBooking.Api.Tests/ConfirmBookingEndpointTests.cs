using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies booking confirmation delivery outcomes at the HTTP boundary.</summary>
[Collection("api")]
public class ConfirmBookingEndpointTests(ApiFactory factory)
{
    /// <summary>A candidate can confirm one of the offered slots.</summary>
    [Fact]
    public async Task ACandidateCanConfirmOneOfTheirOptions()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[1];

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { ConfirmedSlotId = chosen.ConfirmedSlotId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        Assert.NotEqual(Guid.Empty, outcome!.BookingId);
        Assert.Equal(chosen.Date, outcome.Date);
        Assert.Equal(chosen.StartTime, outcome.StartTime);
        Assert.Equal(chosen.EndTime, outcome.EndTime);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
        Assert.Equal("Sent", outcome.DeliveryStatus);
    }

    /// <summary>The confirmation names the API's configured head office, the one the email uses.</summary>
    [Fact]
    public async Task TheConfirmationCarriesTheConfiguredHeadOfficeAddress()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { ConfirmedSlotId = view!.Options[0].ConfirmedSlotId });

        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        var configured = factory.Services.GetRequiredService<CandidatePortalOptions>().HeadOfficeAddress;
        Assert.False(string.IsNullOrWhiteSpace(configured));
        Assert.Equal(configured, outcome!.HeadOfficeAddress);
    }

    /// <summary>Booking confirmation remains successful while a provider rejection is reported.</summary>
    [Fact]
    public async Task AProviderFailureReturnsAConfirmedBookingAndFailedDeliveryStatus()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.EmailTransport.FailNextSend = true;
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { ConfirmedSlotId = view!.Options[0].ConfirmedSlotId });
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", outcome!.DeliveryStatus);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
    }

    /// <summary>The same confirmation link cannot be consumed twice.</summary>
    [Fact]
    public async Task TheSameLinkCannotBeUsedTwice()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[0].ConfirmedSlotId;

        var first = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { ConfirmedSlotId = chosen });
        var second = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { ConfirmedSlotId = chosen });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>A slot absent from the invitation is rejected as a conflict.</summary>
    [Fact]
    public async Task ChoosingASlotThatWasNeverOfferedIsAConflict()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { ConfirmedSlotId = invite.UnofferedSlotId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed record ConfirmResponse(
        Guid BookingId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string ManageToken,
        string DeliveryStatus,
        string HeadOfficeAddress);

    private async Task<InviteFixture> GivenAnInvitedCandidate()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var offeredSlotIds = new List<Guid>();

        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(date, startTime), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var slotId = Guid.NewGuid();
            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(ConfirmedSlot.CreateFrom(slotId, proposal));
            offeredSlotIds.Add(slotId);
        }

        var unofferedProposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0)), Guid.NewGuid());
        unofferedProposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        unofferedProposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        unofferedProposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var unofferedSlotId = Guid.NewGuid();
        context.SlotProposals.Add(unofferedProposal);
        context.ConfirmedSlots.Add(ConfirmedSlot.CreateFrom(unofferedSlotId, unofferedProposal));

        var pilots = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        candidate.MarkInvited();
        context.Candidates.Add(candidate);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            candidate.Id,
            issued.TokenHash,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            offeredSlotIds,
            candidate.RequiredAppointmentTypeIds, 0));
        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, unofferedSlotId);
    }

    private sealed record InviteFixture(string Token, Guid UnofferedSlotId);
}
