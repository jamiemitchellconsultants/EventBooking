namespace EventBooking.Domain.EmployeeGroups;

/// <summary>One fixed Appointment Type required by an Employee Group.</summary>
public sealed class EmployeeGroupRequirement
{
    private EmployeeGroupRequirement()
    {
    }

    /// <summary>Gets the owning Employee Group identifier.</summary>
    public Guid EmployeeGroupId { get; private set; }

    /// <summary>Gets the required fixed Appointment Type identifier.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static EmployeeGroupRequirement For(Guid employeeGroupId, Guid appointmentTypeId) =>
        new() { EmployeeGroupId = employeeGroupId, AppointmentTypeId = appointmentTypeId };
}
