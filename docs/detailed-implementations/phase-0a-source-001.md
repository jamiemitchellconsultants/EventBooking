# 00a — Port source 1 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## .dockerignore — 1/1

<!-- port-file: {"path":".dockerignore","encoding":"utf8","sha256":"f19667b3d0a233c6f5617ebc61735398729bc49c894c563220b357da1a8b2e94","parts":1,"part":1} -->

`````text
**/bin
**/obj
.git
.codex
**/.env
**/.env.*
**/node_modules
`````

## deploy/home-lab/keycloak/eventbooking-realm.json — 1/1

<!-- port-file: {"path":"deploy/home-lab/keycloak/eventbooking-realm.json","encoding":"utf8","sha256":"f35226536d5b35bdb507b634687a03a84f0024f91f9241fe74ec5398abe54c03","parts":1,"part":1} -->

`````text
{
  "realm": "eventbooking",
  "enabled": true,
  "attributes": {
    "userProfileEnabled": "true"
  },
  "components": {
    "org.keycloak.userprofile.UserProfileProvider": [
      {
        "name": "declarative-user-profile",
        "providerId": "declarative-user-profile",
        "subComponents": {},
        "config": {
          "config-pieces-count": [
            "1"
          ],
          "config-piece-0": [
            "{\"attributes\":[{\"name\":\"username\",\"displayName\":\"${username}\",\"validations\":{\"length\":{\"min\":3,\"max\":255},\"username-prohibited-characters\":{}}},{\"name\":\"email\",\"displayName\":\"${email}\",\"validations\":{\"email\":{},\"length\":{\"max\":255}}},{\"name\":\"firstName\",\"displayName\":\"${firstName}\",\"permissions\":{\"view\":[\"user\",\"admin\"],\"edit\":[\"user\",\"admin\"]},\"validations\":{\"length\":{\"max\":255},\"person-name-prohibited-characters\":{}}},{\"name\":\"lastName\",\"displayName\":\"${lastName}\",\"permissions\":{\"view\":[\"user\",\"admin\"],\"edit\":[\"user\",\"admin\"]},\"validations\":{\"length\":{\"max\":255},\"person-name-prohibited-characters\":{}}},{\"name\":\"staffId\",\"displayName\":\"Staff number\",\"required\":{\"roles\":[\"user\",\"admin\"]},\"permissions\":{\"view\":[\"user\",\"admin\"],\"edit\":[\"admin\"]},\"validations\":{\"pattern\":{\"pattern\":\"^[UuNn][0-9]{6}$\"}}}],\"groups\":[]}"
          ]
        }
      }
    ]
  },
  "clients": [
    {
      "clientId": "eventbooking-web",
      "publicClient": true,
      "standardFlowEnabled": true,
      "directAccessGrantsEnabled": false,
      "redirectUris": [
        "https://EVENTBOOKING_HOSTNAME/authentication/login-callback"
      ],
      "webOrigins": [
        "https://EVENTBOOKING_HOSTNAME"
      ],
      "attributes": {
        "pkce.code.challenge.method": "S256",
        "post.logout.redirect.uris": "https://EVENTBOOKING_HOSTNAME/authentication/logout-callback"
      },
      "protocolMappers": [
        {
          "name": "oid",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-property-mapper",
          "consentRequired": false,
          "config": {
            "user.attribute": "id",
            "claim.name": "oid",
            "jsonType.label": "String",
            "id.token.claim": "true",
            "access.token.claim": "true"
          }
        },
        {
          "name": "audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": {
            "included.client.audience": "eventbooking-web",
            "id.token.claim": "false",
            "access.token.claim": "true"
          }
        },
        {
          "name": "staff_id",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-attribute-mapper",
          "consentRequired": false,
          "config": {
            "user.attribute": "staffId",
            "claim.name": "staff_id",
            "jsonType.label": "String",
            "id.token.claim": "true",
            "access.token.claim": "true"
          }
        },
        {
          "name": "roles",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-realm-role-mapper",
          "consentRequired": false,
          "config": {
            "multivalued": "true",
            "claim.name": "roles",
            "jsonType.label": "String",
            "id.token.claim": "true",
            "access.token.claim": "true"
          }
        },
        {
          "name": "name",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-full-name-mapper",
          "consentRequired": false,
          "config": {
            "id.token.claim": "true",
            "access.token.claim": "true",
            "userinfo.token.claim": "true"
          }
        }
      ]
    }
  ],
  "roles": {
    "realm": [
      {
        "name": "Admin",
        "description": "EventBooking Admin",
        "composite": false,
        "clientRole": false
      },
      {
        "name": "Coordinator",
        "description": "EventBooking Coordinator",
        "composite": false,
        "clientRole": false
      },
      {
        "name": "Manager",
        "description": "EventBooking Manager",
        "composite": false,
        "clientRole": false
      },
      {
        "name": "AppointmentStaff",
        "description": "EventBooking AppointmentStaff",
        "composite": false,
        "clientRole": false
      }
    ]
  }
}
`````

## deploy/home-lab/README.md — 1/1

<!-- port-file: {"path":"deploy/home-lab/README.md","encoding":"utf8","sha256":"f33b22d5c9fff3c4382447806f47044ec23507ca8cee7521e713a738e63c1d46","parts":1,"part":1} -->

`````markdown
# Home-lab identity configuration

EventBooking.SeedData supports idempotent Keycloak demo convergence. Supply Keycloak__BaseUrl, Keycloak__AdminUsername, Keycloak__AdminPassword and Keycloak__DemoPassword through the deployment environment. The realm owns roles; EventBooking owns appointment-type scope. Secrets must not be committed.

The complete container installation is introduced by Phase 6.

The API serves its discovery schema at /openapi/v1.json and its interactive
Swagger UI at /swagger. Both describe the deployed REST host.
`````

## deploy/keycloak/realm-export.json — 1/1

<!-- port-file: {"path":"deploy/keycloak/realm-export.json","encoding":"utf8","sha256":"2205276fde76c0c26d1757ddcd41a77d0996a49aeb96bc22b1779887ab6f8838","parts":1,"part":1} -->

