using EventBooking.Domain.Attendees;

namespace EventBooking.Domain.Tests.Attendees;

public sealed class RequiredAttendeeGroupTests
{
    [Fact]
    public void Creation_without_a_group_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Attendee.Create(Guid.NewGuid(), "Demo attendee", "demo@example.test", null!));
    }

    [Fact]
    public void Aggregate_does_not_expose_a_nullable_group_identifier()
    {
        Assert.Equal(typeof(Guid), typeof(Attendee).GetProperty(nameof(Attendee.AttendeeGroupId))!.PropertyType);
    }
}
