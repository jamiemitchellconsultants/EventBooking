using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
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

namespace EventBooking.Mcp.Tests;

/// <summary>Seeds booked and no-show candidate scenarios for MCP parity tests.</summary>
public static class McpScenarioSeeder
{
    /// <summary>Seeds a candidate holding one active original booking plus spare future slots.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded candidate and booking identifiers.</returns>
    public static async Task<(Guid CandidateId, Guid BookingId)> GivenBookedCandidateAsync(McpFactory factory)
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

    /// <summary>Seeds a booked candidate with one no-show appointment for recovery tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <returns>The seeded candidate and booking identifiers.</returns>
    public static async Task<(Guid CandidateId, Guid BookingId)> GivenCandidateWithNoShowAsync(McpFactory factory)
    {
        Guid candidateId;
        Guid appointmentId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
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
            candidateId = candidate.Id;
            appointmentId = appointment.Id;
        }

        var staffUserId = await factory.GivenStaffAsync([Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<UpdateBookingAppointmentStatusHandler>();
            var result = await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = staffUserId,
                    BookingAppointmentId = appointmentId,
                    Status = BookingAppointmentStatus.NoShow,
                    ExpectedVersion = 1,
                },
                CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error.Message);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var bookingId = await context.BookingAppointments
                .Where(a => a.Id == appointmentId)
                .Select(a => a.BookingId)
                .SingleAsync();
            return (candidateId, bookingId);
        }
    }

    /// <summary>Seeds one active slot with a single scoped booking appointment for roster tests.</summary>
    /// <param name="factory">The MCP host providing services and identity helpers.</param>
    /// <param name="appointmentTypeId">The appointment type scoping the seeded workspace.</param>
    /// <returns>The seeded confirmed slot identifier.</returns>
    public static async Task<Guid> GivenAppointmentWorkspaceAsync(McpFactory factory, Guid appointmentTypeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? EmployeeGroupIds.GroundOperationsAgent
            : EmployeeGroupIds.Pilots;
        var group = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(slot, candidate, booking, appointment);
        await context.SaveChangesAsync();
        return slot.Id;
    }
}
