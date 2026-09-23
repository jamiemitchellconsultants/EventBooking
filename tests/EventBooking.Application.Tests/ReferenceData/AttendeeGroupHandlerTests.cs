using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.ReferenceData;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.ReferenceData;

public sealed class AttendeeGroupHandlerTests
{
    private static readonly Guid Admin = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryAppointmentTypeRepository _types = new();
    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryStaffAccessProfileRepository _profiles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeClock _clock = new(Now);
    private readonly MemoryBlocking _blocking = new();

    private Guid _med;
    private Guid _fit;
    private Guid _ind;

    public AttendeeGroupHandlerTests()
    {
        _profiles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
        _types.Items.Clear();
        _med = AddType("MED", "Medical");
        _fit = AddType("FIT", "Fitness");
        _ind = AddType("IND", "Induction");
    }

    private Guid AddType(string code, string name)
    {
        var type = AppointmentType.Create(Guid.NewGuid(), code, name);
        _types.Items.Add(type);
        return type.Id;
    }

    private CreateAttendeeGroupHandler Creator => new(_groups, _types, _profiles, _unitOfWork, _audit);
    private UpdateAttendeeGroupHandler Updater => new(_groups, _types, _attendees, _invites, _profiles, _blocking, _unitOfWork, _audit, _clock);

    [Fact]
    public async Task Requirement_change_rederives_both_members_and_supersedes_the_pending_invite()
    {
        var group = (await Creator.HandleAsync(
            new CreateAttendeeGroupCommand(Admin, "NHS", "NHS staff", [_med, _fit]),
            CancellationToken.None)).Value;
        var stored = _groups.Items.Single();
        var first = Attendee.Create(Guid.NewGuid(), "Amy", "amy@example.invalid", stored, Now);
        var second = Attendee.Create(Guid.NewGuid(), "Bo", "bo@example.invalid", stored, Now);
        _attendees.Items.AddRange([first, second]);
        first.MarkInvited(Now);
        var optionEvents = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var pending = Invite.CreateInitial(Guid.NewGuid(), first.Id, Now.AddDays(7), [Guid.NewGuid()], optionEvents, [_med, _fit], 0);
        _invites.Items.Add(pending);
        _blocking.BlockingMembers = 0;
        var savesBefore = _unitOfWork.SaveCount;

        var result = await Updater.HandleAsync(
            new UpdateAttendeeGroupCommand(Admin, group.Id, null, [_med, _ind], 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { _ind, _med }.Order().ToList(), result.Value.RequirementTypeIds.Order().ToList());
        Assert.Equal(new[] { _ind, _med }.Order().ToList(), first.RequiredAppointmentTypeIds.Order().ToList());
        Assert.Equal(new[] { _ind, _med }.Order().ToList(), second.RequiredAppointmentTypeIds.Order().ToList());
        Assert.Equal(InviteStatus.Superseded, pending.Status);
        Assert.Equal(AttendeeStatus.NotYetInvited, first.Status);
        Assert.Equal(AttendeeStatus.NotYetInvited, second.Status);
        var entry = Assert.Single(_audit.Entries, e => e.Action == AuditAction.AttendeeGroupUpdated);
        Assert.Contains("2 members", entry.Details);
        Assert.Equal(savesBefore + 1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Requirement_change_with_active_booking_is_refused_and_changes_nothing()
    {
        var group = (await Creator.HandleAsync(
            new CreateAttendeeGroupCommand(Admin, "NHS", "NHS staff", [_med, _fit]),
            CancellationToken.None)).Value;
        var stored = _groups.Items.Single();
        var member = Attendee.Create(Guid.NewGuid(), "Amy", "amy@example.invalid", stored, Now);
        _attendees.Items.Add(member);
        member.MarkInvited(Now);
        var optionEvents = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var invite = Invite.CreateInitial(Guid.NewGuid(), member.Id, Now.AddDays(7), [Guid.NewGuid()], optionEvents, [_med, _fit], 0);
        _invites.Items.Add(invite);
        _ = Booking.Create(Guid.NewGuid(), invite, optionEvents[0], Now);
        _blocking.BlockingMembers = 1;

        var result = await Updater.HandleAsync(
            new UpdateAttendeeGroupCommand(Admin, group.Id, null, [_med, _ind], 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("requirements-locked", result.Error.Code);
        Assert.Equal(1L, result.Error.Data!["blockingMembers"]);
        Assert.Equal(new[] { _fit, _med }.Order().ToList(), member.RequiredAppointmentTypeIds.Order().ToList());
        Assert.Equal(AttendeeStatus.Invited, member.Status);
        Assert.Single(_audit.Entries);
    }

    [Fact]
    public async Task Deactivate_group_with_members_returns_in_use()
    {
        var group = (await Creator.HandleAsync(
            new CreateAttendeeGroupCommand(Admin, "NHS", "NHS staff", [_med]),
            CancellationToken.None)).Value;
        _blocking.MemberCount = 3;
        var activer = new SetAttendeeGroupActiveHandler(_groups, _types, _profiles, _blocking, _unitOfWork, _audit);

        var result = await activer.HandleAsync(
            new SetAttendeeGroupActiveCommand(Admin, group.Id, false, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("in-use", result.Error.Code);
        Assert.Equal(3L, result.Error.Data!["members"]);
        Assert.True(_groups.Items.Single().IsActive);
    }
}
