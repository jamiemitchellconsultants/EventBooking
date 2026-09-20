# 00a — Port source 2 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## docs/user-guides/manager-guide.md — 1/1

<!-- port-file: {"path":"docs/user-guides/manager-guide.md","encoding":"utf8","sha256":"54b39f2c36e75c66404fd8003b3751e79e4592e601098d457a477e7c89196a0e","parts":1,"part":1} -->

`````markdown
# Manager guide

[← All user guides](README.md)

As a Manager, you own capacity for one Appointment Type: Drug & Alcohol Testing, Medical Check-up,
or Uniform Fitting. You negotiate shared four-hour windows with the other two Managers, maintain
your type's capacity, and can use the scoped Appointments workspace to deliver the service.

## Your workflow

1. On **Slot proposals**, review open proposals and create any new windows your team can support.
2. Accept each workable proposal with your team's headcount. All three Managers must accept before
   the window becomes bookable.
3. On confirmed slots, keep your total capacity accurate and never reduce it below active demand.
4. Before and during the window, use **Appointments** to monitor Expected candidates and record your
   type's outcomes if you are part of delivery.
5. Tell Coordinators promptly about shortages or cancellations because their invitations depend on
   the capacity you control.

## Home page

After signing in, check that the access summary shows **Manager** and the correct Appointment Type.
The home page provides:

- **Slot proposals** (`/slots`)
- **Appointments** (`/appointments`)
- **Help** (`/help`) — every role's guide, including the candidate guide

![Manager home page showing Slot proposals and Appointments links](screenshots/manager-home.png)

The same links also appear as a navigation bar at the top of every page, so you can move between
Slot proposals and Appointments without returning to the home page first.

A combined Coordinator profile also shows Candidates, Dashboards, Confirmed slots, and Audit trail.
Manager data remains scoped: you never see another Manager's headcount or another Appointment Type's
appointment rows.

### If your Appointment Type is missing or wrong

Your **role** comes from the company identity provider. Your **Appointment Type** is set inside
EventBooking by an Admin. If the access summary shows Manager with no type — or the wrong type —
Slot proposals and Appointments will not work for you. Ask an Admin to set the scope on their Staff
access screen. Do not act in the wrong workspace in the meantime.

## Slot proposals screen

Use `/slots` to propose windows, accept or withdraw, adjust capacity, and cancel Confirmed Slots.

![Slot proposals screen showing open proposals and each type's acceptance chips](screenshots/slot-proposals.png)

### Propose a new slot

1. In **Propose a new slot**, choose a future **Date**.
2. Choose the **Start time**. Every window lasts exactly four hours.
3. Select **Submit proposal**.
4. Find the new row under **Open proposals** and, when ready, enter your own headcount and accept it.

Submitting a proposal does not itself provide your team's acceptance or headcount.

### Review and accept an open proposal

Each row shows Date, Window, Accepted by, My headcount, and Actions.

1. Check the date and four-hour window.
2. Review the **Accepted by** chips to see which Appointment Types have agreed. Their headcount
   values remain private.
3. Enter a positive number in **My headcount**.
4. Select **Accept**.

After your acceptance, the action reads **Update acceptance**. You can replace your own headcount
while the proposal remains open. When the third Manager accepts, EventBooking creates the Confirmed
Slot immediately; Coordinators can then invite candidates against it.

### Withdraw before confirmation

- Select **Withdraw acceptance** to remove your own acceptance while the proposal remains open.
- If you created the proposal, select **Withdraw proposal** to withdraw the entire proposal.

Neither action is available after confirmation. A confirmed window must be managed as a Confirmed
Slot.

### Adjust confirmed capacity

Under **Confirmed slots (this type)**, each row shows your total and remaining capacity only.

1. Enter the replacement total in **My total**.
2. Confirm **Remaining** still makes operational sense.
3. Select **Adjust headcount**.

The new total must be positive and cannot be lower than the number of active Bookings requiring your
type. If a candidate books at the same time, the save may be rejected to prevent overbooking.
Reload the board and enter a total that covers the updated demand.

Employee Groups determine which types each candidate needs. You do not need to know the group to
manage capacity: EventBooking reserves only the capacity required by each invitation or recovery
snapshot.

### Cancel a Confirmed Slot

Cancel only when the whole four-hour window cannot run.

1. Warn Coordinator and delivery colleagues first.
2. Select **Cancel slot**.
3. If active Bookings are affected, read the warning and select **Confirm cancel** only when you
   intend to proceed.
4. Tell the Coordinator to monitor candidates and replacement invitation delivery.

Cancellation voids Bookings on the slot, releases their capacity, and triggers the appropriate
candidate rebooking workflow. Cancelling a recovery slot preserves the original journey and
already Completed appointments.

Admins and Coordinators can cancel the same window from their own Confirmed slots screen, so agree
who is acting before anyone clicks.

## Appointments screen

Managers have the same delivery controls as Appointment staff within their own Appointment Type,
including **Download roster** for a printable working list. Use `/appointments` when you are
recording day-of work. See the [Appointment staff guide](appointment-staff-guide.md) for the
complete screen workflow, screenshots, and correction rules.

## Troubleshooting

- **The wrong Appointment Type is shown** — stop and ask an Admin to correct your scope on Staff
  access. Do not act in the wrong workspace.
- **No Appointment Type is shown at all** — an Admin has not scoped your profile yet. The role alone
  is not enough.
- **Accept or update failed** — the proposal was changed, withdrawn, or confirmed while you were
  viewing it. Reload the board.
- **Another Manager's headcount is missing** — expected. You can see who accepted, not their number.
- **Capacity change was rejected** — the total is invalid, below active demand, or demand changed
  concurrently. Reload and recalculate.
- **Candidates are awaiting availability** — propose more future windows or accept open proposals.
  An invitation needs three suitable Confirmed Slots across every required type.
- **Cancel requires a second confirmation** — active Bookings are affected. Coordinate first; the
  second click performs the cancellation.
- **A window disappeared from your board** — an Admin or Coordinator may have cancelled it. Ask the
  Coordinator to check the audit trail; Managers do not have that screen.
`````

