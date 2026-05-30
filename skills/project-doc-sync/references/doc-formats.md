# Doc Formats & Templates

Concrete layouts for each doc, so reconciliation keeps them structurally consistent. Copy these shapes; fill with verified content.

## Status tags (shared by SPEC and Kanban)

`✅ Built` · `🟡 Partial` · `🔲 Backlog` · `⬜ Open`. `🟦 Up Next` is a Kanban lane only.

---

## GAME_SPEC.md

House style mirrors the team's spec docs: a grid-cards header, then prose sections, then a status table — no cost columns.

**Header (top of file):**
```
# Idle Merchant Guild — Game Specification

<div class="grid cards" markdown>

- :material-file-document-outline: **Version** X.Y
- :material-calendar: **Date** <date>
- :material-gamepad-variant: **Engine** Unity 2D — URP, UI Toolkit, SpriteLibrary
- :material-shape: **Genre** Idle / Management
- :material-monitor: **Platform** PC (primary)
- :material-progress-wrench: **State** <one line>

</div>
```

**Background paragraph** — state that statuses were verified against `Assets/Scripts/` + git (give the commit), and that the Kanban is a cross-reference, not the source.

**Feature block** — one `###` per system, status tag in the heading, then a short prose description and scoped bullets:
```
### <System> ✅  *(<sub-feature> 🟡)*

<1–3 sentence description, naming the real classes.>

- **Built:** <what works>
- 🟡 **Partial — <thing>:** <what exists vs what's missing>
- 🔲 **Backlog — <thing>:** <designed, not started>
```

**Status summary** — a single table: `| System | Status | Notes |`.

**Verification note + snapshot** — close with a blockquote noting what was verified (commit + date) and where the Kanban was wrong, then a short "single biggest gap" summary. Footer: `*Snapshot reconciled against GDD vX.Y and the codebase at commit <hash> (<date>).*`

---

## KANBAN_BOARD.md

**Header:**
```
# Idle Merchant Guild — Kanban Board
**Last Updated:** <date> · **Verified against:** commit `<hash>` (<date>), read from `Assets/Scripts/`.

> Reflects actual codebase state. For the full feature-by-feature breakdown, see `GAME_SPEC.md`.
```

**Lanes, in this order:** `## 🔲 Up Next`, `## 📋 Backlog`, `## ✅ Done` (Done is the archive — keep it last).

**Tables — notes trimmed to a phrase:**
- Done: `| Area | Item | Note |` (group rows by Area: Core, Entities, Dungeon, Shop, Crafting, Traits, Skills, Progression, Hiring, UI, Data).
- Up Next: `| Item | Effort | Note |`
- Backlog: `| Item | Priority | Note |`

Add an italic caption under Up Next/Backlog noting `Effort`/`Priority` are placeholders for the user to set.

---

## GDD.md

It is a **design-intent** doc; touch it lightly.

**Banner (under the version block):**
```
> **This document describes design intent.** For current implementation status (what is built vs. planned), see `GAME_SPEC.md` in the project root — do not treat this GDD as a status source.
```

**Resolving an open question** (in §17) — don't delete it; mark it:
```
**<Question>** — ✅ *Resolved (implemented).* <what the code settled, naming the symbol>. Retained here for design-history context.
```

**Stamps:** bump `**Version:**` + `**Date:**` at the top and the `*End of Game Design Document — Idle Merchant Guild vX.Y*` footer together so they never disagree (they had drifted to v2.2 vs v2.0 before).

---

## CLAUDE.md

Plain architecture notes. Update the relevant section or the "Key Files Quick Reference" table only when code structure actually changes. No status tags here.
