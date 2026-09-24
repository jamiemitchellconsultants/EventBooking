using EventBooking.Application.ReadModels;
using EventBooking.Domain.Audit;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests.Queries;

[Collection("postgres")]
public class AuditSearchQueryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly CallerShape Caller =
        new(Guid.NewGuid(), false, new HashSet<string> { "Coordinator" });

    [Fact]
    public async Task Event_bucket_excludes_attendee_rows()
    {
        await fixture.ResetAsync();
        await SeedBothBucketsAsync();

        await using var read = fixture.NewContext();
        var page = await new AuditSearchQueries(read).SearchAsync(
            Caller, ["event"], null, 50, null, null, null, null, null,
            CancellationToken.None);

        Assert.NotEmpty(page.Items);
        Assert.DoesNotContain(page.Items, r =>
            r.EntityType is "Attendee" or "Invite" or "Booking");
        Assert.Contains(page.Items, r => r.EntityType == "Event");
        Assert.Contains(page.Items, r => r.EntityType == "Location");
        Assert.Contains(page.Items, r => r.EntityType == "SystemSettings");
    }

    [Fact]
    public async Task Attendee_bucket_excludes_event_rows()
    {
        await fixture.ResetAsync();
        await SeedBothBucketsAsync();

        await using var read = fixture.NewContext();
        var page = await new AuditSearchQueries(read).SearchAsync(
            Caller, ["attendee"], null, 50, null, null, null, null, null,
            CancellationToken.None);

        Assert.NotEmpty(page.Items);
        Assert.DoesNotContain(page.Items, r =>
            r.EntityType is "Event" or "Location" or "SystemSettings");
        Assert.Contains(page.Items, r => r.EntityType == "Attendee");
        Assert.Contains(page.Items, r => r.EntityType == "Invite");
    }

    [Fact]
    public async Task Combined_filters_compose()
    {
        await fixture.ResetAsync();
        var target = Guid.NewGuid();
        await using (var write = fixture.NewContext())
        {
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, target,
                AuditAction.EventConfirmed, ActorType.Staff, "staff-1",
                Now, "1 event confirmed"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, target,
                AuditAction.EventCancelled, ActorType.Staff, "staff-2",
                Now.AddHours(1), "6 bookings voided"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, Guid.NewGuid(),
                AuditAction.EventConfirmed, ActorType.Staff, "staff-3",
                Now, "other event"));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AuditSearchQueries(read);
        var page = await queries.SearchAsync(
            Caller, ["event"], null, 50, "Event", "EventCancelled",
            Now.AddMinutes(30), Now.AddHours(2), target, CancellationToken.None);

        var row = Assert.Single(page.Items);
        Assert.Equal("EventCancelled", row.Action);
        Assert.Equal(target, await EntityIdOfAsync(read, row.Id));
    }

    [Fact]
    public async Task Pages_do_not_overlap_under_concurrent_inserts()
    {
        await fixture.ResetAsync();
        await using (var write = fixture.NewContext())
        {
            for (var i = 0; i < 30; i++)
            {
                write.AuditLogs.Add(AuditLog.Record(
                    Guid.NewGuid(), AuditEntityTypes.Event, Guid.NewGuid(),
                    AuditAction.EventConfirmed, ActorType.Staff, "staff-seed",
                    Now.AddMinutes(-i), $"seed row {i}"));
            }

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var queries = new AuditSearchQueries(read);
        var first = await queries.SearchAsync(
            Caller, ["event"], null, 10, null, null, null, null, null,
            CancellationToken.None);

        await using (var write = fixture.NewContext())
        {
            for (var i = 30; i < 40; i++)
            {
                write.AuditLogs.Add(AuditLog.Record(
                    Guid.NewGuid(), AuditEntityTypes.Event, Guid.NewGuid(),
                    AuditAction.EventConfirmed, ActorType.Staff, "staff-seed",
                    Now.AddMinutes(-i), $"seed row {i}"));
            }

            await write.SaveChangesAsync();
        }

        var second = await queries.SearchAsync(
            Caller, ["event"], first.NextCursor, 10, null, null, null, null, null,
            CancellationToken.None);

        Assert.Empty(first.Items.Select(i => i.Cursor)
            .Intersect(second.Items.Select(i => i.Cursor)));
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(10, second.Items.Count);
    }

    [Fact]
    public async Task No_stored_details_contain_personal_data()
    {
        await fixture.ResetAsync();
        await SeedBothBucketsAsync();

        await using var read = fixture.NewContext();
        var details = await read.AuditLogs
            .Select(a => a.Details)
            .ToListAsync(CancellationToken.None);

        Assert.NotEmpty(details);
        Assert.DoesNotContain(details, d => d != null && d.Contains('@'));
    }

    [Fact]
    public async Task Attendee_history_returns_only_that_attendees_rows()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        await using (var write = fixture.NewContext())
        {
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Attendee, attendeeId,
                AuditAction.AttendeeGroupAssigned, ActorType.Staff, "staff-1",
                Now, "group PILOTS"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Attendee, Guid.NewGuid(),
                AuditAction.AttendeeGroupAssigned, ActorType.Staff, "staff-2",
                Now, "group CREW"));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var page = await new AuditSearchQueries(read).HistoryAsync(
            Caller, ["attendee"], attendeeId, null, 50, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("Attendee", page.Items[0].EntityType);
    }

    private async Task SeedBothBucketsAsync()
    {
        await using var write = fixture.NewContext();
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.Event, Guid.NewGuid(),
            AuditAction.EventConfirmed, ActorType.Staff, "staff-1",
            Now, "1 event confirmed"));
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.Location, Guid.NewGuid(),
            AuditAction.LocationCreated, ActorType.Staff, "staff-1",
            Now.AddMinutes(1), "code LON"));
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.SystemSettings, Guid.NewGuid(),
            AuditAction.SystemSettingsChanged, ActorType.Staff, "staff-1",
            Now.AddMinutes(2), "setting theme"));
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.Attendee, Guid.NewGuid(),
            AuditAction.AttendeeGroupAssigned, ActorType.Staff, "staff-2",
            Now.AddMinutes(3), "group PILOTS"));
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(),
            AuditAction.InviteCreated, ActorType.Staff, "staff-2",
            Now.AddMinutes(4), "invite issued"));
        await write.SaveChangesAsync();
    }

    private static async Task<Guid> EntityIdOfAsync(
        Persistence.EventBookingDbContext read, Guid id) =>
        (await read.AuditLogs.FindAsync([id], CancellationToken.None))!.EntityId;
}