## docs/user-guides/README.md — 1/1

<!-- port-file: {"path":"docs/user-guides/README.md","encoding":"utf8","sha256":"c07e8293d279b7923743e61aa7d79c559785832bfbb03af07d6cca42cb842b5d","parts":1,"part":1} -->

`````markdown
# EventBooking user guides

EventBooking coordinates candidate appointments across five user types. Start with the guide for
your role, then use the workflow below to understand which earlier actions your work depends on.

Every guide here is also published inside the application at `/help`. Staff see all five guides on
that page; a signed-out visitor sees only the candidate guide.

| Guide | Who it is for | Main workspace |
|---|---|---|
| [Admin](admin-guide.md) | System administrators who set appointment-type scope, settings, and direct slot imports | System settings, Staff access, Confirmed slots, Audit trail |
| [Coordinator](coordinator-guide.md) | Recruitment coordinators who manage candidates, invitations, bookings, readiness, and recovery | Candidates, Dashboards, Confirmed slots, Audit trail |
| [Manager](manager-guide.md) | Appointment-type managers who negotiate slots, manage capacity, and may deliver appointments | Slot proposals, Appointments |
| [Appointment staff](appointment-staff-guide.md) | Delivery staff who check candidates in and record outcomes | Appointments |
| [Candidate](candidate-guide.md) | Invited candidates who choose and manage appointment times | Email links for booking and booking management |

## End-to-end workflow and dependencies

| Stage | Owner | What must already exist | Screen and result |
|---|---|---|---|
| 1. Assign roles | Corporate identity provider, not EventBooking | The colleague's account exists in the company directory | Roles are granted centrally; EventBooking records what the sign-in token says |
| 2. Give scoped roles their type | Admin | The colleague has signed in once, so a profile exists | **Staff access**: set the one Appointment Type for a Manager or Appointment staff profile |
| 3. Prepare settings | Admin | Admin access | **System settings**: set invitation expiry and the invite re-issue limit |
| 4. Create capacity | Manager, Admin, or Coordinator | One Manager exists for each Appointment Type, or a slot has already been agreed outside EventBooking | **Slot proposals**: all three Managers accept; or **Confirmed slots**: import the agreed window and all three capacities |
| 5. Add candidates | Coordinator | An Employee Group is known for each candidate | **Candidates**: add manually or import `name,email,employee_group`; EventBooking derives the required Appointment Types |
| 6. Send an invitation | Coordinator | At least three future Confirmed Slots have capacity for every required Appointment Type | **Candidates**: select **Invite now**; the candidate receives three options |
| 7. Book | Candidate | A valid pending invitation | **Choose a time**: select an option and confirm; capacity is reserved and one Booking Appointment is created for each required type |
| 8. Deliver appointments | Manager or Appointment staff | The candidate has an active Booking on the selected Confirmed Slot | **Appointments**: check in, complete, or record No-show for the caller's scoped type |
| 9. Check readiness | Coordinator | Appointment outcomes have been recorded | **Candidates**: expand readiness; the candidate is ready only when every current required type has a Completed outcome |
| 10. Recover a missed appointment | Coordinator, then Candidate | The latest unsatisfied attempt for at least one current required type is No-show | **Candidates**: choose **Arrange missed appointments**; the candidate books a new shared slot containing only the missed types |
| 11. Account for a change | Admin or Coordinator | The change has been recorded | **Audit trail**, or the **History** control on a candidate or slot row |

An upstream delay remains visible at the next stage. For example, a Coordinator cannot issue an
invitation until suitable slot capacity exists, and a Candidate cannot appear in the Appointments
workspace until they confirm a time.

## Staff sign-in and navigation

Staff sign in from the EventBooking home page with their company account.

![Staff sign-in screen](screenshots/keycloak-sign-in.png)

The home page greets you by name and shows the roles and Appointment Type scope in the signed-in
access summary, followed by only the workspaces that profile permits. Once signed in, the same links
also appear as a persistent navigation bar at the top of every page, so switching workspaces never
requires returning to the home page first. **Help** is the last link in both places.

| Access profile | Workspace links |
|---|---|
| Admin | System settings, Staff access, Confirmed slots, Audit trail |
| Coordinator | Candidates, Dashboards, Confirmed slots, Audit trail |
| Manager | Slot proposals, Appointments |
| Appointment staff | Appointments |

Admin is exclusive and cannot be combined with another role. Coordinator, Manager, and Appointment
staff can be combined in one profile. A combined profile receives the union of the links above, but
Manager and Appointment staff still share one Appointment Type scope.

If the home page says that no role is assigned, your account has no EventBooking role in the company
identity provider; ask whoever administers application access there, not a EventBooking Admin. If a
page or action is missing, first check the access summary: the absence is normally an access rule,
not a page fault.

Candidates do not sign in. They use personal, single-use links sent by email and see no staff
navigation. They can open `/help` without signing in to read the candidate guide.

## Who assigns what

Roles and the appointment-type scope come from two different places, and this is the most common
source of confusion:

- **Roles** (Admin, Coordinator, Manager, Appointment staff) are assigned centrally in the company
  identity provider. EventBooking records what the sign-in token says and can never change it.
- **Appointment Type scope** (the one type a Manager or Appointment staff profile works on) is
  assigned inside EventBooking by an Admin, on the Staff access screen.

A scoped role therefore needs two separate actions by two different people before it works, and a
profile only appears on the Staff access screen after that person has signed in at least once.

## Everyday vocabulary

- An **Employee Group** is the candidate's employment category. It determines the complete set of
  Appointment Types the candidate requires; nobody selects those requirements individually.
- A **Slot Proposal** is a suggested four-hour window. Once all three Managers accept it, it becomes
  a **Confirmed Slot** that candidates can book.
- An **Invite** offers a candidate exactly three suitable Confirmed Slots. Confirming one creates a
  **Booking**.
- A **Booking Appointment** is one required Appointment Type inside a Booking. Each type is checked
  in and completed separately.
- **Readiness** means every current required Appointment Type has a Completed Booking Appointment in
  the candidate's non-cancelled journey.
- A **recovery booking** contains only missed required Appointment Types. It preserves completed
  work and every earlier attempt.
- The **audit trail** is the record of every change: what changed, who changed it, and when. It is
  searchable on its own page and inline through the **History** control on candidate and slot rows.

## About the screenshots

The screenshots in these guides were captured from the local demo stack against seeded data, and
are representative rather than exhaustive. They were recaptured on 2026-09-16 and 2026-09-17
against the current screens.

The Appointments check-in and correction screenshots need a slot dated today that already holds
bookings: check-in only opens on a Confirmed Slot's own date, and a candidate can only book a slot
dated after today. To retake them, reseed with `--reseed --reanchor` (see the
[demo runbook](../demo-runbook.md)) on the day of capture. That dates the seeded 13:00 journey slot
today, with Expected Uniform Fitting appointments for `appointment.staff` to check in.

Where a screenshot and the surrounding text disagree, the text describes the current screen.
`````

