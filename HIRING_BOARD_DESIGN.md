# Hiring board — pinned posters (build spec)

Mock (approved 2026-10-29, version 40; **revision 2 "pile" approved 2026-10-30, version 43**): https://claude.ai/artifact/9KCK7tHYgFzhRWQd8GMCd7

> **Revision 2 supersedes the cascade.** The section "Revision 2 — pile" at the end overrides anything above that it contradicts.
Replaces the newspaper stack in `Scripts/UI/Panels/Hiring/` (`HirePanel.uxml/.uss`, `Candidate.uxml`, the stack code in
`HireController`). Pools, rosters, hiring sign/badge/stopwatch, layer selector logic, `HireCandidate` routing, favours and
save restore are untouched.

## Concept
A cork notice board. Every candidate in the current roster is one paper poster pinned by a red tack. Posters cascade so
each one behind the front peeks its **header strip** (name · class · cost). The front poster is fully open. You go to the
next by **pulling the front poster off its tack** (it goes to the back); clicking a peeking strip brings that poster to the
front. **Hire tears the poster off** the board, leaving a scrap under the tack. No prev/next buttons, no "3/5" counter.

## Geometry (UI Toolkit px at the 1920×1200 reference; all numbers from the mock)
- Board root 800×1000, centred where the newspaper stand was. Frame: `Sprites/UI/Main UI/Box.png` 9-slice, fill
  `#5a3a22`, 6px dark border `#2b1a12`. Cork: 700×874 at (50, 76) inside the board, flat `#b4885a`, 4px border `#3d2616`
  (the mock's dot pattern is optional; a flat fill is fine).
- Tabs top-left at (60, 24): Adventurers / Porters as wooden signs — restyle the existing `TabView` headers:
  Blackletter 30px `#f3e3c3` on `#6b4426`, 4px `#2b1a12` border, inactive `#4a3222`/`#b89b7a`, two 8px nail dots.
- Depth selector top-right at (right 60, top 26): `◀  Depth 2  ▶` only (no "Posting to"), `#efe3c4` box, 4px dark border,
  BrightLinger 28px for the depth label. Same buttons/handlers as today.
- Stack container 660×840 at (70, 96). Poster **i** (0 = front) sits at `left = i × 36`, `top = 328 − i × 82`,
  z-order front on top (front = last sibling). Each poster keeps a persistent random rotation in ±1.5° (reuse `rotationByCard`).
  Animate `top`/`left` with a 0.25 s transition (USS `transition-property: top, left, rotate`).
- Restock note bottom-right of the cork at (530, 830), 200 wide, `#efe3c4`, 3px `#a98c5c` border, rotate 2°, red tack on
  top, above the posters: line 1 "`{n}` notices pinned" (or "Board empty"), line 2 "New notices in `m:ss`" (BrightLinger 26px
  for the time; drives off the existing refresh countdown that `empty-timer` shows today). Remove the old `text-container`
  empty state.

## Poster (`Poster.uxml`, 440×520)
Background `Sprites/UI/Main UI/Paper/1.png` stretched (`-unity-background-scale-mode: stretch-to-fill`). Content is inset
inside the paper's inner border.
- **Tack**: 22px circle at top centre (`top: −10px`), `#c8362b` fill, 3px `#2b1a12` border, lighter top-left / darker
  bottom-right inset (two nested elements or a tiny sprite). It is a `Button`; hover scales 1.15. Click on the front poster
  = pull off → `RotateTopToBottom()`.
- **Header strip** (every poster): 56px tall, `margin-top 24`, `padding 0 34 0 60`, row, 10px gaps. Hover tint
  `rgba(120,80,40,0.12)`. Click on a *back* poster = bring to front (reorder `CurrentRoster`/`CurrentCards` so it is index 0,
  then `UpdateStackVisuals`). Children, left→right:
  - **Rank gem** — absolute on the poster, `left −22, top 16`, 72×72, `Sprites/Icons/ranks/Rank 1.png`
    (`-unity-background-scale-mode: scale-to-fit`), tinted by `CandidateUIMapper.TierTint(rank)` for rank ≥ 2, untinted for 1.
    Rank numeral centred: Ithaca 32px white, `-unity-text-outline` 2px `#2b1a12`. rank = `max(startRank, def.rank)`.
  - First name only (`identity.firstName`): Blackletter (`TextMesh Pro/Fonts/NotJamBlkltr13.ttf`) 26px, `flex-grow 1`, ellipsis.
  - Class icon 28px (`AdventurerDef.classIcon`; porters: hide).
  - Class name 22px `#6b4426`, width 84 (`def.displayName`).
  - Cost: BrightLinger 24px + 22px coin (`Main UI/Copper Coin.png` first frame). Red `#a8322a` when unaffordable.
- **Body** (front poster only; `display: none` on back posters), `padding 14 36 0`, column, 12px gaps:
  - **Portrait row** (row, 16px gap, `position: relative`):
    - Epithet banner (only if `identity.epithet` non-empty): absolute `left −4, top −20` of the row, so it straddles the
      portrait's top edge right under the strip; rotate 2.6°; sized to its text (`flex-shrink 0`, no fixed width).
      Same build as the Roster detail page: `Book UI/Custom/epithet.png` 9-slice (12/12/2/2, slice-scale 2px) as fill,
      a second copy tinted `--color-box-border` offset 4px down as the shadow; text Ithaca 22px `#2b1a12`, left-aligned.
    - Portrait box 150×150: `#86c7e6` sky, 6px border `#c3895d` (TierTint for rank ≥ 2), overflow hidden,
      `candidate.GetShopSprite()` bottom-centred at 120px wide, grass strip 22px `#5e9c3a` with 3px `#3f6f27` top edge.
    - Stats column (centred vertically, 6px gaps): flavour line italic 20px `#6b4426` (`identity.description`, fallback
      `def.description`); `Damage` 22px + BrightLinger 40px value + `/ s` 18px; `Health` 22px + BrightLinger 40px value.
      Porters: `Carry` (carryCapacity) and `Speed` (moveSpeed, 1 decimal) instead.
  - **Trait chips** row (wrap, 8px gap): per trait `displayName`, Ithaca 22px, `padding 3 12`, 3px border `#6b4426`
    text `#4a3220`, bg `#f7eed8`; cursed combo → border and text `#a8322a`. Hidden when no traits.
  - **Stat bars** (column, 9px gaps), one row per stat: 32px icon + bar. Adventurer rows in order Damage / Attack speed /
    Reach / Speed = `AttackDamage / AttackSpeed / AttackRange / MoveSpeed`; porter rows Speed / Carry / Pickup =
    `MoveSpeed / CarryCapacity / PickupTime`. Bar = 10 cells, 20px tall, 3px gaps, each cell 2px border `#4a3220`, covering
    0.5→1.5 in 0.1 steps; a 3px×30px `#2b1a12` tick at the 1.0 boundary. Cell colours for multiplier `m`:
    `m > 1`: cells in [1.0, m] green `#4f9f3f`; `m < 1`: cells in [m, 1.0] red `#b23a2e` (the mock hatches it; solid is fine);
    cells below `min(m, 1)` tan `#a98c5c`; the rest `#e2d2ad`. Clamp to the 0.5–1.5 range.
    **`m` = product of the candidate's trait-tier `Mult` modifiers for that stat** (`traitDef.tiers[tier-1].modifiers`,
    `TraitDatabase.GetTrait`); an `Add` modifier contributes `(base + value) / base`. No percentage labels.
    Icons: `Assets/Sprites/Icons/Stats/` — `stat_damage.png` (sword, cropped from the Book UI Icons sheet), `stat_aspd.png`
    (bolt), `stat_reach.png` (arrow), `stat_speed.png` (boot) — copy the four 16×16 PNGs from
    `/private/tmp/claude-501/-Users-tuan-Documents-Game-Idle-Merchant-Guild-Project-Idle-Merchant-Guild/18d94d37-ee3a-4dbe-a8b2-45cf00c684c7/scratchpad/hiremock/src/`
    (they are dark `#4a3220` on transparent). Porters reuse speed for Speed and need two more (carry = crate, pickup = hand)
    — draw them in the same 16×16 single-colour style if quick, else reuse `Loot/.../Misc/Crate.png` scaled and the boot.
  - **Hire**: `Button.uss` `btn-success`, Ithaca 30px, "Hire `4,200`" + 26px coin, centred. Unaffordable / depth full /
    type full → `btn-disabled` "Hire for `N`" plus a tag under it: BrightLinger 20px `#f5ead6` on `#a8322a`, 3px `#2b1a12`
    border, rotate −3°, text `NEED {cost − gold} MORE` (gold short) or `DEPTH FULL`. Keep the existing
    "Below this depth's rank" warning logic (show it as a thin red strip over the portrait box like today).
