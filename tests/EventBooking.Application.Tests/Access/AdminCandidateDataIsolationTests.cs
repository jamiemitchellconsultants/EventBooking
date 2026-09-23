using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Access;

public class AdminCandidateDataIsolationTests
{
    [Fact]
    public async Task AdminCandidateListIsDeniedBeforeRepositoryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var candidates = new CountingCandidateRepository();
        var handler = new ListCandidatesHandler(candidates, new InMemoryEmployeeGroupRepository(), new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new ListCandidatesQuery(admin, null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, candidates.Calls);
    }

    [Fact]
    public async Task AdminDashboardIsDeniedBeforeAnyQueryInvocation()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingDashboardQueries();
        var handler = new GetDashboardsHandler(queries, new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new GetDashboardsQuery(admin), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task CoordinatorCandidateListStillRuns()
    {
        var coordinator = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var candidates = new CountingCandidateRepository();
        var handler = new ListCandidatesHandler(candidates, new InMemoryEmployeeGroupRepository(), new StaffAccessAuthorizer(profiles));

        var result = await handler.HandleAsync(
            new ListCandidatesQuery(coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, candidates.Calls);
    }

    [Fact]
    public async Task AdminCanReadSlotAuditButNotCandidateAudit()
    {
        var admin = Guid.NewGuid();
        var profiles = Profiles(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new CountingAuditQueries();
        var handler = new GetAuditHistoryHandler(queries, new StaffAccessAuthorizer(profiles));

        var candidate = await handler.HandleAsync(
            new GetAuditHistoryQuery(admin, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(candidate.IsFailure);
        Assert.Equal(0, queries.Calls);

        var slot = await handler.HandleAsync(
            new GetAuditHistoryQuery(
                admin, AuditEntityTypes.ConfirmedSlot, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(slot.IsSuccess);
        Assert.Equal(1, queries.Calls);
    }

    private static InMemoryStaffAccessProfileRepository Profiles(StaffAccessProfile profile)
    {
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(profile);
        return profiles;
    }

    private sealed class CountingCandidateRepository : ICandidateRepository
    {
        public int Calls { get; private set; }

        public Task<Candidate?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Candidate?>(null);
        }

        public Task<Candidate?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Candidate?>(null);
        }

        public Task<Candidate?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Candidate?>(null);
        }

        public Task<IReadOnlyList<Candidate>> ListAsync(
            CandidateStatus? status,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<Candidate>>([]);
        }

        public void Add(Candidate candidate) => Calls++;
        public void Remove(Candidate candidate) => Calls++;
    }

    private sealed class CountingDashboardQueries : IDashboardQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
            CancellationToken cancellationToken) => Return<AwaitingAvailabilityRow>();

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(
            CancellationToken cancellationToken) => Return<NoResponseRow>();

        public Task<IReadOnlyList<SlotOverviewRow>> SlotsOverviewAsync(
            CancellationToken cancellationToken) => Return<SlotOverviewRow>();

        public Task<IReadOnlyList<CandidateEmailStatusRow>> LatestEmailStatusAsync(
            CancellationToken cancellationToken) => Return<CandidateEmailStatusRow>();

        private Task<IReadOnlyList<T>> Return<T>()
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<T>>([]);
        }
    }

    private sealed class CountingAuditQueries : IAuditQueries
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<AuditHistoryRow>> ForEntityAsync(
            string entityType,
            Guid entityId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);
        }

        public Task<IReadOnlyList<AuditHistoryRow>> ForCandidateAsync(
            Guid candidateId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<AuditHistoryRow>>([]);
        }

        public Task<AuditSearchPage> SearchAsync(
            AuditSearchFilter filter,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AuditSearchPage([], null));
        }
    }
}
