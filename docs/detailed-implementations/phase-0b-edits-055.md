# 00b — Vocabulary edits 55 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.SeedData/demo-seed.json — 1/1

<!-- vocabulary-file: {"id":185,"oldPath":"src/EventBooking.SeedData/demo-seed.json","newPath":"src/EventBooking.SeedData/demo-seed.json","beforeSha":"9ac80e0296480e881727aba8f61633a23fa0d4d4d072c4c78af7dd9746f10aaa","afterSha":"597995374d689d75e7deecbbf5e802c935c4af03029c6a8efbc0fac544a0ef68","side":"after","part":1,"parts":1} -->

`````text
{
  "_comment": "Demo dataset. Day offsets resolve against anchorDate so re-runs always match the same windows. Before seeding a fresh environment, set anchorDate to the intended demo date so negative offsets are historical and positive offsets are future-dated. A null headcount on an open proposal is a placeholder: fill it in and re-run the seeder to apply it (filling all three confirms the event). Staff userIds and staffIds must match deploy/keycloak/realm-export.json. Staff display names are not seeded: they are mirrored from the identity provider's token name claim, composed by Keycloak from firstName and lastName.",
  "anchorDate": "2026-09-12",
  "staff": [
    {
      "username": "admin.user",
      "userId": "17e8cd60-b849-470f-a7d1-44ff39993688",
      "staffId": "U000001",
      "roles": ["Admin"],
      "appointmentType": null
    },
    {
      "username": "manager.dat",
      "userId": "9fd390b8-eaff-4da1-accb-8970a3c8a8d1",
      "staffId": "U000002",
      "roles": ["Manager"],
      "appointmentType": "DAT"
    },
    {
      "username": "manager.med",
      "userId": "b5cedaaf-5bba-4b05-99af-cb0dbe5fc4d0",
      "staffId": "U000003",
      "roles": ["Manager"],
      "appointmentType": "MED"
    },
    {
      "username": "manager.uni",
      "userId": "f9f8f3fc-d8e8-4723-ade9-fc5aaebdf104",
      "staffId": "U000004",
      "roles": ["Manager"],
      "appointmentType": "UNI"
    },
    {
      "username": "coordinator.user",
      "userId": "0939229d-aafe-4acb-870e-a25473bc1cea",
      "staffId": "U000005",
      "roles": ["Coordinator"],
      "appointmentType": null
    },
    {
      "username": "appointment.staff",
      "userId": "dc5f9a90-7f54-46d1-8603-d4c317f47226",
      "staffId": "U000006",
      "roles": ["AppointmentStaff"],
      "appointmentType": "UNI"
    }
  ],
  "agreedEvents": [
    {
      "daysOffset": -4,
      "startTime": "09:00",
      "datHeadcount": 8,
      "medHeadcount": 12,
      "uniHeadcount": 6
    },
    {
      "daysOffset": -8,
      "startTime": "09:00",
      "datHeadcount": 8,
      "medHeadcount": 12,
      "uniHeadcount": 6
    },
    {
      "daysOffset": -12,
      "startTime": "09:00",
      "datHeadcount": 8,
      "medHeadcount": 12,
      "uniHeadcount": 6
    }
  ],
  "openProposals": [
    {
      "daysOffset": 2,
      "startTime": "09:00",
      "createdBy": "manager.dat",
      "datHeadcount": 8,
      "medHeadcount": null,
      "uniHeadcount": null
    },
    {
      "daysOffset": 5,
      "startTime": "09:00",
      "createdBy": "manager.med",
      "datHeadcount": null,
      "medHeadcount": 12,
      "uniHeadcount": null
    },
    {
      "daysOffset": 8,
      "startTime": "09:00",
      "createdBy": "manager.uni",
      "datHeadcount": null,
      "medHeadcount": null,
      "uniHeadcount": 6
    },
    {
      "daysOffset": 11,
      "startTime": "09:00",
      "createdBy": "manager.dat",
      "datHeadcount": 10,
      "medHeadcount": null,
      "uniHeadcount": null
    },
    {
      "daysOffset": 14,
      "startTime": "09:00",
      "createdBy": "manager.med",
      "datHeadcount": null,
      "medHeadcount": 14,
      "uniHeadcount": null
    }
  ],
  "attendees": [
    {
      "name": "Demo Attendee 001",
      "email": "demo-attendee-001@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 002",
      "email": "demo-attendee-002@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 003",
      "email": "demo-attendee-003@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 004",
      "email": "demo-attendee-004@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 005",
      "email": "demo-attendee-005@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 006",
      "email": "demo-attendee-006@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 007",
      "email": "demo-attendee-007@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 008",
      "email": "demo-attendee-008@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 009",
      "email": "demo-attendee-009@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 010",
      "email": "demo-attendee-010@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 011",
      "email": "demo-attendee-011@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 012",
      "email": "demo-attendee-012@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 013",
      "email": "demo-attendee-013@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 014",
      "email": "demo-attendee-014@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 015",
      "email": "demo-attendee-015@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 016",
      "email": "demo-attendee-016@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 017",
      "email": "demo-attendee-017@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 018",
      "email": "demo-attendee-018@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 019",
      "email": "demo-attendee-019@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 020",
      "email": "demo-attendee-020@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Attendee 021",
      "email": "demo-attendee-021@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 022",
      "email": "demo-attendee-022@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 023",
      "email": "demo-attendee-023@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 024",
      "email": "demo-attendee-024@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 025",
      "email": "demo-attendee-025@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 026",
      "email": "demo-attendee-026@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 027",
      "email": "demo-attendee-027@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 028",
      "email": "demo-attendee-028@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 029",
      "email": "demo-attendee-029@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 030",
      "email": "demo-attendee-030@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 031",
      "email": "demo-attendee-031@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 032",
      "email": "demo-attendee-032@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 033",
      "email": "demo-attendee-033@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 034",
      "email": "demo-attendee-034@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 035",
      "email": "demo-attendee-035@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 036",
      "email": "demo-attendee-036@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 037",
      "email": "demo-attendee-037@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 038",
      "email": "demo-attendee-038@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 039",
      "email": "demo-attendee-039@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 040",
      "email": "demo-attendee-040@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 041",
      "email": "demo-attendee-041@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 042",
      "email": "demo-attendee-042@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 043",
      "email": "demo-attendee-043@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 044",
      "email": "demo-attendee-044@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 045",
      "email": "demo-attendee-045@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 046",
      "email": "demo-attendee-046@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 047",
      "email": "demo-attendee-047@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 048",
      "email": "demo-attendee-048@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 049",
      "email": "demo-attendee-049@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 050",
      "email": "demo-attendee-050@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Attendee 051",
      "email": "demo-attendee-051@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 052",
      "email": "demo-attendee-052@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 053",
      "email": "demo-attendee-053@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 054",
      "email": "demo-attendee-054@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 055",
      "email": "demo-attendee-055@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 056",
      "email": "demo-attendee-056@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 057",
      "email": "demo-attendee-057@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 058",
      "email": "demo-attendee-058@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 059",
      "email": "demo-attendee-059@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 060",
      "email": "demo-attendee-060@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 061",
      "email": "demo-attendee-061@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 062",
      "email": "demo-attendee-062@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 063",
      "email": "demo-attendee-063@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 064",
      "email": "demo-attendee-064@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 065",
      "email": "demo-attendee-065@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 066",
      "email": "demo-attendee-066@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 067",
      "email": "demo-attendee-067@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 068",
      "email": "demo-attendee-068@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 069",
      "email": "demo-attendee-069@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 070",
      "email": "demo-attendee-070@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 071",
      "email": "demo-attendee-071@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 072",
      "email": "demo-attendee-072@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 073",
      "email": "demo-attendee-073@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 074",
      "email": "demo-attendee-074@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 075",
      "email": "demo-attendee-075@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Attendee 076",
      "email": "demo-attendee-076@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 077",
      "email": "demo-attendee-077@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 078",
      "email": "demo-attendee-078@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 079",
      "email": "demo-attendee-079@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 080",
      "email": "demo-attendee-080@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 081",
      "email": "demo-attendee-081@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 082",
      "email": "demo-attendee-082@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 083",
      "email": "demo-attendee-083@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 084",
      "email": "demo-attendee-084@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 085",
      "email": "demo-attendee-085@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 086",
      "email": "demo-attendee-086@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 087",
      "email": "demo-attendee-087@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 088",
      "email": "demo-attendee-088@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 089",
      "email": "demo-attendee-089@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 090",
      "email": "demo-attendee-090@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "NoShow"
    },
    {
      "name": "Demo Attendee 091",
      "email": "demo-attendee-091@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Attendee 092",
      "email": "demo-attendee-092@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Attendee 093",
      "email": "demo-attendee-093@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Attendee 094",
      "email": "demo-attendee-094@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Attendee 095",
      "email": "demo-attendee-095@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Attendee 096",
      "email": "demo-attendee-096@example.com",
      "attendeeGroup": "CABIN_CREW",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Attendee 097",
      "email": "demo-attendee-097@example.com",
      "attendeeGroup": "PILOTS",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Attendee 098",
      "email": "demo-attendee-098@example.com",
      "attendeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Attendee 099",
      "email": "demo-attendee-099@example.com",
      "attendeeGroup": "ENGINEERING",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Attendee 100",
      "email": "demo-attendee-100@example.com",
      "attendeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "RecoveryCompleted"
    }
  ]
}
`````

