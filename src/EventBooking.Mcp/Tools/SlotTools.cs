using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Slots;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>Slot negotiation and confirmed-slot operations for the calling manager.</summary>
[McpServerToolType]
public sealed class SlotTools
{
    /// <summary>Proposes a new four-hour candidate-facing slot window.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The slot proposal handler.</param>
    /// <param name="date">The head-office calendar date, yyyy-MM-dd.</param>
    /// <param name="startTime">The window start, HH:mm.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new proposal identifier.</returns>
    [McpServerTool(Name = "propose_slot", Title = "Propose slot", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Propose a new four-hour slot window. Caller must be a manager.")]
    public async Task<Guid> ProposeSlotAsync(
        ICallerAccessor caller,
        ProposeSlotHandler handler,
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
            new ProposeSlotCommand(caller.RequireStaffUserId(), parsedDate, parsedStart),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Records or revises the calling manager's acceptance of a proposal.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The acceptance handler.</param>
    /// <param name="proposalId">The proposal identifier.</param>
    /// <param name="headcount">The manager's headcount for their appointment type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The acceptance outcome, including the confirmed slot once all three managers accept.</returns>
    [McpServerTool(Name = "accept_proposal", Title = "Accept proposal", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Accept a slot proposal with your headcount, or revise your headcount while it stays open. Caller must be a manager; may confirm the slot once every required type accepts.")]
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
    [Description("Withdraw your acceptance of an open slot proposal. Caller must be a manager; removes only your acceptance.")]
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
    [Description("Withdraw one of your own open slot proposals. Caller must be the proposing manager.")]
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

    /// <summary>Lists open proposals and confirmed slots for the calling manager's scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The slot board handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The manager slot board.</returns>
    [McpServerTool(Name = "slot_board", Title = "Slot board", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List open slot proposals and your confirmed slots with remaining capacity. Caller must be a manager; scoped to your appointment type.")]
    public async Task<ManagerSlotBoard> GetSlotBoardAsync(
        ICallerAccessor caller,
        GetManagerSlotBoardHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetManagerSlotBoardQuery(caller.RequireStaffUserId()), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns the candidate-free confirmed-slot operations view.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The slot operations handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Every confirmed slot visible to Admin or Coordinator, without candidate data.</returns>
    [McpServerTool(
        Name = "get_slot_operations",
        Title = "Get slot operations",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("List confirmed-slot dates, windows, capacities, status, and aggregate booking counts without candidate data. Caller must have ViewSlotOperations capability (Admin or Coordinator).")]
    public async Task<SlotOperationsView> GetSlotOperationsAsync(
        ICallerAccessor caller,
        GetSlotOperationsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetSlotOperationsQuery(caller.RequireStaffUserId()), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Replaces the total headcount for the calling manager's type on a slot.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The capacity handler.</param>
    /// <param name="confirmedSlotId">The confirmed slot identifier.</param>
    /// <param name="totalHeadcount">The new positive total covering every active booking.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated capacity.</returns>
    [McpServerTool(Name = "adjust_slot_capacity", Title = "Adjust slot capacity", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Replace the total headcount for your appointment type on an active confirmed slot. Caller must be a manager; must cover every active booking.")]
    public async Task<AdjustConfirmedSlotCapacityOutcome> AdjustSlotCapacityAsync(
        ICallerAccessor caller,
        AdjustConfirmedSlotCapacityHandler handler,
        [Description("The confirmed slot identifier.")] Guid confirmedSlotId,
        [Description("New positive total headcount.")] int totalHeadcount,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AdjustConfirmedSlotCapacityCommand(
                caller.RequireStaffUserId(), confirmedSlotId, totalHeadcount),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels a confirmed slot, optionally cascading to its active bookings.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancellation handler.</param>
    /// <param name="confirmedSlotId">The confirmed slot identifier.</param>
    /// <param name="confirm">Whether cancellation of active bookings is authorized.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation outcome.</returns>
    [McpServerTool(Name = "cancel_confirmed_slot", Title = "Cancel confirmed slot", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel a confirmed slot. Caller must be a manager; set confirm to true to also void its active bookings.")]
    public async Task<CancelSlotOutcome> CancelConfirmedSlotAsync(
        ICallerAccessor caller,
        CancelConfirmedSlotHandler handler,
        [Description("The confirmed slot identifier.")] Guid confirmedSlotId,
        [Description("Whether active bookings may be voided.")] bool confirm,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelConfirmedSlotCommand(caller.RequireStaffUserId(), confirmedSlotId, confirm),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Bulk-imports already-agreed confirmed slots from CSV.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The import handler.</param>
    /// <param name="csv">The CSV content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import outcome.</returns>
    [McpServerTool(Name = "import_confirmed_slots", Title = "Import confirmed slots", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Bulk-import already-agreed confirmed slots from CSV. Caller must be an admin or coordinator.")]
    public async Task<ConfirmedSlotImportOutcome> ImportConfirmedSlotsAsync(
        ICallerAccessor caller,
        ImportConfirmedSlotsHandler handler,
        [Description("CSV content with one already-agreed slot per row.")] string csv,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ImportConfirmedSlotsCommand(caller.RequireStaffUserId(), csv),
            cancellationToken);
        return result.ValueOrThrow();
    }
}
