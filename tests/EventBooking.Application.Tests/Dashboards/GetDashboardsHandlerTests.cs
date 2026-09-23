using EventBooking.Application.Abstractions;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Dashboards;

public class GetDashboardsHandlerTests
{
    private sealed class DashboardQueries : IDashboardQueries
    {
        public Task<IReadOnlyList<AwaitingAvailabilityRow>> AwaitingAvailabilityAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AwaitingAvailabilityRow>>([]);

        public Task<IReadOnlyList<NoResponseRow>> NoResponseAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NoResponseRow>>([]);

        public Task<IReadOnlyList<SlotOverviewRow>> SlotsOverviewAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SlotOverviewRow>>([]);

        public Task<IReadOnlyList<CandidateEmailStatusRow>> LatestEmailStatusAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CandidateEmailStatusRow>>([]);
    }

    [Fact]
    public async Task CoordinatorsCanReadTheDashboards()
    {
        var userId = Guid.NewGuid();
        var roles = new InMemoryStaffAccessProfileRepository();
        roles.Add(StaffAccessProfile.Create(userId, Role.Coordinator, null));
        var handler = new GetDashboardsHandler(new DashboardQueries(), roles);

        var result = await handler.HandleAsync(new GetDashboardsQuery(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.AwaitingAvailability);
        Assert.Empty(result.Value.NoResponse);
        Assert.Empty(result.Value.Slots);
    }

    [Fact]
    public async Task AdminsAreForbiddenTheDashboards()
    {
        var userId = Guid.NewGuid();
        var roles = new InMemoryStaffAccessProfileRepository();
        roles.Add(StaffAccessProfile.Create(userId, Role.Admin, null));
        var handler = new GetDashboardsHandler(new DashboardQueries(), roles);

        var result = await handler.HandleAsync(new GetDashboardsQuery(userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task AppointmentStaffAreForbiddenTheDashboards()
    {
        var userId = Guid.NewGuid();
        var roles = new InMemoryStaffAccessProfileRepository();
        ((IStaffAccessProfileRepository)roles).Add(StaffAccessProfile.Create(
            userId, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var handler = new GetDashboardsHandler(new DashboardQueries(), roles);

        var result = await handler.HandleAsync(new GetDashboardsQuery(userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ManagersAndUnassignedStaffAreForbidden(bool isManager)
    {
        var userId = Guid.NewGuid();
        var roles = new InMemoryStaffAccessProfileRepository();
        if (isManager)
        {
            roles.Add(StaffAccessProfile.Create(
                userId, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting));
        }

        var handler = new GetDashboardsHandler(new DashboardQueries(), roles);

        var result = await handler.HandleAsync(new GetDashboardsQuery(userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
