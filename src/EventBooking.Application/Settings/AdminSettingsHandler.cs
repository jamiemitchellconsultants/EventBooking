using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Settings;

/// <summary>Projects one appointment type with the manager assigned to it, when there is one.</summary>
/// <param name="Id">The appointment type identifier.</param>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="ManagerUserId">The assigned manager's provider identity, or null when unassigned.</param>
/// <param name="ManagerStaffId">The manager's enterprise staff number; null until an identity is recorded.</param>
/// <param name="ManagerDisplayName">
/// The manager's human-readable name mirrored from the identity provider; null when the identity
/// carries none. Presentation data only.
/// </param>
public sealed record AppointmentTypeView(
    Guid Id,
    string Code,
    string Name,
    Guid? ManagerUserId,
    string? ManagerStaffId = null,
    string? ManagerDisplayName = null);

/// <summary>Defines settings view for the current use case.</summary>
/// <param name="InviteExpiryDays">The invite expiry days.</param>
/// <param name="MaxAutoRetryCount">The max auto retry count.</param>
/// <param name="AppointmentTypes">The appointment types.</param>
public sealed record SettingsView(
    int InviteExpiryDays,
    int MaxAutoRetryCount,
    IReadOnlyList<AppointmentTypeView> AppointmentTypes);

/// <summary>Defines update settings command for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="InviteExpiryDays">The invite expiry days.</param>
/// <param name="MaxAutoRetryCount">The max auto retry count.</param>
public sealed record UpdateSettingsCommand(Guid StaffUserId, int InviteExpiryDays, int MaxAutoRetryCount);

/// <summary>Defines admin settings handler for the current use case.</summary>
/// <param name="settings">The settings.</param>
/// <param name="appointmentTypes">The appointment types.</param>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class AdminSettingsHandler(
    ISystemSettingsRepository settings,
    IAppointmentTypeRepository appointmentTypes,
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork)
{
    /// <summary>Defines get async for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<SettingsView>> GetAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ManageSettings,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<SettingsView>.Failure(authorized.Error);
        }

        var current = await settings.GetAsync(cancellationToken);
        var types = await appointmentTypes.ListAsync(cancellationToken);

        var managerByType = (await profiles.ListAsync(cancellationToken))
            .Where(profile => profile.IsManager && profile.AppointmentTypeId is not null)
            .ToDictionary(
                profile => profile.AppointmentTypeId!.Value,
                profile => profile.StaffUserId);

        // One listing serves both projections; the name never costs an extra query.
        var identityByUserId = (await identities.ListAsync(cancellationToken))
            .ToDictionary(identity => identity.StaffUserId);

        return Result<SettingsView>.Success(new SettingsView(
            current.InviteExpiryDays,
            current.MaxAutoRetryCount,
            types.Select(type =>
            {
                Guid? managerUserId = managerByType.TryGetValue(type.Id, out var found) ? found : null;
                var identity = managerUserId is null
                    ? null
                    : identityByUserId.GetValueOrDefault(managerUserId.Value);
                return new AppointmentTypeView(
                    type.Id,
                    type.Code,
                    type.Name,
                    managerUserId,
                    identity?.StaffId.Value,
                    identity?.DisplayName);
            }).ToList()));
    }

    /// <summary>Defines update async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> UpdateAsync(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageSettings,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        var current = await settings.GetAsync(cancellationToken);

        try
        {
            current.Update(command.InviteExpiryDays, command.MaxAutoRetryCount);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
