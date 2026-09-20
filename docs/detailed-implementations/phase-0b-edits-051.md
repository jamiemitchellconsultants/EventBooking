# 00b — Vocabulary edits 51 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Mcp/Tools/SlotTools.cs — 1/1

<!-- vocabulary-file: {"id":177,"oldPath":"src/EventBooking.Mcp/Tools/SlotTools.cs","newPath":"src/EventBooking.Mcp/Tools/EventTools.cs","beforeSha":"eb1d9526afe9028cb9e34604c941db51ae69d95fd06c00699548bc609da6379f","afterSha":"aded52e7de9d9adb29759e9cb0c304febdfb0ee22e7571ce38fb9491abf712d0","side":"before","part":1,"parts":1} -->

`````csharp
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
`````

## after — src/EventBooking.Mcp/Tools/EventTools.cs — 1/1

<!-- vocabulary-file: {"id":177,"oldPath":"src/EventBooking.Mcp/Tools/SlotTools.cs","newPath":"src/EventBooking.Mcp/Tools/EventTools.cs","beforeSha":"eb1d9526afe9028cb9e34604c941db51ae69d95fd06c00699548bc609da6379f","afterSha":"aded52e7de9d9adb29759e9cb0c304febdfb0ee22e7571ce38fb9491abf712d0","side":"after","part":1,"parts":1} -->

`````csharp
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

    /// <summary>Bulk-imports already-agreed events from CSV.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The import handler.</param>
    /// <param name="csv">The CSV content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import outcome.</returns>
    [McpServerTool(Name = "import_events", Title = "Import events", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Bulk-import already-agreed events from CSV. Caller must be an admin or coordinator.")]
    public async Task<EventImportOutcome> ImportEventsAsync(
        ICallerAccessor caller,
        ImportEventsHandler handler,
        [Description("CSV content with one already-agreed event per row.")] string csv,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ImportEventsCommand(caller.RequireStaffUserId(), csv),
            cancellationToken);
        return result.ValueOrThrow();
    }
}
`````

## before — src/EventBooking.Mcp/appsettings.Local.json — 1/1

<!-- vocabulary-file: {"id":178,"oldPath":"src/EventBooking.Mcp/appsettings.Local.json","newPath":"src/EventBooking.Mcp/appsettings.Local.json","beforeSha":"b42048a31267f7418519ee1ba7a6f5e444a85f6c5d06f8298fb2d9f9e9a838fb","afterSha":"19da1e0664eb87dfaae9b4953c8f16ca0ce42dc866e02982e37ebf4b38bf77a8","side":"before","part":1,"parts":1} -->

