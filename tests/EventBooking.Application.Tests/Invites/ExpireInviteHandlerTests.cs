using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Invites;

public sealed class ExpireInviteHandlerTests
{
    private static ExpireInviteHandler Handler(InviteFixture f) => new(
        f.Invites, f.Attendees, f.Eligibility,
        f.UnitOfWork, f.Audit, f.Clock,
        new InviteIssuer(f.Invites, f.Settings, f.Emails, f.Audit, f.Clock, f.Eligibility));

    private static async Task<Guid> IssuedInviteAsync(InviteFixture fixture)
    {
        var trigger = new InviteAttendeeHandler(
            fixture.Attendees, fixture.Locations, fixture.Profiles, fixture.UnitOfWork,
            fixture.Clock,
            new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails, fixture.Audit, fixture.Clock, fixture.Eligibility));
        fixture.WithEligibleEvents(3);
        var result = await trigger.HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);
        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        return result.Value.InviteId!.Value;
    }

    [Fact]
    public async Task Unexpired_invite_is_left_alone()
    {
        var fixture = InviteFixture.Create(optionCount: 3);
        var inviteId = await IssuedInviteAsync(fixture);
        var before = fixture.Audit.Entries.Count;

        var result = await Handler(fixture).HandleAsync(new ExpireInviteCommand(inviteId), CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal(InviteStatus.Pending, fixture.Invites.Items.Single(i => i.Id == inviteId).Status);
        Assert.Single(fixture.Invites.Items);
        Assert.Equal(before, fixture.Audit.Entries.Count);
    }

    [Fact]
    public async Task Expiry_below_limit_reissues_with_same_locations_and_retry_plus_one()
    {
        var fixture = InviteFixture.Create(optionCount: 3);
        var inviteId = await IssuedInviteAsync(fixture);
        fixture.WithEligibleEvents(3);
        fixture.Clock.UtcNow = InviteFixture.Now.AddDays(8);

        var result = await Handler(fixture).HandleAsync(new ExpireInviteCommand(inviteId), CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal(InviteStatus.Expired, fixture.Invites.Items.Single(i => i.Id == inviteId).Status);
        var reissued = fixture.Invites.Items.Single(i => i.Id != inviteId);
        Assert.Equal(fixture.LocationIds.Order().ToList(), reissued.LocationIds.Order().ToList());
        Assert.Equal(1, reissued.RetryCount);
        var delivery = Assert.Single(fixture.Emails.Items, e => e.InviteId == reissued.Id);
        Assert.Equal(EmailTemplate.AttendeeReinvite, delivery.TemplateName);
        Assert.Contains(fixture.Audit.Entries, e => e.Action == AuditAction.InviteExpired);
    }

    [Fact]
    public async Task Expiry_at_limit_yields_no_response_follow_up()
    {
        var fixture = InviteFixture.Create(optionCount: 3);
        var inviteId = await IssuedInviteAsync(fixture);
        var invite = fixture.Invites.Items.Single(i => i.Id == inviteId);
        invite.MarkExpired();
        var second = Invite.Reissue(Guid.NewGuid(), invite, InviteFixture.Now.AddDays(7),
            fixture.Eligibility.EligibleInOrder.Take(3));
        fixture.Invites.Items.Add(second);
        fixture.Invites.Items.Remove(invite);
        second.MarkExpired();
        var third = Invite.Reissue(Guid.NewGuid(), second, InviteFixture.Now.AddDays(7),
            fixture.Eligibility.EligibleInOrder.Take(3));
        fixture.Invites.Items.Add(third);
        fixture.Invites.Items.Remove(second);
        fixture.Clock.UtcNow = InviteFixture.Now.AddDays(30);

        var result = await Handler(fixture).HandleAsync(new ExpireInviteCommand(third.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal("NoResponseNeedsFollowUp", fixture.Attendees.Items.Single().Status.ToString());
    }

    [Fact]
    public async Task Failed_reissue_yields_no_response_with_reason_in_audit()
    {
        var fixture = InviteFixture.Create(optionCount: 3);
        var inviteId = await IssuedInviteAsync(fixture);
        fixture.WithEligibleEvents(1);
        fixture.Clock.UtcNow = InviteFixture.Now.AddDays(8);

        var result = await Handler(fixture).HandleAsync(new ExpireInviteCommand(inviteId), CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal("NoResponseNeedsFollowUp", fixture.Attendees.Items.Single().Status.ToString());
        var entry = Assert.Single(fixture.Audit.Entries, e => e.Action == AuditAction.InviteExpired);
        Assert.Contains("insufficient", entry.Details);
    }

    [Fact]
    public async Task Expired_recovery_invite_leaves_the_attendee_booked()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);
        var attendee = fixture.Attendees.Items.Single();
        attendee.MarkInvited(InviteFixture.Now);
        attendee.MarkBooked(InviteFixture.Now);
        var recovery = Invite.CreateRecovery(
            Guid.NewGuid(), attendee.Id, Guid.NewGuid(), InviteFixture.Now.AddDays(-1),
            fixture.LocationIds[0], null, fixture.Eligibility.EligibleInOrder.Take(3).ToList(),
            [fixture.MedId]);
        fixture.Invites.Items.Add(recovery);

        var result = await Handler(fixture).HandleAsync(new ExpireInviteCommand(recovery.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal(InviteStatus.Expired, fixture.Invites.Items.Single().Status);
        Assert.Equal(AttendeeStatus.Booked, attendee.Status);
        Assert.Single(fixture.Invites.Items);
        Assert.Contains(fixture.Audit.Entries, e => e.Action == AuditAction.InviteExpired);
    }
}
