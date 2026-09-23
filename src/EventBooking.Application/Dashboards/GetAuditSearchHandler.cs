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