`````text
{
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=eventbooking_app;Password=eventbooking_local"
  },
  "HeadOffice": {
    "TimeZoneId": "Europe/London",
    "Address": "1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "a-local-signing-key-that-is-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "http://localhost:5002",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## after — src/EventBooking.Mcp/appsettings.Local.json — 1/1

<!-- vocabulary-file: {"id":178,"oldPath":"src/EventBooking.Mcp/appsettings.Local.json","newPath":"src/EventBooking.Mcp/appsettings.Local.json","beforeSha":"b42048a31267f7418519ee1ba7a6f5e444a85f6c5d06f8298fb2d9f9e9a838fb","afterSha":"19da1e0664eb87dfaae9b4953c8f16ca0ce42dc866e02982e37ebf4b38bf77a8","side":"after","part":1,"parts":1} -->

`````text
{
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=eventbooking_app;Password=eventbooking_local"
  },
  "TransitionalLocation": {
    "TimeZoneId": "Europe/London",
    "Address": "1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "a-local-signing-key-that-is-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "http://localhost:5002",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## before — src/EventBooking.Mcp/appsettings.json — 1/1

<!-- vocabulary-file: {"id":179,"oldPath":"src/EventBooking.Mcp/appsettings.json","newPath":"src/EventBooking.Mcp/appsettings.json","beforeSha":"a137a2b011c42fbad7d3665110e8ddb0f87c7e3258b7d47a10e2f52c67a48e64","afterSha":"32647a1d6d775e2add9a96cd276f81b72f87f52db62b8ed051c686d506f589b6","side":"before","part":1,"parts":1} -->

`````text
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=postgres;Password=postgres"
  },
  "HeadOffice": {
    "TimeZoneId": "Europe/London",
    "Address": "Corporate HQ, 1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "replace-this-with-a-real-secret-of-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "https://localhost:5001",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## after — src/EventBooking.Mcp/appsettings.json — 1/1

<!-- vocabulary-file: {"id":179,"oldPath":"src/EventBooking.Mcp/appsettings.json","newPath":"src/EventBooking.Mcp/appsettings.json","beforeSha":"a137a2b011c42fbad7d3665110e8ddb0f87c7e3258b7d47a10e2f52c67a48e64","afterSha":"32647a1d6d775e2add9a96cd276f81b72f87f52db62b8ed051c686d506f589b6","side":"after","part":1,"parts":1} -->

`````text
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=postgres;Password=postgres"
  },
  "TransitionalLocation": {
    "TimeZoneId": "Europe/London",
    "Address": "Corporate HQ, 1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "replace-this-with-a-real-secret-of-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "https://localhost:5001",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## before — src/EventBooking.SeedData/DemoEmailOptions.cs — 1/1

<!-- vocabulary-file: {"id":180,"oldPath":"src/EventBooking.SeedData/DemoEmailOptions.cs","newPath":"src/EventBooking.SeedData/DemoEmailOptions.cs","beforeSha":"18b842cde9c6996f77671f27098fc40137f6f8cb1d7b2699c4554d41a5ea3970","afterSha":"975f96593fe4210bc1e69f02dab59cf2f466f1e4eaacd084facf9a370044ccc4","side":"before","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using System.Net.Mail;
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.SeedData;

/// <summary>Validated demo SMTP settings and the API-compatible candidate link configuration.</summary>
public sealed class DemoEmailOptions
{
    private DemoEmailOptions(CandidatePortalOptions portal, TokenOptions tokens,
        EmailOptions sender, SmtpOptions smtp, HeadOfficeOptions headOffice)
    {
        Portal = portal;
        Tokens = tokens;
        Sender = sender;
        Smtp = smtp;
        HeadOffice = headOffice;
    }

    /// <summary>Gets the public candidate portal URL, office address and coordinator contact.</summary>
    public CandidatePortalOptions Portal { get; }
    /// <summary>Gets the signing key that must match the API validating candidate tokens.</summary>
    public TokenOptions Tokens { get; }
    /// <summary>Gets the sender identity used for demo messages over SMTP.</summary>
    public EmailOptions Sender { get; }
    /// <summary>Gets the Mailpit SMTP host and port reachable from this process.</summary>
    public SmtpOptions Smtp { get; }
    /// <summary>Gets the timezone used to determine future head-office dates.</summary>
    public HeadOfficeOptions HeadOffice { get; }

    /// <summary>Reads environment-style settings, with local defaults and explicit non-local keys.</summary>
    /// <param name="readSetting">Returns a setting value, or null when the key is absent.</param>
    /// <returns>Configuration validated before the host starts issuing demo invitations.</returns>
    /// <exception cref="SeedException">A required setting is missing, blank or invalid.</exception>
    public static DemoEmailOptions From(Func<string, string?> readSetting)
    {
        string Read(string key, string? fallback)
        {
            var value = readSetting(key) ?? fallback;
            if (string.IsNullOrWhiteSpace(value))
                throw Invalid(key);
            return value;
        }

        var baseUrl = Read("Portal__BaseUrl", "http://localhost:5002");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw Invalid("Portal__BaseUrl");

        var signingKey = Read("Tokens__SigningKey", uri.IsLoopback
            ? "a-local-signing-key-that-is-at-least-32-characters" : null);
        if (signingKey.Length < 32)
            throw Invalid("Tokens__SigningKey");
        var smtpHost = Read("Email__Smtp__Host", uri.IsLoopback ? "localhost" : null);
        var portText = Read("Email__Smtp__Port", "1025");
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port < 1 || port > 65535)
            throw Invalid("Email__Smtp__Port");
        var address = Read("Email__FromAddress", "recruitment@example.com");
        if (!MailAddress.TryCreate(address, out var mailbox)
            || !string.Equals(mailbox.Address, address, StringComparison.Ordinal))
            throw Invalid("Email__FromAddress");
        var name = Read("Email__FromName", "Recruitment Team");
        var timezone = Read("HeadOffice__TimeZoneId", "Europe/London");
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw Invalid("HeadOffice__TimeZoneId");
        }
        catch (InvalidTimeZoneException)
        {
            throw Invalid("HeadOffice__TimeZoneId");
        }
        return new DemoEmailOptions(
            new CandidatePortalOptions(uri.AbsoluteUri.TrimEnd('/'),
                Read("HeadOffice__Address", "1 Example Street, London"),
                Read("Portal__CoordinatorContact", "recruitment@example.com")),
            new TokenOptions(signingKey),
            new EmailOptions(address, name, EmailProvider.Smtp),
            new SmtpOptions(smtpHost, port),
            new HeadOfficeOptions(timezone));
    }

    private static SeedException Invalid(string key) =>
        new($"Demo email setting '{key}' is missing or invalid.");

    /// <summary>Describes the configuration without exposing credentials or deployment values.</summary>
    public override string ToString() => "Demo email configuration (values redacted)";
}
`````

## after — src/EventBooking.SeedData/DemoEmailOptions.cs — 1/1

<!-- vocabulary-file: {"id":180,"oldPath":"src/EventBooking.SeedData/DemoEmailOptions.cs","newPath":"src/EventBooking.SeedData/DemoEmailOptions.cs","beforeSha":"18b842cde9c6996f77671f27098fc40137f6f8cb1d7b2699c4554d41a5ea3970","afterSha":"975f96593fe4210bc1e69f02dab59cf2f466f1e4eaacd084facf9a370044ccc4","side":"after","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using System.Net.Mail;
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.SeedData;

/// <summary>Validated demo SMTP settings and the API-compatible attendee link configuration.</summary>
public sealed class DemoEmailOptions
{
    private DemoEmailOptions(AttendeePortalOptions portal, TokenOptions tokens,
        EmailOptions sender, SmtpOptions smtp, TransitionalLocationOptions transitionalLocation)
    {
        Portal = portal;
        Tokens = tokens;
        Sender = sender;
        Smtp = smtp;
        TransitionalLocation = transitionalLocation;
    }

    /// <summary>Gets the public attendee portal URL, office address and coordinator contact.</summary>
    public AttendeePortalOptions Portal { get; }
    /// <summary>Gets the signing key that must match the API validating attendee tokens.</summary>
    public TokenOptions Tokens { get; }
    /// <summary>Gets the sender identity used for demo messages over SMTP.</summary>
    public EmailOptions Sender { get; }
    /// <summary>Gets the Mailpit SMTP host and port reachable from this process.</summary>
    public SmtpOptions Smtp { get; }
    /// <summary>Gets the timezone used to determine future transitional-location dates.</summary>
    public TransitionalLocationOptions TransitionalLocation { get; }

    /// <summary>Reads environment-style settings, with local defaults and explicit non-local keys.</summary>
    /// <param name="readSetting">Returns a setting value, or null when the key is absent.</param>
    /// <returns>Configuration validated before the host starts issuing demo invitations.</returns>
    /// <exception cref="SeedException">A required setting is missing, blank or invalid.</exception>
    public static DemoEmailOptions From(Func<string, string?> readSetting)
    {
        string Read(string key, string? fallback)
        {
            var value = readSetting(key) ?? fallback;
            if (string.IsNullOrWhiteSpace(value))
                throw Invalid(key);
            return value;
        }

        var baseUrl = Read("Portal__BaseUrl", "http://localhost:5002");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw Invalid("Portal__BaseUrl");

        var signingKey = Read("Tokens__SigningKey", uri.IsLoopback
            ? "a-local-signing-key-that-is-at-least-32-characters" : null);
        if (signingKey.Length < 32)
            throw Invalid("Tokens__SigningKey");
        var smtpHost = Read("Email__Smtp__Host", uri.IsLoopback ? "localhost" : null);
        var portText = Read("Email__Smtp__Port", "1025");
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port < 1 || port > 65535)
            throw Invalid("Email__Smtp__Port");
        var address = Read("Email__FromAddress", "recruitment@example.com");
        if (!MailAddress.TryCreate(address, out var mailbox)
            || !string.Equals(mailbox.Address, address, StringComparison.Ordinal))
            throw Invalid("Email__FromAddress");
        var name = Read("Email__FromName", "Recruitment Team");
        var timezone = Read("TransitionalLocation__TimeZoneId", "Europe/London");
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw Invalid("TransitionalLocation__TimeZoneId");
        }
        catch (InvalidTimeZoneException)
        {
            throw Invalid("TransitionalLocation__TimeZoneId");
        }
        return new DemoEmailOptions(
            new AttendeePortalOptions(uri.AbsoluteUri.TrimEnd('/'),
                Read("TransitionalLocation__Address", "1 Example Street, London"),
                Read("Portal__CoordinatorContact", "recruitment@example.com")),
            new TokenOptions(signingKey),
            new EmailOptions(address, name, EmailProvider.Smtp),
            new SmtpOptions(smtpHost, port),
            new TransitionalLocationOptions(timezone));
    }

    private static SeedException Invalid(string key) =>
        new($"Demo email setting '{key}' is missing or invalid.");

    /// <summary>Describes the configuration without exposing credentials or deployment values.</summary>
    public override string ToString() => "Demo email configuration (values redacted)";
}
`````