## EventBooking.sln — 1/1

<!-- port-file: {"path":"EventBooking.sln","encoding":"utf8","sha256":"9816f2981652a99e731fcc1e9b369d710262979030b05448c0e95998cb25ce6d","parts":1,"part":1} -->

`````text

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{827E0CD3-B72D-47B6-A68D-7590B98EB39B}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Domain", "src\EventBooking.Domain\EventBooking.Domain.csproj", "{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Application", "src\EventBooking.Application\EventBooking.Application.csproj", "{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Infrastructure", "src\EventBooking.Infrastructure\EventBooking.Infrastructure.csproj", "{70477CD1-BBC3-415A-93BA-B8F77528D22B}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Api", "src\EventBooking.Api\EventBooking.Api.csproj", "{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Web", "src\EventBooking.Web\EventBooking.Web.csproj", "{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}"
EndProject
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "tests", "tests", "{0AB3BF05-4346-4AA6-1389-037BE0695223}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Domain.Tests", "tests\EventBooking.Domain.Tests\EventBooking.Domain.Tests.csproj", "{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Application.Tests", "tests\EventBooking.Application.Tests\EventBooking.Application.Tests.csproj", "{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Infrastructure.Tests", "tests\EventBooking.Infrastructure.Tests\EventBooking.Infrastructure.Tests.csproj", "{30E28067-392E-4D12-AF5A-2C7E2DA093F8}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Api.Tests", "tests\EventBooking.Api.Tests\EventBooking.Api.Tests.csproj", "{216F1881-BDBF-4381-A3FF-948E75C60446}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Web.Tests", "tests\EventBooking.Web.Tests\EventBooking.Web.Tests.csproj", "{69443B2C-B26E-4533-BEDC-71BF9667FD0A}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Api.Auth", "src\EventBooking.Api.Auth\EventBooking.Api.Auth.csproj", "{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.SeedData", "src\EventBooking.SeedData\EventBooking.SeedData.csproj", "{40E29523-8E35-46FF-B332-325512633E46}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.SeedData.Tests", "tests\EventBooking.SeedData.Tests\EventBooking.SeedData.Tests.csproj", "{E77EE352-5701-4FBC-88FD-9CF9832483A8}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Mcp", "src\EventBooking.Mcp\EventBooking.Mcp.csproj", "{C5E52C00-B8B2-482E-83BA-A312954D435C}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "EventBooking.Mcp.Tests", "tests\EventBooking.Mcp.Tests\EventBooking.Mcp.Tests.csproj", "{96EF204A-7317-46F4-B702-68D6548C65ED}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Debug|x64.ActiveCfg = Debug|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Debug|x64.Build.0 = Debug|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Debug|x86.ActiveCfg = Debug|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Debug|x86.Build.0 = Debug|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Release|Any CPU.Build.0 = Release|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Release|x64.ActiveCfg = Release|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Release|x64.Build.0 = Release|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Release|x86.ActiveCfg = Release|Any CPU
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67}.Release|x86.Build.0 = Release|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Debug|x64.ActiveCfg = Debug|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Debug|x64.Build.0 = Debug|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Debug|x86.ActiveCfg = Debug|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Debug|x86.Build.0 = Debug|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Release|Any CPU.Build.0 = Release|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Release|x64.ActiveCfg = Release|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Release|x64.Build.0 = Release|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Release|x86.ActiveCfg = Release|Any CPU
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2}.Release|x86.Build.0 = Release|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Debug|x64.ActiveCfg = Debug|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Debug|x64.Build.0 = Debug|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Debug|x86.ActiveCfg = Debug|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Debug|x86.Build.0 = Debug|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Release|Any CPU.Build.0 = Release|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Release|x64.ActiveCfg = Release|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Release|x64.Build.0 = Release|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Release|x86.ActiveCfg = Release|Any CPU
		{70477CD1-BBC3-415A-93BA-B8F77528D22B}.Release|x86.Build.0 = Release|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Debug|x64.ActiveCfg = Debug|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Debug|x64.Build.0 = Debug|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Debug|x86.ActiveCfg = Debug|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Debug|x86.Build.0 = Debug|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Release|Any CPU.Build.0 = Release|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Release|x64.ActiveCfg = Release|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Release|x64.Build.0 = Release|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Release|x86.ActiveCfg = Release|Any CPU
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A}.Release|x86.Build.0 = Release|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Debug|x64.ActiveCfg = Debug|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Debug|x64.Build.0 = Debug|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Debug|x86.ActiveCfg = Debug|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Debug|x86.Build.0 = Debug|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Release|Any CPU.Build.0 = Release|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Release|x64.ActiveCfg = Release|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Release|x64.Build.0 = Release|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Release|x86.ActiveCfg = Release|Any CPU
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4}.Release|x86.Build.0 = Release|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Debug|x64.ActiveCfg = Debug|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Debug|x64.Build.0 = Debug|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Debug|x86.ActiveCfg = Debug|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Debug|x86.Build.0 = Debug|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Release|Any CPU.Build.0 = Release|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Release|x64.ActiveCfg = Release|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Release|x64.Build.0 = Release|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Release|x86.ActiveCfg = Release|Any CPU
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886}.Release|x86.Build.0 = Release|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Debug|x64.ActiveCfg = Debug|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Debug|x64.Build.0 = Debug|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Debug|x86.ActiveCfg = Debug|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Debug|x86.Build.0 = Debug|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Release|Any CPU.Build.0 = Release|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Release|x64.ActiveCfg = Release|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Release|x64.Build.0 = Release|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Release|x86.ActiveCfg = Release|Any CPU
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0}.Release|x86.Build.0 = Release|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Debug|x64.ActiveCfg = Debug|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Debug|x64.Build.0 = Debug|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Debug|x86.ActiveCfg = Debug|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Debug|x86.Build.0 = Debug|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Release|Any CPU.Build.0 = Release|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Release|x64.ActiveCfg = Release|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Release|x64.Build.0 = Release|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Release|x86.ActiveCfg = Release|Any CPU
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8}.Release|x86.Build.0 = Release|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Debug|x64.ActiveCfg = Debug|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Debug|x64.Build.0 = Debug|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Debug|x86.ActiveCfg = Debug|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Debug|x86.Build.0 = Debug|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Release|Any CPU.Build.0 = Release|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Release|x64.ActiveCfg = Release|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Release|x64.Build.0 = Release|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Release|x86.ActiveCfg = Release|Any CPU
		{216F1881-BDBF-4381-A3FF-948E75C60446}.Release|x86.Build.0 = Release|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Debug|x64.ActiveCfg = Debug|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Debug|x64.Build.0 = Debug|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Debug|x86.ActiveCfg = Debug|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Debug|x86.Build.0 = Debug|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Release|Any CPU.Build.0 = Release|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Release|x64.ActiveCfg = Release|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Release|x64.Build.0 = Release|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Release|x86.ActiveCfg = Release|Any CPU
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A}.Release|x86.Build.0 = Release|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Debug|x64.ActiveCfg = Debug|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Debug|x64.Build.0 = Debug|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Debug|x86.ActiveCfg = Debug|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Debug|x86.Build.0 = Debug|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Release|Any CPU.Build.0 = Release|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Release|x64.ActiveCfg = Release|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Release|x64.Build.0 = Release|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Release|x86.ActiveCfg = Release|Any CPU
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5}.Release|x86.Build.0 = Release|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Debug|x64.ActiveCfg = Debug|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Debug|x64.Build.0 = Debug|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Debug|x86.ActiveCfg = Debug|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Debug|x86.Build.0 = Debug|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Release|Any CPU.Build.0 = Release|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Release|x64.ActiveCfg = Release|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Release|x64.Build.0 = Release|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Release|x86.ActiveCfg = Release|Any CPU
		{40E29523-8E35-46FF-B332-325512633E46}.Release|x86.Build.0 = Release|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Debug|x64.ActiveCfg = Debug|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Debug|x64.Build.0 = Debug|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Debug|x86.ActiveCfg = Debug|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Debug|x86.Build.0 = Debug|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Release|Any CPU.Build.0 = Release|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Release|x64.ActiveCfg = Release|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Release|x64.Build.0 = Release|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Release|x86.ActiveCfg = Release|Any CPU
		{E77EE352-5701-4FBC-88FD-9CF9832483A8}.Release|x86.Build.0 = Release|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Debug|x64.ActiveCfg = Debug|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Debug|x64.Build.0 = Debug|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Debug|x86.ActiveCfg = Debug|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Debug|x86.Build.0 = Debug|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Release|Any CPU.Build.0 = Release|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Release|x64.ActiveCfg = Release|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Release|x64.Build.0 = Release|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Release|x86.ActiveCfg = Release|Any CPU
		{C5E52C00-B8B2-482E-83BA-A312954D435C}.Release|x86.Build.0 = Release|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Debug|x64.ActiveCfg = Debug|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Debug|x64.Build.0 = Debug|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Debug|x86.ActiveCfg = Debug|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Debug|x86.Build.0 = Debug|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Release|Any CPU.Build.0 = Release|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Release|x64.ActiveCfg = Release|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Release|x64.Build.0 = Release|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Release|x86.ActiveCfg = Release|Any CPU
		{96EF204A-7317-46F4-B702-68D6548C65ED}.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		{97A7AF8B-ABF3-43A5-A1C8-F739EE56CC67} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{35CDE664-5E41-4D28-9AC6-338F3D48DEA2} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{70477CD1-BBC3-415A-93BA-B8F77528D22B} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{B73D7953-5D0F-419F-A2C9-3B987C9CF21A} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{61DD06BE-0C44-4597-A7F2-A2C8881E4CF4} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{E5F5E489-3EE1-4C9D-896C-B7C5EE565886} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
		{B9B4735D-2E9A-440C-B76E-2492E50B4FE0} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
		{30E28067-392E-4D12-AF5A-2C7E2DA093F8} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
		{216F1881-BDBF-4381-A3FF-948E75C60446} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
		{69443B2C-B26E-4533-BEDC-71BF9667FD0A} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
		{38C40F78-D31E-46FB-AEFD-0C0CF13BF4D5} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{40E29523-8E35-46FF-B332-325512633E46} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{E77EE352-5701-4FBC-88FD-9CF9832483A8} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
		{C5E52C00-B8B2-482E-83BA-A312954D435C} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{96EF204A-7317-46F4-B702-68D6548C65ED} = {0AB3BF05-4346-4AA6-1389-037BE0695223}
	EndGlobalSection
EndGlobal
`````

