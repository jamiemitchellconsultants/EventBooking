using EventBooking.Application.Abstractions;
using EventBooking.Domain.Audit;
using EventBooking.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class EfAuditLoggerTests(PostgresFixture fixture)
{
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as transitional-location time.</summary>
        public DateTimeOffset NowAtTransitionalLocation => now;

        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(now);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AStagedEntryIsWrittenWhenTheUnitOfWorkSaves()
    {
        await fixture.ResetAsync();
        var entityId = Guid.NewGuid();

        await using (var context = fixture.NewContext())
        {
            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.Booking, entityId, AuditAction.BookingCreated,
                ActorType.AttendeeToken, "invite-1", "chose option 2");

            await context.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var entry = await read.AuditLogs.SingleAsync();
        Assert.Equal(AuditEntityTypes.Booking, entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(AuditAction.BookingCreated, entry.Action);
        Assert.Equal(ActorType.AttendeeToken, entry.ActorType);
        Assert.Equal("invite-1", entry.ActorId);
        Assert.Equal(Now, entry.Timestamp);
        Assert.Equal("chose option 2", entry.Details);
    }

    [Fact]
    public async Task AStagedEntryIsLostWhenTheTransactionRollsBack()
    {
        await fixture.ResetAsync();

        await using (var context = fixture.NewContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventCancelled,
                ActorType.Staff, Guid.NewGuid().ToString());

            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Equal(0, await read.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ASystemActorNeedsNoIdentifier()
    {
        await fixture.ResetAsync();

        await using (var context = fixture.NewContext())
        {
            new EfAuditLogger(context, new FixedClock(Now)).Record(
                AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteExpired,
                ActorType.System, null);

            await context.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        Assert.Null((await read.AuditLogs.SingleAsync()).ActorId);
    }
}
