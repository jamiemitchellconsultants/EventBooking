using System.ComponentModel;
using CancelEventHandler = EventBooking.Application.Events.CancelEventHandler;
using EventBooking.Api.Auth;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Events;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>Event negotiation and event operations for the calling manager.</summary>
[McpServerToolType]
public sealed class EventTools
{
    /// <summary>Proposes a new four-hour attendee-facing event window.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The event proposal handler.</param>
    /// <param name="date">The transitional-location calendar date, yyyy-MM-dd.</param>
    /// <param name="startTime">The window start, HH:mm.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new proposal identifier.</returns>
    [McpServerTool(Name = "propose_event", Title = "Propose event", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Propose a new four-hour event window. Caller must be a manager.")]
    public async Task<Guid> ProposeEventAsync(
        ICallerAccessor caller,
        ProposeEventHandler handler,
        [Description("Head-office calendar date, yyyy-MM-dd.")] string date,
        [Description("Window start time, HH:mm.")] string startTime,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(date, out var parsedDate) ||
            !TimeOnly.TryParse(startTime, out var parsedStart))
        {
            throw new ModelContextProtocol.McpException(
                "A yyyy-MM-dd date and HH:mm startTime are required.");
        }

        var result = await handler.HandleAsync(
            new ProposeEventCommand(caller.RequireStaffUserId(), parsedDate, parsedStart),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Records or revises the calling manager's acceptance of a proposal.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The acceptance handler.</param>
    /// <param name="proposalId">The proposal identifier.</param>
    /// <param name="headcount">The manager's headcount for their appointment type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The acceptance outcome, including the event once all three managers accept.</returns>
    [McpServerTool(Name = "accept_proposal", Title = "Accept proposal", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Accept a event proposal with your headcount, or revise your headcount while it stays open. Caller must be a manager; may confirm the event once every required type accepts.")]
    public async Task<AcceptProposalOutcome> AcceptProposalAsync(
        ICallerAccessor caller,
        AcceptProposalHandler handler,
        [Description("The proposal identifier.")] Guid proposalId,
        [Description("Headcount for your appointment type.")] int headcount,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AcceptProposalCommand(caller.RequireStaffUserId(), proposalId, headcount),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Withdraws the calling manager's acceptance while the proposal is still open.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The withdrawal handler.</param>
    /// <param name="proposalId">The proposal identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "withdraw_acceptance", Title = "Withdraw acceptance", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Withdraw your acceptance of an open event proposal. Caller must be a manager; removes only your acceptance.")]
    public async Task<string> WithdrawAcceptanceAsync(
        ICallerAccessor caller,
        WithdrawAcceptanceHandler handler,
        [Description("The proposal identifier.")] Guid proposalId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), proposalId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Acceptance withdrawn.";
    }

    /// <summary>Withdraws a proposal created by the calling manager.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The withdrawal handler.</param>
    /// <param name="proposalId">The proposal identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "withdraw_proposal", Title = "Withdraw proposal", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Withdraw one of your own open event proposals. Caller must be the proposing manager.")]
    public async Task<string> WithdrawProposalAsync(
        ICallerAccessor caller,
        WithdrawProposalHandler handler,
        [Description("The proposal identifier.")] Guid proposalId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new WithdrawProposalCommand(caller.RequireStaffUserId(), proposalId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Proposal withdrawn.";
    }

    /// <summary>Lists open proposals and events for the calling manager's scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The event board handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The manager event board.</returns>
    [McpServerTool(Name = "event_board", Title = "Event board", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List open event proposals and your events with remaining capacity. Caller must be a manager; scoped to your appointment type.")]
    public async Task<ManagerEventBoard> GetEventBoardAsync(
        ICallerAccessor caller,
        GetManagerEventBoardHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetManagerEventBoardQuery(caller.RequireStaffUserId()), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns the attendee-free event operations view.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The event operations handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Every event visible to Admin or Coordinator, without attendee data.</returns>
    [McpServerTool(
        Name = "get_event_operations",
        Title = "Get event operations",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("List event dates, windows, capacities, status, and aggregate booking counts without attendee data. Caller must have ViewEventOperations capability (Admin or Coordinator).")]
    public async Task<EventOperationsView> GetEventOperationsAsync(
        ICallerAccessor caller,
        GetEventOperationsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetEventOperationsQuery(caller.RequireStaffUserId()), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Replaces the total headcount for the calling manager's type on a eventItem.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The capacity handler.</param>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="totalHeadcount">The new positive total covering every active booking.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated capacity.</returns>
    [McpServerTool(Name = "adjust_event_capacity", Title = "Adjust event capacity", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Replace the total headcount for your appointment type on an active eventItem. Caller must be a manager; must cover every active booking.")]
    public async Task<AdjustEventCapacityOutcome> AdjustEventCapacityAsync(
        ICallerAccessor caller,
        AdjustEventCapacityHandler handler,
        [Description("The event identifier.")] Guid eventId,
        [Description("New positive total headcount.")] int totalHeadcount,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AdjustEventCapacityCommand(
                caller.RequireStaffUserId(), eventId, totalHeadcount),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels a eventItem, optionally cascading to its active bookings.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancellation handler.</param>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="confirm">Whether cancellation of active bookings is authorized.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation outcome.</returns>
    [McpServerTool(Name = "cancel_event", Title = "Cancel event", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel a eventItem. Caller must be a manager; set confirm to true to also void its active bookings.")]
    public async Task<CancelEventOutcome> CancelEventAsync(
        ICallerAccessor caller,
        CancelEventHandler handler,
        [Description("The event identifier.")] Guid eventId,
        [Description("Whether active bookings may be voided.")] bool confirm,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelEventCommand(caller.RequireStaffUserId(), eventId, confirm),
            cancellationToken);
        return result.ValueOrThrow();
    }
}
