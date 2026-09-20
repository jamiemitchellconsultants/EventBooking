# 00b — Vocabulary edits 12 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Application/Attendees/GetAttendeeBookingsHandler.cs — 1/1

<!-- vocabulary-file: {"id":62,"oldPath":"src/EventBooking.Application/Candidates/GetCandidateBookingsHandler.cs","newPath":"src/EventBooking.Application/Attendees/GetAttendeeBookingsHandler.cs","beforeSha":"8953dcdce3c9bd9e81af2185ba9e9dbb8e24697ce5424fc9a78c380e1c11c418","afterSha":"04ec62a260ac5bf7b2c6046a505f121d46ebedcc9e569695a0197267026c6aaa","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Attendees;

/// <summary>One active booking a coordinator may cancel, without any management token.</summary>
/// <param name="BookingId">The booking identifier used to target a cancellation.</param>
/// <param name="IsOriginal">True for the original booking; false for an active recovery booking.</param>
/// <param name="EventDate">The date of the confirmed window the booking holds.</param>
/// <param name="EventStartTime">The start of the confirmed window the booking holds.</param>
/// <param name="EventEndTime">The end of the confirmed window the booking holds.</param>
public sealed record AttendeeBookingSummary(
    Guid BookingId,
    bool IsOriginal,
    DateOnly EventDate,
    TimeOnly EventStartTime,
    TimeOnly EventEndTime);

/// <summary>Requests the active bookings a coordinator may cancel for one attendee.</summary>
/// <param name="StaffUserId">The staff member asking for the listing.</param>
/// <param name="AttendeeId">The attendee whose bookings are listed.</param>
public sealed record GetAttendeeBookingsQuery(Guid StaffUserId, Guid AttendeeId);

/// <summary>Authorizes a coordinator before listing a attendee's active bookings.</summary>
/// <param name="access">Authorizes attendee management.</param>
/// <param name="queries">Loads the active-booking projection.</param>
public sealed class GetAttendeeBookingsHandler(
    IStaffAccessAuthorizer access,
    IAttendeeBookingQueries queries)
{
    /// <summary>Authorizes a coordinator before loading or returning attendee-linked state.</summary>
    /// <param name="query">The staff listing request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The attendee's active bookings, a forbidden failure when the caller cannot manage
    /// attendees, or a not-found failure for an unknown attendee.
    /// </returns>
    public async Task<Result<IReadOnlyList<AttendeeBookingSummary>>> HandleAsync(
        GetAttendeeBookingsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<AttendeeBookingSummary>>.Failure(authorized.Error);
        }

        var rows = await queries.ListActiveForAttendeeAsync(query.AttendeeId, cancellationToken);
        if (rows is null)
        {
            return Result<IReadOnlyList<AttendeeBookingSummary>>.Failure(
                Error.NotFound("No such attendee."));
        }

        return Result<IReadOnlyList<AttendeeBookingSummary>>.Success(rows);
    }
}
`````

## before — src/EventBooking.Application/Candidates/GetCandidateReadinessHandler.cs — 1/1

<!-- vocabulary-file: {"id":63,"oldPath":"src/EventBooking.Application/Candidates/GetCandidateReadinessHandler.cs","newPath":"src/EventBooking.Application/Attendees/GetAttendeeReadinessHandler.cs","beforeSha":"54854d50db33dca95adc371476009c33cbe6c9acbd236498c58c5edef39f9551","afterSha":"f7362a3bcbca983468e41c2a2de0ada6a0446d85b7a56f4857b2f7a8e268e5f2","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Application/Attendees/GetAttendeeReadinessHandler.cs — 1/1

<!-- vocabulary-file: {"id":63,"oldPath":"src/EventBooking.Application/Candidates/GetCandidateReadinessHandler.cs","newPath":"src/EventBooking.Application/Attendees/GetAttendeeReadinessHandler.cs","beforeSha":"54854d50db33dca95adc371476009c33cbe6c9acbd236498c58c5edef39f9551","afterSha":"f7362a3bcbca983468e41c2a2de0ada6a0446d85b7a56f4857b2f7a8e268e5f2","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;

namespace EventBooking.Application.Attendees;

/// <summary>Requests internal readiness for one attendee.</summary>
/// <param name="StaffUserId">The staff member asking for readiness.</param>
/// <param name="AttendeeId">The attendee identifier.</param>
public sealed record GetAttendeeReadinessQuery(Guid StaffUserId, Guid AttendeeId);

/// <summary>Authorizes a Coordinator before loading or returning Attendee-linked state.</summary>
/// <param name="access">Authorizes attendee management.</param>
/// <param name="queries">Loads the readiness journey projection.</param>
/// <param name="calculator">Calculates readiness from the projection.</param>
public sealed class GetAttendeeReadinessHandler(
    IStaffAccessAuthorizer access,
    IAttendeeReadinessQueries queries,
    AttendeeReadinessCalculator calculator)
{
    /// <summary>Authorizes a Coordinator before loading or returning Attendee-linked state.</summary>
    /// <param name="query">The staff readiness request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The calculated readiness.</returns>
    public async Task<Result<AttendeeReadiness>> HandleAsync(
        GetAttendeeReadinessQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AttendeeReadiness>.Failure(authorized.Error);
        }

        var snapshot = await queries.GetSnapshotAsync(query.AttendeeId, cancellationToken);
        if (snapshot is null)
        {
            return Result<AttendeeReadiness>.Failure(Error.NotFound("No such attendee."));
        }

        return Result<AttendeeReadiness>.Success(calculator.Calculate(snapshot));
    }
}
`````

