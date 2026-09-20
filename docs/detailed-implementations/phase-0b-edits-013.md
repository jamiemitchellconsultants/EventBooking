# 00b — Vocabulary edits 13 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs — 1/1

<!-- vocabulary-file: {"id":67,"oldPath":"src/EventBooking.Application/Candidates/SaveCandidateHandler.cs","newPath":"src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs","beforeSha":"1010cb4a8b652db59f9d4a78735d7134fe59797d25736b5cd571d9f4c9c64eb1","afterSha":"5972e8ec6386ef1ce8b34be8dca959a1ce99d6f3dbff155ea4a1393f6e478059","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — src/EventBooking.Application/Common/Error.cs — 1/1

<!-- vocabulary-file: {"id":68,"oldPath":"src/EventBooking.Application/Common/Error.cs","newPath":"src/EventBooking.Application/Common/Error.cs","beforeSha":"037c52566afddbe017d74ce68fb5d7c55092ba9bf6364e24ab7770424649274b","afterSha":"2a315f16dddbcde1aa683f5aeb04df4f98696839c1519efbbd64cf579321fdb3","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Common;

/// <summary>A machine-readable code plus text safe to show a user.</summary>
/// <param name="Code">The code.</param>
/// <param name="Message">The message.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>Identifies a stale booking-appointment version conflict.</summary>
    public const string AppointmentVersionConflictCode = "appointment_version_conflict";

    /// <summary>Defines none for the current use case.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Defines validation for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Validation(string message) => new("validation", message);

    /// <summary>Defines not found for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error NotFound(string message) => new("not_found", message);

    /// <summary>Defines conflict for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Conflict(string message) => new("conflict", message);

    /// <summary>Creates a stale booking-appointment version conflict.</summary>
    /// <param name="message">The message.</param>
    public static Error AppointmentVersionConflict(string message) =>
        new(AppointmentVersionConflictCode, message);

    /// <summary>Identifies a group change that would alter an active Booking's requirements.</summary>
    public const string CandidateGroupActiveBookingConflictCode = "candidate_group_active_booking_conflict";

    /// <summary>Creates a group change rejected by an active Booking.</summary>
    /// <param name="message">The message.</param>
    public static Error CandidateGroupActiveBookingConflict(string message) =>
        new(CandidateGroupActiveBookingConflictCode, message);

    /// <summary>Identifies an Invite or readiness action for a legacy unassigned Candidate.</summary>
    public const string CandidateReconciliationRequiredCode = "candidate_reconciliation_required";

    /// <summary>Creates a reconciliation hold for a legacy unassigned Candidate.</summary>
    /// <param name="message">The message.</param>
    public static Error CandidateReconciliationRequired(string message) =>
        new(CandidateReconciliationRequiredCode, message);

    /// <summary>Identifies materialized requirements disagreeing with authoritative state.</summary>
    public const string CandidateRequirementSnapshotMismatchCode = "candidate_requirement_snapshot_mismatch";

    /// <summary>Creates a mismatch between materialized and authoritative requirements.</summary>
    /// <param name="message">The message.</param>
    public static Error CandidateRequirementSnapshotMismatch(string message) =>
        new(CandidateRequirementSnapshotMismatchCode, message);

    /// <summary>Defines forbidden for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Forbidden(string message) => new("forbidden", message);

    /// <summary>Identifies a recovery request while a pending Invite or active recovery exists.</summary>
    public const string RecoveryAlreadyPendingCode = "recovery_already_pending";

    /// <summary>Creates a recovery request rejected by an already-pending recovery.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryAlreadyPending(string message) =>
        new(RecoveryAlreadyPendingCode, message);

    /// <summary>Identifies a recovery request with no recoverable no-show type.</summary>
    public const string RecoveryNotAvailableCode = "recovery_not_available";

    /// <summary>Creates a recovery request with nothing eligible to recover.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryNotAvailable(string message) =>
        new(RecoveryNotAvailableCode, message);

    /// <summary>Identifies a recovery request invalidated by a concurrent eligibility change.</summary>
    public const string RecoveryStateChangedCode = "recovery_state_changed";

    /// <summary>Creates a recovery request invalidated by a concurrent eligibility change.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryStateChanged(string message) =>
        new(RecoveryStateChangedCode, message);
}
`````

## after — src/EventBooking.Application/Common/Error.cs — 1/1

<!-- vocabulary-file: {"id":68,"oldPath":"src/EventBooking.Application/Common/Error.cs","newPath":"src/EventBooking.Application/Common/Error.cs","beforeSha":"037c52566afddbe017d74ce68fb5d7c55092ba9bf6364e24ab7770424649274b","afterSha":"2a315f16dddbcde1aa683f5aeb04df4f98696839c1519efbbd64cf579321fdb3","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Common;

/// <summary>A machine-readable code plus text safe to show a user.</summary>
/// <param name="Code">The code.</param>
/// <param name="Message">The message.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>Identifies a stale booking-appointment version conflict.</summary>
    public const string AppointmentVersionConflictCode = "appointment_version_conflict";

    /// <summary>Defines none for the current use case.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Defines validation for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Validation(string message) => new("validation", message);

    /// <summary>Defines not found for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error NotFound(string message) => new("not_found", message);

    /// <summary>Defines conflict for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Conflict(string message) => new("conflict", message);

    /// <summary>Creates a stale booking-appointment version conflict.</summary>
    /// <param name="message">The message.</param>
    public static Error AppointmentVersionConflict(string message) =>
        new(AppointmentVersionConflictCode, message);

    /// <summary>Identifies a group change that would alter an active Booking's requirements.</summary>
    public const string AttendeeGroupActiveBookingConflictCode = "attendee_group_active_booking_conflict";

    /// <summary>Creates a group change rejected by an active Booking.</summary>
    /// <param name="message">The message.</param>
    public static Error AttendeeGroupActiveBookingConflict(string message) =>
        new(AttendeeGroupActiveBookingConflictCode, message);

    /// <summary>Identifies an Invite or readiness action for a legacy unassigned Attendee.</summary>
    public const string AttendeeReconciliationRequiredCode = "attendee_reconciliation_required";

    /// <summary>Creates a reconciliation hold for a legacy unassigned Attendee.</summary>
    /// <param name="message">The message.</param>
    public static Error AttendeeReconciliationRequired(string message) =>
        new(AttendeeReconciliationRequiredCode, message);

    /// <summary>Identifies materialized requirements disagreeing with authoritative state.</summary>
    public const string AttendeeRequirementSnapshotMismatchCode = "attendee_requirement_snapshot_mismatch";

    /// <summary>Creates a mismatch between materialized and authoritative requirements.</summary>
    /// <param name="message">The message.</param>
    public static Error AttendeeRequirementSnapshotMismatch(string message) =>
        new(AttendeeRequirementSnapshotMismatchCode, message);

    /// <summary>Defines forbidden for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Forbidden(string message) => new("forbidden", message);

    /// <summary>Identifies a recovery request while a pending Invite or active recovery exists.</summary>
    public const string RecoveryAlreadyPendingCode = "recovery_already_pending";

    /// <summary>Creates a recovery request rejected by an already-pending recovery.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryAlreadyPending(string message) =>
        new(RecoveryAlreadyPendingCode, message);

    /// <summary>Identifies a recovery request with no recoverable no-show type.</summary>
    public const string RecoveryNotAvailableCode = "recovery_not_available";

    /// <summary>Creates a recovery request with nothing eligible to recover.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryNotAvailable(string message) =>
        new(RecoveryNotAvailableCode, message);

    /// <summary>Identifies a recovery request invalidated by a concurrent eligibility change.</summary>
    public const string RecoveryStateChangedCode = "recovery_state_changed";

    /// <summary>Creates a recovery request invalidated by a concurrent eligibility change.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryStateChanged(string message) =>
        new(RecoveryStateChangedCode, message);
}
`````

## before — src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs — 1/1

<!-- vocabulary-file: {"id":69,"oldPath":"src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs","newPath":"src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs","beforeSha":"18c93fc6d9585914074795f8e3db8dad0cc95c84982697e2d40f8da404774a4a","afterSha":"0b8cc1a5ee88f3a7b126a78b0d6e9826e59dcf1c86a422a464f2ae477ced02d1","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Dashboards;

/// <summary>A null entity type means "this candidate's whole history".</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="EntityType">The entity type.</param>
/// <param name="EntityId">The entity id.</param>
public sealed record GetAuditHistoryQuery(Guid StaffUserId, string? EntityType, Guid EntityId);

/// <summary>Defines get audit history handler for the current use case.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetAuditHistoryHandler(
    IAuditQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<AuditHistoryRow>>> HandleAsync(
        GetAuditHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var capability = query.EntityType == AuditEntityTypes.ConfirmedSlot
            ? StaffCapability.ViewSlotAudit
            : StaffCapability.ViewCandidateAudit;
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, capability, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<AuditHistoryRow>>.Failure(authorized.Error);
        }

        if (query.EntityType is null)
        {
            return Result<IReadOnlyList<AuditHistoryRow>>.Success(
                await queries.ForCandidateAsync(query.EntityId, cancellationToken));
        }

        if (!AuditEntityTypes.All.Contains(query.EntityType))
        {
            return Result<IReadOnlyList<AuditHistoryRow>>.Failure(
                Error.Validation($"{query.EntityType} is not an audited entity type."));
        }

        return Result<IReadOnlyList<AuditHistoryRow>>.Success(
            await queries.ForEntityAsync(query.EntityType, query.EntityId, cancellationToken));
    }
}
`````

## after — src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs — 1/1

<!-- vocabulary-file: {"id":69,"oldPath":"src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs","newPath":"src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs","beforeSha":"18c93fc6d9585914074795f8e3db8dad0cc95c84982697e2d40f8da404774a4a","afterSha":"0b8cc1a5ee88f3a7b126a78b0d6e9826e59dcf1c86a422a464f2ae477ced02d1","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Dashboards;

/// <summary>A null entity type means "this attendee's whole history".</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="EntityType">The entity type.</param>
/// <param name="EntityId">The entity id.</param>
public sealed record GetAuditHistoryQuery(Guid StaffUserId, string? EntityType, Guid EntityId);

/// <summary>Defines get audit history handler for the current use case.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetAuditHistoryHandler(
    IAuditQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<AuditHistoryRow>>> HandleAsync(
        GetAuditHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var capability = query.EntityType == AuditEntityTypes.Event
            ? StaffCapability.ViewEventAudit
            : StaffCapability.ViewAttendeeAudit;
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, capability, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<AuditHistoryRow>>.Failure(authorized.Error);
        }

        if (query.EntityType is null)
        {
            return Result<IReadOnlyList<AuditHistoryRow>>.Success(
                await queries.ForAttendeeAsync(query.EntityId, cancellationToken));
        }

        if (!AuditEntityTypes.All.Contains(query.EntityType))
        {
            return Result<IReadOnlyList<AuditHistoryRow>>.Failure(
                Error.Validation($"{query.EntityType} is not an audited entity type."));
        }

        return Result<IReadOnlyList<AuditHistoryRow>>.Success(
            await queries.ForEntityAsync(query.EntityType, query.EntityId, cancellationToken));
    }
}
`````

