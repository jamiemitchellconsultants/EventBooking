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
}