## before — src/EventBooking.Web/Layout/CandidateLayout.razor — 1/1

<!-- vocabulary-file: {"id":186,"oldPath":"src/EventBooking.Web/Layout/CandidateLayout.razor","newPath":"src/EventBooking.Web/Layout/AttendeeLayout.razor","beforeSha":"0ef2874bea3185589b681ff310c511d2a00a39db71a395a2457826ff5a2a9378","afterSha":"0ef2874bea3185589b681ff310c511d2a00a39db71a395a2457826ff5a2a9378","side":"before","part":1,"parts":1} -->

`````razor
@inherits LayoutComponentBase

<div class="app-shell">
    <header class="topbar">
        <div class="brand-lockup">
            <BrandMark />
            <div>
                <span class="brand-name">British Airways</span>
                <a class="brand" href="/" aria-label="EventBooking home">EventBooking</a>
            </div>
        </div>
    </header>

    <main class="main-content">
        @Body
    </main>

    <footer class="app-footer">
        <span>British Airways · Recruitment appointments</span>
        <span>Your booking link is personal to you — please do not forward it.</span>
        <a href="/help">Need help?</a>
    </footer>
</div>
`````

## after — src/EventBooking.Web/Layout/AttendeeLayout.razor — 1/1

<!-- vocabulary-file: {"id":186,"oldPath":"src/EventBooking.Web/Layout/CandidateLayout.razor","newPath":"src/EventBooking.Web/Layout/AttendeeLayout.razor","beforeSha":"0ef2874bea3185589b681ff310c511d2a00a39db71a395a2457826ff5a2a9378","afterSha":"0ef2874bea3185589b681ff310c511d2a00a39db71a395a2457826ff5a2a9378","side":"after","part":1,"parts":1} -->

