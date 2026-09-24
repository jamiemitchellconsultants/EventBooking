using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.ReferenceData;
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Invites;

namespace EventBooking.Infrastructure.Tests.Groups;

[Collection("postgres")]
public sealed class ReplaceAttendeeGroupRequirementsTests(PostgresFixture fixture)
    : PostgresBlockingHarness(fixture)
{
    [Fact]
    public async Task Replacement_rederives_both_members_and_supersedes_in_one_save()
    {
        var admin = Guid.NewGuid();
        Profiles.Add(StaffAccessProfile.Create(admin, Role.Admin, null));
        var group = await SeedGroupWithTwoMembersAsync(blockActiveBooking: false);
        var added = await AddTypeAsync("IND", "Induction");
        var handler = new UpdateAttendeeGroupHandler(
            GroupRepository, TypeRepository, AttendeeRepository, InviteRepository,
            Profiles, Queries, UnitOfWork, Audit, Clock);

        var result = await handler.HandleAsync(new UpdateAttendeeGroupCommand(
            admin, group.Id, null, [MedId, added.Id], true, 1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var members = await MembersOfAsync(group.Id);
        Assert.All(members, m => Assert.Equal(
            new[] { MedId, added.Id }.Order().ToList(),
            m.RequiredAppointmentTypeIds.Order().ToList()));
        Assert.All(members, m => Assert.Equal(
            AttendeeStatus.NotYetInvited, m.Status));
        Assert.Equal(InviteStatus.Superseded,
            (await InviteForAsync(members[0].Id)).Status);
        Assert.Equal(1, SaveCount);
    }

    [Fact]
    public async Task Replacement_with_active_booking_is_refused_and_changes_nothing()
    {
        var admin = Guid.NewGuid();
        Profiles.Add(StaffAccessProfile.Create(admin, Role.Admin, null));
        var group = await SeedGroupWithTwoMembersAsync(blockActiveBooking: true);
        var added = await AddTypeAsync("IND", "Induction");
        var before = await GroupVersionAsync(group.Id);
        var handler = new UpdateAttendeeGroupHandler(
            GroupRepository, TypeRepository, AttendeeRepository, InviteRepository,
            Profiles, Queries, UnitOfWork, Audit, Clock);

        var result = await handler.HandleAsync(new UpdateAttendeeGroupCommand(
            admin, group.Id, null, [MedId, added.Id], true, 1), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("requirements-locked", result.Error.Code);
        Assert.Equal(before, await GroupVersionAsync(group.Id));
    }
}
