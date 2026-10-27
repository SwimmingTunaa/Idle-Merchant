# Guild Hall decorations — build spec

Mock (approved 2026-10-27): https://claude.ai/artifact/LGqaWvEXdzG6T8PYz7rU6S (three stages, built from the real scene).
Pairs with `STAR_FANFARE_DESIGN.md` (the GUILD HALL unlock card lights up once this ships).

## Goal
The guild hall should visibly fill up as the player progresses. Decor arrives **free with stars**, trophies are **earned by
kills**, the upstairs room comes with a **real upgrade**, and the player can **swap decor variants for Guild Seals**
(Hades-II style: fixed slots, choose what sits there). No new progression track, no camera moves.

Decisions already taken: no camera drift (hostile while the player is busy) · decor is free, swaps cost seals · porter bunks
not adventurer barracks · trophy at **100 kills at that depth** · the trophy slot is earned, never swappable.

## Hall geometry (world units, from the scene; 8 px per unit)
- Ground floor: floor y = 0, beam top y ≈ 5.75. Lower windows at x ≈ 8.3, 23.5, 35.5. Exit at x ≈ 43.
- Fixtures: counter x 16–20, loot bin x 22.75, Arcane Workshop x 28.25, Smithy x 35.25, Forge x 40.1.
- Upstairs: usable wall x 8–18 under the roof slope (slope runs from (17, 15.5) down to (26, 7.4)); "floor" y ≈ 6.0.
- Big blank upstairs wall x 10–16, y 9–13 → the crest.

## Slots (fixed positions; each is one GameObject under `Guild/Shop/Decor`)
| slot | position | free default (trigger) | swappable | variants from `VP2_Interriors` |
|---|---|---|---|---|
| `crest` | upstairs wall, x 12.5, top y 12 | crest, tinted bronze → 2★ silver → 4★ gold (`StarEarned`) | tint only follows stars; **shape** swappable | Tuan draws ONE crest sprite; tint via the palette material (`_New_Colour`). Alternate shapes later. |
| `banner_l` / `banner_r` | hanging from the beam, x 10 / x 33 | teal banner at 3★ / 4★ | yes | teal, red (`Flag_banner` tint), green |
| `chandelier_l` / `chandelier_r` | beam, x 17.8 / x 31 | 2★ / 4★ | yes | iron (tileset), brass (tint) |
| `trophy_1..5` | under the beam, x 10.6, 12.2, 13.8, 25.4, 27.6 | **100 lifetime kills at depth N** | **no** | depth 1–2 mounted heads, depth 3–5 framed scenes |
| `notice` | by the exit, x 41.5 | blank board at start; paper appears on **first contract accepted** | no | — |
| `bookcase` | x 25.0 | **first recipe pinned** (`OnCraftingConfigChanged`, any slot non-null) | yes | books, potions, empty-dark |
| `bunks` | upstairs, x 9 / 12.9 / 16.8, bottom y 6.0 | **Porter Lodging I / II / III** | blanket colour swappable | red, blue, brown, white beds |
| `shelf` | wall over the counter, x 20.4, top y 5.4 | first crafted item in stock | no (deferred: physical shelves own this) | — |

Rule of thumb: every slot's default is the tileset sprite already used in the mock; a "variant" is a different crop or a tint.

## Triggers
- **Stars / depths / upgrades**: `ProgressionActivator` as-is (`StarEarned` / `LayerUnlocked` / `UpgradePurchased`).
- **Kills at depth** (new): `SaveManager` already tracks per-depth kill rates (`KillsPerMin(layer)`); add a **lifetime kills per depth** counter
  (`int[] killsByDepth` in the progression chunk, saved) incremented where the rate is recorded. Add `ActivationTrigger.KillsAtDepth`
  (`requiredValue` = depth, threshold 100 constant) to `ProgressionActivator`; it checks on enable and on each kill signal, so it also catches up on load.
