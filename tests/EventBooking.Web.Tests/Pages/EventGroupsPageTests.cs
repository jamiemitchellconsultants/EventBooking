using System.Net;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using EventBooking.Web.Tests.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages;

public sealed class EventGroupsPageTests : BunitContext
{
    private static readonly Guid GroupOptionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TypeId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid EventId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    [Fact]
    public void CreateGroupSendsTitleDescriptionAndSelectedGroups()
    {
        var groups = Register();
        var cut = Render<EventGroups>();

        cut.WaitForElement("button[data-action='new']").Click();
        cut.Find("input[name='title']").Change("Autumn intake");
        cut.Find("textarea[name='description']").Change("Choose a date.");
        cut.Find("input[name='attendee-group']").Change(true);
        cut.Find("button[data-action='save']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Autumn intake", cut.Markup));
        var created = Assert.Single(groups.Created);
        Assert.Equal("Autumn intake", created.Title);
        Assert.Equal("Choose a date.", created.Description);
        Assert.Equal([GroupOptionId], created.GroupIds);
    }

    [Fact]
    public void EditDescriptionToggleGroupGateAddEventAndToggleMember()
    {
        var groups = Register(seedGroup: true);
        var cut = Render<EventGroups>();

        cut.WaitForElement("button[data-action='edit']").Click();
        cut.Find("textarea[name='description']").Change("Updated description.");
        cut.Find("input[name='group-open']").Change(true);
        cut.Find("button[data-action='save']").Click();

        cut.WaitForAssertion(() => Assert.Single(groups.Updated));
        var updated = Assert.Single(groups.Updated);
        Assert.Equal("Updated description.", updated.Description);
        Assert.True(updated.IsOpen);

        cut.WaitForElement("button[data-action='edit']").Click();
        Assert.Equal("Updated description.",
            cut.Find("textarea[name='description']").GetAttribute("value"));
        cut.Find("select[name='add-event']").Change(EventId.ToString("D"));
        cut.Find("button[data-action='add-event']").Click();
        cut.WaitForAssertion(() => Assert.Contains(EventId.ToString("D"), cut.Markup));
        Assert.Equal([(groups.SeededId, EventId)], groups.Added);

        cut.Find("input[name='member-open']").Change(true);
        cut.WaitForAssertion(() => Assert.Contains((true, EventId), groups.MemberToggles.Select(x => (x.Open, x.EventId))));
    }

    [Fact]
    public void VersionConflictPreservesEditorValues()
    {
        var groups = Register(seedGroup: true, conflictOnUpdate: true);
        var cut = Render<EventGroups>();

        cut.WaitForElement("button[data-action='edit']").Click();
        cut.Find("input[name='title']").Change("My unsaved title");
        cut.Find("button[data-action='save']").Click();

        cut.WaitForAssertion(() => Assert.Contains("server is now on version 2", cut.Markup));
        Assert.Equal("My unsaved title", cut.Find("input[name='title']").GetAttribute("value"));
    }

    [Fact]
    public void AdminViewShowsNoAttendeeCounts()
    {
        Register(seedGroup: true);
        var cut = Render<EventGroups>();

        cut.WaitForElement("button[data-action='edit']");
        Assert.DoesNotContain("memberCount", cut.Markup);
        Assert.DoesNotContain("Members", cut.Markup);
        Assert.Contains("Publication", cut.Markup);
    }

    private FakeEventGroupsClient Register(bool seedGroup = false, bool conflictOnUpdate = false)
    {
        var groups = new FakeEventGroupsClient(seedGroup, conflictOnUpdate);
        Services.AddSingleton<IEventGroupsClient>(groups);
        Services.AddSingleton<IEventsClient>(new FakeEventsClient());
        const string attendeeGroups = """{"items":[{"id":"30000000-0000-0000-0000-000000000001","code":"FIELD","name":"Field staff","description":"Field folk.","isActive":true,"version":1,"requirementTypeIds":["40000000-0000-0000-0000-000000000001"],"memberCount":0,"_links":{}}],"nextCursor":null}""";
        const string appointmentTypes = """{"items":[{"id":"40000000-0000-0000-0000-000000000001","code":"MED","name":"Medical check","isActive":true,"version":1,"hasManager":true,"managerDisplayName":"M. Manager","_links":{}}],"nextCursor":null}""";
        var queue = new QueueHandler(
            Json(HttpStatusCode.OK, attendeeGroups),
            Json(HttpStatusCode.OK, appointmentTypes),
            Json(HttpStatusCode.OK, attendeeGroups),
            Json(HttpStatusCode.OK, appointmentTypes),
            Json(HttpStatusCode.OK, attendeeGroups),
            Json(HttpStatusCode.OK, appointmentTypes));
        Services.AddSingleton(new AdminClient(new HttpClient(queue)
            { BaseAddress = new Uri("https://api.example") }));
        Services.AddSingleton<IMeClient>(new FakeMeClient());
        return groups;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
    };

    private sealed class FakeEventGroupsClient(bool seedGroup, bool conflictOnUpdate) : IEventGroupsClient
    {
        public Guid SeededId { get; } = Guid.Parse("60000000-0000-0000-0000-000000000001");
        public List<(string Title, string Description, IReadOnlyList<Guid> GroupIds)> Created { get; } = [];
        public List<EventGroupDto> Updated { get; } = [];
        public List<(Guid GroupId, Guid EventId)> Added { get; } = [];
        public List<(Guid EventId, bool Open)> MemberToggles { get; } = [];
        private readonly List<EventGroupDto> _items = seedGroup
            ? [new EventGroupDto(
                Guid.Parse("60000000-0000-0000-0000-000000000001"), "Open days", "Original.",
                false, 1, [GroupOptionId], [TypeId], [])]
            : [];

        public Task<ApiOutcome<PageDto<EventGroupDto>>> ListAsync(CancellationToken ct) =>
            Task.FromResult(ApiOutcome<PageDto<EventGroupDto>>.Success(new([.. _items], null)));

        public Task<ApiOutcome<EventGroupDto>> CreateAsync(string title, string description,
            IReadOnlyList<Guid> groupIds, IdempotencySubmission submission, CancellationToken ct)
        {
            Created.Add((title, description, groupIds));
            var created = new EventGroupDto(Guid.NewGuid(), title, description, false, 1,
                groupIds, [TypeId], []);
            _items.Add(created);
            return Task.FromResult(ApiOutcome<EventGroupDto>.Success(created));
        }

        public Task<ApiOutcome<EventGroupDto>> UpdateAsync(EventGroupDto value, CancellationToken ct)
        {
            if (conflictOnUpdate)
                return Task.FromResult(ApiOutcome<EventGroupDto>.Failure(ApiProblem.FromSlug(
                    "version-conflict", "Another staff member changed this event group.",
                    new Dictionary<string, object?> { ["current"] = new { currentVersion = 2 } })));
            Updated.Add(value);
            var stored = _items.FindIndex(x => x.Id == value.Id);
            var next = value with { Version = value.Version + 1 };
            if (stored >= 0) _items[stored] = next;
            return Task.FromResult(ApiOutcome<EventGroupDto>.Success(next));
        }

        public Task<ApiOutcome<EventGroupDto>> SetEventOpenAsync(Guid groupId, Guid eventId,
            bool open, long expectedVersion, CancellationToken ct)
        {
            MemberToggles.Add((eventId, open));
            var stored = _items.FindIndex(x => x.Id == groupId);
            var next = _items[stored] with
            {
                Version = _items[stored].Version + 1,
                Events = [.. _items[stored].Events.Select(e =>
                    e.EventId == eventId ? e with { IsOpen = open } : e)],
            };
            _items[stored] = next;
            return Task.FromResult(ApiOutcome<EventGroupDto>.Success(next));
        }

        public Task<ApiOutcome<EventGroupDto>> AddEventAsync(Guid groupId, Guid eventId,
            long expectedVersion, CancellationToken ct)
        {
            Added.Add((groupId, eventId));
            var stored = _items.FindIndex(x => x.Id == groupId);
            var next = _items[stored] with
            {
                Version = _items[stored].Version + 1,
                Events = [.. _items[stored].Events, new EventGroupEventDto(eventId, false)],
            };
            _items[stored] = next;
            return Task.FromResult(ApiOutcome<EventGroupDto>.Success(next));
        }

        public Task<ApiOutcome<EventGroupDto>> RemoveEventAsync(Guid groupId, Guid eventId,
            long expectedVersion, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeEventsClient : IEventsClient
    {
        private EventDto Event => new(EventId, Guid.NewGuid(), Guid.NewGuid(), "LON", "London HQ",
            TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Active",
            [new EventCapacityDto(TypeId, "MED", "Medical check", 6, 3, new Dictionary<string, ApiLink>())],
            3, new Dictionary<string, ApiLink>());

        public Task<ApiOutcome<PageDto<EventDto>>> ListEventsAsync(Guid? locationId, DateOnly? from,
            DateOnly? to, string? cursor, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<PageDto<EventDto>>.Success(new([Event], null)));

        public Task<ApiOutcome<NegotiationReferenceData>> GetReferenceDataAsync(CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ApiOutcome<PageDto<EventProposalDto>>> ListProposalsAsync(string? cursor, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ApiOutcome<ProposeEventOutcome>> ProposeAsync(ProposeEventRequest request, IdempotencySubmission submission, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ApiOutcome<RecordAcceptanceOutcome>> RecordAcceptanceAsync(Guid proposalId, int headcount, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ApiOutcome<object>> WithdrawAcceptanceAsync(Guid proposalId, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ApiOutcome<object>> WithdrawProposalAsync(Guid proposalId, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ApiOutcome<AdjustEventCapacityOutcome>> AdjustCapacityAsync(Guid eventId, Guid appointmentTypeId, int totalHeadcount, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ApiOutcome<CancelEventOutcome>> CancelAsync(Guid eventId, bool confirm, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMeClient : IMeClient
    {
        public Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct) => Task.FromResult(
            ApiOutcome<MeDto>.Success(new(
                "Admin User", "A1", ["Admin"], null, null, null,
                ["ManageEventGroups"], null,
                new Dictionary<string, ApiLink>
                {
                    ["createEventGroup"] = new("/api/event-groups", "POST", "createEventGroup"),
                })));
    }

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_responses.Dequeue());
    }
}
