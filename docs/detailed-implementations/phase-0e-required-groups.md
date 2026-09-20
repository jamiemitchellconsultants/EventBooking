# 00e — Require an attendee group (Task 3c)

[← Overview](README.md) · [Ontology](../ontology.md)

This cross-layer retirement checkpoint follows Task 3b on the Phase 0 branch. It keeps the application buildable while removing one predecessor assumption. Complete changed types and exact before/after files are embedded in the numbered companion volumes.

> Use superpowers:executing-plans. This is a lettered split of master Task 3 and retains its commit message.

**Goal:** Require an attendee group without breaking the surviving booking workflows.

**Architecture:** Domain invariants are enforced at request boundaries and persisted by the existing EF Core infrastructure. API, MCP, seed and web remain synchronized.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 3](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

Keep this checkpoint on the existing Phase 0 feature branch. Do not push to main or open the phase PR yet. Preserve canonical names, capacity bounds, lock ordering and attendee CSV import. Do not log personal data or relax authorization to make a test pass.

## Review focus

STOP AND CHECK: do not conflate an unassigned staff role with an unassigned attendee group. Staff without assigned roles remain a supported authorization refusal. Preserve the predecessor migration guard tests until the fresh migration is introduced in Task 9. An inactive assigned group can still be read; only assignment authority requires an active group.

### Task 3c: Require an attendee group

**Files:**

- Modify: src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs
- Modify: src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/ResultResponses.cs
- Modify: src/EventBooking.Application/Attendees/AttendeeReadiness.cs
- Modify: src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs
- Modify: src/EventBooking.Application/Attendees/ListAttendeesHandler.cs
- Modify: src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs
- Modify: src/EventBooking.Application/Common/Error.cs
- Modify: src/EventBooking.Application/Invites/InviteIssuer.cs
- Modify: src/EventBooking.Domain/Attendees/Attendee.cs
- Modify: src/EventBooking.Mcp/Tools/AttendeeTools.cs
- Modify: src/EventBooking.Web/Pages/Attendees.razor
- Modify: src/EventBooking.Web/Services/AttendeePresentation.cs
- Modify: src/EventBooking.Web/Services/AttendeesClient.cs
- Test: tests/EventBooking.Api.Tests/RequiredAttendeeGroupContractTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs
- Test: tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs
- Modify: tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs
- Modify: tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs
- Modify: tests/EventBooking.Web.Tests/AttendeePresentationTests.cs
- Modify: tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs
- Modify: tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs
- Modify: tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
using System.Net.Mail;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Domain.Attendees;

/// <summary>A person invited to attend appointments, whose requirements derive from one attendee group.</summary>
public sealed class Attendee
{
    private readonly List<AttendeeRequirement> _requirements = [];

    private Attendee()
    {
        // Required by the persistence layer's constructor binding.
        Name = string.Empty;
        Email = string.Empty;
    }

    /// <summary>Gets the attendee identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the attendee display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the normalized attendee email address.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the required assigned Attendee Group identifier.</summary>
    public Guid AttendeeGroupId { get; private set; }

    /// <summary>Gets where the attendee sits in the invite and booking lifecycle.</summary>
    public AttendeeStatus Status { get; private set; } = AttendeeStatus.NotYetInvited;

    /// <summary>Gets the materialized appointment types the attendee currently requires.</summary>
    public IReadOnlyList<AttendeeRequirement> Requirements => _requirements;

    /// <summary>Gets the identifiers of the appointment types the attendee currently requires.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).ToList();

    /// <summary>Creates a Attendee and derives every requirement from the active mapped group.</summary>
    /// <param name="id">The id.</param>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    /// <param name="attendeeGroup">The attendee group.</param>
    public static Attendee Create(Guid id, string? name, string? email, AttendeeGroup attendeeGroup)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var attendee = new Attendee
        {
            Id = id,
            Name = Guard.NotBlank(name, "name"),
            Email = NormaliseEmail(email),
            Status = AttendeeStatus.NotYetInvited,
        };

        attendee.AssignAttendeeGroup(attendeeGroup);

        return attendee;
    }

    /// <summary>Replaces the attendee name and email after validating both.</summary>
    /// <param name="name">The name.</param>
    /// <param name="email">The email.</param>
    public void UpdateDetails(string? name, string? email)
    {
        // Validate both before mutating either.
        var newName = Guard.NotBlank(name, "name");
        var newEmail = NormaliseEmail(email);

        Name = newName;
        Email = newEmail;
    }

    /// <summary>Assigns a group and derives its complete set; returns whether that set changed.</summary>
    /// <param name="attendeeGroup">The attendee group.</param>
    public bool AssignAttendeeGroup(AttendeeGroup attendeeGroup)
    {
        ArgumentNullException.ThrowIfNull(attendeeGroup);
        Guard.Against(!attendeeGroup.IsActive, "An inactive attendee group cannot be assignment authority.");

        var mapping = attendeeGroup.RequiredAppointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        AttendeeGroupId = attendeeGroup.Id;

        if (_requirements.Select(r => r.AppointmentTypeId).Order().SequenceEqual(mapping.Order()))
        {
            return false;
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping.Order())
        {
            _requirements.Add(AttendeeRequirement.For(Id, appointmentTypeId));
        }

        return true;
    }

    /// <summary>Moves the attendee to Invited from a pre-booking lifecycle state.</summary>
    public void MarkInvited() => TransitionTo(
        AttendeeStatus.Invited,
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.Invited,
        AttendeeStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves the attendee to AwaitingAvailability from a pre-booking lifecycle state.</summary>
    public void MarkAwaitingAvailability() => TransitionTo(
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.AwaitingAvailability,
        AttendeeStatus.Invited,
        AttendeeStatus.NoResponseNeedsFollowUp);

    /// <summary>Moves an invited attendee to Booked.</summary>
    public void MarkBooked() => TransitionTo(AttendeeStatus.Booked, AttendeeStatus.Invited);

    /// <summary>Moves an invited attendee to NoResponseNeedsFollowUp.</summary>
    public void MarkNoResponse() => TransitionTo(
        AttendeeStatus.NoResponseNeedsFollowUp,
        AttendeeStatus.Invited);

    /// <summary>Returns an invited or booked attendee to NotYetInvited.</summary>
    public void ResetToNotYetInvited() => TransitionTo(
        AttendeeStatus.NotYetInvited,
        AttendeeStatus.Invited,
        AttendeeStatus.Booked);

    /// <summary>Resets an unbooked Attendee after a derived requirement-set change.</summary>
    public void ResetAfterRequirementChange()
    {
        if (Status is AttendeeStatus.NotYetInvited)
        {
            return;
        }

        TransitionTo(
            AttendeeStatus.NotYetInvited,
            AttendeeStatus.AwaitingAvailability,
            AttendeeStatus.NoResponseNeedsFollowUp,
            AttendeeStatus.Invited);
    }

    private void TransitionTo(AttendeeStatus target, params AttendeeStatus[] allowedOrigins)
    {
        Guard.Against(
            !allowedOrigins.Contains(Status),
            $"A attendee cannot move from {Status} to {target}.");

        Status = target;
    }

    private static string NormaliseEmail(string? email)
    {
        var trimmed = email?.Trim() ?? string.Empty;

        var valid =
            trimmed.Length > 0
            && !trimmed.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(trimmed, out var parsed)
            && parsed!.Host.Contains('.');

        Guard.Against(!valid, "email is not a valid email address.");

        return trimmed.ToLowerInvariant();
    }
}
```

```csharp
namespace EventBooking.Application.Attendees;

/// <summary>Explains whether EventBooking has completed every current Attendee requirement.</summary>
public enum AttendeeReadinessCode
{
    /// <summary>Every current requirement has a Completed non-cancelled attempt.</summary>
    Ready = 1,
    /// <summary>The Attendee has no Active original Booking journey.</summary>
    NoActiveBooking = 3,
    /// <summary>The current requirements differ from the journey's Appointment Type snapshots.</summary>
    RequirementSnapshotMismatch = 4,
    /// <summary>At least one current requirement has no Completed non-cancelled attempt.</summary>
    AppointmentsOutstanding = 5,
}

/// <summary>One booked attempt used to choose the latest non-cancelled result per type.</summary>
/// <param name="BookingAppointmentId">The stable appointment-record identifier.</param>
/// <param name="AppointmentTypeId">The attempted appointment type.</param>
/// <param name="Status">The appointment's operational status.</param>
/// <param name="BookingId">The parent booking identifier.</param>
/// <param name="BookingStatus">The parent booking lifecycle status.</param>
/// <param name="BookingCreatedAt">When the parent booking was created.</param>
public sealed record AttendeeReadinessAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    EventBooking.Domain.Bookings.BookingAppointmentStatus Status,
    Guid BookingId,
    EventBooking.Domain.Bookings.BookingStatus BookingStatus,
    DateTimeOffset BookingCreatedAt);

