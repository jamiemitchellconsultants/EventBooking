# 01e — Location-restricted invites and closed attendee transitions, edits 1 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs","beforeSha":"e96097fa8358bc6e181cc618a2e93dcf2e6b0c3b4a79295a0de7ae8ada8b773f","afterSha":"f345d1a12c41c8253aa20b10b9beee5f90911fe285430bce8ba0e49d2d493e17","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Attendees;

/// <summary>Requests a bulk Attendee import from Attendee Group CSV content.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="CsvContent">The csv content.</param>
public sealed record ImportAttendeesCommand(Guid StaffUserId, string? CsvContent);

/// <summary>
/// Accepted is false when the file was rejected. The result itself is still a success — a rejected
/// upload is a normal outcome with a list of row errors, not a failed request.
/// </summary>
/// <param name="Accepted">The accepted.</param>
/// <param name="ImportedCount">The imported count.</param>
/// <param name="Errors">The errors.</param>
public sealed record AttendeeImportOutcome(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<AttendeeCsvError> Errors);

/// <summary>Imports attendees whose requirements derive from one Attendee Group per row.</summary>
/// <param name="attendees">Persists attendee rows.</param>
/// <param name="groups">Resolves row Attendee Group codes.</param>
/// <param name="access">Authorizes attendee management.</param>
/// <param name="unitOfWork">Owns the attendee save.</param>
public sealed class ImportAttendeesHandler(
    IAttendeeRepository attendees,
    IAttendeeGroupRepository groups,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork)
{
    /// <summary>Validates every row before persisting any Attendee.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AttendeeImportOutcome>> HandleAsync(
        ImportAttendeesCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AttendeeImportOutcome>.Failure(authorized.Error);
        }

        var parsed = AttendeeCsvParser.Parse(command.CsvContent);
        var errors = parsed.Errors.ToList();

        // Build every attendee first, collecting failures. Nothing is added to the repository
        // until the whole file is known to be good.
        var built = new List<Attendee>();

        foreach (var row in parsed.Rows)
        {
            var existing = await attendees.GetByEmailAsync(row.Email, cancellationToken);
            if (existing is not null)
            {
                errors.Add(new AttendeeCsvError(row.LineNumber, $"{row.Email} is already a attendee."));
                continue;
            }

            var group = await groups.GetByCodeAsync(row.AttendeeGroupCode, cancellationToken);
            if (group is null)
            {
                errors.Add(new AttendeeCsvError(
                    row.LineNumber, $"{row.AttendeeGroupCode} is not a known attendee group code."));
                continue;
            }

            if (!group.IsActive)
            {
                errors.Add(new AttendeeCsvError(
                    row.LineNumber, $"{row.AttendeeGroupCode} is not an active attendee group."));
                continue;
            }

            if (group.RequiredAppointmentTypeIds.Count == 0)
            {
                errors.Add(new AttendeeCsvError(
                    row.LineNumber, $"{row.AttendeeGroupCode} has no mapped appointment types."));
                continue;
            }

            try
            {
                built.Add(Attendee.Create(Guid.NewGuid(), row.Name, row.Email, group));
            }
            catch (DomainException ex)
            {
                errors.Add(new AttendeeCsvError(row.LineNumber, ex.Message));
            }
        }

        if (errors.Count > 0)
        {
            return Result<AttendeeImportOutcome>.Success(
                new AttendeeImportOutcome(false, 0, errors.OrderBy(e => e.LineNumber).ToList()));
        }

        foreach (var attendee in built)
        {
            attendees.Add(attendee);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AttendeeImportOutcome>.Success(
            new AttendeeImportOutcome(true, built.Count, []));
    }
}
`````

## after — src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs","beforeSha":"e96097fa8358bc6e181cc618a2e93dcf2e6b0c3b4a79295a0de7ae8ada8b773f","afterSha":"f345d1a12c41c8253aa20b10b9beee5f90911fe285430bce8ba0e49d2d493e17","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Attendees;

/// <summary>Requests a bulk Attendee import from Attendee Group CSV content.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="CsvContent">The csv content.</param>
public sealed record ImportAttendeesCommand(Guid StaffUserId, string? CsvContent);

/// <summary>
/// Accepted is false when the file was rejected. The result itself is still a success — a rejected
/// upload is a normal outcome with a list of row errors, not a failed request.
/// </summary>
/// <param name="Accepted">The accepted.</param>
/// <param name="ImportedCount">The imported count.</param>
/// <param name="Errors">The errors.</param>
public sealed record AttendeeImportOutcome(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<AttendeeCsvError> Errors);

/// <summary>Imports attendees whose requirements derive from one Attendee Group per row.</summary>
/// <param name="attendees">Persists attendee rows.</param>
/// <param name="groups">Resolves row Attendee Group codes.</param>
/// <param name="access">Authorizes attendee management.</param>
/// <param name="clock">Stamps each new attendee's status.</param>
/// <param name="unitOfWork">Owns the attendee save.</param>
public sealed class ImportAttendeesHandler(
    IAttendeeRepository attendees,
    IAttendeeGroupRepository groups,
    IStaffAccessAuthorizer access,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>Validates every row before persisting any Attendee.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AttendeeImportOutcome>> HandleAsync(
        ImportAttendeesCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AttendeeImportOutcome>.Failure(authorized.Error);
        }

        var parsed = AttendeeCsvParser.Parse(command.CsvContent);
        var errors = parsed.Errors.ToList();

        // Build every attendee first, collecting failures. Nothing is added to the repository
        // until the whole file is known to be good.
        var built = new List<Attendee>();

        foreach (var row in parsed.Rows)
        {
            var existing = await attendees.GetByEmailAsync(row.Email, cancellationToken);
            if (existing is not null)
            {
                errors.Add(new AttendeeCsvError(row.LineNumber, $"{row.Email} is already a attendee."));
                continue;
            }

            var group = await groups.GetByCodeAsync(row.AttendeeGroupCode, cancellationToken);
            if (group is null)
            {
                errors.Add(new AttendeeCsvError(
                    row.LineNumber, $"{row.AttendeeGroupCode} is not a known attendee group code."));
                continue;
            }

            if (!group.IsActive)
            {
                errors.Add(new AttendeeCsvError(
                    row.LineNumber, $"{row.AttendeeGroupCode} is not an active attendee group."));
                continue;
            }

            if (group.RequiredAppointmentTypeIds.Count == 0)
            {
                errors.Add(new AttendeeCsvError(
                    row.LineNumber, $"{row.AttendeeGroupCode} has no mapped appointment types."));
                continue;
            }

            try
            {
                built.Add(Attendee.Create(Guid.NewGuid(), row.Name, row.Email, group, clock.UtcNow));
            }
            catch (DomainException ex)
            {
                errors.Add(new AttendeeCsvError(row.LineNumber, ex.Message));
            }
        }

        if (errors.Count > 0)
        {
            return Result<AttendeeImportOutcome>.Success(
                new AttendeeImportOutcome(false, 0, errors.OrderBy(e => e.LineNumber).ToList()));
        }

        foreach (var attendee in built)
        {
            attendees.Add(attendee);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AttendeeImportOutcome>.Success(
            new AttendeeImportOutcome(true, built.Count, []));
    }
}
`````

