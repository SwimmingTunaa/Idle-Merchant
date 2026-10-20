# Backlog

Ordered roughly by intended sequence. Pull from the top.

## Next up

- [ ] **Save system + offline progress** — in design review, see [SAVE_DESIGN.md](SAVE_DESIGN.md)
- [ ] **Audio pass** — click thock, kill crack, coin clink/shower jingle, purchase ka-ching, hire stamp, claim fanfare, ambient tavern/forest loop. Asset-wiring job (Kenney/itch packs), pooled AudioSource helper, volume settings (needs save system for persistence).
- [ ] **First-five-minutes sequencing** — order 1★ milestones as an implicit tutorial (kill mobs → collect loot → first sale → first hire), tune rewards so each claim funds the next step. Mostly milestone data tweaks.

## Crafting (in flight — see mocks)

- [ ] **Batch 2 — engine**: output caps, unified keep-N floor (crafting + sales), priority, stations-as-slots (Forging / Smithy / Arcane Workshop ↔ the three chains), save all of it
- [ ] **Batch 3a — book page**: icon tiles on torn sheets grouped by layer, frame-carried status (Painter2D progress sweep / red / gold / grey), Craft→Crafting buttons, pixel stock badge, right-page recipe detail
- [ ] **Batch 3b — world workshops**: inactive prop prefabs behind the guild activated on station purchase (ProgressionActivator), click → themed workshop panel owning slots + slot purchases
- [ ] **Book close button**: pixel slip "X" hanging off the book's top-right corner (BookPanel), per the crafting mock

## Shop depth

- [ ] **Customer archetypes** — whale / bulk buyer / window-shopper as CustomerDef data + distinct palettes + spawn weights. Makes walkout signals richer for free.
- [ ] **Queue patience** — patience meter, angry storm-out; needs a counter-speed upgrade to exist as the player's answer first.
- [ ] **Physical shelves** — Shelf.cs is fully commented out; items displayed in-shop, customers walk to them. Big charm feature, own design conversation. Include: shopkeeper restocks shelves from the loot bin as a background behavior (the "cashier carries items" idea — off the sale's critical path so it can't nerf throughput).

## Data & content fixes

- [ ] **Crafting Station / Potion Station defs** — copy-pasted name/description/type from Forging Station (Crafting Station even displays "Forging Station"). Needs intended names/types decided.
- [ ] **Icons for guild upgrade defs** — all three have none; weapon/materials packs have candidates.
- [ ] **Elevator/Teleporter transport upgrades** — don't exist; ElevatorNow + WizardsOnPayroll milestones were re-typed to porter hires as placeholders, restore to UpgradePurchased when built.
- [ ] **Orphaned Guild UXMLs** — GuildUpgradePanel.uxml, Guild_Left.uxml, Guild_Right.uxml look unreferenced; confirm and delete.
- [ ] **"3 Stars" milestone filenames** — named Milestone_2Star_* (data is correct, names lie).
- [ ] **Mob "Hit" flinch** — MobAgent fires the Hit trigger but mob controllers lack the param (console warning per non-lethal hit); add flinch states or guard the call.
- [ ] **First-play NREs** — Stats.cs:196 / Health.cs:91 throw on a fresh play session (seen repeatedly by agents; also CharacterAppearanceManager.Start entityDef null). Root-cause the init order.

## Older backlog

- [ ] Click crits + treasure mob (click excitement spikes)
- [ ] Early micro-milestones (sub-minute dopamine in the first session)
- [ ] Rank badge art 2–5, promote-indicator arrow sprite
- [ ] Rebake BrightLinger font with ↑ glyph (lvl-up float says "lvl +")
- [ ] Customer-specific anim clips (browse lean, pay gesture, carry walk, upset slump) — animator states already wired and waiting
- [ ] Porter bag-carry sprite rework (then swap the overhead icon stack for in-bag peek)
- [ ] Book milestone list: sort claimable-first like the HUD tracker
- [ ] Doc sync: milestone claim system, tracker, guild restyle, save system once built

## Someday / deliberate

- [ ] **Prestige** ("Guild Charter renewal") — once the content ceiling is real and the economy has been observed under a save system
- [ ] Offline knobs: upgradeable cap/efficiency, offline events
- [ ] Crafting stations working offline
