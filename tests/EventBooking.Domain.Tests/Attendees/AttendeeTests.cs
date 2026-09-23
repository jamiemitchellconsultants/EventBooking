using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Tests.Attendees;

public class AttendeeTests
{
    private static readonly Guid[] TwoTypes =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    /// <summary>Builds the Pilots group used across these fixtures for DAT+UNI.</summary>
    private static AttendeeGroup Pilots() =>
        AttendeeGroup.Define(AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true, TwoTypes);

    private static Attendee NewAttendee() =>
        Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots());

    [Fact]
    public void ANewAttendeeStartsNotYetInvited()
    {
        var attendee = NewAttendee();

        Assert.Equal(AttendeeStatus.NotYetInvited, attendee.Status);
        Assert.Equal("Amara Novak", attendee.Name);
        Assert.Equal("a.novak@mail.com", attendee.Email);
    }

    [Fact]
    public void NameAndEmailAreTrimmedAndTheEmailIsLowerCased()
    {
        var attendee = Attendee.Create(
            Guid.NewGuid(), "  Amara Novak  ", "  A.Novak@Mail.COM ", Pilots());

        Assert.Equal("Amara Novak", attendee.Name);
        Assert.Equal("a.novak@mail.com", attendee.Email);
    }

    [Fact]
    public void RequirementsAreRecordedAgainstTheAttendee()
    {
        var attendee = NewAttendee();

        Assert.Equal(2, attendee.Requirements.Count);
        Assert.All(attendee.Requirements, r => Assert.Equal(attendee.Id, r.AttendeeId));
        Assert.Equal(
            TwoTypes.OrderBy(id => id),
            attendee.RequiredAppointmentTypeIds.OrderBy(id => id));
    }

    [Fact]
    public void AMissingNameIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => Attendee.Create(Guid.NewGuid(), "  ", "a.novak@mail.com", Pilots()));
        Assert.Equal("name must not be blank.", ex.Message);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("no@domain")]
    [InlineData("two@@at.com")]
    [InlineData("spaces in@mail.com")]
    [InlineData("")]
    [InlineData(null)]
    public void AnInvalidEmailIsRejected(string? email)
    {
        var ex = Assert.Throws<DomainException>(
            () => Attendee.Create(Guid.NewGuid(), "Amara Novak", email, Pilots()));
        Assert.Equal("email is not a valid email address.", ex.Message);
    }

    [Fact]
    public void AGroupWithNoMappedTypesIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => AttendeeGroup.Define(Guid.NewGuid(), "EMPTY", "Empty", true, []));
        Assert.Equal("An attendee group must map at least one appointment type.", ex.Message);
    }

    [Fact]
    public void DuplicateMappedTypesAreRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => AttendeeGroup.Define(
                Guid.NewGuid(),
                "DUP",
                "Dup",
                true,
                [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Equal("An attendee group cannot map the same appointment type twice.", ex.Message);
    }

    [Fact]
    public void AnUnknownMappedTypeIsRejected()
    {
        Assert.Throws<DomainException>(
            () => AttendeeGroup.Define(Guid.NewGuid(), "UNKNOWN", "Unknown", true, [Guid.NewGuid()]));
    }

    [Fact]
    public void AllThreeAppointmentTypesAreAllowed()
    {
        var cabinCrew = AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true, AppointmentTypeIds.All);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", cabinCrew);

        Assert.Equal(3, attendee.Requirements.Count);
    }

    [Fact]
    public void UpdatingDetailsRevalidates()
    {
        var attendee = NewAttendee();

        attendee.UpdateDetails("Amara N. Novak", "amara@mail.com");
        Assert.Equal("Amara N. Novak", attendee.Name);
        Assert.Equal("amara@mail.com", attendee.Email);

        Assert.Throws<DomainException>(() => attendee.UpdateDetails("Amara", "broken"));
        Assert.Equal("amara@mail.com", attendee.Email);
    }

    [Fact]
    public void AssigningADifferentGroupReplacesThePreviousSet()
    {
        var attendee = NewAttendee();
        var groundOps = AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]);

        Assert.True(attendee.AssignAttendeeGroup(groundOps));
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], attendee.RequiredAppointmentTypeIds);
    }

    [Fact]
    public void AssigningAnEquivalentGroupPreservesThePreviousSet()
    {
        var attendee = NewAttendee();
        var equivalent = AttendeeGroup.Define(
            Guid.NewGuid(), "PILOTS_EQUIVALENT", "Pilots equivalent", true, TwoTypes);

        Assert.False(attendee.AssignAttendeeGroup(equivalent));
        Assert.Equal(2, attendee.Requirements.Count);
    }
}
