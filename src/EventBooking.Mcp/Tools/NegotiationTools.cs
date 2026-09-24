using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Api.Pagination;
using EventBooking.Application.Negotiation;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>The five proposal tools. Every one is scoped to the caller's own type.</summary>
[McpServerToolType]
public sealed class NegotiationTools
{
    /// <summary>Lists the caller's type's proposals.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="cursors">The REST cursor signer.</param>
    /// <param name="status">The status filter, or null for every status.</param>
    /// <param name="locationId">The location filter, or null for every site.</param>
    /// <param name="cursor">The page cursor, or null for the first page.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One page of proposals.</returns>
    [McpServerTool(
        Name = "list_event_proposals", Title = "List event proposals",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads the proposals listing the caller's own appointment type (FR-2.13), filtered by status and location, one keyset page at a time.")]
    public async Task<EventProposalListView> ListEventProposalsAsync(
        ICallerAccessor caller,
        ListEventProposalsHandler handler,
        PageCursor cursors,
        [Description("Page size, 1 to 200.")] int limit = 50,
        [Description("Open, Confirmed or Withdrawn; omit for every status.")] string? status = null,
        [Description("Narrow to one location.")] Guid? locationId = null,
        [Description("The nextCursor from the previous page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListEventProposalsQuery(
                caller.RequireStaffUserId(), status, locationId, cursors.Unwrap(cursor), limit),
            cancellationToken);
        var page = result.ValueOrThrow();
        return new EventProposalListView(
            [.. page.Items.Select(x => x with { Cursor = cursors.Protect(x.Cursor) })],
            cursors.Wrap(page.NextCursor));
    }

    /// <summary>Proposes an event.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The propose handler.</param>
    /// <param name="locationId">Where the event would be held.</param>
    /// <param name="date">The local calendar date, yyyy-MM-dd.</param>
    /// <param name="startTime">The local start time, HH:mm.</param>
    /// <param name="durationMinutes">The window length.</param>
    /// <param name="appointmentTypeIds">Every type the event will offer.</param>
    /// <param name="headcount">The proposer's own headcount.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The proposal, confirmed already when it lists only your type.</returns>
    [McpServerTool(
        Name = "propose_event", Title = "Propose event",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Creates a proposal for one location, window and listed appointment-type set. A proposal listing only the proposer's type is confirmed on creation.")]
    public async Task<ProposeEventOutcome> ProposeEventAsync(
        ICallerAccessor caller,
        ProposeEventHandler handler,
        [Description("The location identifier.")] Guid locationId,
        [Description("Local calendar date, yyyy-MM-dd.")] string date,
        [Description("Local start time, HH:mm.")] string startTime,
        [Description("Window length in minutes; a multiple of 15, at most 720.")] int durationMinutes,
        [Description("Every appointment type the event will offer, including your own.")]
        Guid[] appointmentTypeIds,
        [Description("Your own type's headcount, 1 to 1000.")] int headcount,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ProposeEventCommand(
                caller.RequireStaffUserId(), locationId,
                IsoInput.Date(date, nameof(date)), IsoInput.Time(startTime, nameof(startTime)),
                durationMinutes, appointmentTypeIds, headcount),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Records or revises the caller's type's acceptance.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The acceptance handler.</param>
    /// <param name="proposalId">The proposal.</param>
    /// <param name="headcount">The accepting type's headcount.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, carrying the event once the last acceptance lands.</returns>
    [McpServerTool(
        Name = "record_acceptance", Title = "Record acceptance",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Records the caller's type's acceptance with its headcount, or revises it while the proposal is open. The last missing acceptance confirms the event.")]
    public async Task<RecordAcceptanceOutcome> RecordAcceptanceAsync(
        ICallerAccessor caller,
        RecordAcceptanceHandler handler,
        [Description("The proposal identifier.")] Guid proposalId,
        [Description("Your type's headcount, 1 to 1000.")] int headcount,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new RecordAcceptanceCommand(caller.RequireStaffUserId(), proposalId, headcount),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Withdraws the caller's type's acceptance.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The withdrawal handler.</param>
    /// <param name="proposalId">The proposal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(
        Name = "withdraw_acceptance", Title = "Withdraw acceptance",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Withdraws the caller's type's acceptance while the proposal is open.")]
    public async Task<string> WithdrawAcceptanceAsync(
        ICallerAccessor caller,
        WithdrawAcceptanceHandler handler,
        [Description("The proposal identifier.")] Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), proposalId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Acceptance withdrawn.";
    }

    /// <summary>Withdraws the whole proposal.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The withdrawal handler.</param>
    /// <param name="proposalId">The proposal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(
        Name = "withdraw_proposal", Title = "Withdraw proposal",
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Withdraws the whole proposal. Judged against the proposing appointment type, so a Manager inherits it from a predecessor.")]
    public async Task<string> WithdrawProposalAsync(
        ICallerAccessor caller,
        WithdrawProposalHandler handler,
        [Description("The proposal identifier.")] Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new WithdrawProposalCommand(caller.RequireStaffUserId(), proposalId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Proposal withdrawn.";
    }
}
