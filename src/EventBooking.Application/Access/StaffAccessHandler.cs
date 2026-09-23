using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Access;

/// <summary>Projects an access profile with its optional enterprise staff number.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="StaffId">The staff id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="AppointmentTypeName">The appointment type name.</param>
/// <param name="Version">The version.</param>
/// <param name="DisplayName">The display name.</param>
public sealed record StaffAccessProfileView(
    Guid StaffUserId,
    StaffId? StaffId,
    IReadOnlyList<Role> Roles,
    Guid? AppointmentTypeId,
    string? AppointmentTypeName,
    long Version,
    // <summary>
    // The human-readable name mirrored from the identity provider, or null when the identity
    // carries none. Presentation data only; never authorization-relevant.
    // </summary>
    string? DisplayName = null);

/// <summary>Requests atomic replacement of one existing profile's appointment-type scope.</summary>
/// <param name="ActorStaffUserId">The actor staff user id.</param>
/// <param name="TargetStaffUserId">The target staff user id.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record ReplaceStaffAccessProfileScopeCommand(
    Guid ActorStaffUserId,
    Guid TargetStaffUserId,
    Guid? AppointmentTypeId,
    long ExpectedVersion);

/// <summary>Requests clearing one existing profile's Admin-owned scope.</summary>
/// <param name="ActorStaffUserId">The actor staff user id.</param>
/// <param name="TargetStaffUserId">The target staff user id.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record ClearStaffAccessProfileScopeCommand(
    Guid ActorStaffUserId,
    Guid TargetStaffUserId,
    long ExpectedVersion);

/// <summary>Returns a profile mutation and any displaced manager identity.</summary>
/// <param name="Profile">The profile.</param>
/// <param name="FormerManagerStaffUserId">The former manager staff user id.</param>
public sealed record StaffAccessMutationView(
    StaffAccessProfileView Profile,
    Guid? FormerManagerStaffUserId);

