using System.Text;
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
    /// <summary>The most data rows one file may hold, on either surface.</summary>
    internal const int MaxDataRows = 1000;

    /// <summary>The largest file, in UTF-8 bytes, one import may carry, on either surface.</summary>
    internal const int MaxBytes = 1_048_576;

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

        // Both bounds live here, not on either transport, so the REST route and the import
        // tool refuse one oversized file with one application error. The import tool takes
        // the CSV as a string, so no file-part check guards it before this point.
        if (command.CsvContent is { } content && Encoding.UTF8.GetByteCount(content) > MaxBytes)
        {
            return Result<AttendeeImportOutcome>.Failure(
                Error.Validation("The file must be 1 MB or smaller."));
        }

        var parsed = AttendeeCsvParser.Parse(command.CsvContent);

        // Every data line becomes exactly one row or one line-numbered error, so their sum is
        // the file's data-line count. Counting rows alone would let a file of malformed lines
        // past the bound and answer it with an error per line.
        var dataLines = parsed.Rows.Count + parsed.Errors.Count(e => e.LineNumber > 1);
        if (dataLines > MaxDataRows)
        {
            return Result<AttendeeImportOutcome>.Failure(
                Error.Validation("The file must hold 1000 data rows or fewer."));
        }

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
