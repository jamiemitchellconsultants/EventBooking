# Appointment staff acceptance script

[← Acceptance pack](README.md) · [Appointment staff guide](../user-guides/appointment-staff-guide.md)

**You are checking:** one appointment type's roster and day-of status changes. Use a scoped Appointment staff test account and a booking reserved for this test. Check-in is available on the event's local date; No-show is available only after its window ends. Plan those cases for the right time, or mark them Blocked.

## APS-01 — Find the scoped roster

1. Sign in and check the home page. **Expect:** the Appointment staff role, your assigned appointment type, **Appointment workspace** under **Your work**, and a **Help** section. If the type is missing or wrong, ask an Admin to set the scope before continuing.
2. Open **Appointment workspace**. **Expect:** the heading names your type. The Event selector groups active events by Location and displays their local date, time and time zone.
3. Choose the event reserved for the test. **Expect:** a row for each booked attendee who needs your type, with Status and the actions currently allowed. If no one is booked, the page says **No appointments on this event yet.**
4. Select **Download roster (CSV)**. **Expect:** a CSV file for the selected event. Check its columns and one reserved attendee, then store or dispose of the file according to the test lead's data-handling instructions.

**Result:** Pass / Fail / Blocked / Not run

**Type, event and evidence:** ____________________

## APS-02 — Check in and complete an appointment

1. On the event's local date, locate the reserved attendee with status Expected and select **Check in**. **Expect:** the row refreshes to Checked in and offers **Complete**.
2. After the appointment is actually delivered in the test scenario, select **Complete**. **Expect:** status Completed. Ask the Coordinator to refresh readiness; this type should no longer be outstanding.
3. If another staff member changed the row first, **Expect:** a message that it changed elsewhere and a refreshed row. Review its current status before taking any further action.

**Result:** Pass / Fail / Blocked / Not run

**Attendee, before/after status and evidence:** ____________________

## APS-03 — Check timing and correction controls

1. Before a reserved event ends, inspect an Expected row. **Expect:** **No-show is available after the event ends**; do not mark someone absent early.
2. After a reserved event ends, select **No-show** on an Expected test row. **Expect:** status No-show. Ask the Coordinator to inspect the attendee's recoverable readiness.
3. On a separate reserved row, use **Undo check-in** after a mistaken check-in. **Expect:** status Expected. On a No-show row without a dependent recovery, use **Correct**. **Expect:** status Expected. These correction steps are Not run when suitable test rows are unavailable.

**Result:** Pass / Fail / Blocked / Not run

**Timing, correction and evidence:** ____________________
