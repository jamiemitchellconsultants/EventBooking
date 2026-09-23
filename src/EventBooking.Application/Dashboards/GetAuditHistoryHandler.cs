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
