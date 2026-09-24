using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;
using EventBooking.Application.ReadModels;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;

namespace EventBooking.Application.Tests.Attendees;

public sealed class AttendeeListHandlerTests
{
    [Fact]
    public async Task Filters_combine_and_required_codes_show_on_awaiting_rows()
    {
        var queries = new MemoryAttendeeQueries()
            .WithAttendee("Amy", "amy@example.invalid", "AwaitingAvailability", "NHS", ["MED", "IND"])
            .WithAttendee("Bo", "bo@example.invalid", "Invited", "NHS", ["MED"]);
        var (handler, staff) = Handler(queries);

        var result = await handler.HandleAsync(new ListAttendeesQuery(
            staff, null, 50, "AwaitingAvailability", null, null, "a"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("Amy", item.Name);
        Assert.Equal(["IND", "MED"], item.RequiredTypeCodes.Order().ToList());
        Assert.NotNull(item.Cursor);
    }

    [Fact]
    public async Task Pages_do_not_overlap_under_concurrent_inserts()
    {
        var queries = new MemoryAttendeeQueries().WithAttendees(30);
        var (handler, staff) = Handler(queries);

        var first = await handler.HandleAsync(
            new ListAttendeesQuery(staff, null, 10, null, null, null, null),
            CancellationToken.None);
        queries.WithAttendees(10);
        var second = await handler.HandleAsync(
            new ListAttendeesQuery(staff, first.Value.NextCursor, 10, null, null, null, null),
            CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Empty(first.Value.Items.Select(i => i.Cursor)
            .Intersect(second.Value.Items.Select(i => i.Cursor)));
    }

    [Fact]
    public async Task Admin_without_a_coordinator_profile_is_forbidden()
    {
        // Admin is exclusive with every other role, so no profile can pass the
        // handler's Coordinator gate while Admin-shaped: the handler refuses here, and
        // the Infrastructure suite proves the read model refuses an Admin shape
        // presented to it directly.
        var queries = new AdminShapedAttendeeQueries();
        var profiles = new InMemoryStaffAccessProfileRepository();
        var staff = Guid.NewGuid();
        profiles.Add(StaffAccessProfile.Create(staff, Role.Admin, null));
        var handler = new ListAttendeesHandler(queries, profiles);

        var result = await handler.HandleAsync(
            new ListAttendeesQuery(staff, null, 50, null, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private static (ListAttendeesHandler Handler, Guid Staff) Handler(IAttendeeListQueries queries)
    {
        var profiles = new InMemoryStaffAccessProfileRepository();
        var staff = Guid.NewGuid();
        profiles.Add(StaffAccessProfile.Create(staff, Role.Coordinator, null));
        return (new ListAttendeesHandler(queries, profiles), staff);
    }

    private sealed class MemoryAttendeeQueries : IAttendeeListQueries
    {
        private readonly List<Row> _rows = [];
        private readonly Dictionary<string, Guid> _groupIds = [];
        private int _next;

        public MemoryAttendeeQueries WithAttendee(
            string name, string email, string status, string groupCode, string[] codes)
        {
            _rows.Add(new Row(
                Guid.NewGuid(), name, email,
                Enum.Parse<AttendeeStatus>(status), groupCode, codes));
            return this;
        }

        public MemoryAttendeeQueries WithAttendees(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var name = $"Attendee{_next++:D4}";
                _rows.Add(new Row(
                    Guid.NewGuid(), name, $"{name.ToLowerInvariant()}@example.invalid",
                    AttendeeStatus.Invited, "NHS", ["MED"]));
            }

            return this;
        }

        public Task<AttendeeListView> ListAttendeesAsync(
            CallerShape shape, string? cursor, int limit, string? status,
            Guid? attendeeGroupId, string? readiness, string? nameOrEmailPrefix,
            CancellationToken ct)
        {
            if (shape.IsAdmin)
            {
                return Task.FromResult(new AttendeeListView([], null));
            }

            AttendeeStatus? statusValue = null;
            if (status is not null)
            {
                if (!Enum.TryParse<AttendeeStatus>(status, ignoreCase: true, out var parsed))
                {
                    return Task.FromResult(new AttendeeListView([], null));
                }

                statusValue = parsed;
            }

            var ordered = _rows
                .Where(r => statusValue is null || r.Status == statusValue)
                .Where(r => attendeeGroupId is null || GroupId(r.GroupCode) == attendeeGroupId)
                .Where(r => nameOrEmailPrefix is null
                    || r.Name.StartsWith(nameOrEmailPrefix, StringComparison.OrdinalIgnoreCase)
                    || r.Email.StartsWith(nameOrEmailPrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Name.ToLowerInvariant())
                .ThenBy(r => r.Id)
                .ToList();

            if (cursor is not null)
            {
                if (!AttendeeCursor.TryDecode(cursor, out var sortKey, out var cursorId))
                {
                    return Task.FromResult(new AttendeeListView([], null));
                }

                ordered = [.. ordered.Where(r =>
                    string.Compare(r.Name.ToLowerInvariant(), sortKey, StringComparison.Ordinal) > 0
                    || (r.Name.ToLowerInvariant() == sortKey && r.Id.CompareTo(cursorId) > 0))];
            }

            var readItems = ordered
                .Take(limit + 1)
                .Select(r => new AttendeeListItem(
                    r.Id,
                    r.Name,
                    r.Email,
                    r.Status.ToString(),
                    r.GroupCode,
                    AttendeeReadiness.Of(r.Status, null),
                    r.Status is AttendeeStatus.NotYetInvited or AttendeeStatus.AwaitingAvailability
                        ? r.Codes
                        : [],
                    null,
                    AttendeeCursor.Encode(r.Name.ToLowerInvariant(), r.Id)))
                .ToList();
            var matched = readiness is null
                ? readItems
                : readItems.Where(i => string.Equals(
                    i.Readiness, readiness, StringComparison.OrdinalIgnoreCase)).ToList();

            // The keyset advances by the last row actually read, so a readiness filter
            // shortens the page without ending it while rows remain.
            if (readItems.Count <= limit)
            {
                return Task.FromResult(new AttendeeListView(matched, null));
            }

            if (matched.Count > limit)
            {
                var page = matched.Take(limit).ToList();
                return Task.FromResult(new AttendeeListView(page, page[^1].Cursor));
            }

            return Task.FromResult(new AttendeeListView(
                matched, (matched.Count == 0 ? readItems[^1] : matched[^1]).Cursor));
        }

        private Guid GroupId(string code)
        {
            if (!_groupIds.TryGetValue(code, out var id))
            {
                id = Guid.NewGuid();
                _groupIds[code] = id;
            }

            return id;
        }

        private sealed record Row(
            Guid Id, string Name, string Email, AttendeeStatus Status, string GroupCode,
            IReadOnlyList<string> Codes);
    }

    private sealed class AdminShapedAttendeeQueries : IAttendeeListQueries
    {
        public Task<AttendeeListView> ListAttendeesAsync(
            CallerShape shape, string? cursor, int limit, string? status,
            Guid? attendeeGroupId, string? readiness, string? nameOrEmailPrefix,
            CancellationToken ct) =>
            Task.FromResult(new AttendeeListView([], null));
    }
}
