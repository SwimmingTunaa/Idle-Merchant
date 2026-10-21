# Adventurer Classes — Design Doc

Status: **phase 2 built** (2026-10-21) — see "Phase 1 as built" and "Phase 2 as built" at the end · Owner: Tuan

## Why

- **Promotion becomes a choice, not a stat bump.** Today Wood → Gold is five identical steps. A class pick at the first promotion, then a named advance each rank, gives every promote a moment.
- **Depth pays off on the hire board.** Unlocking a layer raises the ceiling of what can turn up for hire, so going deeper visibly upgrades your recruiting.
- **It uses art and UI already built.** The job-icon sheet (`job_icons_singleoutline.png`: novice, warrior, tank, archer, wizard, healer, monk + 43 unnamed) and the Roster's class badge, which today is hard-coded to "Novice".
- **It fixes the deep-layer power gap.** Ranks currently add +20% damage / +25% HP each (Gold ≈ 2× a Novice); layer 5 mobs have ~75× layer 1's HP. Class tiers carry the real power curve.

## Class lines

Novice is the classless Wood tier. At the first promotion (Wood V → Bronze) the player picks a line; every later promotion advances along it.

| Line | Bronze | Iron | Silver | Gold | Role |
|---|---|---|---|---|---|
| Warrior | Warrior | Knight | Paladin | Champion | balanced melee |
| Guardian | Guardian | Defender | Bulwark | Juggernaut | high HP, low damage |
| Rogue | Rogue | Assassin | Shadowblade | Nightstalker | fast strikes, fragile |
| Mage | Mage | Sorcerer | Wizard | Archmage | heavy hits, % max-HP damage, fragile |

Four lines to start. Healer and Monk are reserved for later (a healer needs new targeting AI).

Levels inside a rank (I–V) work as today: XP auto-levels, small per-level bumps. Rank-ups stay gold-gated.

## The hire board

**Tiers unlock with layers.** Unlocking layer N lets rank-N class hires appear:

| Unlocked | Can appear |
|---|---|
| Layer 1 | Novice |
| Layer 2 | + Bronze classes |
| Layer 3 | + Iron classes |
| Layer 4 | + Silver classes |
| Layer 5 | + Gold classes |

This is the gate the pools already apply (`def.rank <= maxUnlockedLayer`).

**Odds.** Novices stay the bulk; each higher tier is rarer. Weights over the unlocked tiers: Novice 50 · Bronze 25 · Iron 13 · Silver 8 · Gold 4 (normalised to whatever is unlocked). For 10 minutes after a layer unlocks, its tier's weight doubles so the player sees the payoff straight away. Line is uniform within a tier.

**One board for all layers.** A hired Knight can go to any layer; the layer selector still picks the destination.

**Price — a premium for skipping promotions.** A class hire costs the Novice price plus the promotions it skips, × 1.4. The ×1.25-per-owned scaling applies on top.

| Tier | Skipped promotions | Hire price |
|---|---|---|
| Novice | — | 1,500 |
| Bronze | 1,500 | 4,200 |
| Iron | 5,250 | 9,450 |
| Silver | 14,625 | 22,575 |
| Gold | 38,062 | 55,387 |

(Promotion cost today: 1,500 × 2.5^(rank−1) → 1,500 / 3,750 / 9,375 / 23,437.) Growing your own is always cheaper; buying saves the gold premium's worth of time.

## Power curve

Target: a unit at rank N works layer N comfortably (~4 hits to kill an average mob there, survives ~5 hits).

| Layer | Avg mob HP | Avg mob attack | Damage needed | HP wanted |
|---|---|---|---|---|
| 1 | 40 | 50 | 50 | 200 |
| 2 | 370 | 60 | ~95 | ~330 |
| 3 | 900 | 110 | ~225 | ~550 |
| 4 | 1,270 | 87 | ~320 | ~450 |
| 5 | 3,000 | 200 | ~750 | ~1,000 |

Multipliers on the Novice base (50 damage, 200 HP), per tier, before per-level bumps:

| Tier | Warrior dmg / HP | Guardian dmg / HP | Rogue dmg / HP | Mage dmg / HP |
|---|---|---|---|---|
| Bronze | 2.0 / 1.7 | 1.2 / 3.0 | 1.8 / 1.2 | 2.5 / 1.1 |
| Iron | 4.5 / 2.8 | 2.7 / 5.0 | 4.0 / 2.0 | 5.6 / 1.8 |
| Silver | 6.5 / 3.0 | 3.9 / 5.5 | 5.8 / 2.1 | 8.1 / 1.9 |
| Gold | 11 / 5.0 | 6.6 / 9.0 | 9.8 / 3.5 | 13.8 / 3.2 |

