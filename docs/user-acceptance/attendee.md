# Attendee acceptance script

[← Acceptance pack](README.md) · [Attendee guide](../user-guides/attendee-guide.md)

**You are checking:** the private booking journey, preferably on a phone as well as a desktop browser. Use a fresh invitation sent to a test mailbox you control. The Coordinator should reserve the attendee and event options for this run. You do not need a staff account.

## ATT-01 — Choose and confirm a time

1. Open the **Choose a time** link in the test invitation email. **Expect:** your name, required appointments, and one or more future options showing Location, address, local date, time and time zone. If the name or appointments are wrong, stop and tell the Coordinator.
2. Select one option. **Expect:** **Confirm this time** becomes available. Before selecting it, check that the window and site are workable for you.
3. Select **Confirm this time** once. **Expect:** **Booking confirmed**, the chosen site and window, and **Use your booking management link**. Save the management link privately; the confirmation email should also arrive.
4. Return to the invitation link in a separate tab. **Expect:** **This link has expired.** and no option to create another booking. Record the screen wording without sharing the token.

**Result:** Pass / Fail / Blocked / Not run

**Chosen window and evidence:** ____________________

## ATT-02 — Review and change a reserved booking

1. Open the private management link from ATT-01. **Expect:** **Manage your booking** shows your name, Location, address, window and appointment types.
2. For a booking reserved for rescheduling, select **Cancel and choose a new time**. **Expect:** **Booking cancelled** and a message saying whether a fresh invitation is on its way, already pending, or unavailable. The old booking must no longer stand.
3. If a fresh invitation arrives, open it and choose another time using ATT-01. **Expect:** one new booking, with its own management link. If no suitable time exists, the page should explain that the team will follow up.
4. On a separate reserved booking, use **Cancel booking**. **Expect:** **Booking cancelled** without an automatic request for replacement times. This step is Not run if no second reserved booking exists.

**Result:** Pass / Fail / Blocked / Not run

**Cancellation outcome and evidence:** ____________________

## ATT-03 — Explore link and mobile behaviour

1. Open [Help](https://eventbooking.tqaentry.com/help) while signed out. **Expect:** attendee guidance without staff navigation.
2. With a test link known to be expired or invalid, open the link. **Expect:** an invitation link says **This link has expired.**; an invalid management link says **This link has expired** and **This booking link is no longer valid.** Both direct you to the Coordinator contact. Never test this by editing a real person's token.
3. Repeat the invitation review on a phone-sized screen. **Expect:** all options, addresses, buttons and messages can be read and used without horizontal scrolling. Try keyboard focus or a screen reader if available, and record where the next action is unclear.

**Result:** Pass / Fail / Blocked / Not run

**Device, accessibility observations and evidence:** ____________________
