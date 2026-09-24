using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Audit;
using EventBooking.Application.Common;
using EventBooking.Application.ReadModels;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Audit;

public sealed class AuditSearchHandlerTests
{
    [Fact]
    public async Task Event_bucket_caller_never_sees_attendee_rows()
    {
        var queries = new MemoryAuditQueries()
            .WithRow("Attendee", "InviteCreated")
            .WithRow("Event", "EventConfirmed");
        var profiles = new InMemoryStaffAccessProfileRepository();
        var auditor = Guid.NewGuid();
        profiles.Items.Add(StaffAccessProfile.Create(auditor, Role.Admin, null));
        var handler = new SearchAuditHandler(queries, profiles);

        var result = await handler.HandleAsync(new SearchAuditQuery(
            auditor, null, 50, null, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value.Items, r => r.EntityType == "Attendee");
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task Reference_data_entries_sit_in_event_bucket()
    {
        var queries = new MemoryAuditQueries()
            .WithRow("Location", "LocationCreated")
            .WithRow("SystemSettings", "SystemSettingsChanged");
        var profiles = new InMemoryStaffAccessProfileRepository();
        var auditor = Guid.NewGuid();
        profiles.Items.Add(StaffAccessProfile.Create(auditor, Role.Admin, null));
        var handler = new SearchAuditHandler(queries, profiles);

        var result = await handler.HandleAsync(new SearchAuditQuery(
            auditor, null, 50, null, null, null, null, null), CancellationToken.None);

        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task No_returned_row_carries_personal_data()
    {
        var queries = new MemoryAuditQueries().WithFullHistory();
        var profiles = new InMemoryStaffAccessProfileRepository();
        var auditor = Guid.NewGuid();
        profiles.Items.Add(StaffAccessProfile.Create(auditor, Role.Coordinator, null));
        var handler = new SearchAuditHandler(queries, profiles);

        var result = await handler.HandleAsync(new SearchAuditQuery(
            auditor, null, 200, null, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value.Items, r => r.ContainsPersonalData());
    }

    [Fact]
    public async Task Pages_do_not_overlap_under_concurrent_inserts()
    {
        var queries = new MemoryAuditQueries().WithRows(30);
        var profiles = new InMemoryStaffAccessProfileRepository();
        var auditor = Guid.NewGuid();
        profiles.Items.Add(StaffAccessProfile.Create(auditor, Role.Coordinator, null));
        var handler = new SearchAuditHandler(queries, profiles);

        var first = await handler.HandleAsync(new SearchAuditQuery(
            auditor, null, 10, null, null, null, null, null), CancellationToken.None);
        queries.WithRows(10);
        var second = await handler.HandleAsync(new SearchAuditQuery(
            auditor, first.Value.NextCursor, 10, null, null, null, null, null),
            CancellationToken.None);

        Assert.Empty(first.Value.Items.Select(i => i.Cursor)
            .Intersect(second.Value.Items.Select(i => i.Cursor)));
    }

    private sealed record StoredRow(
        Guid Id, Guid EntityId, string EntityType, string Action, string ActorType,
        DateTimeOffset OccurredAt, string? Details);

    private sealed class MemoryAuditQueries : IAuditSearchQueries
    {
        private static readonly DateTimeOffset Base =
            new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

        private readonly List<StoredRow> rows = [];

        public MemoryAuditQueries WithRow(string entityType, string action) =>
            WithRow(Guid.NewGuid(), entityType, action, "Staff", null);

        public MemoryAuditQueries WithRow(
            Guid entityId, string entityType, string action, string actorType,
            string? details)
        {
            rows.Add(new StoredRow(
                Guid.NewGuid(), entityId, entityType, action, actorType,
                Base.AddMinutes(-rows.Count), details));
            return this;
        }

        public MemoryAuditQueries WithRows(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var entityType = i % 2 == 0 ? "Event" : "Attendee";
                WithRow(Guid.NewGuid(), entityType, "EventConfirmed", "Staff", null);
            }

            return this;
        }

        public MemoryAuditQueries WithFullHistory()
        {
            foreach (var action in Enum.GetNames(typeof(Domain.Audit.AuditAction)))
            {
                WithRow(Guid.NewGuid(), "Event", action, "Staff", "fixed identifiers only");
            }

            return this;
        }

        public Task<AuditSearchView> SearchAsync(
            CallerShape shape, IReadOnlyList<string> buckets, string? cursor, int limit,
            string? entityType, string? action, DateTimeOffset? from, DateTimeOffset? to,
            Guid? entityId, CancellationToken ct)
        {
            var page = Page(
                buckets, cursor, limit,
                r => (entityType is null || r.EntityType == entityType)
                    && (action is null || r.Action == action)
                    && (from is null || r.OccurredAt >= from)
                    && (to is null || r.OccurredAt <= to)
                    && (entityId is null || r.EntityId == entityId));
            return Task.FromResult(page);
        }

        public Task<AuditSearchView> HistoryAsync(
            CallerShape shape, IReadOnlyList<string> buckets, Guid entityId,
            string? cursor, int limit, CancellationToken ct) =>
            Task.FromResult(Page(
                buckets, cursor, limit, r => r.EntityId == entityId));

        private AuditSearchView Page(
            IReadOnlyList<string> buckets, string? cursor, int limit,
            Func<StoredRow, bool> extra)
        {
            DateTimeOffset? cursorAt = null;
            Guid? cursorId = null;
            if (AttendeeCursor.TryDecode(cursor, out var sortKey, out var decodedId)
                && DateTimeOffset.TryParse(sortKey, out var decodedAt))
            {
                cursorAt = decodedAt.ToUniversalTime();
                cursorId = decodedId;
            }

            var ordered = rows
                .Where(r => BucketOf(r.EntityType) is string bucket
                    && buckets.Contains(bucket))
                .Where(extra)
                .Where(r => cursorAt is null
                    || r.OccurredAt < cursorAt
                    || (r.OccurredAt == cursorAt && r.Id.CompareTo(CursorIdOrEmpty(cursorId)) < 0))
                .OrderByDescending(r => r.OccurredAt)
                .ThenByDescending(r => r.Id)
                .Take(limit + 1)
                .ToList();

            var items = ordered.Take(limit)
                .Select(r => new AuditRow(
                    r.Id, r.EntityType, r.Action, r.ActorType, r.OccurredAt,
                    AttendeeCursor.Encode(
                        r.OccurredAt.ToUniversalTime().ToString("o"), r.Id)))
                .ToList();
            return new AuditSearchView(
                items, ordered.Count > limit ? items[^1].Cursor : null);
        }

        private static Guid CursorIdOrEmpty(Guid? cursorId) => cursorId ?? Guid.Empty;

        private static string? BucketOf(string entityType) => entityType switch
        {
            "Attendee" or "Invite" or "Booking" => "attendee",
            "Location" or "AppointmentType" or "AttendeeGroup" or "SystemSettings"
                or "EventProposal" or "Event" or "BookingAppointment"
                or "StaffAccessProfile" => "event",
            _ => null,
        };
    }
}

internal static class AuditRowTestExtensions
{
    internal static bool ContainsPersonalData(this AuditRow row) =>
        row.EntityType.Contains('@', StringComparison.Ordinal)
        || row.Action.Contains('@', StringComparison.Ordinal)
        || row.ActorType.Contains('@', StringComparison.Ordinal);
}