`````text
{
  "realm": "eventbooking",
  "enabled": true,
  "sslRequired": "none",
  "attributes": {
    "userProfileEnabled": "true"
  },
  "components": {
    "org.keycloak.userprofile.UserProfileProvider": [
      {
        "name": "declarative-user-profile",
        "providerId": "declarative-user-profile",
        "subComponents": {},
        "config": {
          "config-pieces-count": [
            "1"
          ],
          "config-piece-0": [
            "{\"attributes\":[{\"name\":\"username\",\"displayName\":\"${username}\",\"validations\":{\"length\":{\"min\":3,\"max\":255},\"username-prohibited-characters\":{}}},{\"name\":\"email\",\"displayName\":\"${email}\",\"validations\":{\"email\":{},\"length\":{\"max\":255}}},{\"name\":\"firstName\",\"displayName\":\"${firstName}\",\"permissions\":{\"view\":[\"user\",\"admin\"],\"edit\":[\"user\",\"admin\"]},\"validations\":{\"length\":{\"max\":255},\"person-name-prohibited-characters\":{}}},{\"name\":\"lastName\",\"displayName\":\"${lastName}\",\"permissions\":{\"view\":[\"user\",\"admin\"],\"edit\":[\"user\",\"admin\"]},\"validations\":{\"length\":{\"max\":255},\"person-name-prohibited-characters\":{}}},{\"name\":\"staffId\",\"displayName\":\"Staff number\",\"required\":{\"roles\":[\"user\",\"admin\"]},\"permissions\":{\"view\":[\"user\",\"admin\"],\"edit\":[\"admin\"]},\"validations\":{\"pattern\":{\"pattern\":\"^[UuNn][0-9]{6}$\"}}}],\"groups\":[]}"
          ]
        }
      }
    ]
  },
  "clients": [
    {
      "clientId": "eventbooking-web",
      "publicClient": true,
      "standardFlowEnabled": true,
      "directAccessGrantsEnabled": true,
      "redirectUris": [
        "http://localhost:5002/authentication/login-callback"
      ],
      "webOrigins": [
        "http://localhost:5002"
      ],
      "attributes": {
        "pkce.code.challenge.method": "S256",
        "post.logout.redirect.uris": "http://localhost:5002/authentication/logout-callback"
      },
      "protocolMappers": [
        {
          "name": "oid",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-property-mapper",
          "consentRequired": false,
          "config": {
            "user.attribute": "id",
            "claim.name": "oid",
            "jsonType.label": "String",
            "id.token.claim": "true",
            "access.token.claim": "true"
          }
        },
        {
          "name": "audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": {
            "included.client.audience": "eventbooking-web",
            "id.token.claim": "false",
            "access.token.claim": "true"
          }
        },
        {
          "name": "staff_id",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-attribute-mapper",
          "consentRequired": false,
          "config": {
            "user.attribute": "staffId",
            "claim.name": "staff_id",
            "jsonType.label": "String",
            "id.token.claim": "true",
            "access.token.claim": "true"
          }
        },
        {
          "name": "roles",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-realm-role-mapper",
          "consentRequired": false,
          "config": {
            "multivalued": "true",
            "claim.name": "roles",
            "jsonType.label": "String",
            "id.token.claim": "true",
            "access.token.claim": "true"
          }
        },
        {
          "name": "name",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-full-name-mapper",
          "consentRequired": false,
          "config": {
            "id.token.claim": "true",
            "access.token.claim": "true",
            "userinfo.token.claim": "true"
          }
        }
      ]
    }
  ],
  "users": [
    {
      "id": "17e8cd60-b849-470f-a7d1-44ff39993688",
      "username": "admin.user",
      "firstName": "Ade",
      "lastName": "Admin",
      "email": "admin.user@mail.com",
      "attributes": {
        "staffId": [
          "U000001"
        ]
      },
      "enabled": true,
      "credentials": [
        {
          "type": "password",
          "value": "password",
          "temporary": false
        }
      ],
      "realmRoles": [
        "Admin"
      ]
    },
    {
      "id": "9fd390b8-eaff-4da1-accb-8970a3c8a8d1",
      "username": "manager.dat",
      "firstName": "Dana",
      "lastName": "Datson",
      "email": "manager.dat@mail.com",
      "attributes": {
        "staffId": [
          "U000002"
        ]
      },
      "enabled": true,
      "credentials": [
        {
          "type": "password",
          "value": "password",
          "temporary": false
        }
      ],
      "realmRoles": [
        "Manager"
      ]
    },
    {
      "id": "b5cedaaf-5bba-4b05-99af-cb0dbe5fc4d0",
      "username": "manager.med",
      "firstName": "Meddy",
      "lastName": "Medson",
      "email": "manager.med@mail.com",
      "attributes": {
        "staffId": [
          "U000003"
        ]
      },
      "enabled": true,
      "credentials": [
        {
          "type": "password",
          "value": "password",
          "temporary": false
        }
      ],
      "realmRoles": [
        "Manager"
      ]
    },
    {
      "id": "f9f8f3fc-d8e8-4723-ade9-fc5aaebdf104",
      "username": "manager.uni",
      "firstName": "Una",
      "lastName": "Unison",
      "email": "manager.uni@mail.com",
      "attributes": {
        "staffId": [
          "U000004"
        ]
      },
      "enabled": true,
      "credentials": [
        {
          "type": "password",
          "value": "password",
          "temporary": false
        }
      ],
      "realmRoles": [
        "Manager"
      ]
    },
    {
      "id": "0939229d-aafe-4acb-870e-a25473bc1cea",
      "username": "coordinator.user",
      "firstName": "Cory",
      "lastName": "Coordinator",
      "email": "coordinator.user@mail.com",
      "attributes": {
        "staffId": [
          "U000005"
        ]
      },
      "enabled": true,
      "credentials": [
        {
          "type": "password",
          "value": "password",
          "temporary": false
        }
      ],
      "realmRoles": [
        "Coordinator"
      ]
    },
    {
      "id": "dc5f9a90-7f54-46d1-8603-d4c317f47226",
      "username": "appointment.staff",
      "firstName": "Avery",
      "lastName": "Appointments",
      "email": "appointment.staff@mail.com",
      "attributes": {
        "staffId": [
          "U000006"
        ]
      },
      "enabled": true,
      "credentials": [
        {
          "type": "password",
          "value": "password",
          "temporary": false
        }
      ],
      "realmRoles": [
        "AppointmentStaff"
      ]
    }
  ],
  "roles": {
    "realm": [
      {
        "name": "Admin",
        "description": "EventBooking Admin",
        "composite": false,
        "clientRole": false
      },
      {
        "name": "Coordinator",
        "description": "EventBooking Coordinator",
        "composite": false,
        "clientRole": false
      },
      {
        "name": "Manager",
        "description": "EventBooking Manager",
        "composite": false,
        "clientRole": false
      },
      {
        "name": "AppointmentStaff",
        "description": "EventBooking AppointmentStaff",
        "composite": false,
        "clientRole": false
      }
    ]
  }
}
`````

