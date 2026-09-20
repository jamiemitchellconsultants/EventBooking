# 00b — Vocabulary edits 74 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs — 1/1

<!-- vocabulary-file: {"id":242,"oldPath":"tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs","newPath":"tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs","beforeSha":"115ed949e9b2846e87b111a9c38402b171fedf85400f6b8566bccd56b7bdb6fe","afterSha":"0a7bc9cee12100c6d395efc131f759f6fc41b07e70947ccec8b1a161c91d841c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Access;

public class StaffAccessAuthorizerTests
{
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();

    [Theory]
    [InlineData(Role.Admin, StaffCapability.ManageSettings, true)]
    [InlineData(Role.Admin, StaffCapability.ManageAttendees, false)]
    [InlineData(Role.Admin, StaffCapability.ViewAttendeeDashboards, false)]
    [InlineData(Role.Admin, StaffCapability.ImportEvents, true)]
    [InlineData(Role.Coordinator, StaffCapability.ManageAttendees, true)]
    [InlineData(Role.Coordinator, StaffCapability.ImportEvents, true)]
    [InlineData(Role.Coordinator, StaffCapability.ManageSettings, false)]
    [InlineData(Role.Manager, StaffCapability.ManageEventNegotiation, true)]
    [InlineData(Role.Manager, StaffCapability.ConductAppointments, true)]
    [InlineData(Role.AppointmentStaff, StaffCapability.ConductAppointments, true)]
    [InlineData(Role.AppointmentStaff, StaffCapability.CancelEvent, false)]
    [InlineData(Role.Admin, StaffCapability.ViewEventOperations, true)]
    [InlineData(Role.Coordinator, StaffCapability.ViewEventOperations, true)]
    public async Task SingleRoleCapabilitiesMatchTheMatrix(
        Role role,
        StaffCapability capability,
        bool expected)
    {
        var staffUserId = Add(role);
        var result = await Authorizer().AuthorizeAsync(
            staffUserId, capability, null, CancellationToken.None);

        Assert.Equal(expected, result.IsSuccess);
    }

    [Fact]
    public async Task CombinedCoordinatorManagerGetsTheUnionAndTrustedScope()
    {
        var staffUserId = Guid.NewGuid();
        _profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.Coordinator, Role.Manager],
            AppointmentTypeIds.MedicalCheckUp));

        var attendee = await Authorizer().AuthorizeAsync(
            staffUserId, StaffCapability.ManageAttendees, null, CancellationToken.None);
        var manager = await Authorizer().AuthorizeAsync(
            staffUserId,
            StaffCapability.ManageEventNegotiation,
            AppointmentTypeIds.MedicalCheckUp,
            CancellationToken.None);

        Assert.True(attendee.IsSuccess);
        Assert.True(manager.IsSuccess);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, manager.Value.AppointmentTypeId);
    }

    [Fact]
    public async Task AScopedCapabilityRejectsAnotherAppointmentType()
    {
        var manager = Add(Role.Manager);

        var result = await Authorizer().AuthorizeAsync(
            manager,
            StaffCapability.ManageEventNegotiation,
            AppointmentTypeIds.UniformFitting,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task AnUnassignedIdentityIsDenied()
    {
        var result = await Authorizer().AuthorizeAsync(
            Guid.NewGuid(), StaffCapability.ViewEventOperations, null, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(StaffCapability.ManageSettings)]
    [InlineData(StaffCapability.ManageStaffAccess)]
    [InlineData(StaffCapability.ImportEvents)]
    [InlineData(StaffCapability.ManageAttendees)]
    [InlineData(StaffCapability.ViewAttendeeDashboards)]
    [InlineData(StaffCapability.ViewAttendeeAudit)]
    [InlineData(StaffCapability.ViewEventAudit)]
    [InlineData(StaffCapability.ManageEventNegotiation)]
    [InlineData(StaffCapability.ViewEventOperations)]
    [InlineData(StaffCapability.CancelEvent)]
    [InlineData(StaffCapability.ConductAppointments)]
    public async Task AScopedCapabilityIsDeniedWhenScopeIsNull(StaffCapability capability)
    {
        var profile = StaffAccessProfile.Create(Guid.NewGuid(), [Role.Manager], null);
        var repository = new InMemoryStaffAccessProfileRepository();
        repository.Items.Add(profile);
        var authorizer = new StaffAccessAuthorizer(repository);

        var result = await authorizer.AuthorizeAsync(
            profile.StaffUserId, capability, requiredAppointmentTypeId: null, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task AnUnscopedCoordinatorCapabilityIsStillGrantedWhenScopeIsNull()
    {
        var profile = StaffAccessProfile.Create(
            Guid.NewGuid(), [Role.Coordinator, Role.Manager], null);
        var repository = new InMemoryStaffAccessProfileRepository();
        repository.Items.Add(profile);
        var authorizer = new StaffAccessAuthorizer(repository);

        var result = await authorizer.AuthorizeAsync(
            profile.StaffUserId,
            StaffCapability.ManageAttendees,
            requiredAppointmentTypeId: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private Guid Add(Role role)
    {
        var id = Guid.NewGuid();
        Guid? scope = role is Role.Manager or Role.AppointmentStaff
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        _profiles.Add(StaffAccessProfile.Create(id, role, scope));
        return id;
    }

    private StaffAccessAuthorizer Authorizer() => new(_profiles);
}
`````

## before — tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs — 1/1

<!-- vocabulary-file: {"id":243,"oldPath":"tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs","beforeSha":"06095d7aea89cedd2af3c006ebdbda44ed31a66cf1250848969a921916c6752d","afterSha":"5eaa326b8bf8718abf6aacc281123726f3eec32fb580bc92c701ec8c8488ea8b","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs — 1/1

<!-- vocabulary-file: {"id":243,"oldPath":"tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs","beforeSha":"06095d7aea89cedd2af3c006ebdbda44ed31a66cf1250848969a921916c6752d","afterSha":"5eaa326b8bf8718abf6aacc281123726f3eec32fb580bc92c701ec8c8488ea8b","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies the roster CSV projection: columns, ordering, escaping, time, and filename.</summary>
public sealed class AppointmentRosterCsvFormatterTests
{
    private const string Header =
        "Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At";

    private static AppointmentRosterCsvFormatter Formatter() => new(new FakeClock());

    private static string[] LinesOf(string csv) =>
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void FormatEmitsExactHeaderAndColumnOrder()
    {
        var result = Formatter().Format(EventWithRows());

        Assert.Equal(Header, LinesOf(result.CsvText)[0]);
    }

    [Fact]
    public void FormatPreservesRowOrderAndRepeatsAppointmentType()
    {
        var result = Formatter().Format(EventWithRows());

        var lines = LinesOf(result.CsvText);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Amina Yusuf,", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("Bruno Costa,", lines[2], StringComparison.Ordinal);
        Assert.Contains(",Drug & Alcohol Testing,", lines[1], StringComparison.Ordinal);
        Assert.Contains(",Drug & Alcohol Testing,", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void FormatEmptyEventYieldsHeaderOnly()
    {
        var result = Formatter().Format(EventWith([]));

        Assert.Equal(Header + "\n", result.CsvText);
    }

    [Fact]
    public void FormatNullTimestampsRenderAsEmpty()
    {
        var result = Formatter().Format(EventWith([Row("Amina Yusuf", BookingAppointmentStatus.Expected)]));

        var dataLine = LinesOf(result.CsvText)[1];
        Assert.EndsWith(",,", dataLine, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatNonNullTimestampsRenderAsTransitionalLocationIso8601()
    {
        var checkedInAt = new DateTimeOffset(2026, 9, 15, 9, 35, 0, TimeSpan.Zero);
        var outcomeAt = new DateTimeOffset(2026, 9, 15, 10, 5, 0, TimeSpan.Zero);
        var result = Formatter().Format(EventWith(
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
        var result = Formatter().Format(EventWith(
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
        var result = Formatter().Format(EventWith(
        [
            Row("Okafor, Ada \"Bisi\"", BookingAppointmentStatus.Expected),
        ]));

        Assert.Contains("\"Okafor, Ada \"\"Bisi\"\"\",", result.CsvText, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatNeverEmitsTheCommandTargetOrConcurrencyToken()
    {
        var detail = EventWithRows();

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
        var result = Formatter().Format(EventWithRows());

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
        var result = Formatter().Format(EventWith(
        [
            Row(name, BookingAppointmentStatus.Expected),
        ]));

        var dataLine = LinesOf(result.CsvText)[1];
        Assert.Contains("'" + name, dataLine, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatFilenameKeepsAMidnightStartTimeFourDigits()
    {
        var result = Formatter().Format(EventWith([], startTime: new TimeOnly(0, 5)));

        Assert.Equal("roster-drug-&-alcohol-testing-2026-09-15-0005.csv", result.FileName);
    }

    private static BookingAppointmentRow Row(
        string attendeeName,
        BookingAppointmentStatus status,
        DateTimeOffset? checkedInAt = null,
        DateTimeOffset? outcomeAt = null) => new()
    {
        BookingAppointmentId = Guid.NewGuid(),
        AttendeeName = attendeeName,
        AttendeeEmail = $"{attendeeName.Split(' ')[0].ToLowerInvariant()}@mail.com",
        Status = status,
        CheckedInAt = checkedInAt,
        OutcomeAt = outcomeAt,
        Version = 1,
    };

    private static AppointmentEventDetail EventWith(
        IReadOnlyList<BookingAppointmentRow> rows,
        TimeOnly? startTime = null) => new()
    {
        AppointmentTypeName = "Drug & Alcohol Testing",
        EventId = Guid.NewGuid(),
        Date = new DateOnly(2026, 9, 15),
        StartTime = startTime ?? new TimeOnly(9, 30),
        EndTime = (startTime ?? new TimeOnly(9, 30)).AddHours(4),
        Appointments = rows,
    };

    private static AppointmentEventDetail EventWithRows() => EventWith(
    [
        Row("Amina Yusuf", BookingAppointmentStatus.Expected),
        Row("Bruno Costa", BookingAppointmentStatus.CheckedIn,
            new DateTimeOffset(2026, 9, 15, 9, 35, 0, TimeSpan.Zero)),
    ]);
}
`````

## before — tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":244,"oldPath":"tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs","beforeSha":"eaf846a320030484025333bbecbd47365f2df1160492cfb71b00dfa94fcc01fd","afterSha":"55e886b8f3de87f3bbf826fe5be20e1b58338eda7600229a16a1107fa45ebe94","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":244,"oldPath":"tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs","beforeSha":"eaf846a320030484025333bbecbd47365f2df1160492cfb71b00dfa94fcc01fd","afterSha":"55e886b8f3de87f3bbf826fe5be20e1b58338eda7600229a16a1107fa45ebe94","side":"after","part":1,"parts":1} -->

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

        var result = await handler.ListEventsAsync(staff, CancellationToken.None);

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
            .GetEventAsync(staff, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentTypeIds.UniformFitting, queries.LastAppointmentTypeId);
    }

    /// <summary>Verifies Admin denial occurs before the attendee-data query is invoked.</summary>
    [Fact]
    public async Task AdminIsDeniedBeforeAnyWorkspaceQuery()
    {
        var admin = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new RecordingQueries();

        var result = await new GetAppointmentWorkspaceHandler(
            new StaffAccessAuthorizer(profiles), queries, new FakeClock())
            .ListEventsAsync(admin, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, queries.ListCallCount);
        Assert.Equal(0, queries.DetailCallCount);
    }

    /// <summary>Verifies absent and cross-scope events share the same not-found application result.</summary>
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
            .GetEventAsync(staff, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("No such appointment workspace eventItem.", result.Error.Message);
    }

    private sealed class RecordingQueries : IAppointmentWorkspaceQueries
    {
        public int ListCallCount { get; private set; }
        public int DetailCallCount { get; private set; }
        public Guid? LastAppointmentTypeId { get; private set; }
        public AppointmentEventDetail? ReturnDetail { get; set; } = new()
        {
            AppointmentTypeName = "Drug & Alcohol Testing",
            EventId = Guid.NewGuid(),
            Date = new DateOnly(2026, 9, 7),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(13, 0),
            Appointments = [],
        };

        public Task<AppointmentWorkspaceEventList> ListEventsAsync(
            Guid appointmentTypeId,
            DateOnly onOrAfter,
            CancellationToken cancellationToken)
        {
            ListCallCount++;
            LastAppointmentTypeId = appointmentTypeId;
            return Task.FromResult(new AppointmentWorkspaceEventList
            {
                AppointmentTypeName = "Medical Check-up",
                Events = [],
            });
        }

        public Task<AppointmentEventDetail?> GetEventAsync(
            Guid appointmentTypeId,
            Guid eventId,
            CancellationToken cancellationToken)
        {
            DetailCallCount++;
            LastAppointmentTypeId = appointmentTypeId;
            return Task.FromResult(ReturnDetail);
        }
    }
}
`````

## before — tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs — 1/1

<!-- vocabulary-file: {"id":245,"oldPath":"tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs","beforeSha":"b3cd75571cc0271ae90fab0105a611a497ea07ba52fb7abfe9fa6d278b5355ac","afterSha":"1d761c8807c81b57e0d24054b03daeb301f8d55b371893f93b541ae43d8388ca","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs — 1/1

<!-- vocabulary-file: {"id":245,"oldPath":"tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs","beforeSha":"b3cd75571cc0271ae90fab0105a611a497ea07ba52fb7abfe9fa6d278b5355ac","afterSha":"1d761c8807c81b57e0d24054b03daeb301f8d55b371893f93b541ae43d8388ca","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies late outcomes on past events follow the existing timing and correction rules.</summary>
public sealed class LateNoShowOutcomeTests
{
    /// <summary>Verifies Expected to NoShow succeeds the day after the event date with version and audit.</summary>
    [Fact]
    public async Task NoShowDayAfterEventDateSucceeds()
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

    /// <summary>Verifies check-in is rejected once the event date has passed.</summary>
    [Fact]
    public async Task CheckInAfterEventDateIsRejected()
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
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = Event.CreateImported(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, "manage-token", now.AddDays(-1));
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
            attendees,
            invites,
            events,
            new RecoveryBookingOutcomeCoordinator(),
            audit,
            unitOfWork,
            clock);
        return new Scenario(
            staff, attendee, eventItem, appointment, booking, audit, operations, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        Attendee Attendee,
        Event Event,
        BookingAppointment Appointment,
        Booking Booking,
        RecordingAuditLogger Audit,
        TransactionOperationLog Operations,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````

## before — tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs — 1/1

<!-- vocabulary-file: {"id":246,"oldPath":"tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs","beforeSha":"25a56a1ec64a906adc4abc5e5ff6e83b911f7cdf9a332160873615b19bb1da90","afterSha":"04494d6848eca8c2d0d5a616e5f7f4724d3a7bd3dc5c756cf3f62c1042908779","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs — 1/1

<!-- vocabulary-file: {"id":246,"oldPath":"tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs","beforeSha":"25a56a1ec64a906adc4abc5e5ff6e83b911f7cdf9a332160873615b19bb1da90","afterSha":"04494d6848eca8c2d0d5a616e5f7f4724d3a7bd3dc5c756cf3f62c1042908779","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

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
        var readiness = new AttendeeReadinessCalculator().Calculate(
            new AttendeeReadinessSnapshot(
                scenario.Attendee.Id,
                Guid.NewGuid(),
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                scenario.Booking.Id,
                [
                    new AttendeeReadinessAttempt(
                        scenario.Appointment.Id,
                        AppointmentTypeIds.DrugAndAlcoholTesting,
                        scenario.Appointment.Status,
                        scenario.Booking.Id,
                        scenario.Booking.Status,
                        outcome.Value.OutcomeAt!.Value),
                ]));

        Assert.Equal(AttendeeReadinessCode.AppointmentsOutstanding, readiness.Code);
        var outstanding = Assert.Single(readiness.OutstandingAppointmentTypes);
        Assert.True(outstanding.IsRecoverable);
    }

    /// <summary>Builds a DAT-only group; the scenario needs a mapping, not an identity.</summary>
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = Event.CreateImported(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, "manage-token", now.AddDays(-1));
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
            attendees,
            invites,
            events,
            new RecoveryBookingOutcomeCoordinator(),
            new RecordingAuditLogger(),
            new FakeUnitOfWork(operations),
            new FakeClock(now));
        return new Scenario(staff, attendee, booking, appointment, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        Attendee Attendee,
        Booking Booking,
        BookingAppointment Appointment,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````

## before — tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs — 1/1

<!-- vocabulary-file: {"id":247,"oldPath":"tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs","newPath":"tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs","beforeSha":"27342724cb7e2b0a8f1adadb2f8939167ac373c3dfae4d10d7d88e8fbfc9a521","afterSha":"f2b55fd02a870208a0444b88b9d0328e1ec833c10b4747c6bf5cd1a2ed1bdb83","side":"before","part":1,"parts":1} -->

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
