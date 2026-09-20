# 00b — Vocabulary edits 88 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs — 1/1

<!-- vocabulary-file: {"id":292,"oldPath":"tests/EventBooking.Application.Tests/Slots/CombinedManagerAuthorizationTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs","beforeSha":"439435d19539465470232ad6a7214806b0ba00e87b6ec2847a174f884676e7f5","afterSha":"d5c584e14c00d4ca85c320a171a699f85e098f752e0ac86623e2db8aecd00133","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Events;

public class CombinedManagerAuthorizationTests
{
    [Fact]
    public async Task ACoordinatorManagerCanProposeUsingTheManagerCapability()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.Coordinator, Role.Manager],
            AppointmentTypeIds.DrugAndAlcoholTesting));
        var proposals = new InMemoryEventProposalRepository();
        var handler = new ProposeEventHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ProposeEventCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(proposals.Items);
    }

    [Fact]
    public async Task AppointmentStaffWithoutManagerCannotPropose()
    {
        var staffUserId = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staffUserId,
            [Role.AppointmentStaff],
            AppointmentTypeIds.DrugAndAlcoholTesting));
        var proposals = new InMemoryEventProposalRepository();
        var handler = new ProposeEventHandler(
            proposals,
            new StaffAccessAuthorizer(profiles),
            new FakeUnitOfWork(),
            new RecordingAuditLogger(),
            new FakeClock());

        var result = await handler.HandleAsync(
            new ProposeEventCommand(staffUserId, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(proposals.Items);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Slots/ConfirmedSlotImportParserTests.cs — 1/1

<!-- vocabulary-file: {"id":293,"oldPath":"tests/EventBooking.Application.Tests/Slots/ConfirmedSlotImportParserTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/EventImportParserTests.cs","beforeSha":"97ab3fb4ebc45c65f4ff53e0c4d89414fc2ac35b8665c63b4e41d2768f54ff60","afterSha":"b710abf09497c47cf1c7fad69836a6d96e1169bfb138b9e5b0aa072c7a3fe818","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Slots;

public class ConfirmedSlotImportParserTests
{
    private const string Header = "date,startTime,DAT,MED,UNI";

    [Fact]
    public void AGoodFileParsesEveryRow()
    {
        var result = ConfirmedSlotImportParser.Parse(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);

        var first = result.Rows[0];
        Assert.Equal(2, first.LineNumber);
        Assert.Equal(new DateOnly(2026, 9, 10), first.Window.Date);
        Assert.Equal(new TimeOnly(9, 0), first.Window.StartTime);
        Assert.Equal(10, first.HeadcountsByAppointmentType[AppointmentTypeIds.DrugAndAlcoholTesting]);
        Assert.Equal(6, first.HeadcountsByAppointmentType[AppointmentTypeIds.MedicalCheckUp]);
        Assert.Equal(8, first.HeadcountsByAppointmentType[AppointmentTypeIds.UniformFitting]);
    }

    [Fact]
    public void BlankLinesAreTolerated()
    {
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n\n2026-09-10,09:00,10,6,8\n\n");

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void AnEmptyFileIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse("");

        Assert.Empty(result.Rows);
        Assert.Equal("The file is empty.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AWrongHeaderIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse("date,startTime,types\n2026-09-10,09:00,DAT");

        Assert.Contains("header line must read exactly", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AFileOfOnlyAHeaderIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse(Header);

        Assert.Equal("The file contains no slot rows.", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("2026-09-10,09:00,10,6")]
    [InlineData("2026-09-10,09:00,10,6,8,1")]
    public void AWrongFieldCountIsRejected(string line)
    {
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n{line}");

        Assert.Contains("Expected 5 comma-separated fields", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnparseableDateIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n10-09-2026,09:00,10,6,8");

        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
        Assert.Contains("date", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnparseableStartTimeIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n2026-09-10,9am,10,6,8");

        Assert.Contains("startTime", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("0,6,8")]
    [InlineData("-1,6,8")]
    [InlineData("abc,6,8")]
    [InlineData(",6,8")]
    public void ANonPositiveOrUnparseableHeadcountIsRejected(string counts)
    {
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n2026-09-10,09:00,{counts}");

        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
        Assert.Contains("DAT", result.Errors[0].Message);
    }

    [Fact]
    public void ADuplicateWindowWithinTheFileIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-10,09:00,1,1,1
             """);

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Contains("line 2", error.Message);
    }

    [Fact]
    public void MoreThanTwoHundredRowsIsRejectedWithoutRowErrors()
    {
        var rows = Enumerable.Range(0, 201)
            .Select(i => $"2026-01-{(i % 27) + 1:D2},{(i % 20) + 1:D2}:00,1,1,1");
        var result = ConfirmedSlotImportParser.Parse($"{Header}\n{string.Join('\n', rows)}");

        Assert.Empty(result.Rows);
        Assert.Contains("at most 200 rows", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ALateStartTimeIsARowErrorNotAnException()
    {
        var result = ConfirmedSlotImportParser.Parse(
            $"{Header}\n2026-09-10,21:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("4-hour window", error.Message);
    }

    [Fact]
    public void APastDatedRowIsRejected()
    {
        var result = ConfirmedSlotImportParser.Parse(
            $"{Header}\n2026-09-01,09:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("future", error.Message);
    }

    [Fact]
    public void AFutureDatedRowParsesWhenTodayIsSupplied()
    {
        var result = ConfirmedSlotImportParser.Parse(
            $"{Header}\n2026-09-10,09:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/EventImportParserTests.cs — 1/1

<!-- vocabulary-file: {"id":293,"oldPath":"tests/EventBooking.Application.Tests/Slots/ConfirmedSlotImportParserTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/EventImportParserTests.cs","beforeSha":"97ab3fb4ebc45c65f4ff53e0c4d89414fc2ac35b8665c63b4e41d2768f54ff60","afterSha":"b710abf09497c47cf1c7fad69836a6d96e1169bfb138b9e5b0aa072c7a3fe818","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Events;

public class EventImportParserTests
{
    private const string Header = "date,startTime,DAT,MED,UNI";

    [Fact]
    public void AGoodFileParsesEveryRow()
    {
        var result = EventImportParser.Parse(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);

        var first = result.Rows[0];
        Assert.Equal(2, first.LineNumber);
        Assert.Equal(new DateOnly(2026, 9, 10), first.Window.Date);
        Assert.Equal(new TimeOnly(9, 0), first.Window.StartTime);
        Assert.Equal(10, first.HeadcountsByAppointmentType[AppointmentTypeIds.DrugAndAlcoholTesting]);
        Assert.Equal(6, first.HeadcountsByAppointmentType[AppointmentTypeIds.MedicalCheckUp]);
        Assert.Equal(8, first.HeadcountsByAppointmentType[AppointmentTypeIds.UniformFitting]);
    }

    [Fact]
    public void BlankLinesAreTolerated()
    {
        var result = EventImportParser.Parse($"{Header}\n\n2026-09-10,09:00,10,6,8\n\n");

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void AnEmptyFileIsRejected()
    {
        var result = EventImportParser.Parse("");

        Assert.Empty(result.Rows);
        Assert.Equal("The file is empty.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AWrongHeaderIsRejected()
    {
        var result = EventImportParser.Parse("date,startTime,types\n2026-09-10,09:00,DAT");

        Assert.Contains("header line must read exactly", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AFileOfOnlyAHeaderIsRejected()
    {
        var result = EventImportParser.Parse(Header);

        Assert.Equal("The file contains no event rows.", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("2026-09-10,09:00,10,6")]
    [InlineData("2026-09-10,09:00,10,6,8,1")]
    public void AWrongFieldCountIsRejected(string line)
    {
        var result = EventImportParser.Parse($"{Header}\n{line}");

        Assert.Contains("Expected 5 comma-separated fields", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnparseableDateIsRejected()
    {
        var result = EventImportParser.Parse($"{Header}\n10-09-2026,09:00,10,6,8");

        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
        Assert.Contains("date", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnparseableStartTimeIsRejected()
    {
        var result = EventImportParser.Parse($"{Header}\n2026-09-10,9am,10,6,8");

        Assert.Contains("startTime", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("0,6,8")]
    [InlineData("-1,6,8")]
    [InlineData("abc,6,8")]
    [InlineData(",6,8")]
    public void ANonPositiveOrUnparseableHeadcountIsRejected(string counts)
    {
        var result = EventImportParser.Parse($"{Header}\n2026-09-10,09:00,{counts}");

        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
        Assert.Contains("DAT", result.Errors[0].Message);
    }

    [Fact]
    public void ADuplicateWindowWithinTheFileIsRejected()
    {
        var result = EventImportParser.Parse(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-10,09:00,1,1,1
             """);

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Contains("line 2", error.Message);
    }

    [Fact]
    public void MoreThanTwoHundredRowsIsRejectedWithoutRowErrors()
    {
        var rows = Enumerable.Range(0, 201)
            .Select(i => $"2026-01-{(i % 27) + 1:D2},{(i % 20) + 1:D2}:00,1,1,1");
        var result = EventImportParser.Parse($"{Header}\n{string.Join('\n', rows)}");

        Assert.Empty(result.Rows);
        Assert.Contains("at most 200 rows", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ALateStartTimeIsARowErrorNotAnException()
    {
        var result = EventImportParser.Parse(
            $"{Header}\n2026-09-10,21:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("4-hour window", error.Message);
    }

    [Fact]
    public void APastDatedRowIsRejected()
    {
        var result = EventImportParser.Parse(
            $"{Header}\n2026-09-01,09:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("future", error.Message);
    }

    [Fact]
    public void AFutureDatedRowParsesWhenTodayIsSupplied()
    {
        var result = EventImportParser.Parse(
            $"{Header}\n2026-09-10,09:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Slots/GetManagerSlotBoardHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":294,"oldPath":"tests/EventBooking.Application.Tests/Slots/GetManagerSlotBoardHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs","beforeSha":"ca7b87cb3066e174e9dcbcaf8670854b75a71d75e44d79e020692c84cdea4cc3","afterSha":"71ab39bb008ddf287ebb6e0ba7cea26bbfc65e61bc9a3fbe06ed7e508c838cd9","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class GetManagerSlotBoardHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerSlotBoardHandler Handler =>
        new(_proposals, _confirmedSlots, _roles, _clock);

    public GetManagerSlotBoardHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));
    }

    [Fact]
    public async Task OpenProposalsShowWhoHasAcceptedAndWhetherIHave()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.OpenProposals);
        Assert.Equal(new DateOnly(2026, 9, 10), view.Date);
        Assert.Equal(new TimeOnly(9, 0), view.StartTime);
        Assert.Equal(new TimeOnly(13, 0), view.EndTime);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Medical Check-up" },
            view.AcceptedByAppointmentTypeNames);
        Assert.True(view.AcceptedByMe);
        Assert.True(view.CreatedByMe);
    }

    [Fact]
    public async Task AProposalIAmYetToAcceptIsFlaggedAsSuch()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 12), new TimeOnly(13, 0)),
            UniformManager);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.False(view.CreatedByMe);
        Assert.Equal(new[] { "Uniform Fitting" }, view.AcceptedByAppointmentTypeNames);
    }

    [Fact]
    public async Task OpenProposalsAreOrderedEarliestFirst()
    {
        foreach (var day in new[] { 14, 10, 12 })
        {
            _proposals.Add(SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                UniformManager));
        }

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            board.OpenProposals.Select(p => p.Date));
    }

    [Fact]
    public async Task ConfirmedSlotsShowOnlyMyOwnHeadcountAndRemainder()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _confirmedSlots.Add(slot);

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.ConfirmedSlots);
        Assert.Equal(10, view.MyHeadcount);
        Assert.Equal(8, view.MyRemainingCapacity);
    }

    [Fact]
    public async Task CancelledAndPastSlotsAreNotOnTheBoard()
    {
        var cancelled = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        cancelled.Cancel();
        _confirmedSlots.Add(cancelled);
        _confirmedSlots.Add(ConfirmedSlot.CreateFrom(
            Guid.NewGuid(), FullyAcceptedProposal(new DateOnly(2026, 8, 30))));

        var board = (await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Empty(board.ConfirmedSlots);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private static SlotProposal FullyAcceptedProposal(DateOnly? date = null)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(date ?? new DateOnly(2026, 9, 8), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        return proposal;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":294,"oldPath":"tests/EventBooking.Application.Tests/Slots/GetManagerSlotBoardHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs","beforeSha":"ca7b87cb3066e174e9dcbcaf8670854b75a71d75e44d79e020692c84cdea4cc3","afterSha":"71ab39bb008ddf287ebb6e0ba7cea26bbfc65e61bc9a3fbe06ed7e508c838cd9","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class GetManagerEventBoardHandlerTests
{
    private static readonly Guid DrugAndAlcoholManager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid MedicalManager = Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager = Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerEventBoardHandler Handler =>
        new(_proposals, _events, _roles, _clock);

    public GetManagerEventBoardHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            DrugAndAlcoholManager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(
            MedicalManager, Role.Manager, AppointmentTypeIds.MedicalCheckUp));
        _roles.Add(StaffAccessProfile.Create(
            UniformManager, Role.Manager, AppointmentTypeIds.UniformFitting));
    }

    [Fact]
    public async Task OpenProposalsShowWhoHasAcceptedAndWhetherIHave()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.OpenProposals);
        Assert.Equal(new DateOnly(2026, 9, 10), view.Date);
        Assert.Equal(new TimeOnly(9, 0), view.StartTime);
        Assert.Equal(new TimeOnly(13, 0), view.EndTime);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Medical Check-up" },
            view.AcceptedByAppointmentTypeNames);
        Assert.True(view.AcceptedByMe);
        Assert.True(view.CreatedByMe);
    }

    [Fact]
    public async Task AProposalIAmYetToAcceptIsFlaggedAsSuch()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 12), new TimeOnly(13, 0)),
            UniformManager);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        _proposals.Add(proposal);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.False(view.CreatedByMe);
        Assert.Equal(new[] { "Uniform Fitting" }, view.AcceptedByAppointmentTypeNames);
    }

    [Fact]
    public async Task OpenProposalsAreOrderedEarliestFirst()
    {
        foreach (var day in new[] { 14, 10, 12 })
        {
            _proposals.Add(EventProposal.Create(
                Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0)),
                UniformManager));
        }

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Equal(
            new[] { new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14) },
            board.OpenProposals.Select(p => p.Date));
    }

    [Fact]
    public async Task EventsShowOnlyMyOwnHeadcountAndRemainder()
    {
        var eventItem = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _events.Add(eventItem);

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        var view = Assert.Single(board.Events);
        Assert.Equal(10, view.MyHeadcount);
        Assert.Equal(8, view.MyRemainingCapacity);
    }

    [Fact]
    public async Task CancelledAndPastEventsAreNotOnTheBoard()
    {
        var cancelled = Event.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());
        cancelled.Cancel();
        _events.Add(cancelled);
        _events.Add(Event.CreateFrom(
            Guid.NewGuid(), FullyAcceptedProposal(new DateOnly(2026, 8, 30))));

        var board = (await Handler.HandleAsync(
            new GetManagerEventBoardQuery(DrugAndAlcoholManager), CancellationToken.None)).Value;

        Assert.Empty(board.Events);
    }

    [Fact]
    public async Task ANonManagerIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private static EventProposal FullyAcceptedProposal(DateOnly? date = null)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(date ?? new DateOnly(2026, 9, 8), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        return proposal;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Slots/ImportConfirmedSlotsHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":295,"oldPath":"tests/EventBooking.Application.Tests/Slots/ImportConfirmedSlotsHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/ImportEventsHandlerTests.cs","beforeSha":"ceb63f221b96cb7fc4515e3b403345781acd0c5d55d1fb8da6e55ca7b9dc2e55","afterSha":"5ddd85b346c67d51ca19a3971b470d74af2300a842e8af8bea5d8589f364a900","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.Slots;

public class ImportConfirmedSlotsHandlerTests
{
    private const string Header = "date,startTime,DAT,MED,UNI";
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();

    private ImportConfirmedSlotsHandler Handler => new(_confirmedSlots, _roles, _unitOfWork, _audit, new FakeClock());

    public ImportConfirmedSlotsHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private Task<Result<ConfirmedSlotImportOutcome>> Import(string csv, Guid? actor = null) =>
        Handler.HandleAsync(new ImportConfirmedSlotsCommand(actor ?? Admin, csv), CancellationToken.None);

    [Fact]
    public async Task AGoodFileCreatesOneConfirmedSlotPerRowWithNoProposal()
    {
        var result = await Import(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Equal(2, result.Value.ImportedCount);
        Assert.Empty(result.Value.Errors);

        Assert.Equal(2, _confirmedSlots.Items.Count);
        Assert.All(_confirmedSlots.Items, s => Assert.Null(s.ProposalId));
        var first = _confirmedSlots.Items.Single(s => s.Window.Date == new DateOnly(2026, 9, 10));
        Assert.Equal(10, first.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task EveryImportedSlotWritesExactlyOneSlotImportedEntry()
    {
        await Import(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.Equal(2, _audit.Entries.Count(e => e.Action == AuditAction.SlotImported));
        Assert.All(_audit.Entries, e => Assert.Equal(Admin.ToString(), e.ActorId));
        Assert.Contains(_audit.Entries, e => e.Details != null && e.Details.Contains("line 2"));
    }

    [Fact]
    public async Task ARejectedFileCreatesNothingAndAuditsNothing()
    {
        var result = await Import($"{Header}\n2026-09-10,09:00,0,6,8");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Single(result.Value.Errors);
        Assert.Empty(_confirmedSlots.Items);
        Assert.Empty(_audit.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AManagerIsForbidden()
    {
        var result = await Import($"{Header}\n2026-09-10,09:00,10,6,8", Manager);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_confirmedSlots.Items);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/ImportEventsHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":295,"oldPath":"tests/EventBooking.Application.Tests/Slots/ImportConfirmedSlotsHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/ImportEventsHandlerTests.cs","beforeSha":"ceb63f221b96cb7fc4515e3b403345781acd0c5d55d1fb8da6e55ca7b9dc2e55","afterSha":"5ddd85b346c67d51ca19a3971b470d74af2300a842e8af8bea5d8589f364a900","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Common;
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.Events;

public class ImportEventsHandlerTests
{
    private const string Header = "date,startTime,DAT,MED,UNI";
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");

    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();

    private ImportEventsHandler Handler => new(_events, _roles, _unitOfWork, _audit, new FakeClock());

    public ImportEventsHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private Task<Result<EventImportOutcome>> Import(string csv, Guid? actor = null) =>
        Handler.HandleAsync(new ImportEventsCommand(actor ?? Admin, csv), CancellationToken.None);

    [Fact]
    public async Task AGoodFileCreatesOneEventPerRowWithNoProposal()
    {
        var result = await Import(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        Assert.Equal(2, result.Value.ImportedCount);
        Assert.Empty(result.Value.Errors);

        Assert.Equal(2, _events.Items.Count);
        Assert.All(_events.Items, s => Assert.Null(s.ProposalId));
        var first = _events.Items.Single(s => s.Window.Date == new DateOnly(2026, 9, 10));
        Assert.Equal(10, first.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task EveryImportedEventWritesExactlyOneEventImportedEntry()
    {
        await Import(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.Equal(2, _audit.Entries.Count(e => e.Action == AuditAction.EventImported));
        Assert.All(_audit.Entries, e => Assert.Equal(Admin.ToString(), e.ActorId));
        Assert.Contains(_audit.Entries, e => e.Details != null && e.Details.Contains("line 2"));
    }

    [Fact]
    public async Task ARejectedFileCreatesNothingAndAuditsNothing()
    {
        var result = await Import($"{Header}\n2026-09-10,09:00,0,6,8");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Accepted);
        Assert.Single(result.Value.Errors);
        Assert.Empty(_events.Items);
        Assert.Empty(_audit.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AManagerIsForbidden()
    {
        var result = await Import($"{Header}\n2026-09-10,09:00,10,6,8", Manager);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(_events.Items);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Slots/ManagerSlotBoardHeadcountRevisionTests.cs — 1/1

<!-- vocabulary-file: {"id":296,"oldPath":"tests/EventBooking.Application.Tests/Slots/ManagerSlotBoardHeadcountRevisionTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs","beforeSha":"93447ed79ec3fdcc6adb16e5afc4a6c2c5f0514f0a4d45cfef0eba05d0bdc88b","afterSha":"b701588adefada6ced00d1e80f6e14203fcef49c7b78ff74b56d61f30980f179","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class ManagerSlotBoardHeadcountRevisionTests
{
    private static readonly Guid CurrentManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryConfirmedSlotRepository _confirmedSlots = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock =
        new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerSlotBoardHandler Handler =>
        new(_proposals, _confirmedSlots, _roles, _clock);

    public ManagerSlotBoardHeadcountRevisionTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            CurrentManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private SlotProposal AddProposal()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CurrentManager);
        _proposals.Add(proposal);
        return proposal;
    }

    [Fact]
    public async Task MyOpenProposalReturnsMyCurrentAcceptedHeadcount()
    {
        var proposal = AddProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            CurrentManager,
            12);

        var result = await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.True(view.AcceptedByMe);
        Assert.Equal(12, view.MyAcceptedHeadcount);
    }

    [Fact]
    public async Task AnotherManagersHeadcountIsNotReturnedAsMine()
    {
        var proposal = AddProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            FormerManager,
            10);

        var result = await Handler.HandleAsync(
            new GetManagerSlotBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.Null(view.MyAcceptedHeadcount);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing" },
            view.AcceptedByAppointmentTypeNames);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs — 1/1

<!-- vocabulary-file: {"id":296,"oldPath":"tests/EventBooking.Application.Tests/Slots/ManagerSlotBoardHeadcountRevisionTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs","beforeSha":"93447ed79ec3fdcc6adb16e5afc4a6c2c5f0514f0a4d45cfef0eba05d0bdc88b","afterSha":"b701588adefada6ced00d1e80f6e14203fcef49c7b78ff74b56d61f30980f179","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class ManagerEventBoardHeadcountRevisionTests
{
    private static readonly Guid CurrentManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeClock _clock =
        new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private GetManagerEventBoardHandler Handler =>
        new(_proposals, _events, _roles, _clock);

    public ManagerEventBoardHeadcountRevisionTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            CurrentManager,
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    private EventProposal AddProposal()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CurrentManager);
        _proposals.Add(proposal);
        return proposal;
    }

    [Fact]
    public async Task MyOpenProposalReturnsMyCurrentAcceptedHeadcount()
    {
        var proposal = AddProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            CurrentManager,
            12);

        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.True(view.AcceptedByMe);
        Assert.Equal(12, view.MyAcceptedHeadcount);
    }

    [Fact]
    public async Task AnotherManagersHeadcountIsNotReturnedAsMine()
    {
        var proposal = AddProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            FormerManager,
            10);

        var result = await Handler.HandleAsync(
            new GetManagerEventBoardQuery(CurrentManager),
            CancellationToken.None);

        var view = Assert.Single(result.Value.OpenProposals);
        Assert.False(view.AcceptedByMe);
        Assert.Null(view.MyAcceptedHeadcount);
        Assert.Equal(
            new[] { "Drug & Alcohol Testing" },
            view.AcceptedByAppointmentTypeNames);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Slots/ProposeSlotHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":297,"oldPath":"tests/EventBooking.Application.Tests/Slots/ProposeSlotHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs","beforeSha":"29770e05189501fd0a6b4d2b7ab293a7c8d160beda656afa060fea71adff73de","afterSha":"1a72f123b0c0b751edfab5694eb12fbd17c455befbb05bb03dbac62b7dc56f88","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Slots;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Slots;

public class ProposeSlotHandlerTests
{
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemorySlotProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private ProposeSlotHandler Handler => new(_proposals, _roles, _unitOfWork, _audit, _clock);

    public ProposeSlotHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
    }

    [Fact]
    public async Task AManagerCanProposeAFutureWindow()
    {
        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var proposal = Assert.Single(_proposals.Items);
        Assert.Equal(result.Value, proposal.Id);
        Assert.Equal(SlotProposalStatus.Open, proposal.Status);
        Assert.Equal(new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)), proposal.Window);
        Assert.Equal(Manager, proposal.CreatedByManagerUserId);
        Assert.Empty(proposal.Acceptances);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProposingWritesAnAuditEntry()
    {
        await Handler.HandleAsync(
            new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditEntityTypes.SlotProposal, entry.EntityType);
        Assert.Equal(AuditAction.ProposalCreated, entry.Action);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(Manager.ToString(), entry.ActorId);
    }

    [Fact]
    public async Task ACoordinatorCannotProposeASlot()
    {
        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Coordinator, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_proposals.Items);
    }

    [Fact]
    public async Task AnUnknownUserCannotProposeASlot()
    {
        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Guid.NewGuid(), new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(2026, 9, 3)]
    [InlineData(2026, 9, 2)]
    public async Task ATodayOrPastWindowIsRejected(int year, int month, int day)
    {
        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Manager, new DateOnly(year, month, day), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("A slot must be proposed for a future date.", result.Error.Message);
    }

    [Fact]
    public async Task AWindowThatWouldRunPastMidnightIsRejectedByTheDomain()
    {
        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(22, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(
            "startTime must leave room for the full 4-hour window on the same day.",
            result.Error.Message);
    }

    [Fact]
    public async Task ASecondOpenProposalForTheSameWindowIsAConflict()
    {
        var command = new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        await Handler.HandleAsync(command, CancellationToken.None);

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("An open proposal already exists for that window.", result.Error.Message);
        Assert.Single(_proposals.Items);
    }

    [Fact]
    public async Task ADifferentWindowOnTheSameDayIsAllowed()
    {
        await Handler.HandleAsync(
            new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var result = await Handler.HandleAsync(
            new ProposeSlotCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(13, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _proposals.Items.Count);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs — 1/1

<!-- vocabulary-file: {"id":297,"oldPath":"tests/EventBooking.Application.Tests/Slots/ProposeSlotHandlerTests.cs","newPath":"tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs","beforeSha":"29770e05189501fd0a6b4d2b7ab293a7c8d160beda656afa060fea71adff73de","afterSha":"1a72f123b0c0b751edfab5694eb12fbd17c455befbb05bb03dbac62b7dc56f88","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Events;

public class ProposeEventHandlerTests
{
    private static readonly Guid Manager = Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryEventProposalRepository _proposals = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

    private ProposeEventHandler Handler => new(_proposals, _roles, _unitOfWork, _audit, _clock);

    public ProposeEventHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
    }

    [Fact]
    public async Task AManagerCanProposeAFutureWindow()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var proposal = Assert.Single(_proposals.Items);
        Assert.Equal(result.Value, proposal.Id);
        Assert.Equal(EventProposalStatus.Open, proposal.Status);
        Assert.Equal(new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)), proposal.Window);
        Assert.Equal(Manager, proposal.CreatedByManagerUserId);
        Assert.Empty(proposal.Acceptances);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProposingWritesAnAuditEntry()
    {
        await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditEntityTypes.EventProposal, entry.EntityType);
        Assert.Equal(AuditAction.ProposalCreated, entry.Action);
        Assert.Equal(ActorType.Staff, entry.ActorType);
        Assert.Equal(Manager.ToString(), entry.ActorId);
    }

    [Fact]
    public async Task ACoordinatorCannotProposeAEvent()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Coordinator, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal("This staff profile cannot perform this operation.", result.Error.Message);
        Assert.Empty(_proposals.Items);
    }

    [Fact]
    public async Task AnUnknownUserCannotProposeAEvent()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Guid.NewGuid(), new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(2026, 9, 3)]
    [InlineData(2026, 9, 2)]
    public async Task ATodayOrPastWindowIsRejected(int year, int month, int day)
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(year, month, day), new TimeOnly(9, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal("A event must be proposed for a future date.", result.Error.Message);
    }

    [Fact]
    public async Task AWindowThatWouldRunPastMidnightIsRejectedByTheDomain()
    {
        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(22, 0)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(
            "startTime must leave room for the full 4-hour window on the same day.",
            result.Error.Message);
    }

    [Fact]
    public async Task ASecondOpenProposalForTheSameWindowIsAConflict()
    {
        var command = new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0));
        await Handler.HandleAsync(command, CancellationToken.None);

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("An open proposal already exists for that window.", result.Error.Message);
        Assert.Single(_proposals.Items);
    }

    [Fact]
    public async Task ADifferentWindowOnTheSameDayIsAllowed()
    {
        await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            CancellationToken.None);

        var result = await Handler.HandleAsync(
            new ProposeEventCommand(Manager, new DateOnly(2026, 9, 10), new TimeOnly(13, 0)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _proposals.Items.Count);
    }
}
`````
