# Administrator guide

Administrators keep the reference data the rest of the service books against: the places events happen, the appointment types on offer, the groups attendees belong to, and who on the staff can do what.

## Locations

Locations live under Admin, then Locations. Each location has a code, a name, an address and a time zone. Events can only be proposed at active locations, so retiring a site is a matter of deactivating it rather than deleting it — past bookings keep their history.

If someone else edits the same location while you are working, saving shows a version conflict. Reload the row to see their values, then reapply your change.

## Appointment types and attendee groups

Appointment types are the kinds of appointment the service offers, such as a medical check. Each type can have a manager, who accepts proposed events on its behalf during negotiation.

Attendee groups decide which appointment types each attendee must attend. Assigning an attendee to a group sets the appointments they owe; moving them to another group re-sets the requirement from that moment on.

## Staff access

Staff access lists every staff account, the roles each account holds, and — for managers — which appointment type their decisions are scoped to. Changes apply the next time the staff member signs in. Someone with no role yet sees a holding page rather than the workspace links.

## Settings

Settings holds the service-wide numbers: how long invitations stay valid, how many times a failed email is retried automatically, and how many options each invitation should offer. Changes apply to invitations created afterwards.

## Event operations

Event operations is the shared board for confirmed events. Administrators usually arrive here to investigate rather than to act: the board shows each event with its remaining capacity per appointment type and its active bookings.

## Audit search

Audit search answers what changed, who changed it and when. Filter by date range, actor, action or entity, then page through the results. Timestamps are shown in UTC so entries from every location compare directly.
