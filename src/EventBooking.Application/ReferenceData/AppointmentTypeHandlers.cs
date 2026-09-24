using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.ReferenceData;

/// <summary>Creates an appointment type.</summary>
/// <param name="types">The types.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unitOfWork.</param>
/// <param name="audit">The audit.</param>
public sealed class CreateAppointmentTypeHandler(
    IAppointmentTypeRepository types,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AppointmentTypeResult>> HandleAsync(CreateAppointmentTypeCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
        if (authorized.IsFailure) return Result<AppointmentTypeResult>.Failure(authorized.Error);

        AppointmentType type;
        try
        {
            type = AppointmentType.Create(Guid.NewGuid(), command.Code, command.Name);
        }
        catch (DomainException ex)
        {
            return Result<AppointmentTypeResult>.Failure(Error.Validation(ex.Message));
        }

        if (await types.GetByCodeAsync(type.Code, ct) is not null)
            return Result<AppointmentTypeResult>.Failure(Error.Conflict($"An appointment type with code '{type.Code}' already exists."));

        types.Add(type);
        audit.Record(AuditEntityTypes.AppointmentType, type.Id, AuditAction.AppointmentTypeCreated,
            ActorType.Staff, command.StaffUserId.ToString(), $"code {type.Code}");
        await unitOfWork.SaveChangesAsync(ct);
        return Result<AppointmentTypeResult>.Success(new AppointmentTypeResult(
            type.Id, type.Code, type.Name, type.IsActive, type.Version, null));
    }
}

/// <summary>Renames an appointment type.</summary>
/// <param name="types">The types.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unitOfWork.</param>
/// <param name="audit">The audit.</param>
/// <param name="blocking">The blocking.</param>
public sealed class UpdateAppointmentTypeHandler(
    IAppointmentTypeRepository types,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IReferenceDataBlockingQueries blocking)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AppointmentTypeResult>> HandleAsync(UpdateAppointmentTypeCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
        if (authorized.IsFailure) return Result<AppointmentTypeResult>.Failure(authorized.Error);

        var type = await types.GetAsync(command.AppointmentTypeId, ct);
        if (type is null) return Result<AppointmentTypeResult>.Failure(Error.NotFound("No such appointment type."));
        if (type.Version != command.ExpectedVersion)
            return Result<AppointmentTypeResult>.Failure(Error.VersionConflict("The appointment type changed under you.", type.Version));

        var changes = new List<string>();
        try
        {
            if (command.Name is not null) { type.Rename(command.Name); changes.Add("name"); }
            if (command.IsActive != type.IsActive)
            {
                if (command.IsActive) type.Reactivate();
                else type.Deactivate(await blocking.AppointmentTypeUsageAsync(type.Id, ct));
                changes.Add($"isActive -> {type.IsActive}");
            }
        }
        catch (ReferenceDataInUseException ex)
        {
            return Result<AppointmentTypeResult>.Failure(Error.ReferenceDataInUse(ex.Message, ex.Blocking));
        }
        catch (DomainException ex)
        {
            return Result<AppointmentTypeResult>.Failure(Error.Validation(ex.Message));
        }

        if (changes.Count == 0)
            return Result<AppointmentTypeResult>.Success(ToResult(type));

        audit.Record(AuditEntityTypes.AppointmentType, type.Id, AuditAction.AppointmentTypeUpdated,
            ActorType.Staff, command.StaffUserId.ToString(), string.Join("; ", changes));
        await unitOfWork.SaveChangesAsync(ct);
        return Result<AppointmentTypeResult>.Success(ToResult(type));
    }

    private static AppointmentTypeResult ToResult(AppointmentType type) => new(
        type.Id, type.Code, type.Name, type.IsActive, type.Version, null);
}

// ManagerDisplayName falls back to StaffId when the identity carries no display
// name — the same projection the settings handler already uses.
/// <summary>Lists appointment types in code order, hiding inactive rows unless asked. Open
/// to any staff member: design 05 names no capability, so the endpoint's staff policy is the gate.</summary>
/// <param name="types">The types.</param>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
public sealed class ListAppointmentTypesHandler(
    IAppointmentTypeRepository types,
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities)
{
    /// <summary>Handles the query.</summary>
    /// <param name="query">Whether to include inactive rows.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<AppointmentTypeListItem>>> HandleAsync(
        ListAppointmentTypesQuery query, CancellationToken ct)
    {
        var rows = await types.ListAsync(ct);
        var managerByType = (await profiles.ListAsync(ct))
            .Where(p => p.IsManager && p.AppointmentTypeId is not null)
            .ToDictionary(p => p.AppointmentTypeId!.Value, p => p.StaffUserId);
        var identityByUserId = (await identities.ListAsync(ct)).ToDictionary(i => i.StaffUserId);

        return Result<IReadOnlyList<AppointmentTypeListItem>>.Success(
            rows.Where(t => query.IncludeInactive || t.IsActive)
                .OrderBy(t => t.Code, StringComparer.Ordinal)
                .Select(t =>
                {
                    var managerUserId = managerByType.TryGetValue(t.Id, out var found) ? found : (Guid?)null;
                    var identity = managerUserId is null ? null : identityByUserId.GetValueOrDefault(managerUserId.Value);
                    return new AppointmentTypeListItem(t.Id, t.Code, t.Name, t.IsActive,
                        identity?.DisplayName ?? identity?.StaffId.Value);
                })
                .ToList());
    }
}
