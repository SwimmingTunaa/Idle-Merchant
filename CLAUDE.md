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
- Top-nav board badges: `UIManager.SetBoardBadge(badge, visible, wiggle)` — shared badge toggle + ~5s button tilt-wiggle (scheduled item kept in the badge's `userData`); used by the cursor and hiring boards
- **UI Toolkit gotchas:** no `gap` (use margins) · no `box-shadow` / `text-shadow` · `var()` resolves only in USS rules, never inline `style=""` (inline `var()` throws an NRE that aborts the whole `CloneTree` → blank panel) · a margin reset must be a bare `*`, not `#scope *` (id specificity wipes class margins)

### Shop Pipeline
`Shop/Inventory.cs` → gold source of truth (use `Inventory.Instance` for gold ops)
`Shop/ShopManager.cs` → orchestrates shelf, counter, sales
`Shop/SalesManager.cs` → sells items to customers; `TryPickDesiredForCustomer` resolves a want and reports a `WalkoutReason` (`NoStock` / `TooExpensive`) on failure
`Shop/Shelf.cs` → holds items for sale
`Shop/QueueController.cs` → customer queue

### Loot Pipeline
`Loot/LootManager.cs` → spawns loot drops
`Loot/PorterAgent` picks up and transports loot via `TransportPoint`

### Hiring Pipeline
`CharacterGeneration/HiringCandidateGenerator.cs` → generates `HiringCandidate` structs (identity, traits, cost, `newspaperName`)
`UI/Panels/Hiring/CandidatePool.cs` → timed pool per unit type; exposes `CandidateCount` / `MinHireCost` (no-alloc, pure) and `GetCandidates()` (allocating copy); base cost = `hireCost × 1.25^owned`, baked at generation (owned-count func injected by `HireController`, summed across layer managers)
`UI/Panels/Hiring/HireController.cs` → stack UI, tab navigation, layer selector, hire animation; top-bar hiring sign (coin badge = cheapest candidate affordable + wiggle, "!" = restocked while closed; pocket-watch refresh countdown within `stopwatchShowSeconds`)
`UI/Panels/Hiring/HireRoster.cs` → weighted rank selection from pools
`UI/Panels/Hiring/CandidateUIMapper.cs` → populates Candidate.uxml from `HiringCandidate` data
`HiringCandidate` struct holds all per-candidate state; `newspaperName` is assigned once at generation, stable across re-populates

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
| `Scripts/UI/Panels/Hiring/HireController.cs` | Hire panel — stack UI, layer selector, animation |
| `Scripts/UI/Panels/Hiring/CandidatePool.cs` | Timed candidate pool per unit type |
| `Scripts/CharacterGeneration/HiringCandidateGenerator.cs` | Generates `HiringCandidate` structs with identity/traits |
| `Scripts/Entities/Health.cs` | Reusable health, damage flash, death/revive |
| `Scripts/Utility/ProgressionActivator.cs` | Activates GameObjects on layer/star/upgrade events |
| `Scripts/Managers/ClickerManager.cs` | Left-click damage/loot (friendlies transparent), right-click inspect, hover outline |
| `Scripts/Systems/DamageNumber/DamageNumberManager.cs` | Floating damage/gold/text numbers (pooled); gold banks on coin arrival |
| `Scripts/Effects/ItemExplosionVFX.cs` | Coins/items fly from world to UI counter (`ItemFlyAnimation`, `UITargetMarker`) |
| `Scripts/Entities/Adventuers/PromoteIndicator.cs` | Persistent badge+arrow above promote-ready adventurers |
| `Scripts/Rendering/HoverOutlineFeature.cs` | URP outline render feature — two instances on `Renderer2D`: hover/selection (layer bit 2, yellow) and permanent friendly outline (bit 4, near-black via `PermanentOutline`) |
| `Scripts/UI/AnimatedNumberUITK.cs` | Lerped count-up for the gold label |
| `Scripts/Effects/ClickHitVFX.cs` | Click juice: hit sparks, kill blow + hit-stop, squash, vacuum/shock rings (code-driven ParticleSystems, Emit-only) |
| `Scripts/UI/Panels/Cursor/CursorUpgradePage.cs` | Book Cursor page — clones `CursorUpgradeCard.uxml` per upgrade, binds via closures |
| `Scripts/Managers/SaveManager.cs` | Single-slot save (`SaveData` DTO, Newtonsoft JSON, atomic .tmp/.bak writes, 30 s autosave), cold-boot restore, offline rates + award; design in `SAVE_DESIGN.md` |
| `Scripts/UI/Panels/Welcome Back/WelcomeBackPanel.cs` | "While you were away" panel — display only, `SaveManager` banks on Collect |

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
- **`lastPlayedUtc` means "collected up to", not "last seen"** — while a welcome-back award is unclaimed it's saved as `now − pendingAway` and rates freeze, so quitting on the panel re-offers the identical award; don't "fix" it to `UtcNow`

---

## Editor Tools (under `Tools/` menu)
- `Tools/Game Signals/Clear All Listeners` — reset event subscriptions in editor
- `Tools/Game Signals/Print Subscriber Counts` — debug event leak detection
- Excel importer for bulk data entry
- ScriptableObject generator for batch asset creation

---

## Scenes
Located in `Assets/Scenes/`. Main scene contains the full game. MainMenu scene is separate.
Scene loading via `GameManager.Instance.LoadScene(sceneName)`.
