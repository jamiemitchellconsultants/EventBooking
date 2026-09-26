# 00 — `EventGroup` self-registration detailed implementation plan (overview)

[Ontology](../../ontology.md) · [Superpowers plan](../../superpowers/plans/2026-09-25-event-group-self-registration/00-overview.md) · [Spec](../../superpowers/specs/2026-09-25-event-group-self-registration-design.md)

This directory expands the code-free superpowers plan into TDD-shaped tasks for opencode +
superpowers + qwen3.6 27b q6. Tasks 1–3 establish reference data and the `EventGroup` model,
Tasks 4–5 add staff management, Tasks 6–7 add the public registration transaction, and Tasks
8–9 finish the anonymous UI and retention. Every task is a separately reviewable commit.

| File | Tasks | Layer |
|---|---|---|
| [01-domain-and-storage.md](01-domain-and-storage.md) | 1–3 | domain, reference data, persistence |
| [02-staff-and-request.md](02-staff-and-request.md) | 4–6 | application, staff/public API, web, outbox |
| [03-confirmation-and-finish.md](03-confirmation-and-finish.md) | 7–9 | booking transaction, anonymous web, maintenance |

## Branch and sequence

Use a feature branch created from the latest main, not main itself. If this planning branch has
already merged, create a fresh execution branch; otherwise continue this branch only after its
spec and plans have been reviewed. Do not rewrite an accepted narrative entry. Every task ends
with its own commit and push, then move to the next task.

## Decisions already made

- `EventGroup` membership is many-to-many. The open flag belongs to the `EventGroupEvent` join,
  separately from the `EventGroup`'s open flag.
- The exact type-set rule is union of listed `AttendeeGroup` requirements equals each member
  Event's capacity type IDs. A group's requirements may be a proper subset.
- Name, email and group are submitted on the initial page. An emailed token must be confirmed
  before any capacity is charged.
- A `SelfRegistration` remains pending for the `SystemSettings` window (default 24 hours, 1 to 168). Its terminal row, including
  submitted personal data, is retained for 30 further days. Existing Attendee names are not
  overwritten.
- Confirmation creates a one-option Invite and immediately uses it. Staff invitations remain
  governed by `SystemSettings`.

## Commands and gates

Run commands from the repository root. Each task below also gives its focused red-test command.
A PostgreSQL container is needed for infrastructure and API test projects.

```bash
dotnet test tests/EventBooking.Domain.Tests
dotnet test tests/EventBooking.Application.Tests
dotnet test tests/EventBooking.Infrastructure.Tests
dotnet test tests/EventBooking.Api.Tests
dotnet test tests/EventBooking.Web.Tests
dotnet build EventBooking.sln -warnaserror
node scripts/build-ontology.mjs --check
node scripts/check-ontology-terms.mjs
```

Before each task's final step, stage only its intended files, inspect `git diff --cached --name-only`
and `git diff --cached`, then run the ontology term check. The required final command block uses
`git add -A`; run it only when the working tree contains that task's intended files. The test
blocks below are the first tests for each task; add the further cases stated in each step before
committing. Code fragments define contracts and critical algorithms. Use the neighboring
repository files named in each task for constructor wiring and response conventions.

## Markdown rule

Use only the canonical ontology spellings for domain concepts. Avoid backticking C# identifiers
in Markdown prose; the term checker treats a backticked PascalCase word as a domain term. C#
identifiers inside fenced code blocks are exempt.