## Directory.Build.props — 1/1

<!-- port-file: {"path":"Directory.Build.props","encoding":"utf8","sha256":"9d1b61c7322c5c50753433592ad77c5d9732272f6e1cb1ae16804710dffd28b4","parts":1,"part":1} -->

`````text
<Project>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile Condition="'$(MSBuildProjectName)' == 'EventBooking.Domain' Or '$(MSBuildProjectName)' == 'EventBooking.Application'">true</GenerateDocumentationFile>
  </PropertyGroup>

</Project>
`````

## Directory.Packages.props — 1/1

<!-- port-file: {"path":"Directory.Packages.props","encoding":"utf8","sha256":"0fddcbdb929a48c2cb6c8754fb5e2efb54d8fd45e1181dfe5c85559d6ee4b61b","parts":1,"part":1} -->

`````text
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MailKit" Version="4.17.0" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.11" />
    <PackageVersion Include="Swashbuckle.AspNetCore.SwaggerUI" Version="10.2.3" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.11" />
    <PackageVersion Include="bunit" Version="2.9.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.11" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly.Authentication" Version="10.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.4" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.4">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageVersion>
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Http" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.11" />
    <PackageVersion Include="Markdig" Version="0.41.3" />
    <PackageVersion Include="ModelContextProtocol.AspNetCore" Version="2.2.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.10" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.10" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.14.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
</Project>
`````

## docs/demo-runbook.md — 1/1

<!-- port-file: {"path":"docs/demo-runbook.md","encoding":"utf8","sha256":"77804b3834be42122e4dd7d75448b198836655fae805c4994f118a748b1852c0","parts":1,"part":1} -->

`````markdown
# Port baseline demo identity

The demo-seed.json dataset pre-mirrors the Keycloak roles claim and application-owned appointment-type scope. The complete generalised demonstration is introduced with the Phase 6 dataset and Phase 7 runbook.
`````

## docs/user-guides/admin-guide.md — 1/1

<!-- port-file: {"path":"docs/user-guides/admin-guide.md","encoding":"utf8","sha256":"7d4926e9e58ad9bd7f01a5dd660f5a20dc28a9290eba884c01f9807aedd0ec05","parts":1,"part":1} -->

