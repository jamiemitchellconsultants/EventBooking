using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines iappointment type repository for the current use case.</summary>
public interface IAppointmentTypeRepository
{
    /// <summary>Provides list async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<AppointmentType>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentType?> GetAsync(Guid id, CancellationToken cancellationToken);
}
