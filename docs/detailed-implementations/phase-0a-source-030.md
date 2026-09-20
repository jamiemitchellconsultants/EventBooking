# 00a — Port source 30 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.SeedData/demo-seed.json — 1/1

<!-- port-file: {"path":"src/EventBooking.SeedData/demo-seed.json","encoding":"utf8","sha256":"9ac80e0296480e881727aba8f61633a23fa0d4d4d072c4c78af7dd9746f10aaa","parts":1,"part":1} -->

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

## src/EventBooking.SeedData/DemoEmailOptions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.SeedData/DemoEmailOptions.cs","encoding":"utf8","sha256":"18b842cde9c6996f77671f27098fc40137f6f8cb1d7b2699c4554d41a5ea3970","parts":1,"part":1} -->

`````csharp
using System.Globalization;
using System.Net.Mail;
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.SeedData;

/// <summary>Validated demo SMTP settings and the API-compatible candidate link configuration.</summary>
public sealed class DemoEmailOptions
{
    private DemoEmailOptions(CandidatePortalOptions portal, TokenOptions tokens,
        EmailOptions sender, SmtpOptions smtp, HeadOfficeOptions headOffice)
    {
        Portal = portal;
        Tokens = tokens;
        Sender = sender;
        Smtp = smtp;
        HeadOffice = headOffice;
    }

    /// <summary>Gets the public candidate portal URL, office address and coordinator contact.</summary>
    public CandidatePortalOptions Portal { get; }
    /// <summary>Gets the signing key that must match the API validating candidate tokens.</summary>
    public TokenOptions Tokens { get; }
    /// <summary>Gets the sender identity used for demo messages over SMTP.</summary>
    public EmailOptions Sender { get; }
    /// <summary>Gets the Mailpit SMTP host and port reachable from this process.</summary>
    public SmtpOptions Smtp { get; }
    /// <summary>Gets the timezone used to determine future head-office dates.</summary>
    public HeadOfficeOptions HeadOffice { get; }

    /// <summary>Reads environment-style settings, with local defaults and explicit non-local keys.</summary>
    /// <param name="readSetting">Returns a setting value, or null when the key is absent.</param>
    /// <returns>Configuration validated before the host starts issuing demo invitations.</returns>
    /// <exception cref="SeedException">A required setting is missing, blank or invalid.</exception>
    public static DemoEmailOptions From(Func<string, string?> readSetting)
    {
        string Read(string key, string? fallback)
        {
            var value = readSetting(key) ?? fallback;
            if (string.IsNullOrWhiteSpace(value))
                throw Invalid(key);
            return value;
        }

        var baseUrl = Read("Portal__BaseUrl", "http://localhost:5002");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw Invalid("Portal__BaseUrl");

        var signingKey = Read("Tokens__SigningKey", uri.IsLoopback
            ? "a-local-signing-key-that-is-at-least-32-characters" : null);
        if (signingKey.Length < 32)
            throw Invalid("Tokens__SigningKey");
        var smtpHost = Read("Email__Smtp__Host", uri.IsLoopback ? "localhost" : null);
        var portText = Read("Email__Smtp__Port", "1025");
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port < 1 || port > 65535)
            throw Invalid("Email__Smtp__Port");
        var address = Read("Email__FromAddress", "recruitment@example.com");
        if (!MailAddress.TryCreate(address, out var mailbox)
            || !string.Equals(mailbox.Address, address, StringComparison.Ordinal))
            throw Invalid("Email__FromAddress");
        var name = Read("Email__FromName", "Recruitment Team");
        var timezone = Read("HeadOffice__TimeZoneId", "Europe/London");
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw Invalid("HeadOffice__TimeZoneId");
        }
        catch (InvalidTimeZoneException)
        {
            throw Invalid("HeadOffice__TimeZoneId");
        }
        return new DemoEmailOptions(
            new CandidatePortalOptions(uri.AbsoluteUri.TrimEnd('/'),
                Read("HeadOffice__Address", "1 Example Street, London"),
                Read("Portal__CoordinatorContact", "recruitment@example.com")),
            new TokenOptions(signingKey),
            new EmailOptions(address, name, EmailProvider.Smtp),
            new SmtpOptions(smtpHost, port),
            new HeadOfficeOptions(timezone));
    }

    private static SeedException Invalid(string key) =>
        new($"Demo email setting '{key}' is missing or invalid.");

    /// <summary>Describes the configuration without exposing credentials or deployment values.</summary>
    public override string ToString() => "Demo email configuration (values redacted)";
}
`````

