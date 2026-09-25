# Coordinator acceptance script

[← Acceptance pack](README.md) · [Coordinator guide](../user-guides/coordinator-guide.md)

**You are checking:** attendee creation, invitation, follow-up and readiness. Use a Coordinator test account and a test mailbox you control. A Manager must provide enough future event options for the selected Attendee Group. Use a unique test attendee name and email for the write steps.

## COO-01 — Find the work queues

1. Sign in. **Expect:** **Attendees**, **Dashboards**, **Event operations**, and **Audit search** under **Your work**; read-only **Locations**, **Appointment types**, and **Attendee groups** under **Reference data**. **Staff access** and **System settings** are absent.
2. Open **Dashboards**. Switch between **Awaiting availability**, **No response**, and **Events**. **Expect:** each tab shows its own count and rows or an empty message. Use the Location filter and note whether the data changes as expected.
3. Open **Attendees**. Use Status, Group, Readiness, and **Search by name or email…** to locate an existing test attendee. **Expect:** the row shows required types, status, readiness, delivery, booking and history.

**Result:** Pass / Fail / Blocked / Not run

**Actual result and evidence:** ____________________

## COO-02 — Create a test attendee and issue an invitation

1. Select **New attendee**. Enter the reserved name and test mailbox, then select an active Attendee group. **Expect:** the required appointment type chips follow the group choice and cannot be edited individually.
2. Select **Save attendee**, then search for the new name. **Expect:** one row with the correct group-derived types and a not-yet-invited status.
3. Select **Invite** on that row. In **Offer events at**, choose one or more Locations. **Expect:** the page reports how many eligible events are available and how many options an invitation requires. If too few events exist, ask Managers to confirm more suitable future events; do not keep sending.
4. Select **Send invite**. **Expect:** the row moves to Invited or another clear delivery state, and the test mailbox receives a personal **Choose a time** link. Do not paste the link into the test log.

**Result:** Pass / Fail / Blocked / Not run

**Test attendee identifier, group and evidence:** ____________________

## COO-03 — Validate the CSV import boundary

1. Select **Import**. Prepare a small CSV with the header `name,email,attendee_group`, one new test attendee in an active group, and one row with an unknown group code. Select the file in **CSV file**, then **Upload**.
2. **Expect:** line-numbered errors and **Nothing was imported**. Search for the valid row's unique name to confirm it was not added.
3. Correct the invalid row to an active group code and upload the whole file again. **Expect:** a successful imported count and both new attendee rows. If the test mailbox or unique addresses are unavailable, mark this case Blocked before uploading.

**Result:** Pass / Fail / Blocked / Not run

**CSV filename, imported count and evidence:** ____________________

## COO-04 — Follow a booking and readiness

1. After the Attendee tester confirms a time, search for that attendee. **Expect:** the status is Booked and the **Bookings** control reveals the active visit.
2. Select **Check readiness** or the displayed readiness badge. **Expect:** outstanding appointment types are listed until Appointment staff complete them.
3. After the Appointment staff tester records a Completed outcome, refresh readiness. **Expect:** the completed type no longer appears as outstanding. An attendee requiring several types is Ready only after all required types are Completed.
4. Open **History** for the attendee. **Expect:** invitation, booking and appointment changes with actor and time. Do not record personal links from the audit detail.

**Result:** Pass / Fail / Blocked / Not run

**Before/after readiness and evidence:** ____________________

## COO-05 — Explore missed-appointment recovery

1. Find a designated test attendee whose latest outstanding appointment is No-show, with no active recovery. Expand their readiness. **Expect:** the missed type is identified as recoverable and **Arrange missed appointments** is available.
2. Select **Arrange missed appointments** only if this attendee is reserved for the test. **Expect:** a recovery-started message that names the missed type or types; a fresh link reaches the test mailbox when eligible capacity exists.
3. If a pending recovery already exists, observe **Cancel recovery** and its second confirmation, but leave it in place unless the test lead reserved it for cancellation.

**Result:** Pass / Fail / Blocked / Not run

**Recovery scope and evidence:** ____________________

## COO-06 — Follow up an unanswered invitation or failed email

1. On **Dashboards**, open **No response** and find a designated test attendee. **Expect:** **Re-invite now** is available. Select it only when that attendee and mailbox are reserved for this run. **Expect:** a fresh invitation and a restarted follow-up window.
2. On **Attendees**, find a designated row with Delivery marked Failed. **Expect:** **Resend** is available. Select it once after the test lead has confirmed the mailbox issue is resolved. **Expect:** the delivery state changes after dispatch. Do not create a duplicate attendee or repeat **Invite** to repair email delivery.
3. If either state is absent, mark that step Not run and record which fixture is needed.

**Result:** Pass / Fail / Blocked / Not run

**Follow-up and delivery evidence:** ____________________

## COO-07 — Cancel a reserved event and trace its bookings

1. On **Event operations**, find a future event reserved for cancellation with at least one test booking. Record its Location, window and active booking count, then select **Cancel event**. **Expect:** the confirmation states how many bookings will be affected.
2. Select **Confirm cancellation**. **Expect:** a result states how many bookings were cancelled, re-invited or left awaiting availability; the event is no longer bookable.
3. On **Attendees**, check one affected test attendee's Booking, Status, Delivery and History. **Expect:** the old booking is no longer active and a replacement invitation or awaiting-availability state is visible. Ask the Attendee tester to check the test mailbox for any new link.

**Result:** Pass / Fail / Blocked / Not run

**Event, affected booking and evidence:** ____________________
