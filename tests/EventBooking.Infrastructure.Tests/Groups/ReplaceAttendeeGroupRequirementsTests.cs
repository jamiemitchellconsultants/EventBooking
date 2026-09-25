using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.ReferenceData;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Audit;
using EventBooking.Domain.EventGroups;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

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
        var handler = Handler();

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
        var handler = Handler();

        var result = await handler.HandleAsync(new UpdateAttendeeGroupCommand(
            admin, group.Id, null, [MedId, added.Id], true, 1), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("requirements-locked", result.Error.Code);
        Assert.Equal(before, await GroupVersionAsync(group.Id));
    }

    [Fact]
    public async Task Drift_breaking_a_private_membership_is_refused()
    {
        var admin = Guid.NewGuid();
        Profiles.Add(StaffAccessProfile.Create(admin, Role.Admin, null));
        var cabinCrew = await SeedGroupWithFutureEventAsync(openGroup: false, openEvent: false);
        var before = cabinCrew.Version;

        var result = await Handler().HandleAsync(new UpdateAttendeeGroupCommand(
            admin, AttendeeGroupIds.CabinCrew, null, [MedId], true, before),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(before, await GroupVersionAsync(AttendeeGroupIds.CabinCrew));
    }

    [Fact]
    public async Task Drift_breaking_an_open_membership_is_refused()
    {
        var admin = Guid.NewGuid();
        Profiles.Add(StaffAccessProfile.Create(admin, Role.Admin, null));
        var cabinCrew = await SeedGroupWithFutureEventAsync(openGroup: true, openEvent: true);
        var before = cabinCrew.Version;

        var result = await Handler().HandleAsync(new UpdateAttendeeGroupCommand(
            admin, AttendeeGroupIds.CabinCrew, null, [MedId], true, before),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Equal(before, await GroupVersionAsync(AttendeeGroupIds.CabinCrew));
    }

    [Fact]
    public async Task Set_equivalent_replacement_is_accepted()
    {
        var admin = Guid.NewGuid();
        Profiles.Add(StaffAccessProfile.Create(admin, Role.Admin, null));
        var cabinCrew = await SeedGroupWithFutureEventAsync(openGroup: true, openEvent: true);
        var reordered = cabinCrew.RequiredAppointmentTypeIds.OrderDescending().ToArray();

        var result = await Handler().HandleAsync(new UpdateAttendeeGroupCommand(
            admin, AttendeeGroupIds.CabinCrew, null, reordered, true, cabinCrew.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(cabinCrew.Version, await GroupVersionAsync(AttendeeGroupIds.CabinCrew));
    }

    [Fact]
    public async Task Deactivation_is_refused_while_listed_even_with_no_events()
    {
        var admin = Guid.NewGuid();
        Profiles.Add(StaffAccessProfile.Create(admin, Role.Admin, null));
        var requirements = new Dictionary<Guid, IReadOnlyCollection<Guid>>
        {
            [AttendeeGroupIds.Pilots] = [
                AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
        };
        var group = EventGroup.Create(Guid.NewGuid(), "Empty days", null, requirements);
        Context.EventGroups.Add(group);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var pilots = await Context.AttendeeGroups
            .SingleAsync(g => g.Id == AttendeeGroupIds.Pilots);

        var result = await Handler().HandleAsync(new UpdateAttendeeGroupCommand(
            admin, AttendeeGroupIds.Pilots, null, null, false, pilots.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("in-use", result.Error.Code);
    }

    private UpdateAttendeeGroupHandler Handler() => new(
        GroupRepository, TypeRepository, AttendeeRepository, InviteRepository,
        Profiles, Queries, UnitOfWork, Audit, Clock,
        new EventGroupRepository(Context), new EventRepository(Context));

    private async Task<AttendeeGroup> SeedGroupWithFutureEventAsync(bool openGroup, bool openEvent)
    {
        var location = await SeedLocationAsync("DRIFTSITE", "Europe/London");
        var eventItem = await SeedFutureEventAsync(location.Id);
        var cabinCrew = await Context.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == AttendeeGroupIds.CabinCrew);
        var requirements = new Dictionary<Guid, IReadOnlyCollection<Guid>>
        {
            [AttendeeGroupIds.CabinCrew] = cabinCrew.RequiredAppointmentTypeIds,
        };
        var group = EventGroup.Create(Guid.NewGuid(), "Drift days", null, requirements);
        group.AddEvent(eventItem.Id,
            eventItem.Capacities.Select(x => x.AppointmentTypeId).ToArray(), requirements, true);
        group.SetOpen(openGroup);
        group.SetEventOpen(eventItem.Id, openEvent);
        Context.EventGroups.Add(group);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return await Context.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == AttendeeGroupIds.CabinCrew);
    }
}
