# Idle Merchant Guild — Claude Notes

## Project Overview
Unity 2D idle game. A merchant guild that hires adventurers, porters, and manages a shop. Dungeon layers produce loot, porters transport it, the shop sells it to customers.

**Engine:** Unity (URP, UI Toolkit, Spine/SpriteLibrary for modular characters)
**Language:** C# (.NET via Unity)
**Working directory:** `Assets/` (open scripts relative to here)

---

## Architecture

### Singleton Pattern
Persistent cross-scene managers use `PersistentSingleton<T>` (`Scripts/Managers/PersistentSingleton.cs`).
Access via `GameManager.Instance`, `UnitManager.Instance`, etc.

### Event System
`Scripts/Core/GameSignals.cs` — static C# events for decoupled communication.
- Raise: `GameSignals.RaiseGoldEarned(amount)`
- Subscribe: `GameSignals.OnGoldEarned += handler`
- Always unsubscribe in `OnDestroy` to avoid leaks.

### Entity Hierarchy
```
EntityBase (MonoBehaviour)
  └── EntityStateMachine<TState>   ← staggered AI tick at 10Hz
        ├── AdventurerAgent        (State: Idle/Wander/Seek/Attack/Hit/Dead)
        ├── MobAgent
        └── PorterAgent
  └── CustomerAgent
```

### Stats System (`Scripts/Entities/Stats/`)
- `BaseStats` — raw values from `EntityDef`
- `StatsMediator` — holds `IStatModifier` list, dirty-flag caching
- `Stats` — cached property access (`entity.Stats.MoveSpeed`, `.AttackDamage`, etc.)
- Add buffs via `entity.Stats.Mediator.AddModifier(modifier)`
- Cache auto-invalidates; no manual refresh needed.

### Data (ScriptableObjects)
- `EntityDef` → base entity data (prefab, stats, sprite libraries, colour palettes)
- `AdventurerDef : EntityDef` → adventurer-specific (health, revive delay, states)
- `MobDef : EntityDef`, `PorterDef : EntityDef`, `CustomerDef : EntityDef`
- `SkillDef`, `TraitDef`, `ItemDef` — content data
- Create via `Assets > Data > ...` menu

### Unit Management
`UnitManager<T>` (abstract) — hiring, spawning, tracking, capacity per dungeon layer.
Implements `IUnitManager` (defined in `Scripts/Core/GlobalInterfaces.cs`).
Concrete: `AdventurerManager`, `PorterManager`, `MobManager`.

