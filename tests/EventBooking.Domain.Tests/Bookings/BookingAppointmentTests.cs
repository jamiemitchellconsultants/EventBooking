using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies the ontology-approved lifecycle of one booking appointment.</summary>
public sealed class BookingAppointmentTests
{
    private static readonly DateTimeOffset CheckInTime =
        new(2026, 9, 7, 8, 55, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OutcomeTime = CheckInTime.AddHours(2);

    /// <summary>Verifies that a new record represents an untouched expected appointment.</summary>
    [Fact]
    public void NewAppointmentStartsExpectedWithNoOperationalTimestamps()
    {
        var appointment = NewAppointment();

        Assert.Equal(BookingAppointmentStatus.Expected, appointment.Status);
        Assert.Null(appointment.CheckedInAt);
        Assert.Null(appointment.OutcomeAt);
        Assert.Null(appointment.LastChangedByStaffUserId);
        Assert.Null(appointment.LastChangedAt);
        Assert.Equal(1, appointment.Version);
    }

    /// <summary>Verifies normal check-in and completion timestamp semantics.</summary>
    [Fact]
    public void CheckInThenCompletionPreservesBothOperationalInstants()
    {
        var staff = Guid.NewGuid();
        var appointment = NewAppointment();

        Assert.True(appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, CheckInTime, true, false));
        Assert.True(appointment.TransitionTo(
            BookingAppointmentStatus.Completed, staff, OutcomeTime, false, false));

        Assert.Equal(BookingAppointmentStatus.Completed, appointment.Status);
        Assert.Equal(CheckInTime, appointment.CheckedInAt);
        Assert.Equal(OutcomeTime, appointment.OutcomeAt);
        Assert.Equal(staff, appointment.LastChangedByStaffUserId);
        Assert.Equal(OutcomeTime, appointment.LastChangedAt);
        Assert.Equal(3, appointment.Version);
    }

    /// <summary>Verifies that no-show is a terminal alternative without check-in.</summary>
    [Fact]
    public void NoShowRecordsOnlyTheOutcomeInstant()
    {
        var appointment = NewAppointment();

        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow,
            Guid.NewGuid(),
            OutcomeTime,
            checkInAllowed: false,
            noShowAllowed: true);

        Assert.Equal(BookingAppointmentStatus.NoShow, appointment.Status);
        Assert.Null(appointment.CheckedInAt);
        Assert.Equal(OutcomeTime, appointment.OutcomeAt);
        Assert.Equal(2, appointment.Version);
    }

    /// <summary>Verifies each correction clears only the timestamp invalidated by that reversal.</summary>
    [Fact]
    public void BoundedCorrectionsClearOnlyInvalidatedTimestamps()
    {
        var staff = Guid.NewGuid();
        var appointment = NewAppointment();
        appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, CheckInTime, true, false);
        appointment.TransitionTo(
            BookingAppointmentStatus.Completed, staff, OutcomeTime, false, false);

        appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, OutcomeTime.AddMinutes(1), false, false);
        Assert.Equal(CheckInTime, appointment.CheckedInAt);
        Assert.Null(appointment.OutcomeAt);

        appointment.TransitionTo(
            BookingAppointmentStatus.Expected, staff, OutcomeTime.AddMinutes(2), false, false);
        Assert.Null(appointment.CheckedInAt);
        Assert.Null(appointment.OutcomeAt);

        appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, staff, OutcomeTime.AddMinutes(3), false, true);
        appointment.TransitionTo(
            BookingAppointmentStatus.Expected, staff, OutcomeTime.AddMinutes(4), false, false);
        Assert.Null(appointment.CheckedInAt);
        Assert.Null(appointment.OutcomeAt);
        Assert.Equal(7, appointment.Version);
    }

    /// <summary>Verifies that retrying the stored state changes no observable field.</summary>
    [Fact]
    public void RepeatingTheCurrentStateIsIdempotent()
    {
        var staff = Guid.NewGuid();
        var appointment = NewAppointment();
        appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, CheckInTime, true, false);

        var changed = appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn,
            Guid.NewGuid(),
            OutcomeTime,
            checkInAllowed: false,
            noShowAllowed: false);

        Assert.False(changed);
        Assert.Equal(staff, appointment.LastChangedByStaffUserId);
        Assert.Equal(CheckInTime, appointment.LastChangedAt);
        Assert.Equal(2, appointment.Version);
    }

    /// <summary>Verifies timing gates and transitions outside the approved graph are rejected.</summary>
    [Fact]
    public void InvalidOrPrematureTransitionsAreRejectedWithoutMutation()
    {
        var staff = Guid.NewGuid();
        var appointment = NewAppointment();

        Assert.Throws<DomainException>(() => appointment.TransitionTo(
            BookingAppointmentStatus.CheckedIn, staff, CheckInTime, false, false));
        Assert.Throws<DomainException>(() => appointment.TransitionTo(
            BookingAppointmentStatus.NoShow, staff, OutcomeTime, false, false));
        Assert.Throws<DomainException>(() => appointment.TransitionTo(
            BookingAppointmentStatus.Completed, staff, OutcomeTime, true, true));

        Assert.Equal(BookingAppointmentStatus.Expected, appointment.Status);
        Assert.Equal(1, appointment.Version);
    }

    /// <summary>Verifies identifiers and fixed appointment-type membership at creation.</summary>
    [Fact]
    public void CreationRejectsEmptyOrUnknownIdentifiers()
    {
        Assert.Throws<DomainException>(() => BookingAppointment.Create(
            Guid.Empty, Guid.NewGuid(), AppointmentTypeIds.DrugAndAlcoholTesting));
        Assert.Throws<DomainException>(() => BookingAppointment.Create(
            Guid.NewGuid(), Guid.Empty, AppointmentTypeIds.DrugAndAlcoholTesting));
        Assert.Throws<DomainException>(() => BookingAppointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }

    private static BookingAppointment NewAppointment() => BookingAppointment.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        AppointmentTypeIds.DrugAndAlcoholTesting);
}
