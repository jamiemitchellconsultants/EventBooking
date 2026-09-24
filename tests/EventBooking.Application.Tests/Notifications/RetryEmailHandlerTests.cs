using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Notifications;

public sealed class RetryEmailHandlerTests
{
    [Fact]
    public async Task Retry_creates_pending_and_resolves_old_with_same_link_inputs()
    {
        var deliveries = new InMemoryEmailDeliveryRepository();
        var profiles = new InMemoryStaffAccessProfileRepository();
        var unitOfWork = new FakeUnitOfWork();
        var coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
        profiles.Items.Add(StaffAccessProfile.Create(coordinator, Role.Coordinator, null));
        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();
        var old = EmailLog.RecordPending(Guid.NewGuid(), attendeeId,
            EmailTemplate.AttendeeInvite, DateTimeOffset.UtcNow, inviteId: inviteId);
        old.MarkFailed(DateTimeOffset.UtcNow);
        deliveries.Items.Add(old);
        var handler = new RetryEmailHandler(
            deliveries, profiles, unitOfWork, new FakeClock());

        var result = await handler.HandleAsync(
            new RetryEmailCommand(coordinator, attendeeId, old.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailStatus.Resolved, old.Status);
        var fresh = Assert.Single(deliveries.Items, d => d.Id != old.Id);
        Assert.Equal(EmailStatus.Pending, fresh.Status);
        Assert.Equal(EmailTemplate.AttendeeInvite, fresh.TemplateName);
        Assert.Equal(inviteId, fresh.InviteId);
    }

    [Fact]
    public async Task Retry_by_non_coordinator_is_forbidden()
    {
        var deliveries = new InMemoryEmailDeliveryRepository();
        var profiles = new InMemoryStaffAccessProfileRepository();
        var handler = new RetryEmailHandler(
            deliveries, profiles, new FakeUnitOfWork(), new FakeClock());

        var result = await handler.HandleAsync(
            new RetryEmailCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Retry_resolves_the_newest_failed_delivery_without_being_given_its_identifier()
    {
        var deliveries = new InMemoryEmailDeliveryRepository();
        var profiles = new InMemoryStaffAccessProfileRepository();
        var coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
        profiles.Items.Add(StaffAccessProfile.Create(coordinator, Role.Coordinator, null));
        var attendeeId = Guid.NewGuid();
        var old = EmailLog.RecordPending(Guid.NewGuid(), attendeeId,
            EmailTemplate.AttendeeInvite, DateTimeOffset.UtcNow, inviteId: Guid.NewGuid());
        old.MarkFailed(DateTimeOffset.UtcNow);
        deliveries.Items.Add(old);
        var handler = new RetryEmailHandler(
            deliveries, profiles, new FakeUnitOfWork(), new FakeClock());

        var result = await handler.HandleAsync(
            new RetryNewestEmailCommand(coordinator, attendeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailStatus.Resolved, old.Status);
        var fresh = Assert.Single(deliveries.Items, d => d.Id != old.Id);
        Assert.Equal(EmailStatus.Pending, fresh.Status);
    }

    /// <summary>
    /// An attendee whose newest delivery did not fail has nothing to retry. Without this case
    /// the route would resend the last successful email, which is the failure the status guard
    /// existed to prevent.
    /// </summary>
    [Fact]
    public async Task An_attendee_whose_newest_delivery_did_not_fail_is_refused()
    {
        var deliveries = new InMemoryEmailDeliveryRepository();
        var profiles = new InMemoryStaffAccessProfileRepository();
        var coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
        profiles.Items.Add(StaffAccessProfile.Create(coordinator, Role.Coordinator, null));
        var attendeeId = Guid.NewGuid();
        var sent = EmailLog.RecordPending(Guid.NewGuid(), attendeeId,
            EmailTemplate.AttendeeInvite, DateTimeOffset.UtcNow, inviteId: Guid.NewGuid());
        sent.MarkSent(DateTimeOffset.UtcNow);
        deliveries.Items.Add(sent);
        var handler = new RetryEmailHandler(
            deliveries, profiles, new FakeUnitOfWork(), new FakeClock());

        var result = await handler.HandleAsync(
            new RetryNewestEmailCommand(coordinator, attendeeId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
    }
}
