using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.AttendeeGroups;

public sealed class AttendeeGroupDescriptionTests
{
    private static AttendeeGroup New(string? description) => AttendeeGroup.Create(
        Guid.NewGuid(), "VISITORS", "Visitors",
        [AppointmentTypeIds.MedicalCheckUp],
        [AppointmentTypeIds.MedicalCheckUp], description);

    [Fact]
    public void DescriptionIsTrimmedAndCanBeEmpty()
    {
        var group = New("  Who should choose this group.  ");
        Assert.Equal("Who should choose this group.", group.Description);
        var version = group.Version;
        group.ChangeDescription(" ");
        Assert.Equal("", group.Description);
        Assert.Equal(version + 1, group.Version);
        group.ChangeDescription(null);
        Assert.Equal(version + 1, group.Version);
    }

    [Fact]
    public void DescriptionLongerThanFiveHundredCharactersIsRejected()
    {
        Assert.Throws<DomainException>(() => New(new string('x', 501)));
    }
}
