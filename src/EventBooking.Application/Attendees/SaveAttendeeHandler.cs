using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Attendees;

/// <summary>Requests creation of a Attendee in one Attendee Group.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
public sealed record CreateAttendeeCommand(
    Guid StaffUserId,
    string? Name,
    string? Email,
    Guid? AttendeeGroupId);

/// <summary>Requests detail and Attendee Group changes for one Attendee.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
public sealed record UpdateAttendeeCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    string? Name,
    string? Email,
    Guid? AttendeeGroupId);

/// <summary>
/// Creates and updates attendees from one active Attendee Group, serializing group edits with
/// the Attendee lifecycle under one Attendee-first lock order.
/// </summary>
public sealed class SaveAttendeeHandler
{
    private static readonly JsonSerializerOptions AuditJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IAttendeeRepository _attendees;
    private readonly IAttendeeGroupRepository _groups;
    private readonly IInviteRepository _invites;
    private readonly IBookingRepository _bookings;
    private readonly IStaffAccessAuthorizer _access;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Creates handler dependencies for group-derived attendee management.</summary>
    /// <param name="attendees">Persists attendee rows.</param>
    /// <param name="groups">Resolves the assigned Attendee Group.</param>
    /// <param name="invites">Locks the attendee's pending invite for lifecycle work.</param>
    /// <param name="bookings">Locks the attendee's active booking for the requirement invariant.</param>
    /// <param name="access">Authorizes attendee management.</param>
    /// <param name="audit">Records Attendee Group assignment.</param>
    /// <param name="unitOfWork">Owns the attendee save.</param>
    public SaveAttendeeHandler(
        IAttendeeRepository attendees,
        IAttendeeGroupRepository groups,
        IInviteRepository invites,
        IBookingRepository bookings,
        IStaffAccessAuthorizer access,
        IAuditLogger audit,
        IUnitOfWork unitOfWork)
    {
        _attendees = attendees;
        _groups = groups;
        _invites = invites;
        _bookings = bookings;
        _access = access;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Creates one attendee and derives every requirement from its Attendee Group.</summary>
    /// <param name="command">The staff creation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new attendee identifier.</returns>
    public async Task<Result<Guid>> CreateAsync(
        CreateAttendeeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await _access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        var resolved = await ResolveGroupAsync(command.AttendeeGroupId, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<Guid>.Failure(resolved.Error);
        }

        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (await _attendees.GetByEmailAsync(email, cancellationToken) is not null)
        {
            return Result<Guid>.Failure(Error.Conflict($"{email} is already a attendee."));
        }

        var id = Guid.NewGuid();

        Attendee attendee;
        try
        {
            attendee = Attendee.Create(id, command.Name, command.Email, resolved.Value);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        _attendees.Add(attendee);
        _audit.Record(
            AuditEntityTypes.Attendee,
            id,
            AuditAction.AttendeeGroupAssigned,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            SerializeAssignment(null, resolved.Value.Code, [], RequirementCodes(resolved.Value)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(id);
    }

    /// <summary>
    /// Updates attendee details under the Attendee lifecycle lock, superseding a pending Invite
    /// and resetting status when the derived set changes, and rejecting set changes that would
    /// alter an active original Booking.
    /// </summary>
    /// <param name="command">The staff update request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success, or a conflict when requirements drift from the active booking snapshot.</returns>
    public async Task<Result> UpdateAsync(
        UpdateAttendeeCommand command,
        CancellationToken cancellationToken)
    {
        var updateAuthorized = await _access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (updateAuthorized.IsFailure)
        {
            return Result.Failure(updateAuthorized.Error);
        }

        if (await _attendees.GetAsync(command.AttendeeId, cancellationToken) is null)
        {
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await _attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        var resolved = await ResolveGroupAsync(command.AttendeeGroupId, cancellationToken);
        if (resolved.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(resolved.Error);
        }

        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        var owner = await _attendees.GetByEmailAsync(email, cancellationToken);
        if (owner is not null && owner.Id != attendee.Id)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict($"{email} is already a attendee."));
        }

        var pendingInvite = await _invites.LockPendingInitialForAttendeeAsync(
            attendee.Id, cancellationToken);
        var activeBooking = await _bookings.LockActiveOriginalForAttendeeAsync(
            attendee.Id, cancellationToken);

        var submittedRequirements = resolved.Value.RequiredAppointmentTypeIds.ToHashSet();
        var currentRequirements = attendee.RequiredAppointmentTypeIds.ToHashSet();
        var setChanged = !submittedRequirements.SetEquals(currentRequirements);
        var detailsChanged =
            !string.Equals((command.Name ?? string.Empty).Trim(), attendee.Name, StringComparison.Ordinal)
            || !string.Equals(email, attendee.Email, StringComparison.Ordinal);
        var groupChanged = attendee.AttendeeGroupId != resolved.Value.Id;

        if (!setChanged && !detailsChanged && !groupChanged)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success();
        }

        if (setChanged && activeBooking is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.AttendeeGroupActiveBookingConflict(
                "Appointment requirements cannot change while the attendee has an active booking. "
                + "Cancel and rebook first."));
        }

        string? oldGroupCode = null;
        if (attendee.AttendeeGroupId.HasValue)
        {
            oldGroupCode = (await _groups.GetAsync(attendee.AttendeeGroupId.Value, cancellationToken))?.Code;
        }

        var oldRequirementCodes = RequirementCodes(attendee.RequiredAppointmentTypeIds);
        var newRequirementCodes = RequirementCodes(resolved.Value.RequiredAppointmentTypeIds);

        try
        {
            attendee.UpdateDetails(command.Name, command.Email);

            if (setChanged)
            {
                pendingInvite?.MarkSuperseded();
                attendee.ResetAfterRequirementChange();
            }

            attendee.AssignAttendeeGroup(resolved.Value);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(ex.Message));
        }

        _audit.Record(
            AuditEntityTypes.Attendee,
            attendee.Id,
            AuditAction.AttendeeGroupReassigned,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            SerializeAssignment(oldGroupCode, resolved.Value.Code, oldRequirementCodes, newRequirementCodes));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<AttendeeGroup>> ResolveGroupAsync(
        Guid? attendeeGroupId,
        CancellationToken cancellationToken)
    {
        if (attendeeGroupId is null)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_required", "An attendee group is required."));
        }

        var group = await _groups.GetAsync(attendeeGroupId.Value, cancellationToken);
        if (group is null)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_unknown", "The attendee group is not known."));
        }

