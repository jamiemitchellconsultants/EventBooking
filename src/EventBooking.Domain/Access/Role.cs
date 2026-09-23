namespace EventBooking.Domain.Access;

/// <summary>Defines role for the current use case.</summary>
public enum Role
{
    /// <summary>Defines manager for the current use case.</summary>
    Manager = 1,
    /// <summary>Defines coordinator for the current use case.</summary>
    Coordinator = 2,
    /// <summary>Defines admin for the current use case.</summary>
    Admin = 3,
    /// <summary>Defines appointment staff for the current use case.</summary>
    AppointmentStaff = 4,
}