- Big numbers: `Damage/s = def.attackDamage × damageMultiplier × m(AttackDamage) ÷ (def.attackInterval ÷ m(AttackSpeed))` —
  check how `StatsMediator` applies an `AttackSpeed` Mult (whether 1.3 means faster) and match it.
  `Health = AdventurerDef.MaxHealth` (× any MaxHealth trait mult if such a StatType exists). Format with thousands separators.

## Interactions
- Pull off (tack click on the front poster) → front poster goes to the back of the cascade (existing `RotateTopToBottom`).
- Strip click on a back poster → that poster comes to the front; the others keep their relative order.
- Hire → poster tears off: 0.35 s, translate up ~140px with a −12° rotate and fade to 0, then removed; a **scrap** stays:
  a 46×34 torn-paper piece (make a tiny paper-scrap sprite, or a 3-cell polygon of paper colour `#efe3c4` built from two
  rotated rectangles) with a tack on top, placed at `left 20 + k × 60, top 8` of the stack container, alternating ±7°,
  one per hire since the panel was built (cleared by `BuildStack`). The hired candidate is consumed exactly as today.
- Keyboard/close/Esc, tabs, layer prev/next, badge, stopwatch: unchanged.
- Sorting: front poster is pickable; back posters pick only on their strip and tack (set `pickingMode` on the body).

