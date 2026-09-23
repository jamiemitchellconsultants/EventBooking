using EventBooking.Application.ReferenceData;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence.Queries;

namespace EventBooking.Infrastructure.Tests.Queries;

[Collection("postgres")]
public sealed class ReferenceDataBlockingQueryTests(PostgresFixture fixture)
    : PostgresBlockingHarness(fixture)
{
    [Fact]
    public async Task Location_usage_counts_open_proposals_and_future_events()
    {
        var location = await SeedLocationAsync("LONDON_HQ", "Europe/London");
        await SeedOpenProposalAsync(location.Id);
        await SeedOpenProposalAsync(location.Id);
        await SeedFutureEventAsync(location.Id);
        var queries = new ReferenceDataBlockingQueries(Context);

        var usage = await queries.LocationUsageAsync(location.Id, CancellationToken.None);

        Assert.Equal(new LocationUsage(2, 1), usage);
    }

    [Fact]
    public async Task Type_usage_counts_proposals_events_and_mapped_groups()
    {
        var queries = new ReferenceDataBlockingQueries(Context);
        var before = await queries.AppointmentTypeUsageAsync(
            AppointmentTypeIds.MedicalCheckUp, CancellationToken.None);

        var (typeId, _) = await SeedTypeWithProposalEventAndGroupAsync();

        var usage = await queries.AppointmentTypeUsageAsync(typeId, CancellationToken.None);

        // Proposals and events are truncated per test, so their counts are absolute; groups
        // persist in the shared fixture, so that count is a delta over earlier suites.
        Assert.Equal(1, usage.OpenProposals);
        Assert.Equal(1, usage.FutureEvents);
        Assert.Equal(before.ActiveGroups + 1, usage.ActiveGroups);
    }

    [Fact]
    public async Task Group_counts_distinguish_members_from_blocking_members()
    {
        var group = await SeedGroupWithTwoMembersAsync(blockActiveBooking: true);

        var queries = new ReferenceDataBlockingQueries(Context);

        Assert.Equal(2, await queries.AttendeeGroupMemberCountAsync(group.Id, CancellationToken.None));
        Assert.Equal(1, await queries.AttendeeGroupBlockingMemberCountAsync(group.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Empty_references_count_zero()
    {
        var location = await SeedLocationAsync("EMPTY", "Europe/London");
        var queries = new ReferenceDataBlockingQueries(Context);

        Assert.Equal(LocationUsage.None, await queries.LocationUsageAsync(location.Id, CancellationToken.None));
    }
}