`````markdown
# Admin guide

[← All user guides](README.md)

As an Admin, you prepare EventBooking for other staff. You set the Appointment Type scope that
scoped roles need, configure invitation timing, import Confirmed Slots agreed outside the normal
Manager negotiation, cancel a window that cannot run, and search the operational audit trail. Admin
access is exclusive: it cannot be combined with another role, and it never exposes candidate,
invitation, booking, readiness, or recovery data.

## What you do and do not control

You do **not** assign roles. Admin, Coordinator, Manager, and Appointment staff are granted centrally
in the company identity provider, and EventBooking only records what a person's sign-in token says.
Nothing on any EventBooking screen can add or remove a role, and EventBooking never writes a role
back to the provider.

You **do** own the Appointment Type scope. A Manager or Appointment staff role means nothing until
you give that profile one of the three types, because the identity provider has no concept of
appointment types. That makes scope assignment the one access task that belongs to you.

## Your workflow

1. Confirm the colleague has been given a EventBooking role in the identity provider, and has signed
   in once — a profile only appears on **Staff access** after a first sign-in.
2. On **Staff access**, set the Appointment Type for every Manager and Appointment staff profile
   showing **Awaiting appointment-type assignment**.
3. On **System settings**, confirm invitation expiry and the invite re-issue limit before
   Coordinators start sending invitations.
4. If a full slot has already been agreed outside EventBooking, import it on **Confirmed slots**.
5. Hand candidate and invitation work to a Coordinator; hand negotiated capacity to the Managers.
6. Use **Audit trail** when someone asks who changed a slot, a proposal, or an access profile.

## Home page

After signing in, check the access summary at the top of the home page. It should show **Admin** and
no Appointment Type. The page provides four workspace links plus Help:

- **System settings** (`/settings`)
- **Staff access** (`/staff-access`)
- **Confirmed slots** (`/confirmed-slots`)
- **Audit trail** (`/audit`)
- **Help** (`/help`) — every role's guide, including this one

![Admin home page with the workspace links](screenshots/admin-home.png)

The same links also appear as a navigation bar at the top of every Admin page, so you can switch
workspaces without returning to the home page first.

If candidate, dashboard, slot-proposal, or appointment links are absent, EventBooking is enforcing
the Admin data boundary correctly.

## Staff access screen

Use `/staff-access` to see who holds which role and to set the one Appointment Type that scoped
roles work within. The table lists Staff number, Roles, Appointment type, and an Action column.

The Staff number column shows the person's name with their staff number in brackets — the staff
number is the identifier to quote when you act on a profile. Roles are read-only chips: they came
from the identity provider and this screen cannot change them.

![Staff access screen listing profiles with read-only role chips and appointment types, with the Edit appointment type editor open below the table](screenshots/staff-access.png)

### Assign or change an Appointment Type

1. Find the row by name or staff number.
2. Look at the Appointment type column:
   - A named type means the scope is already set.
   - **Awaiting appointment-type assignment** means the profile holds Manager or Appointment staff
     but cannot yet use its workspace.
   - An em dash means the profile is Admin or Coordinator, which are never scoped.
3. Select **Assign** (first time) or **Edit** (a change). The editor opens below the table showing
   the staff number and read-only role chips.
4. Choose Drug & Alcohol Testing, Medical Check-up, or Uniform Fitting in **Appointment type**.
5. Select **Save appointment type** and wait for **Appointment type saved.**

If the profile you saved becomes the Manager for a type that already had one, the confirmation also
reports that the former Manager's scope was cleared. Each Appointment Type has only one current
Manager.

### Clear a scope

Open the editor for a profile that already has a type and select **Clear scope**. The role stays
assigned — only the type is removed — and the profile returns to **Awaiting appointment-type
assignment** until you set a new one. Use this when a Manager moves teams and a replacement has not
been named yet.

### When a profile is missing

The empty state reads **No one has signed in with a EventBooking role yet.** A profile you expect to
see is missing for one of two reasons, and neither is repairable from this screen:

- The person has no EventBooking role in the identity provider. Raise it through the corporate
  access process, not here.
- They have the role but have never signed in. Ask them to sign in once, then reload this screen.

You cannot pre-provision scope for somebody who has never signed in.

### Conflicting changes

If another Admin changed the same profile first, your save is rejected and the screen reloads with
the current state. Review the reloaded roles and scope, then reapply your change if it is still
needed. Every successful access change is audited and is searchable on the Audit trail screen.

### Access dependencies to establish

- Scope one Manager profile for each of the three Appointment Types before relying on in-system slot
  negotiation.
- Scope Appointment staff before the appointment date so they can open the Appointments workspace.
- Never treat Admin as "all access": Admin intentionally receives less personal data than a
  Coordinator, and cannot see candidates, invitations, bookings, or candidate audit history.

## System settings screen

Use `/settings` to review the fixed Appointment Types and configure invitation timing.

![System settings screen showing fixed appointment types and invite timing fields](screenshots/system-settings.png)

1. Review **Fixed appointment types**. The three types are fixed by the system; only the Manager
   assignment changes. The Manager identifier column shows the current Manager's name and staff
   number, or **Unassigned** when no Manager holds that type. Change an assignment by scoping a
   Manager profile on Staff access, not here.
2. Set **Invite expiry window (days)** to the number of days a new invitation remains valid.
3. Set **Max auto-retry count** to the number of times an unanswered invitation is automatically
   re-issued before the candidate is marked No response - needs follow-up for a Coordinator to
   re-invite manually.
4. Select **Save changes**.
5. Wait for **Saved. These changes apply to invites created from now on.** before leaving.

Changes do not reach back and alter invitations already sent.

## Confirmed slots screen

Use `/confirmed-slots` for a complete window agreed outside EventBooking, and to cancel a confirmed
window. Coordinators see the same screen.

![Confirmed slots screen with CSV import control](screenshots/confirmed-slots-import.png)

### Import already agreed slots

A direct import skips Manager acceptance and becomes bookable immediately.

1. Prepare a UTF-8 CSV whose exact header is:

   ```text
   date,startTime,DAT,MED,UNI
   ```

2. Add one four-hour window per row in `yyyy-MM-dd,HH:mm` format, with a positive total headcount
   for Drug & Alcohol Testing (`DAT`), Medical Check-up (`MED`), and Uniform Fitting (`UNI`). Use
   future dates for bookable capacity; past slots are retained as history and are never offered.
3. In **Import already agreed slots**, choose the CSV file.
4. Wait for the imported-count confirmation.

The import is all-or-nothing. If the screen says **Nothing was imported**, use every line-numbered
message to correct the file, then upload the whole file again. Imported slots do not require an
additional save or Manager approval.

### Cancel a confirmed slot

The **Cancel a confirmed slot** card lists every confirmed window with its date, four-hour window,
remaining-over-total capacity for each type, and the number of active Bookings. Cancel only when the
whole window cannot run.

![Cancel a confirmed slot card listing each window's date, capacity by type, active bookings, and a Cancel slot button](screenshots/confirmed-slots-cancel.png)

1. Warn the Coordinator and the delivery teams first.
2. Select **Cancel slot** on the row.
3. If the slot holds active Bookings, the request is refused and the message asks you to confirm.
   Read it, then select **Confirm cancel** only when you intend to proceed.
4. Tell the Coordinator to monitor the affected candidates and their replacement invitations.

Cancellation voids the Bookings on that slot, releases their capacity, and triggers the candidate
rebooking workflow. A Manager can cancel their own confirmed windows from the Slot proposals screen,
and a Coordinator can cancel from this screen, so agree who is acting before anyone clicks.

## Audit trail screen

Use `/audit` to answer "who changed this, and when". As an Admin you see the operational record:
slot proposals, confirmed slots, and staff access profiles. Candidate, invitation, booking, and
appointment entries are outside the Admin data boundary and are not returned to you — a Coordinator
searches those.

1. Set **From** and **To** to bound the period. Both are optional.
2. Choose an **Actor** to narrow by who caused the change: Staff, CandidateToken, or System.
3. Choose an **Action** to narrow to one recorded change, such as SlotConfirmed, SlotCancelled,
   CapacityAdjusted, SlotImported, StaffAccessChanged, or StaffRolesSynced.
4. Enter an **Identifier** to match one audited entity id or actor id exactly.
5. Select **Search**. Results are newest first, showing When, What, Who, and Details.
6. Select **Load more** to page further back. **Nothing matches these filters** means the search ran
   and found nothing, not that it failed.

Role changes made in the identity provider appear here as StaffRolesSynced entries, recorded the
next time that person signs in. That is the only trace EventBooking has of a role change, because
the change itself happened elsewhere.

## Troubleshooting

- **A colleague is not on Staff access** — they hold no EventBooking role in the identity provider,
  or they have never signed in. Neither is fixable from this screen.
- **A Manager cannot open Slot proposals** — their profile shows Awaiting appointment-type
  assignment. Assign the type.
- **Roles look wrong and there is no way to edit them** — correct. Raise the role change through the
  corporate identity process; it appears here after their next sign-in.
- **Scope save conflicts** — another Admin changed the profile first. The screen reloads; review and
  reapply.
- **CSV is rejected** — check the exact header, `yyyy-MM-dd` date, `HH:mm` start time, positive
  capacities, and every line-numbered error. No rows have been imported.
- **Cancel slot asks a second time** — active Bookings are affected. Coordinate first; the second
  click performs the cancellation.
- **Audit search returns no candidate entries** — expected. The Admin boundary excludes candidate
  data; ask a Coordinator.
- **Candidate page is unavailable** — expected. A Coordinator must complete candidate work.
`````

## docs/user-guides/appointment-staff-guide.md — 1/1

<!-- port-file: {"path":"docs/user-guides/appointment-staff-guide.md","encoding":"utf8","sha256":"1a50212b8a833807f4c0caef6232a8521d89a6d701f5c7c3489c2cd8a72c0810","parts":1,"part":1} -->