/// <summary>The authorized persistence projection consumed by the readiness calculator.</summary>
/// <param name="AttendeeId">The attendee identifier.</param>
/// <param name="AttendeeGroupId">The required assigned group.</param>
/// <param name="CurrentRequirementTypeIds">The group's current requirement set.</param>
/// <param name="ActiveOriginalBookingId">The active journey root, or null when absent.</param>
/// <param name="Attempts">Every booked attempt in the original and recovery journey.</param>
public sealed record AttendeeReadinessSnapshot(
    Guid AttendeeId,
    Guid AttendeeGroupId,
    IReadOnlyList<Guid> CurrentRequirementTypeIds,
    Guid? ActiveOriginalBookingId,
    IReadOnlyList<AttendeeReadinessAttempt> Attempts);

/// <summary>Minimum canonical detail for one incomplete current Appointment Type.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="IsRecoverable">Whether the latest attempt is a recoverable no-show.</param>
public sealed record OutstandingAppointmentType(
    string Code,
    string Name,
    bool IsRecoverable);

/// <summary>The internal EventBooking readiness result shown to a Coordinator.</summary>
/// <param name="AttendeeId">The attendee identifier.</param>
/// <param name="Code">The machine-readable readiness reason.</param>
/// <param name="OutstandingAppointmentTypes">Incomplete types sorted by code.</param>
public sealed record AttendeeReadiness(
    Guid AttendeeId,
    AttendeeReadinessCode Code,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);
