# Manager acceptance script

[← Acceptance pack](README.md) · [Manager guide](../user-guides/manager-guide.md)

**You are checking:** negotiation and capacity for one appointment type. Use a Manager test account whose scope is set by Admin. To confirm a proposal listing several types, arrange for the other listed Managers to participate. Use a future window and Location reserved for this run.

## MGR-01 — Understand your scope and existing events

1. Sign in and read the role and appointment type on the home page. **Expect:** **Negotiation board** and **Appointment workspace** appear under **Your work**; read-only **Locations**, **Appointment types**, and **Attendee groups** appear under **Reference data**. A combined Coordinator and Manager account also has Coordinator links.
2. Open **Negotiation board**. **Expect:** its heading names your appointment type; **Open proposals** and **Confirmed events** are separate lists.
3. On an open proposal, compare **Types**, **Accepted**, and **You**. **Expect:** the accepted count is visible, but other Managers' headcounts are not. On a confirmed event, find Total and Remaining capacity for your type.
4. Open **Help**. **Expect:** the Manager guide is available.

**Result:** Pass / Fail / Blocked / Not run

**Actual result and evidence:** ____________________

## MGR-02 — Propose and agree a test event

1. Select **Propose event**. Choose an active Location, a future Date, Start, a Duration in 15-minute steps, and your positive **Your headcount**. **Expect:** the displayed end time stays on the same local date.
2. Select your own appointment type and one other active type with a Manager. **Expect:** your own type is selected and cannot be removed; a type with **No Manager assigned** cannot be selected.
3. Select **Propose**. **Expect:** a new row appears under Open proposals with your type already accepted and the count at 1 of 2. Record the Location and window for the second Manager.
4. Have the second Manager sign in, find that proposal, enter a positive number in **Your headcount**, and select **Accept**. **Expect:** the open proposal becomes a confirmed event. Return to your board and refresh to see it under **Confirmed events**.

**Result:** Pass / Fail / Blocked / Not run

**Event window, participating types and evidence:** ____________________

## MGR-03 — Change your own capacity

1. Choose a reserved confirmed event that includes your type. Record its current Total and Remaining figures.
2. Enter a replacement total one higher than the current total and select **Save** beside your type. **Expect:** Total and Remaining both increase by one, with no change to other types.
3. Restore the original total and select **Save**. **Expect:** the original figures return if no booking occurred in between. If a booking changed demand, stop and record the new values instead of forcing a lower total.
4. Where a test event already has active bookings for your type, try a total below the booked count. **Expect:** the change is refused and the prior capacity remains. This step is Not run if its precondition is unavailable.

**Result:** Pass / Fail / Blocked / Not run

**Before/after capacity and evidence:** ____________________

## MGR-04 — Explore delivery access

1. Open **Appointment workspace**. **Expect:** the heading names the same appointment type and the Event selector contains only events for that type.
2. Select an event and inspect its roster. **Expect:** only appointments for your scoped type appear. If there are no rows, the page says so.
3. Note any event or type you expected to see but cannot. Ask the Coordinator to check booking and the Admin to check scope before treating absence as a defect.

**Result:** Pass / Fail / Blocked / Not run

**Actual result and evidence:** ____________________