## README.md — 1/1

<!-- port-file: {"path":"README.md","encoding":"utf8","sha256":"8f06e67c5b25ef754cc25ee8e184ca7d0d9ddd93b8d55bf1fa0f15e7d8894063","parts":1,"before":"ef6ecd472b436ab12dcaa2f2e9b3d45fd2acdb3d7ae42b4c858dde8f168368db","part":1} -->

`````markdown
# EventBooking

Booking system for events with multiple session types.

The REST discovery document is served at /api, the OpenAPI schema at
/openapi/v1.json and the interactive Swagger UI at /swagger. Staff endpoints
require an OIDC bearer token. The MCP host exposes /mcp with the same bearer
authentication and application authorization as REST.
`````

## src/EventBooking.Api.Auth/EventBooking.Api.Auth.csproj — 1/1

<!-- port-file: {"path":"src/EventBooking.Api.Auth/EventBooking.Api.Auth.csproj","encoding":"utf8","sha256":"ea35556399b7aece25aeaaea5b0ad2a341acce14cec3ca692ccf9a39e88b13e8","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
  </ItemGroup>

</Project>
`````

## src/EventBooking.Api.Auth/LocalAuthenticationExtensions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api.Auth/LocalAuthenticationExtensions.cs","encoding":"utf8","sha256":"a5107455d883200477b894f2a8430c377c0fc16bc3f40c442d3ef3007eb4a47e","parts":1,"part":1} -->

`````csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Auth;

public static class LocalAuthenticationExtensions
{
    /// <summary>A generic OIDC Bearer [REDACTED] against a Keycloak realm's issuer and
    /// audience — the Auth:Local configuration section (Authority, Audience). The local realm
    /// is served over plain HTTP, so HTTPS metadata is disabled; this provider is never used
    /// outside a local deployment.</summary>
    public static AuthenticationBuilder AddLocalAuthentication(
        this IServiceCollection services, IConfiguration authLocalSection) =>
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authLocalSection["Authority"];
                options.Audience = authLocalSection["Audience"];
                options.RequireHttpsMetadata = false;

                // Without this, JwtBearer remaps well-known claim names (notably "roles") to
                // legacy http://schemas.xmlsoap.org/... / ClaimTypes URIs via
                // JwtSecurityTokenHandler.DefaultInboundClaimTypeMap, so HttpContextCallerAccessor's
                // literal "roles" / "oid" / "staff_id" lookups silently find nothing.
                // Microsoft.Identity.Web (the EntraId provider) already disables this internally,
                // which is why only this local Keycloak path needs it here.
                options.MapInboundClaims = false;
            });
}
`````

## src/EventBooking.Api/appsettings.json — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/appsettings.json","encoding":"utf8","sha256":"3db7caaea817b92399958425edcfe0a1b69730070dc7586385e0a3fde6def9ec","parts":1,"part":1} -->

`````text
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:5002",
      "http://localhost:5002"
    ]
  },
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=postgres;Password=postgres"
  },
  "HeadOffice": {
    "TimeZoneId": "Europe/London",
    "Address": "Corporate HQ, 1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "replace-this-with-a-real-secret-of-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "https://localhost:5001",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## src/EventBooking.Api/appsettings.Local.json — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/appsettings.Local.json","encoding":"utf8","sha256":"b42048a31267f7418519ee1ba7a6f5e444a85f6c5d06f8298fb2d9f9e9a838fb","parts":1,"part":1} -->

`````text
{
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=eventbooking_app;Password=eventbooking_local"
  },
  "HeadOffice": {
    "TimeZoneId": "Europe/London",
    "Address": "1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "a-local-signing-key-that-is-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "http://localhost:5002",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## src/EventBooking.Api/Auth/AuthenticationExtensions.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Auth/AuthenticationExtensions.cs","encoding":"utf8","sha256":"88ceaabe458ab10e1e8696609b92a8e1b4f8d8780dad0bd5a019cd29a970ad18","parts":1,"part":1} -->

`````csharp
using EventBooking.Api.Auth;
using Microsoft.AspNetCore.Authorization;

namespace EventBooking.Api.Auth;

public static class AuthenticationExtensions
{
    public const string StaffPolicy = "staff";
    public const string AuthenticatedPolicy = "authenticated";

    public static IServiceCollection AddEventBookingAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<ICallerAccessor, HttpContextCallerAccessor>();

        services.AddLocalAuthentication(configuration.GetSection("Auth:Local"));

        services.AddScoped<IAuthorizationHandler, StaffRequirementHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(StaffPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new StaffRequirement());
            })
            .AddPolicy(AuthenticatedPolicy, policy => policy.RequireAuthenticatedUser());

        return services;
    }
}
`````

## src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs","encoding":"utf8","sha256":"1dbf86a42b8773cb5b6e7dbf5009c2baa7fbedbff674fb6fde2457a5f733720a","parts":1,"part":1} -->

`````csharp
using System.Security.Claims;
using EventBooking.Domain.Access;