```

**Context you need**

- FR-4.1 requires one AttendeeGroup for every Attendee.
- FR-4.1 (quoted): the system "shall let a Coordinator create an `Attendee`… and shall derive `AttendeeRequirement` from the group", with `attendeeGroupId` naming "an active group". FR-4.2 governs later edits and refuses a requirement-changing group change while the attendee holds an active original `Booking`.
- Master Task 3 deletes the legacy unassigned-group reconciliation path.
- AttendeeGroupId is a non-null Guid in the aggregate, list response and readiness snapshot.
- Creation without an AttendeeGroup is refused; an inactive or empty group remains invalid.
- Remove the retired readiness enum member without renumbering surviving members.
- A missing active original Booking still yields NoActiveBooking, not Ready.
- Remove the reconciliation flag from API, MCP and web contracts together.
- Attendee CSV import, requirement-snapshot mismatch checks and recovery workflows remain supported.
- The existing database mapping and migration already require attendee_group_id; no new migration is needed.
- The ontology already declares Attendee to belong to exactly one AttendeeGroup; this aligns the ported code to that invariant.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs

```csharp
using EventBooking.Domain.Attendees;

namespace EventBooking.Domain.Tests.Attendees;

public sealed class RequiredAttendeeGroupTests
{
    [Fact]
    public void Creation_without_a_group_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Attendee.Create(Guid.NewGuid(), "Demo attendee", "demo@example.test", null!));
    }

    [Fact]
    public void Aggregate_does_not_expose_a_nullable_group_identifier()
    {
        Assert.Equal(typeof(Guid), typeof(Attendee).GetProperty(nameof(Attendee.AttendeeGroupId))!.PropertyType);
    }
}
```

tests/EventBooking.Api.Tests/RequiredAttendeeGroupContractTests.cs

```csharp
using EventBooking.Application.Attendees;

