# Idle Merchant Guild — Game Design Document

**Version:** 2.2
**Date:** 2026-02-23
**Engine:** Unity 2D (URP, UI Toolkit, Spine/SpriteLibrary)
**Genre:** Idle / Management
**Platform:** PC (primary)

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
  Mouse upgrades → click damage, AOE damage, loot AOE

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
When the player is away, all loops continue:
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

Adventurers gain XP by killing mobs. When enough XP is accumulated, the player can **promote them to the next rank** (Rank 1 → Rank 2 → … → Rank 5). Promotion is a player-initiated action — the adventurer keeps fighting at their current rank until the player promotes them.

**Rank Effects:**
Each rank-up applies a permanent stat improvement — better attack damage, more HP, faster attack speed. The exact modifier values are tuned per rank. There are no branching paths; rank is a linear upgrade.

| Rank | Relative Power |
|---|---|
| 1 | Base (hired state) |
| 2 | Noticeably stronger — visible in faster mob clear |
| 3 | Mid-tier — can handle tougher layer mobs reliably |
| 4 | Strong — preferred for deep layers |
| 5 | Max — top-end adventurer |

**Design Notes:**
- Rank 1–5 exists at both hire time (higher-rank candidates cost more gold) and as an in-field upgrade (free via XP)
- The tension: a Rank 1 adventurer you've levelled to Rank 3 through play may be more valuable than a freshly hired Rank 3 because of their traits
- Higher-rank adventurers should be visually distinct (e.g. better equipment sprites) so rank is readable on screen without opening a panel
- XP progress should be visible on the adventurer's card in the roster UI

**XP & Leveling:**
- XP earned per mob kill, tracked on the entity
- XP per kill scales with layer depth — deeper mobs are worth more XP, incentivising pushing adventurers into harder layers
- **Recommended curve: exponential (3× per rank)**

| Rank Gate | XP Required | Notes |
|---|---|---|
| 1 → 2 | 100 XP | Fast — achievable in early Phase 1 |
| 2 → 3 | 300 XP | Takes noticeable time, feels earned |
| 3 → 4 | 900 XP | Mid-game investment |
| 4 → 5 | 2,700 XP | Late-game grind — Rank 5 should feel prestigious |

At ~1 XP per Layer 1 kill and ~10 kills/min, first rank-up takes roughly 10 minutes of idle time. Deeper layers give more XP per kill so adventurers assigned to them rank up faster — a further incentive to push layers.

- Promotion is capped at Rank 5 — no further gates after that

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
- On reaching counter: picks the highest-priced affordable item within their budget
- Budget is randomised per customer within a range (`CustomerDef.budgetRange`)
- Buys a random quantity (`CustomerDef.batchQuantity` min/max)
- Leaves after purchase, despawns on exit

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

**Click — Loot:**
- Clicking a loot drop instantly collects it into `Inventory`
- Early game this bypasses the porter entirely, enabling the shop to function before a porter is hired
- Late game it can clear a backlogged dungeon floor faster than waiting for the porter

**Mouse Upgrades:**
Mouse upgrades are purchased like guild upgrades (gold + star gate). They make clicking progressively more powerful.

| Upgrade | Effect |
|---|---|
| **Click Damage I / II / III** | Increases damage dealt per mob click |
| **Damage AOE I / II** | Clicking a mob also damages nearby mobs within a radius |
| **Loot AOE I / II** | Clicking a loot drop also collects nearby loot within a radius |

**Design Notes:**
- At game start the player *must* click to make progress — no adventurers, no porter, clicking is the only tool. This creates an active, hands-on opening that teaches the pipeline before automation takes over.
- As adventurers are hired and porters are added, clicking transitions from mandatory to optional-but-satisfying
- Loot AOE is particularly impactful on layers where mobs die in clusters and loot piles up
- Mouse upgrades should be clearly visible milestones — the player should *feel* the power jump after each one
- `ClickerManager` in code handles input, damage delivery, and loot collection; gold income from clicks is fractional (AddGoldFloat)

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

---

## 6. Shop System

### 6.1 Overview

The shop is the gold-generation endpoint of the pipeline. Loot flows in from porters, gets added to `Inventory`, and customers purchase items at the `Counter`. Gold is the output.

### 6.2 Inventory

`Inventory` is the single source of truth for all items and gold.

- Three item dictionaries: **Basic**, **Advanced**, **Premium**
- Items can be **reserved** (for crafting or other use) — reserved items are not available for sale
- Gold supports both integer addition (sales) and fractional addition (clicker income)
- All gold operations go through `Inventory.Instance.TrySpendGold()` / `AddGold()`

### 6.3 Shelf & Counter

