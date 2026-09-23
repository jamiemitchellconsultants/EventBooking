using EventBooking.Domain.Audit;

namespace EventBooking.Domain.Tests.Bookings;

/// <summary>Verifies stable audit names and numeric values for appointment delivery.</summary>
public sealed class BookingAppointmentAuditVocabularyTests
{
    /// <summary>Verifies appointment actions append after Issue #71 without renumbering history.</summary>
    [Fact]
    public void AppointmentActionsAppendAfterStaffAccessActions()
    {
        Assert.Equal(19, (int)AuditAction.AppointmentCheckedIn);
        Assert.Equal(20, (int)AuditAction.AppointmentCompleted);
        Assert.Equal(21, (int)AuditAction.AppointmentMarkedNoShow);
        Assert.Equal(22, (int)AuditAction.AppointmentStatusCorrected);
    }

    /// <summary>Verifies booking appointments can be correlated through the shared audit logger.</summary>
    [Fact]
    public void BookingAppointmentIsAnAuditedEntityType()
    {
        Assert.Equal("BookingAppointment", AuditEntityTypes.BookingAppointment);
        Assert.Single(
            AuditEntityTypes.All,
            value => value == AuditEntityTypes.BookingAppointment);
    }
}