/// <summary>Authorizes and applies complete staff-access administration operations.</summary>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class StaffAccessHandler(
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Defines list async for the current use case.</summary>
    /// <param name="actorStaffUserId">The actor staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<StaffAccessProfileView>>> ListAsync(
        Guid actorStaffUserId,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            actorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<StaffAccessProfileView>>.Failure(authorized.Error);
        }

        var current = await profiles.ListAsync(cancellationToken);
        // One listing serves both the staff number and the name; no extra query.
        var identityByUserId = (await identities.ListAsync(cancellationToken))
            .ToDictionary(identity => identity.StaffUserId);
        return Result<IReadOnlyList<StaffAccessProfileView>>.Success(
            current.Select(profile =>
            {
                var identity = identityByUserId.GetValueOrDefault(profile.StaffUserId);
                return ToView(profile, identity?.StaffId, identity?.DisplayName);
            }).ToList());
    }

    /// <summary>Resolves an observed staff number after checking administration capability.</summary>
    /// <param name="actorStaffUserId">The actor staff user id.</param>
    /// <param name="staffIdText">The staff id text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> ResolveIdentityAsync(
        Guid actorStaffUserId,
        string staffIdText,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            actorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        if (!StaffId.TryParse(staffIdText, out var staffId))
        {
            return Result<Guid>.Failure(Error.Validation("Enter a valid staff number."));
        }

        var identity = await identities.GetByStaffIdAsync(staffId!, cancellationToken);
        return identity is null
            ? Result<Guid>.Failure(Error.NotFound(
                "No one with that staff number has signed in yet."))
            : Result<Guid>.Success(identity.StaffUserId);
    }

    /// <summary>Replaces scope without accepting or changing identity-provider roles.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StaffAccessMutationView>> ReplaceScopeAsync(
        ReplaceStaffAccessProfileScopeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ActorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<StaffAccessMutationView>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = (await profiles.LockAllAsync(cancellationToken)).ToList();
        if (!locked.Any(profile =>
                profile.StaffUserId == command.ActorStaffUserId
                && profile.IsAdmin
                && profile.IsValid()))
        {
            return Result<StaffAccessMutationView>.Failure(
                Error.Forbidden("Only an Admin can manage staff access."));
        }

        var current = locked.SingleOrDefault(
            profile => profile.StaffUserId == command.TargetStaffUserId);
        if (current is null)
        {
            return Result<StaffAccessMutationView>.Failure(Error.NotFound("No such staff profile."));
        }

        if (current.Version != command.ExpectedVersion)
        {
            return Conflict<StaffAccessMutationView>(
                "The staff profile was changed by another administrator.");
        }

        // Blocks moving an already-scoped Manager to a different type directly — matching the former
        // AbandonsManagedType guard from Issue #71, since roles no longer change here so the only way
        // this handler can abandon a managed type is by moving its Manager's scope away from it.
        if (current.IsManager
            && current.AppointmentTypeId is not null
            && command.AppointmentTypeId != current.AppointmentTypeId)
        {
            return Conflict<StaffAccessMutationView>(
                "Assign a replacement Manager for the current appointment type first.");
        }

        var previous = AuditStateOf(current);
        Guid? formerManagerId = null;
        if (current.IsManager && command.AppointmentTypeId is not null)
        {
            var former = locked.SingleOrDefault(profile =>
                profile.StaffUserId != current.StaffUserId
                && profile.IsManager
                && profile.AppointmentTypeId == command.AppointmentTypeId);

            if (former is not null)
            {
                formerManagerId = former.StaffUserId;
                var formerBefore = AuditStateOf(former);
                // Roles are identity-provider-owned and cannot be cleared here, so scope is nulled
                // unconditionally: leaving it set would let the displaced Manager keep passing
                // StaffAccessAuthorizer's IsManager + AppointmentTypeId match for this type, silently
                // un-displacing them and recreating two Managers for the same appointment type.
                former.Replace(former.Roles, null);
                Record(command.ActorStaffUserId, former.StaffUserId,
                    AuditAction.StaffAccessChanged, formerBefore, AuditStateOf(former));
            }
        }

        try
        {
            current.Replace(current.Roles, command.AppointmentTypeId);
        }
        catch (DomainException exception)
        {
            return Result<StaffAccessMutationView>.Failure(Error.Validation(exception.Message));
        }

        Record(command.ActorStaffUserId, current.StaffUserId,
            AuditAction.StaffAccessChanged, previous, AuditStateOf(current));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<StaffAccessMutationView>.Success(
            new StaffAccessMutationView(ToView(current, null), formerManagerId));
    }

    /// <summary>Clears scope without deleting or changing identity-provider roles.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> ClearScopeAsync(
        ClearStaffAccessProfileScopeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ActorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = await profiles.LockAllAsync(cancellationToken);
        if (!locked.Any(profile =>
                profile.StaffUserId == command.ActorStaffUserId
                && profile.IsAdmin
                && profile.IsValid()))
        {
            return Result.Failure(Error.Forbidden("Only an Admin can manage staff access."));
        }

        var current = locked.SingleOrDefault(
            profile => profile.StaffUserId == command.TargetStaffUserId);
        if (current is null)
        {
            return Result.Failure(Error.NotFound("No such staff profile."));
        }

        if (current.Version != command.ExpectedVersion)
        {
            return Result.Failure(
                Error.Conflict("The staff profile was changed by another administrator."));
        }

        if (current.AppointmentTypeId is null)
        {
            return Result.Failure(
                Error.Validation("This profile has no appointment-type scope to clear."));
        }

        if (current.IsManager)
        {
            return Result.Failure(Error.Conflict(
                "Assign a replacement Manager for the current appointment type first."));
        }

        var previous = AuditStateOf(current);
        current.Replace(current.Roles, null);
        Record(command.ActorStaffUserId, current.StaffUserId,
            AuditAction.StaffAccessChanged, previous, AuditStateOf(current));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static Result<T> Conflict<T>(string message) =>
        Result<T>.Failure(Error.Conflict(message));

    private static StaffAccessProfileView ToView(
        StaffAccessProfile profile,
        StaffId? staffId,
        string? displayName = null) => new(
        profile.StaffUserId,
        staffId,
        profile.Roles.OrderBy(role => role).ToList(),
        profile.AppointmentTypeId,
        profile.AppointmentTypeId is null
            ? null
            : AppointmentTypeIds.NameOf(profile.AppointmentTypeId.Value),
        profile.Version,
        displayName);

    private void Record(
        Guid actorStaffUserId,
        Guid targetStaffUserId,
        AuditAction action,
        AuditState? previous,
        AuditState? current) =>
        audit.Record(
            AuditEntityTypes.StaffAccessProfile,
            targetStaffUserId,
            action,
            ActorType.Staff,
            actorStaffUserId.ToString(),
            JsonSerializer.Serialize(new { previous, current }));

    private static AuditState AuditStateOf(StaffAccessProfile profile) => new(
        profile.Roles.OrderBy(role => role).Select(role => role.ToString()).ToList(),
        profile.AppointmentTypeId,
        profile.Version);

    private sealed record AuditState(
        IReadOnlyList<string> Roles,
        Guid? AppointmentTypeId,
        long Version);
}
