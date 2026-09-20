# 00d — Retire direct event import, edits 2 (Task 3b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — docs/user-guides/coordinator-guide.md — 1/1

<!-- retirement-file: {"id":3,"file":"docs/user-guides/coordinator-guide.md","beforeSha":"8270ca6d6f5e679d917d020cd10115a6756b45ecb47b5b9e86807ebb45a5dcbb","afterSha":"4c33d20fccc8c55007bb596bf666779efa897eeb64774c2b42bdbf5e30c37fbf","side":"after","part":1,"parts":1} -->

`````text
# Coordinator guide

[← All user guides](README.md)

As a Coordinator, you own the attendee journey from initial record through invitation, booking,
readiness, and missed-appointment recovery. EventBooking derives every required Appointment Type
from the attendee's Attendee Group; you never add or remove requirements individually. You can also
cancel a attendee's booking on their behalf, cancel a whole confirmed window, and search the full
audit trail.

## Your workflow

1. Check **Dashboards** for attendees waiting for availability or follow-up.
2. Ensure at least three suitable future Confirmed Events exist. Ask Managers to negotiate and accept them.
3. Add attendees on **Attendees**, manually or with a CSV containing one Attendee Group per row.
4. Review the derived Appointment Type chips, then select **Invite now**.
5. Monitor invitation and email status until the attendee books.
6. After delivery staff record outcomes, review each attendee's readiness.
7. If the latest unsatisfied attempt is No-show, select **Arrange missed appointments** and monitor
   the recovery invitation until the missed types are completed.
8. When a plan changes, cancel the attendee's booking from their row — with or without sending
   fresh options — rather than asking them to find their emailed link.

## Home page

After signing in, the home-page access summary should include **Coordinator**. It provides:

- **Attendees** (`/attendees`)
- **Dashboards** (`/dashboards`)
- **Events** (`/events`)
- **Audit trail** (`/audit`)
- **Help** (`/help`) — every role's guide, including the attendee guide

The same links also appear as a navigation bar at the top of every page, so you can switch
workspaces without returning to the home page first.

If your profile also contains Manager or Appointment staff, the home page also shows their
workspaces. Those actions remain scoped to the one Appointment Type shown in the access summary.
Roles themselves are assigned in the company identity provider, not by a EventBooking Admin; an
Admin only sets the Appointment Type that scoped roles work within.

## Attendees screen

Use `/attendees` for attendee records, invitations, delivery status, readiness, bookings, history,
and recovery. There is no outbound onboarding integration and no readiness export: recovery is
arranged only through this screen (or the matching Coordinator API endpoints), never through MCP.

![Attendees screen with the add-attendee row and existing attendee list](screenshots/attendees-list.png)

### Find the correct work queue

- Use **Status** to filter by Not yet invited, Awaiting availability, Invited (pending response),
  Booked, No response - needs follow-up, or Cancelled.
- Enter a name or email in **Search by name or email**, then select **Search**.
- Clear the search and choose **Any status** to return to the full list.

### Add one attendee

The first table row is the add form.

1. Enter **Full name** and **Email address**.
2. Choose the required **Attendee Group**.
3. Check the read-only Appointment Type chips previewed beneath the group. These are derived from
   the approved mapping and cannot be edited.
4. Select **Save attendee**.
5. Confirm the saved row shows the expected group name/code and derived types.

![Add-attendee row filled in with a name, email, and Cabin Crew attendee group, showing the derived DAT/MED/UNI chips](screenshots/attendee-add-form.png)

The mappings are:

| Attendee Group | CSV code | Derived Appointment Types |
|---|---|---|
| Cabin Crew | `CABIN_CREW` | DAT, MED, UNI |
| Pilots | `PILOTS` | DAT, UNI |
| Ground Operations Agent | `GROUND_OPERATIONS_AGENT` | MED |
| Engineering | `ENGINEERING` | MED |
| Ground Transport Services | `GROUND_TRANSPORT_SERVICES` | DAT, MED, UNI |

### Import attendees

For a batch, prepare a CSV with the exact header:

```text
name,email,attendee_group
```

The `attendee_group` field contains one code from the table above. Surrounding whitespace and letter
case are accepted, but EventBooking stores and displays the canonical uppercase code.

1. Select **Upload CSV** and choose a `.csv` file no larger than 1 MiB.
2. Wait while every row is validated.
3. If accepted, the list reloads with the new attendees.
4. If **Nothing was imported** appears, correct every line-numbered error and upload the whole file
   again.

The import is all-or-nothing. Blank or unknown group codes, duplicate emails in the file or system,
invalid names or emails, and malformed rows prevent every row from being added. The old
`appointment_types` column is not accepted.

### Edit a attendee

1. Select **Edit** on the attendee's row.
2. Change the name, email, or Attendee Group.
3. Review the newly derived Appointment Type chips.
4. Read any warning, then select **Save**; select **Cancel** to discard the edit.

The effect of an Attendee Group change depends on the booking journey:

- If the new group derives the same Appointment Types, EventBooking preserves pending invitations,
  active bookings, capacity, and appointment history.
- If it changes the required types before booking, a pending invitation is invalidated and the
  attendee returns to Not yet invited. Select **Invite now** after saving to send correct options.
- If it changes the required types during an active original Booking, the save is blocked. Cancel
  the booking from the **Booking** column first — **Cancel & rebook** keeps the attendee moving —
  then change the group. The entered edit values remain on screen while the save is refused.

Cabin Crew and Ground Transport Services are set-equivalent to each other. Ground Operations Agent
and Engineering are also set-equivalent.

### Read the attendee row

- **Required types** contains read-only code chips derived from the Attendee Group. A legacy record
  with no group instead shows **Attendee Group required**: edit it and choose a group before
  inviting.
- **Status** shows the invitation/booking stage.
- **Readiness** gives a compact result. Expand it for the outstanding Appointment Types.
- **Delivery** shows the latest attendee email. For a retryable Failed or Pending message, select
  **Resend**.
- **Booking** loads the active Bookings on demand and holds the cancellation actions.
- **History** expands the recorded changes for that attendee.
- **Actions** contains Edit, Invite now, and Delete. Delete confirms on a second click.

### Interpret readiness

| Screen text | Meaning | Next action |
|---|---|---|
| All required appointments completed | Every current required type has a Completed outcome | No appointment action is needed |
| Not ready — no active booking | The attendee has not confirmed an active original Booking | Send or follow up an invitation |
| Not ready, with outstanding type chips | One or more required types are Expected, Checked in, or No-show | Wait for delivery, or arrange recovery if the screen offers it |
| Readiness unavailable — contact support | Stored group, Booking, and appointment snapshots do not agree | Stop; do not re-invite or edit around the warning |

Only a Completed outcome satisfies a type. Expected, Checked in, and No-show remain outstanding. An
outstanding type is labelled **(recoverable)** when its latest attempt was a No-show.

### Send an initial invitation

1. Confirm the Attendee Group and derived types are correct.
2. Select **Invite now**.
3. Wait for the row to refresh to Invited and review **Delivery**.

![Attendee row showing status Invited (pending response) and the delivery timestamp](screenshots/attendee-invited.png)

EventBooking selects exactly three future Confirmed Events with remaining capacity for every derived
type and snapshots those requirements into the invitation. If fewer than three suitable options
exist, no invitation is created and the attendee moves to Awaiting availability. Create more
capacity, then try again.

### Cancel a attendee's booking

The **Booking** column shows a **Bookings** button. Select it to load and expand the list; the
button then reads **No active booking**, **1 active booking**, or a count. Each entry shows the date and four-hour window, marked **(recovery)** when it is a
recovery Booking rather than the original.

![Expanded Booking column for a booked attendee, with Cancel & rebook armed and showing Confirm cancel](screenshots/attendee-booking.png)

Two actions are offered, each confirming on its own second click:

1. **Cancel booking** releases the places and stops there. No replacement options are sent, so the
   attendee needs a fresh invitation from you if they still need an appointment.
2. **Cancel & rebook** releases the places and emails the attendee a fresh invitation with new
   options. It appears on the original Booking only — a recovery Booking is never auto-replaced.

The result appears under the list as one of:

- **Booking cancelled.**
- **Booking cancelled; replacement invite sent.**
- **Booking cancelled; replacement invite could not be delivered.** — the cancellation still
  happened. Check the email address and follow up manually.

Cancelling the original Booking cancels that appointment journey, including any pending or active
recovery. Cancelling a recovery Booking leaves the original journey and completed work intact.

### Arrange missed appointments

**Arrange missed appointments** appears only when at least one current outstanding type is
recoverable. A type becomes recoverable when its latest non-cancelled attempt is No-show and no
Completed attempt already satisfies it.

1. Expand readiness and review the recoverable type names.
2. Select **Arrange missed appointments**.
3. Confirm the resulting message lists exactly the missed types and reports the email outcome.
4. While the recovery invitation is Pending, use **Cancel recovery** only if it must be withdrawn;
   confirm the cancellation when prompted.
5. Monitor the attendee's readiness after they book and the replacement appointments are delivered.

Recovery never repeats an already Completed type. It requires three future events with capacity for
all missed types in that recovery. Expected or Checked-in work cannot be recovered; staff must first
record the correct outcome. If the attendee misses the replacement too, a new recovery can be
arranged after that recovery Booking concludes.

### Read a attendee's history

Every attendee row carries a **History** disclosure. Expanding it loads the recorded changes to
the attendee record itself (such as an Attendee Group being assigned or changed) and to that
attendee's invitations, Bookings, and Booking Appointments, newest first, with When, What, Who, and
Details. It is loaded on demand, so opening a long attendee list costs nothing until you ask for a
history.

![Expanded History on a attendee row, listing BookingCreated, InviteSent, InviteCreated, and AttendeeGroupAssigned entries with When, What, Who, and Details](screenshots/attendee-history.png)

Use History for a single attendee's story. Use the Audit trail screen when you need to search
across attendees, dates, or actions.

## Dashboards screen

Use `/dashboards` as the start-of-day and follow-up view.

![Dashboards screen showing the Events tab with capacity by type and active booking counts](screenshots/dashboards.png)

- **Awaiting availability** lists attendees who could not receive three suitable options, their
  required types, and how long they have waited. Arrange more capacity before returning to their
  Attendee row and inviting again.
- **No response** lists attendees whose invitation follow-up window ended. Select **Re-invite now**
  after confirming their email and continued need.
- **Events** shows each Confirmed Event, capacity remaining/total by type, and active Booking count.
- Failed and Pending email totals appear beside the tabs. Resolve them from the Attendee row.
- Every tab carries the same **History** disclosure per row — per attendee on the first two tabs,
  per event on Events.

The dashboard tabs support mouse, touch, and keyboard. With focus on the tab list, use Left/Right,
Home, or End to change views.

## Events screen

Use `/events/operations` to review Events and cancel a window that cannot run. Admins see the same screen.

Event capacity is created through Manager negotiation. Direct event CSV import is not available.

### Cancel a event

The **Cancel a event** card lists every confirmed window with its capacity by type and the
number of active Bookings. Select **Cancel event**; if the window holds active Bookings the request
is refused with a warning, and the button becomes **Confirm cancel**.

![Cancel a event card listing each window's date, capacity by type, active bookings, and a Cancel event button](screenshots/events-cancel.png)

Cancelling voids every Booking on that window, releases the capacity, and starts the attendee
rebooking workflow — so tell the delivery teams first, and expect the affected attendees to need
watching afterwards. A Manager can cancel the same window from their own screen, so agree who is
acting before anyone clicks.

## Audit trail screen

Use `/audit` to search across the whole record. As a Coordinator you see both attendee entries
(attendees, invitations, bookings, and booking appointments) and operational entries (event
proposals, events, and staff access profiles).

![Audit trail screen with From, To, Actor, Action, Identifier, and Entity filters above newest-first results](screenshots/audit-trail.png)

1. Set **From** and **To** to bound the period. Both are optional.
2. Choose an **Actor**: Staff, AttendeeToken (a attendee acting through their emailed link), or
   System.
3. Choose an **Action** to narrow to one recorded change, such as InviteSent, BookingCreated,
   BookingCancelled, AppointmentMarkedNoShow, RecoveryInviteCreated, or EventCancelled.
4. Enter an **Identifier** to match one audited entity id or actor id exactly.
5. Narrow further with **Entity** when you want only one kind of record.
6. Select **Search**, then **Load more** to page further back. Results are newest first.

**Nothing matches these filters** means the search ran and found nothing, not that it failed.

## Troubleshooting

- **Invite cannot be created** — fewer than three suitable events exist, the attendee data is
  inconsistent, or another action changed state. Refresh, fix the stated dependency, and retry.
- **Invitation link is invalid** — it may be expired, used, cancelled, or superseded by an Employee
  Group change. Verify the row and send a fresh invitation where appropriate.
- **Attendee Group change is blocked** — it would alter the requirement set of an active original
  Booking. Cancel the booking from the Booking column first, then change the group.
- **Invite now is refused for a legacy record** — the row shows Attendee Group required. Edit it and
  assign a group.
- **Arrange missed appointments is absent** — no current type has a recoverable No-show, or a
  recovery invitation/Booking already exists.
- **Recovery cannot start** — create three suitable future events, correct any concurrently changed
  appointment state, or finish/cancel the existing recovery first.
- **Cancel & rebook is missing on a booking** — that Booking is a recovery. Cancel it, then arrange
  a new recovery once the appointment state allows it.
- **Email failed after an invitation was created** — the invitation remains valid. Correct the email
  address if necessary and use **Resend**; do not create duplicate attendee records.
- **Readiness unavailable** — contact support. Do not try to repair snapshot mismatches through
  repeated edits or invitations.
`````

