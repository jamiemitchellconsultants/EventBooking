using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class ManageBookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task TheManageLinkShowsTheBookedTimeVenueAndTypes()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingResponse>(
            $"/api/manage/{booking.ManageToken}");

        Assert.NotNull(view);
        Assert.Equal("Amara Novak", view!.AttendeeName);
        Assert.Equal("Transitional location", view.LocationName);
        Assert.Equal(
            "Recorded against the transitional site until Phase 3.", view.Address);
        Assert.Equal(new DateOnly(2030, 1, 14), view.Time.Date);
        Assert.Equal(new TimeOnly(9, 0), view.Time.StartTime);
        Assert.Equal("Europe/London", view.Time.TimeZoneId);
        Assert.Equal(["Drug & Alcohol Testing", "Uniform Fitting"], view.AppointmentTypeNames);

        using var document = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync($"/api/manage/{booking.ManageToken}"));
        Assert.False(document.RootElement.TryGetProperty("display", out _));
        Assert.False(document.RootElement.TryGetProperty("canCancel", out _));
        var links = document.RootElement.GetProperty("_links");
        var cancel = links.GetProperty("cancel");
        Assert.Equal(
            $"/api/manage/{Uri.EscapeDataString(booking.ManageToken)}/cancel",
            cancel.GetProperty("href").GetString());
        Assert.Equal("POST", cancel.GetProperty("method").GetString());
        Assert.Equal("cancelManagedBooking", cancel.GetProperty("operationId").GetString());
    }

    [Fact]
    public void TheCancelLinkAppearsOnlyWhileCancellationIsAllowed()
    {
        var zones = new EventBooking.Infrastructure.Time.NodaTimeEventWindowZones();
        var allowed = EventBooking.Api.Contracts.ApiResponses.ManagedBooking(
            View(canCancel: true), zones, "token");
        var refused = EventBooking.Api.Contracts.ApiResponses.ManagedBooking(
            View(canCancel: false), zones, "token");

        Assert.True(allowed.Links.ContainsKey("cancel"));
        Assert.False(refused.Links.ContainsKey("cancel"));

        static EventBooking.Application.Bookings.BookingView View(bool canCancel) =>
            new(
                "Transitional location", "Somewhere", "Europe/London",
                new DateOnly(2030, 1, 14), new TimeOnly(9, 0), 240,
                ["Drug & Alcohol Testing"], "Amara Novak", canCancel);
    }

    [Fact]
    public async Task CancellingWithoutRebookingReleasesTheBooking()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/manage/{booking.ManageToken}/cancel", new { RequestNewTime = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.Equal("cancelled", outcome!.Outcome);

        var afterwards = await client.GetAsync($"/api/manage/{booking.ManageToken}");
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(AttendeeStatus.NotYetInvited, persisted.AttendeeStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        Assert.Empty(persisted.PendingInviteIds);
    }

    [Fact]
    public async Task CancelAndRebookIssuesAFreshInvite()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/manage/{booking.ManageToken}/cancel", new { RequestNewTime = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.Equal("reinvited", outcome!.Outcome);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(AttendeeStatus.Invited, persisted.AttendeeStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        var inviteId = Assert.Single(persisted.PendingInviteIds);
        Assert.NotEqual(booking.OriginalInviteId, inviteId);
        Assert.Equal(inviteId, outcome.InviteId);
    }

    [Fact]
    public async Task AnUnknownManageTokenIsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/manage/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingFixture> GivenABooking()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var eventId = view!.Options[0].EventId;
        var confirmed = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = eventId });

        var outcome = await confirmed.Content.ReadFromJsonAsync<ConfirmResponse>();
        return new BookingFixture(
            outcome!.BookingId,
            outcome.ManageToken,
            invite.AttendeeId,
            invite.Id,
            eventId);
    }

    private async Task<AttendeeInviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var eventIds = new List<Guid>();
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
            eventIds.Add(eventId);
        }

        // Eligible but never offered: cancelling the booked event still leaves three options,
        // so a rebook has somewhere to go.
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0), 240), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
        }

        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            $"{Guid.NewGuid():N}@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            ProposalFixture.Now);
        attendee.MarkInvited(ProposalFixture.Now);
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));
        await context.SaveChangesAsync();

        return new AttendeeInviteFixture(issued, attendee.Id, inviteId);
    }

    private async Task<CancellationState> ReadCancellationStateAsync(BookingFixture booking)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        var persistedBooking = await context.Bookings.SingleAsync(b => b.Id == booking.BookingId);
        var attendee = await context.Attendees.SingleAsync(c => c.Id == booking.AttendeeId);
        var capacity = await context.EventCapacities.SingleAsync(c =>
            c.EventId == booking.EventId &&
            c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        var pendingInviteIds = await context.Invites
            .Where(i => i.AttendeeId == booking.AttendeeId && i.Status == InviteStatus.Pending)
            .Select(i => i.Id)
            .ToListAsync();

        return new CancellationState(
            persistedBooking.Status,
            attendee.Status,
            capacity.TotalHeadcount,
            capacity.RemainingCapacity,
            pendingInviteIds);
    }

    private sealed record ConfirmResponse(Guid BookingId, string ManageToken);

    private sealed record AttendeeInviteFixture(string Token, Guid AttendeeId, Guid Id);

    private sealed record BookingFixture(
        Guid BookingId,
        string ManageToken,
        Guid AttendeeId,
        Guid OriginalInviteId,
        Guid EventId);

    private sealed record CancellationState(
        BookingStatus BookingStatus,
        AttendeeStatus AttendeeStatus,
        int TotalHeadcount,
        int RemainingCapacity,
        IReadOnlyList<Guid> PendingInviteIds);

    private sealed record BookingTimeResponse(
        DateOnly Date, TimeOnly StartTime, int DurationMinutes, string TimeZoneId);

    private sealed record BookingResponse(
        string AttendeeName,
        string LocationName,
        string Address,
        BookingTimeResponse Time,
        IReadOnlyList<string> AppointmentTypeNames);

    private sealed record CancelResponse(string Outcome, Guid? InviteId);
}
