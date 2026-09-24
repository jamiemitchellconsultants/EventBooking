using EventBooking.Application.Common;
using EventBooking.Application.Negotiation;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Locations;

namespace EventBooking.Application.Tests.Negotiation;

public sealed class NegotiationFixture
{
    public InMemoryEventProposalRepository Proposals = new();
    public InMemoryEventRepository Events = new();
    public InMemoryEventCapacityRepository Capacities = null!;
    public InMemoryLocationRepository Locations = new();
    public InMemoryAppointmentTypeRepository Types = new();
    public InMemoryStaffAccessProfileRepository Profiles = new();
    public FakeUnitOfWork UnitOfWork = new();
    public RecordingAuditLogger Audit = new();
    public FakeClock Clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    public Dictionary<string, Guid> TypeIds = new();
    public Dictionary<string, Guid> Managers = new();
    public Guid LocationId;
    public Guid NullScopedManager = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    public static NegotiationFixture Create()
    {
        var fixture = new NegotiationFixture();
        fixture.Capacities = new InMemoryEventCapacityRepository(fixture.Events);
        fixture.Types.Items.Clear();
        var location = Location.Create(
            Guid.NewGuid(), "LONDON_HQ", "London HQ", "1 High St", "Europe/London",
            ProposalFixture.Zones);
        fixture.Locations.Items.Add(location);
        fixture.LocationId = location.Id;
        fixture.Profiles.Add(StaffAccessProfile.Create(fixture.NullScopedManager, Role.Manager, null));
        return fixture;
    }

    public NegotiationFixture WithTypes(params string[] codes)
    {
        foreach (var code in codes)
        {
            var type = AppointmentType.Create(Guid.NewGuid(), code, code);
            Types.Items.Add(type);
            TypeIds[code] = type.Id;
            var manager = Guid.NewGuid();
            Profiles.Add(StaffAccessProfile.Create(manager, Role.Manager, type.Id));
            Managers[code] = manager;
        }

        return this;
    }

    public NegotiationFixture WithNullScopedManager() => this;

    private ProposeEventHandler Proposer => new(
        Proposals, Locations, Types, Profiles, Profiles, UnitOfWork, Audit, Clock,
        ProposalFixture.Zones, Events);
    private RecordAcceptanceHandler Accepter => new(
        Proposals, Events, Profiles, UnitOfWork, Audit, Types);

    private int _proposalStarts;

    public async Task<ProposeEventOutcome> ProposeAsync(string proposerCode, string[] listed, int headcount)
    {
        // Successive proposals take successive half-hour windows: two open proposals at one
        // location may not share a local window.
        var start = new TimeOnly(9, 30).Add(TimeSpan.FromMinutes(30 * _proposalStarts++));
        var result = await Proposer.HandleAsync(new ProposeEventCommand(
            Managers[proposerCode], LocationId, new DateOnly(2026, 10, 14), start, 90,
            listed.Select(code => TypeIds[code]).ToList(), headcount), CancellationToken.None);
        Assert.True(result.IsSuccess, $"Propose failed: {result.Error?.Code} {result.Error?.Message}");
        return result.Value;
    }

    public async Task<Result<RecordAcceptanceOutcome>> AcceptAsync(string? typeCode, Guid proposalId, int headcount)
    {
        var user = typeCode is null ? NullScopedManager : Managers[typeCode];
        return await Accepter.HandleAsync(new RecordAcceptanceCommand(user, proposalId, headcount), CancellationToken.None);
    }

    public async Task WithdrawProposalAsync(string typeCode, Guid proposalId)
    {
        var handler = new WithdrawProposalHandler(Proposals, Profiles, UnitOfWork, Audit);
        var result = await handler.HandleAsync(new WithdrawProposalCommand(Managers[typeCode], proposalId), CancellationToken.None);
        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
    }

    public IReadOnlyList<string> AuditActionsFor(Guid proposalId) =>
        Audit.Entries.Where(e => e.EntityId == proposalId).Select(e => e.Action.ToString()).ToList();

    public void Occupy(string typeCode, Guid eventId, int occupied)
    {
        var row = Events.Items.Single(e => e.Id == eventId).CapacityFor(TypeIds[typeCode]);
        for (var i = 0; i < occupied; i++) row.Decrement();
    }
}