## before — src/EventBooking.SeedData/DemoInvitationSeeder.cs — 1/1

<!-- vocabulary-file: {"id":181,"oldPath":"src/EventBooking.SeedData/DemoInvitationSeeder.cs","newPath":"src/EventBooking.SeedData/DemoInvitationSeeder.cs","beforeSha":"d81e64452c351f55ec927323b799654d446717fc6e0b0a3618a224878e857fcf","afterSha":"c64bb47a1c778ceb4f5e2262c42e600062e967ec42fe88863a780397d969388e","side":"before","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Slots;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Issues demo invitations after the database seed, preserving existing candidate journeys.</summary>
/// <param name="database">Reads invitation history without changing it directly.</param>
/// <param name="candidates">Resolves the demo recipients by their seeded email address.</param>
/// <param name="slots">Finds already-imported windows without restoring their capacity.</param>
/// <param name="importSlots">Imports missing demo windows under Coordinator authorization.</param>
/// <param name="trigger">Creates initial invitations and sends only after committing.</param>
/// <param name="retry">Retries outstanding messages under the existing claim and token rules.</param>
/// <param name="clock">Determines future head-office dates and invitation usability.</param>
public sealed class DemoInvitationSeeder(
    EventBookingDbContext database,
    ICandidateRepository candidates,
    IConfirmedSlotRepository slots,
    ImportConfirmedSlotsHandler importSlots,
    TriggerInviteHandler trigger,
    RetryEmailHandler retry,
    IClock clock)
{
    /// <summary>Gets or sets a progress sink; messages omit raw tokens, URLs and email bodies.</summary>
    public TextWriter Progress { get; set; } = TextWriter.Null;

    /// <summary>Imports demo availability and sends missing or recoverable demo invitations.</summary>
    /// <param name="cancellationToken">Cancels persistence and provider calls.</param>
    /// <returns>The count of messages successfully sent or retried during this run.</returns>
    /// <exception cref="SeedException">Dates, candidate state, issuance or delivery prevent completion.</exception>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await EnsureSlotsAsync(cancellationToken);
        var recipients = DemoSeedSpec.Candidates()
            .Where(c => c.Journey == DemoCandidateJourney.Unbooked)
            .GroupBy(c => c.EmployeeGroupCode)
            .Select(g => g.First()).ToList();
        if (recipients.Count != 5)
            throw new SeedException("Demo invitations require one Unbooked example per Employee Group.");
        var sent = 0;
        foreach (var spec in recipients)
        {
            var candidate = await candidates.GetByEmailAsync(spec.Email, cancellationToken)
                ?? throw new SeedException($"Seed candidate is missing: {spec.Email}.");
            if (candidate.Status is CandidateStatus.Booked)
            {
                Report($"Preserved existing journey: {spec.Email}.");
                continue;
            }
            if (await database.Invites.AnyAsync(i => i.CandidateId == candidate.Id, cancellationToken))
            {
                var pending = await database.Invites.AsNoTracking().SingleOrDefaultAsync(
                    i => i.CandidateId == candidate.Id && i.Status == InviteStatus.Pending
                        && i.RecoveryOfBookingId == null, cancellationToken);
                if (pending is null || !pending.IsUsableAt(clock.UtcNow))
                {
                    Report($"Preserved invitation history: {spec.Email}; use Coordinator actions or an explicit reseed.");
                    continue;
                }
                // Scope to the current Invite: an unresolved delivery for an older,
                // superseded Invite must not hide a successful Coordinator replacement.
                // Resolved attempts no longer describe the effective delivery outcome.
                var previous = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.CandidateId == candidate.Id && e.InviteId == pending.Id
                        && e.TemplateName == EmailTemplate.CandidateInvite && e.Status != EmailStatus.Resolved)
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (previous is null)
                    throw new SeedException($"Pending demo invitation has no matching delivery: {spec.Email}.");
                if (previous.Status is not EmailStatus.Failed and not EmailStatus.Pending)
                {
                    Report($"Invitation already delivered: {spec.Email}.");
                    continue;
                }
                // The retry handler selects candidate-wide outstanding work. Do not
                // accidentally retry another invitation/template from a mutated demo.
                var retryTarget = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.CandidateId == candidate.Id
                        && (e.Status == EmailStatus.Failed || e.Status == EmailStatus.Pending))
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (retryTarget?.Id != previous.Id)
                    throw new SeedException($"Another outstanding delivery needs Coordinator review: {spec.Email}.");
                var retried = await retry.HandleAsync(
                    new RetryEmailCommand(DemoSeedSpec.CoordinatorUserId(), candidate.Id), cancellationToken);
                if (retried.IsFailure)
                    throw new SeedException($"Demo email retry for {spec.Email} failed: {retried.Error}.");
                if (retried.Value.DeliveryStatus != EmailStatus.Sent.ToString())
                    throw DeliveryFailed(spec.Email);
            }
            else
            {
                if (candidate.Status is not CandidateStatus.NotYetInvited
                    and not CandidateStatus.AwaitingAvailability)
                    throw new SeedException($"Demo candidate has unexpected invitation state: {spec.Email}.");
                var issued = await trigger.HandleAsync(
                    new TriggerInviteCommand(DemoSeedSpec.CoordinatorUserId(), candidate.Id), cancellationToken);
                if (issued.IsFailure)
                    throw new SeedException($"Demo invitation for {spec.Email} failed: {issued.Error}.");
                if (!issued.Value.Invited)
                    throw new SeedException($"Three future slots with capacity are required for {spec.Email}.");
                if (!issued.Value.EmailSent)
                    throw DeliveryFailed(spec.Email);
            }
            sent++;
            Report($"Invitation delivered: {spec.Email}.");
        }
        return sent;
    }

    private async Task EnsureSlotsAsync(CancellationToken cancellationToken)
    {
        var dates = new[] { 3, 6, 9 }.Select(offset => DemoSeedSpec.AnchorDate().AddDays(offset)).ToArray();
        if (dates.Any(date => date <= clock.TodayAtHeadOffice))
            throw new SeedException("Demo invitation dates are stale. Use --reanchor for a fresh dataset; "
                + "use --reseed --reanchor only when deliberately resetting the demo.");
        var existing = (await slots.ListAllAsync(cancellationToken))
            .Select(slot => (slot.Window.Date, slot.Window.StartTime)).ToHashSet();
        var missing = dates.Where(date => !existing.Contains((date, new TimeOnly(11, 0)))).ToList();
        if (missing.Count == 0) return;
        var csv = "date,startTime,DAT,MED,UNI\n" + string.Join("\n", missing.Select(date =>
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ",11:00,20,20,20"));
        var imported = await importSlots.HandleAsync(
            new ImportConfirmedSlotsCommand(DemoSeedSpec.CoordinatorUserId(), csv), cancellationToken);
        if (imported.IsFailure)
            throw new SeedException($"Demo invitation slot import failed: {imported.Error}.");
        if (!imported.Value.Accepted)
            throw new SeedException("Demo invitation slot import rejected: "
                + string.Join("; ", imported.Value.Errors.Select(error => error.Message)));
        Report($"Invitation demo slots imported: {imported.Value.ImportedCount}.");
    }

    private static SeedException DeliveryFailed(string recipient) => new(
        $"Demo invitation delivery failed for {recipient}. Check Mailpit SMTP and rerun; "
        + "the committed invitation will be retried.");

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");
}
`````

## after — src/EventBooking.SeedData/DemoInvitationSeeder.cs — 1/1

<!-- vocabulary-file: {"id":181,"oldPath":"src/EventBooking.SeedData/DemoInvitationSeeder.cs","newPath":"src/EventBooking.SeedData/DemoInvitationSeeder.cs","beforeSha":"d81e64452c351f55ec927323b799654d446717fc6e0b0a3618a224878e857fcf","afterSha":"c64bb47a1c778ceb4f5e2262c42e600062e967ec42fe88863a780397d969388e","side":"after","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Events;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Issues demo invitations after the database seed, preserving existing attendee journeys.</summary>
/// <param name="database">Reads invitation history without changing it directly.</param>
/// <param name="attendees">Resolves the demo recipients by their seeded email address.</param>
/// <param name="events">Finds already-imported windows without restoring their capacity.</param>
/// <param name="importEvents">Imports missing demo windows under Coordinator authorization.</param>
/// <param name="trigger">Creates initial invitations and sends only after committing.</param>
/// <param name="retry">Retries outstanding messages under the existing claim and token rules.</param>
/// <param name="clock">Determines future transitional-location dates and invitation usability.</param>
public sealed class DemoInvitationSeeder(
    EventBookingDbContext database,
    IAttendeeRepository attendees,
    IEventRepository events,
    ImportEventsHandler importEvents,
    TriggerInviteHandler trigger,
    RetryEmailHandler retry,
    IClock clock)
{
    /// <summary>Gets or sets a progress sink; messages omit raw tokens, URLs and email bodies.</summary>
    public TextWriter Progress { get; set; } = TextWriter.Null;

    /// <summary>Imports demo availability and sends missing or recoverable demo invitations.</summary>
    /// <param name="cancellationToken">Cancels persistence and provider calls.</param>
    /// <returns>The count of messages successfully sent or retried during this run.</returns>
    /// <exception cref="SeedException">Dates, attendee state, issuance or delivery prevent completion.</exception>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await EnsureEventsAsync(cancellationToken);
        var recipients = DemoSeedSpec.Attendees()
            .Where(c => c.Journey == DemoAttendeeJourney.Unbooked)
            .GroupBy(c => c.AttendeeGroupCode)
            .Select(g => g.First()).ToList();
        if (recipients.Count != 5)
            throw new SeedException("Demo invitations require one Unbooked example per Attendee Group.");
        var sent = 0;
        foreach (var spec in recipients)
        {
            var attendee = await attendees.GetByEmailAsync(spec.Email, cancellationToken)
                ?? throw new SeedException($"Seed attendee is missing: {spec.Email}.");
            if (attendee.Status is AttendeeStatus.Booked)
            {
                Report($"Preserved existing journey: {spec.Email}.");
                continue;
            }
            if (await database.Invites.AnyAsync(i => i.AttendeeId == attendee.Id, cancellationToken))
            {
                var pending = await database.Invites.AsNoTracking().SingleOrDefaultAsync(
                    i => i.AttendeeId == attendee.Id && i.Status == InviteStatus.Pending
                        && i.RecoveryOfBookingId == null, cancellationToken);
                if (pending is null || !pending.IsUsableAt(clock.UtcNow))
                {
                    Report($"Preserved invitation history: {spec.Email}; use Coordinator actions or an explicit reseed.");
                    continue;
                }
                // Scope to the current Invite: an unresolved delivery for an older,
                // superseded Invite must not hide a successful Coordinator replacement.
                // Resolved attempts no longer describe the effective delivery outcome.
                var previous = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.AttendeeId == attendee.Id && e.InviteId == pending.Id
                        && e.TemplateName == EmailTemplate.AttendeeInvite && e.Status != EmailStatus.Resolved)
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (previous is null)
                    throw new SeedException($"Pending demo invitation has no matching delivery: {spec.Email}.");
                if (previous.Status is not EmailStatus.Failed and not EmailStatus.Pending)
                {
                    Report($"Invitation already delivered: {spec.Email}.");
                    continue;
                }
                // The retry handler selects attendee-wide outstanding work. Do not
                // accidentally retry another invitation/template from a mutated demo.
                var retryTarget = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.AttendeeId == attendee.Id
                        && (e.Status == EmailStatus.Failed || e.Status == EmailStatus.Pending))
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (retryTarget?.Id != previous.Id)
                    throw new SeedException($"Another outstanding delivery needs Coordinator review: {spec.Email}.");
                var retried = await retry.HandleAsync(
                    new RetryEmailCommand(DemoSeedSpec.CoordinatorUserId(), attendee.Id), cancellationToken);
                if (retried.IsFailure)
                    throw new SeedException($"Demo email retry for {spec.Email} failed: {retried.Error}.");
                if (retried.Value.DeliveryStatus != EmailStatus.Sent.ToString())
                    throw DeliveryFailed(spec.Email);
            }
            else
            {
                if (attendee.Status is not AttendeeStatus.NotYetInvited
                    and not AttendeeStatus.AwaitingAvailability)
                    throw new SeedException($"Demo attendee has unexpected invitation state: {spec.Email}.");
                var issued = await trigger.HandleAsync(
                    new TriggerInviteCommand(DemoSeedSpec.CoordinatorUserId(), attendee.Id), cancellationToken);
                if (issued.IsFailure)
                    throw new SeedException($"Demo invitation for {spec.Email} failed: {issued.Error}.");
                if (!issued.Value.Invited)
                    throw new SeedException($"Three future events with capacity are required for {spec.Email}.");
                if (!issued.Value.EmailSent)
                    throw DeliveryFailed(spec.Email);
            }
            sent++;
            Report($"Invitation delivered: {spec.Email}.");
        }
        return sent;
    }

    private async Task EnsureEventsAsync(CancellationToken cancellationToken)
    {
        var dates = new[] { 3, 6, 9 }.Select(offset => DemoSeedSpec.AnchorDate().AddDays(offset)).ToArray();
        if (dates.Any(date => date <= clock.TodayAtTransitionalLocation))
            throw new SeedException("Demo invitation dates are stale. Use --reanchor for a fresh dataset; "
                + "use --reseed --reanchor only when deliberately resetting the demo.");
        var existing = (await events.ListAllAsync(cancellationToken))
            .Select(eventItem => (eventItem.Window.Date, eventItem.Window.StartTime)).ToHashSet();
        var missing = dates.Where(date => !existing.Contains((date, new TimeOnly(11, 0)))).ToList();
        if (missing.Count == 0) return;
        var csv = "date,startTime,DAT,MED,UNI\n" + string.Join("\n", missing.Select(date =>
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ",11:00,20,20,20"));
        var imported = await importEvents.HandleAsync(
            new ImportEventsCommand(DemoSeedSpec.CoordinatorUserId(), csv), cancellationToken);
        if (imported.IsFailure)
            throw new SeedException($"Demo invitation event import failed: {imported.Error}.");
        if (!imported.Value.Accepted)
            throw new SeedException("Demo invitation event import rejected: "
                + string.Join("; ", imported.Value.Errors.Select(error => error.Message)));
        Report($"Invitation demo events imported: {imported.Value.ImportedCount}.");
    }

    private static SeedException DeliveryFailed(string recipient) => new(
        $"Demo invitation delivery failed for {recipient}. Check Mailpit SMTP and rerun; "
        + "the committed invitation will be retried.");

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");
}
`````

