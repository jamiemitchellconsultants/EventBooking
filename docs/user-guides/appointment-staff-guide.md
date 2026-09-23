# Appointment staff guide

[← All user guides](README.md)

As Appointment staff, you deliver one scoped Appointment Type. The Appointments workspace contains
only operational status: who is expected, who checked in, and whether the appointment was completed
or missed. EventBooking stores no clinical findings, notes, measurements, or results in this flow.

## Before the appointment day

- Sign in and check that the home-page access summary names the correct Appointment Type. Your role
  comes from the company identity provider; the Appointment Type is set by an Admin inside
  EventBooking. If the type is missing or wrong, ask an Admin before the day.
- Open **Appointments** and confirm the expected Confirmed Event appears in the selector.
- If the event or a attendee is missing, ask a Coordinator to check the Booking. Do not create a
  substitute record.
- If you will be working away from a screen, use **Download roster** to take the list with you.

The dependencies are deliberate: Managers negotiate and accept the Event, a
Coordinator issues an invitation, and the attendee must book before a Booking Appointment appears
for you.

## Appointments screen

Use `/appointments` for the full day-of workflow.

1. In **Event**, select the date and four-hour window you are delivering. Only current and upcoming
   windows for your Appointment Type are listed.
2. Confirm the page heading names your Appointment Type.
3. Review the summary chips: Expected, Checked in, Completed, and No-show.
4. Find the attendee row by name and email.
5. Use only the action that matches what has happened at your station.

Each row shows the current Status, Check-in recorded time, Outcome recorded time, and available
Actions. The counts update when an action succeeds. The attendee list scrolls in place, so the
summary chips and event selector stay visible while you work down a long list.


### Normal outcome path

1. Select **Check in** when the attendee arrives at your station. Check-in opens only on the
   Confirmed Event's transitional-location date; until then the row says so and the button is unavailable.
2. Select **Complete** when your appointment is finished.

The normal path is Expected → Checked in → Completed. You cannot jump directly from Expected to
Completed.


### Record a no-show

1. Wait until the four-hour window has ended. Until then the row says No-show is available after the
   window ends.
2. On an Expected row, select **No-show**.
3. Review the attendee name in the confirmation prompt.
4. Select **Confirm**.

No-show remains an outstanding requirement. It may make **Arrange missed appointments** available
to the Coordinator, who can send the attendee a recovery invitation containing only recoverable
missed types.

### Correct a mistake

Corrections move one permitted step back and require confirmation:

- Checked in → select **Correct to expected**.
- Completed → select **Correct to checked in**.
- No-show → select **Correct to expected**.

Read the attendee name in the prompt, then select **Confirm** or **Cancel**. The prompt appears as
an overlay above the attendee list so it draws attention before you act.


A correction changes readiness immediately. A No-show correction can be blocked if a later recovery
invitation or Booking already relies on that outcome. Ask the Coordinator to cancel the pending
recovery first; do not record an unrelated status to work around the conflict.

If another staff member changes the same appointment first, EventBooking refreshes the row and asks
you to review it before trying again.

## Download the roster

**Download roster**, beside the summary chips, saves the selected event's list as a CSV file so you
can work from paper or a tablet without the app.

1. Select the event you are delivering.
2. Select **Download roster**. The button reads **Preparing…** while the file is built.
3. The file is saved as `roster-<appointment type>-<date>-<start time>.csv`, for example
   `roster-uniform-fitting-2026-09-18-1300.csv`.

The file holds one row per attendee in the same order as the screen, with these columns:

```text
Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At
```

Times are transitional-location local time. The file is a snapshot taken when you pressed the button — it is
not updated afterwards, and writing on it changes nothing in EventBooking. Every check-in, outcome,
and correction must still be recorded on the screen. Treat the file as personal data: it carries
attendee names and email addresses, so store and dispose of it accordingly.

## How your outcome affects the wider workflow

- Each Appointment Type progresses independently. Completing your row never completes another
  team's row.
- A attendee becomes ready only when every current required type has a Completed outcome across
  the original and any non-cancelled recovery Bookings.
- Expected, Checked in, and No-show all remain outstanding.
- A recovery Booking concludes once every appointment in that recovery is Completed or No-show.
  Another No-show can then be recovered again without deleting the earlier attempts.
- Cancelled Bookings and events cannot be acted on and may disappear from the active list. A
  Coordinator or Admin can cancel a whole window, which removes it from your selector.
- Every action you record is written to the audit trail with your identity and the time. A
  Coordinator can see it on the attendee's History.

## Troubleshooting

- **No current or upcoming events** — no active event in scope contains appointment work. Check your
  Appointment Type with an Admin and the Booking with a Coordinator.
- **Check in is unavailable** — the selected event's transitional-location date is not today.
- **No-show is unavailable** — the four-hour window has not ended.
- **Nobody to see in this window** — no attendee in that event requires your Appointment Type.
- **Attendee is absent** — they may not have booked, may have cancelled, or may be on another event.
  Ask a Coordinator to inspect the attendee journey.
- **Only one type is visible** — expected. Your profile exposes only its scoped type.
- **The row refreshed after an error** — another staff member changed it first. Review the current
  status before choosing another action.
- **No-show correction is blocked** — a later recovery depends on it. Ask a Coordinator to cancel
  the pending recovery before correcting the original outcome.
- **Roster download failed** — reselect the event and try again; if it persists the event may have
  been cancelled while the page was open.
