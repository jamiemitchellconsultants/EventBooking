using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies deterministic Employee Group persistence and nullable Candidate rollout.</summary>
[Collection("postgres")]
public sealed class EmployeeGroupPersistenceTests(PostgresFixture fixture)
{
    /// <summary>The database contains exactly the approved five groups and ten mappings.</summary>
    [Fact]
    public async Task SeededGroupsMatchTheApprovedReferenceData()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var repository = new EmployeeGroupRepository(context);

        var groups = await repository.ListActiveAsync(CancellationToken.None);

        Assert.Equal(5, groups.Count);
        Assert.Equal(10, groups.Sum(group => group.Requirements.Count));
        var pilots = Assert.Single(groups, group => group.Code == "PILOTS");
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            pilots.RequiredAppointmentTypeIds);
        var lowerCaseLookup = await repository.GetByCodeAsync(" pilots ", CancellationToken.None);
        Assert.Equal(EmployeeGroupIds.Pilots, lowerCaseLookup!.Id);
    }

    /// <summary>Release 2 requires the Employee Group on every Candidate row.</summary>
    [Fact]
    public async Task CandidateAssociationIsRequiredAfterReleaseTwo()
    {
        await fixture.ResetAsync();
        await using var context = fixture.NewContext();
        var nullable = context.Model.FindEntityType("EventBooking.Domain.Candidates.Candidate")!
            .FindProperty("EmployeeGroupId")!.IsNullable;

        Assert.False(nullable);
        var relationship = context.Model.FindEntityType("EventBooking.Domain.Candidates.Candidate")!
            .GetForeignKeys()
            .Single(key => key.Properties.Single().Name == "EmployeeGroupId");
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }
}
