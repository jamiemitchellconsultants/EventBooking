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
    private static readonly Error MissingSlot =
        Error.NotFound("No such appointment workspace slot.");

    /// <summary>Lists recent-past, current, and upcoming slots for the caller's trusted appointment type.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AppointmentWorkspaceSlotList>> ListSlotsAsync(
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
            return Result<AppointmentWorkspaceSlotList>.Failure(authorization.Error);
        }

        if (authorization.Value.AppointmentTypeId is not Guid appointmentTypeId)
        {
            return Result<AppointmentWorkspaceSlotList>.Failure(
                Error.Forbidden("This staff profile cannot perform this operation."));
        }

        return Result<AppointmentWorkspaceSlotList>.Success(
            await queries.ListSlotsAsync(
                appointmentTypeId, clock.TodayAtHeadOffice, cancellationToken));
    }

    /// <summary>Gets one active slot inside the caller's trusted appointment type.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AppointmentSlotDetail>> GetSlotAsync(
        Guid staffUserId,
        Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ConductAppointments,
            requiredAppointmentTypeId: null,
            cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<AppointmentSlotDetail>.Failure(authorization.Error);
        }

        if (authorization.Value.AppointmentTypeId is not Guid appointmentTypeId)
        {
            return Result<AppointmentSlotDetail>.Failure(
                Error.Forbidden("This staff profile cannot perform this operation."));
        }

        var detail = await queries.GetSlotAsync(
            appointmentTypeId, confirmedSlotId, cancellationToken);
        return detail is null
            ? Result<AppointmentSlotDetail>.Failure(MissingSlot)
            : Result<AppointmentSlotDetail>.Success(detail);
    }
}
