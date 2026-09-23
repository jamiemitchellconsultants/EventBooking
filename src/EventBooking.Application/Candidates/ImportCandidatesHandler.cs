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
