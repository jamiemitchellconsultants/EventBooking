using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;

namespace EventBooking.Application.Appointments;

/// <summary>Authorizes and serves the scoped minimum-data appointment workspace.</summary>
/// <param name="access">The access.</param>
/// <param name="queries">The queries.</param>
/// <param name="clock">The clock.</param>
public sealed class GetAppointmentWorkspaceHandler(
    IStaffAccessAuthorizer access,
    IAppointmentWorkspaceQueries queries,
    IClock clock)
{
    private static readonly Error MissingEvent =
        Error.NotFound("No such appointment workspace eventItem.");

    /// <summary>Lists recent-past, current, and upcoming events for the caller's trusted appointment type.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AppointmentWorkspaceEventList>> ListEventsAsync(
        Guid staffUserId,
        CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ConductAppointments,
            requiredAppointmentTypeId: null,
            cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<AppointmentWorkspaceEventList>.Failure(authorization.Error);
        }

        if (authorization.Value.AppointmentTypeId is not Guid appointmentTypeId)
        {
            return Result<AppointmentWorkspaceEventList>.Failure(
                Error.Forbidden("This staff profile cannot perform this operation."));
        }

        return Result<AppointmentWorkspaceEventList>.Success(
            await queries.ListEventsAsync(
                appointmentTypeId, clock.UtcNow, cancellationToken));
    }

    /// <summary>Gets one active event inside the caller's trusted appointment type.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="eventId">The event id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AppointmentEventDetail>> GetEventAsync(
        Guid staffUserId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ConductAppointments,
            requiredAppointmentTypeId: null,
            cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<AppointmentEventDetail>.Failure(authorization.Error);
        }

        if (authorization.Value.AppointmentTypeId is not Guid appointmentTypeId)
        {
            return Result<AppointmentEventDetail>.Failure(
                Error.Forbidden("This staff profile cannot perform this operation."));
        }

        var detail = await queries.GetEventAsync(
            appointmentTypeId, eventId, cancellationToken);
        return detail is null
            ? Result<AppointmentEventDetail>.Failure(MissingEvent)
            : Result<AppointmentEventDetail>.Success(detail);
    }
}
