using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Candidates;

/// <summary>Requests creation of a Candidate in one Employee Group.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="EmployeeGroupId">The employee group id.</param>
public sealed record CreateCandidateCommand(
    Guid StaffUserId,
    string? Name,
    string? Email,
    Guid? EmployeeGroupId);

/// <summary>Requests detail and Employee Group changes for one Candidate.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="CandidateId">The candidate id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="EmployeeGroupId">The employee group id.</param>
public sealed record UpdateCandidateCommand(
    Guid StaffUserId,
    Guid CandidateId,
    string? Name,
    string? Email,
    Guid? EmployeeGroupId);

/// <summary>
/// Creates and updates candidates from one active Employee Group, serializing group edits with
/// the Candidate lifecycle under one Candidate-first lock order.
/// </summary>
public sealed class SaveCandidateHandler
{
    private static readonly JsonSerializerOptions AuditJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ICandidateRepository _candidates;
    private readonly IEmployeeGroupRepository _groups;
    private readonly IInviteRepository _invites;
    private readonly IBookingRepository _bookings;
    private readonly IStaffAccessAuthorizer _access;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Creates handler dependencies for group-derived candidate management.</summary>
    /// <param name="candidates">Persists candidate rows.</param>
    /// <param name="groups">Resolves the assigned Employee Group.</param>
    /// <param name="invites">Locks the candidate's pending invite for lifecycle work.</param>
    /// <param name="bookings">Locks the candidate's active booking for the requirement invariant.</param>
    /// <param name="access">Authorizes candidate management.</param>
    /// <param name="audit">Records Employee Group assignment.</param>
    /// <param name="unitOfWork">Owns the candidate save.</param>
    public SaveCandidateHandler(
        ICandidateRepository candidates,
        IEmployeeGroupRepository groups,
        IInviteRepository invites,
        IBookingRepository bookings,
        IStaffAccessAuthorizer access,
        IAuditLogger audit,
        IUnitOfWork unitOfWork)
    {
        _candidates = candidates;
        _groups = groups;
        _invites = invites;
        _bookings = bookings;
        _access = access;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Creates one candidate and derives every requirement from its Employee Group.</summary>
    /// <param name="command">The staff creation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new candidate identifier.</returns>
    public async Task<Result<Guid>> CreateAsync(
        CreateCandidateCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await _access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        var resolved = await ResolveGroupAsync(command.EmployeeGroupId, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<Guid>.Failure(resolved.Error);
        }

        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (await _candidates.GetByEmailAsync(email, cancellationToken) is not null)
        {
            return Result<Guid>.Failure(Error.Conflict($"{email} is already a candidate."));
        }

        var id = Guid.NewGuid();

        Candidate candidate;
        try
        {
            candidate = Candidate.Create(id, command.Name, command.Email, resolved.Value);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        _candidates.Add(candidate);
        _audit.Record(
            AuditEntityTypes.Candidate,
            id,
            AuditAction.EmployeeGroupAssigned,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            SerializeAssignment(null, resolved.Value.Code, [], RequirementCodes(resolved.Value)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(id);
    }

    /// <summary>
    /// Updates candidate details under the Candidate lifecycle lock, superseding a pending Invite
    /// and resetting status when the derived set changes, and rejecting set changes that would
    /// alter an active original Booking.
    /// </summary>
    /// <param name="command">The staff update request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success, or a conflict when requirements drift from the active booking snapshot.</returns>
    public async Task<Result> UpdateAsync(
        UpdateCandidateCommand command,
        CancellationToken cancellationToken)
    {
        var updateAuthorized = await _access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (updateAuthorized.IsFailure)
        {
            return Result.Failure(updateAuthorized.Error);
        }

        if (await _candidates.GetAsync(command.CandidateId, cancellationToken) is null)
        {
            return Result.Failure(Error.NotFound("No such candidate."));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await _candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such candidate."));
        }

        var resolved = await ResolveGroupAsync(command.EmployeeGroupId, cancellationToken);
        if (resolved.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(resolved.Error);
        }

        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        var owner = await _candidates.GetByEmailAsync(email, cancellationToken);
        if (owner is not null && owner.Id != candidate.Id)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict($"{email} is already a candidate."));
        }

        var pendingInvite = await _invites.LockPendingInitialForCandidateAsync(
            candidate.Id, cancellationToken);
        var activeBooking = await _bookings.LockActiveOriginalForCandidateAsync(
            candidate.Id, cancellationToken);

        var submittedRequirements = resolved.Value.RequiredAppointmentTypeIds.ToHashSet();
        var currentRequirements = candidate.RequiredAppointmentTypeIds.ToHashSet();
        var setChanged = !submittedRequirements.SetEquals(currentRequirements);
        var detailsChanged =
            !string.Equals((command.Name ?? string.Empty).Trim(), candidate.Name, StringComparison.Ordinal)
            || !string.Equals(email, candidate.Email, StringComparison.Ordinal);
        var groupChanged = candidate.EmployeeGroupId != resolved.Value.Id;

        if (!setChanged && !detailsChanged && !groupChanged)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success();
        }

        if (setChanged && activeBooking is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.CandidateGroupActiveBookingConflict(
                "Appointment requirements cannot change while the candidate has an active booking. "
                + "Cancel and rebook first."));
        }

        string? oldGroupCode = null;
        if (candidate.EmployeeGroupId.HasValue)
        {
            oldGroupCode = (await _groups.GetAsync(candidate.EmployeeGroupId.Value, cancellationToken))?.Code;
        }

        var oldRequirementCodes = RequirementCodes(candidate.RequiredAppointmentTypeIds);
        var newRequirementCodes = RequirementCodes(resolved.Value.RequiredAppointmentTypeIds);

        try
        {
            candidate.UpdateDetails(command.Name, command.Email);

            if (setChanged)
            {
                pendingInvite?.MarkSuperseded();
                candidate.ResetAfterRequirementChange();
            }

            candidate.AssignEmployeeGroup(resolved.Value);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(ex.Message));
        }

        _audit.Record(
            AuditEntityTypes.Candidate,
            candidate.Id,
            AuditAction.EmployeeGroupChanged,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            SerializeAssignment(oldGroupCode, resolved.Value.Code, oldRequirementCodes, newRequirementCodes));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<EmployeeGroup>> ResolveGroupAsync(
        Guid? employeeGroupId,
        CancellationToken cancellationToken)
    {
        if (employeeGroupId is null)
        {
            return Result<EmployeeGroup>.Failure(
                new Error("employee_group_required", "An employee group is required."));
        }

        var group = await _groups.GetAsync(employeeGroupId.Value, cancellationToken);
        if (group is null)
        {
            return Result<EmployeeGroup>.Failure(
                new Error("employee_group_unknown", "The employee group is not known."));
        }

        if (!group.IsActive)
        {
            return Result<EmployeeGroup>.Failure(
                new Error("employee_group_inactive", "The employee group is not active."));
        }

        if (group.RequiredAppointmentTypeIds.Count == 0)
        {
            return Result<EmployeeGroup>.Failure(
                new Error("employee_group_unmapped", "The employee group has no mapped appointment types."));
        }

        return Result<EmployeeGroup>.Success(group);
    }

    private static IReadOnlyList<string> RequirementCodes(EmployeeGroup group) =>
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
