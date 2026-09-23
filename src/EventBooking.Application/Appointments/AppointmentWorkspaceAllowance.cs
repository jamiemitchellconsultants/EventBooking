namespace EventBooking.Application.Appointments;

/// <summary>The recent-past allowance applied to appointment workspace reads.</summary>
public static class AppointmentWorkspaceAllowance
{
    /// <summary>Number of calendar days of recently past events the workspace retains.</summary>
    public const int RecentPastDays = 7;
}
