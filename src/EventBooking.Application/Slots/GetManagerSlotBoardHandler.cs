using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Slots;

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

/// <summary>Defines manager confirmed slot view for the current use case.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="MyHeadcount">The my headcount.</param>
/// <param name="MyRemainingCapacity">The my remaining capacity.</param>
public sealed record ManagerConfirmedSlotView(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

/// <summary>Defines manager slot board for the current use case.</summary>
/// <param name="OpenProposals">The open proposals.</param>
/// <param name="ConfirmedSlots">The confirmed slots.</param>
public sealed record ManagerSlotBoard(
    IReadOnlyList<OpenProposalView> OpenProposals,
    IReadOnlyList<ManagerConfirmedSlotView> ConfirmedSlots);

/// <summary>Defines get manager slot board query for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
public sealed record GetManagerSlotBoardQuery(Guid ManagerUserId);

/// <summary>Defines get manager slot board handler for the current use case.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="confirmedSlots">The confirmed slots.</param>
/// <param name="access">The access.</param>
/// <param name="clock">The clock.</param>
public sealed class GetManagerSlotBoardHandler(
    ISlotProposalRepository proposals,
    IConfirmedSlotRepository confirmedSlots,
    IStaffAccessAuthorizer access,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<ManagerSlotBoard>> HandleAsync(
        GetManagerSlotBoardQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<ManagerSlotBoard>.Failure(authorized.Error);
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

        var confirmed = (await confirmedSlots.ListActiveAsync(clock.TodayAtHeadOffice, cancellationToken))
            .OrderBy(s => s.Window)
            .Select(s =>
            {
                var capacity = s.CapacityFor(myType);
                return new ManagerConfirmedSlotView(
                    s.Id,
                    s.Window.Date,
                    s.Window.StartTime,
                    s.Window.EndTime,
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity);
            })
            .ToList();

        return Result<ManagerSlotBoard>.Success(new ManagerSlotBoard(open, confirmed));
    }
}
