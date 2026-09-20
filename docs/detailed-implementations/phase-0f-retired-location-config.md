# 00f — Retire the single-site configuration (Task 3d)

[← Overview](README.md) · [Ontology](../ontology.md)

This cross-layer retirement checkpoint follows Task 3c on the Phase 0 branch. It keeps the application buildable while removing one predecessor assumption. Complete changed types and exact before/after files are embedded in the numbered companion volumes.

> Use superpowers:executing-plans. This is a lettered split of master Task 3 and retains its commit message.

**Goal:** Retire the single-site configuration without breaking the surviving booking workflows.

**Architecture:** Domain invariants are enforced at request boundaries and persisted by the existing EF Core infrastructure. API, MCP, seed and web remain synchronized.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 3](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

Keep this checkpoint on the existing Phase 0 feature branch. Do not push to main or open the phase PR yet. Preserve canonical names, capacity bounds, lock ordering and attendee CSV import. Do not log personal data or relax authorization to make a test pass.

## Review focus

STOP AND CHECK: do not rename the address key, and do not substitute a global replacement for it — an address belongs to a `Location` from Task 4. Retire the suites that assert a configured address rather than editing their expected strings into something else, and keep the assertions that the confirmation email and page no longer name one. The temporary clock keeps its transitional member names; only its options type and configuration keys change here.

### Task 3d: Retire the single-site configuration

**Files:**

- Modify: src/EventBooking.Api/EventBookingConfiguration.cs
- Modify: src/EventBooking.Api/appsettings.Local.json
- Modify: src/EventBooking.Api/appsettings.json
- Modify: src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs
- Modify: src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs
- Modify: src/EventBooking.Application/Notifications/AttendeePortalOptions.cs
- Modify: src/EventBooking.Infrastructure/DependencyInjection.cs
- Create: src/EventBooking.Infrastructure/Time/ClockOptions.cs
- Modify: src/EventBooking.Infrastructure/Time/SystemClock.cs
- Modify: src/EventBooking.Infrastructure/Time/TransitionalLocationOptions.cs (delete)
- Modify: src/EventBooking.Mcp/appsettings.Local.json
- Modify: src/EventBooking.Mcp/appsettings.json
- Modify: src/EventBooking.SeedData/DemoEmailOptions.cs
- Modify: src/EventBooking.SeedData/Program.cs
- Modify: src/EventBooking.Web/Pages/Book.razor
- Modify: src/EventBooking.Web/Services/BookingClient.cs
- Modify: tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/HealthTests.cs
- Test: tests/EventBooking.Api.Tests/RetiredLocationConfigurationTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs
- Modify: tests/EventBooking.SeedData.Tests/ReanchorTests.cs
- Modify: tests/EventBooking.SeedData.Tests/ReseedTests.cs
- Modify: tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
namespace EventBooking.Infrastructure.Time;

/// <summary>
/// The one time zone every date rule uses until Task 4 gives each <c>Location</c> its own zone.
/// An identifier the host operating system recognises.
/// </summary>
/// <param name="TimeZoneId">The IANA time-zone identifier.</param>
public sealed record ClockOptions(string TimeZoneId);
```

```csharp
namespace EventBooking.Application.Notifications;

/// <summary>
/// The handful of deployment-specific strings the attendee-facing emails and pages need. Bound
/// from configuration at startup and injected as a singleton. Addresses belong to a
/// <c>Location</c>, not to the deployment.
/// </summary>
/// <param name="BaseUrl">The base url.</param>
/// <param name="CoordinatorContact">The coordinator contact.</param>
public sealed record AttendeePortalOptions(
    string BaseUrl,
    string CoordinatorContact);
```

**Context you need**

- Master Task 3 removes the predecessor’s head-office address and time-zone settings: both now belong to a `Location`.
- Spec §3 (vocabulary mapping): “HeadOffice configuration (address, time zone) → `Location` rows. Configuration keys removed.”
- Design 04 (Configuration) lists no deployment address: `Portal__BaseUrl` and `Portal__CoordinatorContact` are the only portal settings, and the predecessor’s head-office keys are gone.
- FR-11.6 (quoted): “In every email, times shall be given in the event `Location`’s zone, with its abbreviation.” A deployment-wide address cannot satisfy that; do not invent a global substitute.
- FR-6.2 requires the booking page to show “each live option’s location name, address, local date, start and end time, and zone abbreviation”, which Task 4 onward supplies from `Location`.
- The one remaining single-zone clock is temporary: master Task 3 permits it until Task 4 introduces `EventWindow` resolution against `Location.timeZoneId`.
- ClockOptions therefore replaces the retired options type under a neutral name; the IClock members keep their temporary names until Task 4 renames them with the rest of the time model.
- Configuration keys move from the retired section to `Clock:TimeZoneId` (`Clock__TimeZoneId` in the seed utility), and the address key is removed rather than renamed.
- API and MCP share EventBookingConfiguration.Read, so both surfaces lose the retired keys together.
- Startup still fails when a required key is missing: retirement removes a key, it does not make configuration optional.
- No ontology concept changes: the ontology already places `address` and `timeZoneId` on `Location`, and defines no deployment-level address.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Api.Tests/RetiredLocationConfigurationTests.cs

```csharp
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Time;
using Microsoft.Extensions.Configuration;

namespace EventBooking.Api.Tests;

public sealed class RetiredLocationConfigurationTests
{
    [Fact]
    public void Startup_configuration_needs_no_retired_location_section()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:EventBooking"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Clock:TimeZoneId"] = "Europe/London",
            ["Tokens:SigningKey"] = "a-test-signing-key-that-is-at-least-32-characters",
            ["Email:FromAddress"] = "test@example.test",
            ["Email:FromName"] = "Test sender",
            ["Email:Provider"] = "Smtp",
            ["Auth:Provider"] = "Local",
            ["Portal:BaseUrl"] = "http://localhost:5002",
            ["Portal:CoordinatorContact"] = "help@example.test",
        }).Build();

        var values = EventBookingConfiguration.Read(configuration);

        Assert.Equal("http://localhost:5002", values.Portal.BaseUrl);
        Assert.DoesNotContain(typeof(SystemClock).Assembly.GetTypes(),
            type => type.Name is "TransitionalLocationOptions" or "HeadOfficeOptions");
        Assert.DoesNotContain(typeof(AttendeePortalOptions).GetProperties(),
            property => property.Name.EndsWith("LocationAddress", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~RetiredLocationConfigurationTests
```

Expected: RetiredLocationConfigurationTests fails because startup still demands TransitionalLocation:TimeZoneId and TransitionalLocation:Address, so Read throws before the assertions run. After the change it passes on Clock:TimeZoneId alone, with no retired type and no address property. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 15 phase-0f-edits-NNN.md files supply 42 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed retired files whose before hash matches. Deleted files remain recoverable from the previous task commit.

```bash
node --input-type=module <<'RETIREMENT_NODE'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-0f-edits-')&&n.endsWith('.md')).sort();
if(names.length!==15)throw Error('Incomplete edit volumes.');
const entries=new Map();
for(const name of names){
 const text=fs.readFileSync(path.join(plan,name),'utf8');
 const pattern=/<!-- retirement-file: (.+) -->\n\n`{5}[^\n]*\n([\s\S]*?)\n`{5}/g;
 for(const match of text.matchAll(pattern)){
  const m=JSON.parse(match[1]);
  if(path.isAbsolute(m.file)||m.file.split('/').includes('..'))throw Error('Unsafe path.');
  const e=entries.get(m.id)??{...m,before:new Map(),after:new Map(),counts:{}};
  if(e.file!==m.file||e.beforeSha!==m.beforeSha||e.afterSha!==m.afterSha||e[m.side].has(m.part))throw Error('Conflicting metadata.');
  e[m.side].set(m.part,match[2]+'\n');e.counts[m.side]=m.parts;entries.set(m.id,e);
 }
}
if(entries.size!==42)throw Error('Incomplete operation set.');
const actions=[];
for(const e of entries.values()){
 for(const side of ['before','after']){
  if(e[side+'Sha']===null)continue;
  if(e[side].size!==e.counts[side])throw Error('Missing parts.');
  const parts=Array.from({length:e.counts[side]},(_,i)=>e[side].get(i+1));
  if(parts.some(p=>p===undefined))throw Error('Missing part number.');
  e[side+'Text']=parts.join('');
  if(sha(e[side+'Text'])!==e[side+'Sha'])throw Error('Payload checksum mismatch.');
 }
 const target=path.join(root,e.file);
 let parent=path.dirname(target);while(!fs.existsSync(parent))parent=path.dirname(parent);
 const resolved=fs.realpathSync(parent);
 if(resolved!==root&&!resolved.startsWith(root+path.sep))throw Error('Parent escapes checkout.');
 if(fs.existsSync(target)&&fs.lstatSync(target).isSymbolicLink())throw Error('Symlink target.');
 const actual=fs.existsSync(target)?sha(fs.readFileSync(target)):null;
 if(actual!==e.beforeSha&&actual!==e.afterSha)throw Error('Unrelated edit: '+e.file);
 actions.push({target,body:e.afterText,remove:e.afterSha===null});
}
for(const action of actions){
 if(action.remove){if(fs.existsSync(action.target))fs.unlinkSync(action.target);}
 else{fs.mkdirSync(path.dirname(action.target),{recursive:true});fs.writeFileSync(action.target,action.body);}
}
console.log('Applied '+actions.length+' verified file changes.');
RETIREMENT_NODE
```

This checkpoint changes configuration binding only; no schema migration is generated. STOP AND CHECK: do not rename the address key, and do not substitute a global replacement for it — an address belongs to a `Location` from Task 4. Retire the suites that assert a configured address rather than editing their expected strings into something else, and keep the assertions that the confirmation email and page no longer name one. The temporary clock keeps its transitional member names; only its options type and configuration keys change here.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~RetiredLocationConfigurationTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1402 tests: Domain 237, Application 422, Infrastructure 160, API 232, MCP 35, Web 241 and SeedData 75.

- [ ] **Step 6: Commit and push**

The canonical ontology already describes this invariant. No source ontology change is needed for retiring the incompatible predecessor path.

```bash
git add -- \
  'src/EventBooking.Api/EventBookingConfiguration.cs' \
  'src/EventBooking.Api/appsettings.Local.json' \
  'src/EventBooking.Api/appsettings.json' \
  'src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs' \
  'src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs' \
  'src/EventBooking.Application/Notifications/AttendeePortalOptions.cs' \
  'src/EventBooking.Infrastructure/DependencyInjection.cs' \
  'src/EventBooking.Infrastructure/Time/ClockOptions.cs' \
  'src/EventBooking.Infrastructure/Time/SystemClock.cs' \
  'src/EventBooking.Infrastructure/Time/TransitionalLocationOptions.cs' \
  'src/EventBooking.Mcp/appsettings.Local.json' \
  'src/EventBooking.Mcp/appsettings.json' \
  'src/EventBooking.SeedData/DemoEmailOptions.cs' \
  'src/EventBooking.SeedData/Program.cs' \
  'src/EventBooking.Web/Pages/Book.razor' \
  'src/EventBooking.Web/Services/BookingClient.cs' \
  'tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/HealthTests.cs' \
  'tests/EventBooking.Api.Tests/RetiredLocationConfigurationTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs' \
  'tests/EventBooking.SeedData.Tests/ReanchorTests.cs' \
  'tests/EventBooking.SeedData.Tests/ReseedTests.cs' \
  'tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "refactor: drop bulk import, head-office config and fixed StaffId format" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

This is the last Task 3 checkpoint. Return to [the Phase 0 overview](phase-0-port-and-strip.md) for the phase review and pull request.
