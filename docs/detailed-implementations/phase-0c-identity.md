# 00c — Configurable staff identity (Task 3a)

[← Overview](README.md) · [Ontology](../ontology.md)

This cross-layer retirement checkpoint follows Task 2 on the Phase 0 branch. It keeps the application buildable while removing one predecessor assumption. Complete changed types and exact before/after files are embedded in the numbered companion volumes.

> Use superpowers:executing-plans. This is a lettered split of master Task 3 and retains its commit message.

**Goal:** Configurable staff identity without breaking the surviving booking workflows.

**Architecture:** Domain invariants are enforced at request boundaries and persisted by the existing EF Core infrastructure. API, MCP, seed and web remain synchronized.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 3](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

Keep this checkpoint on the existing Phase 0 feature branch. Do not push to main or open the phase PR yet. Preserve canonical names, capacity bounds, lock ordering and attendee CSV import. Do not log personal data or relax authorization to make a test pass.

## Review focus

STOP AND CHECK: the PostgreSQL tests must accept both a 32-character identifier and ORG-10023 under its explicit custom expression. Do not keep the old seven-character database constraint. The Down migration cannot safely retain identifiers that do not fit the old format; do not roll back a populated database without a reviewed data-conversion plan.

### Task 3a: Configurable staff identity

**Files:**

- Modify: src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs
- Modify: src/EventBooking.Api/EventBooking.Api.csproj
- Modify: src/EventBooking.Api/Program.cs
- Modify: src/EventBooking.Application/Access/StaffAccessHandler.cs
- Create: src/EventBooking.Application/Access/StaffIdPolicy.cs
- Modify: src/EventBooking.Application/DependencyInjection.cs
- Modify: src/EventBooking.Domain/Access/StaffId.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/StaffIdentityConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Modify: src/EventBooking.Mcp/Program.cs
- Modify: src/EventBooking.Mcp/Tools/AdminTools.cs
- Modify: src/EventBooking.SeedData/Program.cs
- Modify: tests/EventBooking.Api.Tests/CallerIdentityTests.cs
- Test: tests/EventBooking.Api.Tests/ConfiguredStaffIdBoundaryTests.cs
- Modify: tests/EventBooking.Api.Tests/StaffIdentityRecorderTests.cs
- Test: tests/EventBooking.Domain.Tests/Access/ConfigurableStaffIdTests.cs
- Modify: tests/EventBooking.Domain.Tests/Access/StaffIdTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/ConfigurableStaffIdPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/StaffIdentityPersistenceTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
using System.Text.RegularExpressions;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Access;

/// <summary>A canonical staff number validated against the deployment's configured format.</summary>
public sealed record StaffId
{
    /// <summary>The organisation-neutral format used when no deployment override is supplied.</summary>
    public const string DefaultPattern = "^[A-Z0-9]{1,32}$";

    /// <summary>Trims, uppercases and validates an identity-provider staff number.</summary>
    /// <param name="value">The untrusted input, including any surrounding whitespace.</param>
    /// <param name="pattern">The deployment's complete-value validation expression.</param>
    /// <exception cref="DomainException">The input violates the configured format or size bound.</exception>
    public StaffId(string? value, string pattern = DefaultPattern)
    {
        var canonical = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Matches(canonical, pattern))
            throw new DomainException("staffId does not match the configured format.");
        Value = canonical;
    }

    /// <summary>Gets the normalized identifier; equality compares this value.</summary>
    public string Value { get; }

    /// <summary>Parses input using the deployment's format.</summary>
    /// <param name="value">The untrusted staff number.</param>
    /// <param name="pattern">The deployment's complete-value expression.</param>
    /// <returns>The canonical staff number.</returns>
    public static StaffId Parse(string? value, string pattern = DefaultPattern) => new(value, pattern);

    /// <summary>Validates input without throwing for malformed staff numbers.</summary>
    /// <param name="value">The untrusted staff number.</param>
    /// <param name="staffId">The parsed value, or null on refusal.</param>
    /// <param name="pattern">The deployment's complete-value expression.</param>
    /// <returns>True when the input is valid under the supplied policy.</returns>
    public static bool TryParse(string? value, out StaffId? staffId, string pattern = DefaultPattern)
    {
        try { staffId = new StaffId(value, pattern); return true; }
        catch (DomainException) { staffId = null; return false; }
    }

    /// <summary>Rehydrates a stored identifier without applying a later deployment policy.</summary>
    /// <param name="value">The canonical identifier stored by an earlier authenticated request.</param>
    /// <returns>The same stored identifier without changing its representation.</returns>
    public static StaffId FromPersisted(string value)
    {
        if (value != value.Trim().ToUpperInvariant())
            throw new DomainException("Stored staffId is not canonical.");
        return new StaffId(value, "^.{1,32}$");
    }

    /// <summary>Returns the canonical identifier.</summary>
    /// <returns>The value, without a presentation prefix.</returns>
    public override string ToString() => Value;

    private static bool Matches(string canonical, string pattern)
    {
        if (canonical.Length is < 1 or > 32) return false;
        try
        {
            var match = Regex.Match(canonical, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            return match.Success && match.Index == 0 && match.Length == canonical.Length;
        }
        catch (RegexMatchTimeoutException) { return false; }
    }
}
```

```csharp
using System.Text.RegularExpressions;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>Immutable deployment configuration shared by every staff-number input boundary.</summary>
public sealed class StaffIdPolicy
{
    /// <summary>Validates the deployment expression when the host starts.</summary>
    /// <param name="pattern">The configured regular expression, or the default when absent.</param>
    public StaffIdPolicy(string? pattern = null)
    {
        Pattern = pattern ?? StaffId.DefaultPattern;
        _ = new Regex(Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>Gets the expression applied after trimming and uppercasing staff identifiers.</summary>
    public string Pattern { get; }
}
```

**Context you need**

- FR-10.1 requires a staff_id claim matching the configured StaffId pattern.
- Decision D9 removes the predecessor-specific staff-number format; Task 3 supplies the default ^[A-Z0-9]{1,32}$.
- StaffId is trimmed and upper-cased before matching; 1–32 characters is an independent invariant.
- Identity__StaffIdPattern supplies the deployment expression to API, MCP and seed startup.
- Malformed configuration fails startup; malformed request claims remain refused rather than accepted by a fallback.
- The database stores canonical identifiers without imposing one deployment’s configurable regular expression.
- Persisted identifiers must remain readable if a deployment changes its admission pattern.
- StaffIdentity remains keyed by StaffUserId; StaffId is not an authorization role.
- The StaffId ontology entry already defines configuration-driven validation and uppercase normalization; no new concept is introduced.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Domain.Tests/Access/ConfigurableStaffIdTests.cs

```csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Access;

public sealed class ConfigurableStaffIdTests
{
    [Theory]
    [InlineData("A10023", "A10023")]
    [InlineData(" a10023 ", "A10023")]
    [InlineData("123", "123")]
    public void Default_format_accepts_and_canonicalises_organisation_neutral_identifiers(string input, string expected)
    {
        Assert.Equal(expected, new StaffId(input).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Default_format_rejects_missing_or_overlong_identifiers(string input)
    {
        Assert.Throws<DomainException>(() => new StaffId(input));
    }

    [Fact]
    public void Deployment_format_restricts_identifiers_after_normalisation()
    {
        Assert.Equal("U123456", new StaffId(" u123456 ", "^[UN][0-9]{6}$").Value);
        Assert.Throws<DomainException>(() => new StaffId("A10023", "^[UN][0-9]{6}$"));
    }
}
```

tests/EventBooking.Api.Tests/ConfiguredStaffIdBoundaryTests.cs

```csharp
using System.Security.Claims;
using EventBooking.Api.Auth;
using EventBooking.Application.Access;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Api.Tests;

public sealed class ConfiguredStaffIdBoundaryTests
{
    [Theory]
    [InlineData(" a10023 ", "^[A-Z0-9]{1,32}$", "A10023")]
    [InlineData(" u123456 ", "^[UN][0-9]{6}$", "U123456")]
    [InlineData("A10023", "^[UN][0-9]{6}$", null)]
    public void Request_identity_uses_the_configured_policy(string input, string pattern, string? expected)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("staff_id", input)], "test")),
        };
        var accessor = new HttpContextCallerAccessor(new HttpContextAccessor { HttpContext = context },
            NullLogger<HttpContextCallerAccessor>.Instance, new StaffIdPolicy(pattern));
        Assert.Equal(expected, accessor.StaffId?.Value);
    }

    [Fact]
    public void Invalid_deployment_expression_fails_during_policy_construction()
    {
        Assert.ThrowsAny<ArgumentException>(() => new StaffIdPolicy("["));
    }
}
```

tests/EventBooking.Infrastructure.Tests/ConfigurableStaffIdPersistenceTests.cs

```csharp
using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Persistence.Repositories;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public sealed class ConfigurableStaffIdPersistenceTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("A10023", "^[A-Z0-9]{1,32}$")]
    [InlineData("ORG-10023", "^ORG-[0-9]{5}$")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "^[A-Z0-9]{1,32}$")]
    public async Task Validated_staff_number_round_trips_without_the_retired_seven_character_constraint(string value, string pattern)
    {
        await fixture.ResetAsync();
        var id = Guid.NewGuid();
        var staffId = new StaffId(value, pattern);
        await using (var write = fixture.NewContext())
            await new StaffIdentityRepository(write).UpsertAsync(id, staffId, null,
                DateTimeOffset.Parse("2026-09-20T09:00:00Z"), CancellationToken.None);
        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read).GetByStaffIdAsync(staffId, CancellationToken.None);
        Assert.NotNull(identity);
        Assert.Equal(id, identity.StaffUserId);
        Assert.Equal(value, identity.StaffId.Value);
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~ConfigurableStaffIdTests
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~ConfiguredStaffIdBoundaryTests
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~ConfigurableStaffIdPersistenceTests
```

Expected: The Domain test first fails compilation because the two-argument StaffId constructor is absent. After adding that surface, default A10023 and the PostgreSQL cases fail against the old implementation/constraint. The API test also requires the new policy type. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 6 phase-0c-edits-NNN.md files supply 21 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed retired files whose before hash matches. Deleted files remain recoverable from the previous task commit.

```bash
node --input-type=module <<'RETIREMENT_NODE'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-0c-edits-')&&n.endsWith('.md')).sort();
if(names.length!==6)throw Error('Incomplete edit volumes.');
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
if(entries.size!==21)throw Error('Incomplete operation set.');
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
dotnet ef migrations add GeneralizeStaffIdentifiers --project src/EventBooking.Infrastructure --startup-project src/EventBooking.Api
```

Review the complete migration in the edit volumes before executing database-dependent tests. STOP AND CHECK: the PostgreSQL tests must accept both a 32-character identifier and ORG-10023 under its explicit custom expression. Do not keep the old seven-character database constraint. The Down migration cannot safely retain identifiers that do not fit the old format; do not roll back a populated database without a reviewed data-conversion plan.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Domain.Tests --filter FullyQualifiedName~ConfigurableStaffIdTests
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~ConfiguredStaffIdBoundaryTests
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~ConfigurableStaffIdPersistenceTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1445 tests: Domain 241, Application 451, Infrastructure 160, API 232, MCP 35, Web 251 and SeedData 75.

- [ ] **Step 6: Commit and push**

The canonical ontology already describes this invariant. No source ontology change is needed for retiring the incompatible predecessor path.

```bash
git add -- \
  'src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs' \
  'src/EventBooking.Api/EventBooking.Api.csproj' \
  'src/EventBooking.Api/Program.cs' \
  'src/EventBooking.Application/Access/StaffAccessHandler.cs' \
  'src/EventBooking.Application/Access/StaffIdPolicy.cs' \
  'src/EventBooking.Application/DependencyInjection.cs' \
  'src/EventBooking.Domain/Access/StaffId.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/StaffIdentityConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'src/EventBooking.Mcp/Program.cs' \
  'src/EventBooking.Mcp/Tools/AdminTools.cs' \
  'src/EventBooking.SeedData/Program.cs' \
  'tests/EventBooking.Api.Tests/CallerIdentityTests.cs' \
  'tests/EventBooking.Api.Tests/ConfiguredStaffIdBoundaryTests.cs' \
  'tests/EventBooking.Api.Tests/StaffIdentityRecorderTests.cs' \
  'tests/EventBooking.Domain.Tests/Access/ConfigurableStaffIdTests.cs' \
  'tests/EventBooking.Domain.Tests/Access/StaffIdTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/ConfigurableStaffIdPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/StaffIdentityPersistenceTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "refactor: drop bulk import, head-office config and fixed StaffId format" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Continue to Task 3b on the same branch. The Phase 0 PR waits until all Task 3 checkpoints pass.