## before — src/EventBooking.Application/Candidates/ImportCandidatesHandler.cs — 1/1

<!-- vocabulary-file: {"id":64,"oldPath":"src/EventBooking.Application/Candidates/ImportCandidatesHandler.cs","newPath":"src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs","beforeSha":"1000c390beb399fd62be0be7dda2228a44199d6134eccde1fff76edbeccc22b6","afterSha":"e96097fa8358bc6e181cc618a2e93dcf2e6b0c3b4a79295a0de7ae8ada8b773f","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs — 1/1

<!-- vocabulary-file: {"id":64,"oldPath":"src/EventBooking.Application/Candidates/ImportCandidatesHandler.cs","newPath":"src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs","beforeSha":"1000c390beb399fd62be0be7dda2228a44199d6134eccde1fff76edbeccc22b6","afterSha":"e96097fa8358bc6e181cc618a2e93dcf2e6b0c3b4a79295a0de7ae8ada8b773f","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Application/Candidates/ListCandidatesHandler.cs — 1/1

<!-- vocabulary-file: {"id":65,"oldPath":"src/EventBooking.Application/Candidates/ListCandidatesHandler.cs","newPath":"src/EventBooking.Application/Attendees/ListAttendeesHandler.cs","beforeSha":"e5883fb9ed04c0a8f7e4da907e91320f608a9ad52c328f6e49e314f424d6698d","afterSha":"973d25e90d630c53c6f38fd6519512fc12dbf421e93bb88a6a95f7a04be73275","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Application/Attendees/ListAttendeesHandler.cs — 1/1

<!-- vocabulary-file: {"id":65,"oldPath":"src/EventBooking.Application/Candidates/ListCandidatesHandler.cs","newPath":"src/EventBooking.Application/Attendees/ListAttendeesHandler.cs","beforeSha":"e5883fb9ed04c0a8f7e4da907e91320f608a9ad52c328f6e49e314f424d6698d","afterSha":"973d25e90d630c53c6f38fd6519512fc12dbf421e93bb88a6a95f7a04be73275","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Attendees;

/// <summary>One Attendee with its assigned Attendee Group and derived requirements.</summary>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
/// <param name="AttendeeGroupCode">The attendee group code.</param>
/// <param name="AttendeeGroupName">The attendee group name.</param>
/// <param name="RequiresAttendeeGroupReconciliation">The requires attendee group reconciliation.</param>
/// <param name="RequiredAppointmentTypes">The required appointment types.</param>
/// <param name="Status">The status.</param>
/// <param name="StatusDisplay">The status display.</param>
public sealed record AttendeeListItem(
    Guid AttendeeId,
    string Name,
    string Email,
    Guid? AttendeeGroupId,
    string? AttendeeGroupCode,
    string? AttendeeGroupName,
    bool RequiresAttendeeGroupReconciliation,
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes,
    AttendeeStatus Status,
    string StatusDisplay);

/// <summary>Requests Attendees, optionally narrowed by status or search text.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Status">The status.</param>
/// <param name="Search">The search.</param>
public sealed record ListAttendeesQuery(Guid StaffUserId, AttendeeStatus? Status, string? Search);

/// <summary>Lists attendees with their assigned Attendee Group and derived requirements.</summary>
/// <param name="attendees">Reads attendee rows.</param>
/// <param name="groups">Resolves assigned Attendee Group identity.</param>
/// <param name="access">Authorizes attendee management.</param>
public sealed class ListAttendeesHandler(
    IAttendeeRepository attendees,
    IAttendeeGroupRepository groups,
    IStaffAccessAuthorizer access)
{
    /// <summary>The Area C screen wording for each status.</summary>
    /// <param name="status">The status.</param>
    public static string DisplayOf(AttendeeStatus status) => status switch
    {
        AttendeeStatus.NotYetInvited => "Not yet invited",
        AttendeeStatus.AwaitingAvailability => "Awaiting availability",
        AttendeeStatus.Invited => "Invited (pending response)",
        AttendeeStatus.Booked => "Booked",
        AttendeeStatus.NoResponseNeedsFollowUp => "No response - needs follow-up",
        _ => status.ToString(),
    };

    /// <summary>Returns matching Attendees ordered by name with group identity.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<AttendeeListItem>>> HandleAsync(
        ListAttendeesQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<AttendeeListItem>>.Failure(authorized.Error);
        }

        var all = await attendees.ListAsync(query.Status, cancellationToken);

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
            .Select(attendee => attendee.AttendeeGroupId)
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
            .Select(c => new AttendeeListItem(
                c.Id,
                c.Name,
                c.Email,
                c.AttendeeGroupId,
                c.AttendeeGroupId.HasValue && reference.TryGetValue(c.AttendeeGroupId.Value, out var group)
                    ? group.Code
                    : null,
                c.AttendeeGroupId.HasValue && reference.TryGetValue(c.AttendeeGroupId.Value, out var named)
                    ? named.Name
                    : null,
                !c.AttendeeGroupId.HasValue,
                c.RequiredAppointmentTypeIds
                    .Select(typeId => new AppointmentTypeSummary(
                        AppointmentTypeIds.CodeOf(typeId), AppointmentTypeIds.NameOf(typeId)))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToList(),
                c.Status,
                DisplayOf(c.Status)))
            .ToList();

        return Result<IReadOnlyList<AttendeeListItem>>.Success(items);
    }
}
`````

## before — src/EventBooking.Application/Candidates/ListEmployeeGroupsHandler.cs — 1/1

<!-- vocabulary-file: {"id":66,"oldPath":"src/EventBooking.Application/Candidates/ListEmployeeGroupsHandler.cs","newPath":"src/EventBooking.Application/Attendees/ListAttendeeGroupsHandler.cs","beforeSha":"79ae021ac71a7ee130a57d78f2996ed9721ebeac81d729b27bfc24eb9f2cd1a3","afterSha":"a539811fc83edfedaa661bf15979c71d64d2f816be6cfb5762b1123f36b6901b","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Application/Attendees/ListAttendeeGroupsHandler.cs — 1/1

<!-- vocabulary-file: {"id":66,"oldPath":"src/EventBooking.Application/Candidates/ListEmployeeGroupsHandler.cs","newPath":"src/EventBooking.Application/Attendees/ListAttendeeGroupsHandler.cs","beforeSha":"79ae021ac71a7ee130a57d78f2996ed9721ebeac81d729b27bfc24eb9f2cd1a3","afterSha":"a539811fc83edfedaa661bf15979c71d64d2f816be6cfb5762b1123f36b6901b","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Attendees;

/// <summary>Lists the active Attendee Groups a Coordinator may assign to Attendees.</summary>
/// <param name="groups">Reads change-controlled Attendee Group reference data.</param>
/// <param name="access">Authorizes attendee management.</param>
public sealed class ListAttendeeGroupsHandler(
    IAttendeeGroupRepository groups,
    IStaffAccessAuthorizer access)
{
    /// <summary>Returns active mapped groups ordered by display name.</summary>
    /// <param name="query">The staff list request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable groups with their required appointment types.</returns>
    public async Task<Result<IReadOnlyList<AttendeeGroupListItem>>> HandleAsync(
        ListAttendeeGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<AttendeeGroupListItem>>.Failure(authorized.Error);
        }

        var active = await groups.ListActiveAsync(cancellationToken);

        var items = active
            .Select(group => new AttendeeGroupListItem(
                group.Id,
                group.Code,
                group.Name,
                group.RequiredAppointmentTypeIds
                    .Select(typeId => new AppointmentTypeSummary(
                        AppointmentTypeIds.CodeOf(typeId), AppointmentTypeIds.NameOf(typeId)))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToList()))
            .ToList();

        return Result<IReadOnlyList<AttendeeGroupListItem>>.Success(items);
    }
}
`````

## before — src/EventBooking.Application/Candidates/SaveCandidateHandler.cs — 1/1

<!-- vocabulary-file: {"id":67,"oldPath":"src/EventBooking.Application/Candidates/SaveCandidateHandler.cs","newPath":"src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs","beforeSha":"1010cb4a8b652db59f9d4a78735d7134fe59797d25736b5cd571d9f4c9c64eb1","afterSha":"5972e8ec6386ef1ce8b34be8dca959a1ce99d6f3dbff155ea4a1393f6e478059","side":"before","part":1,"parts":1} -->

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