namespace EventBooking.Api.Tests;

public sealed class RequiredAttendeeGroupContractTests
{
    [Fact]
    public void Readiness_and_list_contracts_have_no_reconciliation_state()
    {
        Assert.DoesNotContain("AttendeeGroupUnassigned", Enum.GetNames<AttendeeReadinessCode>());
        Assert.DoesNotContain(typeof(AttendeeListItem).GetProperties(),
            p => p.Name == "RequiresAttendeeGroupReconciliation");
        Assert.Equal(typeof(Guid), typeof(AttendeeListItem).GetProperty("AttendeeGroupId")!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(AttendeeReadinessSnapshot).GetProperty("AttendeeGroupId")!.PropertyType);
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~RequiredAttendeeGroupTests
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~RequiredAttendeeGroupContractTests
```

Expected: The missing-group creation test already passes, but the aggregate type test fails because the property is nullable. The API contract test fails because the retired readiness member and reconciliation flag still exist. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 8 phase-0e-edits-NNN.md files supply 24 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed retired files whose before hash matches. Deleted files remain recoverable from the previous task commit.

```bash
node --input-type=module <<'RETIREMENT_NODE'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-0e-edits-')&&n.endsWith('.md')).sort();
if(names.length!==8)throw Error('Incomplete edit volumes.');
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
if(entries.size!==24)throw Error('Incomplete operation set.');
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

The database already requires the group identifier; no schema migration is generated in this checkpoint. STOP AND CHECK: do not conflate an unassigned staff role with an unassigned attendee group. Staff without assigned roles remain a supported authorization refusal. Preserve the predecessor migration guard tests until the fresh migration is introduced in Task 9. An inactive assigned group can still be read; only assignment authority requires an active group.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~RequiredAttendeeGroupTests
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~RequiredAttendeeGroupContractTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1404 tests: Domain 237, Application 423, Infrastructure 160, API 232, MCP 35, Web 242 and SeedData 75.

- [ ] **Step 6: Commit and push**

The canonical ontology already describes this invariant. No source ontology change is needed for retiring the incompatible predecessor path.

```bash
git add -- \
  'src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs' \
  'src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/ResultResponses.cs' \
  'src/EventBooking.Application/Attendees/AttendeeReadiness.cs' \
  'src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs' \
  'src/EventBooking.Application/Attendees/ListAttendeesHandler.cs' \
  'src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs' \
  'src/EventBooking.Application/Common/Error.cs' \
  'src/EventBooking.Application/Invites/InviteIssuer.cs' \
  'src/EventBooking.Domain/Attendees/Attendee.cs' \
  'src/EventBooking.Mcp/Tools/AttendeeTools.cs' \
  'src/EventBooking.Web/Pages/Attendees.razor' \
  'src/EventBooking.Web/Services/AttendeePresentation.cs' \
  'src/EventBooking.Web/Services/AttendeesClient.cs' \
  'tests/EventBooking.Api.Tests/RequiredAttendeeGroupContractTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs' \
  'tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs' \
  'tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs' \
  'tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs' \
  'tests/EventBooking.Web.Tests/AttendeePresentationTests.cs' \
  'tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs' \
  'tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs' \
  'tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "refactor: drop bulk import, head-office config and fixed StaffId format" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Continue to Task 3d on the same branch. The Phase 0 PR waits until all Task 3 checkpoints pass.
