# 01 — Domain and storage (Tasks 1–3)

[← Overview](00-overview.md) · [Ontology](../../ontology.md)

Tasks 1–3 add description to reference data, define `EventGroup` compatibility in the domain,
then persist the group and prevent requirement edits from invalidating its Event memberships.
They establish the domain and infrastructure contracts used by the application and API tasks.

### Task 1: `AttendeeGroup` description

**Files:**
- Modify: `src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs`
- Modify: `src/EventBooking.Application/ReferenceData/ReferenceDataCommands.cs`
- Modify: `src/EventBooking.Application/ReferenceData/AttendeeGroupHandlers.cs`
- Modify: `src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Migrations/20260925120000_AttendeeGroupDescription.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Migrations/20260925120000_AttendeeGroupDescription.Designer.cs`
- Modify: `src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs`
- Modify: `src/EventBooking.Api/Endpoints/AttendeeGroupEndpoints.cs`
- Modify: `src/EventBooking.Api/Contracts/ApiResponses.cs`
- Modify: `src/EventBooking.Web/Services/AdminClient.cs`
- Modify: `src/EventBooking.Web/Pages/Admin/AttendeeGroups.razor`
- Test: `tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupDescriptionTests.cs` (Create)
- Test: `tests/EventBooking.Api.Tests/Catalogue/ReferenceDataEndpointTests.cs` (Modify)
- Test: `tests/EventBooking.Web.Tests/Pages/Admin/AttendeeGroupsPageTests.cs` (Modify)

**Interfaces:**

```csharp
namespace EventBooking.Domain.AttendeeGroups;
public sealed class AttendeeGroup
{
    public const int MaximumDescriptionLength = 500; // NEW: trim input, permit empty
    public string Description { get; private set; } // NEW: defaults to empty for old rows
    public static AttendeeGroup Create(Guid id, string? code, string? name,
        IEnumerable<Guid> appointmentTypeIds, IReadOnlyCollection<Guid> activeAppointmentTypeIds,
        string? description = null); // extends existing factory without breaking callers
    public void ChangeDescription(string? description); // increments Version only on real change
}
namespace EventBooking.Application.ReferenceData;
public sealed record CreateAttendeeGroupCommand(Guid StaffUserId, string? Code, string? Name,
    IReadOnlyList<Guid> AppointmentTypeIds, string? Description = null);
public sealed record UpdateAttendeeGroupCommand(Guid StaffUserId, Guid AttendeeGroupId,
    string? Name, IReadOnlyList<Guid>? AppointmentTypeIds, bool IsActive,
    long ExpectedVersion, string? Description = null);
public sealed record AttendeeGroupResult(Guid Id, string Code, string Name, bool IsActive,
    long Version, IReadOnlyList<Guid> RequirementTypeIds, int MemberCount,
    string Description); // NEW final property
public sealed record AttendeeGroupListItem(Guid Id, string Code, string Name, bool IsActive,
    IReadOnlyList<Guid> RequirementTypeIds, int MemberCount, long Version,
    string Description); // NEW final property
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupDescriptionTests.cs`:

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.AttendeeGroups;

public sealed class AttendeeGroupDescriptionTests
{
    private static AttendeeGroup New(string? description) => AttendeeGroup.Create(
        Guid.NewGuid(), "VISITORS", "Visitors",
        [AppointmentTypeIds.MedicalCheckUp],
        [AppointmentTypeIds.MedicalCheckUp], description);

    [Fact]
    public void DescriptionIsTrimmedAndCanBeEmpty()
    {
        var group = New("  Who should choose this group.  ");
        Assert.Equal("Who should choose this group.", group.Description);
        var version = group.Version;
        group.ChangeDescription(" ");
        Assert.Equal("", group.Description);
        Assert.Equal(version + 1, group.Version);
        group.ChangeDescription(null);
        Assert.Equal(version + 1, group.Version);
    }

    [Fact]
    public void DescriptionLongerThanFiveHundredCharactersIsRejected()
    {
        Assert.Throws<DomainException>(() => New(new string('x', 501)));
    }
}
```

- [ ] **Step 2: Run the red test**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter "FullyQualifiedName~AttendeeGroupDescriptionTests"
```

