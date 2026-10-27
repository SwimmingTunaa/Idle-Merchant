# Guild Star Fanfare — build spec

Mock (approved 2026-10-27): https://claude.ai/artifact/RY1VQoUSbfCCt6dW7158d5
Companion: the guild-hall growth mock https://claude.ai/artifact/LGqaWvEXdzG6T8PYz7rU6S (phase 2 hooks in here).

## Goal
A star is the biggest beat in the game and today it ticks an icon, plays `SfxId.StarFanfare` and silently adds seals.
Make it a ~5 s ceremony that (1) is unmissable, (2) says exactly what changed, (3) leaves a trace for anyone who looked away.
Nothing pauses. World clicks keep working.

## Trigger & facts (what already exists)
- Stars come only from claiming a milestone (`ProgressionManager.AwardStar`, called from the claim path). So a star is always
  earned **live, by a player click** — there is no offline case to defer. A star earned while the book is open plays over the book.
- `AwardStar(star)`: unlocks depth `star + 1` (`UnlockLayer`), `GrantStarReward(star)` (per-star reward list), grants
  `StarSeals[star-1]` = 30/50/80/120/200, then raises `ProgressionManager.OnStarEarned` and `GameSignals.RaiseStarEarned`.
- `SfxManager` already plays `StarFanfare` off the signal — keep it; the visuals sync to it.
- HUD pennant: `TopNavigation.uxml` `#stars-container` (five `.star` elements, `.star-filled/.star-empty`) + `#guild-flag`.
- Right-side tracker: `UI/Panels/Milestone Tracker/MilestoneTracker.cs` (already hosts a special `contractRow`; has a `Coin` scheduler-lerp pattern).
- Class tiers unlock by `rank <= maxUnlockedLayer` (`ClassCatalog`), so depth N opening = tier-N classes on the hire board.
- Hall props (phase 2) are `ProgressionActivator`s with `ActivationTrigger.StarEarned`.

## The unlock list (gathered at fire time, in this order; a card is skipped when its source is empty)
1. **DEPTH** — `"<depth name> opened"` using the same depth-name lookup the Roster uses (`RosterPanelController` ~L213 fallback). Icon: that depth's background thumbnail or the layer icon.
2. **HIRING** — `"<Tier> classes on the board"` where Tier = the tier name whose `rank == newly unlocked depth` (from `ClassCatalog`). Icon: that tier's first class icon.
3. **SEALS** — `"+<n> Guild Seals"`, `n = StarSeals[star-1]`. Icon: wax seal.
4. **REWARD** — only if `GrantStarReward` paid something (gold/other): `"+<amount> gold"`. Icon: coin.
5. **GUILD HALL** (phase 2) — names of every `ProgressionActivator` whose trigger is `StarEarned` and `requiredValue == star`, joined with ` & `; e.g. `"Silver crest & chandelier"`. Icon: flag banner. Phase 1 ships without this card.

Cards are **tag + icon + title only** (no sub-line). Max 4 shown; if 5 qualify, drop REWARD into the tracker summary.