        if (!group.IsActive)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_inactive", "The attendee group is not active."));
        }

        if (group.RequiredAppointmentTypeIds.Count == 0)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_unmapped", "The attendee group has no mapped appointment types."));
        }

        return Result<AttendeeGroup>.Success(group);
    }

    private static IReadOnlyList<string> RequirementCodes(AttendeeGroup group) =>
        RequirementCodes(group.RequiredAppointmentTypeIds);

    private static IReadOnlyList<string> RequirementCodes(IEnumerable<Guid> appointmentTypeIds) =>
        appointmentTypeIds.Select(AppointmentTypeIds.CodeOf).Order(StringComparer.Ordinal).ToList();

    private static string SerializeAssignment(
        string? oldGroupCode,
        string newGroupCode,
        IReadOnlyList<string> oldRequirementCodes,
        IReadOnlyList<string> newRequirementCodes) =>
        JsonSerializer.Serialize(
            new GroupAssignmentAudit(oldGroupCode, newGroupCode, oldRequirementCodes, newRequirementCodes),
            AuditJson);

    private sealed record GroupAssignmentAudit(
        string? OldGroupCode,
        string NewGroupCode,
        IReadOnlyList<string> OldRequirementCodes,
        IReadOnlyList<string> NewRequirementCodes);
}
