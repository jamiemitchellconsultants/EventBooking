using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Every appointment-type code, read from the database, so Admin-managed types created after the
/// predecessor's fixed three render exactly like the originals. Load once per pass or query and
/// look codes up in memory.
/// </summary>
public sealed class AppointmentTypeCodes
{
    private readonly IReadOnlyDictionary<Guid, string> _codes;

    private AppointmentTypeCodes(IReadOnlyDictionary<Guid, string> codes) => _codes = codes;

    /// <summary>Reads every appointment type's code.</summary>
    /// <param name="context">The context to read from.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<AppointmentTypeCodes> LoadAsync(
        EventBookingDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new AppointmentTypeCodes(await context.AppointmentTypes.AsNoTracking()
            .ToDictionaryAsync(type => type.Id, type => type.Code, cancellationToken));
    }

    /// <summary>Gets one type's code, refusing a type that no longer exists.</summary>
    /// <param name="appointmentTypeId">The appointment type.</param>
    public string CodeOf(Guid appointmentTypeId) =>
        _codes.TryGetValue(appointmentTypeId, out var code)
            ? code
            : throw new InvalidOperationException($"Appointment type {appointmentTypeId} is gone.");
}
