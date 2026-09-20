# 00a — Port source 8 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Application/Candidates/CandidateReadiness.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/CandidateReadiness.cs","encoding":"utf8","sha256":"927eea9ca97557c7b0e22f86d9d8c91ea49f7ab42230c06c4e2921b356ae49ea","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Application.Candidates;

/// <summary>Explains whether EventBooking has completed every current Candidate requirement.</summary>
public enum CandidateReadinessCode
{
    /// <summary>Every current requirement has a Completed non-cancelled attempt.</summary>
    Ready = 1,
    /// <summary>The Candidate has no assigned Employee Group during Release 1 reconciliation.</summary>
    EmployeeGroupUnassigned = 2,
    /// <summary>The Candidate has no Active original Booking journey.</summary>
    NoActiveBooking = 3,
    /// <summary>The current requirements differ from the journey's Appointment Type snapshots.</summary>
    RequirementSnapshotMismatch = 4,
    /// <summary>At least one current requirement has no Completed non-cancelled attempt.</summary>
    AppointmentsOutstanding = 5,
}

/// <summary>One booked attempt used to choose the latest non-cancelled result per type.</summary>
/// <param name="BookingAppointmentId">The stable appointment-record identifier.</param>
/// <param name="AppointmentTypeId">The attempted appointment type.</param>
/// <param name="Status">The appointment's operational status.</param>
/// <param name="BookingId">The parent booking identifier.</param>
/// <param name="BookingStatus">The parent booking lifecycle status.</param>
/// <param name="BookingCreatedAt">When the parent booking was created.</param>
public sealed record CandidateReadinessAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    EventBooking.Domain.Bookings.BookingAppointmentStatus Status,
    Guid BookingId,
    EventBooking.Domain.Bookings.BookingStatus BookingStatus,
    DateTimeOffset BookingCreatedAt);

/// <summary>The authorized persistence projection consumed by the readiness calculator.</summary>
/// <param name="CandidateId">The candidate identifier.</param>
/// <param name="EmployeeGroupId">The assigned group, or null during reconciliation.</param>
/// <param name="CurrentRequirementTypeIds">The group's current requirement set.</param>
/// <param name="ActiveOriginalBookingId">The active journey root, or null when absent.</param>
/// <param name="Attempts">Every booked attempt in the original and recovery journey.</param>
public sealed record CandidateReadinessSnapshot(
    Guid CandidateId,
    Guid? EmployeeGroupId,
    IReadOnlyList<Guid> CurrentRequirementTypeIds,
    Guid? ActiveOriginalBookingId,
    IReadOnlyList<CandidateReadinessAttempt> Attempts);

/// <summary>Minimum canonical detail for one incomplete current Appointment Type.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="IsRecoverable">Whether the latest attempt is a recoverable no-show.</param>
public sealed record OutstandingAppointmentType(
    string Code,
    string Name,
    bool IsRecoverable);

/// <summary>The internal EventBooking readiness result shown to a Coordinator.</summary>
/// <param name="CandidateId">The candidate identifier.</param>
/// <param name="Code">The machine-readable readiness reason.</param>
/// <param name="OutstandingAppointmentTypes">Incomplete types sorted by code.</param>
public sealed record CandidateReadiness(
    Guid CandidateId,
    CandidateReadinessCode Code,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);