namespace EventBooking.Api.Auth;

/// <summary>Reads provider and enterprise staff identifiers from authenticated HTTP claims.</summary>
public sealed class HttpContextCallerAccessor(IHttpContextAccessor accessor, ILogger<HttpContextCallerAccessor> logger) : ICallerAccessor
{
    /// <summary>The claim type a v1 Entra ID token uses.</summary>
    public const string ObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>The claim type a v2 Entra ID token uses.</summary>
    public const string ShortObjectIdClaim = "oid";

    /// <summary>The shared Keycloak and Entra ID claim containing the enterprise staff number.</summary>
    public const string StaffIdClaim = "staff_id";

    /// <summary>The shared Keycloak and Entra ID claim carrying identity-provider-assigned roles.</summary>
    public const string RolesClaim = "roles";

    /// <summary>The shared Keycloak and Entra ID claim carrying the caller's full name.</summary>
    public const string NameClaim = "name";

    /// <summary>Gets the provider identifier from the current authenticated principal.</summary>
    public Guid? StaffUserId => StaffUserIdOf(accessor.HttpContext?.User);

    /// <summary>Gets the validated enterprise staff number from the current principal.</summary>
    public StaffId? StaffId => StaffIdOf(accessor.HttpContext?.User);

    /// <summary>Gets the human-readable name from the current authenticated principal.</summary>
    public string? DisplayName => DisplayNameOf(accessor.HttpContext?.User);

