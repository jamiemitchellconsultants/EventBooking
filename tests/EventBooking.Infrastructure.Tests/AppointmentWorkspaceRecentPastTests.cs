using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies the workspace retains recently past slots for late outcome recording.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceRecentPastTests(PostgresFixture fixture)
{
    /// <summary>Verifies the slot list includes 7-days-past slots and excludes 8-days-past slots.</summary>
    [Fact]
    public async Task SlotListRetainsSevenDaysPastAndExcludesOlderSlots()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddSlot(write, new DateOnly(2026, 9, 7), cancelled: false);
            var sevenDaysPast = AddSlot(write, new DateOnly(2026, 8, 31), cancelled: false);
            var eightDaysPast = AddSlot(write, new DateOnly(2026, 8, 30), cancelled: false);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, sevenDaysPast, "Recent Past", "recent@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, eightDaysPast, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListSlotsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal(2, result.Slots.Count);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Slots[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Slots[1].Date);
    }

    /// <summary>Verifies slot detail loads a recently past slot inside trusted scope.</summary>
    [Fact]
    public async Task SelectedSlotLoadsRecentlyPastSlotInScope()
    {
        await fixture.ResetAsync();
        Guid slotId;
        await using (var write = fixture.NewContext())
        {
            var slot = AddSlot(write, new DateOnly(2026, 9, 6), cancelled: false);
            slotId = slot.Id;
            AddBooking(write, slot, "Past Candidate", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            slotId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(slotId, detail!.ConfirmedSlotId);
        Assert.Equal(new DateOnly(2026, 9, 6), detail.Date);
        Assert.Single(detail.Appointments);
    }

    /// <summary>Verifies slot detail returns null for slots older than the allowance or outside scope.</summary>
    [Fact]
    public async Task SelectedSlotTooOldOrOutOfScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid tooOldId;
        Guid wrongScopeId;
        await using (var write = fixture.NewContext())
        {
            var tooOld = AddSlot(write, new DateOnly(2026, 8, 30), cancelled: false);
            tooOldId = tooOld.Id;
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            var wrongScope = AddSlot(write, new DateOnly(2026, 9, 6), cancelled: false);
            wrongScopeId = wrongScope.Id;
            AddBooking(write, wrongScope, "Medical Candidate", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AppointmentWorkspaceQueries(read, new TestClock());
        Assert.Null(await queries.GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, tooOldId, CancellationToken.None));
        Assert.Null(await queries.GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, wrongScopeId, CancellationToken.None));
    }

    private static ConfirmedSlot AddSlot(
        EventBookingDbContext context,
        DateOnly date,
        bool cancelled)
    {
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(),
            new SlotWindow(date, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 20));
        if (cancelled)
        {
            slot.Cancel();
        }

        context.ConfirmedSlots.Add(slot);
        return slot;
    }

    private static void AddBooking(
        EventBookingDbContext context,
        ConfirmedSlot slot,
        string name,
        string email,
        Guid appointmentTypeId,
        BookingAppointmentStatus status,
        bool cancelled)
    {
        var group = EmployeeGroup.Define(
            Guid.NewGuid(), $"WORKSPACE_{Guid.NewGuid():N}".ToUpperInvariant(), "Workspace", true,
            [appointmentTypeId]);
        context.EmployeeGroups.Add(group);
        var candidate = Candidate.Create(Guid.NewGuid(), name, email, group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            candidate.Id,
            $"invite-{candidate.Id}",
            DateTimeOffset.UtcNow.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, $"manage-{candidate.Id}", DateTimeOffset.UtcNow);
        if (cancelled)
        {
            booking.Cancel();
        }

        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);

        context.Candidates.Add(candidate);
        context.Bookings.Add(booking);
        context.BookingAppointments.Add(appointment);
    }

    /// <summary>A fixed clock pinning head-office today to 2026-09-07 for workspace tests.</summary>
    private sealed class TestClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        /// <inheritdoc/>
        public DateTimeOffset NowAtHeadOffice => UtcNow;

        /// <inheritdoc/>
        public DateOnly TodayAtHeadOffice => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateOnly DateAtHeadOffice(DateTimeOffset instant) => new(2026, 9, 7);

        /// <inheritdoc/>
        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant;
    }
}