## before — src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs — 1/1

<!-- vocabulary-file: {"id":70,"oldPath":"src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs","newPath":"src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs","beforeSha":"96dea0d4ff0618963e31f094d4865637c1e1c12b7ab97132a5011a2c25312a72","afterSha":"b3bd7935c3a543aae4fd77e4ab4a1f4e5717417cec9058eafe7e69258ce4a6c0","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Dashboards;

/// <summary>Cross-cutting audit search request; allowed entity types are derived from capabilities.</summary>
/// <param name="StaffUserId">The staff identity performing the search.</param>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null.</param>
/// <param name="ActorType">Actor type name to match, or null for any.</param>
/// <param name="Action">Audit action name to match, or null for any.</param>
/// <param name="Identifier">Free-text identifier matched exactly against entity id or actor id.</param>
/// <param name="EntityType">Optional single entity type within the caller's allowed bucket.</param>
/// <param name="Cursor">Opaque keyset cursor, or null for the newest page.</param>
/// <param name="PageSize">Rows per page; clamped to 200.</param>
public sealed record GetAuditSearchQuery(
    Guid StaffUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ActorType,
    string? Action,
    string? Identifier,
    string? EntityType,
    string? Cursor,
    int PageSize);

/// <summary>Searches the audit log within the entity-type bucket the caller's capabilities allow.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetAuditSearchHandler(
    IAuditQueries queries,
    IStaffAccessAuthorizer access)
{
    private static readonly IReadOnlyList<string> CandidateBucket =
    [
        AuditEntityTypes.Candidate,
        AuditEntityTypes.Invite,
        AuditEntityTypes.Booking,
        AuditEntityTypes.BookingAppointment
    ];

    private static readonly IReadOnlyList<string> OperationalBucket =
    [
        AuditEntityTypes.SlotProposal,
        AuditEntityTypes.ConfirmedSlot,
        AuditEntityTypes.StaffAccessProfile
    ];

    /// <summary>Computes the allowed bucket and runs the search, refusing out-of-bucket requests.</summary>
    /// <param name="query">The search request.</param>
    /// <param name="cancellationToken">Cancels the authorization and query.</param>
    /// <returns>The result page, or a forbidden failure when the caller may not search.</returns>
    public async Task<Result<AuditSearchPage>> HandleAsync(
        GetAuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var maySeeCandidates = (await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ViewCandidateAudit, null, cancellationToken)).IsSuccess;
        var maySeeOperations = (await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ViewSlotAudit, null, cancellationToken)).IsSuccess;

        if (!maySeeCandidates && !maySeeOperations)
        {
            return Result<AuditSearchPage>.Failure(Error.Forbidden("Search requires audit access."));
        }

        IReadOnlyList<string> allowed = (maySeeCandidates, maySeeOperations) switch
        {
            (true, true) => AuditEntityTypes.All,
            (true, false) => CandidateBucket,
            _ => OperationalBucket,
        };

        if (query.EntityType is not null && !allowed.Contains(query.EntityType))
        {
            return Result<AuditSearchPage>.Failure(
                Error.Forbidden($"{query.EntityType} is outside the caller's audit access."));
        }

        var page = await queries.SearchAsync(
            new AuditSearchFilter(
                query.From, query.To, query.ActorType, query.Action, query.Identifier,
                allowed, query.EntityType, query.Cursor,
                query.PageSize <= 0 ? 50 : Math.Min(query.PageSize, 200)),
            cancellationToken);

        return Result<AuditSearchPage>.Success(page);
    }
}
`````

## after — src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs — 1/1

<!-- vocabulary-file: {"id":70,"oldPath":"src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs","newPath":"src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs","beforeSha":"96dea0d4ff0618963e31f094d4865637c1e1c12b7ab97132a5011a2c25312a72","afterSha":"b3bd7935c3a543aae4fd77e4ab4a1f4e5717417cec9058eafe7e69258ce4a6c0","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Dashboards;

/// <summary>Cross-cutting audit search request; allowed entity types are derived from capabilities.</summary>
/// <param name="StaffUserId">The staff identity performing the search.</param>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null.</param>
/// <param name="ActorType">Actor type name to match, or null for any.</param>
/// <param name="Action">Audit action name to match, or null for any.</param>
/// <param name="Identifier">Free-text identifier matched exactly against entity id or actor id.</param>
/// <param name="EntityType">Optional single entity type within the caller's allowed bucket.</param>
/// <param name="Cursor">Opaque keyset cursor, or null for the newest page.</param>
/// <param name="PageSize">Rows per page; clamped to 200.</param>
public sealed record GetAuditSearchQuery(
    Guid StaffUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ActorType,
    string? Action,
    string? Identifier,
    string? EntityType,
    string? Cursor,
    int PageSize);

/// <summary>Searches the audit log within the entity-type bucket the caller's capabilities allow.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetAuditSearchHandler(
    IAuditQueries queries,
    IStaffAccessAuthorizer access)
{
    private static readonly IReadOnlyList<string> AttendeeBucket =
    [
        AuditEntityTypes.Attendee,
        AuditEntityTypes.Invite,
        AuditEntityTypes.Booking,
        AuditEntityTypes.BookingAppointment
    ];

    private static readonly IReadOnlyList<string> OperationalBucket =
    [
        AuditEntityTypes.EventProposal,
        AuditEntityTypes.Event,
        AuditEntityTypes.StaffAccessProfile
    ];

    /// <summary>Computes the allowed bucket and runs the search, refusing out-of-bucket requests.</summary>
    /// <param name="query">The search request.</param>
    /// <param name="cancellationToken">Cancels the authorization and query.</param>
    /// <returns>The result page, or a forbidden failure when the caller may not search.</returns>
    public async Task<Result<AuditSearchPage>> HandleAsync(
        GetAuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var maySeeAttendees = (await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ViewAttendeeAudit, null, cancellationToken)).IsSuccess;
        var maySeeOperations = (await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ViewEventAudit, null, cancellationToken)).IsSuccess;

        if (!maySeeAttendees && !maySeeOperations)
        {
            return Result<AuditSearchPage>.Failure(Error.Forbidden("Search requires audit access."));
        }

        IReadOnlyList<string> allowed = (maySeeAttendees, maySeeOperations) switch
        {
            (true, true) => AuditEntityTypes.All,
            (true, false) => AttendeeBucket,
            _ => OperationalBucket,
        };

        if (query.EntityType is not null && !allowed.Contains(query.EntityType))
        {
            return Result<AuditSearchPage>.Failure(
                Error.Forbidden($"{query.EntityType} is outside the caller's audit access."));
        }

        var page = await queries.SearchAsync(
            new AuditSearchFilter(
                query.From, query.To, query.ActorType, query.Action, query.Identifier,
                allowed, query.EntityType, query.Cursor,
                query.PageSize <= 0 ? 50 : Math.Min(query.PageSize, 200)),
            cancellationToken);

        return Result<AuditSearchPage>.Success(page);
    }
}
`````

