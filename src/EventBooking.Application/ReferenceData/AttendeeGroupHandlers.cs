using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.ReferenceData;

/// <summary>Creates an attendee group.</summary>
/// <param name="groups">The groups.</param>
/// <param name="types">The types.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unitOfWork.</param>
/// <param name="audit">The audit.</param>
public sealed class CreateAttendeeGroupHandler(
    IAttendeeGroupRepository groups,
    IAppointmentTypeRepository types,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AttendeeGroupResult>> HandleAsync(CreateAttendeeGroupCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
        if (authorized.IsFailure) return Result<AttendeeGroupResult>.Failure(authorized.Error);

        var activeIds = (await types.ListAsync(ct)).Where(t => t.IsActive).Select(t => t.Id).ToList();

        AttendeeGroup group;
        try
        {
            group = AttendeeGroup.Create(Guid.NewGuid(), command.Code, command.Name, command.AppointmentTypeIds, activeIds);
        }
        catch (DomainException ex)
        {
            return Result<AttendeeGroupResult>.Failure(Error.Validation(ex.Message));
        }

        if (await groups.GetByCodeAsync(group.Code, ct) is not null)
            return Result<AttendeeGroupResult>.Failure(Error.Conflict($"An attendee group with code '{group.Code}' already exists."));

        groups.Add(group);
        audit.Record(AuditEntityTypes.AttendeeGroup, group.Id, AuditAction.AttendeeGroupCreated,
            ActorType.Staff, command.StaffUserId.ToString(), $"code {group.Code}");
        await unitOfWork.SaveChangesAsync(ct);
        return Result<AttendeeGroupResult>.Success(new AttendeeGroupResult(
            group.Id, group.Code, group.Name, group.IsActive, group.Version,
            group.RequiredAppointmentTypeIds, 0));
    }
}

/// <summary>Renames an attendee group or replaces its requirement set.</summary>
/// <param name="groups">The groups.</param>
/// <param name="types">The types.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="access">The access.</param>
/// <param name="blocking">The blocking.</param>
/// <param name="unitOfWork">The unitOfWork.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class UpdateAttendeeGroupHandler(
    IAttendeeGroupRepository groups,
    IAppointmentTypeRepository types,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    IStaffAccessAuthorizer access,
    IReferenceDataBlockingQueries blocking,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AttendeeGroupResult>> HandleAsync(UpdateAttendeeGroupCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
        if (authorized.IsFailure) return Result<AttendeeGroupResult>.Failure(authorized.Error);

        var group = await groups.GetAsync(command.AttendeeGroupId, ct);
        if (group is null) return Result<AttendeeGroupResult>.Failure(Error.NotFound("No such attendee group."));
        if (group.Version != command.ExpectedVersion)
            return Result<AttendeeGroupResult>.Failure(Error.VersionConflict("The attendee group changed under you.", group.Version));

        var changes = new List<string>();
        try
        {
            if (command.Name is not null) { group.Rename(command.Name); changes.Add("name"); }
            if (command.AppointmentTypeIds is not null)
            {
                var activeIds = (await types.ListAsync(ct)).Where(t => t.IsActive).Select(t => t.Id).ToList();
                var blockingMembers = await blocking.AttendeeGroupBlockingMemberCountAsync(group.Id, ct);
                if (group.ReplaceRequirements(command.AppointmentTypeIds, activeIds, blockingMembers))
                {
                    var rederived = await RederiveMembersAsync(group, ct);
                    changes.Add($"requirements; {rederived} members re-derived");
                }
            }
        }
        catch (ReferenceDataInUseException ex) when (ex.Blocking.ContainsKey("blockingMembers"))
        {
            return Result<AttendeeGroupResult>.Failure(
                Error.RequirementsLocked(ex.Message, ex.Blocking["blockingMembers"]));
        }
        catch (ReferenceDataInUseException ex)
        {
            return Result<AttendeeGroupResult>.Failure(Error.ReferenceDataInUse(ex.Message, ex.Blocking));
        }
        catch (DomainException ex)
        {
            return Result<AttendeeGroupResult>.Failure(Error.Validation(ex.Message));
        }

        if (changes.Count == 0)
            return Result<AttendeeGroupResult>.Success(await ToResultAsync(group, ct));

        var memberCount = await blocking.AttendeeGroupMemberCountAsync(group.Id, ct);
        audit.Record(AuditEntityTypes.AttendeeGroup, group.Id, AuditAction.AttendeeGroupUpdated,
            ActorType.Staff, command.StaffUserId.ToString(), $"{string.Join("; ", changes)}; {memberCount} members");
        await unitOfWork.SaveChangesAsync(ct);
        return Result<AttendeeGroupResult>.Success(new AttendeeGroupResult(
            group.Id, group.Code, group.Name, group.IsActive, group.Version,
            group.RequiredAppointmentTypeIds, memberCount));
    }

    // Every member's attendee lock is already held in ascending id order by
    // LockByGroupForUpdateAsync. Re-derive, supersede the pending initial invite,
    // and park members that are not already waiting. Booked members cannot reach
    // here: the blocking check above refused first. Returns the member count.
    private async Task<int> RederiveMembersAsync(AttendeeGroup group, CancellationToken ct)
    {
        var members = await attendees.LockByGroupForUpdateAsync(group.Id, ct);
        foreach (var member in members)
        {
            member.AssignAttendeeGroup(group);
            var pending = await invites.LockPendingInitialForAttendeeAsync(member.Id, ct);
            pending?.MarkSuperseded();
            if (member.Status != AttendeeStatus.NotYetInvited)
                member.ResetToNotYetInvited(clock.UtcNow);
        }

        return members.Count;
    }

    private async Task<AttendeeGroupResult> ToResultAsync(AttendeeGroup group, CancellationToken ct) =>
        new(group.Id, group.Code, group.Name, group.IsActive, group.Version,
            group.RequiredAppointmentTypeIds, await blocking.AttendeeGroupMemberCountAsync(group.Id, ct));
}

