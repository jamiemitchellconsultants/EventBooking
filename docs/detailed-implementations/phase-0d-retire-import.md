# 00d — Retire direct event import (Task 3b)

[← Overview](README.md) · [Ontology](../ontology.md)

This cross-layer retirement checkpoint follows Task 3a on the Phase 0 branch. It keeps the application buildable while removing one predecessor assumption. Complete changed types and exact before/after files are embedded in the numbered companion volumes.

> Use superpowers:executing-plans. This is a lettered split of master Task 3 and retains its commit message.

**Goal:** Retire direct event import without breaking the surviving booking workflows.

**Architecture:** Domain invariants are enforced at request boundaries and persisted by the existing EF Core infrastructure. API, MCP, seed and web remain synchronized.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 3](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

Keep this checkpoint on the existing Phase 0 feature branch. Do not push to main or open the phase PR yet. Preserve canonical names, capacity bounds, lock ordering and attendee CSV import. Do not log personal data or relax authorization to make a test pass.

## Review focus

STOP AND CHECK: the migration refuses an existing event with a null proposal_id. Never substitute an empty GUID or fabricate historical acceptance. Phase 0 is a clean port, not a predecessor production-data upgrade. If such rows exist, stop and obtain an explicit data-migration decision. The seed tests check confirmed proposal links, distinct proposal identifiers and preserved invitation/booking behavior, not just adjusted row totals.

### Task 3b: Retire direct event import

**Files:**

- Modify: docs/user-guides/README.md
- Modify: docs/user-guides/admin-guide.md
- Modify: docs/user-guides/appointment-staff-guide.md
- Modify: docs/user-guides/coordinator-guide.md
- Modify: src/EventBooking.Api/Endpoints/EventEndpoints.cs
- Modify: src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs
- Modify: src/EventBooking.Application/Access/StaffAccessAuthorizer.cs
- Modify: src/EventBooking.Application/Access/StaffCapability.cs
- Modify: src/EventBooking.Application/DependencyInjection.cs
- Modify: src/EventBooking.Application/Events/EventImportParser.cs (delete)
- Modify: src/EventBooking.Application/Events/ImportEventsHandler.cs (delete)
- Modify: src/EventBooking.Domain/Audit/AuditAction.cs
- Modify: src/EventBooking.Domain/Events/Event.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920061416_RequireEventProposal.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920061416_RequireEventProposal.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Modify: src/EventBooking.Mcp/Tools/EventTools.cs
- Create: src/EventBooking.SeedData/DemoEventFactory.cs
- Modify: src/EventBooking.SeedData/DemoInvitationSeeder.cs
- Modify: src/EventBooking.SeedData/DemoSeeder.cs
- Modify: src/EventBooking.Web/Pages/Audit.razor
- Modify: src/EventBooking.Web/Pages/EventOperations.razor
- Modify: src/EventBooking.Web/Program.cs
- Modify: src/EventBooking.Web/Services/EventOperationsClient.cs (delete)
- Modify: src/EventBooking.Web/Services/StaffNavigation.cs
- Modify: tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs
- Modify: tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/Fixtures/EventFixture.cs
- Modify: tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs
- Modify: tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/RetiredEventImportTests.cs
- Modify: tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/EventImportParserTests.cs (delete)
- Modify: tests/EventBooking.Application.Tests/Events/ImportEventsHandlerTests.cs (delete)
- Modify: tests/EventBooking.Application.Tests/Events/SharedEventAuthorizationTests.cs (delete)
- Test: tests/EventBooking.Application.Tests/Fixtures/EventFixture.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs
- Modify: tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs
- Modify: tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentAuditVocabularyTests.cs
- Modify: tests/EventBooking.Domain.Tests/Events/EventImportTests.cs (delete)
- Modify: tests/EventBooking.Domain.Tests/OntologyEnumTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/Fixtures/EventFixture.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs
- Modify: tests/EventBooking.Mcp.Tests/AgentSurfaceParityTests.cs
- Test: tests/EventBooking.Mcp.Tests/Fixtures/EventFixture.cs
- Modify: tests/EventBooking.Mcp.Tests/McpEndpointTests.cs
- Modify: tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs
- Modify: tests/EventBooking.SeedData.Tests/ReseedTests.cs
- Modify: tests/EventBooking.Web.Tests/EventOperationsClientTests.cs (delete)
- Modify: tests/EventBooking.Web.Tests/EventsPageTests.cs
- Modify: tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>Defines event for the current use case.</summary>
public sealed class Event
{
    private readonly List<EventCapacity> _capacities = [];

    private Event()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventStatus Status { get; private set; } = EventStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<EventCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a eventItem is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static Event CreateFrom(Guid id, EventProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var eventItem = new Event
        {
            Id = id,
            ProposalId = proposal.Id,
            Window = proposal.Window,
            Status = EventStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            eventItem._capacities.Add(
                EventCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return eventItem;
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public EventCapacity CapacityFor(Guid appointmentTypeId)
    {
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This event has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == EventStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == EventStatus.Cancelled, "This event has already been cancelled.");
        Status = EventStatus.Cancelled;
    }
}
```

```csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;

namespace EventBooking.SeedData;

/// <summary>Reconstructs accepted demo proposals instead of creating events without negotiation history.</summary>
internal static class DemoEventFactory
{
    internal static Event Create(EventBookingDbContext database, Guid id, EventWindow window,
        IReadOnlyDictionary<Guid, int> headcounts)
    {
        var managers = DemoSeedSpec.Staff().Where(staff => staff.Roles.Contains(Role.Manager))
            .ToDictionary(staff => staff.AppointmentTypeId!.Value, staff => staff.UserId);
        var types = AppointmentTypeIds.All.Order().ToArray();
        var proposal = EventProposal.Create(Guid.NewGuid(), window, managers[types[0]]);
        foreach (var type in types)
            proposal.Accept(type, managers[type], headcounts[type]);
        var created = Event.CreateFrom(id, proposal);
        database.EventProposals.Add(proposal);
        database.Events.Add(created);
        return created;
    }
}
```

**Context you need**

- Decision D6 and master Task 3 remove direct event import from every public surface.
- FR-2.6 (quoted): "When a `ProposalAcceptance` makes the number of distinct accepted types equal the number of listed types, the system shall, in the same transaction: set the proposal `Confirmed`; create an `Active` `Event` with the proposal’s `Location` and `EventWindow`…" An Event therefore has exactly one origin: acceptance of its EventProposal.
- An Event therefore always has a non-null ProposalId; no imported-event factory remains.
- FR-4.3 attendee CSV import remains supported: removing event import must not remove that separate operation.
- The ImportEvents capability and EventImported and StaffAccessRemoved audit members are retired.
- Do not renumber surviving audit members: persisted numeric values retain their meaning.
- The seed utility reconstructs fully accepted proposals for demo events instead of bypassing negotiation history.
- Five standalone negotiation scenarios remain, plus one confirmed proposal per seeded Event.
- The catalogue loses exactly one API/MCP operation, leaving 35 at this checkpoint.
- Capacity behavior and existing ordered-locking algorithms do not change in this task.
- The ontology already requires Event to arise from EventProposal; this removes incompatible predecessor behavior, not a new concept.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Api.Tests/RetiredEventImportTests.cs

```csharp
using System.Text.Json;
using EventBooking.Application.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class RetiredEventImportTests(ApiFactory factory)
{
    [Fact]
    public async Task Event_import_is_absent_but_attendee_csv_import_remains()
    {
        using var document = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("/api/events/import", paths);
        Assert.Contains("/api/attendees/import", paths);
    }

    [Fact]
    public void No_retired_capability_action_or_proposalless_factory_remains()
    {
        Assert.DoesNotContain("ImportEvents", Enum.GetNames<StaffCapability>());
        Assert.DoesNotContain("EventImported", Enum.GetNames<AuditAction>());
        Assert.DoesNotContain("StaffAccessRemoved", Enum.GetNames<AuditAction>());
        Assert.DoesNotContain(typeof(Event).GetMethods(), method => method.Name == "CreateImported");
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~RetiredEventImportTests
```

Expected: Both tests fail: the generated OpenAPI still advertises event import, and the retired capability/action/factory names are still present. Attendee CSV import must remain advertised. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 19 phase-0d-edits-NNN.md files supply 64 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed retired files whose before hash matches. Deleted files remain recoverable from the previous task commit.

```bash
node --input-type=module <<'RETIREMENT_NODE'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-0d-edits-')&&n.endsWith('.md')).sort();
if(names.length!==19)throw Error('Incomplete edit volumes.');
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
if(entries.size!==64)throw Error('Incomplete operation set.');
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

The included migration, designer and model snapshot were generated with this exact command (already represented in the supplied after files; do not create a duplicate migration):

```bash
dotnet ef migrations add RequireEventProposal --project src/EventBooking.Infrastructure --startup-project src/EventBooking.Api
```

Review the complete migration in the edit volumes before executing database-dependent tests. STOP AND CHECK: the migration refuses an existing event with a null proposal_id. Never substitute an empty GUID or fabricate historical acceptance. Phase 0 is a clean port, not a predecessor production-data upgrade. If such rows exist, stop and obtain an explicit data-migration decision. The seed tests check confirmed proposal links, distinct proposal identifiers and preserved invitation/booking behavior, not just adjusted row totals.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~RetiredEventImportTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1404 tests: Domain 235, Application 423, Infrastructure 160, API 231, MCP 35, Web 245 and SeedData 75.

- [ ] **Step 6: Commit and push**

The canonical ontology already describes this invariant. No source ontology change is needed for retiring the incompatible predecessor path.

```bash
git add -- \
  'docs/user-guides/README.md' \
  'docs/user-guides/admin-guide.md' \
  'docs/user-guides/appointment-staff-guide.md' \
  'docs/user-guides/coordinator-guide.md' \
  'src/EventBooking.Api/Endpoints/EventEndpoints.cs' \
  'src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs' \
  'src/EventBooking.Application/Access/StaffAccessAuthorizer.cs' \
  'src/EventBooking.Application/Access/StaffCapability.cs' \
  'src/EventBooking.Application/DependencyInjection.cs' \
  'src/EventBooking.Application/Events/EventImportParser.cs' \
  'src/EventBooking.Application/Events/ImportEventsHandler.cs' \
  'src/EventBooking.Domain/Audit/AuditAction.cs' \
  'src/EventBooking.Domain/Events/Event.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920061416_RequireEventProposal.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920061416_RequireEventProposal.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'src/EventBooking.Mcp/Tools/EventTools.cs' \
  'src/EventBooking.SeedData/DemoEventFactory.cs' \
  'src/EventBooking.SeedData/DemoInvitationSeeder.cs' \
  'src/EventBooking.SeedData/DemoSeeder.cs' \
  'src/EventBooking.Web/Pages/Audit.razor' \
  'src/EventBooking.Web/Pages/EventOperations.razor' \
  'src/EventBooking.Web/Program.cs' \
  'src/EventBooking.Web/Services/EventOperationsClient.cs' \
  'src/EventBooking.Web/Services/StaffNavigation.cs' \
  'tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs' \
  'tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/Fixtures/EventFixture.cs' \
  'tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs' \
  'tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/RetiredEventImportTests.cs' \
  'tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs' \
  'tests/EventBooking.Application.Tests/Events/EventImportParserTests.cs' \
  'tests/EventBooking.Application.Tests/Events/ImportEventsHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/SharedEventAuthorizationTests.cs' \
  'tests/EventBooking.Application.Tests/Fixtures/EventFixture.cs' \
  'tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs' \
  'tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentAuditVocabularyTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventImportTests.cs' \
  'tests/EventBooking.Domain.Tests/OntologyEnumTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/Fixtures/EventFixture.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs' \
  'tests/EventBooking.Mcp.Tests/AgentSurfaceParityTests.cs' \
  'tests/EventBooking.Mcp.Tests/Fixtures/EventFixture.cs' \
  'tests/EventBooking.Mcp.Tests/McpEndpointTests.cs' \
  'tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs' \
  'tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs' \
  'tests/EventBooking.SeedData.Tests/ReseedTests.cs' \
  'tests/EventBooking.Web.Tests/EventOperationsClientTests.cs' \
  'tests/EventBooking.Web.Tests/EventsPageTests.cs' \
  'tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "refactor: drop bulk import, head-office config and fixed StaffId format" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Continue to Task 3c on the same branch. The Phase 0 PR waits until all Task 3 checkpoints pass.