- **First contract / first recipe**: `ActivationTrigger.ContractAccepted` (listens `GameSignals.OnContractAccepted`) and
  `ActivationTrigger.RecipePinned` (listens `OnCraftingConfigChanged`, checks `CraftingManager.GetSlots` has any recipe). Both must check on enable too (restored saves).
- Append enum values only (serialized ints).

## Pop-in ("seen" rule, replaces camera drift)
- Unlocking a prop does **not** show it. It sets `pending = true` on the activator. On every camera move (`CameraScrollController`,
  cheap AABB test against the hall rect x 8–44, y −2–16) a pending prop whose own bounds are on screen **pops in**: scale 0 → 1.15 → 1 over 0.35 s
  (unscaled), one dust puff (`ClickHitVFX` ring reused, small), `SfxId.UiTick`. Several pending props stagger 0.12 s apart.
- A prop already unlocked when the save loads pops silently (no animation) — only *new* unlocks animate.
- Saved as `decor.pending[]` so a quit before looking still pops in next session.

## Swapping (Guild Seals)
- Clicking a swappable slot in the hall (hover = outline bit 2, click priority like `WorkshopProp`) opens **Decorate**: a small modal in the
  Workshop-panel pattern. Row per variant: preview sprite, name, state — *Placed* / *Owned → Place* / *Buy for N seals* (`ContractManager.TrySpendSeals`).
  Locked slots (default not yet unlocked) don't open.
- Prices: banners 20 · chandelier 30 · bookcase 30 · bunk blankets 20 · crest shape 60. Bought = owned forever; placing is free. Trophies and the notice board never open.
- Seal shop page in Contracts gets no new entries; decor is bought in place.
- Data: `DecorVariantDef` SO (`slot`, `displayName`, `sprite`, `tint`, `price`); `DecorCatalog` in `Resources` lists them. `DecorSlot` MonoBehaviour on
  each slot object holds `slotId`, the `SpriteRenderer`, the current variant, and is the click target.
- Save: `decor.placed {slotId: variantName}`, `decor.owned [variantName]`, `decor.pending [slotId]`. SO asset names are save keys (project rule).

## Porter Lodging (the one real upgrade)
- `GuildUpgradeDef` with `UpgradeType.PorterLodging` (enum value already exists), three ranks I/II/III bought from the Guild page like the stations:
  each adds **+2 `maxUnits`** to every `PorterManager`. Costs TBD by Tuan; placeholder 5 000 / 12 000 / 30 000.
- Each rank activates one bunk (`UpgradePurchased`, requiredUpgrade = that rank's def).
- Porters don't walk upstairs; the bunks are set dressing.

## Fanfare hook
- `StarFanfare` GUILD HALL card = names of every activator that fires for this star (`StarEarned`, `requiredValue == star`), joined with ` & `.
  Needs a `displayName` field on `ProgressionActivator` (empty = not listed).

## Art needed from Tuan
- One crest sprite (≈ 22×40 px at 8 PPU, light pixels only so the tint lands; outline dark).
- Optional later: alternate crest shapes, a brass chandelier if tint looks wrong.
Everything else is cropped from `VP2_Interriors.png` (coords in the mock's `compose.py`).

## Out of scope
- Physical shelves / stock display (own build). Adventurer capacity upgrade. Exterior changes. Camera moves of any kind.

## Build order
1. Scene: `Guild/Shop/Decor` slots with default sprites placed per the table, all deactivated, `ProgressionActivator` on each (existing triggers only). Verify with the debugger star buttons.
2. New triggers: `KillsAtDepth` (+ saved counter), `ContractAccepted`, `RecipePinned`.
3. Pop-in "seen" rule + pending save.
4. Porter Lodging upgrade ranks + bunks.
5. Decor swapping: defs, catalog, `DecorSlot` click → Decorate modal, seal spend, save.
6. Fanfare GUILD HALL card.
Play-test each step; save backed up + `SuspendSaving()` as usual.