## before — src/EventBooking.SeedData/DemoSeedSpec.cs — 1/1

<!-- vocabulary-file: {"id":182,"oldPath":"src/EventBooking.SeedData/DemoSeedSpec.cs","newPath":"src/EventBooking.SeedData/DemoSeedSpec.cs","beforeSha":"29a7c06109e485291124b201658d17e474a012db587aaae45b01fb028affeacd","afterSha":"e84cc5428d0f3adda53323a6c52264abe66b9e31740c76744f76af08774247e0","side":"before","part":1,"parts":1} -->

`````csharp
using System.Reflection;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.SeedData;

/// <summary>The deterministic lifecycle state constructed for a demo Candidate.</summary>
public enum DemoCandidateJourney
{
    /// <summary>The Candidate has a group but no Booking.</summary>
    Unbooked = 1,
    /// <summary>Every current requirement has a Completed attempt.</summary>
    Ready = 2,
    /// <summary>At least one current requirement is Expected or CheckedIn.</summary>
    Outstanding = 3,
    /// <summary>At least one current requirement has a recoverable NoShow.</summary>
    NoShow = 4,
    /// <summary>A recovery Booking has completed a previously missed requirement.</summary>
    RecoveryCompleted = 5,
}

/// <summary>One deterministic Candidate seed assignment and requested demo journey.</summary>
/// <param name="Name">The demo Candidate full name.</param>
/// <param name="Email">The demo Candidate email address.</param>
/// <param name="EmployeeGroupCode">The canonical Employee Group code.</param>
/// <param name="Journey">The deterministic lifecycle state to construct.</param>
public sealed record CandidateSpec(
    string Name,
    string Email,
    string EmployeeGroupCode,
    DemoCandidateJourney Journey);

public sealed record AgreedSlotSpec(DateOnly Date, TimeOnly StartTime, int DatHeadcount, int MedHeadcount, int UniHeadcount);

public sealed record OpenProposalSpec(
    DateOnly Date,
    TimeOnly StartTime,
    Guid CreatedByManagerUserId,
    int? DatHeadcount,
    int? MedHeadcount,
    int? UniHeadcount);



/// <summary>One deterministic demo access profile and its provider identity fields.</summary>
/// <param name="Username">The unique Keycloak username created for the demo identity.</param>
/// <param name="UserId">The stable identity-provider object identifier.</param>
/// <param name="StaffId">The canonical HR-issued staff number.</param>
/// <param name="Roles">The identity-provider roles mirrored for the staff user.</param>
/// <param name="AppointmentTypeId">The optional EventBooking-owned appointment-type scope.</param>
public sealed record StaffProfileSpec(
    string Username,
    Guid UserId,
    StaffId StaffId,
    IReadOnlyList<Role> Roles,
    Guid? AppointmentTypeId)
{
    /// <summary>Gets the unique Keycloak username created for the demo identity.</summary>
    public string Username { get; init; } = Username;

    /// <summary>Gets the stable identity-provider object identifier.</summary>
    public Guid UserId { get; init; } = UserId;

    /// <summary>Gets the canonical HR-issued staff number.</summary>
    public StaffId StaffId { get; init; } = StaffId;

    /// <summary>Gets the identity-provider roles mirrored for the staff user.</summary>
    public IReadOnlyList<Role> Roles { get; init; } = Roles;

    /// <summary>Gets the EventBooking-owned appointment-type scope, or null when unscoped.</summary>
    public Guid? AppointmentTypeId { get; init; } = AppointmentTypeId;
}

/// <summary>
/// The demo dataset, loaded from the embedded demo-seed.json so it can be edited without
/// touching code. Day offsets resolve against the file's anchor date, so every run matches
/// the same windows and re-runs only ever fill in newly edited placeholders.
/// </summary>
public static class DemoSeedSpec
{
    private static readonly Lazy<SeedDocument> Document = new(Load);

    private static DateOnly? AnchorOverride;

    /// <summary>
    /// Gets the fixed calendar date against which the demo dataset's day offsets resolve.
    /// </summary>
    public static DateOnly AnchorDate() => AnchorOverride ?? Document.Value.Anchor;

    /// <summary>Gets whether a run override replaced the file anchor.</summary>
    public static bool AnchorOverridden => AnchorOverride.HasValue;

    /// <summary>
    /// Overrides the file anchor for this run (the --reanchor option), or restores file
    /// behavior with null. Applies to agreed slots, proposals, and journey windows alike.
    /// </summary>
    public static void OverrideAnchor(DateOnly? anchor) => AnchorOverride = anchor;

    public static IReadOnlyList<StaffProfileSpec> Staff() => Document.Value.Staff;

    public static Guid AdminUserId() =>
        Document.Value.Staff.Single(profile => profile.Roles.SequenceEqual([Role.Admin])).UserId;

    public static Guid CoordinatorUserId() =>
        Document.Value.Staff.Single(profile =>
            profile.Roles.Contains(Role.Coordinator)
            && !profile.Roles.Contains(Role.Manager)
            && !profile.Roles.Contains(Role.AppointmentStaff)).UserId;

    public static IReadOnlyDictionary<Guid, Guid> ManagerForType() =>
        Document.Value.Staff
            .Where(profile => profile.Roles.Contains(Role.Manager))
            .ToDictionary(profile => profile.AppointmentTypeId!.Value, profile => profile.UserId);

    public static IReadOnlyList<AgreedSlotSpec> AgreedSlots() =>
        Document.Value.AgreedSlots
            .Select(s => new AgreedSlotSpec(
                AnchorDate().AddDays(s.DaysOffset), ParseStartTime(s.StartTime),
                s.DatHeadcount, s.MedHeadcount, s.UniHeadcount))
            .ToList();

    public static IReadOnlyList<OpenProposalSpec> OpenProposals() =>
        Document.Value.OpenProposals
            .Select(p => new OpenProposalSpec(
                AnchorDate().AddDays(p.DaysOffset), ParseStartTime(p.StartTime),
                ManagerId(p.CreatedBy), p.DatHeadcount, p.MedHeadcount, p.UniHeadcount))
            .ToList();

    public static IReadOnlyList<CandidateSpec> Candidates() =>
        Document.Value.Candidates
            .Select(c => new CandidateSpec(
                c.Name, c.Email, GroupCode(c.EmployeeGroup), Journey(c.Journey)))
            .ToList();

    private static string GroupCode(string code) =>
        EmployeeGroupIds.TryFromCode(code, out var id)
            ? EmployeeGroupIds.CodeOf(id)
            : throw new SeedException($"Unknown employee group code '{code}' in demo-seed.json.");

    private static DemoCandidateJourney Journey(string value) =>
        Enum.TryParse<DemoCandidateJourney>(value, ignoreCase: true, out var journey)
            && Enum.IsDefined(journey)
            ? journey
            : throw new SeedException(
                $"Journey '{value}' in demo-seed.json must be one of Unbooked, Ready, Outstanding, NoShow, or RecoveryCompleted.");

    private static Guid ManagerId(string username) =>
        Document.Value.StaffByUsername.TryGetValue(username, out var userId)
            ? userId
            : throw new SeedException($"Unknown staff username '{username}' in demo-seed.json.");

    private static Guid TypeId(string code) =>
        AppointmentTypeIds.TryFromCode(code, out var id)
            ? id
            : throw new SeedException($"Unknown appointment type code '{code}' in demo-seed.json.");

    private static TimeOnly ParseStartTime(string value) =>
        TimeOnly.TryParseExact(
            value, "HH:mm", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var startTime)
            ? startTime
            : throw new SeedException($"Start time '{value}' in demo-seed.json must read HH:mm.");

    private static SeedDocument Load()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EventBooking.SeedData.demo-seed.json")
            ?? throw new SeedException("Embedded demo-seed.json is missing.");

        var document = JsonSerializer.Deserialize<SeedFile>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new SeedException("demo-seed.json is empty.");

        var staffById = new HashSet<Guid>();
        var staffIds = new HashSet<StaffId>();
        var staff = document.Staff.Select(row =>
        {
            if (!Guid.TryParse(row.UserId, out var userId) || userId == Guid.Empty)
            {
                throw new SeedException($"Staff entry '{row.Username}' has an invalid userId.");
            }

            if (!staffById.Add(userId))
            {
                throw new SeedException($"Duplicate staff userId '{row.UserId}'.");
            }

            var parsedRoles = row.Roles.Select(roleName =>
            {
                if (!Enum.TryParse<Role>(roleName, ignoreCase: true, out var role)
                    || !Enum.IsDefined(role))
                {
                    throw new SeedException(
                        $"Staff entry '{row.Username}' has an unknown role '{roleName}'.");
                }

                return role;
            }).ToList();

            Guid? appointmentTypeId = row.AppointmentType is null
                ? null
                : TypeId(row.AppointmentType);

            try
            {
                var staffId = new StaffId(row.StaffId);
                if (!staffIds.Add(staffId))
                {
                    throw new SeedException($"Duplicate staffId '{row.StaffId}'.");
                }

                var validated = StaffAccessProfile.Create(
                    userId, parsedRoles, appointmentTypeId);
                return new StaffRow(
                    row.Username,
                    new StaffProfileSpec(
                        row.Username,
                        userId,
                        staffId,
                        validated.Roles.OrderBy(role => role).ToList(),
                        validated.AppointmentTypeId));
            }
            catch (DomainException exception)
            {
                throw new SeedException(
                    $"Staff entry '{row.Username}' is invalid: {exception.Message}");
            }
        }).ToList();

        if (!DateOnly.TryParseExact(
                document.AnchorDate, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var anchor))
        {
            throw new SeedException("anchorDate in demo-seed.json must read yyyy-MM-dd.");
        }

        return new SeedDocument(
            anchor,
            staff.Select(s => s.Assignment).ToList(),
            staff.ToDictionary(s => s.Username, s => s.Assignment.UserId),
            document.AgreedSlots,
            document.OpenProposals,
            document.Candidates);
    }

    private sealed record StaffRow(string Username, StaffProfileSpec Assignment);

    private sealed record SeedDocument(
        DateOnly Anchor,
        IReadOnlyList<StaffProfileSpec> Staff,
        IReadOnlyDictionary<string, Guid> StaffByUsername,
        IReadOnlyList<AgreedSlotRow> AgreedSlots,
        IReadOnlyList<OpenProposalRow> OpenProposals,
        IReadOnlyList<CandidateRow> Candidates);

    private sealed record SeedFile(
        string AnchorDate,
        List<StaffRowFile> Staff,
        List<AgreedSlotRow> AgreedSlots,
        List<OpenProposalRow> OpenProposals,
        List<CandidateRow> Candidates);

    private sealed record StaffRowFile(
        string Username,
        string UserId,
        string StaffId,
        List<string> Roles,
        string? AppointmentType);

    private sealed record AgreedSlotRow(
        int DaysOffset, string StartTime, int DatHeadcount, int MedHeadcount, int UniHeadcount);

    private sealed record OpenProposalRow(
        int DaysOffset, string StartTime, string CreatedBy,
        int? DatHeadcount, int? MedHeadcount, int? UniHeadcount);

    private sealed record CandidateRow(string Name, string Email, string EmployeeGroup, string Journey);
}
`````