Rogues attack 1.4× as often (`attackInterval` 0.71) for lower damage per hit; Mages attack slower (`attackInterval` 1.25) for heavier hits — same base DPS as the Warrior. Phase 2 retuned Mage and Gold (see "Phase 2 as built"). The old flat rank multipliers (+20% / +25%) are replaced by these tables. Numbers are starting points for playtest.

A weak unit in a deep layer is allowed — it just struggles (dies and revives). The assign/hire screen shows a "below this layer's rank" warning.

## UI

- **First promotion:** the Roster's Promote on a Wood V Novice opens a "Choose a path" picker — four cards (class icon, name, role line, damage/HP/range arrows). Picking confirms the gold spend.
- **Later promotions:** the button reads "Promote → Knight 3,750 🪙".
- **Roster detail:** the class badge shows the real icon and class name.
- **Hire cards:** class icon + class name + rank badge; tiers above Novice get a subtle frame tint so a Gold find pops.
- **Look:** phase 1 keeps today's character sprites; the class reads through icon + name. Class weapons/outfits come later.

## Data & save

- New `ClassLine` enum (None/Novice, Warrior, Guardian, Rogue, Mage) and one `AdventurerDef` per line × tier (16) plus the Novice — each carries line, tier (= rank), stat multipliers, icon, display name. The four unused `Lvl 2–5 Adventuer Temp` defs are deleted.
- A `ClassCatalog` ScriptableObject lists the hireable defs once; every layer's AdventurerManager hires from it (replaces per-manager `hireableDefs` for adventurers).
- The adventurer stores its line; promotion swaps to the next tier's def. **Def asset names are save keys** — the roster saves the current tier def, so names are final once shipped.
- Old saves: existing adventurers are Novices with their current rank; ones already above Wood get a free one-time path pick the first time their Roster detail opens.

## Phases

1. **Classes core:** lines + 17 defs + catalog, tier-gated weighted board with premium pricing, first-promotion picker, named promotions, class badge/name everywhere, power-curve tables, save + migration. Fill layers 2–5 hire lists via the catalog.
2. **Feel:** class signature mechanics (Warrior cleave, Rogue crits + post-kill dash, Mage bonus vs high-HP mobs), Mage VFX, Guardian holding aggro, class weapon/outfit sprites.
3. **More lines:** Healer and Monk.

## Open

1. Odds and the 10-minute unlock boost — tune after a playtest.
2. Porters stay one def for now (layers 2–5 porter managers just get `Porter lvl 1`).

## Phase 1 as built (2026-10-21)

