using EventBooking.Application.ReadModels;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests.Queries;

[Collection("postgres")]
public class AttendeeListQueryTests(PostgresFixture fixture)
{
    private static readonly CallerShape Coordinator =
        new(Guid.NewGuid(), false, new HashSet<string> { "Coordinator" });

    [Fact]
    public async Task Filters_combine_rather_than_replacing_one_another()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();

        var group = await AddGroupAsync(context, "PILOTS");
        await AddAttendeeAsync(context, "Amy", AttendeeStatus.AwaitingAvailability, group);
        await AddAttendeeAsync(context, "Amos", AttendeeStatus.Booked, group);
        await AddAttendeeAsync(context, "Bea", AttendeeStatus.AwaitingAvailability, group);

        var page = await new AttendeeListQueries(context).ListAttendeesAsync(
            Coordinator, null, 50, "AwaitingAvailability", group, null, "a",
            CancellationToken.None);

        // Status AND group AND prefix: Amos fails the status, Bea fails the prefix.
        Assert.Equal("Amy", Assert.Single(page.Items).Name);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task The_cursor_round_trips_and_pages_do_not_overlap()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();

        var group = await AddGroupAsync(context, "PILOTS");
        foreach (var name in new[] { "Amy", "Ben", "Cat", "Dan", "Eve" })
            await AddAttendeeAsync(context, name, AttendeeStatus.AwaitingAvailability, group);

        var queries = new AttendeeListQueries(context);
        var first = await queries.ListAttendeesAsync(
            Coordinator, null, 2, null, null, null, null, CancellationToken.None);
        Assert.NotNull(first.NextCursor);

        // A row inserted between the pages sorts before the cursor and must not shift
        // the second page: that is what keyset paging buys over an offset.
        await AddAttendeeAsync(context, "Abe", AttendeeStatus.AwaitingAvailability, group);

        var second = await queries.ListAttendeesAsync(
            Coordinator, first.NextCursor, 2, null, null, null, null, CancellationToken.None);

        Assert.Equal(["Amy", "Ben"], first.Items.Select(i => i.Name));
        Assert.Equal(["Cat", "Dan"], second.Items.Select(i => i.Name));

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task Required_codes_show_only_before_an_invite_exists()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();

        var group = await AddGroupAsync(context, "PILOTS");
        await AddAttendeeAsync(context, "Amy", AttendeeStatus.AwaitingAvailability, group);
        await AddAttendeeAsync(context, "Ben", AttendeeStatus.Invited, group);
        await AddAttendeeAsync(context, "Cal", AttendeeStatus.NotYetInvited, group);

        var page = await new AttendeeListQueries(context).ListAttendeesAsync(
            Coordinator, null, 50, null, null, null, null, CancellationToken.None);

        Assert.NotEmpty(page.Items.Single(i => i.Name == "Amy").RequiredTypeCodes);
        Assert.NotEmpty(page.Items.Single(i => i.Name == "Cal").RequiredTypeCodes);
        // Once invited, the invite's own snapshot is the authority; showing the live set
        // beside an invited row would contradict it.
        Assert.Empty(page.Items.Single(i => i.Name == "Ben").RequiredTypeCodes);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task The_latest_delivery_id_travels_with_its_status_so_it_can_be_retried()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var group = await AddGroupAsync(context, "PILOTS");
        var attendeeId = await AddAttendeeAsync(context, "Amy", AttendeeStatus.Invited, group);
        var at = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var failed = EventBooking.Domain.Notifications.EmailLog.RecordPending(
            Guid.NewGuid(), attendeeId, EventBooking.Domain.Notifications.EmailTemplate.AttendeeInvite, at);
        failed.MarkFailed(at.AddMinutes(1));
        context.EmailLogs.Add(failed);
        await context.SaveChangesAsync();

        var page = await new AttendeeListQueries(context).ListAttendeesAsync(
            Coordinator, null, 50, null, null, null, null, CancellationToken.None);

        var row = Assert.Single(page.Items);
        Assert.Equal("Failed", row.LatestDeliveryStatus);
        Assert.Equal(failed.Id, row.LatestDeliveryId);

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task An_admin_shaped_caller_reads_no_attendees()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var group = await AddGroupAsync(context, "PILOTS");
        await AddAttendeeAsync(context, "Amy", AttendeeStatus.AwaitingAvailability, group);

        var page = await new AttendeeListQueries(context).ListAttendeesAsync(
            new CallerShape(Guid.NewGuid(), true, new HashSet<string> { "Admin" }),
            null, 50, null, null, null, null, CancellationToken.None);

