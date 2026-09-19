# Repository agent instructions

These instructions apply to the entire repository and are the canonical project instructions for
coding agents. Every rule here is binding regardless of which AI tool is reading it.

`CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, `.cursor/rules/`, `.windsurf/rules/`
and `.clinerules/` are thin pointers back to this file. They restate no rules, deliberately: a
stale copy is worse than no copy, because an agent cannot tell which one is current. **Edit this
file, not those.**

## What this repository is

EventBooking is a booking system for events with multiple session types. The codebase is .NET.

## Git and review discipline

- `main` is protected. Every change reaches it through a pull request that has a code owner's
  approval (see `.github/CODEOWNERS`).
- Do not bypass required review or CI, and do not push directly to `main`.
- Preserve unrelated user changes. Keep commits and pull requests focused.
- Do not commit secrets.
- Do not delete remote branches unless explicitly requested.
- Inspect the staged diff before committing and before pushing.

## Build and test workflow

- `.github/workflows/dotnet-build.yml` builds and tests on pull requests and on pushes to `main`,
  and cancels superseded runs. It is skipped when every changed file is documentation, `docs/`,
  `narrative/` or an agent-instruction file (`paths-ignore`).
- A pull request that touches a build path but does not need a build (for example a workflow-only
  change) takes the `no-build-check` label, which makes the `build` job report "skipped".
- `build` is deliberately not a required status check. Never make it one while it has a `paths`
  filter, and never give a workflow whose job is required a `paths` filter: a required check that
  does not run leaves every pull request pending forever.

## Project Narrative

This repository uses [Project Narrative](https://github.com/jamiemitchellconsultants/Narrative) to
maintain a deterministic, review-first decision history.

- `Narrative.md` is **generated and never hand-edited**. To change its wording, edit the fragment
  under `narrative/entries/` and run `narrative compile`.
- A decision-bearing pull request needs **both** the `narrative-required` label **and** three body
  headings, spelled exactly as `.github/pull_request_template.md` spells them:
  - `## Narrative Context`
  - `## Narrative Decision`
  - `## Narrative Consequences`
- The maintenance workflow fires on the **merge event only**. A missing label makes it exit
  silently; missing sections with the label present make it fail visibly. **Neither is repairable
  after merge** — labelling a merged pull request does nothing, and a missed entry has to be
  written by hand as a fragment.
- Open narrative proposal pull requests (`automation/narrative-pr-<number>`) conflict on
  `Narrative.md` when another entry merges first. `.github/workflows/refresh-narrative.yml` fixes
  this on the next push to `main` that touches the narrative, by merging `main` and recompiling. Never
  hand-merge `Narrative.md`; if the refresh run fails naming other files, resolve those, then run
  `narrative compile`.
- **Supplying a pull-request body replaces the repository template wholesale.** If you pass a body
  to `gh pr create`, carry the three sections in it yourself. This is the single most common way an
  installation decays: the template documents the rule, and the agent never reads it because the
  supplied body replaced it.
- A narrative-only pull request — one that fixes or maintains the narrative itself — carries no
  label, or it would recursively generate an entry about maintaining the narrative.
- An accepted entry is never rewritten to read as though a later, better framing had been there all
  along. A reversal is a new entry of kind `correction` citing the original by slug; otherwise the
  record loses the evidence that the framing ever needed correcting.

Apply the label when a pull request makes a meaningful product, architecture, governance,
operational, correction, or experimental decision. Leave it off for mechanical changes that do not
alter project intent.

## Detailed implementation plans

A superpowers plan (`docs/superpowers/plans/<slug>/`) is deliberately code-free — it is input for
spec-driven development, not something an execution agent can run directly. Once a superpowers plan
for a feature area exists, the next step is to generate its matching detailed implementation plan
under `docs/detailed-implementations/<slug>/`, using the same date-prefixed slug as the superpowers
plan it expands.

Use a prompt along these lines to generate it:

> from superpowers create implementation plans in docs/detailed-implementations detailed enough to
> be processed by opencode + superpowers + qwen3.6 27b q6. include code fragments and domain entity
> detail as needed. each task must end with a commit and push.

- The detailed plan must be self-sufficient for a small local model (opencode + superpowers +
  qwen3.6 27b q6) to execute without re-deriving design decisions already made in the superpowers
  plan or the spec.
- Split the document into numbered tasks (`### Task N: <name>`), continuing the numbering already
  used across `docs/detailed-implementations/<slug>/`'s other files rather than restarting at 1.
  Each task is TDD-shaped and must contain, in this order:
  1. **`**Files:**`** — every file the task creates or touches, marked `Create:`, `Modify:`, or
     `Test:`, with full repo-relative paths.
  2. **`**Interfaces:**`** — what the task consumes from earlier tasks and what it produces
     (namespace plus public surface), as a C# code fragment with member-level comments rather than
     prose. This is the "domain entity detail" a downstream task, and a smaller model, reads instead
     of re-deriving it from the spec.
  3. **`- [ ] **Step 1: Write the failing test**`** — a complete test file, not a sketch, using the
     project's real test framework and fixtures.
  4. Further numbered `- [ ]` steps implementing the production code to make that test pass, plus
     any additional tests the task needs.
  5. A final **`- [ ] **Step N: Commit and push**`** step with the literal shell commands:
     ```
     git add -A
     git commit -m "<type>(<scope>): <summary>"
     git push
     ```
     One commit per task — never batch multiple tasks into one commit, and never end a task without
     this step.
