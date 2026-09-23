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
