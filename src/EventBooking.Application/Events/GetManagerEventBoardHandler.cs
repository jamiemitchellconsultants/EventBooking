using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Events;

/// <summary>Defines open proposal view for the current use case.</summary>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="AcceptedByAppointmentTypeNames">The accepted by appointment type names.</param>
/// <param name="MyAcceptedHeadcount">The my accepted headcount.</param>
/// <param name="AcceptedByMe">The accepted by me.</param>
/// <param name="CreatedByMe">The created by me.</param>
public sealed record OpenProposalView(
    Guid ProposalId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<string> AcceptedByAppointmentTypeNames,
    int? MyAcceptedHeadcount,
    bool AcceptedByMe,
    bool CreatedByMe);

/// <summary>Defines manager event view for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="MyHeadcount">The my headcount.</param>
/// <param name="MyRemainingCapacity">The my remaining capacity.</param>
public sealed record ManagerEventView(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

/// <summary>Defines manager event board for the current use case.</summary>
/// <param name="OpenProposals">The open proposals.</param>
/// <param name="Events">The events.</param>
public sealed record ManagerEventBoard(
    IReadOnlyList<OpenProposalView> OpenProposals,
    IReadOnlyList<ManagerEventView> Events);

/// <summary>Defines get manager event board query for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
public sealed record GetManagerEventBoardQuery(Guid ManagerUserId);

/// <summary>Defines get manager event board handler for the current use case.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="clock">The clock.</param>
public sealed class GetManagerEventBoardHandler(
    IEventProposalRepository proposals,
    IEventRepository events,
    IStaffAccessAuthorizer access,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<ManagerEventBoard>> HandleAsync(
        GetManagerEventBoardQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<ManagerEventBoard>.Failure(authorized.Error);
        }

        var myType = authorized.Value.AppointmentTypeId!.Value;

        var open = (await proposals.ListOpenAsync(cancellationToken))
            .OrderBy(proposal => proposal.Window)
            .Select(proposal =>
            {
                var myAcceptance = proposal.Acceptances.SingleOrDefault(
                    acceptance =>
                        acceptance.AppointmentTypeId == myType
                        && acceptance.ManagerUserId == query.ManagerUserId);

                return new OpenProposalView(
                    proposal.Id,
                    proposal.Window.Date,
                    proposal.Window.StartTime,
                    proposal.Window.EndTime,
                    proposal.Acceptances
                        .Select(acceptance =>
                            AppointmentTypeIds.NameOf(acceptance.AppointmentTypeId))
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToList(),
                    myAcceptance?.Headcount,
                    myAcceptance is not null,
                    proposal.CreatedByManagerUserId == query.ManagerUserId);
            })
            .ToList();

        var confirmed = (await events.ListActiveAsync(clock.TodayAtTransitionalLocation, cancellationToken))
            .OrderBy(s => s.Window)
            .Select(s =>
            {
                var capacity = s.CapacityFor(myType);
                return new ManagerEventView(
                    s.Id,
                    s.Window.Date,
                    s.Window.StartTime,
                    s.Window.EndTime,
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity);
            })
            .ToList();

        return Result<ManagerEventBoard>.Success(new ManagerEventBoard(open, confirmed));
    }
}