Expected: compilation fails because the factory has no description parameter or property.

- [ ] **Step 3: Implement domain, contracts and persistence**

Add a constructor default of Description = string.Empty, a private normalizer, and the
versioned mutator. Keep the old Define calls valid by appending an optional description there
too. Extend create/update/list results, API request/response mapping and Admin client DTO/body.
Add a labelled textarea to the Admin form; preserve it in the form's From and ToDto methods.

```csharp
private static string BoundedDescription(string? value)
{
    var text = (value ?? string.Empty).Trim();
    Guard.Against(text.Length > MaximumDescriptionLength,
        $"description must be at most {MaximumDescriptionLength} characters.");
    return text;
}

public void ChangeDescription(string? value)
{
    var next = BoundedDescription(value);
    if (next == Description) return;
    Description = next;
    Version++;
}
```

Map a required varchar(500) description with default `''`; scaffold a normal EF migration and
rename its generated pair to the listed timestamp before committing. The migration adds the
column with `defaultValue: ""` so existing rows remain valid.

- [ ] **Step 4: Add boundary tests and verify green**

Add API assertions to the existing endpoint tests for description on create/update/list and a
422 for 501 characters. Add a web page test that edits and retains description after a version
conflict. Run the three affected test projects and `dotnet build EventBooking.sln -warnaserror`.

- [ ] **Step 5: Review, validate, commit and push**

Review intended staged files and the cached diff; run `node scripts/check-ontology-terms.mjs`.

```bash
git add -A
git commit -m "feat(attendee-groups): add a description"
git push
```

### Task 2: `EventGroup` compatibility aggregate

**Files:**
- Create: `src/EventBooking.Domain/EventGroups/EventGroup.cs`
- Create: `src/EventBooking.Domain/EventGroups/EventGroupAttendeeGroup.cs`
- Create: `src/EventBooking.Domain/EventGroups/EventGroupEvent.cs`
- Create: `src/EventBooking.Domain/EventGroups/EventGroupTypeSet.cs`
- Test: `tests/EventBooking.Domain.Tests/EventGroups/EventGroupTests.cs` (Create)

**Interfaces:**

```csharp
namespace EventBooking.Domain.EventGroups;
public sealed class EventGroup
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public bool IsOpen { get; private set; } // starts false
    public long Version { get; private set; } // starts 1
    public IReadOnlyList<EventGroupAttendeeGroup> AttendeeGroups { get; }
    public IReadOnlyList<EventGroupEvent> Events { get; }
    public static EventGroup Create(Guid id, string? title, string? description,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> requirements);
    public void Edit(string? title, string? description,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> requirements,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> memberEventTypes);
    public void AddEvent(Guid eventId, IReadOnlyCollection<Guid> eventTypes,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> requirements, bool isFuture);
    public void RemoveEvent(Guid eventId);
    public void SetOpen(bool open);
    public void SetEventOpen(Guid eventId, bool open);
}
public static class EventGroupTypeSet
{
    // Union of selected group requirements, compared by set equality with every member Event.
    public static IReadOnlyList<Guid> Validate(
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> requirements,
        IEnumerable<IReadOnlyCollection<Guid>> memberEventTypes);
}
public sealed class EventGroupAttendeeGroup
{
    public Guid EventGroupId { get; private set; }
    public Guid AttendeeGroupId { get; private set; }
}
public sealed class EventGroupEvent
{
    public Guid EventGroupId { get; private set; }
    public Guid EventId { get; private set; }
    public bool IsOpen { get; private set; }
}
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.Domain.Tests/EventGroups/EventGroupTests.cs`:

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.EventGroups;

namespace EventBooking.Domain.Tests.EventGroups;

public sealed class EventGroupTests
{
    private static readonly Guid Medical = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Uniform = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid GroupA = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid GroupB = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private static Dictionary<Guid, IReadOnlyCollection<Guid>> Groups() => new()
    {
        [GroupA] = [Medical],
        [GroupB] = [Medical, Uniform],
    };