## Files
- New: `Scripts/UI/Panels/Hiring/Poster.uxml`, `Poster.uss` (styles), `Assets/Sprites/Icons/Stats/*.png`.
- Rewrite: `HirePanel.uxml`, `HirePanel.uss`, `CandidateUIMapper.PopulateUI` (populate the poster; keep `TierTint`),
  `HireController.BuildStack / UpdateStackVisuals / AnimateHire / UpdateHireButtonState / UpdateCount` (count → note).
- Delete/retire: `Candidate.uxml`, `CandidateUSS.uss`, prev/next buttons, `counter-container`, `text-container`
  (move to `~/.Trash/`, never hard-delete; update `MainUI.uxml` references if any).
- Fonts via USS rules: `--font-body` for Ithaca; BrightLinger and NotJam urls in `Poster.uss` rules (inline `var()` is an NRE
  in UITK). No `gap` (margins), no `box-shadow`/`text-shadow` (fake with offset layers / `-unity-text-outline`).
- `HirePanel.uxml` is instanced in `MainUI.uxml`; keep the `hire-panel` root name and the `hiring-badge`/`hiring-stopwatch`
  elements the sign uses.

## Verification
- Play in the editor via Unity MCP after backing up `save.json` + `.bak` and calling `SaveManager.Instance.SuspendSaving()`;
  save must be byte-identical afterwards.
- Open the board at depth 1 and 2, Adventurers and Porters: five posters cascade with all strips readable; tack click cycles;
  strip click brings forward; hire tears off and leaves a scrap; unaffordable shows the tag; epithet banner only when the
  candidate has one; bars match the trait multipliers (log one candidate's mods vs cells); no console errors.
- Render the board to a PNG from a script (no super-sized `ScreenCapture`; the Game view is collapsed) and show it.

## Revision 2 — pile (version 43, 2026-10-30)
Play-testing showed the cascade confusing (five strips, two gestures). Posters now sit in **one pile**, browsed one at a time.

- **Board 700×820** (artboard). Frame 660×780 at (20, 20); cork 600×694 at (50, 76). Tabs unchanged at (60, 24). The panel
  goes back under the top nav at its old `top` (no −132 lift): 820 fits the 1200 reference; it must not cover the
  guild-name banner.
- **No depth selector on the title bar.** It becomes a pinned paper slip at the cork's bottom-left: `.note` look
  (`#efe3c4`, 3px `#a98c5c` border, red tack on top, rotate −2°), 210 wide, artboard (125, 671), padding 26 10 14, row
  centred: `◀` button · "Depth N" BrightLinger 28 · `▶` button (same handlers as today, Ithaca 26 arrows `#6b3d1e`).
  **Restock note** moves beside it: artboard (375, 671), 200 wide, rotate +2°, padding-top 26, same two lines as before.
- **Prev / next arrows**: two 44×64 wooden buttons on the cork at artboard `left 64` and `right 64`, `top 340`
  (poster mid-height): bg `#6b4426`, 4px `#2b1a12` border, hover `#7d5230`, Ithaca 30 `◀` / `▶` in `#f3e3c3`,
  inset sign bevel like the tabs. **▶ and the tack both = next**, **◀ = previous**.
- **Pile geometry**: stack container 560×654 at (70, 96). Poster i (0 = front) at `left 60 + 4i`, `top 16 + 4i`, front
  = last sibling, tilt per poster kept (`rotationByCard`). Back posters: body hidden as now **and the whole poster
  `pickingMode = Ignore`** — no strip click, no strip hover tint, delete `OnPosterClicked`'s strip path.
- **Next animation** (`.poster--pulled`): front poster translates (160, 60), rotates +12° on top of its tilt, opacity →
  0 over 0.28 s ease-in (USS transition on translate/rotate/opacity); when it ends, `RotateTopToBottom` (poster to the
  back, class removed, transitions suppressed for that frame so it doesn't fly back). **Previous** (`.poster--arriving`):
  move the last poster to the front with the same offset/opacity and `transition-duration: 0` applied, then next frame
  drop the class so it slides in over 0.28 s. Ignore arrow/tack clicks while a slide is running. Hire tear-off unchanged.
- **Scraps**: `left 70 + 60k`, `top 0` of the stack container, *under* the pile (scraps layer stays below the stack).
- **Stat bars are one colour**: cell k (covering 0.5+0.1k → +0.1) is filled `#6b4426` iff its upper edge ≤ m, else
  `#e2d2ad`. Drop the green / red / tan states; the 1.0 tick stays. Time-stat inversion stays.
- Everything else (poster content, banner, gem, hire button + tags, tabs, badge, stopwatch, roster size 5) unchanged.
