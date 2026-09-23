using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Tests.Invites;

public sealed class TopUpInviteOptionsHandlerTests
{
    [Fact]
    public async Task Top_up_replaces_filled_option_and_audits_replacement()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(4);
        var trigger = new InviteAttendeeHandler(
            fixture.Attendees, fixture.Locations, fixture.Profiles, fixture.UnitOfWork,
            fixture.Clock,
            new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails, fixture.Audit, fixture.Clock, fixture.Eligibility));
        var issued = await trigger.HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);
        var inviteId = issued.Value.InviteId!.Value;
        var filled = fixture.Eligibility.EligibleInOrder[0];
        var fresh = fixture.Eligibility.EligibleInOrder[3];
        var handler = new TopUpInviteOptionsHandler(
            fixture.Invites, fixture.Attendees, fixture.Eligibility, fixture.Profiles,
            fixture.UnitOfWork, fixture.Audit, fixture.Clock);

        var result = await handler.HandleAsync(
            new TopUpInviteOptionsCommand(fixture.Coordinator, inviteId, filled, ExcludeEventId: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        var options = fixture.Invites.Items.Single(i => i.Id == inviteId).Options.Select(o => o.EventId).ToList();
        Assert.DoesNotContain(filled, options);
        Assert.Contains(fresh, options);
        Assert.Contains(fixture.Audit.Entries, e => e.Action == AuditAction.InviteOptionReplaced);
    }

    [Fact]
    public async Task Top_up_with_nothing_eligible_sets_no_response_follow_up()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);
        var trigger = new InviteAttendeeHandler(
            fixture.Attendees, fixture.Locations, fixture.Profiles, fixture.UnitOfWork,
            fixture.Clock,
            new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails, fixture.Audit, fixture.Clock, fixture.Eligibility));
        var issued = await trigger.HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);
        fixture.WithEligibleEvents(0);
        var handler = new TopUpInviteOptionsHandler(
            fixture.Invites, fixture.Attendees, fixture.Eligibility, fixture.Profiles,
            fixture.UnitOfWork, fixture.Audit, fixture.Clock);

        var result = await handler.HandleAsync(
            new TopUpInviteOptionsCommand(
                fixture.Coordinator, issued.Value.InviteId!.Value,
                fixture.Eligibility.EligibleInOrder.FirstOrDefault(), ExcludeEventId: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal("NoResponseNeedsFollowUp", fixture.Attendees.Items.Single().Status.ToString());
    }
}