## before — src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs — 1/1

<!-- vocabulary-file: {"id":71,"oldPath":"src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs","newPath":"src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs","beforeSha":"f8b56e20cc90ffa84c1e15e10533b6c88969c3b0d5bded6b295f3c642e22e1d3","afterSha":"9c8aeba21c3d579d0b6f4aec14987c600f2cb6cc6acb19f3f4431d1fe64b4fc8","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Dashboards;

/// <summary>Defines dashboards view for the current use case.</summary>
/// <param name="AwaitingAvailability">The awaiting availability.</param>
/// <param name="NoResponse">The no response.</param>
/// <param name="Slots">The slots.</param>
/// <param name="EmailStatuses">The email statuses.</param>
public sealed record DashboardsView(
    IReadOnlyList<AwaitingAvailabilityRow> AwaitingAvailability,
    IReadOnlyList<NoResponseRow> NoResponse,
    IReadOnlyList<SlotOverviewRow> Slots,
    IReadOnlyList<CandidateEmailStatusView> EmailStatuses);

/// <summary>Staff-facing delivery state for one candidate.</summary>
/// <param name="CandidateId">The candidate whose latest delivery is shown.</param>
/// <param name="TemplateDisplay">Human-readable template name.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether the current domain state still permits retry.</param>
public sealed record CandidateEmailStatusView(
    Guid CandidateId,
    string TemplateDisplay,
    DateTimeOffset SentAt,
    string Status,
    bool CanRetry);

/// <summary>Defines get dashboards query for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
public sealed record GetDashboardsQuery(Guid StaffUserId);

/// <summary>Defines get dashboards handler for the current use case.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetDashboardsHandler(
    IDashboardQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<DashboardsView>> HandleAsync(
        GetDashboardsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ViewCandidateDashboards,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<DashboardsView>.Failure(authorized.Error);
        }

        var emailStatuses = await queries.LatestEmailStatusAsync(cancellationToken);

        return Result<DashboardsView>.Success(new DashboardsView(
            await queries.AwaitingAvailabilityAsync(cancellationToken),
            await queries.NoResponseAsync(cancellationToken),
            await queries.SlotsOverviewAsync(cancellationToken),
            emailStatuses
                .Select(e => new CandidateEmailStatusView(
                    e.CandidateId,
                    TemplateDisplayOf(e.TemplateName),
                    e.SentAt,
                    e.Status.ToString(),
                    e.CanRetry))
                .ToList()));
    }

    /// <summary>Area H's template names, in the wording a coordinator reads on /candidates and /dashboards.</summary>
    /// <param name="template">The template.</param>
    public static string TemplateDisplayOf(EmailTemplate template) => template switch
    {
        EmailTemplate.CandidateInvite => "Invite",
        EmailTemplate.BookingConfirmation => "Booking confirmation",
        EmailTemplate.SlotCancelledRebookingNeeded => "Slot cancelled - rebooking needed",
        EmailTemplate.CandidateReinvite => "Re-invite",
        _ => template.ToString(),
    };
}
`````

## after — src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs — 1/1

<!-- vocabulary-file: {"id":71,"oldPath":"src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs","newPath":"src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs","beforeSha":"f8b56e20cc90ffa84c1e15e10533b6c88969c3b0d5bded6b295f3c642e22e1d3","afterSha":"9c8aeba21c3d579d0b6f4aec14987c600f2cb6cc6acb19f3f4431d1fe64b4fc8","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Dashboards;

/// <summary>Defines dashboards view for the current use case.</summary>
/// <param name="AwaitingAvailability">The awaiting availability.</param>
/// <param name="NoResponse">The no response.</param>
/// <param name="Events">The events.</param>
/// <param name="EmailStatuses">The email statuses.</param>
public sealed record DashboardsView(
    IReadOnlyList<AwaitingAvailabilityRow> AwaitingAvailability,
    IReadOnlyList<NoResponseRow> NoResponse,
    IReadOnlyList<EventOverviewRow> Events,
    IReadOnlyList<AttendeeEmailStatusView> EmailStatuses);

/// <summary>Staff-facing delivery state for one attendee.</summary>
/// <param name="AttendeeId">The attendee whose latest delivery is shown.</param>
/// <param name="TemplateDisplay">Human-readable template name.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether the current domain state still permits retry.</param>
public sealed record AttendeeEmailStatusView(
    Guid AttendeeId,
    string TemplateDisplay,
    DateTimeOffset SentAt,
    string Status,
    bool CanRetry);

/// <summary>Defines get dashboards query for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
public sealed record GetDashboardsQuery(Guid StaffUserId);

/// <summary>Defines get dashboards handler for the current use case.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetDashboardsHandler(
    IDashboardQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<DashboardsView>> HandleAsync(
        GetDashboardsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ViewAttendeeDashboards,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<DashboardsView>.Failure(authorized.Error);
        }

        var emailStatuses = await queries.LatestEmailStatusAsync(cancellationToken);

        return Result<DashboardsView>.Success(new DashboardsView(
            await queries.AwaitingAvailabilityAsync(cancellationToken),
            await queries.NoResponseAsync(cancellationToken),
            await queries.EventsOverviewAsync(cancellationToken),
            emailStatuses
                .Select(e => new AttendeeEmailStatusView(
                    e.AttendeeId,
                    TemplateDisplayOf(e.TemplateName),
                    e.SentAt,
                    e.Status.ToString(),
                    e.CanRetry))
                .ToList()));
    }

    /// <summary>Area H's template names, in the wording a coordinator reads on /attendees and /dashboards.</summary>
    /// <param name="template">The template.</param>
    public static string TemplateDisplayOf(EmailTemplate template) => template switch
    {
        EmailTemplate.AttendeeInvite => "Invite",
        EmailTemplate.BookingConfirmation => "Booking confirmation",
        EmailTemplate.EventCancelledRebookingNeeded => "Event cancelled - rebooking needed",
        EmailTemplate.AttendeeReinvite => "Re-invite",
        _ => template.ToString(),
    };
}
`````

