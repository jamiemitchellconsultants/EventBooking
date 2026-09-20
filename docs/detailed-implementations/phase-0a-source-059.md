# 00a — Port source 59 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Application.Tests/Access/StaffAccessHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Access/StaffAccessHandlerTests.cs","encoding":"utf8","sha256":"aa2296fcdbe01ef98075a60f45a9c32d9e1949293fd4e23fdb5167c5004cec2b","parts":1,"part":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.Access;

public class StaffAccessHandlerTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly InMemoryStaffIdentityRepository _identities = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    public StaffAccessHandlerTests()
    {
        _profiles.Add(StaffAccessProfile.Create(Admin, [Role.Admin], null));
    }

    [Fact]
    public async Task AdminSetsScopeOnAnExistingScopedRoleProfile()
    {
        var target = Guid.NewGuid();
        var profile = StaffAccessProfile.Create(
            target, [Role.Coordinator, Role.Manager], null); // transitional shape
        _profiles.Add(profile);
        var rolesBefore = profile.Roles.OrderBy(value => value).ToArray();

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, target, AppointmentTypeIds.DrugAndAlcoholTesting, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, result.Value.Profile.AppointmentTypeId);
        Assert.Equal(rolesBefore, result.Value.Profile.Roles.OrderBy(value => value));
        var audit = Assert.Single(_audit.Entries, value => value.EntityId == target);
        Assert.Equal(AuditAction.StaffAccessChanged, audit.Action);
        using var details = JsonDocument.Parse(audit.Details!);
        Assert.Equal(
            details.RootElement.GetProperty("previous").GetProperty("Roles").GetRawText(),
            details.RootElement.GetProperty("current").GetProperty("Roles").GetRawText());
    }

    [Fact]
    public async Task AssigningScopeToANewManagerDisplacesTheFormerManagerOfThatType()
    {
        var former = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            former, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));
        var incoming = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(incoming, [Role.Manager], null));

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, incoming, AppointmentTypeIds.MedicalCheckUp, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(former, result.Value.FormerManagerStaffUserId);
        var formerProfile = _profiles.Items.Single(value => value.StaffUserId == former);
        Assert.True(formerProfile.IsManager);
        Assert.Null(formerProfile.AppointmentTypeId);
    }

    [Fact]
    public async Task DisplacingAManagerWhoIsAlsoAppointmentStaffStillClearsTheirSharedScope()
    {
        // Roles are identity-provider-owned in this handler, so a displaced Manager who is also
        // AppointmentStaff cannot keep the appointment-type scope: it is one shared field, and
        // preserving it would let them keep passing IsManager + AppointmentTypeId checks for the
        // type they were just displaced from.
        var former = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            former, [Role.Manager, Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp));
        var incoming = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(incoming, [Role.Manager], null));

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, incoming, AppointmentTypeIds.MedicalCheckUp, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(former, result.Value.FormerManagerStaffUserId);
        var formerProfile = _profiles.Items.Single(value => value.StaffUserId == former);
        Assert.True(formerProfile.IsManager);
        Assert.True(formerProfile.IsAppointmentStaff);
        Assert.Null(formerProfile.AppointmentTypeId);
    }

    [Fact]
    public async Task AStaleVersionCannotOverwriteANewerScope()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            target, [Role.Manager], AppointmentTypeIds.UniformFitting));

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, target, AppointmentTypeIds.DrugAndAlcoholTesting, ExpectedVersion: 99),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AnUnknownTargetReturnsNotFound()
    {
        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, Guid.NewGuid(), AppointmentTypeIds.UniformFitting, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AManagedTypeCannotBeAbandonedByMovingItsManagerDirectly()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            target, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, target, AppointmentTypeIds.UniformFitting, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task SettingScopeOnAProfileWithNoScopedRoleIsRejected()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(target, [Role.Coordinator], null));

        var result = await Handler().ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                Admin, target, AppointmentTypeIds.UniformFitting, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ClearingScopeOnAnAppointmentStaffProfileSucceeds()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            target, [Role.AppointmentStaff], AppointmentTypeIds.UniformFitting));

        var result = await Handler().ClearScopeAsync(
            new ClearStaffAccessProfileScopeCommand(Admin, target, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var profile = _profiles.Items.Single(p => p.StaffUserId == target);
        Assert.True(profile.IsAppointmentStaff);
        Assert.Equal([Role.AppointmentStaff], profile.Roles);
        Assert.Null(profile.AppointmentTypeId);
        Assert.Equal(2, profile.Version);

        var audit = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.StaffAccessChanged, audit.Action);
        Assert.Equal(ActorType.Staff, audit.ActorType);
        Assert.Equal(Admin.ToString(), audit.ActorId);
        using var details = JsonDocument.Parse(audit.Details!);
        Assert.Equal(
            details.RootElement.GetProperty("previous").GetProperty("Roles").GetRawText(),
            details.RootElement.GetProperty("current").GetProperty("Roles").GetRawText());
        Assert.Equal(
            AppointmentTypeIds.UniformFitting,
            details.RootElement.GetProperty("previous").GetProperty("AppointmentTypeId").GetGuid());
        Assert.Equal(
            JsonValueKind.Null,
            details.RootElement.GetProperty("current").GetProperty("AppointmentTypeId").ValueKind);
    }

    [Fact]
    public async Task ClearingScopeOnACurrentManagerIsBlocked()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            target, [Role.Manager], AppointmentTypeIds.UniformFitting));

        var result = await Handler().ClearScopeAsync(
            new ClearStaffAccessProfileScopeCommand(Admin, target, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ClearingAnAlreadyNullScopeIsRejected()
    {
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(target, [Role.Manager], null));

        var result = await Handler().ClearScopeAsync(
            new ClearStaffAccessProfileScopeCommand(Admin, target, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ACoordinatorCannotListOrMutateProfiles()
    {
        var coordinator = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var target = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(target, [Role.Coordinator], null));
        var handler = Handler();

        var list = await handler.ListAsync(coordinator, CancellationToken.None);
        var replace = await handler.ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                coordinator, target, AppointmentTypeIds.UniformFitting, 1),
            CancellationToken.None);

        Assert.True(list.IsFailure);
        Assert.True(replace.IsFailure);
    }

    [Fact]
    public async Task StaffAccessListCarriesKnownStaffNumbersAndLeavesUnknownOnesNull()
    {
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(known, Role.Coordinator, null));
        _profiles.Add(StaffAccessProfile.Create(unknown, Role.Coordinator, null));
        await _identities.UpsertAsync(
            known,
            new StaffId("u123456"),
            null,
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
            CancellationToken.None);

        var result = await Handler().ListAsync(Admin, CancellationToken.None);

        Assert.Equal(new StaffId("U123456"),
            result.Value.Single(profile => profile.StaffUserId == known).StaffId);
        Assert.Null(result.Value.Single(profile => profile.StaffUserId == unknown).StaffId);
    }

    [Fact]
    public async Task AdminResolvesARecordedStaffNumberToItsProviderKey()
    {
        var target = Guid.NewGuid();
        await _identities.UpsertAsync(
            target,
            new StaffId("N654321"),
            null,
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
            CancellationToken.None);

        var result = await Handler().ResolveIdentityAsync(
            Admin, "n654321", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(target, result.Value);
    }

    private StaffAccessHandler Handler() => new(
        _profiles,
        _identities,
        new StaffAccessAuthorizer(_profiles),
        _unitOfWork,
        _audit);

    /// <summary>Verifies an observed name reaches the listing while an unobserved one stays null.</summary>
    [Fact]
    public async Task ListAsyncPopulatesDisplayNameWhenKnown()
    {
        var named = Guid.NewGuid();
        var unnamed = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(named, Role.Coordinator, null));
        _profiles.Add(StaffAccessProfile.Create(unnamed, Role.Coordinator, null));
        await _identities.UpsertAsync(
            named,
            new StaffId("U000002"),
            "Dana Datson",
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
            CancellationToken.None);
        await _identities.UpsertAsync(
            unnamed,
            new StaffId("U000003"),
            null,
            DateTimeOffset.Parse("2026-09-08T10:00:00Z"),
            CancellationToken.None);

        var result = await Handler().ListAsync(Admin, CancellationToken.None);

        var first = result.Value.Single(profile => profile.StaffUserId == named);
        var second = result.Value.Single(profile => profile.StaffUserId == unnamed);
        Assert.Equal("Dana Datson", first.DisplayName);
        Assert.Equal(new StaffId("U000002"), first.StaffId);
        Assert.Null(second.DisplayName);
        Assert.Equal(new StaffId("U000003"), second.StaffId);
    }
}
`````

## tests/EventBooking.Application.Tests/Access/SyncStaffAccessProfileRolesHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Access/SyncStaffAccessProfileRolesHandlerTests.cs","encoding":"utf8","sha256":"4847a338c6e27079cfe2c7bd16dfb9537d5f94a2cdc8edf1d3b6996ec3e8fe70","parts":1,"part":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Application.Tests.Access;

public class SyncStaffAccessProfileRolesHandlerTests
{
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private SyncStaffAccessProfileRolesHandler Handler() => new(
        _profiles, _unitOfWork, _audit, NullLogger<SyncStaffAccessProfileRolesHandler>.Instance);

    [Fact]
    public async Task MatchingRolesAreANoOpWrite()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Coordinator], null));

        var result = await Handler().SyncAsync(id, new HashSet<Role> { Role.Coordinator }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task ANewIdentityWithClaimedRolesGetsAProfileWithNullScope()
    {
        var id = Guid.NewGuid();

        var result = await Handler().SyncAsync(id, new HashSet<Role> { Role.Manager }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsManager);
        Assert.Null(result.AppointmentTypeId);
        Assert.True(_audit.Contains(AuditAction.StaffRolesSynced));
    }

    [Fact]
    public async Task ANewIdentityWithNoClaimedRolesGetsNoProfile()
    {
        var result = await Handler().SyncAsync(Guid.NewGuid(), new HashSet<Role>(), CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task LosingAllClaimedRolesRemovesTheProfile()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Coordinator], null));

        var result = await Handler().SyncAsync(id, new HashSet<Role>(), CancellationToken.None);

        Assert.Null(result);
        Assert.DoesNotContain(id, _profiles.Items.Select(p => p.StaffUserId));
        Assert.True(_audit.Contains(AuditAction.StaffRolesSynced));
    }

    [Fact]
    public async Task LosingTheScopedRoleClearsScopeButKeepsCoordinator()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            id, [Role.Coordinator, Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting));

        var result = await Handler().SyncAsync(id, new HashSet<Role> { Role.Coordinator }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsCoordinator);
        Assert.False(result.IsManager);
        Assert.Null(result.AppointmentTypeId);
    }

    [Fact]
    public async Task GainingAScopedRoleOnAnExistingProfilePreservesItsExistingScope()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            id, [Role.Manager], AppointmentTypeIds.MedicalCheckUp));

        var result = await Handler().SyncAsync(
            id, new HashSet<Role> { Role.Manager, Role.AppointmentStaff }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, result!.AppointmentTypeId);
    }

    [Fact]
    public async Task RemovingAnUnscopedRolePreservesScopeWhileManagerRemains()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            id, [Role.Coordinator, Role.Manager], AppointmentTypeIds.MedicalCheckUp));

        var result = await Handler().SyncAsync(
            id, new HashSet<Role> { Role.Manager }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, result!.AppointmentTypeId);
        Assert.Equal([Role.Manager], result.Roles);
    }

    [Fact]
    public async Task RemovingOneScopedRolePreservesScopeWhileTheOtherRemains()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            id, [Role.Manager, Role.AppointmentStaff], AppointmentTypeIds.UniformFitting));

        var result = await Handler().SyncAsync(
            id, new HashSet<Role> { Role.AppointmentStaff }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AppointmentTypeIds.UniformFitting, result!.AppointmentTypeId);
        Assert.Equal([Role.AppointmentStaff], result.Roles);
    }

    [Fact]
    public async Task AChangedRoleSetRecordsExactSystemAuditState()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            id, [Role.Coordinator, Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting));

        await Handler().SyncAsync(
            id, new HashSet<Role> { Role.Coordinator }, CancellationToken.None);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.StaffRolesSynced, entry.Action);
        Assert.Equal(ActorType.System, entry.ActorType);
        Assert.Null(entry.ActorId);
        using var details = JsonDocument.Parse(entry.Details!);
        Assert.Equal(
            ["Manager", "Coordinator"],
            details.RootElement.GetProperty("previous").GetProperty("Roles")
                .EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            details.RootElement.GetProperty("previous").GetProperty("AppointmentTypeId").GetGuid());
        Assert.Equal(
            ["Coordinator"],
            details.RootElement.GetProperty("current").GetProperty("Roles")
                .EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            JsonValueKind.Null,
            details.RootElement.GetProperty("current").GetProperty("AppointmentTypeId").ValueKind);
    }

    [Fact]
    public async Task LosingAllClaimedRolesAsTheLastAdminRefusesAndKeepsTheProfile()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Admin], null));

        var result = await Handler().SyncAsync(id, new HashSet<Role>(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsAdmin);
        Assert.Contains(id, _profiles.Items.Select(p => p.StaffUserId));
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task DemotingTheLastAdminAwayFromAdminIsRefusedAndKeepsTheProfile()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Admin], null));

        var result = await Handler().SyncAsync(
            id, new HashSet<Role> { Role.Coordinator }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsAdmin);
        Assert.False(result.IsCoordinator);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task LosingAllClaimedRolesWhenAnotherAdminRemainsStillRemovesTheProfile()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Admin], null));
        _profiles.Add(StaffAccessProfile.Create(Guid.NewGuid(), [Role.Admin], null));

        var result = await Handler().SyncAsync(id, new HashSet<Role>(), CancellationToken.None);

        Assert.Null(result);
        Assert.DoesNotContain(id, _profiles.Items.Select(p => p.StaffUserId));
    }

    [Fact]
    public async Task AnInvalidClaimedCombinationIsRejectedWithoutOverwritingThePreviousProfile()
    {
        var id = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(id, [Role.Coordinator], null));

        var result = await Handler().SyncAsync(
            id, new HashSet<Role> { Role.Admin, Role.Manager }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsCoordinator);
        Assert.False(result.IsAdmin);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_audit.Entries);
    }
}
`````

## tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs","encoding":"utf8","sha256":"06095d7aea89cedd2af3c006ebdbda44ed31a66cf1250848969a921916c6752d","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies the roster CSV projection: columns, ordering, escaping, time, and filename.</summary>
public sealed class AppointmentRosterCsvFormatterTests
{
    private const string Header =
        "Candidate Name,Candidate Email,Appointment Type,Status,Checked In At,Outcome At";

    private static AppointmentRosterCsvFormatter Formatter() => new(new FakeClock());

    private static string[] LinesOf(string csv) =>
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void FormatEmitsExactHeaderAndColumnOrder()
    {
        var result = Formatter().Format(SlotWithRows());

        Assert.Equal(Header, LinesOf(result.CsvText)[0]);
    }

    [Fact]
    public void FormatPreservesRowOrderAndRepeatsAppointmentType()
    {
        var result = Formatter().Format(SlotWithRows());

        var lines = LinesOf(result.CsvText);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Amina Yusuf,", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("Bruno Costa,", lines[2], StringComparison.Ordinal);
        Assert.Contains(",Drug & Alcohol Testing,", lines[1], StringComparison.Ordinal);
        Assert.Contains(",Drug & Alcohol Testing,", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void FormatEmptySlotYieldsHeaderOnly()
    {
        var result = Formatter().Format(SlotWith([]));

        Assert.Equal(Header + "\n", result.CsvText);
    }

    [Fact]
    public void FormatNullTimestampsRenderAsEmpty()
    {
        var result = Formatter().Format(SlotWith([Row("Amina Yusuf", BookingAppointmentStatus.Expected)]));

        var dataLine = LinesOf(result.CsvText)[1];
        Assert.EndsWith(",,", dataLine, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatNonNullTimestampsRenderAsHeadOfficeIso8601()
    {
        var checkedInAt = new DateTimeOffset(2026, 9, 15, 9, 35, 0, TimeSpan.Zero);
        var outcomeAt = new DateTimeOffset(2026, 9, 15, 10, 5, 0, TimeSpan.Zero);
        var result = Formatter().Format(SlotWith(
        [
            Row("Amina Yusuf", BookingAppointmentStatus.Completed, checkedInAt, outcomeAt),
        ]));

        var fields = LinesOf(result.CsvText)[1].Split(',');
        Assert.Equal(checkedInAt.ToString("o"), fields[4]);
        Assert.Equal(outcomeAt.ToString("o"), fields[5]);
    }

    [Fact]
    public void FormatUsesTheStatusEnumNameNotADisplayLabel()
    {
        var result = Formatter().Format(SlotWith(
        [
            Row("Amina Yusuf", BookingAppointmentStatus.CheckedIn),
            Row("Bruno Costa", BookingAppointmentStatus.NoShow),
        ]));

        var lines = LinesOf(result.CsvText);
        Assert.Contains(",CheckedIn,", lines[1], StringComparison.Ordinal);
        Assert.Contains(",NoShow,", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void FormatNameWithCommaAndQuoteIsRfc4180Escaped()
    {
        var result = Formatter().Format(SlotWith(
        [
            Row("Okafor, Ada \"Bisi\"", BookingAppointmentStatus.Expected),
        ]));

        Assert.Contains("\"Okafor, Ada \"\"Bisi\"\"\",", result.CsvText, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatNeverEmitsTheCommandTargetOrConcurrencyToken()
    {
        var detail = SlotWithRows();

        var result = Formatter().Format(detail);

        foreach (var row in detail.Appointments)
        {
            Assert.DoesNotContain(row.BookingAppointmentId.ToString(), result.CsvText, StringComparison.Ordinal);
        }

        Assert.Equal(3, LinesOf(result.CsvText).Length);
        Assert.All(LinesOf(result.CsvText), line => Assert.Equal(5, line.Count(c => c == ',')));
    }

    [Fact]
    public void FormatBuildsSlugifiedFilename()
    {
        var result = Formatter().Format(SlotWithRows());

        Assert.Equal("roster-drug-&-alcohol-testing-2026-09-15-0930.csv", result.FileName);
    }

    [Theory]
    [InlineData("=2+2")]
    [InlineData("+441234567")]
    [InlineData("-Robert")]
    [InlineData("@malicious")]
    [InlineData("\tindented")]
    [InlineData("\rreturn")]
    public void FormatNeutralisesFormulaTriggersWithASingleQuote(string name)
    {
        var result = Formatter().Format(SlotWith(
        [
            Row(name, BookingAppointmentStatus.Expected),
        ]));

        var dataLine = LinesOf(result.CsvText)[1];
        Assert.Contains("'" + name, dataLine, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatFilenameKeepsAMidnightStartTimeFourDigits()
    {
        var result = Formatter().Format(SlotWith([], startTime: new TimeOnly(0, 5)));

        Assert.Equal("roster-drug-&-alcohol-testing-2026-09-15-0005.csv", result.FileName);
    }

    private static BookingAppointmentRow Row(
        string candidateName,
        BookingAppointmentStatus status,
        DateTimeOffset? checkedInAt = null,
        DateTimeOffset? outcomeAt = null) => new()
    {
        BookingAppointmentId = Guid.NewGuid(),
        CandidateName = candidateName,
        CandidateEmail = $"{candidateName.Split(' ')[0].ToLowerInvariant()}@mail.com",
        Status = status,
        CheckedInAt = checkedInAt,
        OutcomeAt = outcomeAt,
        Version = 1,
    };

    private static AppointmentSlotDetail SlotWith(
        IReadOnlyList<BookingAppointmentRow> rows,
        TimeOnly? startTime = null) => new()
    {
        AppointmentTypeName = "Drug & Alcohol Testing",
        ConfirmedSlotId = Guid.NewGuid(),
        Date = new DateOnly(2026, 9, 15),
        StartTime = startTime ?? new TimeOnly(9, 30),
        EndTime = (startTime ?? new TimeOnly(9, 30)).AddHours(4),
        Appointments = rows,
    };

    private static AppointmentSlotDetail SlotWithRows() => SlotWith(
    [
        Row("Amina Yusuf", BookingAppointmentStatus.Expected),
        Row("Bruno Costa", BookingAppointmentStatus.CheckedIn,
            new DateTimeOffset(2026, 9, 15, 9, 35, 0, TimeSpan.Zero)),
    ]);
}
`````

## tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs","encoding":"utf8","sha256":"eaf846a320030484025333bbecbd47365f2df1160492cfb71b00dfa94fcc01fd","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies workspace reads authorize before using minimum-data query ports.</summary>
public sealed class GetAppointmentWorkspaceHandlerTests
{
    /// <summary>Verifies both scoped roles pass the profile's trusted type to the query.</summary>
    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.AppointmentStaff)]
    public async Task ScopedRoleUsesItsTrustedAppointmentType(Role role)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [role], AppointmentTypeIds.MedicalCheckUp));
        var queries = new RecordingQueries();
        var handler = new GetAppointmentWorkspaceHandler(
            new StaffAccessAuthorizer(profiles), queries, new FakeClock());

        var result = await handler.ListSlotsAsync(staff, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, queries.ListCallCount);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, queries.LastAppointmentTypeId);
    }

    /// <summary>Verifies a combined Coordinator profile receives its scoped-role capability.</summary>
    [Fact]
    public async Task CombinedCoordinatorAppointmentStaffCanReadTheWorkspace()
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff,
            [Role.Coordinator, Role.AppointmentStaff],
            AppointmentTypeIds.UniformFitting));
        var queries = new RecordingQueries();

        var result = await new GetAppointmentWorkspaceHandler(
            new StaffAccessAuthorizer(profiles), queries, new FakeClock())
            .GetSlotAsync(staff, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentTypeIds.UniformFitting, queries.LastAppointmentTypeId);
    }

    /// <summary>Verifies Admin denial occurs before the candidate-data query is invoked.</summary>
    [Fact]
    public async Task AdminIsDeniedBeforeAnyWorkspaceQuery()
    {
        var admin = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new RecordingQueries();

        var result = await new GetAppointmentWorkspaceHandler(
            new StaffAccessAuthorizer(profiles), queries, new FakeClock())
            .ListSlotsAsync(admin, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, queries.ListCallCount);
        Assert.Equal(0, queries.DetailCallCount);
    }

    /// <summary>Verifies absent and cross-scope slots share the same not-found application result.</summary>
    [Fact]
    public async Task QueryNullBecomesTheStableNotFoundResult()
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting));
        var queries = new RecordingQueries { ReturnDetail = null };

        var result = await new GetAppointmentWorkspaceHandler(
            new StaffAccessAuthorizer(profiles), queries, new FakeClock())
            .GetSlotAsync(staff, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("No such appointment workspace slot.", result.Error.Message);
    }

    private sealed class RecordingQueries : IAppointmentWorkspaceQueries
    {
        public int ListCallCount { get; private set; }
        public int DetailCallCount { get; private set; }
        public Guid? LastAppointmentTypeId { get; private set; }
        public AppointmentSlotDetail? ReturnDetail { get; set; } = new()
        {
            AppointmentTypeName = "Drug & Alcohol Testing",
            ConfirmedSlotId = Guid.NewGuid(),
            Date = new DateOnly(2026, 9, 7),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(13, 0),
            Appointments = [],
        };

        public Task<AppointmentWorkspaceSlotList> ListSlotsAsync(
            Guid appointmentTypeId,
            DateOnly onOrAfter,
            CancellationToken cancellationToken)
        {
            ListCallCount++;
            LastAppointmentTypeId = appointmentTypeId;
            return Task.FromResult(new AppointmentWorkspaceSlotList
            {
                AppointmentTypeName = "Medical Check-up",
                Slots = [],
            });
        }

        public Task<AppointmentSlotDetail?> GetSlotAsync(
            Guid appointmentTypeId,
            Guid confirmedSlotId,
            CancellationToken cancellationToken)
        {
            DetailCallCount++;
            LastAppointmentTypeId = appointmentTypeId;
            return Task.FromResult(ReturnDetail);
        }
    }
}
`````

## tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs","encoding":"utf8","sha256":"b3cd75571cc0271ae90fab0105a611a497ea07ba52fb7abfe9fa6d278b5355ac","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies late outcomes on past slots follow the existing timing and correction rules.</summary>
public sealed class LateNoShowOutcomeTests
{
    /// <summary>Verifies Expected to NoShow succeeds the day after the slot date with version and audit.</summary>
    [Fact]
    public async Task NoShowDayAfterSlotDateSucceeds()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.NoShow, result.Value.Status);
        Assert.Equal(2, result.Value.Version);
        Assert.NotNull(result.Value.OutcomeAt);
        Assert.Null(result.Value.CheckedInAt);
        var entry = Assert.Single(scenario.Audit.Entries);
        Assert.Equal(AuditAction.AppointmentMarkedNoShow, entry.Action);
    }

    /// <summary>Verifies check-in is rejected once the slot date has passed.</summary>
    [Fact]
    public async Task CheckInAfterSlotDateIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingAppointmentStatus.Expected, scenario.Appointment.Status);
    }

    /// <summary>Verifies a late NoShow corrects back to Expected while parents remain active.</summary>
    [Fact]
    public async Task LateNoShowCorrectsToExpected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Expected, 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.Expected, result.Value.Status);
        Assert.Equal(3, result.Value.Version);
        Assert.Null(result.Value.OutcomeAt);
        Assert.Contains(
            scenario.Audit.Entries, e => e.Action == AuditAction.AppointmentStatusCorrected);
    }

    private static UpdateBookingAppointmentStatusCommand Command(
        Scenario scenario,
        BookingAppointmentStatus status,
        long version) => new()
    {
        StaffUserId = scenario.StaffUserId,
        BookingAppointmentId = scenario.Appointment.Id,
        Status = status,
        ExpectedVersion = version,
    };

    /// <summary>Builds a DAT-only group; the scenario needs a mapping, not an identity.</summary>
    private static EmployeeGroup DatOnly() =>
        EmployeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var candidates = new InMemoryCandidateRepository(operations);
        candidates.Add(candidate);
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var slots = new InMemoryConfirmedSlotRepository(operations);
        slots.Add(slot);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "invite-token", now.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()], candidate.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, "manage-token", now.AddDays(-1));
        var bookings = new InMemoryBookingRepository(operations);
        bookings.Add(booking);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var appointments = new InMemoryBookingAppointmentRepository(bookings, operations);
        appointments.Add(appointment);
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork(operations);
        var clock = new FakeClock(now);
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            candidates,
            invites,
            slots,
            new RecoveryBookingOutcomeCoordinator(),
            audit,
            unitOfWork,
            clock);
        return new Scenario(
            staff, candidate, slot, appointment, booking, audit, operations, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        Candidate Candidate,
        ConfirmedSlot Slot,
        BookingAppointment Appointment,
        Booking Booking,
        RecordingAuditLogger Audit,
        TransactionOperationLog Operations,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````

## tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs","encoding":"utf8","sha256":"25a56a1ec64a906adc4abc5e5ff6e83b911f7cdf9a332160873615b19bb1da90","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies a late-recorded outcome makes its requirement recoverable.</summary>
public sealed class RecentPastRecoveryEligibilityTests
{
    /// <summary>Verifies the late NoShow latest attempt reads as outstanding and recoverable.</summary>
    [Fact]
    public async Task LateNoShowMakesRequirementRecoverable()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));
        var outcome = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = scenario.StaffUserId,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.NoShow,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var readiness = new CandidateReadinessCalculator().Calculate(
            new CandidateReadinessSnapshot(
                scenario.Candidate.Id,
                Guid.NewGuid(),
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                scenario.Booking.Id,
                [
                    new CandidateReadinessAttempt(
                        scenario.Appointment.Id,
                        AppointmentTypeIds.DrugAndAlcoholTesting,
                        scenario.Appointment.Status,
                        scenario.Booking.Id,
                        scenario.Booking.Status,
                        outcome.Value.OutcomeAt!.Value),
                ]));

        Assert.Equal(CandidateReadinessCode.AppointmentsOutstanding, readiness.Code);
        var outstanding = Assert.Single(readiness.OutstandingAppointmentTypes);
        Assert.True(outstanding.IsRecoverable);
    }