    [Fact]
    public void EveryMemberEventMustEqualTheUnionOfSelectedRequirements()
    {
        var group = EventGroup.Create(Guid.NewGuid(), "  Autumn intake  ", "  Two dates  ", Groups());
        Assert.Equal("Autumn intake", group.Title);
        Assert.False(group.IsOpen);
        var eventId = Guid.NewGuid();
        group.AddEvent(eventId, [Uniform, Medical], Groups(), true);
        Assert.False(group.Events.Single().IsOpen);
        Assert.Throws<DomainException>(() => group.AddEvent(Guid.NewGuid(), [Medical], Groups(), true));
        Assert.Throws<DomainException>(() => group.AddEvent(Guid.NewGuid(),
            [Medical, Uniform, Guid.NewGuid()], Groups(), true));
    }

    [Fact]
    public void GatesAndMembershipsAreIndependent()
    {
        var eventId = Guid.NewGuid();
        var first = EventGroup.Create(Guid.NewGuid(), "First", null, Groups());
        var second = EventGroup.Create(Guid.NewGuid(), "Second", null, Groups());
        first.AddEvent(eventId, [Medical, Uniform], Groups(), true);
        second.AddEvent(eventId, [Medical, Uniform], Groups(), true);
        first.SetOpen(true);
        first.SetEventOpen(eventId, true);
        Assert.True(first.Events.Single().IsOpen);
        Assert.False(second.Events.Single().IsOpen);
        Assert.Throws<DomainException>(() => first.AddEvent(eventId, [Medical, Uniform], Groups(), true));
    }
}
```

- [ ] **Step 2: Run the red test**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter "FullyQualifiedName~EventGroupTests"
```

Expected: new namespace/type cannot be found.

- [ ] **Step 3: Implement the aggregate and compatibility policy**

Use private EF constructors and field-backed collections like AttendeeGroup. Title is 1–160
characters after trim, description 0–2,000. Every selected group must have at least one required
type. Reject empty IDs, duplicate Event memberships, and nonfuture Events. Increment Version
only on real edits or gate/membership changes. Do not persist an independent type set.

```csharp
var expected = requirements.Values.SelectMany(ids => ids).Distinct().Order().ToArray();
if (requirements.Count == 0 || requirements.Values.Any(ids => ids.Count == 0))
    throw new DomainException("An event group needs active attendee groups with requirements.");
foreach (var actual in memberEventTypes)
    if (!actual.Order().SequenceEqual(expected))
        throw new DomainException("Every event must list exactly the group's appointment types.");
return expected;
```

The application rejects repeated selected group IDs and verifies that selected groups are active;
the domain policy verifies set logic. Add tests for title/description bounds, no group, empty requirement set, duplicate membership,
nonfuture Event and replacing groups with a changed union.

- [ ] **Step 4: Verify green**

Run all domain tests, then build the solution to catch nullable or namespace mismatches.

- [ ] **Step 5: Review, validate, commit and push**

Review intended staged files and the cached diff; run `node scripts/check-ontology-terms.mjs`.

```bash
git add -A
git commit -m "feat(event-groups): define compatibility and publication"
git push
```

### Task 3: `EventGroup` persistence and mapping guard

**Files:**
- Modify: `src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Configurations/EventGroupConfiguration.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Configurations/EventGroupAttendeeGroupConfiguration.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Configurations/EventGroupEventConfiguration.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Repositories/EventGroupRepository.cs`
- Create: `src/EventBooking.Application/Abstractions/IEventGroupRepository.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Migrations/20260925123000_EventGroups.cs`
- Create: `src/EventBooking.Infrastructure/Persistence/Migrations/20260925123000_EventGroups.Designer.cs`
- Modify: `src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs`
- Modify: `src/EventBooking.Infrastructure/DependencyInjection.cs`
- Modify: `src/EventBooking.Application/ReferenceData/AttendeeGroupHandlers.cs`
- Test: `tests/EventBooking.Infrastructure.Tests/EventGroups/EventGroupPersistenceTests.cs` (Create)
- Test: `tests/EventBooking.Infrastructure.Tests/Groups/ReplaceAttendeeGroupRequirementsTests.cs` (Modify)

**Interfaces:**

