using EventBooking.Application.Common;
using EventBooking.Application.Negotiation;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Negotiation;

public sealed class ProposeEventHandlerTests
{
    [Fact]
    public async Task Propose_with_every_failure_at_once_reports_all_of_them()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        fixture.Types.Items.Single(t => t.Id == fixture.TypeIds["MED"])
            .Deactivate(AppointmentTypeUsage.None);
        var proposer = fixture.Managers["MED"];
        var handler = new ProposeEventHandler(
            fixture.Proposals, fixture.Locations, fixture.Types, fixture.Profiles,
            fixture.Profiles, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
            ProposalFixture.Zones, fixture.Events);
        var unknownType = Guid.NewGuid();

        var result = await handler.HandleAsync(new ProposeEventCommand(
            proposer, fixture.LocationId, new DateOnly(2026, 9, 1), new TimeOnly(9, 0), 90,
            [fixture.TypeIds["MED"], unknownType], 0), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Contains("MED", result.Error.Message);
        Assert.Contains(unknownType.ToString(), result.Error.Message);
        Assert.Empty(fixture.Proposals.Items);
        Assert.Empty(fixture.Audit.Entries);
    }

    [Fact]
    public async Task Propose_at_inactive_location_is_refused()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        fixture.Locations.Items.Single().Deactivate(Domain.Locations.LocationUsage.None);
        var proposer = fixture.Managers["MED"];
        var handler = new ProposeEventHandler(
            fixture.Proposals, fixture.Locations, fixture.Types, fixture.Profiles,
            fixture.Profiles, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
            ProposalFixture.Zones, fixture.Events);

        var result = await handler.HandleAsync(new ProposeEventCommand(
            proposer, fixture.LocationId, new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90,
            [fixture.TypeIds["MED"]], 6), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task Appointment_staff_without_manager_cannot_propose()
    {
        var fixture = NegotiationFixture.Create().WithTypes("MED");
        var staff = Guid.NewGuid();
        fixture.Profiles.Add(StaffAccessProfile.Create(staff, Role.AppointmentStaff, null));
        var handler = new ProposeEventHandler(
            fixture.Proposals, fixture.Locations, fixture.Types, fixture.Profiles,
            fixture.Profiles, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
            ProposalFixture.Zones, fixture.Events);

        var result = await handler.HandleAsync(new ProposeEventCommand(
            staff, fixture.LocationId, new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90,
            [fixture.TypeIds["MED"]], 6), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(fixture.Proposals.Items);
    }
}
