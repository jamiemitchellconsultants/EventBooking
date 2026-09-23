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

/// <summary>Verifies recovery-invite start/cancel routes, auth, and the delivery-outcome projection.</summary>
[Collection("api")]
public sealed class RecoveryInviteEndpointTests(ApiFactory factory)
{
    private sealed record DeliveryOutcomeResponse(
        Guid InviteId,
        IReadOnlyList<Guid> AppointmentTypeIds,
        bool EmailSent);

    /// <summary>A Coordinator starts recovery for a missed appointment and gets the outcome.</summary>
    [Fact]
    public async Task CoordinatorStartsRecoveryAndReceivesDeliveryOutcome()
    {
        var candidateId = await GivenCandidateWithNoShowAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/candidates/{candidateId}/recovery-invites", null);
        var body = await response.Content.ReadFromJsonAsync<DeliveryOutcomeResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, body!.InviteId);
        Assert.Contains(AppointmentTypeIds.MedicalCheckUp, body.AppointmentTypeIds);
    }

    /// <summary>A Coordinator learns nothing is recoverable through the stable error code.</summary>
    [Fact]
    public async Task CoordinatorReceivesConflictWhenNothingRecoverable()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/candidates", new
        {
            Name = "Amara Novak",
            Email = $"{Guid.NewGuid():N}@example.com",
            EmployeeGroupId = EmployeeGroupIds.CabinCrew,
        });
        var candidateId = await created.Content.ReadFromJsonAsync<Guid>();

        using var response = await client.PostAsync(
            $"/api/candidates/{candidateId}/recovery-invites", null);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "recovery_not_available",
            problem.RootElement.GetProperty("title").GetString());
    }

    /// <summary>Only Coordinators may start or cancel a recovery invite.</summary>
    [Fact]
    public async Task AdminCannotStartRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        using var response = await factory.CreateClient().PostAsync(
            $"/api/candidates/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A scoped Manager still lacks the Coordinator-only recovery capability.</summary>
    [Fact]
    public async Task ManagerCannotStartRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PostAsync(
            $"/api/candidates/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Anonymous callers cannot start a recovery invite.</summary>
    [Fact]
    public async Task AnonymousCannotStartRecovery()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient().PostAsync(
            $"/api/candidates/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A Coordinator cancels a pending recovery, and a second cancel conflicts.</summary>
    [Fact]
    public async Task CoordinatorCancelsPendingRecovery()
    {
        var candidateId = await GivenCandidateWithNoShowAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        using var started = await client.PostAsync(
            $"/api/candidates/{candidateId}/recovery-invites", null);
        var outcome = await started.Content.ReadFromJsonAsync<DeliveryOutcomeResponse>();

        using var cancelled = await client.DeleteAsync(
            $"/api/candidates/{candidateId}/recovery-invites/{outcome!.InviteId}");
        using var again = await client.DeleteAsync(
            $"/api/candidates/{candidateId}/recovery-invites/{outcome.InviteId}");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    /// <summary>Cancelling an unknown recovery invite is not found.</summary>
    [Fact]
    public async Task CancellingUnknownRecoveryReturnsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/candidates", new
        {
            Name = "Amara Novak",
            Email = $"{Guid.NewGuid():N}@example.com",
            EmployeeGroupId = EmployeeGroupIds.CabinCrew,
        });
        var candidateId = await created.Content.ReadFromJsonAsync<Guid>();

        using var response = await client.DeleteAsync(
            $"/api/candidates/{candidateId}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Only Coordinators may cancel a recovery invite.</summary>
    [Fact]
    public async Task AdminCannotCancelRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        using var response = await factory.CreateClient().DeleteAsync(
            $"/api/candidates/{Guid.NewGuid()}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A scoped Manager still lacks the Coordinator-only recovery capability.</summary>
    [Fact]
    public async Task ManagerCannotCancelRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().DeleteAsync(
            $"/api/candidates/{Guid.NewGuid()}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Seeds a booked candidate with one Medical Check-up no-show and a spare slot.</summary>
    private async Task<Guid> GivenCandidateWithNoShowAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;
        var bookedSlot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(-1), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareSlots = new[]
        {
            new TimeOnly(11, 0),
            new TimeOnly(13, 0),
            new TimeOnly(15, 0),
        }
        .Select(start => ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(2), start),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
        .ToList();
        var group = context.EmployeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedSlot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedSlot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
        context.AddRange(bookedSlot);
        context.AddRange(spareSlots);
        context.AddRange(candidate, booking, appointment);
        await context.SaveChangesAsync();

        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp);
        using var marked = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{appointment.Id}/status",
            new { status = "NoShow", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        return candidate.Id;
    }
}