```csharp
namespace EventBooking.Application.Abstractions;
public interface IEventGroupRepository
{
    // Read paths load both membership collections; lock precedes any gate/mapping mutation.
    Task<EventGroup?> GetAsync(Guid id, CancellationToken ct);
    Task<EventGroup?> LockForUpdateAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<EventGroup>> ListAsync(CancellationToken ct);
    Task<IReadOnlyList<EventGroup>> ListContainingAttendeeGroupAsync(Guid attendeeGroupId, CancellationToken ct);
    void Add(EventGroup group);
}
namespace EventBooking.Infrastructure.Persistence;
public sealed class EventBookingDbContext
{
    public DbSet<EventGroup> EventGroups => Set<EventGroup>();
    public DbSet<EventGroupEvent> EventGroupEvents => Set<EventGroupEvent>();
}
```

- [ ] **Step 1: Write the failing test**

Create `tests/EventBooking.Infrastructure.Tests/EventGroups/EventGroupPersistenceTests.cs`:

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.EventGroups;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests.EventGroups;

[Collection("postgres")]
public sealed class EventGroupPersistenceTests(PostgresFixture fixture)
    : PostgresBlockingHarness(fixture)
{
    [Fact]
    public async Task MembershipGateAndGroupVersionRoundTrip()
    {
        var location = await SeedLocationAsync("GROUPSITE", "Europe/London");
        var eventItem = await SeedFutureEventAsync(location.Id);
        var groups = new Dictionary<Guid, IReadOnlyCollection<Guid>>
        {
            [AttendeeGroupIds.CabinCrew] = [
                AppointmentTypeIds.DrugAndAlcoholTesting,
                AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting],
        };
        var group = EventGroup.Create(Guid.NewGuid(), "Open days", "Choose a date", groups);
        group.AddEvent(eventItem.Id, eventItem.Capacities.Select(x => x.AppointmentTypeId).ToArray(),
            groups, true);
        group.SetEventOpen(eventItem.Id, true);
        Context.EventGroups.Add(group);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var reloaded = await Context.EventGroups.Include(x => x.AttendeeGroups)
            .Include(x => x.Events).SingleAsync(x => x.Id == group.Id);
        Assert.Single(reloaded.AttendeeGroups);
        Assert.True(reloaded.Events.Single().IsOpen);
        Assert.False(reloaded.IsOpen);
        Assert.True(reloaded.Version > 1);
    }
}
```

- [ ] **Step 2: Run the red test**

```bash
dotnet test tests/EventBooking.Infrastructure.Tests --filter "FullyQualifiedName~EventGroupPersistenceTests"
```

Expected: EventGroups DbSet and mapping types are missing.

- [ ] **Step 3: Map, migrate and register the repository**

Use `event_group`, `event_group_attendee_group`, and `event_group_event`; composite keys on the
two joins; required FKs with Restrict delete; a concurrency token on `EventGroup` Version; and a
filtered index supporting open memberships by group. Scaffold and inspect the migration, rename
its generated pair to the listed timestamp, and keep the snapshot in sync. Register the
repository in DependencyInjection. Lock with PostgreSQL `FOR UPDATE` on the parent `EventGroup`
row before loading gate and selected-group data.

```csharp
builder.HasKey(x => new { x.EventGroupId, x.EventId });
builder.HasIndex(x => new { x.EventGroupId, x.IsOpen });
builder.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId)
    .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 4: Protect reference-data edits**

In UpdateAttendeeGroupHandler begin a transaction when requirements or activation may change.
Lock containing `EventGroup` rows in ascending ID order, then validate the proposed replacement
requirement set against every member Event's capacity type IDs using EventGroupTypeSet. Refuse
deactivation whenever any `EventGroup` contains the group, including groups with zero Events.
Keep the existing active-booking/member rules. Add focused PostgreSQL tests for drift, safe
set-equivalent updates, and deactivation. Run infrastructure and application tests.

- [ ] **Step 5: Review, validate, commit and push**

Review intended staged files and the cached diff; run `node scripts/check-ontology-terms.mjs`.

```bash
git add -A
git commit -m "feat(event-groups): persist memberships and guard group mappings"
git push
```