    /// <summary>Gets the recognised roles the current authenticated principal's token carries.</summary>
    public IReadOnlySet<Role> Roles => RolesOf(
        accessor.HttpContext?.User,
        value => logger.LogWarning("Ignoring unknown identity-provider role {Role}.", value));

    /// <inheritdoc />
    public Guid RequireStaffUserId() =>
        StaffUserId ?? throw new InvalidOperationException("The request has no staff identity.");

    /// <inheritdoc />
    public StaffId RequireStaffId() =>
        StaffId ?? throw new InvalidOperationException("The request has no valid staff number.");

    /// <summary>Reads a provider identifier only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The provider identifier, or null when absent or malformed.</returns>
    public static Guid? StaffUserIdOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var value =
                identity.FindFirst(ShortObjectIdClaim)?.Value
                ?? identity.FindFirst(ObjectIdClaim)?.Value;

            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>Reads and validates a staff number only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The canonical staff number, or null when absent or malformed.</returns>
    public static StaffId? StaffIdOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            if (StaffId.TryParse(identity.FindFirst(StaffIdClaim)?.Value, out var staffId))
            {
                return staffId;
            }
        }

        return null;
    }

    /// <summary>Reads a human-readable name only from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <returns>The name, or null when absent, empty, or whitespace.</returns>
    public static string? DisplayNameOf(ClaimsPrincipal? principal)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var value = identity.FindFirst(NameClaim)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Reads and parses every recognised role claim value from an authenticated identity.</summary>
    /// <param name="principal">The request principal.</param>
    /// <param name="onRejected">Receives each rejected claim value for warning-level logging.</param>
    /// <returns>The parsed role set; empty when the claim is absent or unauthenticated.</returns>
    public static IReadOnlySet<Role> RolesOf(
        ClaimsPrincipal? principal,
        Action<string>? onRejected = null)
    {
        foreach (var identity in principal?.Identities ?? [])
        {
            if (!identity.IsAuthenticated)
            {
                continue;
            }

            var claims = identity.FindAll(RolesClaim).ToList();
            if (claims.Count == 0)
            {
                continue;
            }

            var roles = new HashSet<Role>();
            foreach (var claim in claims)
            {
                if (Enum.TryParse<Role>(claim.Value, ignoreCase: false, out var role) && Enum.IsDefined(role))
                {
                    roles.Add(role);
                }
                else
                {
                    onRejected?.Invoke(claim.Value);
                }
            }

            return roles;
        }

        return new HashSet<Role>();
    }
}
`````

## src/EventBooking.Api/Auth/ICallerAccessor.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Auth/ICallerAccessor.cs","encoding":"utf8","sha256":"5175036e079551bce60fc354e09ab4bd21977c18c12f27610665bacf504cecd0","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;

namespace EventBooking.Api.Auth;

/// <summary>Reads validated staff identity values from the current authenticated principal.</summary>
public interface ICallerAccessor
{
    /// <summary>The signed-in staff user's provider identifier, or null if absent or malformed.</summary>
    Guid? StaffUserId { get; }

    /// <summary>The signed-in staff user's enterprise staff number, or null if absent or malformed.</summary>
    StaffId? StaffId { get; }

    /// <summary>
    /// The signed-in staff user's human-readable name from the token name claim, or null when it
    /// is absent or blank. Presentation data only: it is never authorization-relevant, so no
    /// Require member exists for it.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>The signed-in staff user's identity-provider-assigned roles. Empty when the
    /// `roles` claim is absent; a claim value that does not name a known `Role` is dropped.</summary>
    IReadOnlySet<Role> Roles { get; }

    /// <summary>Returns the provider identifier or throws when the request has no staff identity.</summary>
    Guid RequireStaffUserId();

    /// <summary>Returns the staff number or throws when the request has no valid staff number.</summary>
    StaffId RequireStaffId();
}
`````