`````razor
@inherits LayoutComponentBase

<div class="app-shell">
    <header class="topbar">
        <div class="brand-lockup">
            <BrandMark />
            <div>
                <span class="brand-name">British Airways</span>
                <a class="brand" href="/" aria-label="EventBooking home">EventBooking</a>
            </div>
        </div>
    </header>

    <main class="main-content">
        @Body
    </main>

    <footer class="app-footer">
        <span>British Airways · Recruitment appointments</span>
        <span>Your booking link is personal to you — please do not forward it.</span>
        <a href="/help">Need help?</a>
    </footer>
</div>
`````

## before — src/EventBooking.Web/Layout/MainLayout.razor — 1/1

<!-- vocabulary-file: {"id":187,"oldPath":"src/EventBooking.Web/Layout/MainLayout.razor","newPath":"src/EventBooking.Web/Layout/MainLayout.razor","beforeSha":"572ce555c47ed10edbb7969a4973bf395e24dd054b7530ec6abb7007bc612605","afterSha":"0e8fb108f23cb2ccab96a60a63d5895402326b70aaa074dba52e965b965ec107","side":"before","part":1,"parts":1} -->

`````razor
@inherits LayoutComponentBase
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication
@using Microsoft.AspNetCore.Components.Authorization
@using EventBooking.Web.Services
@implements IDisposable
@inject NavigationManager Navigation
@inject MeClient Me
@inject AuthenticationStateProvider AuthState

<div class="app-shell">
    <header class="topbar">
        <div class="brand-lockup">
            <BrandMark />
            <div>
                <span class="brand-name">British Airways</span>
                <a class="brand" href="/" aria-label="EventBooking home">EventBooking</a>
            </div>
        </div>
        <AuthorizeView>
            <Authorized>
                <div class="identity" aria-label="Signed-in staff member">
                    <strong>@context.User.Identity?.Name</strong>
                    <span>Staff workspace</span>
                    <button type="button" @onclick="SignOut"
                            title="End this session on this device. Any unsaved changes on the page are discarded.">
                        Sign out
                    </button>
                </div>
            </Authorized>
            <NotAuthorized>
                <a class="sign-in-button" href="authentication/login">Sign in</a>
            </NotAuthorized>
        </AuthorizeView>
    </header>

    @if (_navLinks.Count > 0)
    {
        <nav class="staff-nav" aria-label="Workspace sections">
            @foreach (var link in _navLinks)
            {
                <a class="staff-nav-link @(IsActive(link.Href) ? "active" : null)"
                   href="@link.Href" title="@link.Description">
                    @link.Label
                </a>
            }
        </nav>
    }

    <main class="main-content">
        <CascadingValue Value="_meOutcome">
            @Body
        </CascadingValue>
    </main>

    <footer class="app-footer">
        <span>British Airways · Recruitment appointment coordination</span>
        <span>Times shown at head office local time</span>
    </footer>
</div>

@code {
    private IReadOnlyList<StaffLink> _navLinks = [];

    // Cascaded so pages needing the caller's roles (e.g. Home) reuse this single fetch instead
    // of racing it with a second concurrent call to the same token-protected endpoint.
    private ApiOutcome<MeDto>? _meOutcome;

    protected override async Task OnInitializedAsync()
    {
        Navigation.LocationChanged += OnLocationChanged;
        AuthState.AuthenticationStateChanged += OnAuthenticationStateChanged;

        await LoadMeAsync();
    }

    // Right after the OIDC login redirect completes, the first GetAuthenticationStateAsync call
    // here can still race the provider and see an unauthenticated user even though the header's
    // AuthorizeView (subscribed to this same event via CascadingAuthenticationState) goes on to
    // render as signed in. Without this subscription that miss was permanent: OnInitializedAsync
    // runs once, so _meOutcome stayed null and the workspace tiles never left "Loading…".
    private async void OnAuthenticationStateChanged(Task<AuthenticationState> _)
    {
        await LoadMeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadMeAsync()
    {
        if (_meOutcome is not null)
        {
            return;
        }

        var state = await AuthState.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        _meOutcome = await Me.GetAsync(CancellationToken.None);
        if (_meOutcome.IsSuccess && _meOutcome.Value is not null)
        {
            var links = StaffNavigation.LinksFor(_meOutcome.Value);
            if (links.Count > 0)
            {
                links = [.. links, new StaffLink("/help", "Help", "Read the guide for your role")];
            }

            _navLinks = links;
        }
    }

    private bool IsActive(string href) =>
        string.Equals(
            Navigation.ToBaseRelativePath(Navigation.Uri).TrimEnd('/'),
            href.TrimStart('/').TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => StateHasChanged();

    // Sign-out must start as in-page navigation: landing on authentication/logout
    // any other way makes the framework reject it as not initiated from the page.
    private void SignOut() =>
        Navigation.NavigateToLogout("authentication/logout");

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        AuthState.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
`````