Built as specced: `ClassLine` + 17 defs (`Lvl 1 Novice` + `Prefabs/Entities/Adventurers/Classes/<Tier> <Class>`), `Resources/ClassCatalog`, tier-gated weighted board with the unlock boost and premium pricing, "Choose a path" picker, named promotions, class icon/name on the Roster badge and hire cards (tier-tinted frame + badge), "Below this layer's rank" warnings (Roster detail + hire card), power-curve multipliers, save + migration. The four `Lvl 2–5 Adventuer Temp` defs are deleted (layer 2–5 adventurer managers' `unitLimits` pointed at them — removed; older scenes `Main Level 1.0` / `Old Main` / `1.11` keep dangling refs). Verified: odds over 3,000 rosters (all tiers unlocked) 49.7 / 25.0 / 13.3 / 8.0 / 4.1 %; an Iron Knight hire (Iron I, 308–320 dmg with level bumps) kills an average live layer-3 mob (~1,130 HP) in ~4 hits.

Deviations / decisions:
- **Gold hire price is 55,388**, not 55,387 — promotion costs are rounded per step before the ×1.4.
- **No separate "pending pick" save flag.** A Novice def above Wood *is* the pending state (it can't arise any other way), so it persists for free; pre-classes adventurers keep `Lvl 1 Novice` as their save key.
- **Recruitment Favor** still grants a Novice at rank stars+1 (min Bronze); it's hired as a Novice above Wood and therefore gets the free path pick. Favors restore by def name (pools are per catalog def now).
- **Mage splash = the existing AOE skill as a periodic aura** (`Mage Splash`: 25% of attack damage every 2 s within 2.5 m, Enemy layer only), not on-hit splash. `AOEDamageSkillDef` gained `attackDamageFraction` so it scales with tier. Adventurers now actually apply `startingSkills` (nothing did before).
- **Archer → Rogue (before shipping).** Range turned out meaningless because a mob is almost always in reach, so the line became the Rogue (from the older ADVENTURER_CLASS_SYSTEM.md): Rogue / Assassin / Shadowblade / Nightstalker, melee range, 1.4× attack speed, damage multipliers ÷1.4 so DPS is unchanged. Same `ClassLine` value (3), same asset GUIDs (renamed). Crits + post-kill dash are phase 2.
- **Picker arrows** compare each line to the same tier's Warrior (1 = less, 2 = about the same, 3 = clearly more) so they still read at Iron+ free picks; the "Splash" lives in the Mage role line.
- **Promote label** uses "»" ("Promote » Knight 3,750") — the body font has no →.
- **Per-rank attack-speed step (×0.9) is kept**; only the flat damage/HP rank multipliers were replaced.
- **Unlock boost isn't saved** — quitting inside the 10-minute window drops the rest; a save load's replayed unlock signals don't start one.
- **HireRoster now draws without replacement** — the old re-roll-on-duplicate leaked the shallow Novice pool's share to the 4× deeper class tiers (measured 57/43 instead of 67/33 with two tiers).
- Promoted units stay tracked under their hire def in `UnitManager.spawnedByType` (harmless: adventurer managers have no per-type limits).

## Phase 2 as built (2026-10-21) — signature mechanics

Every number lives on the tier `AdventurerDef` ("Signature" headers); a mechanic is on iff its number is > 0, so the line is just whichever fields its defs fill. Hit resolution is `AdventurerAgent.ResolveHit` (wired as `CombatBehavior.HitOverride`); the kill hook (XP) is `OnCombatDamageDealt`.

| Line | Mechanic | Bronze | Iron | Silver | Gold |
|---|---|---|---|---|---|
| Warrior | **Cleave** — every hit also deals X% of the hit to each other live mob within R of the primary target (plain `Health` damage, so splash never chains; splash kills give XP) | 40% · R 1.5 | 45% · 1.5 | 50% · 1.75 | 60% · 2.0 |
| Guardian | **Hold the line** — every 1 s, mobs within R whose target is another adventurer switch to the Guardian (skips mobs already on another Guardian, so two don't ping-pong); takes Y% less damage (`Health.DamageTakenMultiplier`) | R 4 · 15% | 4.5 · 20% | 5 · 25% | 6 · 30% |
| Rogue | **Crits** C% for ×M. **Pursuit** — after a kill, a 0.12 s lerp to the nearest live same-layer mob within 3 (lands at ½ attack range, clamped to the wander area; no target = no dash); the next hit is a guaranteed crit | 20% ×2 | 25% ×2.25 | 30% ×2.5 | 35% ×3 |
| Mage | **Arcane weight** — each hit adds P% of the target's *max* HP; `attackInterval` 1.25; `Mage Splash` removed from the defs (asset kept) | 2% | 3% | 4% | 5% |

Feedback: crits = big gold number (24 pt, 1.4× lifetime); cleave splash = small grey numbers + a white/gold slash arc at the primary target (only when something was splashed); Mage bonus = violet "+N" after the hit number; pursuit = dust puff at the start + faint dust trail; taunt = a "!" over the switched mob. Particles via `ClickHitVFX` (`PlayCleave`, `PlayDust`, sorting 10000).

UI: each tier def's `description` (role line) names its mechanic — "Cleaves nearby mobs" / "Draws aggro, takes less damage" / "Crits and pursues" / "Heavy hits, % max-HP damage". The path picker shows it; Roster detail leads the description box with it in bold (runtime UI Toolkit has no tooltips).

### Rebalance

Live average mob HP (spawn-weighted, × the layer curve): L2 420 · L3 1,179 · L4 1,860 · L5 4,101 — higher than the phase-1 table. Damage per hit at rank N, level I (incl. the earned +4%/level bumps, ×1.17 / ×1.37 / ×1.60 / ×1.87), and hits to kill that average mob before procs:

| Rank → layer | Warrior | Guardian | Rogue | Mage (+ % max HP) |
|---|---|---|---|---|
| Bronze → L2 | 117 · 3.6 | 70 · 6.0 | 105 · 4.0 | 146 · 2.9 (2.7) |
| Iron → L3 | 308 · 3.8 | 185 · 6.4 | 274 · 4.3 | 383 · 3.1 (2.8) |
| Silver → L4 | 520 · 3.6 | 312 · 6.0 | 464 · 4.0 | 649 · 2.9 (2.6) |
| Gold → L5 | 1,030 · 4.0 | 618 · 6.6 | 918 · 4.5 | 1,292 · 3.2 (2.7) |

- **Gold tier damage cut ×0.73** for every line (Warrior 15 → 11, Guardian 9 → 6.6, Rogue 13.4 → 9.8): with the level bumps Gold one-shotted the table at ~2.9 hits. HP unchanged.
- **Mage** multipliers = Warrior × 1.25 (2.5 / 5.6 / 8.1 / 13.8) so base DPS matches the Warrior at the slower interval; the % max-HP bonus is the upside (a levelled Iron Sorcerer hit a debug-spawned Demon Lord on L3, 7,222 HP, for 431 + 217 vs the Knight's 333).
- **Rogue** keeps its ratio to the Warrior (0.9× per hit at 1.4× speed); crits add ×1.2 / 1.31 / 1.45 / 1.7 expected damage, so it is the single-target DPS line. **Guardian** keeps its HP and ~0.6× damage; the damage cut and aggro are its job.