## before — src/EventBooking.Api/Endpoints/EventEndpoints.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Api/Endpoints/EventEndpoints.cs","beforeSha":"71e9b33a96c28ae157a4a11924c978d38ca7e1ea2f4d3d4dcf3d90fbe95ff4f4","afterSha":"40f26eb32bbec07a57f100665efbec6a70c291dc8a4a1480b88d2be677550ac3","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Events;

namespace EventBooking.Api.Endpoints;

public static class EventEndpoints
{
    public sealed record ProposeEventRequest(DateOnly Date, TimeOnly StartTime);

    public sealed record AcceptProposalRequest(int Headcount);

    public sealed record AdjustEventCapacityRequest(int TotalHeadcount);

    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").RequireAuthorization(AuthenticationExtensions.StaffPolicy);
        var proposals = app.MapGroup("/api/event-proposals").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/board", async (
            ICallerAccessor caller,
            GetManagerEventBoardHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetManagerEventBoardQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(EventBoardResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getEventBoard")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/operations", async (
            ICallerAccessor caller,
            GetEventOperationsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetEventOperationsQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(EventOperationsResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getEventOperations")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        proposals.MapPost("", async (
            ProposeEventRequest request,
            ICallerAccessor caller,
            ProposeEventHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ProposeEventCommand(caller.RequireStaffUserId(), request.Date, request.StartTime),
                cancellationToken))
                .ToCreated(id => $"/api/event-proposals/{id}"))
            .WithAgentMetadata("proposeEvent")
            .Produces<Guid>(201)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        proposals.MapPost("/{id:guid}/acceptance", async (
            Guid id,
            AcceptProposalRequest request,
            ICallerAccessor caller,
            AcceptProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AcceptProposalCommand(caller.RequireStaffUserId(), id, request.Headcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("acceptProposal")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        proposals.MapDelete("/{id:guid}/acceptance", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawAcceptanceHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawAcceptance")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        proposals.MapDelete("/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawProposalCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawProposal")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPut("/{id:guid}/capacity", async (
            Guid id,
            AdjustEventCapacityRequest request,
            ICallerAccessor caller,
            AdjustEventCapacityHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AdjustEventCapacityCommand(
                    caller.RequireStaffUserId(),
                    id,
                    request.TotalHeadcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("adjustEventCapacity")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            CancelEventHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelEventCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelEvent")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        app.MapPost("/api/events/import", async (
            HttpRequest request,
            ICallerAccessor caller,
            ImportEventsHandler handler,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            return (await handler.HandleAsync(
                new ImportEventsCommand(caller.RequireStaffUserId(), csv),
                cancellationToken)).ToResponse();
        }).RequireAuthorization(AuthenticationExtensions.StaffPolicy)
            .WithAgentMetadata("importEvents")
            .Accepts<string>("text/csv")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(413)
            .ProducesProblem(415);

        return app;
    }
}
`````

## after — src/EventBooking.Api/Endpoints/EventEndpoints.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Api/Endpoints/EventEndpoints.cs","beforeSha":"71e9b33a96c28ae157a4a11924c978d38ca7e1ea2f4d3d4dcf3d90fbe95ff4f4","afterSha":"40f26eb32bbec07a57f100665efbec6a70c291dc8a4a1480b88d2be677550ac3","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Events;

namespace EventBooking.Api.Endpoints;

public static class EventEndpoints
{
    public sealed record ProposeEventRequest(DateOnly Date, TimeOnly StartTime);

    public sealed record AcceptProposalRequest(int Headcount);

    public sealed record AdjustEventCapacityRequest(int TotalHeadcount);

    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").RequireAuthorization(AuthenticationExtensions.StaffPolicy);
        var proposals = app.MapGroup("/api/event-proposals").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/board", async (
            ICallerAccessor caller,
            GetManagerEventBoardHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetManagerEventBoardQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(EventBoardResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getEventBoard")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/operations", async (
            ICallerAccessor caller,
            GetEventOperationsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new GetEventOperationsQuery(caller.RequireStaffUserId()), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(EventOperationsResourceResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getEventOperations")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        proposals.MapPost("", async (
            ProposeEventRequest request,
            ICallerAccessor caller,
            ProposeEventHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new ProposeEventCommand(caller.RequireStaffUserId(), request.Date, request.StartTime),
                cancellationToken))
                .ToCreated(id => $"/api/event-proposals/{id}"))
            .WithAgentMetadata("proposeEvent")
            .Produces<Guid>(201)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(409);

        proposals.MapPost("/{id:guid}/acceptance", async (
            Guid id,
            AcceptProposalRequest request,
            ICallerAccessor caller,
            AcceptProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AcceptProposalCommand(caller.RequireStaffUserId(), id, request.Headcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("acceptProposal")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        proposals.MapDelete("/{id:guid}/acceptance", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawAcceptanceHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawAcceptance")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        proposals.MapDelete("/{id:guid}", async (
            Guid id,
            ICallerAccessor caller,
            WithdrawProposalHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new WithdrawProposalCommand(caller.RequireStaffUserId(), id), cancellationToken))
                .ToResponse())
            .WithAgentMetadata("withdrawProposal")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapPut("/{id:guid}/capacity", async (
            Guid id,
            AdjustEventCapacityRequest request,
            ICallerAccessor caller,
            AdjustEventCapacityHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new AdjustEventCapacityCommand(
                    caller.RequireStaffUserId(),
                    id,
                    request.TotalHeadcount),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("adjustEventCapacity")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            bool? confirm,
            ICallerAccessor caller,
            CancelEventHandler handler,
            CancellationToken cancellationToken) =>
            (await handler.HandleAsync(
                new CancelEventCommand(caller.RequireStaffUserId(), id, confirm ?? false),
                cancellationToken))
                .ToResponse())
            .WithAgentMetadata("cancelEvent")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        return app;
    }
}
`````

## before — src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs","beforeSha":"6097e2dcb14e98fd1264aeb990de65895c9d2a44eef3c545987be5876f8ff8d3","afterSha":"205be03211171c7224b7d36ed7a2d40087d68285546cfc50b0d4409a89354846","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Api.OpenApi;

/// <summary>Behavior hints shared by OpenAPI and MCP discovery.</summary>
/// <param name="ReadOnly">Whether the operation performs no state change.</param>
/// <param name="Destructive">Whether the operation deletes, cancels, withdraws, clears, replaces, or transitions existing state.</param>
/// <param name="Idempotent">Whether repeating the operation has the same effect as performing it once.</param>
/// <param name="OpenWorld">Whether the operation interacts with arbitrary external entities. Always false for this tool set.</param>
public sealed record AgentHints(
    /// <summary>Gets whether the operation performs no state change.</summary>
    bool ReadOnly,
    /// <summary>Gets whether the operation deletes, cancels, withdraws, clears, replaces, or transitions existing state.</summary>
    bool Destructive,
    /// <summary>Gets whether repeating the operation has the same effect as performing it once.</summary>
    bool Idempotent,
    /// <summary>Gets whether the operation interacts with arbitrary external entities.</summary>
    bool OpenWorld);

/// <summary>One stable HTTP operation and its MCP parity decision.</summary>
/// <param name="OperationId">The unique lower-camel-case OpenAPI operation id.</param>
/// <param name="Method">The uppercase HTTP method of the operation.</param>
/// <param name="Route">The route pattern of the operation.</param>
/// <param name="Tag">The OpenAPI tag grouping the operation.</param>
/// <param name="Summary">The short human-readable summary of the operation.</param>
/// <param name="Description">The longer description stating purpose, authorization, and side effects.</param>
/// <param name="RequiresBearer">Whether the operation requires a staff bearer token.</param>
/// <param name="McpTool">The snake-case MCP tool name, or null when intentionally excluded.</param>
/// <param name="ExclusionReason">The reviewed reason for exclusion, or null when an MCP tool exists.</param>
/// <param name="Hints">The behavior hints shared by OpenAPI and MCP discovery.</param>
public sealed record AgentOperation(
    /// <summary>Gets the unique lower-camel-case OpenAPI operation id.</summary>
    string OperationId,
    /// <summary>Gets the uppercase HTTP method of the operation.</summary>
    string Method,
    /// <summary>Gets the route pattern of the operation.</summary>
    string Route,
    /// <summary>Gets the OpenAPI tag grouping the operation.</summary>
    string Tag,
    /// <summary>Gets the short human-readable summary of the operation.</summary>
    string Summary,
    /// <summary>Gets the longer description stating purpose, authorization, and side effects.</summary>
    string Description,
    /// <summary>Gets whether the operation requires a staff bearer token.</summary>
    bool RequiresBearer,
    /// <summary>Gets the snake-case MCP tool name, or null when intentionally excluded.</summary>
    string? McpTool,
    /// <summary>Gets the reviewed reason for exclusion, or null when an MCP tool exists.</summary>
    string? ExclusionReason,
    /// <summary>Gets the behavior hints shared by OpenAPI and MCP discovery.</summary>
    AgentHints Hints);

/// <summary>Shared registry of HTTP operations and their MCP parity decisions.</summary>
public static class AgentOperationCatalog
{
    /// <summary>Gets every catalogued application operation keyed by operation id.</summary>
    public static IReadOnlyDictionary<string, AgentOperation> All { get; }

    static AgentOperationCatalog()
    {
        var operations = new List<AgentOperation>
        {
            Staff("getMyAccess", HttpMethods.Get, "/api/me", "Identity", "Read the signed-in staff access.", "Reads the signed-in staff identity and capability scope. Requires a staff bearer token.", "get_my_access", Read()),
            Staff("getEventBoard", HttpMethods.Get, "/api/events/board", "Events", "Read the scoped event board.", "Reads the event board visible to the signed-in staff member. Requires a staff bearer token.", "event_board", Read()),
            Staff("getEventOperations", HttpMethods.Get, "/api/events/operations", "Events", "Read the scoped event operations view.", "Reads the event operations view visible to the signed-in staff member. Requires a staff bearer token.", "get_event_operations", Read()),
            Staff("proposeEvent", HttpMethods.Post, "/api/event-proposals", "Events", "Propose a eventItem.", "Creates a event proposal. Requires a staff bearer token.", "propose_event", Create()),
            Staff("acceptProposal", HttpMethods.Post, "/api/event-proposals/{id}/acceptance", "Events", "Accept a event proposal.", "Transitions a event proposal to accepted. Requires a staff bearer token.", "accept_proposal", Transition()),
            Staff("withdrawAcceptance", HttpMethods.Delete, "/api/event-proposals/{id}/acceptance", "Events", "Withdraw a event acceptance.", "Withdraws an accepted event proposal. Requires a staff bearer token.", "withdraw_acceptance", Delete()),
            Staff("withdrawProposal", HttpMethods.Delete, "/api/event-proposals/{id}", "Events", "Withdraw a event proposal.", "Withdraws a event proposal. Requires a staff bearer token.", "withdraw_proposal", Delete()),
            Staff("adjustEventCapacity", HttpMethods.Put, "/api/events/{id}/capacity", "Events", "Adjust event capacity.", "Updates the capacity of a eventItem. Requires a staff bearer token.", "adjust_event_capacity", Transition()),
            Staff("cancelEvent", HttpMethods.Delete, "/api/events/{id}", "Events", "Cancel a eventItem.", "Cancels a eventItem. Requires a staff bearer token.", "cancel_event", Delete()),
            Staff("importEvents", HttpMethods.Post, "/api/events/import", "Events", "Import events.", "Imports events from CSV. Requires a staff bearer token.", "import_events", Create()),
            Staff("listAttendees", HttpMethods.Get, "/api/attendees", "Attendees", "List attendees.", "Reads the attendee collection. Requires a staff bearer token.", "list_attendees", Read()),
            Staff("createAttendee", HttpMethods.Post, "/api/attendees", "Attendees", "Create a attendee.", "Creates a attendee. Requires a staff bearer token.", "create_attendee", Create()),
            Staff("updateAttendee", HttpMethods.Put, "/api/attendees/{id}", "Attendees", "Update a attendee.", "Updates an existing attendee. Requires a staff bearer token.", "update_attendee", Transition()),
            Staff("deleteAttendee", HttpMethods.Delete, "/api/attendees/{id}", "Attendees", "Delete a attendee.", "Deletes a attendee. Requires a staff bearer token.", "delete_attendee", Delete()),
            Staff("importAttendees", HttpMethods.Post, "/api/attendees/import", "Attendees", "Import attendees.", "Imports attendees from CSV. Requires a staff bearer token.", "import_attendees", Create()),
            Staff("triggerAttendeeInvite", HttpMethods.Post, "/api/attendees/{id}/invite", "Attendees", "Trigger a attendee invite.", "Sends a booking invite to a attendee. Requires a staff bearer token.", "trigger_invite", Create()),
            Staff("retryAttendeeEmail", HttpMethods.Post, "/api/attendees/{id}/email-retry", "Attendees", "Retry attendee email.", "Retries pending attendee email delivery. Requires a staff bearer token.", "retry_attendee_email", Create()),
            Staff("startRecoveryInvite", HttpMethods.Post, "/api/attendees/{attendeeId}/recovery-invites", "Attendees", "Start a recovery invite.", "Starts a recovery invite for a attendee. Requires a staff bearer token.", "start_recovery_invite", Create()),
            Staff("cancelRecoveryInvite", HttpMethods.Delete, "/api/attendees/{attendeeId}/recovery-invites/{inviteId}", "Attendees", "Cancel a recovery invite.", "Cancels a pending recovery invite. Requires a staff bearer token.", "cancel_recovery_invite", Delete()),
            Staff("listAttendeeBookings", HttpMethods.Get, "/api/attendees/{attendeeId}/bookings", "Attendee Booking", "List attendee bookings.", "Reads the active bookings of a attendee. Requires a staff bearer token.", "list_attendee_bookings", Read()),
            Staff("cancelAttendeeBooking", HttpMethods.Post, "/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", "Attendee Booking", "Cancel a attendee booking.", "Cancels a attendee booking. Requires a staff bearer token.", "cancel_attendee_booking", Delete()),
            Staff("getAttendeeReadiness", HttpMethods.Get, "/api/attendees/{attendeeId}/readiness", "Attendees", "Read attendee readiness.", "Reads the booking readiness of a attendee. Requires a staff bearer token.", "get_attendee_readiness", Read()),
            Staff("listAttendeeGroups", HttpMethods.Get, "/api/attendee-groups", "Attendee Groups", "List attendee groups.", "Reads the attendee group collection. Requires a staff bearer token.", "list_attendee_groups", Read()),
            Staff("getSettings", HttpMethods.Get, "/api/admin/settings", "Settings", "Read settings.", "Reads the application settings. Requires a staff bearer token.", "get_settings", Read()),
            Staff("updateSettings", HttpMethods.Put, "/api/admin/settings", "Settings", "Update settings.", "Updates the application settings. Requires a staff bearer token.", "update_settings", Transition()),
            Staff("listStaffAccess", HttpMethods.Get, "/api/admin/staff-access", "Staff Access", "List staff access.", "Reads the staff access collection. Requires a staff bearer token.", "list_staff_access", Read()),
            Staff("replaceStaffAccessScope", HttpMethods.Put, "/api/admin/staff-access/{staffUserId}", "Staff Access", "Replace staff access scope.", "Replaces the access scope of a staff user. Requires a staff bearer token.", "replace_staff_access_scope", Transition()),
            Staff("clearStaffAccessScope", HttpMethods.Delete, "/api/admin/staff-access/{staffUserId}", "Staff Access", "Clear staff access scope.", "Clears the access scope of a staff user. Requires a staff bearer token.", "clear_staff_access_scope", Delete()),
            Staff("getDashboards", HttpMethods.Get, "/api/dashboards", "Dashboards", "Read dashboards.", "Reads the dashboard views visible to the signed-in staff member. Requires a staff bearer token.", "get_dashboards", Read()),
            Staff("getEventAuditHistory", HttpMethods.Get, "/api/audit/event/{id}", "Audit", "Read event audit history.", "Reads the audit history of a eventItem. Requires a staff bearer token.", "event_audit_history", Read()),
            Staff("getAttendeeAuditHistory", HttpMethods.Get, "/api/audit/attendee/{id}", "Audit", "Read attendee audit history.", "Reads the audit history of a attendee. Requires a staff bearer token.", "attendee_audit_history", Read()),
            Staff("searchAudit", HttpMethods.Get, "/api/audit/search", "Audit", "Search audit events.", "Searches audit events with filters and cursor paging. Requires a staff bearer token.", "search_audit", Read()),
            Staff("listAppointmentEvents", HttpMethods.Get, "/api/appointment-workspace/events", "Appointment Workspace", "List appointment events.", "Reads the appointment workspace events. Requires a staff bearer token.", "appointment_events", Read()),
            Staff("getAppointmentEvent", HttpMethods.Get, "/api/appointment-workspace/events/{eventId}", "Appointment Workspace", "Read an appointment eventItem.", "Reads one appointment workspace eventItem. Requires a staff bearer token.", "appointment_event_detail", Read()),
            Staff("exportAppointmentRoster", HttpMethods.Get, "/api/appointment-workspace/events/{eventId}/roster", "Appointment Workspace", "Export an appointment roster.", "Exports the appointment roster CSV. Requires a staff bearer token.", "export_appointment_roster", Read()),
            Staff("updateAppointmentStatus", HttpMethods.Put, "/api/appointment-workspace/appointments/{bookingAppointmentId}/status", "Appointment Workspace", "Update appointment status.", "Updates the status of a booking appointment. Requires a staff bearer token.", "update_appointment_status", Transition()),
            Excluded("getApiIndex", HttpMethods.Get, "/api", "Discovery", "Read the API entry document.", "Anonymous API entry document listing principal entry points.", "Transport discovery, not a business capability. MCP has tools/list."),
            Excluded("getOpenApiDocument", HttpMethods.Get, "/openapi/v1.json", "Discovery", "Read the OpenAPI document.", "Anonymous machine-readable API contract.", "Transport discovery, not a business capability."),
            Excluded("getSwaggerUi", HttpMethods.Get, "/swagger", "Discovery", "Open the Swagger UI.", "Anonymous human documentation UI.", "Human documentation UI, not a business capability."),
            Excluded("getHealth", HttpMethods.Get, "/health", "Health", "Read the deployment health probe.", "Anonymous deployment liveness probe.", "Deployment probe, not a staff workflow."),
            Excluded("viewInvite", HttpMethods.Get, "/api/booking/{token}", "Attendee Booking", "View an invite.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
            Excluded("confirmBooking", HttpMethods.Post, "/api/booking/{token}/confirm", "Attendee Booking", "Confirm a booking.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
            Excluded("viewManagedBooking", HttpMethods.Get, "/api/booking/manage/{token}", "Attendee Booking", "View a managed booking.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
            Excluded("cancelManagedBooking", HttpMethods.Post, "/api/booking/manage/{token}/cancel", "Attendee Booking", "Cancel a managed booking.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
        };

        var byId = new Dictionary<string, AgentOperation>(StringComparer.Ordinal);
        var byRoute = new HashSet<string>(StringComparer.Ordinal);
        var byTool = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            if ((operation.McpTool is null) == (operation.ExclusionReason is null))
            {
                throw new InvalidOperationException($"Operation {operation.OperationId} must set exactly one of McpTool or ExclusionReason.");
            }

            if (!byId.TryAdd(operation.OperationId, operation))
            {
                throw new InvalidOperationException($"Duplicate operation id {operation.OperationId}.");
            }

            if (!byRoute.Add($"{operation.Method} {operation.Route}"))
            {
                throw new InvalidOperationException($"Duplicate method and route {operation.Method} {operation.Route}.");
            }

            if (operation.McpTool is not null && !byTool.Add(operation.McpTool))
            {
                throw new InvalidOperationException($"Duplicate MCP tool {operation.McpTool}.");
            }
        }

        All = byId;
    }

    /// <summary>Gets one required operation or throws for a programming error.</summary>
    /// <param name="operationId">The operation id to look up.</param>
    /// <returns>The catalogued operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the operation id is unknown.</exception>
    public static AgentOperation Get(string operationId) => All[operationId];

    private static AgentOperation Staff(
        string operationId, string method, string route, string tag,
        string summary, string description, string mcpTool, AgentHints hints) =>
        new(operationId, method, route, tag, summary, description, true, mcpTool, null, hints);

    private static AgentOperation Excluded(
        string operationId, string method, string route, string tag,
        string summary, string description, string reason) =>
        new(operationId, method, route, tag, summary, description, false, null, reason, new AgentHints(true, false, true, false));

    private static AgentHints Read() => new(true, false, true, false);

    private static AgentHints Create() => new(false, false, false, false);

    private static AgentHints Transition() => new(false, true, true, false);

    private static AgentHints Delete() => new(false, true, true, false);
}
`````

## after — src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs","beforeSha":"6097e2dcb14e98fd1264aeb990de65895c9d2a44eef3c545987be5876f8ff8d3","afterSha":"205be03211171c7224b7d36ed7a2d40087d68285546cfc50b0d4409a89354846","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Api.OpenApi;

/// <summary>Behavior hints shared by OpenAPI and MCP discovery.</summary>
/// <param name="ReadOnly">Whether the operation performs no state change.</param>
/// <param name="Destructive">Whether the operation deletes, cancels, withdraws, clears, replaces, or transitions existing state.</param>
/// <param name="Idempotent">Whether repeating the operation has the same effect as performing it once.</param>
/// <param name="OpenWorld">Whether the operation interacts with arbitrary external entities. Always false for this tool set.</param>
public sealed record AgentHints(
    /// <summary>Gets whether the operation performs no state change.</summary>
    bool ReadOnly,
    /// <summary>Gets whether the operation deletes, cancels, withdraws, clears, replaces, or transitions existing state.</summary>
    bool Destructive,
    /// <summary>Gets whether repeating the operation has the same effect as performing it once.</summary>
    bool Idempotent,
    /// <summary>Gets whether the operation interacts with arbitrary external entities.</summary>
    bool OpenWorld);

/// <summary>One stable HTTP operation and its MCP parity decision.</summary>
/// <param name="OperationId">The unique lower-camel-case OpenAPI operation id.</param>
/// <param name="Method">The uppercase HTTP method of the operation.</param>
/// <param name="Route">The route pattern of the operation.</param>
/// <param name="Tag">The OpenAPI tag grouping the operation.</param>
/// <param name="Summary">The short human-readable summary of the operation.</param>
/// <param name="Description">The longer description stating purpose, authorization, and side effects.</param>
/// <param name="RequiresBearer">Whether the operation requires a staff bearer token.</param>
/// <param name="McpTool">The snake-case MCP tool name, or null when intentionally excluded.</param>
/// <param name="ExclusionReason">The reviewed reason for exclusion, or null when an MCP tool exists.</param>
/// <param name="Hints">The behavior hints shared by OpenAPI and MCP discovery.</param>
public sealed record AgentOperation(
    /// <summary>Gets the unique lower-camel-case OpenAPI operation id.</summary>
    string OperationId,
    /// <summary>Gets the uppercase HTTP method of the operation.</summary>
    string Method,
    /// <summary>Gets the route pattern of the operation.</summary>
    string Route,
    /// <summary>Gets the OpenAPI tag grouping the operation.</summary>
    string Tag,
    /// <summary>Gets the short human-readable summary of the operation.</summary>
    string Summary,
    /// <summary>Gets the longer description stating purpose, authorization, and side effects.</summary>
    string Description,
    /// <summary>Gets whether the operation requires a staff bearer token.</summary>
    bool RequiresBearer,
    /// <summary>Gets the snake-case MCP tool name, or null when intentionally excluded.</summary>
    string? McpTool,
    /// <summary>Gets the reviewed reason for exclusion, or null when an MCP tool exists.</summary>
    string? ExclusionReason,
    /// <summary>Gets the behavior hints shared by OpenAPI and MCP discovery.</summary>
    AgentHints Hints);

/// <summary>Shared registry of HTTP operations and their MCP parity decisions.</summary>
public static class AgentOperationCatalog
{
    /// <summary>Gets every catalogued application operation keyed by operation id.</summary>
    public static IReadOnlyDictionary<string, AgentOperation> All { get; }

    static AgentOperationCatalog()
    {
        var operations = new List<AgentOperation>
        {
            Staff("getMyAccess", HttpMethods.Get, "/api/me", "Identity", "Read the signed-in staff access.", "Reads the signed-in staff identity and capability scope. Requires a staff bearer token.", "get_my_access", Read()),
            Staff("getEventBoard", HttpMethods.Get, "/api/events/board", "Events", "Read the scoped event board.", "Reads the event board visible to the signed-in staff member. Requires a staff bearer token.", "event_board", Read()),
            Staff("getEventOperations", HttpMethods.Get, "/api/events/operations", "Events", "Read the scoped event operations view.", "Reads the event operations view visible to the signed-in staff member. Requires a staff bearer token.", "get_event_operations", Read()),
            Staff("proposeEvent", HttpMethods.Post, "/api/event-proposals", "Events", "Propose a eventItem.", "Creates a event proposal. Requires a staff bearer token.", "propose_event", Create()),
            Staff("acceptProposal", HttpMethods.Post, "/api/event-proposals/{id}/acceptance", "Events", "Accept a event proposal.", "Transitions a event proposal to accepted. Requires a staff bearer token.", "accept_proposal", Transition()),
            Staff("withdrawAcceptance", HttpMethods.Delete, "/api/event-proposals/{id}/acceptance", "Events", "Withdraw a event acceptance.", "Withdraws an accepted event proposal. Requires a staff bearer token.", "withdraw_acceptance", Delete()),
            Staff("withdrawProposal", HttpMethods.Delete, "/api/event-proposals/{id}", "Events", "Withdraw a event proposal.", "Withdraws a event proposal. Requires a staff bearer token.", "withdraw_proposal", Delete()),
            Staff("adjustEventCapacity", HttpMethods.Put, "/api/events/{id}/capacity", "Events", "Adjust event capacity.", "Updates the capacity of a eventItem. Requires a staff bearer token.", "adjust_event_capacity", Transition()),
            Staff("cancelEvent", HttpMethods.Delete, "/api/events/{id}", "Events", "Cancel a eventItem.", "Cancels a eventItem. Requires a staff bearer token.", "cancel_event", Delete()),
            Staff("listAttendees", HttpMethods.Get, "/api/attendees", "Attendees", "List attendees.", "Reads the attendee collection. Requires a staff bearer token.", "list_attendees", Read()),
            Staff("createAttendee", HttpMethods.Post, "/api/attendees", "Attendees", "Create a attendee.", "Creates a attendee. Requires a staff bearer token.", "create_attendee", Create()),
            Staff("updateAttendee", HttpMethods.Put, "/api/attendees/{id}", "Attendees", "Update a attendee.", "Updates an existing attendee. Requires a staff bearer token.", "update_attendee", Transition()),
            Staff("deleteAttendee", HttpMethods.Delete, "/api/attendees/{id}", "Attendees", "Delete a attendee.", "Deletes a attendee. Requires a staff bearer token.", "delete_attendee", Delete()),
            Staff("importAttendees", HttpMethods.Post, "/api/attendees/import", "Attendees", "Import attendees.", "Imports attendees from CSV. Requires a staff bearer token.", "import_attendees", Create()),
            Staff("triggerAttendeeInvite", HttpMethods.Post, "/api/attendees/{id}/invite", "Attendees", "Trigger a attendee invite.", "Sends a booking invite to a attendee. Requires a staff bearer token.", "trigger_invite", Create()),
            Staff("retryAttendeeEmail", HttpMethods.Post, "/api/attendees/{id}/email-retry", "Attendees", "Retry attendee email.", "Retries pending attendee email delivery. Requires a staff bearer token.", "retry_attendee_email", Create()),
            Staff("startRecoveryInvite", HttpMethods.Post, "/api/attendees/{attendeeId}/recovery-invites", "Attendees", "Start a recovery invite.", "Starts a recovery invite for a attendee. Requires a staff bearer token.", "start_recovery_invite", Create()),
            Staff("cancelRecoveryInvite", HttpMethods.Delete, "/api/attendees/{attendeeId}/recovery-invites/{inviteId}", "Attendees", "Cancel a recovery invite.", "Cancels a pending recovery invite. Requires a staff bearer token.", "cancel_recovery_invite", Delete()),
            Staff("listAttendeeBookings", HttpMethods.Get, "/api/attendees/{attendeeId}/bookings", "Attendee Booking", "List attendee bookings.", "Reads the active bookings of a attendee. Requires a staff bearer token.", "list_attendee_bookings", Read()),
            Staff("cancelAttendeeBooking", HttpMethods.Post, "/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", "Attendee Booking", "Cancel a attendee booking.", "Cancels a attendee booking. Requires a staff bearer token.", "cancel_attendee_booking", Delete()),
            Staff("getAttendeeReadiness", HttpMethods.Get, "/api/attendees/{attendeeId}/readiness", "Attendees", "Read attendee readiness.", "Reads the booking readiness of a attendee. Requires a staff bearer token.", "get_attendee_readiness", Read()),
            Staff("listAttendeeGroups", HttpMethods.Get, "/api/attendee-groups", "Attendee Groups", "List attendee groups.", "Reads the attendee group collection. Requires a staff bearer token.", "list_attendee_groups", Read()),
            Staff("getSettings", HttpMethods.Get, "/api/admin/settings", "Settings", "Read settings.", "Reads the application settings. Requires a staff bearer token.", "get_settings", Read()),
            Staff("updateSettings", HttpMethods.Put, "/api/admin/settings", "Settings", "Update settings.", "Updates the application settings. Requires a staff bearer token.", "update_settings", Transition()),
            Staff("listStaffAccess", HttpMethods.Get, "/api/admin/staff-access", "Staff Access", "List staff access.", "Reads the staff access collection. Requires a staff bearer token.", "list_staff_access", Read()),
            Staff("replaceStaffAccessScope", HttpMethods.Put, "/api/admin/staff-access/{staffUserId}", "Staff Access", "Replace staff access scope.", "Replaces the access scope of a staff user. Requires a staff bearer token.", "replace_staff_access_scope", Transition()),
            Staff("clearStaffAccessScope", HttpMethods.Delete, "/api/admin/staff-access/{staffUserId}", "Staff Access", "Clear staff access scope.", "Clears the access scope of a staff user. Requires a staff bearer token.", "clear_staff_access_scope", Delete()),
            Staff("getDashboards", HttpMethods.Get, "/api/dashboards", "Dashboards", "Read dashboards.", "Reads the dashboard views visible to the signed-in staff member. Requires a staff bearer token.", "get_dashboards", Read()),
            Staff("getEventAuditHistory", HttpMethods.Get, "/api/audit/event/{id}", "Audit", "Read event audit history.", "Reads the audit history of a eventItem. Requires a staff bearer token.", "event_audit_history", Read()),
            Staff("getAttendeeAuditHistory", HttpMethods.Get, "/api/audit/attendee/{id}", "Audit", "Read attendee audit history.", "Reads the audit history of a attendee. Requires a staff bearer token.", "attendee_audit_history", Read()),
            Staff("searchAudit", HttpMethods.Get, "/api/audit/search", "Audit", "Search audit events.", "Searches audit events with filters and cursor paging. Requires a staff bearer token.", "search_audit", Read()),
            Staff("listAppointmentEvents", HttpMethods.Get, "/api/appointment-workspace/events", "Appointment Workspace", "List appointment events.", "Reads the appointment workspace events. Requires a staff bearer token.", "appointment_events", Read()),
            Staff("getAppointmentEvent", HttpMethods.Get, "/api/appointment-workspace/events/{eventId}", "Appointment Workspace", "Read an appointment eventItem.", "Reads one appointment workspace eventItem. Requires a staff bearer token.", "appointment_event_detail", Read()),
            Staff("exportAppointmentRoster", HttpMethods.Get, "/api/appointment-workspace/events/{eventId}/roster", "Appointment Workspace", "Export an appointment roster.", "Exports the appointment roster CSV. Requires a staff bearer token.", "export_appointment_roster", Read()),
            Staff("updateAppointmentStatus", HttpMethods.Put, "/api/appointment-workspace/appointments/{bookingAppointmentId}/status", "Appointment Workspace", "Update appointment status.", "Updates the status of a booking appointment. Requires a staff bearer token.", "update_appointment_status", Transition()),
            Excluded("getApiIndex", HttpMethods.Get, "/api", "Discovery", "Read the API entry document.", "Anonymous API entry document listing principal entry points.", "Transport discovery, not a business capability. MCP has tools/list."),
            Excluded("getOpenApiDocument", HttpMethods.Get, "/openapi/v1.json", "Discovery", "Read the OpenAPI document.", "Anonymous machine-readable API contract.", "Transport discovery, not a business capability."),
            Excluded("getSwaggerUi", HttpMethods.Get, "/swagger", "Discovery", "Open the Swagger UI.", "Anonymous human documentation UI.", "Human documentation UI, not a business capability."),
            Excluded("getHealth", HttpMethods.Get, "/health", "Health", "Read the deployment health probe.", "Anonymous deployment liveness probe.", "Deployment probe, not a staff workflow."),
            Excluded("viewInvite", HttpMethods.Get, "/api/booking/{token}", "Attendee Booking", "View an invite.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
            Excluded("confirmBooking", HttpMethods.Post, "/api/booking/{token}/confirm", "Attendee Booking", "Confirm a booking.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
            Excluded("viewManagedBooking", HttpMethods.Get, "/api/booking/manage/{token}", "Attendee Booking", "View a managed booking.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
            Excluded("cancelManagedBooking", HttpMethods.Post, "/api/booking/manage/{token}/cancel", "Attendee Booking", "Cancel a managed booking.", "Anonymous attendee token flow.", "Anonymous Attendee token flow; excluded by the approved remote MCP design."),
        };

        var byId = new Dictionary<string, AgentOperation>(StringComparer.Ordinal);
        var byRoute = new HashSet<string>(StringComparer.Ordinal);
        var byTool = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            if ((operation.McpTool is null) == (operation.ExclusionReason is null))
            {
                throw new InvalidOperationException($"Operation {operation.OperationId} must set exactly one of McpTool or ExclusionReason.");
            }

            if (!byId.TryAdd(operation.OperationId, operation))
            {
                throw new InvalidOperationException($"Duplicate operation id {operation.OperationId}.");
            }

            if (!byRoute.Add($"{operation.Method} {operation.Route}"))
            {
                throw new InvalidOperationException($"Duplicate method and route {operation.Method} {operation.Route}.");
            }

            if (operation.McpTool is not null && !byTool.Add(operation.McpTool))
            {
                throw new InvalidOperationException($"Duplicate MCP tool {operation.McpTool}.");
            }
        }

        All = byId;
    }

    /// <summary>Gets one required operation or throws for a programming error.</summary>
    /// <param name="operationId">The operation id to look up.</param>
    /// <returns>The catalogued operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the operation id is unknown.</exception>
    public static AgentOperation Get(string operationId) => All[operationId];

    private static AgentOperation Staff(
        string operationId, string method, string route, string tag,
        string summary, string description, string mcpTool, AgentHints hints) =>
        new(operationId, method, route, tag, summary, description, true, mcpTool, null, hints);

    private static AgentOperation Excluded(
        string operationId, string method, string route, string tag,
        string summary, string description, string reason) =>
        new(operationId, method, route, tag, summary, description, false, null, reason, new AgentHints(true, false, true, false));

    private static AgentHints Read() => new(true, false, true, false);

    private static AgentHints Create() => new(false, false, false, false);

    private static AgentHints Transition() => new(false, true, true, false);

    private static AgentHints Delete() => new(false, true, true, false);
}
`````

## before — src/EventBooking.Application/Access/StaffAccessAuthorizer.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Application/Access/StaffAccessAuthorizer.cs","beforeSha":"49ce24da2329dcc629c46b8e6903e08a15e408e9a7682059eedb047e6eede5de","afterSha":"42e53b7d3b4c7ab3f00f0be8ad1d34f37709ac31d71415ef3805cb6dd265460a","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>Defines staff access context for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
public sealed record StaffAccessContext(
    Guid StaffUserId,
    IReadOnlySet<Role> Roles,
    Guid? AppointmentTypeId);

/// <summary>Defines istaff access authorizer for the current use case.</summary>
public interface IStaffAccessAuthorizer
{
    /// <summary>Provides authorize async within this contract.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="requiredAppointmentTypeId">The required appointment type id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken);
}

/// <summary>Defines staff access authorizer for the current use case.</summary>
/// <param name="profiles">The profiles.</param>
public sealed class StaffAccessAuthorizer(IStaffAccessProfileRepository profiles)
    : IStaffAccessAuthorizer
{
    private static readonly Error Denied = Error.Forbidden("This staff profile cannot perform this operation.");

    /// <summary>Defines authorize async for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="requiredAppointmentTypeId">The required appointment type id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(staffUserId, cancellationToken);
        if (profile is null || !profile.IsValid() || !IsAllowed(profile, capability))
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        // A Manager or AppointmentStaff profile with no appointment-type scope grants no
        // capabilities on its own: the null scope denies every capability unless another role
        // on the same profile grants it.
        if (StaffAccessProfile.NeedsScope(profile.Roles)
            && profile.AppointmentTypeId is null
            && !IsAllowed(profile.IsAdmin, profile.IsCoordinator, false, false, capability))
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        var scopedCapability = capability is
            StaffCapability.ManageEventNegotiation or
            StaffCapability.ViewEventOperations or
            StaffCapability.ConductAppointments;

        if (scopedCapability
            && StaffAccessProfile.NeedsScope(profile.Roles)
            && profile.AppointmentTypeId is null)
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        if (requiredAppointmentTypeId is not null
            && scopedCapability
            && profile.AppointmentTypeId != requiredAppointmentTypeId)
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        return Result<StaffAccessContext>.Success(new StaffAccessContext(
            profile.StaffUserId,
            profile.Roles,
            profile.AppointmentTypeId));
    }

    private static bool IsAllowed(StaffAccessProfile profile, StaffCapability capability) =>
        IsAllowed(profile.IsAdmin, profile.IsCoordinator, profile.IsManager, profile.IsAppointmentStaff, capability);

    private static bool IsAllowed(bool isAdmin, bool isCoordinator, bool isManager, bool isAppointmentStaff, StaffCapability capability)
    {
        // Explicit attendee-data deny for Admin is retained even though valid profiles make Admin
        // exclusive. It fails closed if invalid data reaches this method in a future refactor.
        if (isAdmin && capability is
            StaffCapability.ManageAttendees or
            StaffCapability.ViewAttendeeDashboards or
            StaffCapability.ViewAttendeeAudit)
        {
            return false;
        }

        return capability switch
        {
            StaffCapability.ManageSettings => isAdmin,
            StaffCapability.ManageStaffAccess => isAdmin,
            StaffCapability.ImportEvents => isAdmin || isCoordinator,
            StaffCapability.ManageAttendees => isCoordinator,
            StaffCapability.ViewAttendeeDashboards => isCoordinator,
            StaffCapability.ViewAttendeeAudit => isCoordinator,
            StaffCapability.ViewEventAudit => isAdmin || isCoordinator,
            StaffCapability.ManageEventNegotiation => isManager,
            StaffCapability.ViewEventOperations =>
                isAdmin || isCoordinator || isManager || isAppointmentStaff,
            StaffCapability.CancelEvent =>
                isAdmin || isCoordinator || isManager,
            StaffCapability.ConductAppointments => isManager || isAppointmentStaff,
            _ => false,
        };
    }
}
`````

## after — src/EventBooking.Application/Access/StaffAccessAuthorizer.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Application/Access/StaffAccessAuthorizer.cs","beforeSha":"49ce24da2329dcc629c46b8e6903e08a15e408e9a7682059eedb047e6eede5de","afterSha":"42e53b7d3b4c7ab3f00f0be8ad1d34f37709ac31d71415ef3805cb6dd265460a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>Defines staff access context for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
public sealed record StaffAccessContext(
    Guid StaffUserId,
    IReadOnlySet<Role> Roles,
    Guid? AppointmentTypeId);

/// <summary>Defines istaff access authorizer for the current use case.</summary>
public interface IStaffAccessAuthorizer
{
    /// <summary>Provides authorize async within this contract.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="requiredAppointmentTypeId">The required appointment type id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken);
}

/// <summary>Defines staff access authorizer for the current use case.</summary>
/// <param name="profiles">The profiles.</param>
public sealed class StaffAccessAuthorizer(IStaffAccessProfileRepository profiles)
    : IStaffAccessAuthorizer
{
    private static readonly Error Denied = Error.Forbidden("This staff profile cannot perform this operation.");

    /// <summary>Defines authorize async for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="requiredAppointmentTypeId">The required appointment type id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(staffUserId, cancellationToken);
        if (profile is null || !profile.IsValid() || !IsAllowed(profile, capability))
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        // A Manager or AppointmentStaff profile with no appointment-type scope grants no
        // capabilities on its own: the null scope denies every capability unless another role
        // on the same profile grants it.
        if (StaffAccessProfile.NeedsScope(profile.Roles)
            && profile.AppointmentTypeId is null
            && !IsAllowed(profile.IsAdmin, profile.IsCoordinator, false, false, capability))
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        var scopedCapability = capability is
            StaffCapability.ManageEventNegotiation or
            StaffCapability.ViewEventOperations or
            StaffCapability.ConductAppointments;

        if (scopedCapability
            && StaffAccessProfile.NeedsScope(profile.Roles)
            && profile.AppointmentTypeId is null)
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        if (requiredAppointmentTypeId is not null
            && scopedCapability
            && profile.AppointmentTypeId != requiredAppointmentTypeId)
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        return Result<StaffAccessContext>.Success(new StaffAccessContext(
            profile.StaffUserId,
            profile.Roles,
            profile.AppointmentTypeId));
    }

    private static bool IsAllowed(StaffAccessProfile profile, StaffCapability capability) =>
        IsAllowed(profile.IsAdmin, profile.IsCoordinator, profile.IsManager, profile.IsAppointmentStaff, capability);

    private static bool IsAllowed(bool isAdmin, bool isCoordinator, bool isManager, bool isAppointmentStaff, StaffCapability capability)
    {
        // Explicit attendee-data deny for Admin is retained even though valid profiles make Admin
        // exclusive. It fails closed if invalid data reaches this method in a future refactor.
        if (isAdmin && capability is
            StaffCapability.ManageAttendees or
            StaffCapability.ViewAttendeeDashboards or
            StaffCapability.ViewAttendeeAudit)
        {
            return false;
        }

        return capability switch
        {
            StaffCapability.ManageSettings => isAdmin,
            StaffCapability.ManageStaffAccess => isAdmin,
            StaffCapability.ManageAttendees => isCoordinator,
            StaffCapability.ViewAttendeeDashboards => isCoordinator,
            StaffCapability.ViewAttendeeAudit => isCoordinator,
            StaffCapability.ViewEventAudit => isAdmin || isCoordinator,
            StaffCapability.ManageEventNegotiation => isManager,
            StaffCapability.ViewEventOperations =>
                isAdmin || isCoordinator || isManager || isAppointmentStaff,
            StaffCapability.CancelEvent =>
                isAdmin || isCoordinator || isManager,
            StaffCapability.ConductAppointments => isManager || isAppointmentStaff,
            _ => false,
        };
    }
}
`````

## before — src/EventBooking.Application/Access/StaffCapability.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Application/Access/StaffCapability.cs","beforeSha":"7f1ee3d8f41a7d559c7e708277c7619cd998329aba3eaa90186b600d130973e7","afterSha":"465e37f5f16cc8dd9d07e764e1c36982bb06d38bd57d988d59182d8081cf55da","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Access;

/// <summary>Defines staff capability for the current use case.</summary>
public enum StaffCapability
{
    /// <summary>Defines contract for the current use case.</summary>
    ManageSettings,
    /// <summary>Defines contract for the current use case.</summary>
    ManageStaffAccess,
    /// <summary>Defines contract for the current use case.</summary>
    ImportEvents,
    /// <summary>Defines contract for the current use case.</summary>
    ManageAttendees,
    /// <summary>Defines contract for the current use case.</summary>
    ViewAttendeeDashboards,
    /// <summary>Defines contract for the current use case.</summary>
    ViewAttendeeAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ViewEventAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ManageEventNegotiation,
    /// <summary>Defines contract for the current use case.</summary>
    ViewEventOperations,
    /// <summary>Defines contract for the current use case.</summary>
    CancelEvent,
    /// <summary>Defines contract for the current use case.</summary>
    ConductAppointments,
}
`````

## after — src/EventBooking.Application/Access/StaffCapability.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Application/Access/StaffCapability.cs","beforeSha":"7f1ee3d8f41a7d559c7e708277c7619cd998329aba3eaa90186b600d130973e7","afterSha":"465e37f5f16cc8dd9d07e764e1c36982bb06d38bd57d988d59182d8081cf55da","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Access;

/// <summary>Defines staff capability for the current use case.</summary>
public enum StaffCapability
{
    /// <summary>Defines contract for the current use case.</summary>
    ManageSettings,
    /// <summary>Defines contract for the current use case.</summary>
    ManageStaffAccess,
    /// <summary>Creates, updates and invites attendees under Coordinator authorization.</summary>
    ManageAttendees,
    /// <summary>Defines contract for the current use case.</summary>
    ViewAttendeeDashboards,
    /// <summary>Defines contract for the current use case.</summary>
    ViewAttendeeAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ViewEventAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ManageEventNegotiation,
    /// <summary>Defines contract for the current use case.</summary>
    ViewEventOperations,
    /// <summary>Defines contract for the current use case.</summary>
    CancelEvent,
    /// <summary>Defines contract for the current use case.</summary>
    ConductAppointments,
}
`````

## before — src/EventBooking.Application/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Application/DependencyInjection.cs","beforeSha":"956c5c0f41c77a77e31b33cbe5c91c2dd16625a97b9626569e320358048d2389","afterSha":"a2ac56baac0e4c164adfbfb8438f425d074d6725f83a63a17efe909774751f3e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Settings;
using EventBooking.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Application;

/// <summary>Defines application service collection extensions for the current use case.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Defines add event booking application for the current use case.</summary>
    /// <param name="services">The services.</param>
    /// <param name="portal">The portal.</param>
    /// <param name="staffIdPolicy">The deployment staff-number validation policy.</param>
    public static IServiceCollection AddEventBookingApplication(
        this IServiceCollection services,
        AttendeePortalOptions portal,
        StaffIdPolicy? staffIdPolicy = null)
    {
        services.AddSingleton(portal);
        services.AddSingleton(staffIdPolicy ?? new StaffIdPolicy());

        // Shared services.
        services.AddScoped<EligibleEventFinder>();
        services.AddScoped<EmailDeliveryService>();
        services.AddScoped<InviteIssuer>();
        services.AddScoped<BookingCanceller>();
        services.AddScoped<IStaffAccessAuthorizer, StaffAccessAuthorizer>();
        services.AddScoped<StaffAccessHandler>();

        // Event negotiation.
        services.AddScoped<ProposeEventHandler>();
        services.AddScoped<AcceptProposalHandler>();
        services.AddScoped<ImportEventsHandler>();
        services.AddScoped<WithdrawAcceptanceHandler>();
        services.AddScoped<WithdrawProposalHandler>();
        services.AddScoped<GetManagerEventBoardHandler>();
        services.AddScoped<CancelEventHandler>();
        services.AddScoped<AdjustEventCapacityHandler>();

        // Attendees.
        services.AddScoped<ImportAttendeesHandler>();
        services.AddScoped<SaveAttendeeHandler>();
        services.AddScoped<DeleteAttendeeHandler>();
        services.AddScoped<ListAttendeesHandler>();
        services.AddScoped<ListAttendeeGroupsHandler>();
        services.AddScoped<AttendeeReadinessCalculator>();
        services.AddScoped<GetAttendeeReadinessHandler>();
        services.AddScoped<GetAttendeeBookingsHandler>();
        services.AddScoped<GetDashboardsHandler>();
        services.AddScoped<GetEventOperationsHandler>();
        services.AddScoped<GetAuditHistoryHandler>();
        services.AddScoped<GetAuditSearchHandler>();

        // Administration.
        services.AddScoped<AdminSettingsHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<SyncStaffAccessProfileRolesHandler>();

        // Appointments.
        services.AddScoped<GetAppointmentWorkspaceHandler>();
        services.AddScoped<AppointmentRosterCsvFormatter>();
        services.AddScoped<RecoveryBookingOutcomeCoordinator>();
        services.AddScoped<UpdateBookingAppointmentStatusHandler>();

        // Invites and bookings.
        services.AddScoped<TriggerInviteHandler>();
        services.AddScoped<StartRecoveryHandler>();
        services.AddScoped<CancelRecoveryInviteHandler>();
        services.AddScoped<RetryEmailHandler>();
        services.AddScoped<ExpireInvitesHandler>();
        services.AddScoped<ViewInviteHandler>();
        services.AddScoped<ViewBookingHandler>();
        services.AddScoped<ConfirmBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<CancelAttendeeBookingHandler>();

        return services;
    }
}
`````