`````markdown
# Appointment staff guide

[← All user guides](README.md)

As Appointment staff, you deliver one scoped Appointment Type. The Appointments workspace contains
only operational status: who is expected, who checked in, and whether the appointment was completed
or missed. EventBooking stores no clinical findings, notes, measurements, or results in this flow.

## Before the appointment day

- Sign in and check that the home-page access summary names the correct Appointment Type. Your role
  comes from the company identity provider; the Appointment Type is set by an Admin inside
  EventBooking. If the type is missing or wrong, ask an Admin before the day.
- Open **Appointments** and confirm the expected Confirmed Slot appears in the selector.
- If the slot or a candidate is missing, ask a Coordinator to check the Booking. Do not create a
  substitute record.
- If you will be working away from a screen, use **Download roster** to take the list with you.

The dependencies are deliberate: Managers or a direct import create the Confirmed Slot, a
Coordinator issues an invitation, and the candidate must book before a Booking Appointment appears
for you.

## Appointments screen

Use `/appointments` for the full day-of workflow.

1. In **Slot**, select the date and four-hour window you are delivering. Only current and upcoming
   windows for your Appointment Type are listed.
2. Confirm the page heading names your Appointment Type.
3. Review the summary chips: Expected, Checked in, Completed, and No-show.
4. Find the candidate row by name and email.
5. Use only the action that matches what has happened at your station.

Each row shows the current Status, Check-in recorded time, Outcome recorded time, and available
Actions. The counts update when an action succeeds. The candidate list scrolls in place, so the
summary chips and slot selector stay visible while you work down a long list.

![Appointments screen for a slot with summary chips, the Download roster button, and Expected candidates](screenshots/appointments-list.png)

### Normal outcome path

1. Select **Check in** when the candidate arrives at your station. Check-in opens only on the
   Confirmed Slot's head-office date; until then the row says so and the button is unavailable.
2. Select **Complete** when your appointment is finished.

The normal path is Expected → Checked in → Completed. You cannot jump directly from Expected to
Completed.

![Appointments screen after a check-in, showing the CheckedIn status and updated summary chips](screenshots/appointments-after-checkin.png)

### Record a no-show

1. Wait until the four-hour window has ended. Until then the row says No-show is available after the
   window ends.
2. On an Expected row, select **No-show**.
3. Review the candidate name in the confirmation prompt.
4. Select **Confirm**.

No-show remains an outstanding requirement. It may make **Arrange missed appointments** available
to the Coordinator, who can send the candidate a recovery invitation containing only recoverable
missed types.

### Correct a mistake

Corrections move one permitted step back and require confirmation:

- Checked in → select **Correct to expected**.
- Completed → select **Correct to checked in**.
- No-show → select **Correct to expected**.

Read the candidate name in the prompt, then select **Confirm** or **Cancel**. The prompt appears as
an overlay above the candidate list so it draws attention before you act.

![Correction confirmation overlay for a Checked in candidate, with Confirm and Cancel buttons](screenshots/appointments-correction-overlay.png)

A correction changes readiness immediately. A No-show correction can be blocked if a later recovery
invitation or Booking already relies on that outcome. Ask the Coordinator to cancel the pending
recovery first; do not record an unrelated status to work around the conflict.

If another staff member changes the same appointment first, EventBooking refreshes the row and asks
you to review it before trying again.

## Download the roster

**Download roster**, beside the summary chips, saves the selected slot's list as a CSV file so you
can work from paper or a tablet without the app.

1. Select the slot you are delivering.
2. Select **Download roster**. The button reads **Preparing…** while the file is built.
3. The file is saved as `roster-<appointment type>-<date>-<start time>.csv`, for example
   `roster-uniform-fitting-2026-09-18-1300.csv`.

The file holds one row per candidate in the same order as the screen, with these columns:

```text
Candidate Name,Candidate Email,Appointment Type,Status,Checked In At,Outcome At
```

Times are head-office local time. The file is a snapshot taken when you pressed the button — it is
not updated afterwards, and writing on it changes nothing in EventBooking. Every check-in, outcome,
and correction must still be recorded on the screen. Treat the file as personal data: it carries
candidate names and email addresses, so store and dispose of it accordingly.

## How your outcome affects the wider workflow

- Each Appointment Type progresses independently. Completing your row never completes another
  team's row.
- A candidate becomes ready only when every current required type has a Completed outcome across
  the original and any non-cancelled recovery Bookings.
- Expected, Checked in, and No-show all remain outstanding.
- A recovery Booking concludes once every appointment in that recovery is Completed or No-show.
  Another No-show can then be recovered again without deleting the earlier attempts.
- Cancelled Bookings and slots cannot be acted on and may disappear from the active list. A
  Coordinator or Admin can cancel a whole window, which removes it from your selector.
- Every action you record is written to the audit trail with your identity and the time. A
  Coordinator can see it on the candidate's History.

## Troubleshooting

- **No current or upcoming slots** — no active slot in scope contains appointment work. Check your
  Appointment Type with an Admin and the Booking with a Coordinator.
- **Check in is unavailable** — the selected slot's head-office date is not today.
- **No-show is unavailable** — the four-hour window has not ended.
- **Nobody to see in this window** — no candidate in that slot requires your Appointment Type.
- **Candidate is absent** — they may not have booked, may have cancelled, or may be on another slot.
  Ask a Coordinator to inspect the candidate journey.
- **Only one type is visible** — expected. Your profile exposes only its scoped type.
- **The row refreshed after an error** — another staff member changed it first. Review the current
  status before choosing another action.
- **No-show correction is blocked** — a later recovery depends on it. Ask a Coordinator to cancel
  the pending recovery before correcting the original outcome.
- **Roster download failed** — reselect the slot and try again; if it persists the slot may have
  been cancelled while the page was open.
`````

## docs/user-guides/candidate-guide.md — 1/1

<!-- port-file: {"path":"docs/user-guides/candidate-guide.md","encoding":"utf8","sha256":"4dc4f217816639ceaa63be533ef5241787e676de34f6420a83c97d4f15d51705","parts":1,"part":1} -->

