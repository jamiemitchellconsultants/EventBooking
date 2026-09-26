using EventBooking.Application.Access;
using EventBooking.Application.Settings;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Settings;

public sealed class SettingsHandlerTests
{
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();

    public SettingsHandlerTests() =>
        _profiles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));

    private SaveSystemSettingsHandler Saver => new(_settings, _profiles, _unitOfWork, _audit);

    [Fact]
    public async Task Valid_save_writes_SystemSettingsChanged_with_version()
    {
        var result = await Saver.HandleAsync(
            new SaveSystemSettingsCommand(Admin, 14, 3, 5, 72, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new SystemSettingsResult(14, 3, 5, 72, 2), result.Value);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.SystemSettingsChanged, entry.Action);
        Assert.Equal(AuditEntityTypes.SystemSettings, entry.EntityType);
    }

    [Fact]
    public async Task Each_field_is_refused_outside_its_range()
    {
        foreach (var command in new[]
        {
            new SaveSystemSettingsCommand(Admin, 0, 2, 3, 48, 1),
            new SaveSystemSettingsCommand(Admin, 61, 2, 3, 48, 1),
            new SaveSystemSettingsCommand(Admin, 7, -1, 3, 48, 1),
            new SaveSystemSettingsCommand(Admin, 7, 11, 3, 48, 1),
            new SaveSystemSettingsCommand(Admin, 7, 2, 0, 48, 1),
            new SaveSystemSettingsCommand(Admin, 7, 2, 6, 48, 1),
            new SaveSystemSettingsCommand(Admin, 7, 2, 3, 0, 1),
            new SaveSystemSettingsCommand(Admin, 7, 2, 3, 169, 1),
        })
        {
            var result = await Saver.HandleAsync(command, CancellationToken.None);
            Assert.True(result.IsFailure);
            Assert.Equal("validation", result.Error.Code);
        }

        Assert.Equal(7, _settings.Settings.InviteExpiryDays);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Valid_settings_do_not_alter_existing_invites()
    {
        var optionEvents = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var invite = Invite.CreateInitial(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(7), [Guid.NewGuid()], optionEvents, [Guid.NewGuid()], 0);

        var result = await Saver.HandleAsync(
            new SaveSystemSettingsCommand(Admin, 30, 5, 1, 48, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, invite.InviteOptionCount);
        Assert.Equal(7, invite.InviteExpiryDays);
        Assert.Equal(2, invite.MaxAutoRetryCount);
    }

    [Fact]
    public async Task Stale_version_returns_version_conflict()
    {
        var result = await Saver.HandleAsync(
            new SaveSystemSettingsCommand(Admin, 14, 3, 3, 48, 99),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("version-conflict", result.Error.Code);
        Assert.Equal(1L, result.Error.Data!["currentVersion"]);
    }
}
