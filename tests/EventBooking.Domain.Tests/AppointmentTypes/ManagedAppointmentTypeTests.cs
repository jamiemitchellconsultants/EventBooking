using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.AppointmentTypes;

/// <summary>
/// Task 5: appointment types become Admin-managed reference data rather than a fixed set
/// (FR-1.3, FR-1.6, FR-1.7; design 01 — Reference data).
/// </summary>
public class ManagedAppointmentTypeTests
{
    private static AppointmentType Create(string code = "MED", string name = "Medical check") =>
        AppointmentType.Create(Guid.NewGuid(), code, name);

    [Fact]
    public void ACreatedTypeIsActiveAtVersionOneWithACanonicalCode()
    {
        var type = Create("med");

        Assert.Equal("MED", type.Code);
        Assert.Equal("Medical check", type.Name);
        Assert.True(type.IsActive);
        Assert.Equal(1, type.Version);
    }

    [Theory]
    [InlineData("1MED")]
    [InlineData("MED-1")]
    [InlineData(" ")]
    public void AnUncanonicalCodeIsRefused(string code)
    {
        Assert.Throws<DomainException>(() => Create(code));
    }

    [Fact]
    public void RenamingBumpsTheVersionAndBoundsTheName()
    {
        var type = Create();

        type.Rename("Medical screening");

        Assert.Equal("Medical screening", type.Name);
        Assert.Equal(2, type.Version);
        Assert.Throws<DomainException>(() => type.Rename(new string('x', 101)));
    }

    [Fact]
    public void DeactivationNamesEveryBlockingUse()
    {
        var type = Create();

        var refusal = Assert.Throws<ReferenceDataInUseException>(
            () => type.Deactivate(new AppointmentTypeUsage(2, 1, 3)));

        Assert.True(type.IsActive);
        Assert.Equal(2, refusal.Blocking["openProposals"]);
        Assert.Equal(1, refusal.Blocking["futureEvents"]);
        Assert.Equal(3, refusal.Blocking["activeGroups"]);
    }

    [Fact]
    public void AnUnusedTypeDeactivatesAndReactivates()
    {
        var type = Create();

        type.Deactivate(AppointmentTypeUsage.None);
        Assert.False(type.IsActive);

        type.Reactivate();
        Assert.True(type.IsActive);
        Assert.Equal(3, type.Version);
    }
}
