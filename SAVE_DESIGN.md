# Save System & Offline Progress — Design Doc

Status: **built** (`SaveManager`, `WelcomeBackPanel`) · Owner: Tuan · Updated 2026-10-20 — later chunks (contracts, crafting, volumes) are listed in §1; the code is the source of truth for field names

## Goals

1. The game remembers everything that matters across sessions.
2. Reopening the game is a *moment*: "While you were away…" with claimable offline earnings.
3. Saving is invisible (autosave), robust (no corrupt-save wipes), and cheap to extend when new systems land.

## Non-goals (v1)

- Cloud sync, multiple save slots, cross-device. Single local slot.
- Simulating the full game while offline (we award from rates, not simulation — see below).
- Saving mid-flight transients (coins in the air, customers mid-queue, loot on the dungeon floor).

---

## 1. What gets saved

One `SaveData` DTO, collected by a new `SaveManager` (PersistentSingleton). No per-component ISaveable ceremony — the managers are already singletons; SaveManager asks each for its chunk.

| Chunk | Fields | Source of truth |
|---|---|---|
| Meta | save version (int), last played UTC timestamp, total play time | SaveManager |
| Economy | gold; per-item stock counts (itemId → qty); per-item keep-N floors (itemId → N) | Inventory |
| Progression | stars; unlocked layers; lifetime counters (gold earned, mobs killed, loot collected, units hired/promoted, items crafted, crafted sold, customers served); reached + claimed milestone ids | ProgressionManager |
| Clicker | all ladder levels (damage, pickup radius, AOE, auto click, auto collect) | ClickerManager |
| Guild | owned upgrade ids | GuildUpgradePanel's backing data (move ownership into ProgressionManager or a small GuildState if it currently lives in UI — flag for implementation) |
| Roster | per unit: type (adventurer/porter), identity (name, appearance seed/indices), traits + tiers, rank, level/xp, assigned layer | AdventurerManager / PorterManager / IdentityComponent / TraitComponent |
| Offline-rate stats | rolling gold/min and loot/min from the last session (see §3) | SaveManager |
| Settings | game speed; Master / SFX / Music volumes | GameManager, SfxManager |
| Contracts | seal balance + lifetime seals, rotation time left, the 3 offers (null = taken), active contract (kind, params, snapshot target, progress, rewards), unhired Recruitment Favor candidates | ContractManager, HireController |
| Crafting | per recipe (RecipeDef name): enabled, cap, priority; slots per station; running jobs (recipe + seconds left — ingredients were already consumed, so jobs resume rather than refund) | CraftingManager |

The offline-rate meta also carries customers/min and kills/min per layer (contract sizing only — never offline income). Old saves without the contracts or crafting chunk load defaults (fresh offers rolled against the restored stock; every unlocked recipe enabled).

**Deliberately not saved:** candidate pools (regenerate on load — fresh stock is a feature), live customers, loot on the ground (banked: see §4), mob state (respawn naturally), VFX/UI state.

**IDs:** milestones, items, and upgrades are ScriptableObjects — save their asset *names* (stable, human-readable). A rename breaks saves; acceptable pre-release, note it in CLAUDE.md conventions.

## 2. Format, location, lifecycle

