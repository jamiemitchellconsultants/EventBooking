using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Dashboards;

/// <summary>Defines dashboards view for the current use case.</summary>
/// <param name="AwaitingAvailability">The awaiting availability.</param>
/// <param name="NoResponse">The no response.</param>
/// <param name="Slots">The slots.</param>
/// <param name="EmailStatuses">The email statuses.</param>
public sealed record DashboardsView(
    IReadOnlyList<AwaitingAvailabilityRow> AwaitingAvailability,
    IReadOnlyList<NoResponseRow> NoResponse,
    IReadOnlyList<SlotOverviewRow> Slots,
    IReadOnlyList<CandidateEmailStatusView> EmailStatuses);

/// <summary>Staff-facing delivery state for one candidate.</summary>
/// <param name="CandidateId">The candidate whose latest delivery is shown.</param>
/// <param name="TemplateDisplay">Human-readable template name.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether the current domain state still permits retry.</param>
public sealed record CandidateEmailStatusView(
    Guid CandidateId,
    string TemplateDisplay,
    DateTimeOffset SentAt,
    string Status,
    bool CanRetry);

/// <summary>Defines get dashboards query for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
public sealed record GetDashboardsQuery(Guid StaffUserId);

/// <summary>Defines get dashboards handler for the current use case.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetDashboardsHandler(
    IDashboardQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<DashboardsView>> HandleAsync(
        GetDashboardsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ViewCandidateDashboards,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<DashboardsView>.Failure(authorized.Error);
        }

        var emailStatuses = await queries.LatestEmailStatusAsync(cancellationToken);

        return Result<DashboardsView>.Success(new DashboardsView(
            await queries.AwaitingAvailabilityAsync(cancellationToken),
            await queries.NoResponseAsync(cancellationToken),
            await queries.SlotsOverviewAsync(cancellationToken),
            emailStatuses
                .Select(e => new CandidateEmailStatusView(
                    e.CandidateId,
                    TemplateDisplayOf(e.TemplateName),
                    e.SentAt,
                    e.Status.ToString(),
                    e.CanRetry))
                .ToList()));
    }

    /// <summary>Area H's template names, in the wording a coordinator reads on /candidates and /dashboards.</summary>
    /// <param name="template">The template.</param>
    public static string TemplateDisplayOf(EmailTemplate template) => template switch
    {
        EmailTemplate.CandidateInvite => "Invite",
        EmailTemplate.BookingConfirmation => "Booking confirmation",
        EmailTemplate.SlotCancelledRebookingNeeded => "Slot cancelled - rebooking needed",
        EmailTemplate.CandidateReinvite => "Re-invite",
        _ => template.ToString(),
    };
}
