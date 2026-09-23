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
