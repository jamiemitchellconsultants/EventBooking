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

    /// <summary>Gets a type from a trimmed case-insensitive canonical-code input.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AppointmentType?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Stages a new appointment type for the next save.</summary>
    /// <param name="type">The type.</param>
    void Add(AppointmentType type);
}