- **Shelf**: Displays items available for sale, organised by category. Visual representation of shop stock.
- **Counter**: Where sales happen. `SalesManager.TrySellItem()` validates stock, removes the item from inventory, and adds gold.
- **SalesManager** tracks which items are "marked for sale" and calculates available quantity (total − reserved − allocated to waiting customers).
- **Shopkeeper**: The counter is always staffed by the player's avatar. No staffing mechanic — the counter is never unstaffed.

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
Customers spawn regardless of whether items are actually in stock. A customer who arrives to nothing will attempt to buy, fail, and visibly leave empty-handed. This is intentional — it gives the player clear feedback that they're missing sales, without punishing them with a permanent traffic penalty.

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
3. At counter: `SalesManager.TryPickDesiredForCustomer()` finds the best affordable item (highest priced, within budget)
4. Transaction: item removed from inventory, gold added, `GameSignals.RaiseItemSold()` fired
5. Customer exits

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
- **Hire cost**: base cost from `EntityDef.hireCost` × rank modifier × trait cost multipliers
- **Newspaper name**: the candidate's display name in the hiring panel, generated once and stable across re-populates

### 7.3 Candidate Pool

`CandidatePool` manages a timed pool per unit type (adventurers, porters):
- Pool refreshes on a timer, replacing candidates that aren't hired
- `CandidateCount` property for no-alloc count checks
- `GetCandidates()` returns an allocating copy only when the UI needs to display the list

### 7.4 Hiring Panel

`HireController` drives the UI:
- Tab navigation between unit types (Adventurers, Porters, Customers)
- **Layer selector** — player chooses which layer the hired unit will be assigned to at hire time
- Hire animation plays on successful hire
- Gold is deducted from `Inventory`; `GameSignals.OnUnitHired` fires

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

To earn a star, the player must complete **all milestones** for the next star level.

### 8.2 Milestones

Milestones are tracked progress gates tied to in-game events. Each milestone has a **target value** and fires `GameSignals` events to increment counters.

**Milestone Types:**

| Type | Tracks |
|---|---|
| GoldEarned | Cumulative gold earned (all time) |
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

**Performance note:** Milestone checks use a dirty flag (`milestoneCheckPending`). Events set the flag; the actual O(n) scan runs once per frame at most, not on every gold tick.

### 8.3 Milestone Rewards

Each milestone has an associated `MilestoneRewardDef`. Reward types include:
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
| **Forging Station** | Unlocks the crafting system |
| **Research Station** | Unlocks a research/tech tree |
| **Elevator** | Faster porter transit on a specific layer |
| **Teleporter** | Instant porter transit on a specific layer |
| **Porter Lodging** | Increases porter capacity (more porters per layer) |
| **Storage Expansion** | Increases inventory capacity |
| **Guild Hall** | Visual upgrade, possible passive bonuses |
| **Price Mark-Up** | Applies a sell price multiplier to all items (future — not yet designed) |

**Mouse Upgrades** — improve player click power:

| Upgrade | Effect |
|---|---|
| **Click Damage I / II / III** | +damage per mob click |
| **Damage AOE I / II** | Clicking a mob damages nearby mobs in a radius |
| **Loot AOE I / II** | Clicking loot collects nearby loot in a radius |

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

Gold is the sole currency. All operations flow through `Inventory.Instance`.

**Income sources:**
- Counter sales (primary) — customer buys item, gold added
- Clicker income — manual tapping for small fractional gold amounts

**Spending:**
- Hiring units (cost varies by rank + traits)
- Purchasing upgrades (fixed cost from `GuildUpgradeDef`)

### 10.2 Item Pricing

Each `ItemDef` has:
- `sellPrice` — what customers pay at the counter
- `baseValue` — used for internal calculations (crafting value, etc.)

Higher-tier items (Advanced, Premium) have significantly higher `sellPrice`. This is the economic driver for pushing into deeper dungeon layers.

### 10.3 Customer Budget

Each customer has a randomly determined budget (`CustomerDef.budgetRange`, e.g. 50–100g). They will buy the **most expensive item they can afford** from available stock. Players who have expensive items but few cheap ones may see customers unable to buy, so keeping stock variety matters.

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

While offline, the loop continues at normal speed (no catch-up multiplier in current design). Gold accumulates, milestones tick, loot builds up in inventory, adventurers accumulate XP. The player returns to see meaningful progress — and possibly a promotion waiting.

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

Skills are applied to entities at spawn from `EntityDef.startingSkills`.

**Passive Skills (implemented):**
- `Regeneration` — periodic HP recovery
- `AOEDamage` — splash damage on attacks
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
- Promotions apply a permanent modifier set at the moment of promotion
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

The crafting system is unlocked via the **Forging Station** upgrade. It converts raw loot into higher-tier goods with greater sell value.

### 13.2 Recipes

- Defined as `RecipeDef` ScriptableObjects (linked to `CraftingManager`)
- Recipes unlock via star rewards or milestone rewards
- Each recipe specifies: input items (type + quantity), output item (type + quantity), crafting time
- A configurable **reserve amount** prevents crafting from consuming all stock of an input item — the station auto-crafts only when stock exceeds the reserve threshold

### 13.3 Recipe Reference

Recipes are organised in tiers. Each tier requires the crafted output of the previous tier as an ingredient, creating a crafting chain that spans all five dungeon zones.

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