`````

## src/EventBooking.Application/Candidates/CandidateReadinessCalculator.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/CandidateReadinessCalculator.cs","encoding":"utf8","sha256":"92f51c07edebd5430ba80bcdf5dffe831fa5714f31d334b394f4068d72bfd9be","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Candidates;

/// <summary>Calculates deterministic readiness from one persistence snapshot.</summary>
public sealed class CandidateReadinessCalculator
{
    /// <summary>Calculates one deterministic result without inferring an Employee Group.</summary>
    /// <param name="snapshot">The authorized journey projection.</param>
    /// <returns>Ready, or the highest-precedence failure with outstanding types.</returns>
    public CandidateReadiness Calculate(CandidateReadinessSnapshot snapshot)
    {
        if (!snapshot.EmployeeGroupId.HasValue)
        {
            return new CandidateReadiness(
                snapshot.CandidateId, CandidateReadinessCode.EmployeeGroupUnassigned, []);
        }

        if (!snapshot.ActiveOriginalBookingId.HasValue)
        {
            return new CandidateReadiness(
                snapshot.CandidateId, CandidateReadinessCode.NoActiveBooking, []);
        }

        var current = snapshot.CurrentRequirementTypeIds;
        if (current.Count == 0
            || current.Distinct().Count() != current.Count
            || current.Any(id => !AppointmentTypeIds.All.Contains(id)))
        {
            return Mismatch(snapshot);
        }

        var live = snapshot.Attempts
            .Where(attempt => attempt.BookingStatus != BookingStatus.Cancelled)
            .ToList();

        var originalTypes = live
            .Where(attempt => attempt.BookingId == snapshot.ActiveOriginalBookingId.Value)
            .Select(attempt => attempt.AppointmentTypeId)
            .Distinct()
            .Order()
            .ToList();

        if (!originalTypes.SequenceEqual(current.Order())
            || live.Any(attempt => !originalTypes.Contains(attempt.AppointmentTypeId)))
        {
            return Mismatch(snapshot);
        }

        var outstanding = new List<OutstandingAppointmentType>();
        foreach (var typeId in current.Order())
        {
            var attempts = live
                .Where(attempt => attempt.AppointmentTypeId == typeId)
                .OrderBy(attempt => attempt.BookingCreatedAt)
                .ThenBy(attempt => attempt.BookingAppointmentId)
                .ToList();

            if (attempts.Any(attempt => attempt.Status == BookingAppointmentStatus.Completed))
            {
                continue;
            }

            var latest = attempts.Count == 0 ? null : attempts[^1];
            outstanding.Add(new OutstandingAppointmentType(
                AppointmentTypeIds.CodeOf(typeId),
                AppointmentTypeIds.NameOf(typeId),
                latest is not null && latest.Status == BookingAppointmentStatus.NoShow));
        }

        outstanding.Sort((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));

        return outstanding.Count == 0
            ? new CandidateReadiness(snapshot.CandidateId, CandidateReadinessCode.Ready, [])
            : new CandidateReadiness(
                snapshot.CandidateId, CandidateReadinessCode.AppointmentsOutstanding, outstanding);
    }

    private static CandidateReadiness Mismatch(CandidateReadinessSnapshot snapshot) =>
        new(snapshot.CandidateId, CandidateReadinessCode.RequirementSnapshotMismatch, []);
}
`````

## src/EventBooking.Application/Candidates/DeleteCandidateHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/DeleteCandidateHandler.cs","encoding":"utf8","sha256":"8b7611cd3b0da50e3f2919cc4b327a46290e2e9e9a1d853b9c8040969805cc50","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Candidates;

/// <summary>Defines delete candidate command for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="CandidateId">The candidate id.</param>
/// <param name="ConfirmCascade">The confirm cascade.</param>
public sealed record DeleteCandidateCommand(Guid StaffUserId, Guid CandidateId, bool ConfirmCascade);

