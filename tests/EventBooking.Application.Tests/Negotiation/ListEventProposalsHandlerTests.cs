using EventBooking.Application.Negotiation;
using EventBooking.Application.ReadModels;
using EventBooking.Domain.Access;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Negotiation;

public sealed class ListEventProposalsHandlerTests
{
    [Fact]
    public async Task A_manager_lists_their_own_types_proposals()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var queries = new RecordingProposalQueries();
        var handler = new ListEventProposalsHandler(fixture.Profiles, queries);

        var result = await handler.HandleAsync(
            new ListEventProposalsQuery(fixture.Managers["MED"], null, null, null, 50),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.TypeIds["MED"], queries.LastActingType);
    }

    [Fact]
    public async Task A_caller_without_negotiation_is_forbidden()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var coordinator = Guid.NewGuid();
        fixture.Profiles.Add(StaffAccessProfile.Create(coordinator, Role.Coordinator, null));
        var handler = new ListEventProposalsHandler(fixture.Profiles, new RecordingProposalQueries());

        var result = await handler.HandleAsync(
            new ListEventProposalsQuery(coordinator, null, null, null, 50),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task A_manager_without_a_type_scope_is_forbidden()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var handler = new ListEventProposalsHandler(fixture.Profiles, new RecordingProposalQueries());

        var result = await handler.HandleAsync(
            new ListEventProposalsQuery(fixture.NullScopedManager, null, null, null, 50),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task A_misspelt_status_is_a_validation_error_not_an_empty_page()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var queries = new RecordingProposalQueries();
        var handler = new ListEventProposalsHandler(fixture.Profiles, queries);

        var result = await handler.HandleAsync(
            new ListEventProposalsQuery(fixture.Managers["MED"], "open", null, null, 50),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Null(queries.LastActingType);
    }

    [Fact]
    public async Task A_valid_status_reaches_the_query()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var queries = new RecordingProposalQueries();
        var handler = new ListEventProposalsHandler(fixture.Profiles, queries);

        var result = await handler.HandleAsync(
            new ListEventProposalsQuery(fixture.Managers["MED"], "Open", null, null, 50),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EventProposalStatus.Open, queries.LastStatus);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task A_limit_outside_bounds_is_a_validation_error(int limit)
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var handler = new ListEventProposalsHandler(
            fixture.Profiles, new RecordingProposalQueries());

        var result = await handler.HandleAsync(
            new ListEventProposalsQuery(fixture.Managers["MED"], null, null, null, limit),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task An_over_read_page_trims_to_the_limit()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var queries = new RecordingProposalQueries
        {
            Rows =
            [
                Item("2026-10-14"),
                Item("2026-10-15"),
                Item("2026-10-16"),
            ],
        };
        var handler = new ListEventProposalsHandler(fixture.Profiles, queries);

        var result = await handler.HandleAsync(
            new ListEventProposalsQuery(fixture.Managers["MED"], null, null, null, 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(queries.Rows[1].Cursor, result.Value.NextCursor);
        Assert.Equal(3, queries.LastLimit);
    }

    private static EventProposalListItem Item(string date) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "LON", "London", "Europe/London",
            DateOnly.Parse(date), new TimeOnly(9, 30), 240, "Open", 1, 1, 10,
            true, false, KeysetCursor.Encode(date, Guid.NewGuid()));

    private sealed class RecordingProposalQueries : IEventProposalListQueries
    {
        public Guid? LastActingType;
        public EventProposalStatus? LastStatus;
        public int LastLimit;
        public IReadOnlyList<EventProposalListItem> Rows { get; init; } = [];

        public Task<IReadOnlyList<EventProposalListItem>> ListAsync(
            Guid actingAppointmentTypeId, Guid staffUserId, EventProposalStatus? status,
            Guid? locationId, string? cursor, int limit, CancellationToken ct)
        {
            LastActingType = actingAppointmentTypeId;
            LastStatus = status;
            LastLimit = limit;
            return Task.FromResult(Rows);
        }
    }
}
