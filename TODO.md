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
- [x] **Batch 3b — world workshops + slot pinning** (spec agreed 2026-10-21; mock v31 workshop panel) — built 2026-10-21; props are scene objects under `Guild/Shop/Workshops` driven by `WorkshopProp` (not `ProgressionActivator`):
  - **Props**: one prefab per station in the guild's back-corner bay (interior back wall, between the posts under the window). Cold frame while unowned, lit 5-frame loop (fire + smoke, ~6 fps) once bought; activated by `ProgressionActivator` on `OnUpgradePurchased`. Sprites at 8 PPU: `forging_station` 54×72 ×6 frames + `anvil` 22×14 (scratchpad `forge/out/`, script `draw_forge2.py`); Smithy + Arcane Workshop to follow in the same family. Clicking the prop opens its panel.
  - **Model change — a recipe runs iff it is pinned in a station slot.** Jobs key by slot, not recipe; the same recipe may fill several slots (parallel crafting; caps count stock + jobs in flight). Priority is removed (slots are the priority); caps + keep floors unchanged. `IsRecipeEnabled` ≡ "has a slot".
  - **Book Craft stamp** = put the recipe in the first free slot of its station; if the station is full the stamp doesn't land, the plank shakes and reads "<Station> full". Dither-off clears its slot. The time line shows "<Station> · slot N" / "not in a slot". Priority pips gone.
  - **Workshop panel** (stone wall in the sprite's palette, timber beam, lit station sprite + side prop as hero, pixel-slip close): a row of iron-plate slots + "+ Slot <cost>" button. Click a slot → themed dropdown "Assign recipe": the station's recipes with icon in status frame · name · state ("also in slot N" when stacked), plus "Clear slot". Empty slots show "+" / "Empty slot". No recipe list, no subtitle.
  - **Save**: crafting chunk becomes `slots:{station:[recipeName|null,...]}` + jobs `[{station, slot, recipe, remaining}]`; migrate old `enabled` recipes into free slots on load.
  - **Guild upgrade card icons**: assigned on all three station defs.
- [ ] **Book close button**: pixel slip "X" hanging off the book's top-right corner (BookPanel), per the crafting mock

## Adventurer classes

- [x] **Classes phase 1** — lines, 17 defs + catalog, tier-gated odds, premium price, path picker, named promotions, save + free-pick migration (see CLASSES_DESIGN.md "Phase 1 as built")
- [x] **Classes phase 2 (feel)** — signature mechanics: Warrior cleave, Rogue crits + post-kill dash, Mage % max-HP bonus (aura removed), Guardian aggro + damage reduction
- [ ] **Class weapon/outfit sprites** — Tuan is drawing these by hand; all tiers keep the Novice look as placeholders until then (don't generate them)
- [ ] **Playtest the odds / unlock boost / power-curve numbers** (CLASSES_DESIGN "Open" 1)
- [ ] **Healer + Monk lines** (healer needs targeting AI)

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
- [x] **Mob "Hit" flinch** — `Mob Controller` has no `Hit` param; `MobAgent` now caches `hasHitParam` in Init and skips the trigger (no flinch states added).
- [x] **Stray empty save write** — root cause: `SaveManager.OnApplicationPause(true)` saved, and the editor raises that pause whenever the Unity window loses focus with run-in-background off, so a save landed the moment focus left (reproduced 2026-10-20: a 16 s-old save written while the editor was unfocused). Pause-saves are now `#if !UNITY_EDITOR`; autosave + quit still save in the editor, so agents still back up and suspend.
- [x] **Inventory "reserve" stepper** now sets the keep-N floor but still said "reserve" — relabelled KEEP (batch 3a). Contracts hand-ins deliberately ignore floors (explicit player action).
- [x] **First-play NREs** — not init order: Stats/Health NREs came from a mid-play domain reload ("Recompile And Continue Playing" pref) nulling non-serialized fields + singletons; `GameDebugger` now locks assembly reload during play. The `CharacterAppearanceManager.Start` NRE was the def-only `SpawnUnit(def)` path never assigning the appearance def; `EntityBase.Init` now sets it for every spawn.

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

## Equipment — rejected (2026-10-27)

Tuan's call: an equipment system adds too much complexity for an idle game. Crafting's purpose is to be a value multiplier on loot (raw → crafted sells for more), not to feed a loadout. Don't re-propose.

- [ ] **Material-tier recipe variants** — still worth it on its own as a sell-value ladder (Iron Sword → Obsidian Sword: higher value, uses the eight material tints already in the weapon/armor packs). Deliberately NOT AtS-style any-of substitutes.

## Someday / deliberate

- [ ] **Prestige** ("Guild Charter renewal") — once the content ceiling is real and the economy has been observed under a save system
- [ ] Offline knobs: upgradeable cap/efficiency, offline events
- [ ] Crafting stations working offline
