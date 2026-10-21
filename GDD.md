# Idle Merchant Guild — Game Design Document

**Version:** 2.6
**Date:** 2026-10-20
**Engine:** Unity 2D (URP, UI Toolkit, Spine/SpriteLibrary)
**Genre:** Idle / Management
**Platform:** PC (primary)

> **This document describes design intent.** For current implementation status (what is built vs. planned), see `GAME_SPEC.md` in the project root — do not treat this GDD as a status source.

---

## Table of Contents

1. [Game Overview](#1-game-overview)
2. [Design Pillars](#2-design-pillars)
3. [Core Game Loop](#3-core-game-loop)
4. [Entities](#4-entities)
5. [Dungeon & Loot System](#5-dungeon--loot-system)
6. [Shop System](#6-shop-system)
7. [Hiring System](#7-hiring-system)
8. [Progression System](#8-progression-system)
9. [Upgrade System](#9-upgrade-system)
10. [Economy](#10-economy)
11. [Traits & Skills](#11-traits--skills)
12. [Stats System](#12-stats-system)
13. [Crafting System](#13-crafting-system)
14. [UI & Presentation](#14-ui--presentation)
15. [Audio & Feel](#15-audio--feel)
16. [Content Reference](#16-content-reference)
17. [Open Design Questions](#17-open-design-questions)
18. [Technical Architecture Notes](#18-technical-architecture-notes)

---

## 1. Game Overview

**Elevator Pitch:**
You run a merchant guild. Hire adventurers to clear dungeon layers, porters to haul the loot up, and watch customers flood your shop. Reinvest gold to go deeper, sell better goods, and build the most powerful guild on the continent.

**Core Fantasy:**
The player is the behind-the-scenes mastermind of a growing operation. They're not swinging a sword — they're making things happen. The satisfaction comes from watching a well-oiled machine: adventurers clearing rooms, porters jogging back and forth, customers lining up at the counter, gold ticking upward.

**Target Audience:**
Players who enjoy idle/clicker progression games (Melvor Idle, Shop Titans, Recettear, Moonlighter) and light management sims. Appeals to players who like watching systems interact and optimising resource flow.

**Design Philosophy:**
- **Cozy, not grindy** — progress always feels reachable; no brick walls
- **Strategic, not overwhelming** — meaningful choices at each milestone, but the game never demands constant attention
- **Visible, not opaque** — the state of every system should be readable from the main screen

**Session Length:**
Designed for both long passive sessions (leave it running) and active short sessions (hire units, unlock upgrades, check milestones). The game rewards both playstyles.

---

## 2. Design Pillars

### 2.1 Automation Over Action
The player sets things up; entities handle themselves. There is no direct combat — the adventurers fight autonomously. The joy is in watching the machine you've built operate and then tuning it.

### 2.2 Layered Complexity
Early play is simple (one layer, two unit types). Each star unlocks a new layer, a new challenge, and a new tool. Complexity is introduced gradually so it never feels overwhelming.

### 2.3 Personality Over Numbers
Units are procedurally generated with names, traits, and epithets. Hiring someone named "Gorn the Relentless" with the Berserker trait feels different from hiring "Mira the Swift" with the Nimble trait. The guild should feel populated with characters, not stats.

### 2.4 Readable at a Glance
The game's state should be immediately legible from the main screen. Gold going up? Good. Porter standing around? Something's wrong. The visual pipeline — combat, loot pickup, porter travel, counter sale — communicates health without needing a dashboard.

### 2.5 Satisfying Progression Hooks
Stars, milestones, upgrades, new layers, promotions — there should always be a clear next goal within reach. Milestones bridge the gap between stars. Upgrades and promotions provide immediate, visible payoff.

---

## 3. Core Game Loop

### 3.1 Macro Loop

```
Click mobs/loot → Earn Gold → Hire Units / Buy Upgrades → Unlock Stars → Unlock Layers
                                         ↑
                     Adventurers gain XP → Promote to next Rank
                     Upgrade mouse click (damage, AOE, loot radius)
```

### 3.2 Micro Loop (Per Session)

```
1. Player clicks mobs to damage them (early game — before enough adventurers)
2. Adventurers join in and fight mobs autonomously → mobs drop loot
3. Player clicks loot to collect it instantly (early game — before a porter)
4. Porters collect remaining loot → transport it to the shop
5. Shop inventory fills → customers queue at counter
6. Counter sells items → gold added to Inventory
7. Player spends gold on new hires / mouse upgrades / guild upgrades
8. Adventurers accumulate XP → player promotes to next rank (Rank 1 → 2 → … → 5)
9. Milestones tick → Stars earned → New layers & upgrades unlocked
```

### 3.3 Flow Diagram

```
[Player Input]
  Mouse click → damage mobs / collect loot (early game; bonus later)
  Mouse upgrades → click damage, AOE damage, loot AOE, auto click/collect

[Dungeon Layer]
  Adventurers ←fight→ Mobs
      ↓ gain XP              ↓ drop loot
  [Rank Promotion]     [Porter Pipeline]
  Rank 1 → … → 5  Porters scan → pick up loot → travel to shop
                                                       ↓
[Shop]
  Inventory fills → Shelf stocks → Counter serves Customers
                                                   ↓ gold
[Economy]
  Gold → hire units / mouse upgrades / guild upgrades
       ↓
[Progression]
  Milestones → Stars → New Layers / Upgrades unlocked
```

### 3.4 Idle Behaviour
When the player is away but the game is running, all loops continue (a closed game earns offline instead — see 10.5):
- Adventurers fight, die, revive, and accumulate XP automatically
- Porters collect and deposit loot automatically
- Customers arrive, buy, and leave automatically
- Gold accumulates
- Milestones continue ticking

The click mechanics are a player-agency layer on top of the idle loop — powerful early, optional later.

### 3.5 Player Experience Arc

**Phase 1 — Survival (0–15 min)**
The player is clicking goblins to kill them and clicking loot to collect it. Adventurers and porters barely exist yet. The clicking loop teaches the pipeline: mobs die → loot drops → shop fills → gold appears. Milestones are small and fast. The first star feels earned and the first hire feels like genuine relief.

**Phase 2 — Automation (15–60 min)**
Adventurers are doing most of the fighting. Porters are running. Clicking is now a meaningful bonus rather than a necessity — but mouse upgrades (AOE damage, loot radius) make clicking feel more powerful. First rank promotions happen. Second and third stars unlock new layers. Forging Station comes online.

**Phase 3 — Multi-Layer (1–3 hrs)**
Multiple layers active simultaneously. Crafting is producing Advanced items. Higher-rank adventurers are noticeably stronger. The player is optimising porter throughput with Elevators. The click system is fully upgraded and used tactically (click-bombing a tough mob, vacuuming a loot pile). The pipeline feels like a machine.

**Phase 4 — Mastery (3+ hrs)**
All layers unlocked, 5★ achieved. The player is deep in optimisation: trait hunting in the hire pool, fine-tuning which layers get Teleporters, crafting Premium goods, maximising customer throughput. Clicking is a fun bonus. The game is a puzzle to solve at maximum efficiency.

---

## 4. Entities

All entities use a staggered 10Hz state machine (`EntityStateMachine`) — no per-frame AI updates. This keeps performance stable across large entity counts.

---

### 4.1 Adventurer (Novice)

**Role:** Combat unit. Enters dungeon layers, fights mobs, earns XP, dies and revives. At Level 5, becomes eligible for promotion.

**State Machine:**
```
Idle → Wander → Seek (target mob) → Attack → Hit (stagger) → Dead → [Revive] → Idle
```

**Core Behaviours:**
- Wanders the dungeon layer when no target is in range
- Scans for mobs within `ScanRange`; enters Seek state toward nearest mob
- Attacks within `AttackRange`; fires cooldown via `AttackInterval`
- On taking damage: enters Hit (stagger) state briefly
- On death: plays death animation, waits `ReviveDelay` (default 5s), revives at full HP
- If target escapes beyond `ChaseBreakRange`, returns to Seek/Wander
- Gains XP on each mob kill; XP tracked on the entity

**Stats (modifiable at runtime):**

| Stat | Description |
|---|---|
| MoveSpeed | Walk/chase speed |
| AttackDamage | Damage per hit |
| AttackInterval | Time between attacks |
| ScanRange | Radius for spotting mobs |
| AttackRange | Melee strike distance |
| ChaseBreakRange | Max chase distance before giving up |
| StopDistance | Engagement standoff distance |

**Visuals:**
Modular sprite assembly — body, shirt, pants, hair (back + top), weapon (back + front) — randomised at hire time from `EntityDef` sprite libraries. Colour palettes for skin, hair, clothing also randomised. Damage flash (red shader blend) on hit.

**Design Intent:**
Adventurers are the engine of loot production. More adventurers = faster mob clear = more loot = more gold. XP gain creates a secondary investment — each adventurer in the field is building toward a rank-up, which adds stakes to every hire and reward to every session.

---

### 4.2 Adventurer Promotion (Rank-Up)

Adventurers progress on two nested tracks: **5 ranks** — Wood → Bronze → Iron → Silver → Gold — each holding **5 levels** (shown as roman numerals I–V), for 25 levels total. Killing mobs earns XP, which **auto-levels** the adventurer within their rank (Wood I → Wood II → …). At a rank's top level (V), the player can **promote** to the next rank, which **costs gold** and resets the adventurer to Level I of the higher tier (e.g. Wood V → Bronze I). Auto-leveling is automatic; promotion is a deliberate, player-initiated, gold-gated choice.

**Level & Rank Effects:**
Each **level-up** applies a small permanent stat bump (attack damage, attack speed). Each **promote** (rank-up) steps attack speed and swaps the adventurer to the **next tier of their class** — the tier def carries the damage / HP multipliers, range and skills (see Classes below). ✅ *Reconciled (classes phase 1): the old flat +20% damage / +25% HP per rank is gone; class tiers carry the power curve.*

**Classes (phase 1, implemented — design in [CLASSES_DESIGN.md](CLASSES_DESIGN.md)):**
Wood is the classless **Novice**. The first promotion (Wood V → Bronze) opens a **"Choose a path"** picker on the Roster's right page — four cards (job icon, name, role line, DMG/HP/RNG arrows vs that tier's Warrior); clicking a card spends the gold and makes the adventurer that line's Bronze class. Later promotions advance along the line and the button names the step ("Promote » Knight 3,750" — Ithaca has no →).

| Line | Bronze | Iron | Silver | Gold | Role |
|---|---|---|---|---|---|
| Warrior | Warrior | Knight | Paladin | Champion | balanced melee |
| Guardian | Guardian | Defender | Bulwark | Juggernaut | high HP, low damage |
| Rogue | Rogue | Assassin | Shadowblade | Nightstalker | 1.4× attack speed, fragile |
| Mage | Mage | Sorcerer | Wizard | Archmage | splash aura (Mage Splash skill), fragile |

Damage / HP multipliers on the Novice base (50 / 200) per tier are the CLASSES_DESIGN power-curve table, e.g. Iron Knight 4.5× / 2.8× — target: a rank-N unit kills an average layer-N mob in ~4 hits. A weak unit in a deep layer is allowed; the Roster detail and the hire card say "Below this layer's rank". The Roster badge shows the class icon + name. Phase 1 keeps the Novice sprites; class weapons/outfits, archer projectiles and splash VFX are phase 2.

A Novice already above Wood (pre-classes saves, Recruitment Favor recruits) gets a **free one-time path pick**: the picker opens the first time their Roster detail opens, and the promote gem/indicator/badge light up until they pick.

**Design Notes:**
- Ranks exist at both hire time (higher-rank candidates cost more gold) and as an in-field upgrade. Levelling within a rank is free via XP; **promoting between ranks costs gold** — a deliberate gold sink and decision point (invest in this adventurer now, or save). ✅ *Reconciled: promotion was originally "free via XP"; the build settled on a gold cost.*
- The tension: a Rank 1 adventurer you've levelled to Rank 3 through play may be more valuable than a freshly hired Rank 3 because of their traits
- Higher-rank adventurers should be visually distinct (e.g. better equipment sprites) so rank is readable on screen without opening a panel
- XP progress should be visible on the adventurer's card in the roster UI

**XP & Leveling:**
- XP earned per mob kill, tracked on the entity
- XP per kill scales with layer depth — deeper mobs are worth more XP, incentivising pushing adventurers into harder layers
- **Curve: exponential per level** — `XPForLevel(n) = baseXP × xpGrowth^(n-1)` over the global level (1–25). Defaults `baseXP 40`, `xpGrowth 1.22` (tunable in `AdventurerDef`): Lv I→II ≈ 40 XP, ramping to ≈ 3,900 XP for the final level-up. ✅ *Reconciled: replaces the old per-rank gate table (100/300/900/2700, 3×/rank) — XP is now a single per-level curve, and ranks advance via a gold-gated promote rather than XP.*

Early levels arrive fast (frequent small wins); the rank gate at Level V is the prestige milestone. Deeper layers give more XP per kill, so adventurers assigned to them level faster — a further incentive to push layers.

- Levels cap at V within a rank; ranks cap at Gold (`maxRank` 5) — Gold V is the ceiling, no further gates after that

**In-world feedback (implemented):**
- Each level-up floats a green **"lvl ↑"** above the adventurer (via the damage-number system; the arrow is a pixel sprite since the font has no ↑ glyph)
- When an adventurer reaches a rank's top level, a **persistent promote indicator** — the next rank's badge plus a green up-arrow — bobs above their head until promoted (`PromoteIndicator`). This is the standing prompt to open the Roster and spend the gold; it disappears on promotion. (The indicator's arrow is still the generated placeholder; the new pixel up-arrow is used in the UI and the level-up float.)

**Roster promote affordances (implemented):**
- A promote-ready adventurer's slot gem turns gold and shows the pixel up-arrow; it **pulses only while the promotion is affordable** (motion = actionable).
- The **Units bookmark** carries a badge with the number of promote-ready adventurers and wiggles while any of those promotions is affordable.
- The Promote button reads "Promote <cost>" with the copper coin, like the other buy buttons.
- Layer rows in the Roster wrap at five slots.
- Rank badge art exists for Rank 1 only — ranks 2–5 badges are pending art (the indicator clamps to the last available badge)

---

### 4.3 Mob

**Role:** Obstacle and loot source. Populates dungeon layers. Drops items on death.

**State Machine (Passive):**
```
Wander → Idle → Damaged (stun 0.2s) → Wander
```

**State Machine (Aggressive):**
```
Wander → Idle → Damaged → Seek → Attack → Wander
```

**Core Behaviours:**
- Wanders and idles on its layer
- On taking damage: short stun, then resumes (or retaliates if aggressive)
- On death: drops loot from its loot table (`ItemDef` list with per-item `chance`)
- Multiple adventurers can target the same mob (configurable `maxSimultaneousAttackers`, default 3)
- HP scales by layer (multiplier curve: e.g. 1× at Layer 1, 3× at Layer 10)

**Design Intent:**
Mobs are soft obstacles that create the resource pipeline. Tougher mobs on deeper layers incentivise hiring stronger adventurers (or promoted Guards). Passive vs. aggressive configuration allows content designers to vary the feel of each layer.

---

### 4.4 Porter

**Role:** Transport unit. Collects loot dropped in the dungeon and carries it to the shop.

**State Machine:**
```
Idle → Wander → Seek (loot) → PickUp → Return → Travel (climb) → Deposit → ReturnToLayer
```

**Core Behaviours:**
- Scans for nearby unclaimed loot using the spatial grid (`LootManager`)
- Reserves loot via `LootManager.RequestLoot()` — prevents other porters claiming the same drop
- Picks up items until carry capacity is reached (default 5 items)
- Travels to `TransportPoint`, plays climb animation during vertical transit
- Deposits carried loot to `Inventory`
- Returns to layer and repeats
- **Carry display (implemented):** up to 5 item icons stack above the porter's head, one per carried stack; on deposit they fly into the shop's loot pile (6.3)

**Stats:**

| Stat | Description |
|---|---|
| CarryCapacity | Max items carried per trip |
| PickupTime | Time to pick up each item (0.5s default) |
| DepositTime | Time to deposit each item (0.5s default) |
| MoveSpeed | Walk speed |
| ScanRange | Radius for detecting loot |

**Design Intent:**
Porters are the bottleneck between loot production and gold income. One porter per layer is the baseline; upgrades (Elevator, Teleporter) dramatically reduce round-trip time. Carrying capacity upgrades also help. The porter running back and forth is a key piece of the visual machine — players should clearly see when a porter is struggling.

---

### 4.5 Customer

**Role:** Spends gold at the shop counter. Arrives, queues, buys items, leaves.

**State Machine:**
```
Idle → Wander → SeekingQueue → Queueing → Buying → Leaving → Exited
```

**Core Behaviours:**
- Arrives and wanders near the shop
- Seeks the queue when they decide to buy; gives up and leaves after 5s if they can't reach the queue end
- Joins queue when within 1m of the queue tail
- Queue position calculated dynamically (spacing tightens as queue grows: 0.08–0.35m gap)
- Each customer has an **archetype** — Commoner / Adventurer / Noble (`CustomerDef.customerArcheType`) — with a preferred item category (`CustomerDef.itemPreferance`), a budget range (`CustomerDef.budget`), and a per-visit batch quantity (`CustomerDef.batchRange`)
- On reaching the counter: resolves a **want** against current stock within budget (`SalesManager.TryPickDesiredForCustomer`) — the most expensive affordable item in the archetype's **preferred category first**, and only if that yields nothing, the most expensive affordable item from any other category; the desired item is shown via a want-bubble above the customer
- Buys a quantity within the batch range that budget and stock allow
- **Archetypes as built:** Commoner (Villager, Potion Lady — Basic, 200–600g, from the start), Adventurer (Advanced, 600–3,000g, from 2★), Noble (Premium, 6,000–10,000g, from 3★). Star gates live on `CustomerSpawner`; Adventurer/Noble customers have no distinct art yet
- Leaves after purchase, despawns on exit. The bought item **relays** from the loot pile to the counter, then homes onto the walking buyer; the overhead carry icon and Carry Walk start only when it lands, so the item is never in two places
- **Intent bubbles (implemented):** "?" while browsing, a 0.6s "!" flash on picking a want, then the item + qty while queueing; an empty-handed walkout shows **why** — crossed crate (nothing matching in stock) or crossed coin (over budget) — including counter rejections
- **Animation (implemented):** Browse at wander pauses, Pay at the counter, Carry Walk when leaving with a purchase, Upset Walk on empty-handed walkouts

**Design Intent:**
Customers are the gold spigot. Their throughput is limited by queue capacity, item availability, and their budget. Expensive items = more gold per transaction. Queue length is a visible signal of shop health.

---

### 4.6 Party Customer

**Role:** Rare, high-value customer event. A group of 2–4 characters arrives together, buys multiple items through a single leader, then leaves as a group.

**State Machine (Leader):**
```
Idle → SeekingQueue → Queueing → Buying (multiple purchases) → Leaving → Exited
```

**State Machine (Party Members):**
```
Idle → Wander (near shop) → Leaving → Exited
```

**Core Behaviours:**
- Spawned on a separate cooldown timer, independent of the regular customer pool
- Spawn is **weighted** by inventory quality (higher-tier items = higher chance) and active layer count — parties won't appear if there's nothing worth buying
- Guild Stars gate party size and item tier: early stars = 2-person groups wanting Basic gear; late stars = 4-person elite squads wanting Advanced/Premium crafted goods
- **Only the leader** joins the queue and interacts with the counter. The leader cycles through a shopping list of multiple item purchases in sequence
- Party members wander nearby while the leader shops
- When the leader's shopping list is exhausted, the whole group leaves together
- Maximum 1 party present at a time (potentially 2 at 5★ — TBD)
- `CounterService` and queue logic are unchanged — the leader is just a customer with a multi-item purchase list

**Design Intent:**
Parties are punctuation marks in the shop loop. Regular customers are the steady income; a party arrival is an event — a burst of sales that rewards having good stock. They create anticipation and make inventory management feel meaningful. The visual of a group wandering outside while their leader queues is a strong readable signal that something special is happening.

---

### 4.7 Player Mouse (Click Mechanics)

**Role:** The player's direct agency in the dungeon. Essential in early game; a powerful bonus in late game.

**Click — Mob:**
- Clicking a mob deals damage to it directly
- Early game this is the primary kill method before adventurers are strong enough
- Late game it's a tactical tool: burst-kill a priority mob, accelerate a tough fight on a deep layer
- **Gold feedback chain (implemented):** each damaging click floats a gold number + coin above the mob; when the float ends the coin flies to the gold counter (same VFX as customer purchases), and the gold is **banked on arrival** — the counter ticks up exactly when the coin lands, never before

**Click — Friendly units:**
- Adventurers, porters and customers are **transparent to the left-click** — a hero standing over a mob never eats the damage click
- **Hovering** an adventurer shows a yellow outline (interactability hint)
- **Right-clicking** an adventurer opens the Roster focused on them — a separate input channel from the left-click clicker

**Click — Loot:**
- Clicking a loot drop instantly collects it into `Inventory`
- Early game this bypasses the porter entirely, enabling the shop to function before a porter is hired
- Late game it can clear a backlogged dungeon floor faster than waiting for the porter

**Mouse Upgrades (implemented):**
Bought from the **Cursor page** of the Book (top-nav cursor board opens it; the board shows a badge while any upgrade is affordable). All ladders are repeatable with exponential costs, tunable on `ClickerManager`. ✅ *Reconciled: replaces the old guild-upgrade-style I/II/III one-shots.*

| Upgrade | Ladder | Cost | Gate |
|---|---|---|---|
| **Click Damage** | +10% of base per level, uncapped | 100 × 1.15ⁿ | — |
| **Pickup Radius** | 0.3 wu base, +0.2 wu/level, max 5 | 250 × 1.5ⁿ | — |
| **Damage AOE** | 0.8 wu at lvl 1, +0.3 wu/level, max 3 (lvl 0 = single target) | 1000 × 2ⁿ | 2★ |
| **Auto Click** | clicks a random mob every 3.0s at lvl 1, −0.1s/level, max 21 (1.0s floor); full click pipeline incl. AOE | 10000 × 1.35ⁿ | 3★ |
| **Auto Collect** | vacuums a random loot drop (with pickup radius) every 3.0s at lvl 1, −0.1s/level, max 21 (1.0s floor) | 20000 × 1.35ⁿ | 4★ |

**Click feedback (implemented):** every damaging click fires a pixel spark burst at the cursor plus a squash-and-stretch on the mob; the **kill blow** adds a crack-star flash, mob-coloured debris, and a 45 ms hit-stop (kills only, respects pause and the speed keys). Loot clicks show a **white ring collapsing from the effective pickup radius** to the click point (radius upgrades are visible on every click) with a spark pop per absorbed piece; AOE clicks show a **white-to-red ring expanding to the damage radius in sync with the shockwave**, so the visual edge is the damage edge. All effects are code-driven ParticleSystems (`ClickHitVFX`) — no spawned objects.

**Design Notes:**
- At game start the player *must* click to make progress — no adventurers, no porter, clicking is the only tool. This creates an active, hands-on opening that teaches the pipeline before automation takes over.
- As adventurers are hired and porters are added, clicking transitions from mandatory to optional-but-satisfying
- Pickup radius is particularly impactful on layers where mobs die in clusters and loot piles up
- `ClickerManager` in code handles input, damage delivery, loot collection, the auto click/collect ticks, and all five upgrade ladders; gold income from clicks is fractional (AddGoldFloat) and banks when the coin reaches the counter

---

## 5. Dungeon & Loot System

### 5.1 Dungeon Layers

There are 10 dungeon layers (1–10) organised into **5 themed zones** of 2 layers each. Each zone has its own visual identity, mob roster, and loot pool. The second layer of each zone shares the same mob types but is harder (more HP, more aggressive spawns).

```
Zone 1 — Abandoned Mines     (Layers 1–2)   Basic loot
Zone 2 — Deep Caverns        (Layers 3–4)   Basic / early Advanced loot
Zone 3 — Crystal Hollows     (Layers 5–6)   Advanced loot
Zone 4 — Cursed Crypts       (Layers 7–8)   Advanced / early Premium loot
Zone 5 — Infernal Depths     (Layers 9–10)  Premium loot
```

| Layer | Zone | Unlocked By | Mob Difficulty | Item Tier |
|---|---|---|---|---|
| 1 | Abandoned Mines | Default | 1× | Basic |
| 2 | Abandoned Mines | 1★ | ~1.3× | Basic |
| 3 | Deep Caverns | 2★ | ~1.6× | Basic / Advanced |
| 4 | Deep Caverns | 3★ | ~2.0× | Advanced |
| 5 | Crystal Hollows | 3★ | ~2.4× | Advanced |
| 6 | Crystal Hollows | 4★ | ~2.8× | Advanced |
| 7 | Cursed Crypts | 4★ | ~3.2× | Advanced / Premium |
| 8 | Cursed Crypts | 4★ | ~3.6× | Premium |
| 9 | Infernal Depths | 5★ | ~4.0× | Premium |
| 10 | Infernal Depths | 5★ | ~4.5× | Premium |

Each layer has its own set of spawners (mobs), available `ItemDef` entries in its loot table, and its own adventurers/porters assigned to it.

### 5.2 Loot Pipeline

1. **Mob dies** → `MobAgent` calls `DropLoot()`, spawning `Loot` objects at the mob's position
2. **Loot object** contains a `ResourceStack` (item type, quantity, gold value) and a `Collider2D` for porter detection
3. **Porter** scans nearby loot via `LootManager`'s spatial grid (O(1) query per cell)
4. **Reservation** — porter calls `RequestLoot()` on `LootManager`; only one porter can claim a loot object
5. **PickUp** — porter waits `PickupTime` at loot location; loot object moves visually to porter's head during pickup animation
6. **Transport** — porter carries loot up the `TransportPoint` (with climb animation)
7. **Deposit** — porter calls `Deposit()`, adding each `ResourceStack` to `Inventory`; `GameSignals.OnItemAdded` fires
8. **Return** — porter travels back to its layer and resumes scanning

### 5.3 Loot Drop Configuration

Each `MobDef` has a `loot` list of `ItemDef` references. Each `ItemDef` has:
- `chance` (0.0–1.0): probability this item drops on mob death
- `lootDropAmount` (Vector2Int): min/max quantity per drop

Multiple items can drop simultaneously. Drops are independent rolls per item in the table.

### 5.4 Spatial Grid Optimisation

`LootManager` maintains a per-layer spatial grid with 5m cells. Porter loot scans query only nearby cells rather than iterating all live loot objects. This keeps performance stable as loot density grows.

### 5.5 Mob Respawn

Mobs respawn continuously via `Spawner.cs`. Each layer has one or more spawners that operate on a budget (spawns per minute), capped by `maxAlive` per spawner. When a mob dies, a new one can spawn once the budget allows and the alive count is below the cap. There is no "all mobs cleared" downtime — the dungeon layer is always active.

This is critical to the idle loop: loot production is a continuous flow, not a wave-based burst.

### 5.6 Mob Reference

| Zone | Mob | HP | Behaviour | Primary Drop | Bonus Drop |
|---|---|---|---|---|---|
| Abandoned Mines | Giant Rat | 8 | Passive | Rat Fang (100%) | Glowcap Spore (15%) |
| Abandoned Mines | Blue Slime | 12 | Territorial | Slime Gel (100%) | Glowcap Spore (15%) |
| Abandoned Mines | Cave Bat | 10 | Passive | Bat Wing (100%) | Glowcap Spore (15%) |
| Deep Caverns | Stone Golem | 35 | Aggressive | Stone Chunk (100%) | Mineral Dust (15%) |
| Deep Caverns | Red Slime | 25 | Territorial | Molten Gel (100%) | Mineral Dust (15%) |
| Deep Caverns | Iron Golem | 50 | Aggressive | Iron Ore (100%) | Mineral Dust (30%) |
| Crystal Hollows | Crystal Golem | 60 | Aggressive | Crystal Shard (100%) | Crystal Mote (15%) |
| Crystal Hollows | Corrupted Dwarf | 90 | Aggressive | Cursed Ore (100%) | Crystal Mote (15%) |
| Crystal Hollows | Deep Dwarf King | 120 | Aggressive | Runed Stone (100%) | Crystal Mote (30%) |
| Cursed Crypts | Skeleton | 100 | Aggressive | Bone Fragment (100%) | Spirit Essence (15%) |
| Cursed Crypts | Ghost | 80 | Aggressive | Ectoplasm (100%) | Spirit Essence (15%) |
| Cursed Crypts | Necromancer | 200 | Aggressive | Soul Shard (100%) | Spirit Essence (30%) |
| Infernal Depths | Imp | 150 | Aggressive | Demon Horn (100%) | Brimstone Ash (15%) |
| Infernal Depths | Tormentor | 250 | Aggressive | Infernal Hide (100%) | Brimstone Ash (15%) |
| Infernal Depths | Demon Lord | 500 | Aggressive | Infernal Core (100%) | Brimstone Ash (30%) |

HP values shown are base (Layer 1/3/5/7/9 of each zone). Layer 2/4/6/8/10 variants use the layer difficulty multiplier from Section 5.1.

### 5.7 Loot Item Reference

| Zone | Item | Category | Sell Price | Source |
|---|---|---|---|---|
| Abandoned Mines | Rat Fang | Basic | 2g | Giant Rat |
| Abandoned Mines | Slime Gel | Basic | 2g | Blue Slime |
| Abandoned Mines | Bat Wing | Basic | 3g | Cave Bat |
| Abandoned Mines | Glowcap Spore | Basic | 5g | All Zone 1 mobs (bonus) |
| Deep Caverns | Stone Chunk | Basic | 3g | Stone Golem |
| Deep Caverns | Molten Gel | Basic | 4g | Red Slime |
| Deep Caverns | Iron Ore | Basic | 6g | Iron Golem |
| Deep Caverns | Mineral Dust | Basic | 5g | All Zone 2 mobs (bonus) |
| Crystal Hollows | Crystal Shard | Advanced | 10g | Crystal Golem |
| Crystal Hollows | Cursed Ore | Advanced | 12g | Corrupted Dwarf |
| Crystal Hollows | Runed Stone | Advanced | 18g | Deep Dwarf King |
| Crystal Hollows | Crystal Mote | Advanced | 8g | All Zone 3 mobs (bonus) |
| Cursed Crypts | Bone Fragment | Advanced | 8g | Skeleton |
| Cursed Crypts | Ectoplasm | Advanced | 12g | Ghost |
| Cursed Crypts | Soul Shard | Advanced | 30g | Necromancer |
| Cursed Crypts | Spirit Essence | Advanced | 15g | All Zone 4 mobs (bonus) |
| Infernal Depths | Demon Horn | Premium | 25g | Imp |
| Infernal Depths | Infernal Hide | Premium | 35g | Tormentor |
| Infernal Depths | Infernal Core | Premium | 80g | Demon Lord |
| Infernal Depths | Brimstone Ash | Premium | 20g | All Zone 5 mobs (bonus) |

**Icons (implemented):** all 35 items (raw + crafted) carry real `icon` + `spriteDrop` art from licensed 32px pixel packs — Monster Loot for raw drops, weapon/armor/jewelry/materials packs for crafted gear — under `Assets/Sprites/Loot/<pack>/`.

---

## 6. Shop System

### 6.1 Overview

The shop is the gold-generation endpoint of the pipeline. Loot flows in from porters, gets added to `Inventory`, and customers purchase items at the `Counter`. Gold is the output.

### 6.2 Inventory

`Inventory` is the single source of truth for all items and gold.

- Three item dictionaries: **Basic**, **Advanced**, **Premium**
- Each item can have a **keep-N floor** ("KEEP" stepper on the Inventory page, `Inventory.SetFloor`) — customer sales and crafting never take stock below it. ✅ *Reconciled: the old separate "reserve" became this single floor (crafting batch 2). Contract hand-ins ignore it — handing goods over is an explicit player action.*
- Gold supports both integer addition (sales) and fractional addition (clicker income)
- All gold operations go through `Inventory.Instance.TrySpendGold()` / `AddGold()`

### 6.3 Shelf & Counter

- **Shelf**: Displays items available for sale, organised by category. Visual representation of shop stock.
- **Counter**: Where sales happen. `SalesManager.TrySellItem()` validates stock, removes the item from inventory, and adds gold.
- **SalesManager** tracks which items are "marked for sale" and calculates available quantity (total − floor; there is no per-customer allocation).
- **Shopkeeper**: The counter is always staffed by the player's avatar. No staffing mechanic — the counter is never unstaffed.
- **Loot pile (implemented)**: shop stock is shown as a physical heap in an open bin behind the shopkeeper (capped at 16 sprites, a mix of the stocked item types). Porter deposits and vacuumed loot fly into it; each sale hops the item from the pile to the counter and on into the buyer's hands (the shopkeeper turns to face the bin while it lifts out) — stock level is readable at a glance.

### 6.4 Customer Spawn System

Customer traffic is driven by three stacking **multiplicative** inputs:

**Base rate — Guild Stars:**
Each star tier increases the baseline spawn rate. Stars are the primary traffic driver — the shop gets meaningfully busier with each progression milestone.

**Multiplier 1 — Items Marked for Sale:**
Having items available for purchase provides a spawn rate bonus. The more items (and the higher their tier), the stronger the multiplier. This creates an incentive to actively manage inventory and mark things for sale rather than leaving stock unmarked.

**Multiplier 2 — Active Layer Activity:**
Having dungeon layers running (adventurers fighting, loot being generated) contributes a bonus on top. An idle dungeon means slightly less foot traffic.

**Soft cap:**
The combined rate is capped by `maxAlive` customer limits, which scale upward with Guild Stars. You can't flood the shop with infinite customers — there's a meaningful ceiling that grows with progression.

**Empty shelf behaviour:**
Customers spawn regardless of whether items are actually in stock. A customer who arrives to nothing will attempt to buy, fail, and visibly leave empty-handed (upset walk + a reason bubble: out of stock vs. too expensive). This is intentional — it gives the player clear feedback that they're missing sales, without punishing them with a permanent traffic penalty.

**Party Customer spawning:**
Parties use a completely separate cooldown. Their weighted spawn chance is influenced by inventory quality and active layers, but they will **not** appear if the shop has nothing of value. See Section 4.6 for party behaviour details.

### 6.5 Failure State & Recovery

If the player reaches 0 gold with no units:
- Minimal customer traffic continues — the shop never completely dies
- The player avatar remains at the counter
- Clicking mobs on Layer 1 (always available) is always possible and generates loot/gold
- There is no game-over screen — the player can always click their way back to a functional state

This ensures the game never hard-locks, while still making gold mismanagement feel consequential.

### 6.6 Customer Purchase Flow

1. Customer decides to buy → enters `SeekingQueue` state
2. Joins queue when within 1m of queue tail
3. At counter: `SalesManager.TryPickDesiredForCustomer()` resolves the customer's **want** — the best affordable in-stock item in the archetype's preferred category, else the best affordable item anywhere
4. Transaction: item removed from inventory, gold added, `GameSignals.RaiseItemSold()` fired
5. Customer leaves; the item relays pile → counter → buyer (4.5), then the customer exits

### 6.7 Queue System

`QueueController` manages the customer line:

- **Max queue size**: configurable cap (no infinite queues)
- **Position calculation**: dynamic spacing based on queue length — gaps tighten as the queue grows (0.08–0.35m spacing range), using customer sprite widths for accurate placement
- **Queue end caching**: queue tail position is cached once per frame (O(1) per customer check, not O(n))
- **Dequeue and reflow**: when a customer leaves the counter, remaining customers step forward

### 6.8 Item Categories

| Category | Source | Typical Value |
|---|---|---|
| Basic | Dungeon loot (Layers 1–5) | Low |
| Advanced | Dungeon loot (Layers 3–8) / Crafted | Medium |
| Premium | Dungeon loot (Layers 6–10) | High |

---

## 7. Hiring System

### 7.1 Overview

Players spend gold to hire adventurers, porters, and customers from a rotating candidate pool. Each candidate is procedurally generated with a name, traits, rank, and cost.

### 7.2 Candidate Generation

`HiringCandidateGenerator` creates `HiringCandidate` structs with:
- **Identity**: procedurally generated name + epithet (e.g. "Gorn the Relentless") via `IdentityGenerator`
- **Rank**: 1–5 (affects base stats and hire cost)
- **Traits**: 0–3 randomly selected traits from the available pool (tier-based)
- **Hire cost**: base cost × 1.25^(units of that role already owned, guild-wide across all layers) × trait cost multipliers. Base = `EntityDef.hireCost`; class hires pay a premium — (Novice cost + the promotions they skip) × 1.4 (Bronze 4,200 · Iron 9,450 · Silver 22,575 · Gold 55,388)
- **Adventurer class + rank**: the board draws from `Resources/ClassCatalog` (Novice + 16 class tier defs). Tier N appears once layer N is unlocked; tier odds Novice 50 · Bronze 25 · Iron 13 · Silver 8 · Gold 4 over the unlocked tiers (the newest tier ×2 for 10 min after its layer unlocks — not re-applied by a save load), line uniform within a tier. Class hires join at their tier's rank. Cards show the class icon + name; tiers above Novice tint the portrait frame and rank badge (bronze / iron / silver / gold)
- **Newspaper name**: the candidate's display name in the hiring panel, generated once and stable across re-populates

### 7.3 Candidate Pool

`CandidatePool` manages a timed pool per unit type (adventurers, porters):
- Pool refreshes on a timer, replacing candidates that aren't hired
- Prices are baked at generation — after a hire, the remaining candidates keep their pre-hire prices until the next refresh (intended)
- `CandidateCount` property for no-alloc count checks
- `GetCandidates()` returns an allocating copy only when the UI needs to display the list

### 7.4 Hiring Panel

`HireController` drives the UI:
- Tab navigation between unit types (Adventurers, Porters, Customers)
- **Layer selector** — player chooses which layer the hired unit will be assigned to at hire time (one adventurer board for all layers; a card below the selected layer's rank shows a "Below this layer's rank" ribbon)
- Hire animation plays on successful hire
- Gold is deducted from `Inventory`; `GameSignals.OnUnitHired` fires
- **Top-bar hiring sign (implemented):** copper-coin badge + periodic button wiggle when gold covers the cheapest candidate in any pool (`CandidatePool.MinHireCost`); otherwise a paper "!" badge when pools restocked while the board was closed (cleared on open, no wiggle). A pixel pocket-watch on the sign's left edge drains toward the next pool refresh, shown only within the last `stopwatchShowSeconds` (120s default)

**Unit reassignment (future):** Drag-and-drop reassignment between layers is planned but not yet implemented. For now, layer assignment is set on hire and units stay on their assigned layer. Firing and re-hiring is the workaround for reassignment.

### 7.5 Unit Capacity

Each dungeon layer has a configurable max unit count per type. Players cannot hire beyond capacity without purchasing upgrades that raise the cap (e.g. `PorterLodging` upgrade for more porters).

---

## 8. Progression System

### 8.1 Star System

Progression is measured in **stars** (1★ to 5★). Stars gate layer unlocks and upgrades.

| Star | Layers Unlocked | What It Grants |
|---|---|---|
| 1★ | 1 (default) | Starting state |
| 2★ | Layer 2 | Star rewards (upgrades, recipes) |
| 3★ | Layers 3–5 | Star rewards |
| 4★ | Layers 6–7 | Star rewards |
| 5★ | Layers 8–10 | Star rewards (max progression) |

To earn a star, the player must complete **and claim** all milestones for the next star level (see 8.3). Each star also grants a one-time **Guild Seal** award — 30 / 50 / 80 / 120 / 200 for 1★–5★ (see 10.6).

⚠️ *Divergence (pre-existing, not yet reconciled):* the build starts the guild at **0★** with Layer 1 and unlocks **one layer per star** (`ProgressionManager`: star N → Layer N+1), so 5★ reaches Layer 6, not 10. The table above is the original 10-layer intent; decide which one stands.

### 8.2 Milestones

Milestones are tracked progress gates tied to in-game events. Each milestone has a **target value** and fires `GameSignals` events to increment counters.

**Milestone Types:**

| Type | Tracks |
|---|---|
| GoldEarned | Cumulative gold earned (all time), **excluding** milestone claim payouts — granted gold isn't earned gold |
| MobsKilled | Total mob deaths |
| UnitsHired | Total units hired |
| LootCollected | Total loot pieces collected |
| ItemsCrafted | Items produced at Forging Station |
| CraftedItemsSold | Crafted items sold to customers |
| UpgradePurchased | Upgrades bought |
| CustomersServed | Customers who completed a purchase |
| AdventurerRankedUp | Total adventurer rank-ups across all units |

**Example milestone targets (indicative, subject to balance):**

| Star Gate | Example Milestones |
|---|---|
| 1★ → 2★ | Hire 3 adventurers, earn 500 total gold, kill 50 mobs |
| 2★ → 3★ | Hire 5 adventurers, earn 5,000 gold, sell 20 Advanced items, unlock Forging Station |
| 3★ → 4★ | Rank up 2 adventurers, earn 25,000 gold, craft 10 items, serve 50 customers |
| 4★ → 5★ | Earn 100,000 gold, sell 20 Premium items, achieve 5 total rank-ups, buy 3 upgrades |

**First star — the implicit tutorial (implemented):** the 1★ tier teaches one verb per claim, and each payout funds the next step. Tracker order: kill 15 mobs (150g) → collect 25 loot (200g) → serve 1 customer (300g) → earn 2,500 gold (500g) → hire an adventurer (750g) → hire a porter (1,000g). Targets were stretched so the star lands around the **3-minute mark** (a first cut starred in under a minute). Tracker order comes from the asset file-name prefixes (`1_` … `6_`).

**Performance note:** Milestone checks use a dirty flag (`milestoneCheckPending`). Events set the flag; the actual O(n) scan runs once per frame at most, not on every gold tick.

### 8.3 Milestone Rewards

**Claiming (implemented):** reaching a milestone's target makes it **claimable** rather than completing it. The player presses Claim (HUD tracker or the Guild page in the Book); only then does it pay out and count toward the next star. Every milestone pays a **gold reward** (`goldReward`, ~100–300g at 1★ scaling to 500K at 5★), flown as a coin shower from the Claim button to the gold counter — gold banks per coin on arrival and every failure path banks directly. Unlock rewards (below) also grant at claim time. A few Crown-flavoured milestones also carry a **seal reward** (`sealReward`: The Tax Collector Noticed, 3★ tier, 20; The Crown Sent a Letter, 4★ tier, 30); everything else is gold-only, so a seal on a card stays special.

**Milestone tracker (implemented):** a torn-paper card list on the HUD, under the gold counter, sorted claimable > in progress > claimed, each card showing progress / gold Claim button / claimed state. The header badges and wiggles while anything is claimable. The Book's Guild page lists the same milestones with the same three states (and claims from there too), beside the guild upgrade cards (buy button, "Requires N-star" lock, or Owned).

Each milestone also has an associated `MilestoneRewardDef`. Reward types include:
- **Unlock upgrade** — make a `GuildUpgradeDef` available for purchase
- **Grant upgrade** — apply a `GuildUpgradeDef` for free
- **Unlock feature** — enable a game system (crafting station, etc.)
- **Unlock recipe** — add a recipe to `CraftingManager`
- **Spawn NPC** — trigger a world event

### 8.4 Layer Unlock

When a star is earned:
- `GameSignals.RaiseStarEarned(star)` fires
- `ProgressionManager` unlocks corresponding layers
- `GameSignals.OnLayerUnlocked(layer)` fires for each newly available layer
- `ProgressionManager` rebuilds the **available items cache** for the new layer (lists all lootable items by category)
- Spawners on unlocked layers activate

---

## 9. Upgrade System

### 9.1 Overview

Upgrades are one-time purchases from `GuildUpgradeDef` ScriptableObjects. They are unlocked via milestones or star rewards and purchased with gold.

### 9.2 Upgrade Types

**Guild Upgrades** — one-time structural improvements:

| Upgrade | Effect |
|---|---|
| **Forging Station** | Crafting station for the blade line; starts with 2 crafting slots |
| **Smithy** | Crafting station for the armor line; 1 slot |
| **Arcane Workshop** | Crafting station for the charm/jewelry line; 1 slot |
| **Research Station** | Unlocks a research/tech tree |
| **Elevator** | Faster porter transit on a specific layer |
| **Teleporter** | Instant porter transit on a specific layer |
| **Porter Lodging** | Increases porter capacity (more porters per layer) |
| **Storage Expansion** | Increases inventory capacity |
| **Guild Hall** | Visual upgrade, possible passive bonuses |
| **Price Mark-Up** | Applies a sell price multiplier to all items (future — not yet designed) |

**Mouse Upgrades** — improve player click power. ✅ *Implemented as repeatable ladders on the Book's Cursor page rather than guild-upgrade one-shots — see §4.7 for the live numbers.*

### 9.3 Purchase Flow

1. Upgrade must be in **available** state (unlocked by milestone or star reward)
2. Player must meet **star requirement** and have sufficient **gold**
3. `TrySpendGold()` deducts cost from `Inventory`
4. `GameSignals.RaiseUpgradePurchased(upgrade)` fires
5. Relevant managers respond (e.g. `CraftingManager` activates recipes, `PorterManager` raises capacity)
6. Upgrade is added to `ownedUpgrades` HashSet — cannot be purchased again

### 9.4 Layer-Specific Upgrades

Elevator and Teleporter upgrades are per-layer. The player selects the target layer at purchase. Multiple layers can each have their own Elevator independently.

---

## 10. Economy

### 10.1 Gold

Gold is the main currency; all gold operations flow through `Inventory.Instance`. **Guild Seals** are a second, time-gated currency (10.6).

**Income sources:**
- Counter sales (primary) — customer buys item, gold added
- Clicker income — manual tapping for small fractional gold amounts
- Milestone claims — one-off gold rewards (8.3)
- Contract claims — wholesale premium for handing over stock, or a share of passive income for counter contracts (10.6)
- Offline earnings — collected on return (10.5)

**Spending:**
- Hiring units (cost varies by rank + traits, ×1.25 per unit of that role already owned)
- Promoting adventurers (rank-scaled gold cost, 4.2)
- Purchasing upgrades (fixed cost from `GuildUpgradeDef`)
- Crafting station slots (2,500 × 1.6^slots bought at that station; currently bought from the Game Debugger only, until the world workshops land)

### 10.2 Item Pricing

Each `ItemDef` has:
- `sellPrice` — what customers pay at the counter
- `baseValue` — used for internal calculations (crafting value, etc.)

Higher-tier items (Advanced, Premium) have significantly higher `sellPrice`. This is the economic driver for pushing into deeper dungeon layers.

### 10.3 Customer Budget

Each customer has a randomly determined budget from their archetype (`CustomerDef.budget`): Commoner 200–600g, Adventurer 600–3,000g (2★+), Noble 6,000–10,000g (3★+). They buy the **most expensive item they can afford** in their preferred category, falling back to any category only when that yields nothing. Players who have expensive items but few cheap ones may see customers unable to buy, so keeping stock variety matters. ✅ *Reconciled: the old "50–100g generic budget" predates the archetype budgets; live item prices sit far above the original design tables (see the 13.3 note).*

### 10.4 Economy Balance Levers

| Lever | Effect |
|---|---|
| More adventurers | Faster mob kills → more loot |
| Higher adventurer rank | Stronger per-unit output — fewer deaths, faster clears |
| Mouse click (early) | Direct mob kills and loot pickup before automation exists |
| Damage AOE upgrade | Click-bombing clusters of mobs for burst clear |
| Loot AOE upgrade | Vacuum-collecting loot piles in one click |
| More porters | Faster loot delivery → shop fills faster |
| Elevator/Teleporter | Reduce porter round-trip time → higher throughput |
| Porter capacity | More items per trip → fewer wasted trips |
| Item tier | Higher-tier loot = higher gold per sale |
| Crafting | Convert basic items into more valuable goods |
| Queue capacity | More customers served simultaneously |

### 10.5 Idle Accumulation

While the game is running, the loop continues on its own (3.4). While it's **closed**, nothing is simulated — the guild is paid from rates measured during play (implemented; full design in `SAVE_DESIGN.md`):

- **Autosave:** every 30s, on pause/focus loss (builds only — the editor raises pause on focus loss) and on quit — no Save button. One local slot. What persists: gold, shop stock and keep-N floors, stars, milestone reached/claimed state, lifetime counters, clicker ladder levels, owned guild upgrades, game speed and Master/SFX/Music volumes, the full roster (identity, appearance, traits, rank, level/XP, assigned layer), contracts (offers, active contract + progress, rotation timer, seal balance, unhired Recruitment Favor candidates) and crafting (per-recipe on/cap/priority, station slots, running jobs with their remaining time). Loot on the floor or in porters' arms banks into stock on save. Not persisted: hire pools (fresh candidates on return), customers, mobs, VFX.
- **Passive rates:** while playing, the game keeps rolling ~10-minute averages of gold/min and loot/min — **customer sales and porter deliveries only**. Clicks, milestone claims and debug gold never count, and the rate reads zero unless at least one adventurer and one porter are employed: clicking isn't passive income. Measured on game time, so a 3× session doesn't earn 3× offline.
- **Offline award:** away time (capped at **8 hours**) × rate × **50%** efficiency, for both gold and items. Items split across what the unlocked layers' mobs drop, weighted by drop rates. Crafting does not run offline (v1).
- **Welcome back:** if away more than 2 minutes, a parchment panel shows time away, gold earned and the top 3 items ("+ N more"). Nothing banks until the big **Collect** button: gold flies as a coin shower to the counter, items fly into the loot pile. Quitting on the panel — or mid-shower — re-offers the same award next launch; nothing is lost or doubled.
- **Future knobs:** upgradeable cap ("Night Shift") and efficiency, offline events.

### 10.6 Guild Contracts & Guild Seals

Full design, rationale and phase plan: `CONTRACTS_DESIGN.md`. Phase 1 is built (`ContractManager`); the summary below is what ships.

- **Board:** a Contracts tab in the Book (7th bookmark) shows **3 offers**; the player accepts **1** at a time. Offers rotate every **10 minutes** (ignoring them is free); an accepted contract never expires. The active contract also appears as a card in the HUD milestone tracker.
- **Kinds:** **Deliver** a raw item (fulfilled from stock with a Fulfil button, ignoring keep-N floors) · **Hunt** — cull N mobs on a staffed layer · **Patronage** — serve N customers. Hunt/Patronage count from accept time.
- **Sizing:** target = max(measured rate × 5–10 min, 30–40% of current stock for Deliver, a small floor), **snapshotted when the offer rolls** so dumping stock can't shrink it.
- **Rewards:** Deliver pays target × sell price × **1.6** in gold (via the coin shower); Hunt/Patronage pay half the passive gold/min over the time the work takes. Plus flat seals: **10**, or **20–30** for contracts on layer 3 and deeper.
- **Seals** are active-play currency: they never come from offline time. Other sources are one-off — star awards (8.1) and a couple of Crown milestones (8.3). Seal count shows on a wax-seal plate beside the gold counter.
- **Seal shop:** **Recruitment Favor** (50) — an adventurer candidate joins the best adventurer pool (highest-rank def, then deepest layer) already promoted (rank = stars + 1, at least Bronze), survives pool refreshes and is kept in the save until hired; **Rush Order** (20) — every hiring pool restocks now.
- **Not yet built (phase 2+):** crafted/bulk deliver templates, a second active slot, a new-offer badge and rotation watch on the bookmark, the armory seal vendor.

---

## 11. Traits & Skills

### 11.1 Traits

Traits are modifiers assigned to hired units at generation time. They add personality and mechanical differentiation.

**Structure:**
- `TraitDef`: name, epithet list, tier data (1–3 tiers), stat modifiers per tier, hire cost multiplier, conflict list
- Each tier is stronger than the last
- Higher-tier traits are rarer in candidate pools

**Application:**
- Traits are assigned at `HiringCandidateGenerator` time
- Applied to the entity via `TraitComponent` at hire/spawn
- Stat modifiers added to `StatsMediator` — no manual refresh needed (dirty-flag cache)

**Conflict System:**
- Traits can list soft conflicts with other traits (e.g. "Berserker" conflicts with "Patient")
- Conflicts prevent conflicting traits from appearing together in normal generation
- Can be surfaced as a warning in UI

**Cost Impact:**
- Each trait has a `hireCostMultiplier` — powerful traits make candidates more expensive

### 11.2 Trait Examples (Design Targets)

| Trait | Effect |
|---|---|
| Berserker | +AttackDamage, -HP |
| Nimble | +MoveSpeed |
| Relentless | Reduced ReviveDelay |
| Hauler | +CarryCapacity (porter) |
| Hardy | +Max HP |
| Merchant's Eye | +Sell value multiplier (customer) |
| Keen Eye | +ScanRange |
| Veteran | Faster XP gain → reaches next rank sooner |

### 11.3 Identity & Epithets

Traits can contribute epithets to a unit's generated name:
- "Gorn **the Unstoppable**" ← contributed by Relentless trait
- "Mira **the Swift**" ← contributed by Nimble trait

This makes each hire feel like a distinct character, not a stat sheet.

### 11.4 Skills

Adventurers apply `EntityDef.startingSkills` at spawn and swap them when a promotion changes their class tier def (other entity types don't read the field yet).

**Passive Skills (implemented):**
- `Regeneration` — periodic HP recovery
- `AOEDamage` — periodic aura: every `tickInterval` it hits enemies within `radius` (flat `damagePerTick`, or `attackDamageFraction` × the owner's AttackDamage). The Mage line uses it as **Mage Splash** (25% of attack every 2 s within 2.5 m, Enemy layer only) — phase 1's stand-in for on-hit splash
- `GoldGeneration` — passive gold trickle

**Active Skills:** planned for future implementation.

Skills are defined via `SkillDef` ScriptableObjects and are separate from the trait system. They can stack with trait modifiers.

---

## 12. Stats System

### 12.1 Overview

Stats are layered: raw values (`BaseStats`) flow through a modifier stack (`StatsMediator`) and are exposed as cached properties (`Stats`).

```
EntityDef (design-time) → BaseStats → StatsMediator (runtime modifiers) → Stats (cached)
```

### 12.2 How Modifiers Work

- Traits add `IStatModifier` instances to `StatsMediator` at hire time
- Level-ups and promotions apply permanent modifiers (damage / attack speed); a promotion also swaps the class tier def, which replaces `BaseStats` (`Stats.SetBaseStats`) and max HP
- Skills and combat effects (e.g. stun = MoveSpeed → 0) add/remove modifiers at runtime
- Stats cache invalidates automatically when modifiers change
- No manual "refresh" calls needed
- Modifiers are removed by ID (`RemoveModifier(id)`)

### 12.3 Stat Reference

| Stat | Relevant Entities |
|---|---|
| MoveSpeed | Adventurer, Porter, Customer |
| AttackDamage | Adventurer, Mob (if aggressive) |
| AttackInterval | Adventurer, Mob (if aggressive) |
| ScanRange | Adventurer, Porter |
| AttackRange | Adventurer, Mob |
| ChaseBreakRange | Adventurer |
| StopDistance | Adventurer |
| CarryCapacity | Porter |
| PickupTime | Porter |
| DepositTime | Porter |

---

## 13. Crafting System

### 13.1 Overview

Crafting converts raw loot into higher-tier goods with greater sell value. Three guild stations each run one chain: **Forging Station** (blades), **Smithy** (armor), **Arcane Workshop** (charms/jewelry), each bought as a guild upgrade (5,000g; Arcane Workshop needs 2★).

### 13.2 Recipes

- Defined as `RecipeDef` ScriptableObjects (linked to `CraftingManager`); each names its **station**
- Recipes unlock via star rewards or milestone rewards
- Each recipe specifies: input items (type + quantity), output item (type + quantity), crafting time
- Ingredients are consumed when a job **starts**; output lands when it finishes
- ✅ *Reconciled: the per-recipe "reserve amount" became the shared per-item **keep-N floor** (6.2) — crafting never takes an ingredient below it.*

### 13.2a Production Controls (implemented — Against the Storm–shaped)

An enabled recipe auto-starts a job when all of these hold:
- its **station is owned** and has a **free slot** — stations are slot pools: Forging starts with 2, Smithy and Arcane Workshop with 1; extra slots cost 2,500 × 1.6^(slots bought there)
- the output's stock is **below the recipe's cap** ("produce until stock ≥ N"; default 10, 1–999)
- every ingredient stays **at or above its floor** after the job takes its share
- it isn't already running (one job per recipe)

**Priority 1–4** decides who goes first when recipes compete for slots or materials (higher first; ties in chain/tier order). Settings, floors, slots and running jobs all persist; a job running at quit resumes with its remaining time, so ingredients are never lost.

### 13.3 Recipe Reference

Recipes are organised in tiers. Each tier requires the crafted output of the previous tier as an ingredient, creating a crafting chain that spans all five dungeon zones.

> **Note:** the chains below match the build; **sell prices and a few ingredients/quantities are the original design targets** (e.g. Crystal Plate takes Runed Stone, not Spirit Essence; Runed Focus also takes Iron Ore). Live prices in the `ItemDef` assets are roughly 10–25× higher (Fanged Dagger 250g, Iron Sword 1,500g, Abyssal Armor 7,500g), and every recipe step is priced to clear **≥ 1.4×** the sell value of its ingredients. Treat the assets as the price source.

**Tier 1 — Basic Crafted (Zone 1 inputs only)**

| Recipe | Sell Price | Craft Time | Ingredient 1 | Ingredient 2 |
|---|---|---|---|---|
| Fanged Dagger | 10g | 5s | Rat Fang ×2 | Slime Gel ×1 |
| Padded Vest | 18g | 8s | Bat Wing ×2 | Slime Gel ×2 |
| Glow Charm | 22g | 7s | Glowcap Spore ×2 | Bat Wing ×1 |

**Tier 2 — Basic Crafted (Cross-zone: Zone 1 + Zone 2)**

| Recipe | Sell Price | Craft Time | Ingredient 1 | Ingredient 2 | Ingredient 3 |
|---|---|---|---|---|---|
| Stone Blade | 35g | 12s | Fanged Dagger ×1 | Stone Chunk ×2 | Molten Gel ×1 |
| Cavern Mail | 45g | 15s | Padded Vest ×1 | Iron Ore ×2 | Mineral Dust ×2 |
| Runed Focus | 50g | 14s | Glow Charm ×1 | Glowcap Spore ×1 | — |

**Tier 3 — Advanced Crafted (Cross-zone: Zone 2 + Zone 3)**

| Recipe | Sell Price | Craft Time | Ingredient 1 | Ingredient 2 | Ingredient 3 |
|---|---|---|---|---|---|
| Iron Sword | 100g | 25s | Stone Blade ×1 | Cursed Ore ×2 | Crystal Mote ×1 |
| Crystal Plate | 130g | 28s | Cavern Mail ×1 | Crystal Shard ×2 | Spirit Essence ×1 |
| Enchanted Ring | 140g | 26s | Runed Focus ×1 | Crystal Shard ×1 | Cursed Ore ×1 |

**Tier 4 — Advanced Crafted (Cross-zone: Zone 3 + Zone 4)**

| Recipe | Sell Price | Craft Time | Ingredient 1 | Ingredient 2 | Ingredient 3 |
|---|---|---|---|---|---|
| Bone Blade | 280g | 35s | Iron Sword ×1 | Bone Fragment ×2 | Spirit Essence ×1 |
| Ghostforged Plate | 320g | 38s | Crystal Plate ×1 | Ectoplasm ×2 | Soul Shard ×1 |
| Soul Gem | 350g | 36s | Enchanted Ring ×1 | Soul Shard ×2 | Spirit Essence ×1 |

**Tier 5 — Premium Crafted (Cross-zone: Zone 4 + Zone 5)**

| Recipe | Sell Price | Craft Time | Ingredient 1 | Ingredient 2 | Ingredient 3 |
|---|---|---|---|---|---|
| Demon Warblade | 600g | 50s | Bone Blade ×1 | Demon Horn ×2 | Infernal Core ×1 |
| Abyssal Armour | 750g | 52s | Ghostforged Plate ×1 | Infernal Hide ×2 | Brimstone Ash ×2 |
| Infernal Amulet | 650g | 48s | Soul Gem ×1 | Infernal Core ×1 | Demon Horn ×1 |

**Cross-zone dependency design:**
Each recipe tier requires inputs from at least one zone deeper than the previous tier. This incentivises the player to run multiple layers simultaneously — a Stone Blade needs Zone 1 and Zone 2 materials, an Iron Sword needs Zone 2 and Zone 3. Unlocking a new zone always opens new recipe possibilities immediately, even before that zone is fully farmed.

### 13.4 Integration with Economy

- Crafted items are marked as **Advanced** or **Premium** category
- They go into `Inventory` and are sold at the counter like any other item
- `GameSignals.OnProductCrafted` fires, incrementing the `ItemsCrafted` milestone counter
- The **Research Station** upgrade is intended to expand the recipe tree further

### 13.5 Design Intent

Crafting adds a secondary economy loop: instead of selling raw loot directly, the player can invest items into higher-value goods. The choice — sell basic items for steady low income, or hold stock and craft for bigger payouts — is the core tension. Cross-layer dependencies give the player a reason to care about multiple layers at once rather than just stacking the deepest one.

### 13.6 Crafting Page (implemented)

The Book's Crafting tab:
- **Left page:** recipes grouped by dungeon layer on torn sheets under the shared sub-header, three tiles per row. Each tile's frame carries its state: brown idle (or waiting for a slot), **gold progress sweep** while crafting, **red** when blocked on materials, **solid gold** at cap, **grey + padlock** when the station isn't owned. A pixel stock slip and a red "on" corner sit on the tile. Tiles only select.
- **Right page:** inventory-style detail — icon in the status frame, name banner, station / value / cap stepper planks, description, ingredient rows with have/need, craft time and priority pips. A full-width foot plank toggles the recipe: switching it on slams a **CRAFTING** stamp onto it, switching it off dithers the stamp away (the same stamp grammar as the Inventory's FOR SALE).
- Station slot purchases are not on this page; they move to the world workshops (planned: batch 3b).

---

## 14. UI & Presentation

### 14.1 UI Framework

All panels use **Unity UI Toolkit** (UXML/USS). Panels are orchestrated by `UIManager`. Each panel has a controller inheriting `BasePanelController`.

**Key Panels:**
- **Main HUD / Top Navigation** — gold display (animated count-up), star indicator, bookmark tabs, hiring sign (affordable-hire coin / new-stock "!" badge, refresh pocket-watch — see 7.4), cursor board (opens the Cursor page; badge while any click upgrade is affordable); boards tilt-wiggle every ~5s while actionable; the bookmarks hide while the Book UI is open (its own edge tabs take over)
- **Cursor Page (Book tab 6)** — cursor stats on the left page, upgrade cards on the right: template-cloned torn-paper cards (icon, name, current ➜ next in green, full-width price button with trailing coin), star-gated cards swap a lock pill for the buy button when unlocked
- **Hiring Panel** — tabs per unit type, candidate cards, layer selector; hiring badge refreshes with the candidate pool
- **Roster Panel** — active units, current rank, XP progress bar, promote button (when rank-up threshold is met) and promote affordances (gold gems, Units bookmark badge — see 4.2); right-page detail panel aligned to Figma, selecting a unit outlines them in-world and locks the camera to them
- **Milestone Tracker (HUD)** — torn-paper milestone cards under the gold counter with Claim buttons (see 8.3)
- **Guild Page (Book)** — star progress, claimable milestone list, guild upgrade cards
- **Welcome Back** — offline earnings summary + Collect, shown on launch after >2 min away (see 10.5)
- **Crafting Page (Book tab 4)** — recipe tiles per layer with state frames, recipe detail with cap/priority and the stamp toggle (see 13.6)
- **Inventory Page (Book)** — item counts by category, FOR SALE stamp, KEEP (floor) stepper
- **Contracts Page (Book tab 7)** — offers, active contract with Fulfil/Claim, rotation clock, seal shop (see 10.6); a wax-seal counter sits beside the gold plate

**UI look (implemented):** body text is **Ithaca** via the `--font-body` token (headers stay NotJam blackletter, the Guild Bulletin keeps Salmon Typewriter); buttons share one raised-key pixel sprite with a pressed-state swap on click and tint-driven variants; costs show the Copper Coin icon trailing the number everywhere; Book scroll views use a wooden-plank scrollbar on a parchment groove.

### 14.2 Camera & Layer Navigation

The dungeon is a **vertical side-scrolling 2D world**. The shop sits at the top; dungeon layers descend below it. Each layer is a horizontal space visible at one time; the player pans to navigate.

**Camera controls:**
- **WASD** — pan camera horizontally (left/right within a layer) and vertically (up/down between layers)
- **Mouse edge pan** — moving the cursor to the screen edge pans the camera in that direction
- Both input methods work simultaneously
- **Game speed:** number keys **1 / 2 / 3** set 1× / 2× / 3× time scale (a speed chosen while paused applies on unpause)

**Layer navigation intent:**
At low layer counts this is trivial. As the player reaches 5–10 active layers, vertical panning becomes a meaningful part of interacting with the game — jumping from the shop counter down to the deep dungeon layers and back. This also means all action is visible and the player can always observe bottlenecks directly (a full loot floor, a struggling adventurer, an idle porter).

### 14.3 Reactive UI

The UI does not poll — it subscribes to `GameSignals`. Gold display updates on `OnGoldChanged`. Unit counts update on `OnUnitHired`. Promotion eligibility updates when an adventurer's XP reaches the next rank threshold. This keeps UI CPU cost minimal.

### 14.4 Character Visuals

Characters are assembled from modular sprite parts:
- **Layers:** body, shirt, pants, hair (back + top), weapon (back + front)
- **Palettes:** skin tone, shirt colour, pants colour, hair colour
- Randomised from pools in `EntityDef` at hire time
- Higher-rank adventurers use progressively better equipment sprites (better weapon, armour overlay) so rank is readable at a glance without opening the roster

**Outlines (implemented):** friendly units (adventurers, porters) carry a **permanent near-black silhouette outline** via a URP render feature (`PermanentOutline` + `HoverOutlineFeature` on `Renderer2D`), reading them as "ours" against the background; mobs deliberately don't get it. Hover and Roster selection draw a **yellow outline** on a separate rendering-layer bit, compositing over the black one. Baked character icons (hire cards, roster portraits) are captured at true 1:1 sprite scale with a matching baked outline (`CharacterIconSettings`), so icons match the world look pixel-perfectly.

### 14.5 Sorting

`EntitySortingManager` manages sprite draw order. Entities closer to the foreground sort in front of those further back. `SortingGroup` per entity prevents z-fighting between modular sprite layers.

### 14.6 Animations

Key animation states (via `AnimatorOverrideController`):
- `Idle`, `Walk`, `Attack`, `Hit`, `Dead`
- Porter-specific: `PickUp`, `Climb`, `Deposit`
- Customer-specific: `Browse`, `Pay` (counter; reuses each character's jump clip), `Carry Walk`, `Upset Walk` — no combat states

`AnimatorOverrideController` is randomised per entity at spawn, providing visual variety within the same animation set.

### 14.7 Damage Numbers & Floating Text

`DamageNumberManager` spawns pooled floating indicators above entities (BrightLinger pixel font):
- **Damage** (white) — adventurer attacks, driven by the attacker in `CombatBehavior`
- **Gold + coin** (warm gold) — player clicks; the coin hands off to `ItemExplosionVFX` at the end of its float and flies to the gold counter, banking the gold on arrival
- **Level-up** (green "lvl ↑", the arrow a TMP sprite) and arbitrary hint text via `ShowText(message, transform, colour)`
- Floats render at sorting order 10000 so they draw over entities
- **Loot multiplier** (green ×N) — manual pickup bonus

Gold integrity rule: gold carried by a flying coin is banked by the arrival callback, and every failure path (spawn failure, mid-flight despawn) banks it immediately — visual effects may be skipped, gold is never lost.

### 14.8 Damage Flash

On hit, entities flash red via a shader (`_BlendAmount` property). Short duration, communicates impact clearly.

---

## 15. Audio & Feel

**First audio pass (implemented):** ten synthesized chiptune one-shots (`Tools/gen_sfx.py`, deterministic) played by `SfxManager` through an 8-voice pool, with per-sound pitch jitter and a throttle so bursts stay musical (a 36-coin shower ticks at ~12/s; 40 same-frame auto-clicks collapse to one hit). Master / SFX / Music volumes persist in the save; they are set from the Game Debugger for now (no in-game settings UI yet). **Ambience and music are not built** — Music is an empty volume slot.

| Built sound | Plays on |
|---|---|
| Click hit / Kill | player click damage / click kill |
| Vacuum | click loot vacuum |
| Coin | coin arrivals (fly-to-counter, claim showers) |
| Ka-ching | every item sold |
| Hire | unit hired (not on save-load respawns) |
| Milestone ding / Star fanfare | milestone reached / star earned |
| UI tick | buy / claim / accept buttons |
| Page flip | Book open, close and tab switch |

The table below is the original intent; loot pickup, promotion, mob hits by adventurers and a distinct upgrade sound are still unbuilt.

### 15.1 Ambience

- Dungeon ambient loop (per layer — deeper = darker, more ominous)
- Shop ambient loop (busier as more customers are present)

### 15.2 Feedback Sounds

| Event | Sound |
|---|---|
| Mob hit | Sword/thud impact |
| Mob death | Short death sound |
| Loot pickup | Jingle/item rustle |
| Gold earned | Coin clink |
| Customer purchase | Register chime |
| Unit hired | Fanfare sting |
| Adventurer promoted | Distinct flourish (different from hire) |
| Star earned | Achievement sound |
| Upgrade purchased | Upgrade sound |

### 15.3 Music

- Menu: calm guild-hall theme
- Main game: upbeat but not distracting merchant/tavern loop
- Deep layers: slightly more intense/tense variant

---

## 16. Content Reference

### 16.1 Dungeon Zones & Themes

| Zone | Layers | Theme | Mob Set | Loot |
|---|---|---|---|---|
| 1 | 1–2 | Abandoned Mines | Giant Rat, Blue Slime, Cave Bat | Rat Fang, Slime Gel, Bat Wing, Glowcap Spore |
| 2 | 3–4 | Deep Caverns | Stone Golem, Red Slime, Iron Golem | Stone Chunk, Molten Gel, Iron Ore, Mineral Dust |
| 3 | 5–6 | Crystal Hollows | Crystal Golem, Corrupted Dwarf, Deep Dwarf King | Crystal Shard, Cursed Ore, Runed Stone, Crystal Mote |
| 4 | 7–8 | Cursed Crypts | Skeleton, Ghost, Necromancer | Bone Fragment, Ectoplasm, Soul Shard, Spirit Essence |
| 5 | 9–10 | Infernal Depths | Imp, Tormentor, Demon Lord | Demon Horn, Infernal Hide, Infernal Core, Brimstone Ash |

### 16.2 Unit Types

| Type | Notes |
|---|---|
| Adventurer (Rank 1–5) | Multiple base visual variants; equipment sprites upgrade per rank |
| Porter | Fewer visual variants; focused on carry/speed stats |
| Customer (Regular) | Archetypes Commoner / Adventurer (2★) / Noble (3★) with archetype budgets — see 4.5 |
| Customer (Party) | Rare event; 2–4 member group, leader only queues |
| Mob (15 types) | 3 mobs per zone; see Section 5.6 for full reference |

### 16.3 Raw Items (20 total)

See Section 5.7 for full reference. Summary:
- 8 Basic items (Zones 1–2)
- 8 Advanced items (Zones 3–4)
- 4 Premium items (Zone 5)

### 16.4 Crafted Items (15 total)

| Tier | Count | Category | Sell Price Range |
|---|---|---|---|
| 1 (Zone 1 only) | 3 | Basic | 10–22g |
| 2 (Zones 1+2) | 3 | Basic | 35–50g |
| 3 (Zones 2+3) | 3 | Advanced | 100–140g |
| 4 (Zones 3+4) | 3 | Advanced | 280–350g |
| 5 (Zones 4+5) | 3 | Premium | 600–750g |

See Section 13.3 for full recipe table. Price ranges above are the original design targets; live prices are ~10–25× higher (13.3 note).

### 16.5 Upgrades

| Name | Unlock | Notes |
|---|---|---|
| Forging Station | 1★, 5,000g | Blade chain; 2 crafting slots |
| Smithy | 1★, 5,000g | Armor chain; 1 slot (asset `Potion Station`) |
| Arcane Workshop | 2★, 5,000g | Charm/jewelry chain; 1 slot (asset `Crafting Station`) |
| Research Station | 3★ | Unlocks tech tree |
| Elevator (per layer) | Milestone-gated | Faster porter transit |
| Teleporter (per layer) | Late-game | Instant porter transit |
| Porter Lodging | 2★+ | More porters per layer |
| Storage Expansion | 2★+ | More inventory capacity |
| Price Mark-Up | Future | Sell price multiplier |
| Guild Hall | Cosmetic | Visual upgrade |
| Mouse upgrades | Cursor page | Repeatable ladders — see Section 4.7 table |

### 16.6 Milestones (Per Star Tier)

Each star tier (2★–5★) has milestones spanning: Economy, Combat, Workforce, Production, Collection, Rank-Ups, and Upgrades. See Section 8.2 for example targets.

### 16.7 Traits (Target Pool)

Target: 15–25 distinct `TraitDef` entries across roles (Combat, Utility, Porter, Social, XP). Each with 1–3 tiers.

---

## 17. Open Design Questions

These are unresolved or actively debated design decisions, preserved here to avoid re-litigating them without context.

**Prestige / Rebirth**
Is there a reset mechanic? A prestige layer that resets progression in exchange for a permanent multiplier? This would extend the game's lifespan significantly but risks undermining the satisfaction of reaching 5★. Not planned for initial scope.

**Dynamic Pricing**
Should item sell prices fluctuate based on supply? If the shop has 100 Iron Swords, their price could drop. This adds a market management layer but significantly complicates the economy. Not planned; worth revisiting if the shop loop feels too passive.

**Trait Rerolling**
Should players be able to spend gold to reroll traits on a hired unit? Adds spending sink, but risks making the hiring pool irrelevant. Alternative: a one-time "veteran bonus" trait earned at some milestone.

**Guild Naming & Customisation**
Letting players name their guild and potentially customise the shop's visual style. Purely cosmetic; easy win for player attachment. Should be added when core systems are stable.

**Porter Route Visualisation**
A subtle dotted-line or indicator showing a porter's planned route could help players understand bottlenecks. Low priority but high readability value.

**Contract / Quest System** — ✅ *Resolved (implemented, phase 1).* Built as Guild Contracts + Guild Seals (`ContractManager`): 3 rotating offers, accept 1, deliver-from-stock / hunt / patronage, gold premium + a seal currency with its own shop — see 10.6 and `CONTRACTS_DESIGN.md`. Retained here for design-history context.

**Rank-Up Timing** — ✅ *Resolved (implemented).* Adventurers do **not** auto-rank-up; promotion is an explicit player action (`AdventurerAgent.Promote()`). The Roster panel surfaces a promote button (with its gold cost) once the adventurer reaches the rank's top level. Note the redesign changed the delay calculus: promotion now **costs gold**, and XP earned while parked at a rank's top level is **discarded** (not banked) — so delaying a promote wastes that adventurer's XP output. This is a deliberate anti-hoarding pressure toward prompt promotion. Retained here for design-history context.

**Party Customer Cooldown Tuning**
How frequent should parties be? Too rare and they feel like a lottery. Too common and they lose their "event" quality. A starting point: one party per 5–10 minutes of active play, scaling down (more frequent) at higher stars. Needs playtesting.

**End Game / Post-5★ Loop**
What keeps a 5★ player engaged? Options: a prestige/rebirth system, endless wave layers beyond Layer 10, seasonal leaderboards, or simply "optimisation sandbox" mode. No decision yet — defer until the core loop is fully stable.

**Crafting Reserve Threshold UX** — ✅ *Resolved (implemented).* One per-item keep-N floor set on the Inventory page (KEEP stepper), shared by sales and crafting, plus a per-recipe output cap and priority on the Crafting page (13.2a, 13.6) — no per-ingredient inputs per recipe. Retained here for design-history context.

**Party Size Scaling**
At 5★ the GDD notes "potentially 2 parties simultaneously." This needs to be evaluated carefully — two parties at once could overflow the queue and frustrate regular customers. A safer alternative: larger parties (5–6 members) rather than two simultaneous groups.

---

## 18. Technical Architecture Notes

*For developers. Summarises conventions relevant to extending the game.*

### 18.1 Entity Architecture
- All entities inherit from `EntityBase (MonoBehaviour)`
- AI uses `EntityStateMachine<TState>` at 10Hz — never `Update()` in AI; the tick accumulator resets each tick so `TickDelta` stays bounded at any frame rate / time scale
- Pooled entities implement `IPoolable` (`OnSpawned`, `OnDespawned`)
- Rank-up applies a permanent stat modifier set via `StatsMediator`; it does not swap the `EntityDef`
- Each rank tier's modifier values should be defined in `AdventurerDef` as an array (indexed by rank)

### 18.2 Event System
- All cross-system communication goes through `GameSignals` (static C# events)
- Raise via `GameSignals.Raise*()` helper methods
- Always unsubscribe in `OnDestroy` to prevent leaks

### 18.3 Gold Rules
- All gold operations: `Inventory.Instance.AddGold()` / `TrySpendGold()` / `CanAfford()`
- Never modify gold fields directly

### 18.4 Stat Rules
- Add modifiers via `entity.Stats.Mediator.AddModifier(modifier)`
- Never modify `BaseStats` at runtime
- Cache auto-invalidates; no manual flush

### 18.5 Query Rules
- Query methods (`GetUnitCount`, `GetTotalCount`) must be pure — no mutations
- Collection cleanup lives exclusively in `CleanupNullReferences` (runs every 2s)
- Dirty flags for high-frequency checks; never O(n) scan per increment

### 18.6 Pooling
- Frequent spawns: use `ObjectPoolManager` instead of `Instantiate`/`Destroy`
- Entities implement `IPoolable`

### 18.7 ScriptableObjects
- Data only — no MonoBehaviour logic
- Create content via `Assets > Data > ...` menu
- `EntityDef` subclasses for all entity configuration

### 18.8 Timers
- Reuse existing `CountdownTimer` instances: `timer.Reset(duration); timer.Start()`
- Never `new CountdownTimer(duration)` on every event

### 18.9 Singleton Pattern
- Persistent cross-scene managers use `PersistentSingleton<T>`
- Access: `GameManager.Instance`, `UnitManager.Instance`, `Inventory.Instance`, etc.

### 18.10 Save System
- `SaveManager` collects one `SaveData` DTO from each manager; new persistent state adds its chunk there and restores by re-raising the existing `GameSignals`
- ScriptableObject asset names are save keys — renaming a milestone, item, upgrade, recipe or unit def orphans it in existing saves (hence the Smithy and Arcane Workshop keep their old `Potion Station` / `Crafting Station` asset names)
- Chunks so far: meta (+ rates), economy (gold, stock, floors), progression, clicker, guild, roster, contracts (+ favors), crafting, settings (game speed, volumes)
- Pause-saves run in builds only; the editor still autosaves every 30s and on quit
- New income sources must decide whether they count toward the offline rate (only sales + porter deliveries do today)

---

*End of Game Design Document — Idle Merchant Guild v2.6*