## before — src/EventBooking.Application/Dashboards/GetSlotOperationsHandler.cs — 1/1

<!-- vocabulary-file: {"id":72,"oldPath":"src/EventBooking.Application/Dashboards/GetSlotOperationsHandler.cs","newPath":"src/EventBooking.Application/Dashboards/GetEventOperationsHandler.cs","beforeSha":"f8d497386b7d85d6daf16de1697558c30bf834ca491989f965491269015e256e","afterSha":"3670c6f19e75ae3a7dae1762ea4db6ab4dd61274df2681b9a74377db1db9cb2a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Dashboards;

/// <summary>Slot-only view of every cancellable confirmed window, free of candidate data.</summary>
/// <param name="Slots">One row per confirmed slot with per-appointment-type capacity and aggregate booking count.</param>
public sealed record SlotOperationsView(IReadOnlyList<SlotOverviewRow> Slots);

/// <summary>Query carrying the caller's staff identity for the slot-only operations view.</summary>
/// <param name="StaffUserId">The authenticated staff identity making the request.</param>
public sealed record GetSlotOperationsQuery(Guid StaffUserId);

/// <summary>Returns the slot-only operations view after a view-slot-operations check.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetSlotOperationsHandler(
    IDashboardQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Authorizes the caller then returns every cancellable slot-overview row.</summary>
    /// <param name="query">The query carrying the caller's staff identity.</param>
    /// <param name="cancellationToken">Propagated to the authorizer and queries.</param>
    /// <returns>The slot-only view, or the authorizer's forbidden failure.</returns>
    public async Task<Result<SlotOperationsView>> HandleAsync(
        GetSlotOperationsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ViewSlotOperations,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<SlotOperationsView>.Failure(authorized.Error);
        }

        var slots = await queries.SlotsOverviewAsync(cancellationToken);
        return Result<SlotOperationsView>.Success(new SlotOperationsView(slots));
    }
}
`````

## after — src/EventBooking.Application/Dashboards/GetEventOperationsHandler.cs — 1/1

<!-- vocabulary-file: {"id":72,"oldPath":"src/EventBooking.Application/Dashboards/GetSlotOperationsHandler.cs","newPath":"src/EventBooking.Application/Dashboards/GetEventOperationsHandler.cs","beforeSha":"f8d497386b7d85d6daf16de1697558c30bf834ca491989f965491269015e256e","afterSha":"3670c6f19e75ae3a7dae1762ea4db6ab4dd61274df2681b9a74377db1db9cb2a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Dashboards;

/// <summary>Event-only view of every cancellable confirmed window, free of attendee data.</summary>
/// <param name="Events">One row per event with per-appointment-type capacity and aggregate booking count.</param>
public sealed record EventOperationsView(IReadOnlyList<EventOverviewRow> Events);

/// <summary>Query carrying the caller's staff identity for the event-only operations view.</summary>
/// <param name="StaffUserId">The authenticated staff identity making the request.</param>
public sealed record GetEventOperationsQuery(Guid StaffUserId);

/// <summary>Returns the event-only operations view after a view-event-operations check.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetEventOperationsHandler(
    IDashboardQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Authorizes the caller then returns every cancellable event-overview row.</summary>
    /// <param name="query">The query carrying the caller's staff identity.</param>
    /// <param name="cancellationToken">Propagated to the authorizer and queries.</param>
    /// <returns>The event-only view, or the authorizer's forbidden failure.</returns>
    public async Task<Result<EventOperationsView>> HandleAsync(
        GetEventOperationsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ViewEventOperations,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<EventOperationsView>.Failure(authorized.Error);
        }

        var events = await queries.EventsOverviewAsync(cancellationToken);
        return Result<EventOperationsView>.Success(new EventOperationsView(events));
    }
}
`````