---

## 14. UI & Presentation

### 14.1 UI Framework

All panels use **Unity UI Toolkit** (UXML/USS). Panels are orchestrated by `UIManager`. Each panel has a controller inheriting `BasePanelController`.

**Key Panels:**
- **Main HUD** — gold display, star indicator, quick stats
- **Hiring Panel** — tabs per unit type, candidate cards, layer selector
- **Roster Panel** — active units, current rank, XP progress bar, promote button (when rank-up threshold is met)
- **Progression Panel** — milestone tracker, star progress
- **Upgrade Panel** — available upgrades, cost display
- **Crafting Panel** — recipe list, input/output display, reserve settings
- **Inventory Panel** — item counts by category

### 14.2 Camera & Layer Navigation

The dungeon is a **vertical side-scrolling 2D world**. The shop sits at the top; dungeon layers descend below it. Each layer is a horizontal space visible at one time; the player pans to navigate.

**Camera controls:**
- **WASD** — pan camera horizontally (left/right within a layer) and vertically (up/down between layers)
- **Mouse edge pan** — moving the cursor to the screen edge pans the camera in that direction
- Both input methods work simultaneously

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

### 14.5 Sorting

`EntitySortingManager` manages sprite draw order. Entities closer to the foreground sort in front of those further back. `SortingGroup` per entity prevents z-fighting between modular sprite layers.

### 14.6 Animations

Key animation states (via `AnimatorOverrideController`):
- `Idle`, `Walk`, `Attack`, `Hit`, `Dead`
- Porter-specific: `PickUp`, `Climb`, `Deposit`
- Customer-specific: `Buy`

`AnimatorOverrideController` is randomised per entity at spawn, providing visual variety within the same animation set.

### 14.7 Damage Numbers

`DamageNumberManager` spawns floating damage indicators above mobs when hit. Pooled for performance.

### 14.8 Damage Flash

On hit, entities flash red via a shader (`_BlendAmount` property). Short duration, communicates impact clearly.

---

## 15. Audio & Feel

*Note: Audio systems are not yet documented in code — this section describes design intent.*

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
| Customer (Regular) | Visual variety, randomised budget |
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

See Section 13.3 for full recipe table.

### 16.5 Upgrades

| Name | Unlock | Notes |
|---|---|---|
| Forging Station | 2★ milestone | Unlocks crafting |
| Research Station | 3★ | Unlocks tech tree |
| Elevator (per layer) | Milestone-gated | Faster porter transit |
| Teleporter (per layer) | Late-game | Instant porter transit |
| Porter Lodging | 2★+ | More porters per layer |
| Storage Expansion | 2★+ | More inventory capacity |
| Price Mark-Up | Future | Sell price multiplier |
| Guild Hall | Cosmetic | Visual upgrade |
| Click Damage I/II/III | Early-mid game | +click damage |
| Damage AOE I/II | Mid game | Click splash damage |
| Loot AOE I/II | Mid game | Click loot vacuum |

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

**Contract / Quest System**
A `ContractManager` exists in code. Contracts could give players short-term goals ("sell 10 Steel Swords by end of day") with gold/XP rewards. This could supplement the milestone system for mid-session engagement. Worth prototyping.

**Rank-Up Timing**
Should adventurers auto-rank-up when XP threshold is met, or require an explicit player action? Explicit creates satisfying engagement moments (and a clear notification hook) but risks friction if players don't notice. Auto-rank reduces friction but removes a feeling of agency. Currently leaning toward explicit with a prominent UI notification and no downside to delaying.

**Party Customer Cooldown Tuning**
How frequent should parties be? Too rare and they feel like a lottery. Too common and they lose their "event" quality. A starting point: one party per 5–10 minutes of active play, scaling down (more frequent) at higher stars. Needs playtesting.

**End Game / Post-5★ Loop**
What keeps a 5★ player engaged? Options: a prestige/rebirth system, endless wave layers beyond Layer 10, seasonal leaderboards, or simply "optimisation sandbox" mode. No decision yet — defer until the core loop is fully stable.

**Crafting Reserve Threshold UX**
The Forging Station auto-crafts when stock exceeds a reserve amount. How does the player set this reserve? A simple number input per ingredient seems right, but the UI for managing 5+ recipes each with 2–3 inputs could get cluttered. Needs UI design attention.

**Party Size Scaling**
At 5★ the GDD notes "potentially 2 parties simultaneously." This needs to be evaluated carefully — two parties at once could overflow the queue and frustrate regular customers. A safer alternative: larger parties (5–6 members) rather than two simultaneous groups.

---

## 18. Technical Architecture Notes

*For developers. Summarises conventions relevant to extending the game.*

### 18.1 Entity Architecture
- All entities inherit from `EntityBase (MonoBehaviour)`
- AI uses `EntityStateMachine<TState>` at 10Hz — never `Update()` in AI
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

---

*End of Game Design Document — Idle Merchant Guild v2.0*