/// <summary>Deletes a candidate only after serializing and reconciling their current lifecycle rows.</summary>
/// <param name="candidates">The candidates.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="slots">The slots.</param>
/// <param name="access">The access.</param>
/// <param name="bookingCanceller">The booking canceller.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class DeleteCandidateHandler(
    ICandidateRepository candidates,
    IInviteRepository invites,
    IBookingRepository bookings,
    IConfirmedSlotRepository slots,
    IStaffAccessAuthorizer access,
    BookingCanceller bookingCanceller,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Deletes the candidate and releases their active booking after confirmed cascade authorization.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        DeleteCandidateCommand command,
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

        // Candidate is the lifecycle root. Every authoritative cascade read occurs only after its
        // row lock is held, then follows Candidate -> Invite -> Booking.
        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure(Error.NotFound("No such candidate."));
        }

        var invite = await invites.LockPendingForCandidateAsync(candidate.Id, cancellationToken);
        var booking = await bookings.LockActiveOriginalForCandidateAsync(candidate.Id, cancellationToken);

        // An active recovery booking holds its own slot capacity and is invisible to the
        // original-only lookup above, so it is locked and cascaded here as well.
        var activeRecovery = booking is not null && booking.IsOriginal
            ? await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken)
            : null;

        var bookingCount = (booking is null ? 0 : 1) + (activeRecovery is null ? 0 : 1);
        var inviteCount = invite is null ? 0 : 1;

        if (!command.ConfirmCascade && bookingCount + inviteCount > 0)
        {
            return Result.Failure(Error.Conflict(
                $"Deleting this candidate will cancel {bookingCount} booking and {inviteCount} pending invite, "
                + "and free the capacity they hold. Confirm to proceed."));
        }

        var actorId = command.StaffUserId.ToString();

        try
        {
            if (activeRecovery is not null)
            {
                var recoverySlot = activeRecovery.ConfirmedSlotId == booking!.ConfirmedSlotId
                    ? await slots.LockForUpdateAsync(booking.ConfirmedSlotId, cancellationToken)
                    : await slots.LockForUpdateAsync(activeRecovery.ConfirmedSlotId, cancellationToken);
                if (recoverySlot is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("No such slot."));
                }

                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    activeRecovery,
                    recoverySlot,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (releasedRecovery.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(releasedRecovery.Error);
                }
            }

            if (booking is not null)
            {
                var slot = await slots.LockForUpdateAsync(booking.ConfirmedSlotId, cancellationToken);
                if (slot is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("No such slot."));
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    slot,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(released.Error);
                }
            }

            if (invite is not null)
            {
                invite.MarkSuperseded();
            }

            audit.Record(
                AuditEntityTypes.Candidate,
                candidate.Id,
                AuditAction.CandidateDeleted,
                ActorType.Staff,
                actorId,
                null);

            candidates.Remove(candidate);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict(ex.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
`````

## src/EventBooking.Application/Candidates/EmployeeGroupModels.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/EmployeeGroupModels.cs","encoding":"utf8","sha256":"6d38f2cd8fefa2d2301e9e361a3eb3cbb94f124683b512b04e4e151ab30af087","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Application.Candidates;

/// <summary>One fixed Appointment Type projected for group and Candidate read models.</summary>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
public sealed record AppointmentTypeSummary(string Code, string Name);

/// <summary>One active Employee Group available for assignment.</summary>
/// <param name="EmployeeGroupId">The employee group id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="RequiredAppointmentTypes">The required appointment types.</param>
public sealed record EmployeeGroupListItem(
    Guid EmployeeGroupId,
    string Code,
    string Name,
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes);

/// <summary>Requests active Employee Groups for one authorized Coordinator.</summary>
/// <param name="StaffUserId">The staff user id.</param>
public sealed record ListEmployeeGroupsQuery(Guid StaffUserId);
`````

## src/EventBooking.Application/Candidates/GetCandidateBookingsHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/GetCandidateBookingsHandler.cs","encoding":"utf8","sha256":"8953dcdce3c9bd9e81af2185ba9e9dbb8e24697ce5424fc9a78c380e1c11c418","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Candidates;

/// <summary>One active booking a coordinator may cancel, without any management token.</summary>
/// <param name="BookingId">The booking identifier used to target a cancellation.</param>
/// <param name="IsOriginal">True for the original booking; false for an active recovery booking.</param>
/// <param name="SlotDate">The date of the confirmed window the booking holds.</param>
/// <param name="SlotStartTime">The start of the confirmed window the booking holds.</param>
/// <param name="SlotEndTime">The end of the confirmed window the booking holds.</param>
public sealed record CandidateBookingSummary(
    Guid BookingId,
    bool IsOriginal,
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    TimeOnly SlotEndTime);

/// <summary>Requests the active bookings a coordinator may cancel for one candidate.</summary>
/// <param name="StaffUserId">The staff member asking for the listing.</param>
/// <param name="CandidateId">The candidate whose bookings are listed.</param>
public sealed record GetCandidateBookingsQuery(Guid StaffUserId, Guid CandidateId);

/// <summary>Authorizes a coordinator before listing a candidate's active bookings.</summary>
/// <param name="access">Authorizes candidate management.</param>
/// <param name="queries">Loads the active-booking projection.</param>
public sealed class GetCandidateBookingsHandler(
    IStaffAccessAuthorizer access,
    ICandidateBookingQueries queries)
{
    /// <summary>Authorizes a coordinator before loading or returning candidate-linked state.</summary>
    /// <param name="query">The staff listing request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The candidate's active bookings, a forbidden failure when the caller cannot manage
    /// candidates, or a not-found failure for an unknown candidate.
    /// </returns>
    public async Task<Result<IReadOnlyList<CandidateBookingSummary>>> HandleAsync(
        GetCandidateBookingsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<CandidateBookingSummary>>.Failure(authorized.Error);
        }

        var rows = await queries.ListActiveForCandidateAsync(query.CandidateId, cancellationToken);
        if (rows is null)
        {
            return Result<IReadOnlyList<CandidateBookingSummary>>.Failure(
                Error.NotFound("No such candidate."));
        }

        return Result<IReadOnlyList<CandidateBookingSummary>>.Success(rows);
    }
}
`````

## src/EventBooking.Application/Candidates/GetCandidateReadinessHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/GetCandidateReadinessHandler.cs","encoding":"utf8","sha256":"54854d50db33dca95adc371476009c33cbe6c9acbd236498c58c5edef39f9551","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;

namespace EventBooking.Application.Candidates;

/// <summary>Requests internal readiness for one candidate.</summary>
/// <param name="StaffUserId">The staff member asking for readiness.</param>
/// <param name="CandidateId">The candidate identifier.</param>
public sealed record GetCandidateReadinessQuery(Guid StaffUserId, Guid CandidateId);

/// <summary>Authorizes a Coordinator before loading or returning Candidate-linked state.</summary>
/// <param name="access">Authorizes candidate management.</param>
/// <param name="queries">Loads the readiness journey projection.</param>
/// <param name="calculator">Calculates readiness from the projection.</param>
public sealed class GetCandidateReadinessHandler(
    IStaffAccessAuthorizer access,
    ICandidateReadinessQueries queries,
    CandidateReadinessCalculator calculator)
{
    /// <summary>Authorizes a Coordinator before loading or returning Candidate-linked state.</summary>
    /// <param name="query">The staff readiness request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The calculated readiness.</returns>
    public async Task<Result<CandidateReadiness>> HandleAsync(
        GetCandidateReadinessQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CandidateReadiness>.Failure(authorized.Error);
        }

        var snapshot = await queries.GetSnapshotAsync(query.CandidateId, cancellationToken);
        if (snapshot is null)
        {
            return Result<CandidateReadiness>.Failure(Error.NotFound("No such candidate."));
        }

        return Result<CandidateReadiness>.Success(calculator.Calculate(snapshot));
    }
}
`````