- Each detailed-implementation file opens with a one-line title (`# NN — <area> (Tasks X–Y)`), a
  `[← Overview](00-overview.md) · [Ontology](../../ontology.md)` link line, and a short paragraph
  giving the task range's scope and its position in the layering (domain / application /
  infrastructure / API / web) — mirror the existing files' framing rather than inventing new
  section furniture.
- Domain terminology must still follow the [Ontology protocol](#ontology-protocol) below — detailed
  plans are exactly the kind of plan doc that protocol covers.

<!-- ontology-protocol:start -->
## Ontology protocol

The application ontology's source is `docs/ontology.ttl`. [`docs/ontology.md`](docs/ontology.md)
is generated from it and must never be hand-edited — read it for reference, but make every change
in `docs/ontology.ttl` and run `node scripts/build-ontology.mjs` to regenerate
`docs/ontology.md`. It is the canonical source for all domain terminology. Code, specs and plan
documents must match the ontology — not the other way around.

### Before writing anything that touches domain concepts

1. Read `docs/ontology.md` in full.
2. Use the exact names defined there. Do not invent synonyms, abbreviations, or alternative
   spellings.
3. If a concept you need is not in the ontology, define it in `docs/ontology.ttl` first, run
   `node scripts/build-ontology.mjs`, then write the code.

### Before local validation and commit

1. Stage only the files intended for the proposed commit.
2. Review the staged file list and cached diff with `git diff --cached --name-only` and
   `git diff --cached` before validation.
3. Run `node scripts/build-ontology.mjs` after editing `docs/ontology.ttl`, so
   `docs/ontology.md` reflects it before you stage either file.
4. Run `node scripts/check-ontology-terms.mjs` after the intended files are staged and reviewed,
   but before creating the commit.
5. Stage newly created Markdown intended for the commit — the checker discovers Git-tracked
   Markdown with `git ls-files "*.md"` and does not see an untracked file.
6. Staging is not committing: files can still be corrected or unstaged before the commit is made.
7. Do not stage unrelated untracked or working files merely to expose them to validation.
8. CI remains the independent validation of the committed state.

### Checking text that is not a tracked file

A drafted pull-request body is Markdown that no tracked file contains, so the checker cannot see
it — and a bare backticked term sitting only in a PR description passes silently. Save the drafted
body to a scratch file and lint it explicitly:

```bash
node scripts/check-ontology-terms.mjs --also /path/to/drafted-body.md
```

### After completing any task that touches domain objects

1. Update `docs/ontology.ttl` — add, rename, or remove entities, value objects, events, enums,
   relationships, or invariants as needed.
2. Run `node scripts/build-ontology.mjs` to regenerate `docs/ontology.md` from it.
3. Include both files in the same commit as the code change.

### Enforcement

`scripts/build-ontology.mjs --check` and `scripts/check-ontology-terms.mjs` both run on every pull
request and every push to the default branch, via `.github/workflows/ontology-lint.yml`. The first
fails the build if `docs/ontology.md` does not match what `docs/ontology.ttl` would generate —
catching a forgotten `node scripts/build-ontology.mjs` before the second check even runs. The
second fails the build if any Markdown file uses a backticked PascalCase term not defined in the
ontology, or — where the ontology names External Systems — paraphrases one instead of naming it.

If the term check flags your term, there are exactly three correct responses:

1. Use the canonical name.
2. Add the concept to `docs/ontology.ttl` first, regenerate, then use it.
3. Only for a genuinely non-domain term — a framework type, an interface name, a config key — add
   it to `allowlist` in `ontology.config.json` **with a written reason**. An entry without a reason
   is rejected by the checker.

Reaching for option 3 by default is how this control decays. Prefer 1 and 2.
<!-- ontology-protocol:end -->

## AI fingerprint protocol

Every pull request body must carry an `AI-Fingerprint:` footer line proving it was produced by
following this protocol rather than typed by hand — it isn't cryptographically secure and isn't
meant to resist a determined forger; it only has to be tedious enough that nobody bothers computing
it manually.

- Before running `gh pr create` (or updating an open PR's body after pushing new commits), compute:
  ```
  MERGE_BASE=$(git merge-base origin/main HEAD)
  git diff "$MERGE_BASE" HEAD | sha256sum | cut -c1-12
  ```
  and add this exact footer line to the PR body:
  ```
  AI-Fingerprint: sha256:<the 12 hex characters from above>
  ```
- The hash is stable while `origin/main` advances (the merge-base doesn't move unless this branch
  is itself rebased), but changes with every commit pushed to the branch — recompute and update the
  PR body's footer line after any push that changes `HEAD`, or the `ai-fingerprint` check will fail
  on the stale hash. On macOS without `sha256sum`, use `shasum -a 256` in its place.
- `.github/workflows/ai-fingerprint.yml` recomputes the same hash from
  `merge-base(base, head)..head` on every `pull_request` event and compares it against the body's
  `AI-Fingerprint:` line. A missing or mismatched fingerprint is a **failing** check.
- There is no author exemption. A human committing directly is expected to run the same two
  commands. An administrator can bypass a red check, since `enforce_admins` is off.
- Supplying a pull-request body replaces the repository template wholesale (same rule as the
  Narrative sections above) — if you pass a body, carry the `AI-Fingerprint:` footer in it yourself.
- A narrative proposal pull request (branch `automation/narrative-pr-<number>`, opened by
  `github-actions[bot]`) is exempt: the Narrative action writes its body, and the check skips it.