/// <summary>Activates or deactivates an attendee group.</summary>
/// <param name="groups">The groups.</param>
/// <param name="types">The types.</param>
/// <param name="access">The access.</param>
/// <param name="blocking">The blocking.</param>
/// <param name="unitOfWork">The unitOfWork.</param>
/// <param name="audit">The audit.</param>
public sealed class SetAttendeeGroupActiveHandler(
    IAttendeeGroupRepository groups,
    IAppointmentTypeRepository types,
    IStaffAccessAuthorizer access,
    IReferenceDataBlockingQueries blocking,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<AttendeeGroupResult>> HandleAsync(SetAttendeeGroupActiveCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
        if (authorized.IsFailure) return Result<AttendeeGroupResult>.Failure(authorized.Error);

        var group = await groups.GetAsync(command.AttendeeGroupId, ct);
        if (group is null) return Result<AttendeeGroupResult>.Failure(Error.NotFound("No such attendee group."));
        if (group.Version != command.ExpectedVersion)
            return Result<AttendeeGroupResult>.Failure(Error.VersionConflict("The attendee group changed under you.", group.Version));

        var beforeVersion = group.Version;
        try
        {
            if (command.IsActive)
            {
                var activeIds = (await types.ListAsync(ct)).Where(t => t.IsActive).Select(t => t.Id).ToList();
                group.Reactivate(activeIds);
            }
            else group.Deactivate(await blocking.AttendeeGroupMemberCountAsync(group.Id, ct));
        }
        catch (ReferenceDataInUseException ex)
        {
            return Result<AttendeeGroupResult>.Failure(Error.ReferenceDataInUse(ex.Message, ex.Blocking));
        }

        var memberCount = await blocking.AttendeeGroupMemberCountAsync(group.Id, ct);
        if (group.Version == beforeVersion)
            return Result<AttendeeGroupResult>.Success(new AttendeeGroupResult(
                group.Id, group.Code, group.Name, group.IsActive, group.Version,
                group.RequiredAppointmentTypeIds, memberCount));

        audit.Record(AuditEntityTypes.AttendeeGroup, group.Id, AuditAction.AttendeeGroupUpdated,
            ActorType.Staff, command.StaffUserId.ToString(), $"isActive -> {group.IsActive}");
        await unitOfWork.SaveChangesAsync(ct);
        return Result<AttendeeGroupResult>.Success(new AttendeeGroupResult(
            group.Id, group.Code, group.Name, group.IsActive, group.Version,
            group.RequiredAppointmentTypeIds, memberCount));
    }
}

/// <summary>Lists every attendee group with member counts.</summary>
/// <param name="groups">The groups.</param>
/// <param name="blocking">The blocking.</param>
public sealed class ListAttendeeGroupsHandler(
    IAttendeeGroupRepository groups,
    IReferenceDataBlockingQueries blocking)
{
    /// <summary>Handles the command.</summary>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<AttendeeGroupListItem>>> HandleAsync(CancellationToken ct)
    {
        var rows = await groups.ListAsync(ct);
        var items = new List<AttendeeGroupListItem>();
        foreach (var group in rows.OrderBy(g => g.Code, StringComparer.Ordinal))
        {
            items.Add(new AttendeeGroupListItem(group.Id, group.Code, group.Name, group.IsActive,
                group.RequiredAppointmentTypeIds, await blocking.AttendeeGroupMemberCountAsync(group.Id, ct)));
        }

        return Result<IReadOnlyList<AttendeeGroupListItem>>.Success(items);
    }
}