### UI
- **UI Toolkit** (UXML/USS) — all panels use `UI Toolkit`
- Panels live in `Scripts/UI/Panels/`; each has a controller inheriting `BasePanelController`
- `UIManager` — top-level panel orchestration
- `GameSignals` drives reactive UI updates (gold display, unit counts, etc.)
- Design tokens: `Scripts/UI/GlobalVariables.uss` — colour / text / spacing / font-size `--vars` mirrored from Figma; consume via `var()` in `.class`/`#id` rules
- Reusable Button: `Scripts/UI/Components/Button.uss` + `Button.uxml` — tint-driven 9-slice variants (`btn-primary` / `-secondary` / `-success` / `-danger` / `-disabled`)
- Top-nav board badges: `UIManager.SetBoardBadge(badge, visible, wiggle)` — shared badge toggle + ~5s button tilt-wiggle (scheduled item kept in the badge's `userData`); used by the cursor and hiring boards and the Units bookmark (promote-ready count via pure `AdventurerManager.CountPromoteReady`, gold dirty flag)
- Book pages (`BookPanelController`, tabs 0 Roster · 1 Guild · 2 Inventory · 3 Crafting · 4 Settings · 5 Cursor · 6 Contracts) share: `.book-subhead` rule·label·rule header (`BookPanel.uss`, `--subhead-*` tokens), `UI/BookStamp.cs` slam-on / dither-off stamp (Inventory FOR SALE, Crafting CRAFTING), `UI/Components/StatusFrame.cs` (Painter2D progress sweep; idle/crafting/blocked/capped/locked)
- **UI Toolkit gotchas:** no `gap` (use margins) · no `box-shadow` / `text-shadow` · `var()` resolves only in USS rules, never inline `style=""` (inline `var()` throws an NRE that aborts the whole `CloneTree` → blank panel) · a margin reset must be a bare `*`, not `#scope *` (id specificity wipes class margins)

### Shop Pipeline
`Shop/Inventory.cs` → gold source of truth (use `Inventory.Instance` for gold ops)
`Shop/ShopManager.cs` → orchestrates shelf, counter, sales
`Shop/SalesManager.cs` → sells items to customers; `TryPickDesiredForCustomer` resolves a want (archetype's preferred category first, else best affordable anywhere) and reports a `WalkoutReason` (`NoStock` / `TooExpensive`) on failure
`Shop/Inventory.cs` floors → `SetFloor/GetFloor` "keep at least N" per item; respected by sales and crafting (UI label KEEP)
`Shop/LootPile.cs` → stock heap behind the counter; `FlySale` relays a sold item pile → counter → homing onto the buyer (`CustomerCarryDisplay` shows the carry on arrival); shopkeeper flips toward the bin while a leg is in flight
`Shop/Shelf.cs` → holds items for sale (fully commented out)
`Shop/QueueController.cs` → customer queue

### Crafting
`Crafting/CraftingManager.cs` (scene singleton, not persistent) → a recipe runs iff it is pinned in a station slot. Each station (`RecipeDef.station`: Forging 2 base slots / Smithy 1 / Arcane Workshop 1) holds an ordered `RecipeDef|null` list; jobs key by (station, slot), one per slot; the same recipe may fill several slots and crafts in parallel. Slot order is the priority (no priority field). Per-recipe output cap (default 25, 1–999) counts stock + jobs in flight. API: `GetSlots` / `AssignSlot(station, i, recipe|null)` (cancels + refunds that slot's running job) / `PinToFirstFreeSlot` / `ClearRecipeSlots` / `HasFreeSlot` / `GetSlotState` / `GetSlotProgress`; `IsRecipeEnabled` ≡ pinned somewhere, `EnableRecipe` = pin unless already pinned (progression unlocks). `BuyStationSlot` appends an empty slot, 2500×1.6^bought. `GetBlockReason` → `No station` / `At cap` / `Missing materials`. Signals `OnCraftStarted`, `OnCraftingConfigChanged` (every slot/cap change)
`UI/Panels/Crafting Menu/CraftingController.cs` → book Crafting page (tiles per layer on torn sheets, inventory-style right page; foot-plank stamp = pin to the station's first free slot — a full station shakes the plank and reads "<Station> full" — dither-off clears its slots; "<Station> · slot N" line)
`Crafting/WorkshopProp.cs` → station props in the guild interior (`Guild/Shop/Workshops`: Forging bay at the right end by the exit, Smithy left of it, Arcane Workshop left of the counter): frame 0 cold until the upgrade is owned, then a 6 fps lit loop; hover = outline bit 2; click (ClickerManager priority 3) opens the Workshop panel
`UI/Panels/Workshop/WorkshopPanelController.cs` → modal Workshop panel (`WorkshopPanel.uxml/.uss`, instanced in MainUI.uxml): stone wall + beam, the clicked prop's frames at 3× as hero, iron-plate slots (click → "Assign recipe" dropdown, "also in slot N" when stacked, Clear slot), "+ Slot" buy button; unowned station shows the cold frame + "Buy the <Station> in the Guild book"

### Contracts & Seals
`Managers/ContractManager.cs` → 3 offers rotating every 10 min, accept 1 (never expires); Deliver (raw items, fulfilled from stock) / Hunt (kills on a staffed layer) / Patronage (customers served). Target snapshotted at roll = max(rate × 5–10 min, stock × 30–40%, floor). Deliver gold = target × sellPrice × 1.6; counters pay 50% of passive gold/min over the work time. Seals: 10, or 20–30 for layer ≥3. Also owns the Guild Seal balance + seal shop (Recruitment Favor 50, Rush Order 20). Seals also come from star awards (30/50/80/120/200, `ProgressionManager`) and `StarMilestoneDef.sealReward`. Signals `OnContractAccepted/Completed`, `OnSealsChanged`. Design: `CONTRACTS_DESIGN.md`
`UI/Panels/Contracts/ContractsPage.cs` → book Contracts tab; the active contract also rides the HUD milestone tracker
`Shop/CaravanManager.cs` → the Caravan, a stock valve: every 8 min game time a wagon (scene singleton on `Guild/Shop/Caravan`, parked x 46.5 outside the right-end annex) offers 5 Guild Seals for 70% of the surplus above floor of the raw item with the highest (stock − floor) × sellPrice; no visit if that qty < 20; offer open 2 min, Accept = `TryRemove` + `GrantSeals` (no gold, no `OnItemSold` → not passive income). Click opens `UI/Panels/Caravan/CaravanPanelController` (reuses the Workshop `wp-*` shell). Signal `OnCaravanChanged`; saved as `SaveData.caravan`

### Audio
`Managers/SfxManager.cs` → `PersistentSingleton`, 8-voice round-robin pool, per-`SfxId` volume / pitch jitter / unscaled throttle; `Play(SfxId)` from VFX/UI seams, signal-driven for sale/hire/milestone/star. Volumes Master/SFX/Music (`SfxGain = master × sfx`; music is an empty slot) saved in `SaveData.settings`. Clips are synthesized by `Tools/gen_sfx.py` (project root, deterministic) into `Assets/Audio/SFX`

### Loot Pipeline
`Loot/LootManager.cs` → spawns loot drops
`Loot/PorterAgent` picks up and transports loot via `TransportPoint`

### Hiring Pipeline
`CharacterGeneration/HiringCandidateGenerator.cs` → generates `HiringCandidate` structs (identity, traits, cost, `newspaperName`)
`UI/Panels/Hiring/CandidatePool.cs` → timed pool per def; exposes `CandidateCount` / `MinHireCost` (no-alloc, pure) and `GetCandidates()` (allocating copy); base cost = `hireCost × 1.25^owned` (class defs: `ClassCatalog.HireCost` premium instead of `hireCost`), baked at generation (owned-count func injected by `HireController`, summed across layer managers)
`UI/Panels/Hiring/HireController.cs` → stack UI, tab navigation, layer selector, hire animation; top-bar hiring sign (coin badge = cheapest candidate affordable + wiggle, "!" = restocked while closed; pocket-watch refresh countdown within `stopwatchShowSeconds`)
`UI/Panels/Hiring/HireRoster.cs` → weighted selection by pool `LayerIndex` (adventurer pools: the def's tier), drawn without replacement
`UI/Panels/Hiring/CandidateUIMapper.cs` → populates Candidate.uxml from `HiringCandidate` data
`HiringCandidate` struct holds all per-candidate state; `newspaperName` is assigned once at generation, stable across re-populates

### Adventurer Classes (`Entities/Adventuers/ClassCatalog.cs`, design in `CLASSES_DESIGN.md`)
- `ClassLine` enum (Novice, Warrior, Guardian, Rogue, Mage — serialized ints, append only); one `AdventurerDef` per line × tier in `Prefabs/Entities/Adventurers/Classes/` + `Lvl 1 Novice`. Tier = the def's `rank`; `displayName` = class name, `description` = role line, `classIcon`, `damageMultiplier` / `hpMultiplier` (applied in `BaseStats.FromEntityDef` / `AdventurerDef.MaxHealth`); Rogue = 1.4× attack speed (`attackInterval` 0.71), Mage = slower heavy hits (`attackInterval` 1.25)
- `Resources/ClassCatalog.asset` (`ClassCatalog.Instance`) lists every hireable adventurer def + tier weights, unlock boost and hire premium. The hire board builds adventurer pools from it (one per def, gated `rank <= maxUnlockedLayer`); AdventurerManagers' `hireableDefs` are empty and unused — porters still use theirs
- `AdventurerAgent.Promote(line)` = AdvanceRank + `SetClassDef(next tier)` (base stats, max HP, skills swap; level/trait modifiers stay). A Novice must name a line (Roster "Choose a path" picker); `NeedsPathPick` (Novice above Wood) → free `ChoosePath(line)`, counted by `CanPromote` with `PromoteCost` 0
- Spawn/restore never swaps defs: units spawn with their tier def and `RestoreProgress` replays ranks/levels; `SpawnUnitWithCandidate` joins at `max(startRank, def.rank)`

### Object Pooling
`Systems/Pooling/ObjectPoolManager.cs` — use for frequently spawned objects
Entities implement `IPoolable` (`OnSpawned`, `OnDespawned`)

### Character Appearance
`CharacterGeneration/CharacterAppearanceManager.cs` — modular sprite assembly
`CharacterGeneration/CharacterSpriteGenerator.cs` — generates sprites from `EntityDef` libraries
`EntityDef` has sprite library arrays for body, shirt, pants, hair, weapons + colour palettes

### Traits & Skills
- `Traits/TraitDef.cs` + `TraitComponent.cs` on entities
- `Skills/Base/SkillDef.cs` → starting skills defined in `EntityDef.startingSkills`
- `Skills/Active/` and `Skills/Passive/`

### Identity System
`Identity/IdentityGenerator.cs` — generates names/identities for hired units
`Identity/IdentityComponent.cs` — attached to entities

---

## Key Files Quick Reference

| File | Purpose |
|------|---------|
| `Scripts/Core/GameSignals.cs` | Global event bus |
| `Scripts/Core/GlobalInterfaces.cs` | Shared interfaces (`ITickable`, `ResourceStack`, `IUnitManager`) |
| `Scripts/Managers/GameManager.cs` | Time scale, scene loading, pause |
| `Scripts/Managers/UnitManager.cs` | Abstract hiring/spawning base |
| `Scripts/Managers/ProgressionManager.cs` | Layer unlock progression |
| `Scripts/Entities/Base/EntityBase.cs` | Base MonoBehaviour for all entities |
| `Scripts/Entities/Base/EntityStateMachine.cs` | Staggered-tick FSM (10Hz) |
| `Scripts/Entities/Stats/Stats.cs` | Cached stat access |
| `Scripts/Entities/Adventuers/AdventuererAgent.cs` | Adventurer AI |
| `Scripts/ScriptableObjects/Entities/EntityDef.cs` | Base entity data |
| `Scripts/Shop/Inventory.cs` | Gold — single source of truth |
| `Scripts/UI/UIManager.cs` | Panel orchestration |
| `Editor/EntityDefEditor.cs` | Custom inspector for EntityDef |
| `Editor/ExcelImporter.cs` | Data import from Excel |
| `Editor/scriptableobject_generator.cs` | SO batch generation tool |
| `Scripts/UI/Panels/Hiring/HireController.cs` | Hire panel — stack UI, layer selector, animation, catalog pools + tier odds |
| `Scripts/Entities/Adventuers/ClassCatalog.cs` | `ClassLine` + class def catalog (Resources), tier odds, premium price |
| `Scripts/UI/Panels/Hiring/CandidatePool.cs` | Timed candidate pool per unit type |
| `Scripts/CharacterGeneration/HiringCandidateGenerator.cs` | Generates `HiringCandidate` structs with identity/traits |
| `Scripts/Entities/Health.cs` | Reusable health, damage flash, death/revive |
| `Scripts/Utility/ProgressionActivator.cs` | Activates GameObjects on layer/star/upgrade events |
| `Scripts/Managers/ClickerManager.cs` | Left-click damage/loot (friendlies transparent), right-click inspect, hover outline |
| `Scripts/Systems/DamageNumber/DamageNumberManager.cs` | Floating damage/gold/text numbers (pooled, sorting order 10000); gold banks on coin arrival; level-up float uses TMP sprite asset `UpArrow` (BrightLinger has no ↑) |
| `Scripts/Effects/ItemExplosionVFX.cs` | Coins/items fly from world to UI counter (`ItemFlyAnimation`, `UITargetMarker`) |
| `Scripts/Entities/Adventuers/PromoteIndicator.cs` | Persistent badge+arrow above promote-ready adventurers |
| `Scripts/Rendering/HoverOutlineFeature.cs` | URP outline render feature — two instances on `Renderer2D`: hover/selection (layer bit 2, yellow) and permanent friendly outline (bit 4, near-black via `PermanentOutline`) |
| `Scripts/UI/AnimatedNumberUITK.cs` | Lerped count-up for the gold label |
| `Scripts/Effects/ClickHitVFX.cs` | Click juice: hit sparks, kill blow + hit-stop, squash, vacuum/shock rings (code-driven ParticleSystems, Emit-only) |
| `Scripts/UI/Panels/Cursor/CursorUpgradePage.cs` | Book Cursor page — clones `CursorUpgradeCard.uxml` per upgrade, binds via closures |
| `Scripts/Managers/SaveManager.cs` | Single-slot save (`SaveData` DTO, Newtonsoft JSON, atomic .tmp/.bak writes, 30 s autosave), cold-boot restore, offline rates + award; design in `SAVE_DESIGN.md` |
| `Scripts/UI/Panels/Welcome Back/WelcomeBackPanel.cs` | "While you were away" panel — display only, `SaveManager` banks on Collect |
| `Scripts/Managers/SfxManager.cs` | One-shot SFX pool + Master/SFX/Music volumes |
| `Scripts/Managers/ContractManager.cs` | Contract offers/active/claim, Guild Seals, seal shop |
| `Scripts/Crafting/CraftingManager.cs` | Auto-crafting: station slots (pinning = running), caps, block reasons |
| `Scripts/Crafting/WorkshopProp.cs` | Guild-interior station prop: cold/lit frames, hover outline, opens the Workshop panel |
| `Scripts/UI/Panels/Workshop/WorkshopPanelController.cs` | Workshop panel — slot plates, assign dropdown, buy slot |
| `Scripts/UI/Panels/Crafting Menu/CraftingController.cs` | Book Crafting page (`Crafting_Page.uxml`, `CraftingPanelStyle.uss`) |
| `Scripts/UI/Panels/Contracts/ContractsPage.cs` | Book Contracts page (offers, active card, seal shop) |
| `Scripts/UI/BookStamp.cs` | Shared stamp slam/dither for book pages |
| `Scripts/UI/Components/StatusFrame.cs` | State-carrying icon frame with Painter2D progress sweep |
| `Scripts/Shop/LootPile.cs` | Stock heap; fly-in target and sale relay (`FlySale`) |
| `Scripts/Shop/CaravanManager.cs` | Caravan wagon: visit timer, raw-lot offer for seals, slide/wheel frames, save chunk |

---

## Conventions

- **No `Update()` in entity AI** — use `EntityStateMachine` staggered tick (`OnUpdateState`)
- **`TickDelta` must stay bounded** — the staggered tick resets its accumulator to 0 each tick; never go back to `-= UPDATE_INTERVAL` (long frames / high timeScale drift it upward and every AI timer expires on its first tick)
- **Customer walkouts clear the want** — any path that sends a customer to `Leaving` without a purchase nulls `desiredItem`/`desiredQty` and stamps `walkoutReason` first; carry display, the `Upset` anim bool and the reason bubble all key off it (see `CounterService.LeaveNow`)
- **Gold ops** — always go through `Inventory.Instance`, not direct field manipulation
- **Events** — raise via `GameSignals.Raise*()`, never invoke static events directly
- **Stat modifiers** — add via `Stats.Mediator.AddModifier()`, never modify `BaseStats` at runtime
- **Pooled objects** — implement `IPoolable`; call `ObjectPoolManager` instead of `Instantiate`/`Destroy`
- **ScriptableObjects** — data only, no MonoBehaviour logic; use `EntityDef` subclasses for entity config
- **Namespaces** — not used; all classes are in global namespace
- **`[Header]` and `[Tooltip]`** — used extensively in inspectors for designer-friendly fields
- **Debug logs** — use `[ClassName]` prefix convention, e.g. `Debug.Log("[UnitManager] ...")`
- **`showDebugLogs` field** — guard verbose logs with this serialized bool on managers/agents; default to `false`
- **Layer unlock events** — subscribe to `GameSignals.OnLayerUnlocked`; never add a duplicate `OnLayerUnlocked` event to individual managers
- **Query methods must be pure** — never call `RemoveAll` or mutate collections inside `GetUnitCount`, `GetTotalCount`, etc.; null cleanup lives exclusively in `CleanupNullReferences` (runs every 2 s)
- **Dirty flags for frequent checks** — set a `bool pending` flag on high-frequency events (gold earned, loot collected, etc.) and flush once per frame in `Update`; never call an O(n) scan directly from every increment
- **Count before list** — expose a `FooCount` property for callers that only need a count; reserve `GetFoos()` (allocates) for callers that iterate; e.g. `CandidatePool.CandidateCount`
- **Reuse timers** — call `timer.Reset(duration); timer.Start()` on an existing `CountdownTimer` instead of `new CountdownTimer(duration)` on every event
- **Click gold banks on coin arrival** — `ShowGoldGain` owns the gold; it's added via the fly-to-counter arrival callback. Any new failure path in that chain (DamageNumber, ItemExplosionVFX, ItemFlyAnimation) must bank the gold directly — VFX may be skipped, gold is never dropped
- **Pooled visual toggles must be symmetric** — anything a `Show*`/spawn path disables (e.g. `textMesh.enabled`) must be re-enabled by every sibling path; pooled reuse otherwise leaks the off state into unrelated displays
- **Outline rendering-layer bits** — bit 2 = hover/selection (yellow), bit 4 = permanent friendly outline (near-black); world-space markers that shouldn't be stroked must clear these bits on their renderers
- **Overlay VFX sorting** — `EntitySortingManager` assigns entities orders 2000–8000; screen-feedback particles/markers that must draw over them use order 10000+
- **Body font via token** — `--font-body` in `GlobalVariables.uss` (currently Ithaca); never hardcode a body-font url in USS. Inline UXML styles can't use `var()`, so the few inline font urls (Inventory, Candidate) need manual updates on a font change
- **Clicker upgrade ladders live on `ClickerManager`** — cost/level formulas in code, levels serialized; all purchases raise `OnClickUpgradePurchased` (UI + board badge refresh off it). Game Debugger → Economy has free level set/bump controls
- **New persistent state goes in `SaveData`** — add a chunk to the DTO, fill it in `SaveManager.Collect`, restore it in `ApplyEarly`/`ApplyLate`, then re-raise the existing `GameSignals` so UI rebuilds (no new "loaded" events, no per-component ISaveable). Game Debugger → Save has Save Now / Load / Wipe / Simulate Away
- **ScriptableObject asset names are save keys** — items, milestones, guild upgrades and unit `EntityDef`s are saved by `.name`; renaming one orphans it in existing saves (not restored; only items log an error)
- **Offline rates count passive income only** — customer sales (`OnItemSold`) and porter deliveries (`RecordPorterDelivery`); clicks, milestone claims and debug gold never count. A new income source must decide explicitly whether it's passive
- **Gold in flight rides the save** — gold that's committed but still animating (offline Collect shower) is tracked in `goldInFlight` and saved as banked, decremented per coin on arrival; follow that pattern for any new delayed-bank flow
- **Editor never saves on pause** — `OnApplicationPause` saves only in builds (`#if !UNITY_EDITOR`; the editor raises pause on focus loss). Autosave (30 s) and quit still save in the editor, so agents still back up `save.json` and call `SaveManager.SuspendSaving()` before play-testing
- **Transport is per depth** — `Elevator I–V` / `Teleporter I–V` assets are `layerSpecific` save keys (roman numeral = depth, display name "Depth N …"); each ladder's mode = that depth's owned upgrades (`PorterManager.ApplyTransport`), a teleporter leg hops one floor up, never straight to the hall. Elevator = `Elevator_Shaft` sprite, teleporter = ladder hidden + the `Teleport Pad` child (entry dais at the base, its `Teleport Exit` slab on the floor above; activator per depth); a porter stands 0.25 s on the pad before the hop
- **Station defs keep old asset names as save keys** — `Potion Station.asset` is the Smithy, `Crafting Station.asset` the Arcane Workshop; don't rename. `UpgradeType` values are serialized ints, so append new ones only
- **Upgrades may cost materials** — `GuildUpgradeDef.materials` (`RecipeDef.Ingredient` list, empty = gold only) is checked in `CanPurchaseUpgrade` and consumed in `PurchaseUpgrade`, floors ignored like contract hand-ins; station slot k also costs 100×1.5^k of the station's signature raw (Forging StoneChunk / Smithy IronOre / Arcane CrystalShard, `CraftingManager.GetSlotMaterial`/`GetSlotMaterialCost`). Cost chips: `GuildUpgradePanel.MaterialRow`/`MaterialChip` + `.mat-*` in `GlobalVariables.uss`
- **One stock floor** — `Inventory.SetFloor` is the only "keep N"; sales and crafting respect it. Contract hand-ins (`ContractManager.Fulfil`) deliberately ignore it (explicit player action)
- **Crafting display state ≠ `GetBlockReason`** — that's the start gate for a free slot (cap counts jobs in flight); the book derives capped/blocked from stock alone (`CraftingController.StateOf`), the Workshop panel uses `GetSlotState` per slot
- **Slots are the crafting switch** — never add a separate enabled/priority flag; "on" = pinned in a slot. A slot's recipe change cancels and refunds its running job, so a job's recipe always equals its slot's. Crafting saves `slots:{station:[recipe|null]}` + `jobs:[{station, slot, recipe, remaining}]`; pre-3b `enabled` flags migrate into free slots on load (recipes with a running job first) and a job that finds no slot is refunded
- **Don't reimport UXML/USS during a play session** — UITK live reload rebuilds the document tree and controllers holding element refs (Workshop panel, book pages) go stale; restart play to check style edits
- **Stamp overlays sit at page-root level** — the `Stampanim` element is a sibling of left/right content, never inside a page (pages clip overflow); aim it with `BookStamp.AimAt`
- **Crafting right page is height-budgeted** — fits 650px with three 46px ingredient rows and the 56px foot plank 22px off the bottom, no scrolling; growing one block means shrinking another (budget note atop `CraftingPanelStyle.uss`)
- **New pages use `--font-size-body` 24 / `-small` 20 / `-caption` 18** (Ithaca, nothing smaller) — never the legacy `--text-*` scale; Roster + `Components/*.uss` still consume it, so migrate page by page, don't bump it in place
- **Class tier defs are save keys** — the roster saves the adventurer's *current* tier def name; class def asset names (`Bronze Warrior` … `Gold Archmage`, `Lvl 1 Novice`) are final. An unknown adventurer def restores as the Novice (free path pick if above Wood) — never orphaned
- **Class power lives on the tier def, not on promotion** — don't reintroduce per-rank damage/HP modifiers in `AdvanceRank`; tune `damageMultiplier` / `hpMultiplier` on the def (only attack speed still steps per rank)
- **New adventurer classes/tiers go in `ClassCatalog.defs`** — not on a layer manager's `hireableDefs`
- **Class signatures are data on the tier def** — cleave / taunt + damage reduction / crit + pursuit / % max-HP fields on `AdventurerDef` (on iff > 0); resolved in `AdventurerAgent.ResolveHit` via `CombatBehavior.HitOverride`. Splash and other secondary damage go straight to `Health.OnDamage` so procs never chain
- **`lastPlayedUtc` means "collected up to", not "last seen"** — while a welcome-back award is unclaimed it's saved as `now − pendingAway` and rates freeze, so quitting on the panel re-offers the identical award; don't "fix" it to `UtcNow`

---

## Editor Tools (under `Tools/` menu)
- `Tools/Game Signals/Clear All Listeners` — reset event subscriptions in editor
- `Tools/Game Signals/Print Subscriber Counts` — debug event leak detection
- Excel importer for bulk data entry
- ScriptableObject generator for batch asset creation
- `Editor/SpriteImportDefaults.cs` — first import of any texture under `Sprites/` gets point filter / uncompressed / no mipmaps / Sprite type automatically; PPU is not set (varies per folder), and existing files are never touched

---

## Scenes
Located in `Assets/Scenes/`. Main scene contains the full game. MainMenu scene is separate.
Scene loading via `GameManager.Instance.LoadScene(sceneName)`.