`````markdown
# Candidate guide

[← All user guides](README.md)

EventBooking helps you choose one four-hour visit for the appointments required by your Employee
Group. You do not need an account and never sign in. Use only the personal links sent to your email.

## Book your first appointment visit

You can start after the recruitment team sends your invitation.

1. Open the **Choose a time** link in the invitation email.
2. Check that your name and the listed appointments are correct.
3. Review the three available dates and four-hour windows.
4. Select one option.

   ![Choose a time page listing the candidate's name, required appointments, and three date options](screenshots/choose-a-time.png)

5. Select **Confirm this time** once and wait for **Booking confirmed**.
6. Check the confirmed date, start and end time, and head-office address.
7. Open or save **Use your booking management link**. A copy is also sent in the confirmation email.

   ![Booking confirmed page showing the confirmed date, head-office address, and management link](screenshots/booking-confirmed.png)

The invitation link is personal and single-use. EventBooking reserves space only for the
appointments listed on that invitation. If one option loses capacity while you are choosing, the
page refreshes the available choices; select one of the remaining options or contact the recruitment
team if none remain.

Your Booking is valid as soon as **Booking confirmed** appears. If the page says email delivery
could not be confirmed, save the on-screen management link and contact the recruitment team only if
you need help; do not repeat the booking.

## Attend your appointments

Come to head office during the confirmed four-hour window. Follow the team's arrival instructions.
Each appointment is checked in and completed separately, but every appointment listed in your
initial confirmation takes place within the same shared window.

Completing one appointment does not automatically complete the others. The recruitment team can
see when all required appointments are complete.

## Manage or cancel a Booking

Open the management link for the Booking you want to change. The **Manage your booking** screen
shows its date and four-hour window.

![Manage your booking screen showing the appointment date/window and Cancel booking / Cancel and choose a new time actions](screenshots/manage-booking.png)

### Cancel without choosing another time

1. Select **Cancel booking**.
2. Wait for **Booking cancelled**.
3. Contact the recruitment team if you still need an appointment; no replacement invitation is
   requested by this action.

### Cancel and request fresh choices for your original visit

1. Select **Cancel and choose a new time**.
2. Wait for **Booking cancelled**.
3. If suitable capacity exists, EventBooking sends a new invitation with fresh options.
4. If no suitable times exist or email delivery cannot be confirmed, the page tells you the
   recruitment team will follow up.

Cancelling the original Booking cancels that appointment journey, including any pending or active
missed-appointment recovery.

### Cancel a missed-appointment recovery

Use the management link from the recovery confirmation and select **Cancel booking**. Cancelling a
recovery returns only that recovery's reserved capacity. It leaves the original visit, already
Completed appointments, and earlier attempts intact. EventBooking does not automatically create a
replacement recovery invitation; the recruitment team can arrange another if the appointment is
still required.

### If the recruitment team cancels for you

The recruitment team can also cancel your booking on your behalf — for example when a whole visiting
window has to be called off. You may then receive one of two things without having asked for it:

- A fresh invitation email with new times. Choose one exactly as you did the first time; your old
  management link no longer works.
- Nothing immediately, because no suitable times were open. The team will contact you.

Either way, the visit you had booked no longer stands. If you receive a new invitation you were not
expecting, treat it as the current one and contact the recruitment team if it looks wrong.

## Book missed appointments

If an appointment is recorded as No-show and still needs to be completed, the recruitment team may
send a recovery invitation.

1. Open the email link. The page heading says **Choose a new time for your missed appointment**
   (one missed appointment) or **Choose a new time for your missed appointments** (two or three).
2. Check the list: it contains only the still-required missed appointments being recovered. It does
   not repeat appointments you already completed.
3. Choose one of the three shared four-hour windows.
4. Select **Confirm this time** and wait for **Booking confirmed**.
5. Keep the new management link; it manages this recovery Booking.

Attend only the appointment types named in that recovery confirmation. If a replacement appointment
is also missed, the recruitment team can arrange another recovery without erasing the earlier
history.

## When a link does not work

For privacy, malformed, expired, used, cancelled, superseded, or otherwise stale invitation links
all show the same message: **This booking link is no longer valid.**

- If the page says the link has expired or is no longer valid, contact the recruitment team for a
  fresh invitation. The contact address is shown on the page.
- If **Nothing fits right now** appears, contact the recruitment team; suitable capacity is not
  currently available.
- If the page reports a temporary loading or confirmation error, select **Try again** once. If it
  persists, contact the recruitment team and do not forward your personal link.
- If your name or appointment list is wrong, stop before confirming and contact the recruitment
  team.
- If you lose a management link, check the confirmation email. If it is not available, ask the
  recruitment team for help.

## Reading this guide again

This guide is also published inside EventBooking. Open `/help` on the same site as your booking
link — no sign-in needed — and it is the page you see.
`````

## docs/user-guides/coordinator-guide.md — 1/1

<!-- port-file: {"path":"docs/user-guides/coordinator-guide.md","encoding":"utf8","sha256":"5de0b40e07963664a03e5695ebfaded31d6c0e4b9e8b321e9f587131eee6aae0","parts":1,"part":1} -->

