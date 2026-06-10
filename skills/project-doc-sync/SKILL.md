---
name: project-doc-sync
description: Keep this project's design and planning docs — GDD.md, GAME_SPEC.md, KANBAN_BOARD.md, and CLAUDE.md — aligned with the actual codebase. Use whenever updating, reviewing, or reconciling any of these docs; when asked whether the docs are accurate or current; when the kanban or spec status looks stale; after a batch of features lands; or before snapshotting or sharing project state — even for a casual "update the kanban" or "is the GDD still right?". Always verify claims against source code and git history, never against the docs' own status statements.
---

# Project Doc Sync

## Outcome

The four project docs stay truthful about the code, each doing its own job without duplicating the others:
- Accurate status (built / partial / planned) grounded in the code, not in what a doc says about itself.
- A short drift report whenever docs are reconciled, so changes are auditable.
- Docs that don't rot — a clear division of labour plus a repeatable verification method.

## Core principle: the code is the source of truth

Docs are *derived* from the code, never the reverse. A doc's own status claims are untrustworthy by default — they reflect whatever was true when someone last typed them, not what is true now.

> Cautionary example from this project: `KANBAN_BOARD.md` literally said "reflects actual codebase state" while sitting ~27 commits behind. It listed Adventurer XP/Rank-Up as unbuilt when it had fully shipped (backend + Roster UI), and listed transport upgrades as untouched when the tier mechanic already existed. Trusting it produced a wrong spec on the first pass.

So when reconciling: **read the code and git history. Where a doc and the code disagree, the code wins.**

## The four docs and their roles

Keep these jobs distinct — overlap between them is what makes them rot.

| Doc | Job | Don't |
|---|---|---|
| `Idle Merchant Guild/GDD.md` | **Design intent** — the *why*, mechanics, content tables, rationale | Turn it into a status tracker |
| `GAME_SPEC.md` (repo root) | **Current build status** — feature-by-feature, code-referenced, status-tagged | Restate full design rationale |
| `KANBAN_BOARD.md` (repo root) | **Lightweight board** — Up Next / Backlog / Done as scannable tables | Duplicate the spec's detail |
| `Idle Merchant Guild/CLAUDE.md` | **Architecture & conventions** for working in the code | Track feature status here |

## Workflow: a drift-sync pass

1. **Establish the baseline.** Note each doc's "Last Updated" date / version. Find current head:
   `git -C "Idle Merchant Guild" log -1 --format="%h %cd" --date=short`
2. **Detect drift.** List commits since the doc's date:
   `git -C "Idle Merchant Guild" log --since="<doc date>" --format="%h %cd %s" --date=short`
   Treat commit subjects as *hints*, not proof.
3. **Verify in code.** For each area that may have changed, confirm actual state in `Assets/Scripts/` (see Verification techniques). Separate *designed* from *built*.
4. **Write the drift report first** (see format below) — what changed, which doc claims were wrong — *before* editing anything. This is the audit trail.
5. **Update each doc per its rules** (see Per-doc update discipline).
6. **Stamp and record.** Update each touched doc's "Last Updated" + the commit hash verified against. Flag any doc you didn't touch but noticed is stale.

## Verification techniques

The goal is to tell *built* from *designed* from *stubbed*.

- **Grep high-signal terms** across `Assets/Scripts/` to map reality fast — feature nouns like `XP` / `Promote`, `Party`, `PlayerPrefs` / `SaveData`, `Elevator` / `Teleporter`. Zero matches is strong evidence of "not built"; matches mean "go read it." (zsh tip: quote globs, e.g. `grep -rln --include='*.cs' "term" .`)
- **Read the implementation, not the filename.** A class can exist and be empty — `ContractManager` was a real file with empty `Start()`/`Update()`. A field can exist without being wired — the clicker's AOE radius existed but no purchasable upgrade drove it.
- **Three states to separate:**
  - **Built** — implemented and wired end-to-end.
  - **Partial** — mechanism exists but not fully wired (e.g. `TransportPoint.TryUpgrade()` works but isn't connected to the purchase pipeline).
  - **Planned / Open** — designed only, or undecided.
- **Match confidence to depth.** If a "Built" system was only inferred from filenames or grep, say so rather than asserting it works. Note explicitly which systems were read line-by-line and which were only spot-checked.

## Status taxonomy

Use one consistent set across SPEC and Kanban:

- ✅ **Built** — implemented and working in code
- 🟡 **Partial** — core exists; incomplete or not fully wired
- 🔲 **Backlog** — designed, not started
- ⬜ **Open** — design decision unresolved

(🟦 **Up Next** is a Kanban *lane*, not a status — reserve it for the board.)

## Per-doc update discipline

- **GDD** — surgical only. Resolve open questions the code has answered (mark them resolved, keep them for design history rather than deleting). Reconcile genuine divergences (where the build settled something differently than written — e.g. customer archetypes + want-bubble vs. the old "generic budget" text). Leave intent, rationale, and content tables alone. Keep the "see GAME_SPEC.md for status" banner at the top.
- **GAME_SPEC.md** — full status refresh. Re-tag every feature against verified code. Keep the verification note (commit hash + date) and keep the snapshot summary's "biggest gap" honest.
- **KANBAN_BOARD.md** — per-lane tables, notes trimmed to a phrase (detail lives in the spec). Lanes ordered Up Next → Backlog → Done. `Effort` / `Priority` columns are the user's to set — flag them as placeholders, don't assert estimates as fact.
- **CLAUDE.md** — update only if architecture or conventions changed (new manager, new pattern, renamed key file).

## Drift report format

Before editing, output a short report:

```
## Doc Drift Report — <date>, verified @ <commit>
<doc> last updated <date>, N commits behind.

Wrong in docs:
- <doc>: claimed <X>; code shows <Y>
  (e.g. Kanban: XP/Rank-Up "Up Next" → actually built, backend + UI)

Newly built since last sync:
- <feature> (evidence: <file / symbol>)

Still absent (docs correct):
- <feature> (zero code matches)

Planned edits:
- <doc>: <change>
```

## Formats and templates

For the exact section layouts — spec feature blocks, Kanban table columns, the GDD banner, and header/footer stamps — read `references/doc-formats.md` before editing, so the docs stay structurally consistent.

## A note on triggers

This skill is the *reconciliation method*; it does not run itself. Run a drift-sync pass on demand — after a feature lands, before sharing project state, or on a cadence. A cheap optional nudge: a git hook or CI check that simply *flags* drift (e.g. warns if `KANBAN_BOARD.md` hasn't changed in N commits, or if HEAD is well past a doc's last-updated). The hook only reminds; this skill does the judgement-based work.

> **Git LFS trap:** if the repo uses LFS (check `.gitattributes` for `filter=lfs`), any custom `pre-push` hook must keep `git lfs pre-push "$@"` as its last line — overwriting the hook without it clobbers LFS's own uploader and every push is rejected for missing LFS objects.
