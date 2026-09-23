using System.Security.Claims;
using EventBooking.Api.Auth;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Api.Tests;

public class StaffRequirementHandlerTests
{
    [Fact]
    public async Task TheRoleLookupUsesTheRequestCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var httpContext = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token,
        };
        var roles = new CapturingStaffAccessProfileRepository();
        var handler = new StaffRequirementHandler(
            new FixedCallerAccessor(
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                new StaffId("U999999")),
            roles,
            SyncFor(roles));
        var authorizationContext = new AuthorizationHandlerContext(
            [new StaffRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            httpContext);

        await handler.HandleAsync(authorizationContext);

        Assert.Equal(cancellation.Token, roles.CapturedCancellationToken);
    }

    [Fact]
    public async Task ARevokedRoleIsAppliedWithoutCallingMeFirst()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new CapturingStaffAccessProfileRepository
        {
            Profile = StaffAccessProfile.Create(staffUserId, Role.Coordinator, null),
        };
        // The token no longer carries any role; the stored profile must not linger.
        var handler = new StaffRequirementHandler(
            new FixedCallerAccessor(staffUserId, new StaffId("U999999")),
            profiles,
            SyncFor(profiles));
        var authorizationContext = new AuthorizationHandlerContext(
            [new StaffRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            new DefaultHttpContext());

        await handler.HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
    }

    [Fact]
    public async Task AProfileDoesNotAuthorizeACallerWithoutAStaffNumber()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new CapturingStaffAccessProfileRepository
        {
            Profile = StaffAccessProfile.Create(staffUserId, Role.Coordinator, null),
        };
        var handler = new StaffRequirementHandler(
            new FixedCallerAccessor(staffUserId, null),
            profiles,
            SyncFor(profiles));
        var authorizationContext = new AuthorizationHandlerContext(
            [new StaffRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            new DefaultHttpContext());

        await handler.HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
    }

    private static SyncStaffAccessProfileRolesHandler SyncFor(
        IStaffAccessProfileRepository profiles) =>
        new(
            profiles,
            new NoOpUnitOfWork(),
            new NoOpAuditLogger(),
            NullLogger<SyncStaffAccessProfileRolesHandler>.Instance);

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken) =>
            Task.FromResult<ITransactionScope>(new NoOpTransactionScope());

        private sealed class NoOpTransactionScope : ITransactionScope
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }

    private sealed class NoOpAuditLogger : IAuditLogger
    {
        public void Record(
            string entityType,
            Guid entityId,
            AuditAction action,
            ActorType actorType,
            string? actorId,
            string? details = null)
        {
        }
    }

    private sealed class FixedCallerAccessor(Guid staffUserId, StaffId? staffId = null) : ICallerAccessor
    {
        public Guid? StaffUserId => staffUserId;

        public StaffId? StaffId => staffId;

        public string? DisplayName => null;

        public IReadOnlySet<Role> Roles => new HashSet<Role>();

        public Guid RequireStaffUserId() => staffUserId;

        public StaffId RequireStaffId() => staffId
            ?? throw new InvalidOperationException("The request has no staff number.");
    }

    private sealed class CapturingStaffAccessProfileRepository : IStaffAccessProfileRepository
    {
        private readonly List<StaffAccessProfile> _items = [];

        public StaffAccessProfile? Profile
        {
            get => _items.SingleOrDefault();
            init
            {
                if (value is not null)
                {
                    _items.Add(value);
                }
            }
        }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public Task<StaffAccessProfile?> GetAsync(Guid staffUserId, CancellationToken cancellationToken)
        {
            CapturedCancellationToken = cancellationToken;
            return Task.FromResult(_items.SingleOrDefault(profile => profile.StaffUserId == staffUserId));
        }

        public Task<IReadOnlyList<StaffAccessProfile>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccessProfile>>(_items.ToList());

        public Task<IReadOnlyList<StaffAccessProfile>> LockAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccessProfile>>(_items.ToList());

        public void Add(StaffAccessProfile profile) => _items.Add(profile);

        public void Remove(StaffAccessProfile profile) => _items.Remove(profile);
    }
}
