using EventBooking.Application.Invites;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Invites;

public sealed class InviteIssuerTests
{
    [Fact]
    public async Task Issuer_snapshots_settings_and_supersedes_pending()
    {
        var fixture = InviteFixture.Create(optionCount: 2).WithEligibleEvents(2);
        var issuer = new InviteIssuer(fixture.Invites, fixture.Settings, fixture.Emails, fixture.Audit, fixture.Clock, fixture.Eligibility);
        var attendee = fixture.Attendees.Items.Single();

        var first = await issuer.IssueInitialAsync(
            attendee, fixture.LocationIds, EmailTemplate.AttendeeInvite, ActorType.Staff,
            fixture.Coordinator.ToString(), CancellationToken.None);
        var second = await issuer.IssueInitialAsync(
            attendee, [fixture.LocationIds[0]], EmailTemplate.AttendeeInvite, ActorType.Staff,
            fixture.Coordinator.ToString(), CancellationToken.None);

        Assert.True(first.IsSuccess, $"first failed: {first.Error?.Code} {first.Error?.Message}");
        Assert.True(second.IsSuccess, $"second failed: {second.Error?.Code} {second.Error?.Message}");
        var firstInvite = fixture.Invites.Items.Single(i => i.Id == first.Value.InviteId);
        Assert.Equal(Domain.Invites.InviteStatus.Superseded, firstInvite.Status);
        var secondInvite = fixture.Invites.Items.Single(i => i.Id == second.Value.InviteId);
        Assert.Equal(2, secondInvite.InviteOptionCount);
        Assert.Equal(7, secondInvite.InviteExpiryDays);
        Assert.Equal(2, secondInvite.MaxAutoRetryCount);
        Assert.Equal(InviteFixture.Now.AddDays(7), secondInvite.ExpiresAt);
    }
}
