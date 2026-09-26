using EventBooking.Application.SelfRegistrations;
using EventBooking.Domain.SelfRegistrations;

namespace EventBooking.Application.Tests.SelfRegistrations;

public sealed class SelfRegistrationMaintenanceTests
{
    [Fact]
    public void RetentionBoundaryIsThirtyDaysAfterTerminalState()
    {
        var terminal = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var request = PendingRegistration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Amara Novak", "amara@example.test", terminal.AddDays(-3), 48);
        request.Expire(terminal);

        Assert.False(SelfRegistrationMaintenance.IsDueForDeletion(
            request, terminal.AddDays(30).AddTicks(-1)));
        Assert.True(SelfRegistrationMaintenance.IsDueForDeletion(
            request, terminal.AddDays(30)));
        var pending = PendingRegistration.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Ivo Chen", "ivo@example.test", terminal, 48);
        Assert.False(SelfRegistrationMaintenance.IsDueForDeletion(
            pending, terminal.AddDays(40)));
    }
}
