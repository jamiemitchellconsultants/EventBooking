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

/// <summary>Verifies workspace projections are scoped and contain only operational data.</summary>
[Collection("postgres")]
public sealed class AppointmentWorkspaceQueryTests(PostgresFixture fixture)
{
    /// <summary>Verifies slot counts retain recent-past rows and exclude older, cancelled, inactive-booking, and other-type rows.</summary>
    [Fact]
    public async Task SlotListContainsOnlyRetainedActiveScopedAppointments()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            var current = AddSlot(write, new DateOnly(2026, 9, 7), cancelled: false);
            var future = AddSlot(write, new DateOnly(2026, 9, 8), cancelled: false);
            var recentPast = AddSlot(write, new DateOnly(2026, 9, 6), cancelled: false);
            var tooOld = AddSlot(write, new DateOnly(2026, 8, 30), cancelled: false);
            var cancelled = AddSlot(write, new DateOnly(2026, 9, 9), cancelled: true);
            AddBooking(write, current, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.CheckedIn, false);
            AddBooking(write, current, "Priya Shah", "priya@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Other Type", "other@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            AddBooking(write, future, "Future Candidate", "future@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, recentPast, "Past Candidate", "past@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, tooOld, "Too Old", "old@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, cancelled, "Cancelled Slot", "slot@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, current, "Cancelled Booking", "booking@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, true);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var result = await new AppointmentWorkspaceQueries(read, new TestClock()).ListSlotsAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            new DateOnly(2026, 9, 7),
            CancellationToken.None);

        Assert.Equal("Drug & Alcohol Testing", result.AppointmentTypeName);
        Assert.Equal(3, result.Slots.Count);
        Assert.Equal(new DateOnly(2026, 9, 6), result.Slots[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Slots[1].Date);
        Assert.Equal(1, result.Slots[1].Counts.Expected);
        Assert.Equal(1, result.Slots[1].Counts.CheckedIn);
        Assert.Equal(0, result.Slots[1].Counts.Completed);
        Assert.Equal(0, result.Slots[1].Counts.NoShow);
        Assert.Equal(new DateOnly(2026, 9, 8), result.Slots[2].Date);
    }

    /// <summary>Verifies slot detail returns only same-type active rows ordered for staff use.</summary>
    [Fact]
    public async Task SelectedSlotReturnsOnlyMinimumScopedCandidateRows()
    {
        await fixture.ResetAsync();
        Guid slotId;
        await using (var write = fixture.NewContext())
        {
            var slot = AddSlot(write, new DateOnly(2026, 9, 7), cancelled: false);
            slotId = slot.Id;
            AddBooking(write, slot, "Zara Young", "zara@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Completed, false);
            AddBooking(write, slot, "Alex Morgan", "alex@example.com",
                AppointmentTypeIds.DrugAndAlcoholTesting, BookingAppointmentStatus.Expected, false);
            AddBooking(write, slot, "Medical Candidate", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var detail = await new AppointmentWorkspaceQueries(read, new TestClock()).GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            slotId,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Drug & Alcohol Testing", detail!.AppointmentTypeName);
        Assert.Equal(slotId, detail.ConfirmedSlotId);
        Assert.Collection(
            detail.Appointments,
            row =>
            {
                Assert.Equal("Alex Morgan", row.CandidateName);
                Assert.Equal("alex@example.com", row.CandidateEmail);
                Assert.Equal(BookingAppointmentStatus.Expected, row.Status);
            },
            row =>
            {
                Assert.Equal("Zara Young", row.CandidateName);
                Assert.Equal(BookingAppointmentStatus.Completed, row.Status);
                Assert.NotNull(row.CheckedInAt);
                Assert.NotNull(row.OutcomeAt);
            });
    }

    /// <summary>Verifies a slot that has no row in trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task SelectedSlotOutsideTrustedScopeIsNotFound()
    {
        await fixture.ResetAsync();
        Guid slotId;
        await using (var write = fixture.NewContext())
        {
            var slot = AddSlot(write, new DateOnly(2026, 9, 7), cancelled: false);
            slotId = slot.Id;
            AddBooking(write, slot, "Medical Candidate", "medical@example.com",
                AppointmentTypeIds.MedicalCheckUp, BookingAppointmentStatus.Expected, false);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null(await new AppointmentWorkspaceQueries(read, new TestClock()).GetSlotAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            slotId,
            CancellationToken.None));
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
        var staff = Guid.NewGuid();
        var checkIn = new DateTimeOffset(2026, 9, 7, 9, 5, 0, TimeSpan.Zero);
        if (status is BookingAppointmentStatus.CheckedIn or BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.CheckedIn, staff, checkIn, true, false);
        }
        if (status == BookingAppointmentStatus.Completed)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.Completed, staff, checkIn.AddHours(1), false, false);
        }
        if (status == BookingAppointmentStatus.NoShow)
        {
            appointment.TransitionTo(
                BookingAppointmentStatus.NoShow, staff, checkIn.AddHours(4), false, true);
        }

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
