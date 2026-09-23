using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using Microsoft.Extensions.Logging;

namespace EventBooking.Application.Tests.Access;

public sealed class AccessToken
{
    public Guid StaffUserId { get; set; } = Guid.NewGuid();
    public string? StaffIdValue { get; set; } = "A10023";
    public string? DisplayName { get; set; } = "Sam";
    public List<Role> Roles { get; set; } = [Role.Coordinator];
    public string StaffIdClaim { get; set; } = "staff_id";
    public string StaffIdPattern { get; set; } = StaffId.DefaultPattern;
}

public sealed class AccessFixture
{
    public InMemoryStaffAccessProfileRepository Profiles = new();
    public InMemoryStaffIdentityRepository Identities = new();
    public FakeUnitOfWork UnitOfWork = new();
    public RecordingAuditLogger Audit = new();
    public FakeClock Clock = new();
    public List<string> Logs = [];

    public static AccessFixture Create() => new();

    public AccessFixture WithStoredRoles(params Role[] roles)
    {
        Profiles.Items.Add(StaffAccessProfile.Create(Guid.NewGuid(), roles.ToList(), null));
        return this;
    }

    // Authorize through the real authorizer against the configured claim names,
    // syncing roles first exactly as the pipeline does.
    public async Task<Result<StaffAccessContext>> AuthorizeAsync(
        AccessToken token, StaffCapability capability)
    {
        var syncer = new RoleSyncShim(this);
        await syncer.SyncAsync(token);
        var parsed = ParseStaffId(token);
        if (parsed is null)
            return Result<StaffAccessContext>.Failure(
                Error.Forbidden("No usable staff_id."));
        var authorizer = new StaffAccessAuthorizer(Profiles);
        return await authorizer.AuthorizeAsync(
            token.StaffUserId, capability, null, CancellationToken.None);
    }

    public static StaffId? ParseStaffId(AccessToken token) =>
        token.StaffIdValue is null ? null
        : StaffId.TryParse(token.StaffIdValue, out var parsed, token.StaffIdPattern) ? parsed : null;

    private sealed class RoleSyncShim(AccessFixture fixture)
    {
        public Task<StaffAccessProfile?> SyncAsync(AccessToken token) =>
            new SyncStaffAccessProfileRolesHandler(
                fixture.Profiles,
                fixture.UnitOfWork,
                fixture.Audit,
                new ListLogger<SyncStaffAccessProfileRolesHandler>(fixture.Logs))
            .SyncAsync(token.StaffUserId, token.Roles.ToHashSet(), CancellationToken.None);
    }

    private sealed class ListLogger<T>(List<string> lines) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }
}
