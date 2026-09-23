using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.AttendeeGroups;

/// <summary>
/// Task 5: an attendee group's mapping set is Admin-managed, and a requirement change is refused
/// while any member holds an active original booking (FR-1.4 to FR-1.6; design 01 — Requirements).
/// </summary>
public class ManagedAttendeeGroupTests
{
    private static readonly Guid Medical = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid Fitting = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    private static readonly Guid Induction = Guid.Parse("a0000003-0000-0000-0000-000000000003");

    private static readonly IReadOnlyCollection<Guid> ActiveTypes = [Medical, Fitting, Induction];

    private static AttendeeGroup Group(params Guid[] typeIds) =>
        AttendeeGroup.Create(Guid.NewGuid(), "field_staff", "Field staff", typeIds, ActiveTypes);

    [Fact]
    public void ACreatedGroupIsActiveAtVersionOneWithACanonicalCode()
    {
        var group = Group(Medical, Induction);

        Assert.Equal("FIELD_STAFF", group.Code);
        Assert.True(group.IsActive);
        Assert.Equal(1, group.Version);
        Assert.Equal([Medical, Induction], group.RequiredAppointmentTypeIds.Order());
    }

    [Fact]
    public void AMappingMustNameAtLeastOneActiveTypeAndNoDuplicates()
    {
        Assert.Throws<DomainException>(() => Group());
        Assert.Throws<DomainException>(() => Group(Medical, Medical));
        Assert.Throws<DomainException>(() =>
            AttendeeGroup.Create(Guid.NewGuid(), "x_group", "X", [Guid.NewGuid()], ActiveTypes));
    }

    [Fact]
    public void ReplacingTheSetWithTheSameTypesInAnotherOrderChangesNothing()
    {
        var group = Group(Medical, Induction);

        var changed = group.ReplaceRequirements([Induction, Medical], ActiveTypes, blockingMembers: 4);

        Assert.False(changed);
        Assert.Equal(1, group.Version);
    }

    [Fact]
    public void ARealRequirementChangeIsRefusedWhileMembersHoldActiveBookings()
    {
        var group = Group(Medical);

        var refusal = Assert.Throws<ReferenceDataInUseException>(
            () => group.ReplaceRequirements([Medical, Fitting], ActiveTypes, blockingMembers: 2));

        Assert.Equal(2, refusal.Blocking["blockingMembers"]);
        Assert.Equal([Medical], group.RequiredAppointmentTypeIds);
        Assert.Equal(1, group.Version);
    }

    [Fact]
    public void ARealRequirementChangeWithNoBlockingMembersIsApplied()
    {
        var group = Group(Medical);

        var changed = group.ReplaceRequirements([Fitting, Induction], ActiveTypes, blockingMembers: 0);

        Assert.True(changed);
        Assert.Equal([Fitting, Induction], group.RequiredAppointmentTypeIds.Order());
        Assert.Equal(2, group.Version);
    }

    [Fact]
    public void DeactivationIsRefusedWhileTheGroupHasMembers()
    {
        var group = Group(Medical);

        var refusal = Assert.Throws<ReferenceDataInUseException>(() => group.Deactivate(memberCount: 7));
        Assert.Equal(7, refusal.Blocking["members"]);
        Assert.True(group.IsActive);

        group.Deactivate(memberCount: 0);
        Assert.False(group.IsActive);

        group.Reactivate(ActiveTypes);
        Assert.True(group.IsActive);
    }

    [Fact]
    public void ReactivationRejectsMappingsToTypesDeactivatedWhileTheGroupWasInactive()
    {
        var type = AppointmentType.Create(Guid.NewGuid(), "MEDICAL", "Medical");
        var group = AttendeeGroup.Create(Guid.NewGuid(), "GROUP", "Group", [type.Id], [type.Id]);
        group.Deactivate(memberCount: 0);
        type.Deactivate(AppointmentTypeUsage.None);

        Assert.Throws<DomainException>(() => group.Reactivate([]));
        Assert.False(group.IsActive);
        Assert.Equal(2, group.Version);
    }

    [Fact]
    public void RenamingBumpsTheVersion()
    {
        var group = Group(Medical);

        group.Rename("Field crew");

        Assert.Equal("Field crew", group.Name);
        Assert.Equal(2, group.Version);
    }
}