    /// <summary>Builds a DAT-only group; the scenario needs a mapping, not an identity.</summary>
    private static EmployeeGroup DatOnly() =>
        EmployeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var candidates = new InMemoryCandidateRepository(operations);
        candidates.Add(candidate);
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var slots = new InMemoryConfirmedSlotRepository(operations);
        slots.Add(slot);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "invite-token", now.AddDays(1),
            [slot.Id, Guid.NewGuid(), Guid.NewGuid()], candidate.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, "manage-token", now.AddDays(-1));
        var bookings = new InMemoryBookingRepository(operations);
        bookings.Add(booking);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var appointments = new InMemoryBookingAppointmentRepository(bookings, operations);
        appointments.Add(appointment);
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            candidates,
            invites,
            slots,
            new RecoveryBookingOutcomeCoordinator(),
            new RecordingAuditLogger(),
            new FakeUnitOfWork(operations),
            new FakeClock(now));
        return new Scenario(staff, candidate, booking, appointment, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        Candidate Candidate,
        Booking Booking,
        BookingAppointment Appointment,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````

## tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs","encoding":"utf8","sha256":"27342724cb7e2b0a8f1adadb2f8939167ac373c3dfae4d10d7d88e8fbfc9a521","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Appointments;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies recovery Booking status follows only its own appointment outcomes.</summary>
public sealed class RecoveryBookingOutcomeCoordinatorTests
{
    /// <summary>All terminal outcomes conclude a recovery while leaving its root untouched.</summary>
    [Fact]
    public void TerminalRecoveryAppointmentsConcludeRecoveryOnly()
    {
        var staff = Guid.NewGuid();
        var originalSlot = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "initial", DateTimeOffset.UtcNow.AddDays(1),
            [originalSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, originalSlot, "original", DateTimeOffset.UtcNow);
        var recoverySlot = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), original.CandidateId, original.Id, "recovery",
            DateTimeOffset.UtcNow.AddDays(2),
            [recoverySlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoverySlot, "manage", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp);
        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, staff, DateTimeOffset.UtcNow,
            checkInAllowed: false, noShowAllowed: true);

        var changed = new RecoveryBookingOutcomeCoordinator()
            .Synchronize(recovery, [appointment], laterRecoveryExists: false);

        Assert.True(changed);
        Assert.Equal(BookingStatus.Concluded, recovery.Status);
        Assert.Equal(BookingStatus.Active, original.Status);
    }

    /// <summary>A corrected non-terminal appointment cannot reopen behind a later recovery.</summary>
    [Fact]
    public void LaterRecoveryPreventsReopen()
    {
        var originalSlot = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "initial", DateTimeOffset.UtcNow.AddDays(1),
            [originalSlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(Guid.NewGuid(), initial, originalSlot, "root", DateTimeOffset.UtcNow);
        var recoverySlot = Guid.NewGuid();
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), original.CandidateId, original.Id, "recovery",
            DateTimeOffset.UtcNow.AddDays(1),
            [recoverySlot, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoverySlot, "manage", DateTimeOffset.UtcNow);
        recovery.Conclude();
        var expected = BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp);

        Assert.Throws<EventBooking.Domain.Common.DomainException>(() =>
            new RecoveryBookingOutcomeCoordinator()
                .Synchronize(recovery, [expected], laterRecoveryExists: true));
    }
}
`````
