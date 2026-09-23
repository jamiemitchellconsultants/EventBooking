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