## src/EventBooking.SeedData/DemoInvitationSeeder.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.SeedData/DemoInvitationSeeder.cs","encoding":"utf8","sha256":"d81e64452c351f55ec927323b799654d446717fc6e0b0a3618a224878e857fcf","parts":1,"part":1} -->

`````csharp
using System.Globalization;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Slots;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Issues demo invitations after the database seed, preserving existing candidate journeys.</summary>
/// <param name="database">Reads invitation history without changing it directly.</param>
/// <param name="candidates">Resolves the demo recipients by their seeded email address.</param>
/// <param name="slots">Finds already-imported windows without restoring their capacity.</param>
/// <param name="importSlots">Imports missing demo windows under Coordinator authorization.</param>
/// <param name="trigger">Creates initial invitations and sends only after committing.</param>
/// <param name="retry">Retries outstanding messages under the existing claim and token rules.</param>
/// <param name="clock">Determines future head-office dates and invitation usability.</param>
public sealed class DemoInvitationSeeder(
    EventBookingDbContext database,
    ICandidateRepository candidates,
    IConfirmedSlotRepository slots,
    ImportConfirmedSlotsHandler importSlots,
    TriggerInviteHandler trigger,
    RetryEmailHandler retry,
    IClock clock)
{
    /// <summary>Gets or sets a progress sink; messages omit raw tokens, URLs and email bodies.</summary>
    public TextWriter Progress { get; set; } = TextWriter.Null;

    /// <summary>Imports demo availability and sends missing or recoverable demo invitations.</summary>
    /// <param name="cancellationToken">Cancels persistence and provider calls.</param>
    /// <returns>The count of messages successfully sent or retried during this run.</returns>
    /// <exception cref="SeedException">Dates, candidate state, issuance or delivery prevent completion.</exception>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await EnsureSlotsAsync(cancellationToken);
        var recipients = DemoSeedSpec.Candidates()
            .Where(c => c.Journey == DemoCandidateJourney.Unbooked)
            .GroupBy(c => c.EmployeeGroupCode)
            .Select(g => g.First()).ToList();
        if (recipients.Count != 5)
            throw new SeedException("Demo invitations require one Unbooked example per Employee Group.");
        var sent = 0;
        foreach (var spec in recipients)
        {
            var candidate = await candidates.GetByEmailAsync(spec.Email, cancellationToken)
                ?? throw new SeedException($"Seed candidate is missing: {spec.Email}.");
            if (candidate.Status is CandidateStatus.Booked)
            {
                Report($"Preserved existing journey: {spec.Email}.");
                continue;
            }
            if (await database.Invites.AnyAsync(i => i.CandidateId == candidate.Id, cancellationToken))
            {
                var pending = await database.Invites.AsNoTracking().SingleOrDefaultAsync(
                    i => i.CandidateId == candidate.Id && i.Status == InviteStatus.Pending
                        && i.RecoveryOfBookingId == null, cancellationToken);
                if (pending is null || !pending.IsUsableAt(clock.UtcNow))
                {
                    Report($"Preserved invitation history: {spec.Email}; use Coordinator actions or an explicit reseed.");
                    continue;
                }
                // Scope to the current Invite: an unresolved delivery for an older,
                // superseded Invite must not hide a successful Coordinator replacement.
                // Resolved attempts no longer describe the effective delivery outcome.
                var previous = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.CandidateId == candidate.Id && e.InviteId == pending.Id
                        && e.TemplateName == EmailTemplate.CandidateInvite && e.Status != EmailStatus.Resolved)
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (previous is null)
                    throw new SeedException($"Pending demo invitation has no matching delivery: {spec.Email}.");
                if (previous.Status is not EmailStatus.Failed and not EmailStatus.Pending)
                {
                    Report($"Invitation already delivered: {spec.Email}.");
                    continue;
                }
                // The retry handler selects candidate-wide outstanding work. Do not
                // accidentally retry another invitation/template from a mutated demo.
                var retryTarget = await database.EmailLogs.AsNoTracking()
                    .Where(e => e.CandidateId == candidate.Id
                        && (e.Status == EmailStatus.Failed || e.Status == EmailStatus.Pending))
                    .OrderByDescending(e => e.SentAt).ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (retryTarget?.Id != previous.Id)
                    throw new SeedException($"Another outstanding delivery needs Coordinator review: {spec.Email}.");
                var retried = await retry.HandleAsync(
                    new RetryEmailCommand(DemoSeedSpec.CoordinatorUserId(), candidate.Id), cancellationToken);
                if (retried.IsFailure)
                    throw new SeedException($"Demo email retry for {spec.Email} failed: {retried.Error}.");
                if (retried.Value.DeliveryStatus != EmailStatus.Sent.ToString())
                    throw DeliveryFailed(spec.Email);
            }
            else
            {
                if (candidate.Status is not CandidateStatus.NotYetInvited
                    and not CandidateStatus.AwaitingAvailability)
                    throw new SeedException($"Demo candidate has unexpected invitation state: {spec.Email}.");
                var issued = await trigger.HandleAsync(
                    new TriggerInviteCommand(DemoSeedSpec.CoordinatorUserId(), candidate.Id), cancellationToken);
                if (issued.IsFailure)
                    throw new SeedException($"Demo invitation for {spec.Email} failed: {issued.Error}.");
                if (!issued.Value.Invited)
                    throw new SeedException($"Three future slots with capacity are required for {spec.Email}.");
                if (!issued.Value.EmailSent)
                    throw DeliveryFailed(spec.Email);
            }
            sent++;
            Report($"Invitation delivered: {spec.Email}.");
        }
        return sent;
    }

    private async Task EnsureSlotsAsync(CancellationToken cancellationToken)
    {
        var dates = new[] { 3, 6, 9 }.Select(offset => DemoSeedSpec.AnchorDate().AddDays(offset)).ToArray();
        if (dates.Any(date => date <= clock.TodayAtHeadOffice))
            throw new SeedException("Demo invitation dates are stale. Use --reanchor for a fresh dataset; "
                + "use --reseed --reanchor only when deliberately resetting the demo.");
        var existing = (await slots.ListAllAsync(cancellationToken))
            .Select(slot => (slot.Window.Date, slot.Window.StartTime)).ToHashSet();
        var missing = dates.Where(date => !existing.Contains((date, new TimeOnly(11, 0)))).ToList();
        if (missing.Count == 0) return;
        var csv = "date,startTime,DAT,MED,UNI\n" + string.Join("\n", missing.Select(date =>
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ",11:00,20,20,20"));
        var imported = await importSlots.HandleAsync(
            new ImportConfirmedSlotsCommand(DemoSeedSpec.CoordinatorUserId(), csv), cancellationToken);
        if (imported.IsFailure)
            throw new SeedException($"Demo invitation slot import failed: {imported.Error}.");
        if (!imported.Value.Accepted)
            throw new SeedException("Demo invitation slot import rejected: "
                + string.Join("; ", imported.Value.Errors.Select(error => error.Message)));
        Report($"Invitation demo slots imported: {imported.Value.ImportedCount}.");
    }

    private static SeedException DeliveryFailed(string recipient) => new(
        $"Demo invitation delivery failed for {recipient}. Check Mailpit SMTP and rerun; "
        + "the committed invitation will be retried.");

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");
}
`````
