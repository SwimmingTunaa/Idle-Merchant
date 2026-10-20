# Backlog

Ordered roughly by intended sequence. Pull from the top.

## Next up

- [x] **Save system + offline progress** — built, see [SAVE_DESIGN.md](SAVE_DESIGN.md)
- [x] **Audio pass (first)** — 10 synthesized SFX (Tools/gen_sfx.py), SfxManager pool, Master/SFX/Music volumes saved.
- [ ] **Audio follow-ups** — ambient tavern/forest loop + music (Music volume slot is empty; AudioMixer when ducking lands), in-game volume sliders (debugger-only today), sounds for promote / loot pickup / adventurer kills.
- [x] **First-five-minutes sequencing** — 1★ tier re-sequenced as an implicit tutorial, star at ~3 min.

## Crafting (in flight — see mocks)

- [x] **Batch 2 — engine**: output caps, unified keep-N floor (crafting + sales), priority, stations-as-slots (Forging / Smithy / Arcane Workshop ↔ the three chains), save all of it
- [x] **Batch 3a — book page**: icon tiles on torn sheets grouped by layer, frame-carried status (Painter2D progress sweep / red / gold / grey), Craft→Crafting buttons, pixel stock badge, right-page recipe detail
- [ ] **Batch 3b — world workshops**: inactive prop prefabs behind the guild activated on station purchase (ProgressionActivator), click → themed workshop panel owning slots + slot purchases
- [ ] **Book close button**: pixel slip "X" hanging off the book's top-right corner (BookPanel), per the crafting mock

## Shop depth

- [ ] **Customer archetypes** — buying archetypes done (Commoner / Adventurer 2★ / Noble 3★ budgets + preferred-category fallback). Still open: whale / bulk buyer / window-shopper, and distinct art/palettes for Adventurer + Noble customers (customers don't use the palette system).
- [ ] **Queue patience** — patience meter, angry storm-out; needs a counter-speed upgrade to exist as the player's answer first.
- [ ] **Physical shelves** — Shelf.cs is fully commented out; items displayed in-shop, customers walk to them. Big charm feature, own design conversation. Include: shopkeeper restocks shelves from the loot bin as a background behavior (the "cashier carries items" idea — off the sale's critical path so it can't nerf throughput).

## Data & content fixes

- [x] **Crafting Station / Potion Station defs** — now Arcane Workshop / Smithy with their own upgrade types (asset names kept as save keys).
- [ ] **Icons for guild upgrade defs** — all three have none; weapon/materials packs have candidates.
- [ ] **Elevator/Teleporter transport upgrades** — don't exist; ElevatorNow + WizardsOnPayroll milestones were re-typed to porter hires as placeholders, restore to UpgradePurchased when built.
- [ ] **Orphaned Guild UXMLs** — GuildUpgradePanel.uxml, Guild_Left.uxml, Guild_Right.uxml look unreferenced; confirm and delete.
- [ ] **"3 Stars" milestone filenames** — named Milestone_2Star_* (data is correct, names lie).
- [ ] **Mob "Hit" flinch** — MobAgent fires the Hit trigger but mob controllers lack the param (console warning per non-lethal hit); add flinch states or guard the call.
- [x] **Stray empty save write** — root cause: `SaveManager.OnApplicationPause(true)` saved, and the editor raises that pause whenever the Unity window loses focus with run-in-background off, so a save landed the moment focus left (reproduced 2026-10-20: a 16 s-old save written while the editor was unfocused). Pause-saves are now `#if !UNITY_EDITOR`; autosave + quit still save in the editor, so agents still back up and suspend.
- [x] **Inventory "reserve" stepper** now sets the keep-N floor but still said "reserve" — relabelled KEEP (batch 3a). Contracts hand-ins deliberately ignore floors (explicit player action).
- [ ] **First-play NREs** — Stats.cs:196 / Health.cs:91 throw on a fresh play session (seen repeatedly by agents; also CharacterAppearanceManager.Start entityDef null). Root-cause the init order.

## Older backlog

- [ ] Click crits + treasure mob (click excitement spikes)
- [ ] Early micro-milestones (sub-minute dopamine in the first session)
- [ ] Rank badge art 2–5; wire `Sprites/Icons/up-arrow.png` into `PromoteIndicator.arrowSprite` (still the generated arrow)
- [x] Level-up float arrow — TMP sprite asset `UpArrow` instead of a font rebake
- [ ] Customer-specific anim clips (browse lean, pay gesture, carry walk, upset slump) — animator states already wired and waiting
- [ ] Porter bag-carry sprite rework (then swap the overhead icon stack for in-bag peek)
- [ ] Book milestone list: sort claimable-first like the HUD tracker
- [x] Doc sync through crafting batch 3a (2026-10-20)
- [ ] Refresh `GAME_SPEC.md` + `KANBAN_BOARD.md` (parent folder) — last verified June 2026

## Equipment (design next, after crafting)

- [ ] **Equipment loop design doc** — two slots (weapon/armor) to start, auto-assign armory pool with manual override later, stat modifiers via StatsMediator, gear outgrown via tiers
- [ ] **Material-tier recipe variants** — one recipe family with a tier-selectable material slot (Iron Sword → Obsidian Sword: better stats, higher value, uses the eight material tints already in the weapon/armor packs). This is how the game does "choose your ingredient" — deliberately NOT AtS-style any-of substitutes (our materials are deterministic per layer, so substitutes solve a problem we don't have). Revisit any-of only for tier-1 common slots if surplus still feels unabsorbed after contracts phase 2.

## Someday / deliberate

- [ ] **Prestige** ("Guild Charter renewal") — once the content ceiling is real and the economy has been observed under a save system
- [ ] Offline knobs: upgradeable cap/efficiency, offline events
- [ ] Crafting stations working offline