## src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs","encoding":"utf8","sha256":"9c1e0d3eda5998f41233de610bfa0f091b90736bb9c3a2cb3d310137a8087ccb","parts":1,"part":1} -->

`````csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace EventBooking.Api.Auth;

/// <summary>Applies the anonymous candidate-link allowance independently per client address.</summary>
public sealed class RemoteIpRateLimiterPolicy : IRateLimiterPolicy<string>
{
    /// <inheritdoc/>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    /// <inheritdoc/>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        // ForwardedHeadersMiddleware runs before the limiter, so RemoteIpAddress is already the
        // real client address when behind a proxy. Unknown addresses share one fallback bucket
        // rather than bypassing the limit.
        var client = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    }
}
`````

## src/EventBooking.Api/Auth/StaffIdentityRecorder.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Auth/StaffIdentityRecorder.cs","encoding":"utf8","sha256":"5c2a98dca261f0c948b9d5b47155419352054128e9e5182dc42a56ce225e2efa","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace EventBooking.Api.Auth;

/// <summary>
/// Records each valid authenticated provider/staff-number pair before staff authorization runs.
/// A bounded process cache avoids placing a database write in front of every request.
/// </summary>
public sealed class StaffIdentityRecorder(RequestDelegate next)
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Records a complete identity pair when needed, then continues the request pipeline.</summary>
    /// <param name="context">The current request.</param>
    /// <param name="caller">The validated caller claims.</param>
    /// <param name="identities">The durable identity mirror.</param>
    /// <param name="clock">The application's source of the current instant.</param>
    /// <param name="cache">The process-local record-suppression cache.</param>
    /// <param name="logger">Records identity-provider uniqueness conflicts for operators.</param>
    public async Task InvokeAsync(
        HttpContext context,
        ICallerAccessor caller,
        IStaffIdentityRepository identities,
        IClock clock,
        IMemoryCache cache,
        ILogger<StaffIdentityRecorder> logger)
    {
        var staffUserId = caller.StaffUserId;
        var staffId = caller.StaffId;
        if (staffUserId is not null
            && staffId is not null
            && !cache.TryGetValue(CacheKey(staffUserId.Value), out _))
        {
            try
            {
                await identities.UpsertAsync(
                    staffUserId.Value,
                    staffId,
                    caller.DisplayName,
                    clock.UtcNow,
                    context.RequestAborted);
                cache.Set(CacheKey(staffUserId.Value), true, CacheLifetime);
            }
            catch (PostgresException exception)
                when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                logger.LogWarning(
                    exception,
                    "Staff identity conflict for provider user {StaffUserId} and staff number {StaffId}",
                    staffUserId,
                    staffId.Value);
            }
        }

        await next(context);
    }

    private static string CacheKey(Guid staffUserId) => $"staff-identity:{staffUserId:D}";
}
`````

## src/EventBooking.Api/Auth/StaffRequirement.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Auth/StaffRequirement.cs","encoding":"utf8","sha256":"f3c655a30f5d5e6ffc392e64451af2e115a7dbbfead952f6114d90ce5b3a2e8f","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Domain.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace EventBooking.Api.Auth;

/// <summary>Signed in, and known to this application as a member of staff.</summary>
public sealed class StaffRequirement : IAuthorizationRequirement;

/// <summary>Requires both validated identity claims and an application-owned access profile.</summary>
public sealed class StaffRequirementHandler(
    ICallerAccessor caller,
    IStaffAccessProfileRepository profiles,
    SyncStaffAccessProfileRolesHandler sync) : AuthorizationHandler<StaffRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        StaffRequirement requirement)
    {
        var staffUserId = caller.StaffUserId;
        if (staffUserId is null || caller.StaffId is null)
        {
            return;
        }

        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;

        // Identity-provider role changes take effect on every staff request, not only when
        // /api/me is called: the stored profile is reconciled with the token's roles first,
        // so a reduced role set cannot linger for REST or MCP callers that never call /api/me.
        // The sync is a cheap no-op read when nothing changed, and refuses to leave the
        // application without its last Admin (see the IdP-sourced roles design).
        var profile = await sync.SyncAsync(staffUserId.Value, caller.Roles, cancellationToken)
            ?? await profiles.GetAsync(staffUserId.Value, cancellationToken);
        if (profile is not null && profile.IsValid())
        {
            context.Succeed(requirement);
        }
    }
}
`````

## src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs","encoding":"utf8","sha256":"3ac8df71105816ed4916c18ffb68b76d86c8196d82dfefe792886f2c77e3f507","parts":1,"part":1} -->