## before — src/EventBooking.Application/DependencyInjection.cs — 1/1

<!-- vocabulary-file: {"id":73,"oldPath":"src/EventBooking.Application/DependencyInjection.cs","newPath":"src/EventBooking.Application/DependencyInjection.cs","beforeSha":"ba995637fc954d038d4ab49f975099d11a7458b9a42ceeea1bd6b10fa0450da7","afterSha":"086f5950790fdcd0e286802031d2097a72038d0e9fca76bf26b271f375bff76e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Bookings;
using EventBooking.Application.Candidates;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Settings;
using EventBooking.Application.Slots;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Application;

/// <summary>Defines application service collection extensions for the current use case.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Defines add event booking application for the current use case.</summary>
    /// <param name="services">The services.</param>
    /// <param name="portal">The portal.</param>
    public static IServiceCollection AddEventBookingApplication(
        this IServiceCollection services,
        CandidatePortalOptions portal)
    {
        services.AddSingleton(portal);

        // Shared services.
        services.AddScoped<EligibleSlotFinder>();
        services.AddScoped<EmailDeliveryService>();
        services.AddScoped<InviteIssuer>();
        services.AddScoped<BookingCanceller>();
        services.AddScoped<IStaffAccessAuthorizer, StaffAccessAuthorizer>();
        services.AddScoped<StaffAccessHandler>();

        // Slot negotiation.
        services.AddScoped<ProposeSlotHandler>();
        services.AddScoped<AcceptProposalHandler>();
        services.AddScoped<ImportConfirmedSlotsHandler>();
        services.AddScoped<WithdrawAcceptanceHandler>();
        services.AddScoped<WithdrawProposalHandler>();
        services.AddScoped<GetManagerSlotBoardHandler>();
        services.AddScoped<CancelConfirmedSlotHandler>();
        services.AddScoped<AdjustConfirmedSlotCapacityHandler>();

        // Candidates.
        services.AddScoped<ImportCandidatesHandler>();
        services.AddScoped<SaveCandidateHandler>();
        services.AddScoped<DeleteCandidateHandler>();
        services.AddScoped<ListCandidatesHandler>();
        services.AddScoped<ListEmployeeGroupsHandler>();
        services.AddScoped<CandidateReadinessCalculator>();
        services.AddScoped<GetCandidateReadinessHandler>();
        services.AddScoped<GetCandidateBookingsHandler>();
        services.AddScoped<GetDashboardsHandler>();
        services.AddScoped<GetSlotOperationsHandler>();
        services.AddScoped<GetAuditHistoryHandler>();
        services.AddScoped<GetAuditSearchHandler>();

        // Administration.
        services.AddScoped<AdminSettingsHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<SyncStaffAccessProfileRolesHandler>();

        // Appointments.
        services.AddScoped<GetAppointmentWorkspaceHandler>();
        services.AddScoped<AppointmentRosterCsvFormatter>();
        services.AddScoped<RecoveryBookingOutcomeCoordinator>();
        services.AddScoped<UpdateBookingAppointmentStatusHandler>();

        // Invites and bookings.
        services.AddScoped<TriggerInviteHandler>();
        services.AddScoped<StartRecoveryHandler>();
        services.AddScoped<CancelRecoveryInviteHandler>();
        services.AddScoped<RetryEmailHandler>();
        services.AddScoped<ExpireInvitesHandler>();
        services.AddScoped<ViewInviteHandler>();
        services.AddScoped<ViewBookingHandler>();
        services.AddScoped<ConfirmBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<CancelCandidateBookingHandler>();

        return services;
    }
}
`````

## after — src/EventBooking.Application/DependencyInjection.cs — 1/1

<!-- vocabulary-file: {"id":73,"oldPath":"src/EventBooking.Application/DependencyInjection.cs","newPath":"src/EventBooking.Application/DependencyInjection.cs","beforeSha":"ba995637fc954d038d4ab49f975099d11a7458b9a42ceeea1bd6b10fa0450da7","afterSha":"086f5950790fdcd0e286802031d2097a72038d0e9fca76bf26b271f375bff76e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Settings;
using EventBooking.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Application;

/// <summary>Defines application service collection extensions for the current use case.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Defines add event booking application for the current use case.</summary>
    /// <param name="services">The services.</param>
    /// <param name="portal">The portal.</param>
    public static IServiceCollection AddEventBookingApplication(
        this IServiceCollection services,
        AttendeePortalOptions portal)
    {
        services.AddSingleton(portal);

        // Shared services.
        services.AddScoped<EligibleEventFinder>();
        services.AddScoped<EmailDeliveryService>();
        services.AddScoped<InviteIssuer>();
        services.AddScoped<BookingCanceller>();
        services.AddScoped<IStaffAccessAuthorizer, StaffAccessAuthorizer>();
        services.AddScoped<StaffAccessHandler>();

        // Event negotiation.
        services.AddScoped<ProposeEventHandler>();
        services.AddScoped<AcceptProposalHandler>();
        services.AddScoped<ImportEventsHandler>();
        services.AddScoped<WithdrawAcceptanceHandler>();
        services.AddScoped<WithdrawProposalHandler>();
        services.AddScoped<GetManagerEventBoardHandler>();
        services.AddScoped<CancelEventHandler>();
        services.AddScoped<AdjustEventCapacityHandler>();

        // Attendees.
        services.AddScoped<ImportAttendeesHandler>();
        services.AddScoped<SaveAttendeeHandler>();
        services.AddScoped<DeleteAttendeeHandler>();
        services.AddScoped<ListAttendeesHandler>();
        services.AddScoped<ListAttendeeGroupsHandler>();
        services.AddScoped<AttendeeReadinessCalculator>();
        services.AddScoped<GetAttendeeReadinessHandler>();
        services.AddScoped<GetAttendeeBookingsHandler>();
        services.AddScoped<GetDashboardsHandler>();
        services.AddScoped<GetEventOperationsHandler>();
        services.AddScoped<GetAuditHistoryHandler>();
        services.AddScoped<GetAuditSearchHandler>();

        // Administration.
        services.AddScoped<AdminSettingsHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<SyncStaffAccessProfileRolesHandler>();

        // Appointments.
        services.AddScoped<GetAppointmentWorkspaceHandler>();
        services.AddScoped<AppointmentRosterCsvFormatter>();
        services.AddScoped<RecoveryBookingOutcomeCoordinator>();
        services.AddScoped<UpdateBookingAppointmentStatusHandler>();

        // Invites and bookings.
        services.AddScoped<TriggerInviteHandler>();
        services.AddScoped<StartRecoveryHandler>();
        services.AddScoped<CancelRecoveryInviteHandler>();
        services.AddScoped<RetryEmailHandler>();
        services.AddScoped<ExpireInvitesHandler>();
        services.AddScoped<ViewInviteHandler>();
        services.AddScoped<ViewBookingHandler>();
        services.AddScoped<ConfirmBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<CancelAttendeeBookingHandler>();

        return services;
    }
}
`````

