using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.AppointmentTypes;

public class AppointmentTypeTests
{
    [Fact]
    public void ThereAreExactlyThreeAppointmentTypes()
    {
        Assert.Equal(3, AppointmentTypeIds.All.Count);
        Assert.Equal(
            new[]
            {
                AppointmentTypeIds.DrugAndAlcoholTesting,
                AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting,
            },
            AppointmentTypeIds.All);
    }

    [Fact]
    public void TheIdentifiersAreStableConstants()
    {
        Assert.Equal(Guid.Parse("a0000001-0000-0000-0000-000000000001"), AppointmentTypeIds.DrugAndAlcoholTesting);
        Assert.Equal(Guid.Parse("a0000002-0000-0000-0000-000000000002"), AppointmentTypeIds.MedicalCheckUp);
        Assert.Equal(Guid.Parse("a0000003-0000-0000-0000-000000000003"), AppointmentTypeIds.UniformFitting);
    }

    [Theory]
    [InlineData("DAT")]
    [InlineData("dat")]
    [InlineData(" Dat ")]
    public void CodeLookupIsCaseAndWhitespaceInsensitive(string code)
    {
        Assert.True(AppointmentTypeIds.TryFromCode(code, out var id));
        Assert.Equal(AppointmentTypeIds.DrugAndAlcoholTesting, id);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownCodesAreRejected(string? code)
    {
        Assert.False(AppointmentTypeIds.TryFromCode(code, out var id));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void NamesMatchTheSpecWording()
    {
        Assert.Equal("Drug & Alcohol Testing", AppointmentTypeIds.NameOf(AppointmentTypeIds.DrugAndAlcoholTesting));
        Assert.Equal("Medical Check-up", AppointmentTypeIds.NameOf(AppointmentTypeIds.MedicalCheckUp));
        Assert.Equal("Uniform Fitting", AppointmentTypeIds.NameOf(AppointmentTypeIds.UniformFitting));
    }

    [Fact]
    public void NameOfRejectsAnUnknownIdentifier()
    {
        Assert.Throws<DomainException>(() => AppointmentTypeIds.NameOf(Guid.NewGuid()));
    }

    [Fact]
    public void TheFixedSetCarriesIdentityCodeAndName()
    {
        var types = AppointmentType.CreateFixedSet();

        Assert.Equal(3, types.Count);
        Assert.Equal(
            new[] { "DAT", "MED", "UNI" },
            types.Select(t => t.Code));
        Assert.Equal(
            new[] { "Drug & Alcohol Testing", "Medical Check-up", "Uniform Fitting" },
            types.Select(t => t.Name));
        Assert.Equal(AppointmentTypeIds.All, types.Select(t => t.Id).ToList());
    }
}
