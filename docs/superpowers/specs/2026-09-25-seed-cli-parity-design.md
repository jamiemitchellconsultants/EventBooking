# Seed CLI parity with JointBooking — design

**Date:** 2026-09-25
**Status:** Approved for planning (direction given by the repository owner: "same switches on the
seed data project as JointBooking, e.g. reanchor and seed verbose")

## Problem

EventBooking's seed host ports the JointBooking seed host's behaviour but exposes a thinner
command line. Operators moving between the two repositories, and the LocalAI installer that drives
both, cannot use the same switches. Comparing the two hosts:

| Capability | JointBooking | EventBooking today |
|---|---|---|
| Migrate only | `--skip-seed` | no `--demo` (deliberate, see below) |
| Demo seed | default | `--demo` |
| `--reseed` | yes | yes (env-guarded) |
| `--reanchor` | optional `yyyy-MM-dd` argument; default today at head office | flag only, always today in London |
| `--verbose` | `[seed]` progress for every step, full exception on failure | seeders report progress; the host's own steps do not; failures print the message only |
| Reanchor confirmation line | `[seed] Reanchored to <date>.` | none |
| Keycloak summary counts | printed | printed only as "converged" |
| Usage text | full, per-switch, with environment notes | one line |

## Goals

1. `--reanchor` accepts an optional `yyyy-MM-dd` date, resolving every demo day offset against
   that date instead of today in London, without editing any file.
2. `--verbose` reports every host step (roles, migrations, reanchor, Keycloak, seed, invitations,
   load fixture) as `[seed] ...` lines, and on failure prints the full exception rather than only
   its message.
3. The host confirms the anchor it used and prints the Keycloak summary counts.
4. `--help` (and any usage error) prints complete usage text covering every switch and the
   environment variables each one depends on.

## Non-goals

- **`--skip-seed`.** The original EventBooking design deliberately chose migrate-only as the
  default with `--demo` as the opt-in, rejecting JointBooking's `--skip-seed` (see the
  implementation plan's "Known detail differences"). Not passing `--demo` is the equivalent, and
  reversing a recorded decision is out of scope for a parity change.
- Changing the LocalAI installer. It can gain matching `-Reanchor`, `-ReanchorDate` and
  `-SeedVerbose` switches in a follow-up once this lands.
- Changing what a normal seed, reseed or reanchor does to the database or Keycloak.

## Design

### Command line

SeedCliOptions gains ReanchorDate (`DateOnly?`). Parsing:

- The token immediately after `--reanchor` is consumed as the date when it matches
  `^\d{4}-\d{2}-\d{2}$`. It must then parse as a real calendar date, otherwise the run fails with
  `--reanchor date must be yyyy-MM-dd (got '<token>').` JointBooking silently falls back to today
  for an unparsable value, which is exactly the class of mistake that leaves demo dates stale
  without any signal, so EventBooking is stricter.
- A date-shaped token is never treated as the connection string, so the existing
  "exactly one connection string" rule is unchanged for every other input.
- `--reanchor` still requires `--demo`. ReanchorDate is null when the bare flag is used, meaning
  today in `Europe/London` (existing behaviour).

`--help` and `-h` print usage and exit 0 before any other parsing, so they work without a
connection string. Any usage error prints the same text after the error and exits 2.

### Host steps

`ISeedRunSteps.ReanchorToTodayAsync(ct)` becomes
`Task<DateOnly> ReanchorAsync(DateOnly? date, ct)`: it resolves the date (the argument, or today
in `Europe/London`), applies it as the demo anchor, re-anchors any existing demo rows, and returns
the date used. SeedCommand prints `[seed] Reanchored to <yyyy-MM-dd>.` unconditionally, matching
JointBooking.

### Verbose reporting

`SeedCommand.RunAsync` writes `[seed] ...` lines to its `output` writer only when
`options.Verbose`:

```
[seed] Applying database roles...
[seed] Applying pending migrations...
[seed] Migrations applied.
[seed] Reseed requested: the Keycloak realm will be deleted and recreated first, if configured.
[seed] Keycloak convergence complete.            (or "...realm reset and convergence complete.")
[seed] Keycloak provider seed skipped.           (when not configured)
[seed] Seeding demo data...
[seed] Sending demo invitations...
```

The seeders' own per-item progress continues to flow through their Progress writers, which
SeedRunSteps already wires to the console under `--verbose`. Non-verbose output is unchanged
except for the two always-printed lines below.

Always printed (not gated by `--verbose`), matching JointBooking:

- `Keycloak seed complete: N roles created, N mapper writes, N users created, N role-mapping writes.`
  when Keycloak is configured. The final demo summary line keeps its current wording.
- `[seed] Reanchored to <date>.` when `--reanchor` is given.

### Failure output

Program prints `Seed failed: <message>` normally and `Seed failed: <full exception>` when
`--verbose` is among the raw arguments. Verbosity is read from the raw arguments, not the parsed
options, so a parse failure under `--verbose` also prints the full exception. This logic moves
into a small testable SeedFailureReport type alongside the usage text in a new `SeedUsage.cs`.

### Usage text

`SeedUsage.Text` documents: the connection string argument and `ConnectionStrings__EventBooking`;
migrate-only default; `--demo`; `--reanchor [yyyy-MM-dd]`; `--reseed` and its
`EVENTBOOKING_ALLOW_RESEED=true` guard and realm-recreation behaviour; `--load-fixture` with its
two environment guards; `--verbose`; the Keycloak environment variables; and the invitation email
settings (`Portal__BaseUrl`, `Tokens__SigningKey`, `Email__Smtp__Host`/Port). Text is adapted from
JointBooking's, with EventBooking names.

## Testing

All in `tests/EventBooking.SeedData.Tests`, using the existing recording-steps double, no database:

- Parse: date consumed and not counted as a connection string; date before or after the
  connection string; invalid date rejected with the documented message; bare `--reanchor` leaves
  ReanchorDate null; `--reanchor <date>` without `--demo` rejected.
- Command: the reanchor step receives the requested date, and the printed anchor line shows the
  date the step returned; verbose emits the step lines in order and non-verbose emits none;
  Keycloak counts printed when a summary is returned.
- Usage: mentions every switch; `--help` recognised.
- Failure report: message-only without `--verbose`, full exception with it, including when the
  failure is a parse error.

## Documentation

`deploy/home-lab/README.md` and the seed section of the operator docs gain the new switch
descriptions. `docs/ontology.ttl` needs no change: no domain concept is added.

## Consequences

- Operators get one mental model across both repositories, apart from the deliberate
  `--demo`/`--skip-seed` inversion, which the usage text states.
- ReanchorToTodayAsync disappears from ISeedRunSteps; its only implementers are
  SeedRunSteps and the test double, both updated in the same change.
- A mistyped reanchor date now fails fast instead of silently anchoring to today.
