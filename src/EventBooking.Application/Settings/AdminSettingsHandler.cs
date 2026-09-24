using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Settings;

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
/// <param name="InviteOptionCount">How many event options each invite offers.</param>
/// <param name="Version">The optimistic-concurrency version to send back with the next save.</param>
/// <param name="AppointmentTypes">The appointment types.</param>
public sealed record SettingsView(
    int InviteExpiryDays,
    int MaxAutoRetryCount,
    int InviteOptionCount,
    long Version,
    IReadOnlyList<AppointmentTypeView> AppointmentTypes);

/// <summary>Saves the singleton system settings.</summary>
/// <param name="StaffUserId">The acting staff member.</param>
/// <param name="InviteExpiryDays">How many days an invite stays open.</param>
/// <param name="MaxAutoRetryCount">How many times a failed invite is retried automatically.</param>
/// <param name="InviteOptionCount">How many event options each invite offers.</param>
/// <param name="ExpectedVersion">The version the caller read; stale versions conflict.</param>
public sealed record SaveSystemSettingsCommand(
    Guid StaffUserId,
    int InviteExpiryDays,
    int MaxAutoRetryCount,
    int InviteOptionCount,
    long ExpectedVersion);

/// <summary>The saved system settings.</summary>
/// <param name="InviteExpiryDays">How many days an invite stays open.</param>
/// <param name="MaxAutoRetryCount">How many times a failed invite is retried automatically.</param>
/// <param name="InviteOptionCount">How many event options each invite offers.</param>
/// <param name="Version">The new optimistic-concurrency version.</param>
public sealed record SystemSettingsResult(
    int InviteExpiryDays,
    int MaxAutoRetryCount,
    int InviteOptionCount,
    long Version);

/// <summary>Saves the singleton system settings with a version check.</summary>
/// <param name="settings">The system settings repository.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit logger.</param>
public sealed class SaveSystemSettingsHandler(
    ISystemSettingsRepository settings,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    // The singleton's key is the integer 1, but audit rows carry a Guid entity id that must
    // not be empty; every settings change is recorded against this fixed stand-in.
    private static readonly Guid SettingsEntityId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<SystemSettingsResult>> HandleAsync(
        SaveSystemSettingsCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ManageSettings, null, ct);
        if (authorized.IsFailure) return Result<SystemSettingsResult>.Failure(authorized.Error);

        var current = await settings.GetAsync(ct);
        if (current.Version != command.ExpectedVersion)
            return Result<SystemSettingsResult>.Failure(
                Error.VersionConflict("The settings changed under you.", current.Version));

        var before = new SystemSettingsResult(
            current.InviteExpiryDays, current.MaxAutoRetryCount, current.InviteOptionCount, current.Version);
        try
        {
            current.Update(command.InviteExpiryDays, command.MaxAutoRetryCount, command.InviteOptionCount);
        }
        catch (DomainException ex)
        {
            return Result<SystemSettingsResult>.Failure(Error.Validation(ex.Message));
        }

        audit.Record(AuditEntityTypes.SystemSettings, SettingsEntityId, AuditAction.SystemSettingsChanged,
            ActorType.Staff, command.StaffUserId.ToString(),
            $"expiryDays {before.InviteExpiryDays} -> {current.InviteExpiryDays}; "
            + $"retryCount {before.MaxAutoRetryCount} -> {current.MaxAutoRetryCount}; "
            + $"optionCount {before.InviteOptionCount} -> {current.InviteOptionCount}");
        await unitOfWork.SaveChangesAsync(ct);
        return Result<SystemSettingsResult>.Success(new SystemSettingsResult(
            current.InviteExpiryDays, current.MaxAutoRetryCount, current.InviteOptionCount, current.Version));
    }
}

/// <summary>Defines admin settings handler for the current use case.</summary>
/// <param name="settings">The settings.</param>
/// <param name="appointmentTypes">The appointment types.</param>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AdminSettingsHandler(
    ISystemSettingsRepository settings,
    IAppointmentTypeRepository appointmentTypes,
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
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
            current.InviteOptionCount,
            current.Version,
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

    /// <summary>Saves the system settings through the versioned settings handler.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<Result<SystemSettingsResult>> SaveAsync(
        SaveSystemSettingsCommand command, CancellationToken cancellationToken) =>
        new SaveSystemSettingsHandler(settings, access, unitOfWork, audit)
            .HandleAsync(command, cancellationToken);
}