## Sequence (timings from the mock; all unscaled time, independent of `GameManager` time scale)
| t | what |
|---|---|
| 0.00 | edge vignette fades in (0.35 s). SFX already fired by `SfxManager`. If the star came from a tracker/book claim, start the whole sequence **0.4 s after** the claim so the coin shower lands first. |
| 0.20 | ribbon slams in: scale 2.2→0.96→1.0, 0.5 s, overshoot curve; 28 sparks burst from its centre (see Sparks). Text: NotJam 46 px `Guild Star Earned`; sub-line Ithaca body `"<Ordinal> of five · the guild is <RankWord>"`. |
| 0.45 | big star (96 px, `Star_active`) spins in above the ribbon (0.45 s). |
| 0.90 | big star flies to the pennant's next star slot (0.55 s, ease-in-out), shrinking to 32 px and fading. |
| 1.40 | pennant star N swaps to `.star-filled` with a pop (scale .3→1.45→1, 0.55 s, brightness flash); `#guild-flag` wiggles (reuse `UIManager.SetBoardBadge`'s tilt-wiggle, ~0.9 s); 14 sparks at the slot. |
| 1.30 / 1.60 / 1.90 / 2.20 | unlock cards stamp in one by one (scale 1.8 rot −6° → 0.94 rot 1° → 1, 0.32 s). |
| 2.60 | hint "click to continue" fades in under the cards. |
| 4.60 | **fold**: ribbon + cards shrink and fly toward the tracker (0.5 s), vignette out, tracker card slides in (0.4 s). |

**Skip**: a click on the ribbon or any card jumps to the fold. The overlay host is `picking-mode="Ignore"`; only the ribbon
and cards pick, so world clicks (mob damage) still land during the 5 s.

**RankWord** by star: 1 Founded · 2 Known · 3 Established · 4 Renowned · 5 Legendary. (Pure strings in the fanfare class; reuse later for the hall crest.)

### Sparks
No particle system in UITK. Reuse `MilestoneTracker.Coin`'s scheduler-lerp: N 10 px square `VisualElement`s (palette `#ffe07a #f0b429 #fff4c8 #ff8a3a`),
random angle, distance 120–380 px, y squashed ×0.6, 0.9 s, scale 1.3→0.2, opacity 1→0, then removed. One `IVisualElementScheduledItem` drives all of them.

## Tracker card (the trace)
- `MilestoneTracker` gets a `starRow` like `contractRow`: parchment card, red border, `★ <Ordinal> Guild Star earned`, one-line summary built from the same unlock list (`Deep Caverns · Bronze classes · +80 seals`), a **Seen** button (secondary style).
- Sorted first (group −1 above claimables). Stays until Seen is clicked. No reward on dismiss — seals were already granted by `AwardStar`.
- Persist: `SaveData.progression.unseenStar` (int, 0 = none). Set when the fanfare fires, cleared on Seen. On load, if > 0 rebuild the row (no fanfare replay). Follows the SaveData rule: fill in `Collect`, restore in `ApplyLate`, re-raise nothing new — the tracker just reads it in its refresh.

## Files
- **New** `Assets/Scripts/UI/Panels/Star Fanfare/StarFanfare.uxml / .uss / StarFanfare.cs`
  - `StarFanfare : MonoBehaviour` (same shape as `WorkshopPanelController`/`ClassPickPanelController`: finds its root in the `UIDocument`, subscribes `GameSignals.OnStarEarned` in `OnEnable`, unsubscribes in `OnDisable`).
  - Instance is the **last child of `main-root`** in `MainUI.uxml` (after `ClassPickPanel`) so it draws over the book, HUD and tracker.
  - All motion = USS transitions toggled by classes + `schedule.Execute` chains; the star flight and sparks are scheduler lerps. No coroutines (unscaled time, survives time scale 3).
  - Public `static void PlayDebug(int star)` for the Game Debugger (Economy/Progression tab: "Play star fanfare N").
- **Edit** `MilestoneTracker.cs/.uxml/.uss` — `starRow`.
- **Edit** `SaveData` + `SaveManager.Collect/ApplyLate` — `unseenStar`.
- **Edit** `TopNavigation.uxml` — none needed; the fanfare queries `#stars-container` children and `#guild-flag` by name.
- **Art**: reuse `ribbon.png` (Class Pick), `card-torn.png`, `Star.png` (`Star_active`), `wax-seal.png`, `Copper Coin`, `Flag_banner.png`, class icons, depth thumbnails. No new sprites.
- Fonts/sizes: ribbon title NotJam 46; everything else `--font-size-body/-small` tokens (never below 18).

## "Depth" rename (player-facing strings only — code keeps `layer` / `LayerIndex`)
- `HirePanel.uxml` `#layer-label` text `"Dungeon Level 1"` → runtime `"Depth {n} · {name}"` (set from `HireController`).
- `RosterPanelController.cs` ~L213 fallback `$"Layer {layerIndex}"` → `$"Depth {layerIndex}"`.
- `ContractManager.cs` L25 `"Cull {target} mobs on layer {layer}"` → `"… at Depth {layer}"`.
- `ProgressionActivator.cs` L164 label `$"Layer {requiredValue}"` → `$"Depth {requiredValue}"`.
- Milestone defs: `Milestone_4Star_LogisticsNightmare` ("materials from multiple layers" → "depths"), `Milestone_4Star_JugglingAct` and `Milestone_5Star_TopToBottom` ("dungeon layers" → "depths").
- `Roster_Page.uxml` / `Candidate.uxml` "Below this layer's rank" → "Below this depth's rank".
- Leave `GameDebugger` and `[Header]` texts alone (editor-only).

## Out of scope (deliberate)
- Milestones keep their existing ding + tracker card; **only stars** get the ceremony.
- No camera drift to the hall; that comes with the hall-growth build (phase 2), where the GUILD HALL card and prop pop-ins land.
- No new progression track, no "guild level". RankWord is a label on the star count.

## Build order
1. `StarFanfare` overlay with hard-coded unlock list → verify timing against the mock with `PlayDebug` (Unity must be open; bridge via the uvx command in memory).
2. Real unlock list (depth name, tier name, seals, reward).
3. Tracker `starRow` + `unseenStar` save.
4. Depth rename pass.
5. Play-test: claim the milestone that awards 2★ from the tracker and from the book; confirm world clicks still hit mobs during the fanfare; skip; quit mid-fanfare → tracker row is back on load; save backed up + `SuspendSaving()` as usual.
