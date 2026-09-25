# 00 — Seed CLI parity detailed implementation plan (overview)

[Ontology](../../ontology.md) · [Superpowers plan](../../superpowers/plans/2026-09-25-seed-cli-parity/00-overview.md) · [Spec](../../superpowers/specs/2026-09-25-seed-cli-parity-design.md)

This directory expands the code-free superpowers plan into tasks a small local model (opencode +
superpowers + qwen3.6 27b q6) can execute without re-deriving any design decision. It gives the
seed host the same operator-facing switches as JointBooking's: an optional reanchor date,
step-level verbose progress with full exceptions on failure, a reanchor confirmation line,
Keycloak summary counts and complete help text.

| File | Tasks | Layer |
|---|---|---|
| [01-seed-cli-parity.md](01-seed-cli-parity.md) | 1–4 | SeedData host (parser, orchestration, entry point) and operator docs |

## Branch

The work is on `feat/seed-cli-parity`, created from the latest `origin/main`. Do not push to
`main`; the change reaches it through a pull request with a code owner's approval.

```bash
git fetch origin
git switch feat/seed-cli-parity || git switch -c feat/seed-cli-parity origin/main
```

## Decisions already made (do not reopen)

- `--skip-seed` is **not** added. Migrate-only remains "no `--demo`" (recorded in the master plan's
  "Known detail differences").
- A date after `--reanchor` must be shaped yyyy-MM-dd and be a real date; a bad date fails with
  `--reanchor date must be yyyy-MM-dd (got '<token>').` It never silently falls back to today.
- The reanchor confirmation line and the Keycloak counts line are always printed; every other new
  line is verbose-only.
- Usage text is printed for `--help`/`-h` (exit 0) and after option-parse errors (exit 2), not
  after runtime failures such as a Keycloak error.

## Commands used by every task

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test tests/EventBooking.SeedData.Tests --filter "FullyQualifiedName~<TestClass>"
node scripts/check-ontology-terms.mjs
```

Run them from the repository root. No database or Docker is needed for any test in this plan.

## Markdown rule

Do not put backticks around code identifiers (class, method, property names) in Markdown prose:
`scripts/check-ontology-terms.mjs` flags them as undefined domain terms. Identifiers inside fenced
code blocks are fine.