## src/EventBooking.Application/Candidates/ImportCandidatesHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/ImportCandidatesHandler.cs","encoding":"utf8","sha256":"1000c390beb399fd62be0be7dda2228a44199d6134eccde1fff76edbeccc22b6","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Candidates;

/// <summary>Requests a bulk Candidate import from Employee Group CSV content.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="CsvContent">The csv content.</param>
public sealed record ImportCandidatesCommand(Guid StaffUserId, string? CsvContent);

/// <summary>
/// Accepted is false when the file was rejected. The result itself is still a success — a rejected
/// upload is a normal outcome with a list of row errors, not a failed request.
/// </summary>
/// <param name="Accepted">The accepted.</param>
/// <param name="ImportedCount">The imported count.</param>
/// <param name="Errors">The errors.</param>
public sealed record CandidateImportOutcome(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<CandidateCsvError> Errors);

/// <summary>Imports candidates whose requirements derive from one Employee Group per row.</summary>
/// <param name="candidates">Persists candidate rows.</param>
/// <param name="groups">Resolves row Employee Group codes.</param>
/// <param name="access">Authorizes candidate management.</param>
/// <param name="unitOfWork">Owns the candidate save.</param>
public sealed class ImportCandidatesHandler(
    ICandidateRepository candidates,
    IEmployeeGroupRepository groups,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork)
{
    /// <summary>Validates every row before persisting any Candidate.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CandidateImportOutcome>> HandleAsync(
        ImportCandidatesCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CandidateImportOutcome>.Failure(authorized.Error);
        }

        var parsed = CandidateCsvParser.Parse(command.CsvContent);
        var errors = parsed.Errors.ToList();

        // Build every candidate first, collecting failures. Nothing is added to the repository
        // until the whole file is known to be good.
        var built = new List<Candidate>();

        foreach (var row in parsed.Rows)
        {
            var existing = await candidates.GetByEmailAsync(row.Email, cancellationToken);
            if (existing is not null)
            {
                errors.Add(new CandidateCsvError(row.LineNumber, $"{row.Email} is already a candidate."));
                continue;
            }

            var group = await groups.GetByCodeAsync(row.EmployeeGroupCode, cancellationToken);
            if (group is null)
            {
                errors.Add(new CandidateCsvError(
                    row.LineNumber, $"{row.EmployeeGroupCode} is not a known employee group code."));
                continue;
            }

            if (!group.IsActive)
            {
                errors.Add(new CandidateCsvError(
                    row.LineNumber, $"{row.EmployeeGroupCode} is not an active employee group."));
                continue;
            }

            if (group.RequiredAppointmentTypeIds.Count == 0)
            {
                errors.Add(new CandidateCsvError(
                    row.LineNumber, $"{row.EmployeeGroupCode} has no mapped appointment types."));
                continue;
            }

            try
            {
                built.Add(Candidate.Create(Guid.NewGuid(), row.Name, row.Email, group));
            }
            catch (DomainException ex)
            {
                errors.Add(new CandidateCsvError(row.LineNumber, ex.Message));
            }
        }

        if (errors.Count > 0)
        {
            return Result<CandidateImportOutcome>.Success(
                new CandidateImportOutcome(false, 0, errors.OrderBy(e => e.LineNumber).ToList()));
        }

        foreach (var candidate in built)
        {
            candidates.Add(candidate);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CandidateImportOutcome>.Success(
            new CandidateImportOutcome(true, built.Count, []));
    }
}
`````

## src/EventBooking.Application/Candidates/ListCandidatesHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/ListCandidatesHandler.cs","encoding":"utf8","sha256":"e5883fb9ed04c0a8f7e4da907e91320f608a9ad52c328f6e49e314f424d6698d","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Application.Candidates;

/// <summary>One Candidate with its assigned Employee Group and derived requirements.</summary>
/// <param name="CandidateId">The candidate id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="EmployeeGroupId">The employee group id.</param>
/// <param name="EmployeeGroupCode">The employee group code.</param>
/// <param name="EmployeeGroupName">The employee group name.</param>
/// <param name="RequiresEmployeeGroupReconciliation">The requires employee group reconciliation.</param>
/// <param name="RequiredAppointmentTypes">The required appointment types.</param>
/// <param name="Status">The status.</param>
/// <param name="StatusDisplay">The status display.</param>
public sealed record CandidateListItem(
    Guid CandidateId,
    string Name,
    string Email,
    Guid? EmployeeGroupId,
    string? EmployeeGroupCode,
    string? EmployeeGroupName,
    bool RequiresEmployeeGroupReconciliation,
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes,
    CandidateStatus Status,
    string StatusDisplay);

/// <summary>Requests Candidates, optionally narrowed by status or search text.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Status">The status.</param>
/// <param name="Search">The search.</param>
public sealed record ListCandidatesQuery(Guid StaffUserId, CandidateStatus? Status, string? Search);

/// <summary>Lists candidates with their assigned Employee Group and derived requirements.</summary>
/// <param name="candidates">Reads candidate rows.</param>
/// <param name="groups">Resolves assigned Employee Group identity.</param>
/// <param name="access">Authorizes candidate management.</param>
public sealed class ListCandidatesHandler(
    ICandidateRepository candidates,
    IEmployeeGroupRepository groups,
    IStaffAccessAuthorizer access)
{
    /// <summary>The Area C screen wording for each status.</summary>
    /// <param name="status">The status.</param>
    public static string DisplayOf(CandidateStatus status) => status switch
    {
        CandidateStatus.NotYetInvited => "Not yet invited",
        CandidateStatus.AwaitingAvailability => "Awaiting availability",
        CandidateStatus.Invited => "Invited (pending response)",
        CandidateStatus.Booked => "Booked",
        CandidateStatus.NoResponseNeedsFollowUp => "No response - needs follow-up",
        _ => status.ToString(),
    };

    /// <summary>Returns matching Candidates ordered by name with group identity.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<CandidateListItem>>> HandleAsync(
        ListCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<CandidateListItem>>.Failure(authorized.Error);
        }

        var all = await candidates.ListAsync(query.Status, cancellationToken);

        var search = query.Search?.Trim();
        var filtered = string.IsNullOrEmpty(search)
            ? all
            : all
                .Where(c =>
                    c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || c.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // The five reference rows are loaded once; only exceptional unlisted groups fall back
        // to an individual lookup.
        var reference = (await groups.ListActiveAsync(cancellationToken))
            .ToDictionary(group => group.Id);
        foreach (var missing in filtered
            .Select(candidate => candidate.EmployeeGroupId)
            .Where(id => id.HasValue && !reference.ContainsKey(id.Value))
            .Select(id => id!.Value)
            .Distinct()
            .ToList())
        {
            var group = await groups.GetAsync(missing, cancellationToken);
            if (group is not null)
            {
                reference[missing] = group;
            }
        }

        var items = filtered
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CandidateListItem(
                c.Id,
                c.Name,
                c.Email,
                c.EmployeeGroupId,
                c.EmployeeGroupId.HasValue && reference.TryGetValue(c.EmployeeGroupId.Value, out var group)
                    ? group.Code
                    : null,
                c.EmployeeGroupId.HasValue && reference.TryGetValue(c.EmployeeGroupId.Value, out var named)
                    ? named.Name
                    : null,
                !c.EmployeeGroupId.HasValue,
                c.RequiredAppointmentTypeIds
                    .Select(typeId => new AppointmentTypeSummary(
                        AppointmentTypeIds.CodeOf(typeId), AppointmentTypeIds.NameOf(typeId)))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToList(),
                c.Status,
                DisplayOf(c.Status)))
            .ToList();

        return Result<IReadOnlyList<CandidateListItem>>.Success(items);
    }
}
`````

