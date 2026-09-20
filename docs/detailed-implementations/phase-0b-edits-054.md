# 00b — Vocabulary edits 54 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.SeedData/demo-seed.json — 1/1

<!-- vocabulary-file: {"id":185,"oldPath":"src/EventBooking.SeedData/demo-seed.json","newPath":"src/EventBooking.SeedData/demo-seed.json","beforeSha":"9ac80e0296480e881727aba8f61633a23fa0d4d4d072c4c78af7dd9746f10aaa","afterSha":"597995374d689d75e7deecbbf5e802c935c4af03029c6a8efbc0fac544a0ef68","side":"before","part":1,"parts":1} -->

`````text
{
  "_comment": "Demo dataset. Day offsets resolve against anchorDate so re-runs always match the same windows. Before seeding a fresh environment, set anchorDate to the intended demo date so negative offsets are historical and positive offsets are future-dated. A null headcount on an open proposal is a placeholder: fill it in and re-run the seeder to apply it (filling all three confirms the slot). Staff userIds and staffIds must match deploy/keycloak/realm-export.json. Staff display names are not seeded: they are mirrored from the identity provider's token name claim, composed by Keycloak from firstName and lastName.",
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
  "agreedSlots": [
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
  "candidates": [
    {
      "name": "Demo Candidate 001",
      "email": "demo-candidate-001@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 002",
      "email": "demo-candidate-002@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 003",
      "email": "demo-candidate-003@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 004",
      "email": "demo-candidate-004@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 005",
      "email": "demo-candidate-005@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 006",
      "email": "demo-candidate-006@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 007",
      "email": "demo-candidate-007@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 008",
      "email": "demo-candidate-008@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 009",
      "email": "demo-candidate-009@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 010",
      "email": "demo-candidate-010@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 011",
      "email": "demo-candidate-011@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 012",
      "email": "demo-candidate-012@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 013",
      "email": "demo-candidate-013@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 014",
      "email": "demo-candidate-014@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 015",
      "email": "demo-candidate-015@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 016",
      "email": "demo-candidate-016@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 017",
      "email": "demo-candidate-017@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 018",
      "email": "demo-candidate-018@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 019",
      "email": "demo-candidate-019@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 020",
      "email": "demo-candidate-020@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Unbooked"
    },
    {
      "name": "Demo Candidate 021",
      "email": "demo-candidate-021@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 022",
      "email": "demo-candidate-022@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 023",
      "email": "demo-candidate-023@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 024",
      "email": "demo-candidate-024@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 025",
      "email": "demo-candidate-025@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 026",
      "email": "demo-candidate-026@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 027",
      "email": "demo-candidate-027@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 028",
      "email": "demo-candidate-028@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 029",
      "email": "demo-candidate-029@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 030",
      "email": "demo-candidate-030@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 031",
      "email": "demo-candidate-031@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 032",
      "email": "demo-candidate-032@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 033",
      "email": "demo-candidate-033@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 034",
      "email": "demo-candidate-034@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 035",
      "email": "demo-candidate-035@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 036",
      "email": "demo-candidate-036@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 037",
      "email": "demo-candidate-037@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 038",
      "email": "demo-candidate-038@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 039",
      "email": "demo-candidate-039@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 040",
      "email": "demo-candidate-040@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 041",
      "email": "demo-candidate-041@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 042",
      "email": "demo-candidate-042@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 043",
      "email": "demo-candidate-043@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 044",
      "email": "demo-candidate-044@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 045",
      "email": "demo-candidate-045@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 046",
      "email": "demo-candidate-046@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 047",
      "email": "demo-candidate-047@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 048",
      "email": "demo-candidate-048@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 049",
      "email": "demo-candidate-049@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 050",
      "email": "demo-candidate-050@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Ready"
    },
    {
      "name": "Demo Candidate 051",
      "email": "demo-candidate-051@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 052",
      "email": "demo-candidate-052@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 053",
      "email": "demo-candidate-053@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 054",
      "email": "demo-candidate-054@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 055",
      "email": "demo-candidate-055@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 056",
      "email": "demo-candidate-056@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 057",
      "email": "demo-candidate-057@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 058",
      "email": "demo-candidate-058@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 059",
      "email": "demo-candidate-059@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 060",
      "email": "demo-candidate-060@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 061",
      "email": "demo-candidate-061@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 062",
      "email": "demo-candidate-062@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 063",
      "email": "demo-candidate-063@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 064",
      "email": "demo-candidate-064@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 065",
      "email": "demo-candidate-065@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 066",
      "email": "demo-candidate-066@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 067",
      "email": "demo-candidate-067@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 068",
      "email": "demo-candidate-068@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 069",
      "email": "demo-candidate-069@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 070",
      "email": "demo-candidate-070@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 071",
      "email": "demo-candidate-071@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 072",
      "email": "demo-candidate-072@example.com",
      "employeeGroup": "PILOTS",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 073",
      "email": "demo-candidate-073@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 074",
      "email": "demo-candidate-074@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 075",
      "email": "demo-candidate-075@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "Outstanding"
    },
    {
      "name": "Demo Candidate 076",
      "email": "demo-candidate-076@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 077",
      "email": "demo-candidate-077@example.com",
      "employeeGroup": "PILOTS",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 078",
      "email": "demo-candidate-078@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 079",
      "email": "demo-candidate-079@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 080",
      "email": "demo-candidate-080@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 081",
      "email": "demo-candidate-081@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 082",
      "email": "demo-candidate-082@example.com",
      "employeeGroup": "PILOTS",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 083",
      "email": "demo-candidate-083@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 084",
      "email": "demo-candidate-084@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 085",
      "email": "demo-candidate-085@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 086",
      "email": "demo-candidate-086@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 087",
      "email": "demo-candidate-087@example.com",
      "employeeGroup": "PILOTS",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 088",
      "email": "demo-candidate-088@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 089",
      "email": "demo-candidate-089@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 090",
      "email": "demo-candidate-090@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "NoShow"
    },
    {
      "name": "Demo Candidate 091",
      "email": "demo-candidate-091@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Candidate 092",
      "email": "demo-candidate-092@example.com",
      "employeeGroup": "PILOTS",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Candidate 093",
      "email": "demo-candidate-093@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Candidate 094",
      "email": "demo-candidate-094@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Candidate 095",
      "email": "demo-candidate-095@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Candidate 096",
      "email": "demo-candidate-096@example.com",
      "employeeGroup": "CABIN_CREW",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Candidate 097",
      "email": "demo-candidate-097@example.com",
      "employeeGroup": "PILOTS",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Candidate 098",
      "email": "demo-candidate-098@example.com",
      "employeeGroup": "GROUND_OPERATIONS_AGENT",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Candidate 099",
      "email": "demo-candidate-099@example.com",
      "employeeGroup": "ENGINEERING",
      "journey": "RecoveryCompleted"
    },
    {
      "name": "Demo Candidate 100",
      "email": "demo-candidate-100@example.com",
      "employeeGroup": "GROUND_TRANSPORT_SERVICES",
      "journey": "RecoveryCompleted"
    }
  ]
}
`````