## before — src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs","beforeSha":"31345607e76e4112c64dce27ed7c303250bb8feedf849f2257a7f65ea54aad89","afterSha":"adf6884779af045901b15746418db4dc8f92c13dd53b1de77df946361817f5da","side":"before","part":1,"parts":1} -->

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

        var oldGroupCode = (await _groups.GetAsync(attendee.AttendeeGroupId, cancellationToken))?.Code;

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

## after — src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs","beforeSha":"31345607e76e4112c64dce27ed7c303250bb8feedf849f2257a7f65ea54aad89","afterSha":"adf6884779af045901b15746418db4dc8f92c13dd53b1de77df946361817f5da","side":"after","part":1,"parts":1} -->

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
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Creates handler dependencies for group-derived attendee management.</summary>
    /// <param name="attendees">Persists attendee rows.</param>
    /// <param name="groups">Resolves the assigned Attendee Group.</param>
    /// <param name="invites">Locks the attendee's pending invite for lifecycle work.</param>
    /// <param name="bookings">Locks the attendee's active booking for the requirement invariant.</param>
    /// <param name="access">Authorizes attendee management.</param>
    /// <param name="audit">Records Attendee Group assignment.</param>
    /// <param name="clock">Stamps the attendee's status changes.</param>
    /// <param name="unitOfWork">Owns the attendee save.</param>
    public SaveAttendeeHandler(
        IAttendeeRepository attendees,
        IAttendeeGroupRepository groups,
        IInviteRepository invites,
        IBookingRepository bookings,
        IStaffAccessAuthorizer access,
        IAuditLogger audit,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _attendees = attendees;
        _groups = groups;
        _invites = invites;
        _bookings = bookings;
        _access = access;
        _audit = audit;
        _clock = clock;
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
            attendee = Attendee.Create(id, command.Name, command.Email, resolved.Value, _clock.UtcNow);
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

        var oldGroupCode = (await _groups.GetAsync(attendee.AttendeeGroupId, cancellationToken))?.Code;

        var oldRequirementCodes = RequirementCodes(attendee.RequiredAppointmentTypeIds);
        var newRequirementCodes = RequirementCodes(resolved.Value.RequiredAppointmentTypeIds);

        try
        {
            attendee.UpdateDetails(command.Name, command.Email);

            if (setChanged)
            {
                pendingInvite?.MarkSuperseded();
                attendee.ResetAfterRequirementChange(_clock.UtcNow);
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

## before — src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs","beforeSha":"3f12a48e14049a0bcd029180acd43fc71e25fee1a91f1d77c3d5efba889072b6","afterSha":"fe369bb993f1343304bdfcda5fe531d71232d83026c19dfaf1060c90a788cdba","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Requests staff cancellation of one attendee's booking.</summary>
/// <param name="StaffUserId">The coordinator performing the cancellation; recorded as the audit actor.</param>
/// <param name="AttendeeId">The attendee the booking must belong to.</param>
/// <param name="BookingId">The booking to cancel.</param>
/// <param name="Rebook">
/// Whether to issue a replacement invite. Applies only to an original booking; rebooking a
/// cancelled recovery booking is the recovery path's job and is refused here.
/// </param>
public sealed record CancelAttendeeBookingCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    Guid BookingId,
    bool Rebook);

/// <summary>
/// Cancels one attendee booking on a coordinator's behalf, reusing the attendee self-service
/// cancellation path so capacity release, recovery cascade, and audit semantics stay identical.
/// </summary>
/// <param name="access">Authorizes attendee management before anything is read or locked.</param>
/// <param name="bookings">Locks and re-reads the targeted booking and any active recovery.</param>
/// <param name="events">Locks every event whose capacity is released.</param>
/// <param name="attendees">Locks the attendee lifecycle root.</param>
/// <param name="invites">Locks pending invites so recovery invites can be superseded.</param>
/// <param name="bookingCanceller">Performs the cancellation, capacity release, and audit write.</param>
/// <param name="issuer">Issues the optional replacement invite.</param>
/// <param name="deliveries">Dispatches a staged replacement invite after commit.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="clock">The clock.</param>
public sealed class CancelAttendeeBookingHandler(
    IStaffAccessAuthorizer access,
    IBookingRepository bookings,
    IEventRepository events,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private const string NoSuchBooking = "No such active booking for this attendee.";

    /// <summary>
    /// Cancels the targeted booking under the attendee lifecycle lock order, cascading onto an
    /// active recovery when the target is the original, and optionally re-inviting the attendee.
    /// </summary>
    /// <param name="command">The coordinator's cancellation request.</param>
    /// <param name="cancellationToken">Cancels the authorization, locks, and dispatch.</param>
    /// <returns>
    /// The cancellation outcome, a forbidden failure when the caller lacks attendee management,
    /// a not-found failure for an unknown or already-inactive booking, or a conflict when a
    /// replacement invite is requested for a recovery booking.
    /// </returns>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelAttendeeBookingCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelBookingOutcome>.Failure(authorized.Error);
        }

        var actorId = command.StaffUserId.ToString();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
        }

        var pending = await invites.LockPendingListForAttendeeAsync(attendee.Id, cancellationToken);

        var booking = await bookings.LockByIdForAttendeeAsync(
            command.BookingId, attendee.Id, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.Active)
        {
            return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
        }

        if (!booking.IsOriginal && command.Rebook)
        {
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "A recovery booking cannot be cancelled and rebooked. "
                + "Cancel it, then arrange the missed appointments again."));
        }

        Booking? activeRecovery = null;
        if (booking.IsOriginal)
        {
            activeRecovery = await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken);
        }

        var eventIds = new List<Guid> { booking.EventId };
        if (activeRecovery is not null && activeRecovery.EventId != booking.EventId)
        {
            eventIds.Add(activeRecovery.EventId);
        }

        eventIds.Sort();
        var lockedEvents = new Dictionary<Guid, Event>();
        foreach (var eventId in eventIds)
        {
            var locked = await events.LockForUpdateAsync(eventId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
            }

            lockedEvents[eventId] = locked;
        }

        var eventItem = lockedEvents[booking.EventId];

        // A booking can no longer be cancelled once its event date has started.
        if (lockedEvents.Values.Any(locked => locked.Window.Date < clock.TodayAtTransitionalLocation))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        var reinvited = false;
        InviteIssueResult? issued = null;

        try
        {
            if (!booking.IsOriginal)
            {
                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (releasedRecovery.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(releasedRecovery.Error);
                }
            }
            else
            {
                foreach (var pendingRecovery in pending.Where(invite => invite.RecoveryOfBookingId.HasValue && invite.Status == Domain.Invites.InviteStatus.Pending))
                {
                    pendingRecovery.MarkSuperseded();
                }

                if (activeRecovery is not null)
                {
                    var releasedActiveRecovery = await bookingCanceller.CancelLockedAsync(
                        activeRecovery,
                        lockedEvents[activeRecovery.EventId],
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (releasedActiveRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(releasedActiveRecovery.Error);
                    }
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(released.Error);
                }

                attendee.ResetToNotYetInvited();

                if (command.Rebook)
                {
                    var issueResult = await issuer.IssueInitialAsync(
                        attendee,
                        0,
                        ActorType.Staff,
                        actorId,
                        isReinvite: false,
                        cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(issueResult.Error);
                    }

                    issued = issueResult.Value;
                    reinvited = issued.Invited;
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(ex.Message));
        }

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

        if (issued?.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            return Result<CancelBookingOutcome>.Success(
                new CancelBookingOutcome(
                    reinvited,
                    InviteCreated: reinvited,
                    status.ToString(),
                    plan.DeliveryId));
        }

        return Result<CancelBookingOutcome>.Success(
            new CancelBookingOutcome(reinvited, InviteCreated: reinvited, "Unavailable"));
    }
}
`````

## after — src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs","beforeSha":"3f12a48e14049a0bcd029180acd43fc71e25fee1a91f1d77c3d5efba889072b6","afterSha":"fe369bb993f1343304bdfcda5fe531d71232d83026c19dfaf1060c90a788cdba","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Requests staff cancellation of one attendee's booking.</summary>
/// <param name="StaffUserId">The coordinator performing the cancellation; recorded as the audit actor.</param>
/// <param name="AttendeeId">The attendee the booking must belong to.</param>
/// <param name="BookingId">The booking to cancel.</param>
/// <param name="Rebook">
/// Whether to issue a replacement invite. Applies only to an original booking; rebooking a
/// cancelled recovery booking is the recovery path's job and is refused here.
/// </param>
public sealed record CancelAttendeeBookingCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    Guid BookingId,
    bool Rebook);

/// <summary>
/// Cancels one attendee booking on a coordinator's behalf, reusing the attendee self-service
/// cancellation path so capacity release, recovery cascade, and audit semantics stay identical.
/// </summary>
/// <param name="access">Authorizes attendee management before anything is read or locked.</param>
/// <param name="bookings">Locks and re-reads the targeted booking and any active recovery.</param>
/// <param name="events">Locks every event whose capacity is released.</param>
/// <param name="attendees">Locks the attendee lifecycle root.</param>
/// <param name="invites">Locks pending invites so recovery invites can be superseded.</param>
/// <param name="bookingCanceller">Performs the cancellation, capacity release, and audit write.</param>
/// <param name="issuer">Issues the optional replacement invite.</param>
/// <param name="deliveries">Dispatches a staged replacement invite after commit.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="clock">The clock.</param>
public sealed class CancelAttendeeBookingHandler(
    IStaffAccessAuthorizer access,
    IBookingRepository bookings,
    IEventRepository events,
    IAttendeeRepository attendees,
    IInviteRepository invites,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private const string NoSuchBooking = "No such active booking for this attendee.";

    /// <summary>
    /// Cancels the targeted booking under the attendee lifecycle lock order, cascading onto an
    /// active recovery when the target is the original, and optionally re-inviting the attendee.
    /// </summary>
    /// <param name="command">The coordinator's cancellation request.</param>
    /// <param name="cancellationToken">Cancels the authorization, locks, and dispatch.</param>
    /// <returns>
    /// The cancellation outcome, a forbidden failure when the caller lacks attendee management,
    /// a not-found failure for an unknown or already-inactive booking, or a conflict when a
    /// replacement invite is requested for a recovery booking.
    /// </returns>
    public async Task<Result<CancelBookingOutcome>> HandleAsync(
        CancelAttendeeBookingCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelBookingOutcome>.Failure(authorized.Error);
        }

        var actorId = command.StaffUserId.ToString();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
        }

        var pending = await invites.LockPendingListForAttendeeAsync(attendee.Id, cancellationToken);

        var booking = await bookings.LockByIdForAttendeeAsync(
            command.BookingId, attendee.Id, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.Active)
        {
            return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
        }

        if (!booking.IsOriginal && command.Rebook)
        {
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "A recovery booking cannot be cancelled and rebooked. "
                + "Cancel it, then arrange the missed appointments again."));
        }

        Booking? activeRecovery = null;
        if (booking.IsOriginal)
        {
            activeRecovery = await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken);
        }

        var eventIds = new List<Guid> { booking.EventId };
        if (activeRecovery is not null && activeRecovery.EventId != booking.EventId)
        {
            eventIds.Add(activeRecovery.EventId);
        }

        eventIds.Sort();
        var lockedEvents = new Dictionary<Guid, Event>();
        foreach (var eventId in eventIds)
        {
            var locked = await events.LockForUpdateAsync(eventId, cancellationToken);
            if (locked is null)
            {
                return Result<CancelBookingOutcome>.Failure(Error.NotFound(NoSuchBooking));
            }

            lockedEvents[eventId] = locked;
        }

        var eventItem = lockedEvents[booking.EventId];

        // A booking can no longer be cancelled once its event date has started.
        if (lockedEvents.Values.Any(locked => locked.Window.Date < clock.TodayAtTransitionalLocation))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        var reinvited = false;
        InviteIssueResult? issued = null;

        try
        {
            if (!booking.IsOriginal)
            {
                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (releasedRecovery.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(releasedRecovery.Error);
                }
            }
            else
            {
                foreach (var pendingRecovery in pending.Where(invite => invite.RecoveryOfBookingId.HasValue && invite.Status == Domain.Invites.InviteStatus.Pending))
                {
                    pendingRecovery.MarkSuperseded();
                }

                if (activeRecovery is not null)
                {
                    var releasedActiveRecovery = await bookingCanceller.CancelLockedAsync(
                        activeRecovery,
                        lockedEvents[activeRecovery.EventId],
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (releasedActiveRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(releasedActiveRecovery.Error);
                    }
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<CancelBookingOutcome>.Failure(released.Error);
                }

                attendee.ResetToNotYetInvited(clock.UtcNow);

                if (command.Rebook)
                {
                    var issueResult = await issuer.IssueInitialAsync(
                        attendee,
                        0,
                        ActorType.Staff,
                        actorId,
                        isReinvite: false,
                        cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelBookingOutcome>.Failure(issueResult.Error);
                    }

                    issued = issueResult.Value;
                    reinvited = issued.Invited;
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelBookingOutcome>.Failure(Error.Conflict(ex.Message));
        }

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

        if (issued?.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            return Result<CancelBookingOutcome>.Success(
                new CancelBookingOutcome(
                    reinvited,
                    InviteCreated: reinvited,
                    status.ToString(),
                    plan.DeliveryId));
        }

        return Result<CancelBookingOutcome>.Success(
            new CancelBookingOutcome(reinvited, InviteCreated: reinvited, "Unavailable"));
    }
}
`````