## src/EventBooking.Application/Candidates/ListEmployeeGroupsHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/ListEmployeeGroupsHandler.cs","encoding":"utf8","sha256":"79ae021ac71a7ee130a57d78f2996ed9721ebeac81d729b27bfc24eb9f2cd1a3","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Candidates;

/// <summary>Lists the active Employee Groups a Coordinator may assign to Candidates.</summary>
/// <param name="groups">Reads change-controlled Employee Group reference data.</param>
/// <param name="access">Authorizes candidate management.</param>
public sealed class ListEmployeeGroupsHandler(
    IEmployeeGroupRepository groups,
    IStaffAccessAuthorizer access)
{
    /// <summary>Returns active mapped groups ordered by display name.</summary>
    /// <param name="query">The staff list request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable groups with their required appointment types.</returns>
    public async Task<Result<IReadOnlyList<EmployeeGroupListItem>>> HandleAsync(
        ListEmployeeGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<EmployeeGroupListItem>>.Failure(authorized.Error);
        }

        var active = await groups.ListActiveAsync(cancellationToken);

        var items = active
            .Select(group => new EmployeeGroupListItem(
                group.Id,
                group.Code,
                group.Name,
                group.RequiredAppointmentTypeIds
                    .Select(typeId => new AppointmentTypeSummary(
                        AppointmentTypeIds.CodeOf(typeId), AppointmentTypeIds.NameOf(typeId)))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToList()))
            .ToList();

        return Result<IReadOnlyList<EmployeeGroupListItem>>.Success(items);
    }
}
`````

## src/EventBooking.Application/Candidates/SaveCandidateHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Candidates/SaveCandidateHandler.cs","encoding":"utf8","sha256":"1010cb4a8b652db59f9d4a78735d7134fe59797d25736b5cd571d9f4c9c64eb1","parts":1,"part":1} -->

`````csharp
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
`````

## src/EventBooking.Application/Common/Error.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Common/Error.cs","encoding":"utf8","sha256":"037c52566afddbe017d74ce68fb5d7c55092ba9bf6364e24ab7770424649274b","parts":1,"part":1} -->

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

## src/EventBooking.Application/Common/Result.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Common/Result.cs","encoding":"utf8","sha256":"7eb1fc3f046a8ca651835341601b61700f96a96387182bcd9a7830263d0ab89b","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Application.Common;

/// <summary>Defines result for the current use case.</summary>
public sealed class Result
{
    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Defines is success for the current use case.</summary>
    public bool IsSuccess { get; }

    /// <summary>Defines is failure for the current use case.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Defines error for the current use case.</summary>
    public Error Error { get; }

    /// <summary>Defines success for the current use case.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Defines failure for the current use case.</summary>
    /// <param name="error">The error.</param>
    public static Result Failure(Error error) => new(false, error);
}

/// <summary>Defines result for the current use case.</summary>
public sealed class Result<TValue>
{
    private readonly TValue? _value;

    private Result(bool isSuccess, TValue? value, Error error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    /// <summary>Defines is success for the current use case.</summary>
    public bool IsSuccess { get; }

    /// <summary>Defines is failure for the current use case.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Defines error for the current use case.</summary>
    public Error Error { get; }

    /// <summary>Defines value for the current use case.</summary>
    public TValue Value =>
        IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    /// <summary>Defines success for the current use case.</summary>
    /// <param name="value">The value.</param>
    public static Result<TValue> Success(TValue value) => new(true, value, Error.None);

    /// <summary>Defines failure for the current use case.</summary>
    /// <param name="error">The error.</param>
    public static Result<TValue> Failure(Error error) => new(false, default, error);
}
`````

## src/EventBooking.Application/Common/UniqueConstraintViolationException.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Common/UniqueConstraintViolationException.cs","encoding":"utf8","sha256":"fa065e5c6f214aac47ac449e73c3581442df91351ce91f4bb8022630a54f7213","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Application.Common;

/// <summary>
/// Signals that a database uniqueness backstop rejected a concurrent state transition. Handlers
/// convert this infrastructure-neutral exception to their stable conflict result instead of
/// exposing a database provider exception to API callers.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    /// <summary>
    /// Creates a uniqueness-conflict signal while preserving the provider exception for logging.
    /// </summary>
    /// <param name="innerException">The inner exception.</param>
    public UniqueConstraintViolationException(Exception innerException)
        : base("A database uniqueness constraint rejected the operation.", innerException)
    {
    }
}
`````

## src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs","encoding":"utf8","sha256":"18c93fc6d9585914074795f8e3db8dad0cc95c84982697e2d40f8da404774a4a","parts":1,"part":1} -->

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