## before — src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs — 1/1

<!-- vocabulary-file: {"id":74,"oldPath":"src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs","newPath":"src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs","beforeSha":"9733af18cec087533b1c8960d65ecce810f990710df081fcaf856bf1da707947","afterSha":"e2dbb458e31f1845935dcc751092faebcb638ec707a03fde002ed9d0197309a5","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Invites;

/// <summary>Cancels one pending recovery Invite without touching capacity or appointments.</summary>
/// <param name="StaffUserId">The Coordinator cancelling the recovery Invite.</param>
/// <param name="CandidateId">The candidate route the Invite must belong to.</param>
/// <param name="InviteId">The pending recovery Invite to cancel.</param>
public sealed record CancelRecoveryInviteCommand(Guid StaffUserId, Guid CandidateId, Guid InviteId);

/// <summary>Cancels one Pending recovery Invite without changing capacity or Candidate status.</summary>
/// <param name="candidates">The candidates.</param>
/// <param name="access">The access.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelRecoveryInviteHandler(
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    IInviteRepository invites,
    IBookingRepository bookings,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels one Pending recovery Invite without changing capacity or Candidate status.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        CancelRecoveryInviteCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such candidate."));
        }

        var invite = await invites.LockForUpdateAsync(command.InviteId, cancellationToken);
        if (invite is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such invite."));
        }

        if (invite.RecoveryOfBookingId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("Only a recovery invite can be cancelled."));
        }

        if (invite.CandidateId != command.CandidateId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this candidate."));
        }

        var root = await bookings.LockForUpdateAsync(invite.RecoveryOfBookingId.Value, cancellationToken);
        if (root is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("The original booking no longer exists."));
        }

        if (root.CandidateId != command.CandidateId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this candidate."));
        }

        if (invite.Status != InviteStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This recovery invite is no longer pending."));
        }

        try
        {
            invite.RotateTokenHash(Guid.NewGuid().ToString("N"));
            invite.CancelRecovery();
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.Invite,
            invite.Id,
            AuditAction.RecoveryInviteCancelled,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            $"root {root.Id}");

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result.Success();
    }
}
`````
