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
