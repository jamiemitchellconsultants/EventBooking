using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class ManageBookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task TheManageLinkShowsTheBookedTime()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingResponse>(
            $"/api/booking/manage/{booking.ManageToken}");

        Assert.NotNull(view);
        Assert.Equal("Amara Novak", view!.CandidateName);
        Assert.Contains("-", view.Display);

        using var document = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync($"/api/booking/manage/{booking.ManageToken}"));
        var links = document.RootElement.GetProperty("_links");
        var cancel = links.GetProperty("cancel");
        Assert.Equal(
            $"/api/booking/manage/{Uri.EscapeDataString(booking.ManageToken)}/cancel",
            cancel.GetProperty("href").GetString());
        Assert.Equal("POST", cancel.GetProperty("method").GetString());
        Assert.Equal("cancelManagedBooking", cancel.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task CancellingWithoutRebookingReleasesTheBooking()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(outcome!.Reinvited);

        var afterwards = await client.GetAsync($"/api/booking/manage/{booking.ManageToken}");
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(CandidateStatus.NotYetInvited, persisted.CandidateStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        Assert.Empty(persisted.PendingInviteIds);
    }

    [Fact]
    public async Task CancelAndRebookIssuesAFreshInvite()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.True(outcome!.Reinvited);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(CandidateStatus.Invited, persisted.CandidateStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        var inviteId = Assert.Single(persisted.PendingInviteIds);
        Assert.NotEqual(booking.OriginalInviteId, inviteId);
    }

    [Fact]
    public async Task AnUnknownManageTokenIsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/manage/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingFixture> GivenABooking()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var confirmedSlotId = view!.Options[0].ConfirmedSlotId;
        var confirmed = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { ConfirmedSlotId = confirmedSlotId });

        var outcome = await confirmed.Content.ReadFromJsonAsync<ConfirmResponse>();
        return new BookingFixture(
            outcome!.BookingId,
            outcome.ManageToken,
            invite.CandidateId,
            invite.Id,
            confirmedSlotId);
    }

    private async Task<CandidateInviteFixture> GivenAnInvitedCandidate()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var confirmedSlotIds = new List<Guid>();
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

            var confirmedSlotId = Guid.NewGuid();
            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(ConfirmedSlot.CreateFrom(confirmedSlotId, proposal));
            confirmedSlotIds.Add(confirmedSlotId);
        }

        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com",
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        candidate.MarkInvited();
        context.Candidates.Add(candidate);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, candidate.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            confirmedSlotIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0));
        await context.SaveChangesAsync();

        return new CandidateInviteFixture(issued.Token, candidate.Id, inviteId);
    }

    private async Task<CancellationState> ReadCancellationStateAsync(BookingFixture booking)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        var persistedBooking = await context.Bookings.SingleAsync(b => b.Id == booking.BookingId);
        var candidate = await context.Candidates.SingleAsync(c => c.Id == booking.CandidateId);
        var capacity = await context.SlotCapacities.SingleAsync(c =>
            c.ConfirmedSlotId == booking.ConfirmedSlotId &&
            c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        var pendingInviteIds = await context.Invites
            .Where(i => i.CandidateId == booking.CandidateId && i.Status == InviteStatus.Pending)
            .Select(i => i.Id)
            .ToListAsync();

        return new CancellationState(
            persistedBooking.Status,
            candidate.Status,
            capacity.TotalHeadcount,
            capacity.RemainingCapacity,
            pendingInviteIds);
    }

    private sealed record ConfirmResponse(Guid BookingId, string ManageToken);

    private sealed record CandidateInviteFixture(string Token, Guid CandidateId, Guid Id);

    private sealed record BookingFixture(
        Guid BookingId,
        string ManageToken,
        Guid CandidateId,
        Guid OriginalInviteId,
        Guid ConfirmedSlotId);

    private sealed record CancellationState(
        BookingStatus BookingStatus,
        CandidateStatus CandidateStatus,
        int TotalHeadcount,
        int RemainingCapacity,
        IReadOnlyList<Guid> PendingInviteIds);

    private sealed record BookingResponse(DateOnly Date, string Display, string CandidateName);

    private sealed record CancelResponse(bool Reinvited);
}