`````markdown
# Coordinator guide

[← All user guides](README.md)

As a Coordinator, you own the candidate journey from initial record through invitation, booking,
readiness, and missed-appointment recovery. EventBooking derives every required Appointment Type
from the candidate's Employee Group; you never add or remove requirements individually. You can also
cancel a candidate's booking on their behalf, cancel a whole confirmed window, and search the full
audit trail.

## Your workflow

1. Check **Dashboards** for candidates waiting for availability or follow-up.
2. Ensure at least three suitable future Confirmed Slots exist. Ask Managers to negotiate them or
   import already-agreed capacity on **Confirmed slots**.
3. Add candidates on **Candidates**, manually or with a CSV containing one Employee Group per row.
4. Review the derived Appointment Type chips, then select **Invite now**.
5. Monitor invitation and email status until the candidate books.
6. After delivery staff record outcomes, review each candidate's readiness.
7. If the latest unsatisfied attempt is No-show, select **Arrange missed appointments** and monitor
   the recovery invitation until the missed types are completed.
8. When a plan changes, cancel the candidate's booking from their row — with or without sending
   fresh options — rather than asking them to find their emailed link.

## Home page

After signing in, the home-page access summary should include **Coordinator**. It provides:

- **Candidates** (`/candidates`)
- **Dashboards** (`/dashboards`)
- **Confirmed slots** (`/confirmed-slots`)
- **Audit trail** (`/audit`)
- **Help** (`/help`) — every role's guide, including the candidate guide

The same links also appear as a navigation bar at the top of every page, so you can switch
workspaces without returning to the home page first.

If your profile also contains Manager or Appointment staff, the home page also shows their
workspaces. Those actions remain scoped to the one Appointment Type shown in the access summary.
Roles themselves are assigned in the company identity provider, not by a EventBooking Admin; an
Admin only sets the Appointment Type that scoped roles work within.

## Candidates screen

Use `/candidates` for candidate records, invitations, delivery status, readiness, bookings, history,
and recovery. There is no outbound onboarding integration and no readiness export: recovery is
arranged only through this screen (or the matching Coordinator API endpoints), never through MCP.

![Candidates screen with the add-candidate row and existing candidate list](screenshots/candidates-list.png)

### Find the correct work queue

- Use **Status** to filter by Not yet invited, Awaiting availability, Invited (pending response),
  Booked, No response - needs follow-up, or Cancelled.
- Enter a name or email in **Search by name or email**, then select **Search**.
- Clear the search and choose **Any status** to return to the full list.

### Add one candidate

The first table row is the add form.

1. Enter **Full name** and **Email address**.
2. Choose the required **Employee Group**.
3. Check the read-only Appointment Type chips previewed beneath the group. These are derived from
   the approved mapping and cannot be edited.
4. Select **Save candidate**.
5. Confirm the saved row shows the expected group name/code and derived types.

![Add-candidate row filled in with a name, email, and Cabin Crew employee group, showing the derived DAT/MED/UNI chips](screenshots/candidate-add-form.png)

The mappings are:

| Employee Group | CSV code | Derived Appointment Types |
|---|---|---|
| Cabin Crew | `CABIN_CREW` | DAT, MED, UNI |
| Pilots | `PILOTS` | DAT, UNI |
| Ground Operations Agent | `GROUND_OPERATIONS_AGENT` | MED |
| Engineering | `ENGINEERING` | MED |
| Ground Transport Services | `GROUND_TRANSPORT_SERVICES` | DAT, MED, UNI |

### Import candidates

For a batch, prepare a CSV with the exact header:

```text
name,email,employee_group
```

The `employee_group` field contains one code from the table above. Surrounding whitespace and letter
case are accepted, but EventBooking stores and displays the canonical uppercase code.

1. Select **Upload CSV** and choose a `.csv` file no larger than 1 MiB.
2. Wait while every row is validated.
3. If accepted, the list reloads with the new candidates.
4. If **Nothing was imported** appears, correct every line-numbered error and upload the whole file
   again.

The import is all-or-nothing. Blank or unknown group codes, duplicate emails in the file or system,
invalid names or emails, and malformed rows prevent every row from being added. The old
`appointment_types` column is not accepted.

### Edit a candidate

1. Select **Edit** on the candidate's row.
2. Change the name, email, or Employee Group.
3. Review the newly derived Appointment Type chips.
4. Read any warning, then select **Save**; select **Cancel** to discard the edit.

The effect of an Employee Group change depends on the booking journey:

- If the new group derives the same Appointment Types, EventBooking preserves pending invitations,
  active bookings, capacity, and appointment history.
- If it changes the required types before booking, a pending invitation is invalidated and the
  candidate returns to Not yet invited. Select **Invite now** after saving to send correct options.
- If it changes the required types during an active original Booking, the save is blocked. Cancel
  the booking from the **Booking** column first — **Cancel & rebook** keeps the candidate moving —
  then change the group. The entered edit values remain on screen while the save is refused.

Cabin Crew and Ground Transport Services are set-equivalent to each other. Ground Operations Agent
and Engineering are also set-equivalent.

### Read the candidate row

- **Required types** contains read-only code chips derived from the Employee Group. A legacy record
  with no group instead shows **Employee Group required**: edit it and choose a group before
  inviting.
- **Status** shows the invitation/booking stage.
- **Readiness** gives a compact result. Expand it for the outstanding Appointment Types.
- **Delivery** shows the latest candidate email. For a retryable Failed or Pending message, select
  **Resend**.
- **Booking** loads the active Bookings on demand and holds the cancellation actions.
- **History** expands the recorded changes for that candidate.
- **Actions** contains Edit, Invite now, and Delete. Delete confirms on a second click.

### Interpret readiness

| Screen text | Meaning | Next action |
|---|---|---|
| All required appointments completed | Every current required type has a Completed outcome | No appointment action is needed |
| Not ready — no active booking | The candidate has not confirmed an active original Booking | Send or follow up an invitation |
| Not ready, with outstanding type chips | One or more required types are Expected, Checked in, or No-show | Wait for delivery, or arrange recovery if the screen offers it |
| Readiness unavailable — contact support | Stored group, Booking, and appointment snapshots do not agree | Stop; do not re-invite or edit around the warning |

Only a Completed outcome satisfies a type. Expected, Checked in, and No-show remain outstanding. An
outstanding type is labelled **(recoverable)** when its latest attempt was a No-show.

### Send an initial invitation

1. Confirm the Employee Group and derived types are correct.
2. Select **Invite now**.
3. Wait for the row to refresh to Invited and review **Delivery**.

![Candidate row showing status Invited (pending response) and the delivery timestamp](screenshots/candidate-invited.png)

EventBooking selects exactly three future Confirmed Slots with remaining capacity for every derived
type and snapshots those requirements into the invitation. If fewer than three suitable options
exist, no invitation is created and the candidate moves to Awaiting availability. Create more
capacity, then try again.

### Cancel a candidate's booking

The **Booking** column shows a **Bookings** button. Select it to load and expand the list; the
button then reads **No active booking**, **1 active booking**, or a count. Each entry shows the date and four-hour window, marked **(recovery)** when it is a
recovery Booking rather than the original.

![Expanded Booking column for a booked candidate, with Cancel & rebook armed and showing Confirm cancel](screenshots/candidate-booking.png)

Two actions are offered, each confirming on its own second click:

1. **Cancel booking** releases the places and stops there. No replacement options are sent, so the
   candidate needs a fresh invitation from you if they still need an appointment.
2. **Cancel & rebook** releases the places and emails the candidate a fresh invitation with new
   options. It appears on the original Booking only — a recovery Booking is never auto-replaced.

The result appears under the list as one of:

- **Booking cancelled.**
- **Booking cancelled; replacement invite sent.**
- **Booking cancelled; replacement invite could not be delivered.** — the cancellation still
  happened. Check the email address and follow up manually.

Cancelling the original Booking cancels that appointment journey, including any pending or active
recovery. Cancelling a recovery Booking leaves the original journey and completed work intact.

### Arrange missed appointments

**Arrange missed appointments** appears only when at least one current outstanding type is
recoverable. A type becomes recoverable when its latest non-cancelled attempt is No-show and no
Completed attempt already satisfies it.

1. Expand readiness and review the recoverable type names.
2. Select **Arrange missed appointments**.
3. Confirm the resulting message lists exactly the missed types and reports the email outcome.
4. While the recovery invitation is Pending, use **Cancel recovery** only if it must be withdrawn;
   confirm the cancellation when prompted.
5. Monitor the candidate's readiness after they book and the replacement appointments are delivered.

Recovery never repeats an already Completed type. It requires three future slots with capacity for
all missed types in that recovery. Expected or Checked-in work cannot be recovered; staff must first
record the correct outcome. If the candidate misses the replacement too, a new recovery can be
arranged after that recovery Booking concludes.

### Read a candidate's history

Every candidate row carries a **History** disclosure. Expanding it loads the recorded changes to
the candidate record itself (such as an Employee Group being assigned or changed) and to that
candidate's invitations, Bookings, and Booking Appointments, newest first, with When, What, Who, and
Details. It is loaded on demand, so opening a long candidate list costs nothing until you ask for a
history.

![Expanded History on a candidate row, listing BookingCreated, InviteSent, InviteCreated, and EmployeeGroupAssigned entries with When, What, Who, and Details](screenshots/candidate-history.png)

Use History for a single candidate's story. Use the Audit trail screen when you need to search
across candidates, dates, or actions.

## Dashboards screen

Use `/dashboards` as the start-of-day and follow-up view.

![Dashboards screen showing the Slots tab with capacity by type and active booking counts](screenshots/dashboards.png)

- **Awaiting availability** lists candidates who could not receive three suitable options, their
  required types, and how long they have waited. Arrange more capacity before returning to their
  Candidate row and inviting again.
- **No response** lists candidates whose invitation follow-up window ended. Select **Re-invite now**
  after confirming their email and continued need.
- **Slots** shows each Confirmed Slot, capacity remaining/total by type, and active Booking count.
- Failed and Pending email totals appear beside the tabs. Resolve them from the Candidate row.
- Every tab carries the same **History** disclosure per row — per candidate on the first two tabs,
  per slot on Slots.

The dashboard tabs support mouse, touch, and keyboard. With focus on the tab list, use Left/Right,
Home, or End to change views.

## Confirmed slots screen

Use `/confirmed-slots` when all three teams have already agreed a complete window outside
EventBooking, and to cancel a window that cannot run. Admins see the same screen.

![Confirmed slots screen with CSV import control](screenshots/confirmed-slots-import.png)

### Import already agreed slots

The exact header is:

```text
date,startTime,DAT,MED,UNI
```

Choose the file in **Import already agreed slots**. Each row uses a `yyyy-MM-dd` date, `HH:mm` start
time, and positive capacity for all three types. Use future dates for bookable capacity; past slots
are never offered. A valid future import becomes bookable immediately. If any row is invalid,
nothing is imported and the screen lists every line error.

### Cancel a confirmed slot

The **Cancel a confirmed slot** card lists every confirmed window with its capacity by type and the
number of active Bookings. Select **Cancel slot**; if the window holds active Bookings the request
is refused with a warning, and the button becomes **Confirm cancel**.

![Cancel a confirmed slot card listing each window's date, capacity by type, active bookings, and a Cancel slot button](screenshots/confirmed-slots-cancel.png)

Cancelling voids every Booking on that window, releases the capacity, and starts the candidate
rebooking workflow — so tell the delivery teams first, and expect the affected candidates to need
watching afterwards. A Manager can cancel the same window from their own screen, so agree who is
acting before anyone clicks.

## Audit trail screen

Use `/audit` to search across the whole record. As a Coordinator you see both candidate entries
(candidates, invitations, bookings, and booking appointments) and operational entries (slot
proposals, confirmed slots, and staff access profiles).

![Audit trail screen with From, To, Actor, Action, Identifier, and Entity filters above newest-first results](screenshots/audit-trail.png)

1. Set **From** and **To** to bound the period. Both are optional.
2. Choose an **Actor**: Staff, CandidateToken (a candidate acting through their emailed link), or
   System.
3. Choose an **Action** to narrow to one recorded change, such as InviteSent, BookingCreated,
   BookingCancelled, AppointmentMarkedNoShow, RecoveryInviteCreated, or SlotCancelled.
4. Enter an **Identifier** to match one audited entity id or actor id exactly.
5. Narrow further with **Entity** when you want only one kind of record.
6. Select **Search**, then **Load more** to page further back. Results are newest first.

**Nothing matches these filters** means the search ran and found nothing, not that it failed.

## Troubleshooting

- **Invite cannot be created** — fewer than three suitable slots exist, the candidate data is
  inconsistent, or another action changed state. Refresh, fix the stated dependency, and retry.
- **Invitation link is invalid** — it may be expired, used, cancelled, or superseded by an Employee
  Group change. Verify the row and send a fresh invitation where appropriate.
- **Employee Group change is blocked** — it would alter the requirement set of an active original
  Booking. Cancel the booking from the Booking column first, then change the group.
- **Invite now is refused for a legacy record** — the row shows Employee Group required. Edit it and
  assign a group.
- **Arrange missed appointments is absent** — no current type has a recoverable No-show, or a
  recovery invitation/Booking already exists.
- **Recovery cannot start** — create three suitable future slots, correct any concurrently changed
  appointment state, or finish/cancel the existing recovery first.
- **Cancel & rebook is missing on a booking** — that Booking is a recovery. Cancel it, then arrange
  a new recovery once the appointment state allows it.
- **Email failed after an invitation was created** — the invitation remains valid. Correct the email
  address if necessary and use **Resend**; do not create duplicate candidate records.
- **Readiness unavailable** — contact support. Do not try to repair snapshot mismatches through
  repeated edits or invitations.
`````
