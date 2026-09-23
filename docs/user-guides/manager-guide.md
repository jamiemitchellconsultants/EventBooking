# Manager guide

[← All user guides](README.md)

As a Manager, you own capacity for one Appointment Type: Drug & Alcohol Testing, Medical Check-up,
or Uniform Fitting. You negotiate shared four-hour windows with the other two Managers, maintain
your type's capacity, and can use the scoped Appointments workspace to deliver the service.

## Your workflow

1. On **Slot proposals**, review open proposals and create any new windows your team can support.
2. Accept each workable proposal with your team's headcount. All three Managers must accept before
   the window becomes bookable.
3. On confirmed slots, keep your total capacity accurate and never reduce it below active demand.
4. Before and during the window, use **Appointments** to monitor Expected candidates and record your
   type's outcomes if you are part of delivery.
5. Tell Coordinators promptly about shortages or cancellations because their invitations depend on
   the capacity you control.

## Home page

After signing in, check that the access summary shows **Manager** and the correct Appointment Type.
The home page provides:

- **Slot proposals** (`/slots`)
- **Appointments** (`/appointments`)
- **Help** (`/help`) — every role's guide, including the candidate guide

![Manager home page showing Slot proposals and Appointments links](screenshots/manager-home.png)

The same links also appear as a navigation bar at the top of every page, so you can move between
Slot proposals and Appointments without returning to the home page first.

A combined Coordinator profile also shows Candidates, Dashboards, Confirmed slots, and Audit trail.
Manager data remains scoped: you never see another Manager's headcount or another Appointment Type's
appointment rows.

### If your Appointment Type is missing or wrong

Your **role** comes from the company identity provider. Your **Appointment Type** is set inside
EventBooking by an Admin. If the access summary shows Manager with no type — or the wrong type —
Slot proposals and Appointments will not work for you. Ask an Admin to set the scope on their Staff
access screen. Do not act in the wrong workspace in the meantime.

## Slot proposals screen

Use `/slots` to propose windows, accept or withdraw, adjust capacity, and cancel Confirmed Slots.

![Slot proposals screen showing open proposals and each type's acceptance chips](screenshots/slot-proposals.png)

### Propose a new slot

1. In **Propose a new slot**, choose a future **Date**.
2. Choose the **Start time**. Every window lasts exactly four hours.
3. Select **Submit proposal**.
4. Find the new row under **Open proposals** and, when ready, enter your own headcount and accept it.

Submitting a proposal does not itself provide your team's acceptance or headcount.

### Review and accept an open proposal

Each row shows Date, Window, Accepted by, My headcount, and Actions.

1. Check the date and four-hour window.
2. Review the **Accepted by** chips to see which Appointment Types have agreed. Their headcount
   values remain private.
3. Enter a positive number in **My headcount**.
4. Select **Accept**.

After your acceptance, the action reads **Update acceptance**. You can replace your own headcount
while the proposal remains open. When the third Manager accepts, EventBooking creates the Confirmed
Slot immediately; Coordinators can then invite candidates against it.

### Withdraw before confirmation

- Select **Withdraw acceptance** to remove your own acceptance while the proposal remains open.
- If you created the proposal, select **Withdraw proposal** to withdraw the entire proposal.

Neither action is available after confirmation. A confirmed window must be managed as a Confirmed
Slot.

### Adjust confirmed capacity

Under **Confirmed slots (this type)**, each row shows your total and remaining capacity only.

1. Enter the replacement total in **My total**.
2. Confirm **Remaining** still makes operational sense.
3. Select **Adjust headcount**.

The new total must be positive and cannot be lower than the number of active Bookings requiring your
type. If a candidate books at the same time, the save may be rejected to prevent overbooking.
Reload the board and enter a total that covers the updated demand.

Employee Groups determine which types each candidate needs. You do not need to know the group to
manage capacity: EventBooking reserves only the capacity required by each invitation or recovery
snapshot.

### Cancel a Confirmed Slot

Cancel only when the whole four-hour window cannot run.

1. Warn Coordinator and delivery colleagues first.
2. Select **Cancel slot**.
3. If active Bookings are affected, read the warning and select **Confirm cancel** only when you
   intend to proceed.
4. Tell the Coordinator to monitor candidates and replacement invitation delivery.

Cancellation voids Bookings on the slot, releases their capacity, and triggers the appropriate
candidate rebooking workflow. Cancelling a recovery slot preserves the original journey and
already Completed appointments.

Admins and Coordinators can cancel the same window from their own Confirmed slots screen, so agree
who is acting before anyone clicks.

## Appointments screen

Managers have the same delivery controls as Appointment staff within their own Appointment Type,
including **Download roster** for a printable working list. Use `/appointments` when you are
recording day-of work. See the [Appointment staff guide](appointment-staff-guide.md) for the
complete screen workflow, screenshots, and correction rules.

## Troubleshooting

- **The wrong Appointment Type is shown** — stop and ask an Admin to correct your scope on Staff
  access. Do not act in the wrong workspace.
- **No Appointment Type is shown at all** — an Admin has not scoped your profile yet. The role alone
  is not enough.
- **Accept or update failed** — the proposal was changed, withdrawn, or confirmed while you were
  viewing it. Reload the board.
- **Another Manager's headcount is missing** — expected. You can see who accepted, not their number.
- **Capacity change was rejected** — the total is invalid, below active demand, or demand changed
  concurrently. Reload and recalculate.
- **Candidates are awaiting availability** — propose more future windows or accept open proposals.
  An invitation needs three suitable Confirmed Slots across every required type.
- **Cancel requires a second confirmation** — active Bookings are affected. Coordinate first; the
  second click performs the cancellation.
- **A window disappeared from your board** — an Admin or Coordinator may have cancelled it. Ask the
  Coordinator to check the audit trail; Managers do not have that screen.
