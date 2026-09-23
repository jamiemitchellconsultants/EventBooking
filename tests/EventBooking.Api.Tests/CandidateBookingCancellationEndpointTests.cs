using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
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

/// <summary>Verifies the staff booking-cancellation and active-booking-listing routes and their auth.</summary>
[Collection("api")]
public sealed class CandidateBookingCancellationEndpointTests(ApiFactory factory)
{
    private sealed record CancelResponse(
        bool Reinvited, bool InviteCreated, string? DeliveryStatus, Guid? DeliveryId);

    private sealed record BookingRow(
        Guid BookingId, bool IsOriginal, DateOnly SlotDate, TimeOnly SlotStartTime, TimeOnly SlotEndTime);

    [Fact]
    public async Task CoordinatorCanCancelAnOriginalBooking()
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(body!.Reinvited);
        Assert.False(body.InviteCreated);
        Assert.Equal(BookingStatus.Cancelled, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task CoordinatorCanCancelAndRebookAnOriginalBooking()
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.True(body!.Reinvited);
        Assert.True(body.InviteCreated);
        Assert.NotNull(body.DeliveryStatus);
        Assert.Equal(BookingStatus.Cancelled, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task RebookTrueOnARecoveryBookingIsAConflict()
    {
        var (candidateId, originalId) = await GivenBookedCandidateAsync();
        var recoveryId = await GivenActiveRecoveryAsync(candidateId, originalId);
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{recoveryId}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("conflict", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(BookingStatus.Active, await StatusOfAsync(recoveryId));
    }

    [Fact]
    public async Task UnknownBookingIdIsNotFound()
    {
        var (candidateId, _) = await GivenBookedCandidateAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{Guid.NewGuid()}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("AppointmentStaff")]
    [InlineData("Admin")]
    public async Task NonCoordinatorRolesAreForbidden(string role)
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = role switch
        {
            "Manager" => await factory.GivenStaffAsync(Role.Manager, AppointmentTypeIds.UniformFitting),
            "AppointmentStaff" => await factory.GivenStaffAsync(
                Role.AppointmentStaff, AppointmentTypeIds.UniformFitting),
            _ => await factory.GivenStaffAsync(Role.Admin),
        };
        var client = factory.CreateClient();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = false });
        using var list = await client.GetAsync($"/api/candidates/{candidateId}/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(BookingStatus.Active, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task AnUnassignedProfileIsForbidden()
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/candidates/{Guid.NewGuid()}/bookings/{Guid.NewGuid()}/cancel", new { Rebook = false });
        using var list = await client.GetAsync($"/api/candidates/{Guid.NewGuid()}/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task CoordinatorCanListActiveBookings()
    {
        var (candidateId, originalId) = await GivenBookedCandidateAsync();
        var recoveryId = await GivenActiveRecoveryAsync(candidateId, originalId);
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<BookingRow>>(
            $"/api/candidates/{candidateId}/bookings");

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);
        Assert.True(rows[0].IsOriginal);
        Assert.Equal(originalId, rows[0].BookingId);
        Assert.False(rows[1].IsOriginal);
        Assert.Equal(recoveryId, rows[1].BookingId);
        Assert.Equal(rows[0].SlotStartTime.AddHours(4), rows[0].SlotEndTime);
    }

    [Fact]
    public async Task ListingForAnUnknownCandidateIsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/candidates/{Guid.NewGuid()}/bookings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingStatus> StatusOfAsync(Guid bookingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        return await context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => b.Status)
            .SingleAsync();
    }

    /// <summary>Seeds a candidate holding one active original booking plus spare future slots.</summary>
    private async Task<(Guid CandidateId, Guid BookingId)> GivenBookedCandidateAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;

        var bookedSlot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(30), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareSlots = new[] { new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0) }
            .Select(start => ConfirmedSlot.CreateImported(
                Guid.NewGuid(), new SlotWindow(today.AddDays(31), start),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();

        var group = context.EmployeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedSlot.Id, spareSlots[0].Id, spareSlots[1].Id],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedSlot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        candidate.MarkInvited();
        invite.MarkUsed();
        candidate.MarkBooked();

        context.AddRange(bookedSlot);
        context.AddRange(spareSlots);
        context.AddRange(candidate, invite, booking);
        foreach (var typeId in candidate.RequiredAppointmentTypeIds)
        {
            context.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
            bookedSlot.CapacityFor(typeId).Decrement();
        }

        await context.SaveChangesAsync();
        return (candidate.Id, booking.Id);
    }

    /// <summary>Seeds one active recovery booking on a later slot for an existing original.</summary>
    private async Task<Guid> GivenActiveRecoveryAsync(Guid candidateId, Guid originalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;

        var recoverySlot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(40), new TimeOnly(13, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var original = await context.Bookings.SingleAsync(b => b.Id == originalId);
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, originalId, $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            [recoverySlot.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoverySlot.Id,
            $"manage-recovery-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddHours(1));
        recoveryInvite.MarkUsed();

        context.Add(recoverySlot);
        context.AddRange(recoveryInvite, recovery);
        context.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp));
        recoverySlot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();

        await context.SaveChangesAsync();
        return recovery.Id;
    }
}
