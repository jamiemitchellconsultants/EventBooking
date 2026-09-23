using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies deterministic Attendee Group persistence and nullable Attendee rollout.</summary>
[Collection("postgres")]
public sealed class AttendeeGroupPersistenceTests(PostgresFixture fixture)
{
    /// <summary>The database contains exactly the approved five groups and ten mappings.</summary>
    [Fact]
    public async Task SeededGroupsMatchTheApprovedReferenceData()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var repository = new AttendeeGroupRepository(context);

        var groups = await repository.ListActiveAsync(CancellationToken.None);

        Assert.Equal(5, groups.Count);
        Assert.Equal(10, groups.Sum(group => group.Requirements.Count));
        var pilots = Assert.Single(groups, group => group.Code == "PILOTS");
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            pilots.RequiredAppointmentTypeIds);
        var lowerCaseLookup = await repository.GetByCodeAsync(" pilots ", CancellationToken.None);
        Assert.Equal(AttendeeGroupIds.Pilots, lowerCaseLookup!.Id);
    }

    /// <summary>Release 2 requires the Attendee Group on every Attendee row.</summary>
    [Fact]
    public async Task AttendeeAssociationIsRequiredAfterReleaseTwo()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var nullable = context.Model.FindEntityType("EventBooking.Domain.Attendees.Attendee")!
            .FindProperty("AttendeeGroupId")!.IsNullable;

        Assert.False(nullable);
        var relationship = context.Model.FindEntityType("EventBooking.Domain.Attendees.Attendee")!
            .GetForeignKeys()
            .Single(key => key.Properties.Single().Name == "AttendeeGroupId");
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }
}
