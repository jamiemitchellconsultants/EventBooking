using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class DashboardQueryTests(PostgresFixture fixture)
{
    private sealed class MovableLondonClock(DateTimeOffset now) : IClock
    {
        private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        public DateTimeOffset UtcNow { get; set; } = now;

        /// <summary>Gets the current instant converted to the London head-office time zone.</summary>
        public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(UtcNow, London);

        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(UtcNow);

        public DateOnly DateAtHeadOffice(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, London).DateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, London);
    }

    [Fact]
    public async Task AwaitingCandidatesUseHeadOfficeDatesAcrossTheUtcMidnightBoundary()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var awaitingGroup = EmployeeGroup.Define(
                Guid.NewGuid(), "DASH_DAT_MED", "Dashboard DAT MED", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp]);
            write.EmployeeGroups.Add(awaitingGroup);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com", awaitingGroup);
            candidate.MarkAwaitingAvailability();
            write.Candidates.Add(candidate);
            await write.SaveChangesAsync();
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 23, 30, 0, TimeSpan.Zero);

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .AwaitingAvailabilityAsync(CancellationToken.None));

        Assert.Equal("C. Diallo", row.Name);
        Assert.Equal(new[] { "DAT", "MED" }, row.RequiredCodes);
        Assert.Equal(new DateOnly(2026, 9, 3), row.WaitingSince);
        Assert.Equal(1, row.DaysWaiting);
    }

    [Fact]
    public async Task TheStatusStampIsWrittenOnAddAndMovesOnlyWhenStatusMoves()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid candidateId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = EmployeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI", "Dashboard UNI", true,
                [AppointmentTypeIds.UniformFitting]);
            write.EmployeeGroups.Add(uniformOnly);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly);
            write.Candidates.Add(candidate);
            await write.SaveChangesAsync();
            candidateId = candidate.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var added = await addedRead.Candidates.SingleAsync(c => c.Id == candidateId);
            Assert.Equal(clock.UtcNow, addedRead.Entry(added)
                .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var candidate = await rename.Candidates.SingleAsync(c => c.Id == candidateId);
            candidate.UpdateDetails("B. Chen-Smith", "b.chen@mail.com");
            await rename.SaveChangesAsync();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var renamed = await renamedRead.Candidates.SingleAsync(c => c.Id == candidateId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamedRead.Entry(renamed)
                    .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var candidate = await statusChange.Candidates.SingleAsync(c => c.Id == candidateId);
            candidate.MarkAwaitingAvailability();
            await statusChange.SaveChangesAsync();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Candidates.SingleAsync(c => c.Id == candidateId);
        Assert.Equal(clock.UtcNow, changedRead.Entry(changed)
            .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
    }

    [Fact]
    public async Task TheSynchronousSavePathStampsAddsAndStatusChangesButNotUnrelatedEdits()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid candidateId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = EmployeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_TWO", "Dashboard UNI two", true,
                [AppointmentTypeIds.UniformFitting]);
            write.EmployeeGroups.Add(uniformOnly);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "S. Patel", "s.patel@mail.com", uniformOnly);
            write.Candidates.Add(candidate);
            write.SaveChanges();
            candidateId = candidate.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var candidate = await addedRead.Candidates.SingleAsync(c => c.Id == candidateId);
            Assert.Equal(clock.UtcNow, addedRead.Entry(candidate)
                .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var candidate = await rename.Candidates.SingleAsync(c => c.Id == candidateId);
            candidate.UpdateDetails("S. Patel-Jones", "s.patel@mail.com");
            rename.SaveChanges();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var candidate = await renamedRead.Candidates.SingleAsync(c => c.Id == candidateId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamedRead.Entry(candidate)
                    .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var candidate = await statusChange.Candidates.SingleAsync(c => c.Id == candidateId);
            candidate.MarkAwaitingAvailability();
            statusChange.SaveChanges();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Candidates.SingleAsync(c => c.Id == candidateId);
        Assert.Equal(clock.UtcNow, changedRead.Entry(changed)
            .Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty).CurrentValue);
    }

    [Fact]
    public async Task TheFollowUpListUsesTheHeadOfficeDayTheAutoRetryGaveUp()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var groundOps = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "D. Reyes", "d.reyes@mail.com", groundOps);
            candidate.MarkInvited();
            candidate.MarkNoResponse();
            write.Candidates.Add(candidate);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .NoResponseAsync(CancellationToken.None));

        Assert.Equal("D. Reyes", row.Name);
        Assert.Equal(new DateOnly(2026, 9, 3), row.GaveUpOn);
    }

    [Fact]
    public async Task TheSlotsOverviewShowsCapacityAndActiveBookingCount()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var slots = new[]
            {
                SlotFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0)),
                SlotFor(new DateOnly(2026, 9, 22), new TimeOnly(9, 0)),
                SlotFor(new DateOnly(2026, 9, 23), new TimeOnly(9, 0)),
            };
            slots[0].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "E. Martin", "e.martin@mail.com", pilots);
            candidate.MarkInvited();
            var invite = Invite.CreateInitial(
                Guid.NewGuid(), candidate.Id, "invite-hash", clock.UtcNow.AddDays(4),
                slots.Select(s => s.Id), candidate.RequiredAppointmentTypeIds, 0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, slots[0].Id, "booking-hash", clock.UtcNow);

            write.ConfirmedSlots.AddRange(slots);
            write.Candidates.Add(candidate);
            write.Invites.Add(invite);
            write.Bookings.Add(booking);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock).SlotsOverviewAsync(CancellationToken.None);
        var row = rows.Single(s => s.Date == new DateOnly(2026, 9, 21));

        Assert.Equal(new TimeOnly(13, 0), row.EndTime);
        Assert.Equal(1, row.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, row.Capacities.Select(c => c.Code));
        var drugAndAlcohol = row.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    [Fact]
    public async Task ACancelledSlotIsNotOnTheOverview()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var slot = SlotFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
            slot.Cancel();
            write.ConfirmedSlots.Add(slot);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(await new DashboardQueries(read, clock).SlotsOverviewAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnlyTheLatestEmailLogRowPerCandidateIsReturned()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var candidateId = Guid.NewGuid();

        await using (var write = NewContext(clock))
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                candidateId, "A. Novak", "a.novak@mail.com", pilots);
            write.Candidates.Add(candidate);
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), candidateId, EmailTemplate.CandidateInvite,
                new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), EmailStatus.Resolved));
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), candidateId, EmailTemplate.CandidateReinvite,
                new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero), EmailStatus.Sent));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));

        Assert.Equal(candidateId, row.CandidateId);
        Assert.Equal(EmailTemplate.CandidateReinvite, row.TemplateName);
        Assert.Equal(EmailStatus.Sent, row.Status);
    }

    [Fact]
    public async Task ACandidateWithNoEmailLogRowsIsAbsent()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var uniformOnly = EmployeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_THREE", "Dashboard UNI three", true,
                [AppointmentTypeIds.UniformFitting]);
            write.EmployeeGroups.Add(uniformOnly);
            write.Candidates.Add(Candidate.Create(
                Guid.NewGuid(), "B. Chen", "b.chen@mail.com", uniformOnly));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));
    }

    /// <summary>Retry visibility follows the latest delivery's current candidate and slot context.</summary>
    [Fact]
    public async Task LatestEmailStatusMarksOnlyActionableDeliveryContextAsRetryable()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var actionableId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var cancelledSlot = SlotFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
        cancelledSlot.Cancel();
        // Production stages the cancellation notice with its booking identifier, so the
        // retryable delivery carries one; the stale delivery below omits it on purpose.
        var cancelledBookingId = Guid.NewGuid();
        var outstanding = EmailLog.RecordPending(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.SlotCancelledRebookingNeeded,
            clock.UtcNow,
            bookingId: cancelledBookingId,
            confirmedSlotId: cancelledSlot.Id);
        var laterSent = EmailLog.Record(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.CandidateInvite,
            clock.UtcNow.AddMinutes(3),
            EmailStatus.Sent);

        await using (var write = NewContext(clock))
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var actionableCandidate = Candidate.Create(
                actionableId, "Actionable", "actionable@mail.com", pilots);
            actionableCandidate.MarkAwaitingAvailability();
            var staleCandidate = Candidate.Create(
                staleId, "Stale", "stale@mail.com", pilots);
            var cancelledInvite = Invite.CreateInitial(
                Guid.NewGuid(), actionableId, "cancelled-invite-hash", clock.UtcNow.AddDays(4),
                [cancelledSlot.Id, Guid.NewGuid(), Guid.NewGuid()],
                actionableCandidate.RequiredAppointmentTypeIds, 0);
            var cancelledBooking = Booking.Create(
                cancelledBookingId, cancelledInvite, cancelledSlot.Id, "cancelled-booking-hash", clock.UtcNow);
            cancelledBooking.Cancel();
            write.Candidates.AddRange(actionableCandidate, staleCandidate);
            write.ConfirmedSlots.Add(cancelledSlot);
            write.Invites.Add(cancelledInvite);
            write.Bookings.Add(cancelledBooking);
            write.EmailLogs.AddRange(
                outstanding,
                laterSent,
                EmailLog.RecordPending(
                    Guid.NewGuid(),
                    staleCandidate.Id,
                    EmailTemplate.SlotCancelledRebookingNeeded,
                    clock.UtcNow,
                    confirmedSlotId: cancelledSlot.Id));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock)
            .LatestEmailStatusAsync(CancellationToken.None);

        var actionable = rows.Single(row => row.CandidateId == actionableId);
        Assert.True(actionable.CanRetry);
        Assert.Equal(EmailTemplate.SlotCancelledRebookingNeeded, actionable.TemplateName);
        Assert.Equal(EmailStatus.Pending, actionable.Status);
        Assert.False(rows.Single(row => row.CandidateId == staleId).CanRetry);

        await using (var resolve = NewContext(clock))
        {
            var persisted = await resolve.EmailLogs.SingleAsync(delivery => delivery.Id == outstanding.Id);
            persisted.MarkResolved(clock.UtcNow.AddMinutes(2));
            await resolve.SaveChangesAsync();
        }

        await using var reread = NewContext(clock);
        var terminal = (await new DashboardQueries(reread, clock)
            .LatestEmailStatusAsync(CancellationToken.None))
            .Single(row => row.CandidateId == actionableId);
        Assert.Equal(EmailTemplate.CandidateInvite, terminal.TemplateName);
        Assert.Equal(EmailStatus.Sent, terminal.Status);
        Assert.False(terminal.CanRetry);
    }

    private EventBookingDbContext NewContext(IClock clock)
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .AddInterceptors(new StatusStampingInterceptor(clock))
            .Options;

        return new EventBookingDbContext(options);
    }

    private static ConfirmedSlot SlotFor(DateOnly date, TimeOnly startTime)
    {
        var proposal = SlotProposal.Create(Guid.NewGuid(), new SlotWindow(date, startTime), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        return ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
    }
}
