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
- Global USS variables: `Scripts/UI/GlobalVariables.uss`

### Shop Pipeline
`Shop/Inventory.cs` → gold source of truth (use `Inventory.Instance` for gold ops)
`Shop/ShopManager.cs` → orchestrates shelf, counter, sales
`Shop/SalesManager.cs` → sells items to customers
`Shop/Shelf.cs` → holds items for sale
`Shop/QueueController.cs` → customer queue

### Loot Pipeline
`Loot/LootManager.cs` → spawns loot drops
`Loot/PorterAgent` picks up and transports loot via `TransportPoint`

### Hiring Pipeline
`CharacterGeneration/HiringCandidateGenerator.cs` → generates `HiringCandidate` structs (identity, traits, cost, `newspaperName`)
`UI/Panels/Hiring/CandidatePool.cs` → timed pool per unit type; exposes `CandidateCount` (no-alloc) and `GetCandidates()` (allocating copy)
`UI/Panels/Hiring/HireController.cs` → stack UI, tab navigation, layer selector, hire animation
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

---

## Conventions

- **No `Update()` in entity AI** — use `EntityStateMachine` staggered tick (`OnUpdateState`)
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