## after — src/EventBooking.Web/Layout/MainLayout.razor — 1/1

<!-- vocabulary-file: {"id":187,"oldPath":"src/EventBooking.Web/Layout/MainLayout.razor","newPath":"src/EventBooking.Web/Layout/MainLayout.razor","beforeSha":"572ce555c47ed10edbb7969a4973bf395e24dd054b7530ec6abb7007bc612605","afterSha":"0e8fb108f23cb2ccab96a60a63d5895402326b70aaa074dba52e965b965ec107","side":"after","part":1,"parts":1} -->

`````razor
@inherits LayoutComponentBase
@using Microsoft.AspNetCore.Components.WebAssembly.Authentication
@using Microsoft.AspNetCore.Components.Authorization
@using EventBooking.Web.Services
@implements IDisposable
@inject NavigationManager Navigation
@inject MeClient Me
@inject AuthenticationStateProvider AuthState

<div class="app-shell">
    <header class="topbar">
        <div class="brand-lockup">
            <BrandMark />
            <div>
                <span class="brand-name">British Airways</span>
                <a class="brand" href="/" aria-label="EventBooking home">EventBooking</a>
            </div>
        </div>
        <AuthorizeView>
            <Authorized>
                <div class="identity" aria-label="Signed-in staff member">
                    <strong>@context.User.Identity?.Name</strong>
                    <span>Staff workspace</span>
                    <button type="button" @onclick="SignOut"
                            title="End this session on this device. Any unsaved changes on the page are discarded.">
                        Sign out
                    </button>
                </div>
            </Authorized>
            <NotAuthorized>
                <a class="sign-in-button" href="authentication/login">Sign in</a>
            </NotAuthorized>
        </AuthorizeView>
    </header>

    @if (_navLinks.Count > 0)
    {
        <nav class="staff-nav" aria-label="Workspace sections">
            @foreach (var link in _navLinks)
            {
                <a class="staff-nav-link @(IsActive(link.Href) ? "active" : null)"
                   href="@link.Href" title="@link.Description">
                    @link.Label
                </a>
            }
        </nav>
    }

    <main class="main-content">
        <CascadingValue Value="_meOutcome">
            @Body
        </CascadingValue>
    </main>

    <footer class="app-footer">
        <span>British Airways · Recruitment appointment coordination</span>
        <span>Times shown at transitional location local time</span>
    </footer>
</div>

@code {
    private IReadOnlyList<StaffLink> _navLinks = [];

    // Cascaded so pages needing the caller's roles (e.g. Home) reuse this single fetch instead
    // of racing it with a second concurrent call to the same token-protected endpoint.
    private ApiOutcome<MeDto>? _meOutcome;

    protected override async Task OnInitializedAsync()
    {
        Navigation.LocationChanged += OnLocationChanged;
        AuthState.AuthenticationStateChanged += OnAuthenticationStateChanged;

        await LoadMeAsync();
    }

    // Right after the OIDC login redirect completes, the first GetAuthenticationStateAsync call
    // here can still race the provider and see an unauthenticated user even though the header's
    // AuthorizeView (subscribed to this same event via CascadingAuthenticationState) goes on to
    // render as signed in. Without this subscription that miss was permanent: OnInitializedAsync
    // runs once, so _meOutcome stayed null and the workspace tiles never left "Loading…".
    private async void OnAuthenticationStateChanged(Task<AuthenticationState> _)
    {
        await LoadMeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadMeAsync()
    {
        if (_meOutcome is not null)
        {
            return;
        }

        var state = await AuthState.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        _meOutcome = await Me.GetAsync(CancellationToken.None);
        if (_meOutcome.IsSuccess && _meOutcome.Value is not null)
        {
            var links = StaffNavigation.LinksFor(_meOutcome.Value);
            if (links.Count > 0)
            {
                links = [.. links, new StaffLink("/help", "Help", "Read the guide for your role")];
            }

            _navLinks = links;
        }
    }

    private bool IsActive(string href) =>
        string.Equals(
            Navigation.ToBaseRelativePath(Navigation.Uri).TrimEnd('/'),
            href.TrimStart('/').TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => StateHasChanged();

    // Sign-out must start as in-page navigation: landing on authentication/logout
    // any other way makes the framework reject it as not initiated from the page.
    private void SignOut() =>
        Navigation.NavigateToLogout("authentication/logout");

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        AuthState.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
`````