        Assert.Empty(page.Items);

        await fixture.ResetAsync();
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task A_page_of_fifty_thousand_rows_returns_within_its_budget()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();

        // Seeded mostly outside the filter so the index decides the plan rather than the
        // table being small enough to scan — the same correction Task 11's performance
        // scenario needed.
        await SeedManyAsync(context, awaiting: 2_000, others: 48_000);

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var page = await new AttendeeListQueries(context).ListAttendeesAsync(
            Coordinator, null, 50, "AwaitingAvailability", null, null, null,
            CancellationToken.None);
        timer.Stop();

        Assert.Equal(50, page.Items.Count);
        Assert.True(
            timer.Elapsed.TotalMilliseconds < 300,
            $"The page took {timer.Elapsed.TotalMilliseconds:F0} ms, above the 300 ms budget.");

        await fixture.ResetAsync();
    }

    [Fact]
    public async Task A_readiness_filter_shortens_the_page_without_ending_it()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();

        var group = await AddGroupAsync(context, "PILOTS");
        await AddAttendeeAsync(context, "ra-able", AttendeeStatus.AwaitingAvailability, group);
        await AddAttendeeAsync(context, "ra-baker", AttendeeStatus.Booked, group);
        await AddAttendeeAsync(context, "ra-charlie", AttendeeStatus.AwaitingAvailability, group);
        await AddAttendeeAsync(context, "ra-dog", AttendeeStatus.Booked, group);

        var queries = new AttendeeListQueries(context);
        var first = await queries.ListAttendeesAsync(
            Coordinator, null, 2, null, null, "NoActiveBooking", null, CancellationToken.None);

        // Two of the three rows read match, so the page is short but paging continues.
        Assert.Equal(["ra-able", "ra-charlie"], first.Items.Select(i => i.Name));
        Assert.NotNull(first.NextCursor);

        var second = await queries.ListAttendeesAsync(
            Coordinator, first.NextCursor, 2, null, null, "NoActiveBooking", null,
            CancellationToken.None);

        Assert.Empty(second.Items);
        Assert.Null(second.NextCursor);

        await fixture.ResetAsync();
    }

    private static async Task<Guid> AddGroupAsync(EventBookingDbContext context, string code)
    {
        var group = await context.AttendeeGroups.SingleAsync(g => g.Code == code);
        return group.Id;
    }

    private static async Task<Guid> AddAttendeeAsync(
        EventBookingDbContext context, string name, AttendeeStatus status, Guid groupId)
    {
        var group = await context.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == groupId);
        var at = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var attendee = Attendee.Create(
            Guid.NewGuid(), name, $"{name.ToLowerInvariant()}@example.invalid", group,
            at.AddDays(-30));
        switch (status)
        {
            case AttendeeStatus.NotYetInvited:
                break;
            case AttendeeStatus.AwaitingAvailability:
                attendee.MarkAwaitingAvailability(at.AddDays(-30));
                break;
            case AttendeeStatus.NoResponseNeedsFollowUp:
                attendee.MarkInvited(at.AddDays(-30));
                attendee.MarkNoResponse(at.AddDays(-29));
                break;
            case AttendeeStatus.Booked:
                attendee.MarkInvited(at.AddDays(-30));
                attendee.MarkBooked(at.AddDays(-29));
                break;
            default:
                attendee.MarkInvited(at.AddDays(-30));
                break;
        }

        context.Attendees.Add(attendee);
        await context.SaveChangesAsync();
        return attendee.Id;
    }

    private static async Task SeedManyAsync(
        EventBookingDbContext context, int awaiting, int others)
    {
        var group = await context.AttendeeGroups
            .SingleAsync(g => g.Code == "PILOTS");
        var at = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO attendee (id, name, email, status, attendee_group_id, status_changed_at)
            SELECT gen_random_uuid(),
                   'perf-await-' || g,
                   'perf-await-' || g || '@example.invalid',
                   {(int)AttendeeStatus.AwaitingAvailability},
                   {group.Id},
                   {at}
              FROM generate_series(1, {awaiting}) g
            """);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO attendee (id, name, email, status, attendee_group_id, status_changed_at)
            SELECT gen_random_uuid(),
                   'perf-other-' || g,
                   'perf-other-' || g || '@example.invalid',
                   {(int)AttendeeStatus.Invited},
                   {group.Id},
                   {at}
              FROM generate_series(1, {others}) g
            """);
    }
}