- **Format:** JSON via **Newtonsoft Json.NET** (official `com.unity.nuget.newtonsoft-json` package) — dictionaries serialize naturally, no parallel-array contortions, and it's the upgrade we'd otherwise regret skipping. Human-readable = debuggable; perf is irrelevant at this size (~tens of KB).
- **Location:** `Application.persistentDataPath/save.json`.
- **Atomic writes:** write to `save.json.tmp`, then File.Replace into `save.json`, keeping the previous file as `save.json.bak`. On load: if `save.json` fails to parse, fall back to `.bak` (and log loudly). A corrupt save never silently wipes progress.
- **Threading:** collect the DTO on the main thread (Unity objects aren't thread-safe), but serialize + write on a background Task so autosaves never hitch a frame; one in-flight save at a time, quit waits for the last write.
- **When to save:** every 30s (timer), on application pause/focus-loss, on quit. Manual "Save" not needed. *As built:* the pause-save is compiled out in the editor (`#if !UNITY_EDITOR`) — the editor raises pause whenever its window loses focus, which wrote stray saves during agent test sessions. Autosave and quit still save in the editor, so back up `save.json` and call `SaveManager.SuspendSaving()` before play-testing.
- **When to load:** once on boot, before managers spawn units. Load order: Inventory/Progression/Clicker state first → then managers spawn roster from data → then UI refreshes off the usual GameSignals (raise the existing changed-events after load rather than inventing new ones).
- **Versioning:** `version` int in the DTO. Migrations are a switch in one place; unknown-future-version loads refuse (don't destroy the file). Missing fields default safely (JsonUtility already zero-defaults).
- **New game:** no file = fresh start. A debug "wipe save" button goes in the Game Debugger.

## 3. Offline progress — rates, not simulation

Running the real sim for 8 hours of catch-up is fragile and unnecessary. Instead the game continuously measures itself while playing, and awards from those measurements:

- **While playing**, SaveManager keeps rolling rates over the last ~10 minutes of active play: `goldPerMin` (from GoldChanged deltas that come from sales — not from milestone claims or debug) and `lootPerMin` (raw items banked). Stored in the save.
- **On load**, `away = now − lastPlayed` (clamped: negative → 0, cap at **8 hours** v1).
- **Award:**
  - gold = `goldPerMin × awayMin × 0.5` (50% offline efficiency — away time is half as good as attention, standard genre tuning knob)
  - items = `lootPerMin × awayMin × 0.5`, distributed across the item types the player's unlocked layers actually drop, weighted by drop rates.
- **Honesty guard:** rates only accumulate while adventurers+porters exist (a pure-clicker session measures ~0 passive rate, so offline awards ~0 — clicking isn't passive income, which is exactly right thematically).
- **Clock tampering:** cap + clamp already bounds it; not worth more defense in a single-player game.

**Welcome-back screen:** a parchment panel on load when `away > 2 min`: time away, gold earned, items collected (top 3 icons + "and N more"), one big green **Collect** button → the existing UI coin shower flies to the counter, items fly to the loot pile. Reuses the claim juice wholesale. Until Collect is pressed nothing is banked (the one new transient: if they quit on this screen, it re-derives next boot since lastPlayed hasn't been advanced until collection).

**Future knobs** (not v1): offline cap upgradeable via a guild upgrade ("Night Shift"), efficiency upgradeable, offline events ("a wandering merchant bought 3 swords").

## 4. Edge cases & rules

- **Loot on the ground at save time** is banked directly into inventory on save (count it, despawn visuals on next load naturally). Gold never dropped — same house rule as VFX.
- **Units mid-anything** (fighting, hauling): saved as "exists, assigned to layer N"; they respawn at spawn points on load. Carried loot on porters banks on save like ground loot.
- **Milestone claim state** survives (reached + claimed sets) — this fixes the current "runtime-only" caveat.
- **Scene reload / debug restarts** in-editor: SaveManager only auto-loads on a cold boot flag, so editor play-mode iteration doesn't fight the save. Debugger gets Save Now / Load / Wipe buttons.

## 5. Implementation plan (3 commits)

1. **Core save/load:** SaveManager + DTO + atomic IO + load-order wiring + debugger buttons. Exit criteria: quit/reopen keeps gold, stock, stars, claims, ladders, upgrades, and the full roster with names/traits/ranks intact.
2. **Offline earnings:** rate tracking + award math + welcome-back panel with Collect (coin shower + item fly-ins). Exit criteria: measured rate in a 10-min play session matches the award for a simulated absence within rounding; 8h cap respected.
3. **Convention + docs:** CLAUDE.md rules ("every new persistent system adds its chunk to SaveData", "SO asset names are save keys — don't rename"), GDD section, debugger polish.

## Open questions (your call before implementation)

1. **Offline efficiency 50% and cap 8h** — happy with those starting numbers?
2. Welcome-back threshold: suppress under 2 minutes away?
3. Should **crafting stations** keep working offline (consume raw, produce crafted at measured craft rate)? v1 says no — only gold + raw loot. Cheap to add later.
