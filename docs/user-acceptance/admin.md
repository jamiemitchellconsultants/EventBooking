# Admin acceptance script

[← Acceptance pack](README.md) · [Admin guide](../user-guides/admin-guide.md)

**You are checking:** reference data, invitation settings, staff appointment-type scope, and the Admin data boundary. Use an Admin test account. Ask the test lead which records and settings you may change, and record their original values so they can be restored.

## ADM-01 — Find your workspaces and access boundary

1. Sign in at [EventBooking](https://eventbooking.tqaentry.com/). **Expect:** the home page shows the Admin role and no appointment-type scope.
2. In **Administration**, find **Locations**, **Appointment types**, **Attendee groups**, **System settings**, and **Staff access**. In **Your work**, find **Event operations** and **Audit search**. Open **Help**. **Expect:** the pages load and the Admin guide is available.
3. Inspect the home page and navigation. **Expect:** no **Attendees**, **Dashboards**, **Negotiation board**, or **Appointment workspace** link. An Admin must not be able to read attendee records through the application.

**Result:** Pass / Fail / Blocked / Not run

**Actual result and evidence:** ____________________

## ADM-02 — Explore reference data

1. Open **Locations**. Find an active site, read its address and time zone, then select **Edit**. **Expect:** Code is fixed; Name, Address, Time zone and Active are visible. Select **Cancel** without saving.
2. Open **Appointment types**. **Expect:** each row shows Code, Name, Manager, Status and **Edit**. A type without a Manager is visibly identified.
3. Open **Attendee groups**. Open one row with **Edit**. **Expect:** its required appointment types are selected. Select **Cancel** without saving.
4. If the test lead reserved a unique code for you, create one test Location with **New location** and a valid IANA time zone, then confirm it appears in the table. Leave it active for later tests. Otherwise mark this step Not run.
5. If the test lead reserved a second unique code, select **New attendee group**, enter that Code and a Name, and select at least one active appointment type with a Manager. Select **Save**. **Expect:** the new group appears with those required types. Otherwise mark this step Not run.

**Result:** Pass / Fail / Blocked / Not run

**Actual result and evidence:** ____________________

## ADM-03 — Check invitation settings

1. Open **System settings**. Record **Invitation expiry (days)**, **Automatic retries**, and **Options per invitation**. **Expect:** all three current values are visible; options per invitation is between 1 and 5.
2. If settings changes are reserved for this run, change one value by 1 within its displayed range and select **Save changes**. Refresh the page. **Expect:** the new value remains.
3. Restore the original value and save again. **Expect:** it remains after refresh. Existing invitations should retain the settings captured when they were issued; confirm that with the Coordinator if an existing test invitation is available.

**Result:** Pass / Fail / Blocked / Not run

**Original and restored values / evidence:** ____________________

## ADM-04 — Check a scoped staff account

1. Open **Staff access**. Find the designated Manager or Appointment staff test account. **Expect:** Staff, Roles, Scope, and Change scope are shown; roles have no edit control.
2. For a designated unscoped test account, choose its approved appointment type in **Scope for …** and select **Save scope**. **Expect:** a success message and the new scope in the row. Ask that person to refresh their home page and confirm the appointment type appears there.
3. If the test plan calls for a temporary scope, restore it to **No scope** and select **Save scope**. **Expect:** the row returns to **No scope**. Do not change a working Manager's scope, which may displace them from their appointment type.

**Result:** Pass / Fail / Blocked / Not run

**Account, before/after scope and evidence:** ____________________

## ADM-05 — Review operations and history

1. Open **Event operations**. Use Location, From and To to find a future event. **Expect:** each row shows its location, local time, appointment types, remaining capacity and active booking count.
2. Select **Cancel event** on a reserved event, then select **Keep event**. **Expect:** the confirmation can be dismissed and the event remains. Do not select **Confirm cancellation** unless this event was explicitly reserved for cancellation.
3. Open **Audit search**. Set a date range covering your reference-data or scope change and select **Search**. **Expect:** a matching operational change with When, What, Who and Details. Attendee records should not be visible to Admin.

**Result:** Pass / Fail / Blocked / Not run

**Actual result and evidence:** ____________________