`````csharp
using System.Text.Json.Serialization;
using EventBooking.Application.Settings;

namespace EventBooking.Api.Contracts;

/// <summary>Application settings plus the self and update affordances.</summary>
public sealed record SettingsResourceResponse(
    /// <summary>Gets the number of days an invite stays usable.</summary>
    int InviteExpiryDays,
    /// <summary>Gets the maximum number of times an unanswered invite is automatically re-issued.</summary>
    int MaxAutoRetryCount,
    /// <summary>Gets the appointment types with their assigned managers.</summary>
    IReadOnlyList<AppointmentTypeView> AppointmentTypes,
    /// <summary>Gets the settings affordances.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Projects one settings view into its hypermedia resource.</summary>
    /// <param name="view">The application settings view to project.</param>
    /// <returns>The API resource with settings links.</returns>
    public static SettingsResourceResponse From(SettingsView view) =>
        new(view.InviteExpiryDays, view.MaxAutoRetryCount, view.AppointmentTypes,
            StaffResourceLinks.ForSettings());
}

/// <summary>One staff access profile plus its replace and clear affordances.</summary>
public sealed record StaffAccessResourceResponse(
    /// <summary>Gets the stable staff identity targeted by administration.</summary>
    Guid StaffUserId,
    /// <summary>Gets the enterprise staff number, or null until the identity signs in.</summary>
    string? StaffId,
    /// <summary>Gets the identity-provider roles mirrored on the profile.</summary>
    IReadOnlyList<string> Roles,
    /// <summary>Gets the scoped appointment-type identifier, or null when unscoped.</summary>
    Guid? AppointmentTypeId,
    /// <summary>Gets the scoped appointment-type name, or null when unscoped.</summary>
    string? AppointmentTypeName,
    /// <summary>Gets the positive concurrency version required by scope commands.</summary>
    long Version,
    /// <summary>Gets the human-readable name mirrored from the identity provider, or null when absent.</summary>
    string? DisplayName,
    /// <summary>Gets the replace and clear affordances for the profile.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>A staff-access mutation result plus the follow-up affordances.</summary>
public sealed record StaffAccessMutationResourceResponse(
    /// <summary>Gets the updated staff access profile.</summary>
    StaffAccessResourceResponse Profile,
    /// <summary>Gets the displaced manager identity, when a replacement displaced one.</summary>
    Guid? FormerManagerStaffUserId,
    /// <summary>Gets the follow-up affordances for the mutated profile.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);

/// <summary>The signed-in staff identity plus self and collection entry affordances.</summary>
public sealed record MeResourceResponse(
    /// <summary>Gets the caller's enterprise staff number, or null until recorded.</summary>
    string? StaffId,
    /// <summary>Gets the caller's current role names.</summary>
    IReadOnlyList<string> Roles,
    /// <summary>Gets the caller's scoped appointment-type identifier, or null when unscoped.</summary>
    Guid? AppointmentTypeId,
    /// <summary>Gets the caller's scoped appointment-type name, or null when unscoped.</summary>
    string? AppointmentTypeName,
    /// <summary>Gets the self and role-relevant collection entry affordances. Links are discoverability hints, not authorization.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links)
{
    /// <summary>Builds the identity resource with self plus role-relevant entry links.</summary>
    /// <param name="staffId">The caller's enterprise staff number, or null until recorded.</param>
    /// <param name="roles">The caller's current role names.</param>
    /// <param name="appointmentTypeId">The caller's scoped appointment-type identifier, or null.</param>
    /// <param name="appointmentTypeName">The caller's scoped appointment-type name, or null.</param>
    /// <returns>The API resource with identity links.</returns>
    public static MeResourceResponse From(
        string? staffId,
        IReadOnlyList<string> roles,
        Guid? appointmentTypeId,
        string? appointmentTypeName)
    {
        var links = new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/me", "GET", "getMyAccess"),
        };
        if (roles.Contains("Coordinator") || roles.Contains("Admin"))
        {
            links["candidates"] = new("/api/candidates", "GET", "listCandidates");
            links["dashboards"] = new("/api/dashboards", "GET", "getDashboards");
            links["audit"] = new("/api/audit/search", "GET", "searchAudit");
        }

        if (roles.Contains("Manager"))
        {
            links["slotBoard"] = new("/api/slots/board", "GET", "getSlotBoard");
            links["slotOperations"] = new("/api/slots/operations", "GET", "getSlotOperations");
        }

        if (roles.Contains("AppointmentStaff") || roles.Contains("Manager"))
        {
            links["appointmentSlots"] = new("/api/appointment-workspace/slots", "GET", "listAppointmentSlots");
        }

        if (roles.Contains("Admin"))
        {
            links["settings"] = new("/api/admin/settings", "GET", "getSettings");
            links["staffAccess"] = new("/api/admin/staff-access", "GET", "listStaffAccess");
        }

        return new MeResourceResponse(staffId, roles, appointmentTypeId, appointmentTypeName, links);
    }
}
`````

## src/EventBooking.Api/Contracts/ApiLink.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Api/Contracts/ApiLink.cs","encoding":"utf8","sha256":"2175c263b0074b629a7fe09a1556a47a41286e2e8b0ac6923bdb8602fe411476","parts":1,"part":1} -->

`````csharp
using System.Text.Json.Serialization;

namespace EventBooking.Api.Contracts;

/// <summary>One root-relative API affordance joined to OpenAPI by operation id.</summary>
/// <param name="Href">The root-relative URI of the linked operation.</param>
/// <param name="Method">The uppercase HTTP method of the linked operation.</param>
/// <param name="OperationId">The OpenAPI operation id of the linked operation.</param>
public sealed record ApiLink(
    /// <summary>Gets the root-relative URI of the linked operation.</summary>
    string Href,
    /// <summary>Gets the uppercase HTTP method of the linked operation.</summary>
    string Method,
    /// <summary>Gets the OpenAPI operation id of the linked operation.</summary>
    string OperationId);

/// <summary>The anonymous top-level API entry document.</summary>
/// <param name="Name">The service name displayed to API clients.</param>
/// <param name="Version">The API version displayed to API clients.</param>
/// <param name="Links">The entry-point relations keyed by stable relation name.</param>
public sealed record ApiDiscoveryResponse(
    /// <summary>Gets the service name displayed to API clients.</summary>
    string Name,
    /// <summary>Gets the API version displayed to API clients.</summary>
    string Version,
    /// <summary>Gets the entry-point relations keyed by stable relation name.</summary>
    [property: JsonPropertyName("_links")] IReadOnlyDictionary<string, ApiLink> Links);
`````
