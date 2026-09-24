# Coordinator guide

Coordinators own the attendee journey: bringing people in, inviting them to book, chasing the ones who go quiet, and arranging new times when appointments are missed.

## Attendees

The Attendees page is the working list. Filter it by invitation status, attendee group, readiness or a name and email search, then act on each row:

- New attendee adds someone without inviting them yet. The attendee group you pick decides which appointment types they must attend.
- Invite opens a panel showing how many events the invite would offer at the chosen locations. Narrow the locations only when the attendee can only reach some of them.
- Check readiness shows which appointments the attendee still owes. When some are marked recoverable, Arrange missed appointments emails a fresh booking link covering only those.
- The booking cell lists active bookings and cancels them, asking for confirmation first.
- History shows every recorded change for that attendee and who made it.

Import brings in several attendees from a CSV file at once. The header line must read exactly name, email, attendee group, and the whole file is accepted or rejected together — if any row has an error, nothing is imported and every faulty line is listed so the file can be fixed and uploaded again.

## Dashboards

Dashboards answers three questions at a glance: who is still waiting to be invited, who was invited but never responded, and what confirmed events are coming up. The no-response tab offers a re-invite for each stuck attendee, which sends a fresh link and restarts the follow-up window.

## Event operations

Event operations is the shared board for confirmed events: each event with its remaining capacity per appointment type and its active bookings. Coordinators watch it to see where invited attendees still have room to book.

## Audit search

Audit search answers what changed, who changed it and when. Filter by date range, actor, action or entity, then page through the results. Timestamps are shown in UTC so entries from every location compare directly.
