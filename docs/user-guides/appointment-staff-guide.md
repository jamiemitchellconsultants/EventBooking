# Appointment staff guide

[← All user guides](README.md)

As Appointment staff, you deliver one scoped Appointment Type. The Appointments workspace contains
only operational status: who is expected, who checked in, and whether the appointment was completed
or missed. EventBooking stores no clinical findings, notes, measurements, or results in this flow.

## Before the appointment day

- Sign in and check that the home-page access summary names the correct Appointment Type. Your role
  comes from the company identity provider; the Appointment Type is set by an Admin inside
  EventBooking. If the type is missing or wrong, ask an Admin before the day.
- Open **Appointments** and confirm the expected Confirmed Slot appears in the selector.
- If the slot or a candidate is missing, ask a Coordinator to check the Booking. Do not create a
  substitute record.
- If you will be working away from a screen, use **Download roster** to take the list with you.

The dependencies are deliberate: Managers or a direct import create the Confirmed Slot, a
Coordinator issues an invitation, and the candidate must book before a Booking Appointment appears
for you.

## Appointments screen

Use `/appointments` for the full day-of workflow.

1. In **Slot**, select the date and four-hour window you are delivering. Only current and upcoming
   windows for your Appointment Type are listed.
2. Confirm the page heading names your Appointment Type.
3. Review the summary chips: Expected, Checked in, Completed, and No-show.
4. Find the candidate row by name and email.
5. Use only the action that matches what has happened at your station.

Each row shows the current Status, Check-in recorded time, Outcome recorded time, and available
Actions. The counts update when an action succeeds. The candidate list scrolls in place, so the
summary chips and slot selector stay visible while you work down a long list.

![Appointments screen for a slot with summary chips, the Download roster button, and Expected candidates](screenshots/appointments-list.png)

### Normal outcome path

1. Select **Check in** when the candidate arrives at your station. Check-in opens only on the
   Confirmed Slot's head-office date; until then the row says so and the button is unavailable.
2. Select **Complete** when your appointment is finished.

The normal path is Expected → Checked in → Completed. You cannot jump directly from Expected to
Completed.

![Appointments screen after a check-in, showing the CheckedIn status and updated summary chips](screenshots/appointments-after-checkin.png)

### Record a no-show

1. Wait until the four-hour window has ended. Until then the row says No-show is available after the
   window ends.
2. On an Expected row, select **No-show**.
3. Review the candidate name in the confirmation prompt.
4. Select **Confirm**.

No-show remains an outstanding requirement. It may make **Arrange missed appointments** available
to the Coordinator, who can send the candidate a recovery invitation containing only recoverable
missed types.

### Correct a mistake

Corrections move one permitted step back and require confirmation:

- Checked in → select **Correct to expected**.
- Completed → select **Correct to checked in**.
- No-show → select **Correct to expected**.

Read the candidate name in the prompt, then select **Confirm** or **Cancel**. The prompt appears as
an overlay above the candidate list so it draws attention before you act.

![Correction confirmation overlay for a Checked in candidate, with Confirm and Cancel buttons](screenshots/appointments-correction-overlay.png)

A correction changes readiness immediately. A No-show correction can be blocked if a later recovery
invitation or Booking already relies on that outcome. Ask the Coordinator to cancel the pending
recovery first; do not record an unrelated status to work around the conflict.

If another staff member changes the same appointment first, EventBooking refreshes the row and asks
you to review it before trying again.

## Download the roster

**Download roster**, beside the summary chips, saves the selected slot's list as a CSV file so you
can work from paper or a tablet without the app.

1. Select the slot you are delivering.
2. Select **Download roster**. The button reads **Preparing…** while the file is built.
3. The file is saved as `roster-<appointment type>-<date>-<start time>.csv`, for example
   `roster-uniform-fitting-2026-09-18-1300.csv`.

The file holds one row per candidate in the same order as the screen, with these columns:

```text
Candidate Name,Candidate Email,Appointment Type,Status,Checked In At,Outcome At
```

Times are head-office local time. The file is a snapshot taken when you pressed the button — it is
not updated afterwards, and writing on it changes nothing in EventBooking. Every check-in, outcome,
and correction must still be recorded on the screen. Treat the file as personal data: it carries
candidate names and email addresses, so store and dispose of it accordingly.

## How your outcome affects the wider workflow

- Each Appointment Type progresses independently. Completing your row never completes another
  team's row.
- A candidate becomes ready only when every current required type has a Completed outcome across
  the original and any non-cancelled recovery Bookings.
- Expected, Checked in, and No-show all remain outstanding.
- A recovery Booking concludes once every appointment in that recovery is Completed or No-show.
  Another No-show can then be recovered again without deleting the earlier attempts.
- Cancelled Bookings and slots cannot be acted on and may disappear from the active list. A
  Coordinator or Admin can cancel a whole window, which removes it from your selector.
- Every action you record is written to the audit trail with your identity and the time. A
  Coordinator can see it on the candidate's History.

## Troubleshooting

- **No current or upcoming slots** — no active slot in scope contains appointment work. Check your
  Appointment Type with an Admin and the Booking with a Coordinator.
- **Check in is unavailable** — the selected slot's head-office date is not today.
- **No-show is unavailable** — the four-hour window has not ended.
- **Nobody to see in this window** — no candidate in that slot requires your Appointment Type.
- **Candidate is absent** — they may not have booked, may have cancelled, or may be on another slot.
  Ask a Coordinator to inspect the candidate journey.
- **Only one type is visible** — expected. Your profile exposes only its scoped type.
- **The row refreshed after an error** — another staff member changed it first. Review the current
  status before choosing another action.
- **No-show correction is blocked** — a later recovery depends on it. Ask a Coordinator to cancel
  the pending recovery before correcting the original outcome.
- **Roster download failed** — reselect the slot and try again; if it persists the slot may have
  been cancelled while the page was open.
