# Tutorial — design

**Status:** built 2026-11-09 (`Scripts/Tutorial/TutorialManager.cs`). Deviations from the draft below: the step table is code, not `TutorialStepDef` assets (one designer, copy edits are a recompile); triggers and done-conditions are polled twice a second plus a handful of session flags (`TutorialManager.Flag`) at the verbs, no new signals; the Workshops note is done by opening the Workshop panel (recipes are pinned on unlock, so "a recipe pinned" was already true); the cold-open camera uses the existing `JumpTo` ease; the first-open board string is the restock note's count line; Show me never scrolls inside a page, it opens the tab.
**Companions:** `GDD.md` §8.2 (the 1★ chain is already "the implicit tutorial"), `STAR_FANFARE_DESIGN.md`, `HIRING_BOARD_DESIGN.md`, `CONTRACTS_DESIGN.md`.

## 1. The problem

A new guild starts with **0 gold and no units**. The only thing that moves the game forward in the first minute is the
player clicking a creature on depth 1. Nothing on screen says so. The milestone tracker shows "Defeat 15 dungeon
creatures", which names the goal but not the verb, and the camera opens on the shop, where there are no creatures.

After the first star the game adds a system roughly every few minutes (contracts, seals, workshops, caravan, events,
notices, promotions, depth moves, shelves and queue upgrades), each with its own panel and its own click. None of them
introduces itself. A player who misses the one wiggle or badge that announces a system can play for an hour without
opening it.

Two different problems, two different tools:

1. **The first three minutes** need a guided hand: the player must learn *click creature → click loot → loot sells
   upstairs → gold hires people*. Get this wrong and they quit before the game starts.
2. **Everything after** needs a *first-contact note*, not a tutorial: one line, one pointer, the first time a system
   appears, then never again.

## 2. Goals, non-goals, anti-goals

**Goals**
- Time from New Guild to first click on a creature: under 10 seconds for a player who has never seen the game.
- Every verb in the 1★ chain is shown *where it happens* (a pointer in the world or on the button), not described.
- Every later system gets exactly one introduction, at the moment it first matters, dismissed by doing the thing.
- A returning player never sees any of it again. A player who knows the game can turn it off.

**Non-goals**
- No scripted story, no mentor NPC with dialogue, no cutscene. The shopkeeper stays mute.
- No teaching of depth: traits, rank fit, class signatures, keep floors, crafting caps. Those have tooltips and
  faces already; the tutorial points at the panel, the panel teaches itself.
- No tutorial for Settings, the Pause menu, or volume.

**Anti-goals** (would make it a failure even if it "works")
- A modal that stops the world. The GDD's pillar is *watch the machine run*; a tutorial that freezes it teaches the
  wrong thing in the first minute.
- Dimmed-screen spotlight overlays. They read as a different game pasted on top of this one.
- A step that can be left half-done and nag forever. Every note must have a done-condition the game can detect.
- More than one line of copy per step. If the step needs a paragraph, the UI it points at is the thing to fix.

## 3. What already does tutorial work

Reuse before building. All of this exists:

| Existing piece | What it teaches | Where |
|---|---|---|
| 1★ milestone chain, ordered 1–6 | the core loop, one verb per card, each payout funds the next | `Resources/Milestones/1 Stars`, HUD `MilestoneTracker` |
| LMB glyph over the customer at the counter until 3 serves | click-to-serve | `ClickerManager.Serves`, `CounterService` |
| RMB glyph on hover until 3 inspects | right-click a unit for its card | `ClickerManager.Inspects` nameplate |
| Hiring sign coin badge + wiggle when a hire is affordable, "!" on restock | the board has something for you | `HireController`, `UIManager.SetBoardBadge` |
| Units bookmark badge + wiggle when a promotion is ready | go promote | `AdventurerManager.CountPromoteReady` |
| `PromoteIndicator` badge + arrow over a unit, fit-face thought bubble | this unit can rank up / is misplaced | world |
| Star fanfare "what changed" line | the new depth and reward | `STAR_FANFARE_DESIGN.md` |
| Event tracker cards + Caravan card, click = camera goes there | something is happening at the shop | `EventTracker`, `EventManager.FocusOn` |
| Milestone tracker header badge + wiggle when claimable | come claim | `MilestoneTracker` |

The gap is consistent: every one of these announces *that* something is ready, none says *what to do*. The tutorial
adds the verb and the pointer; it does not add new attention-grabbers.

## 4. Part one: the first three minutes

The 1★ chain stays exactly as it is (targets, rewards, order). We add a **pointer** per step and a **Show me** on the
active card. Beat by beat, as the player sees it:

### 4.0 Cold open
New Guild → the book cover → Continue. Today the camera sits on the shop. The shop is empty and nothing moves.

**Add:** on the first Continue of a new guild (`PauseMenuController.AfterContinue`), the camera eases down to depth 1
over ~1.5 s and stops with the spawner's mobs in frame. The milestone tracker's first card is already "Dungeon
Curious · 0/15". That's the whole opening. No letter, no title card. The movement itself says "the game is down here".

### 4.1 Defeat 15 creatures
**Pointer:** a bobbing LMB glyph (the TMP sprite the counter already uses) over the nearest living mob, re-targeted when
it dies, until **3 kills**. Same visual language as click-to-serve, so the second time the player meets it they
already know what it means.
**Card line:** "Click the creatures. You hit as hard as any hero, for now."
**Done:** 3 clicker kills. The glyph is gone before the milestone completes; the remaining 12 are the player's.

### 4.2 Collect 25 items
Loot drops the moment a mob dies, so this overlaps 4.1. Loot is collected by clicking it (or walking a porter past it,
but there is no porter yet).
**Pointer:** the LMB glyph moves to the oldest uncollected drop until **3 pickups**.
**Card line:** "Click the drops. Everything you pick up goes on sale upstairs."
**Done:** 3 click pickups.

### 4.3 Serve a customer
This is the step that loses people: the gold is made *upstairs*, and the player is looking *down*. Customers only buy
what is in stock, so this card can't complete until 4.2 has put something in the bin.
**Pointer:** none in the world. The card gets a **Show me** button: `JumpTo(counter)`. Once a customer is in the line,
the existing serve glyph takes over.
**Card line:** "Your loot is in the shop. Customers buy it on their own; click one at the counter to hurry it along."
**Done:** 1 customer served (the milestone itself).
**Also:** the first time stock lands in the bin after a cold open, the camera does *not* auto-jump. The Show me button
is enough; a camera that moves on its own twice in a minute feels haunted.

### 4.4 Earn 2,500 gold
Pure accumulation. Nothing to point at.
**Card line:** "Keep clicking downstairs, keep serving upstairs. Hires cost about a thousand."
**Done:** milestone.

### 4.5 Hire an adventurer
The sign already wiggles with a coin badge when affordable. Add the verb.
**Pointer:** top-bar hiring sign gets the LMB glyph beside the badge until the board is opened once.
**Card:** **Show me** opens the hiring board on the Adventurers tab.
**On the board, first open only:** the cork's restock note reads "Pull the tack to see the next poster. Hire when you
like one." instead of the countdown, until the first hire or the first ▶/tack pull.
**Done:** milestone.

### 4.6 Hire a porter
**Card:** **Show me** opens the board on the Porters tab.
**Card line:** "Porters carry loot up so you don't have to click it."
**Done:** milestone. Star fanfare follows; its "what changed" line already names depth 2.

### 4.7 After the star
The player has done every verb of the core loop with their own hands and now owns one of each unit. Part two takes
over. The clicker glyphs for kills and pickups never return (they are 3-count, like the existing two).

## 5. Part two: first-contact notes

One **note** per system. A note is a single torn-paper card pinned at the **top** of the milestone tracker list (same
component, same styling, no new HUD region), with one line of copy, an optional **Show me**, and a ✕. It disappears on
its done-condition or the ✕. One note at a time; later triggers queue behind it (a player who hits three firsts in one
minute reads them in order, not in a pile).

| Note | Trigger (first time ever) | Copy | Show me | Done |
|---|---|---|---|---|
| Seals | the 1★ fanfare ends (30 seals granted) | "Stars pay Guild Seals. Spend them on the Contracts page." | book tab 6 | Contracts page opened |
| Contracts | first offer appears | "The guild is being asked for things. Take one; it never expires." | book tab 6 | a contract accepted |
| Promotion | first `CountPromoteReady` > 0 | "Someone's ready to rank up. Open the Roster and promote them." | book tab 0 on that unit | a promotion |
| Choose a path | first Novice above Wood | "A Novice picks a class on their first promotion. No wrong answer." | the class picker | path chosen |
| Workshops | first station bought | "Your new workshop is in the hall. Click it and pin a recipe to a slot." | `JumpTo(prop)` + outline pulse on the prop | a recipe pinned |
| Crafting page | first recipe unlocked, if no station yet | "Recipes live in the book. Stations are bought on the Guild page." | book tab 3 | Guild page opened |
| Depth 2 | depth 2 unlocks | "A new depth. Your units stay where they are; drag a portrait in the Roster, or carry them with the mouse." | `JumpTo(depth 2)` | any move/swap |
| Rank fit | first unit at rank 2 below its depth, or first "slacking" | "The face over a hero says how the depth fits them. Hover it." | `LockOnto(unit)` | the face hovered |
| Caravan | first wagon parks | "The caravan buys surplus for Seals. It leaves in two minutes." | `LockOnto(wagon)` | panel opened or wagon gone |
| Events | first rolled event | "Things happen at the shop. The card on the left follows them." | `FocusOn(event)` | event tracker card clicked |
| Pirates | first raid | "Pirates. Click them before they reach the door." | `JumpTo(loot pile)` | a pirate killed or raid over |
| Royal audience | first royal visit | "Royalty waits at the dais. Click them to make an offer." | `LockOnto(lead)` | audience panel opened |
| Notices | Contracts page opened at ★2+ with ≥ 500 gold | "Post a notice to pull a crowd or push an item." | scroll to notices | a notice posted |
| Keep floors | first crafting job blocked on materials | "Crafting is eating your stock. Set a KEEP on the Inventory page to hold some back." | book tab 2 | a floor set |
| Shelves / Queue | Guild page opened at ★1 with Quick Counter affordable | "Shop upgrades are at the bottom. Quick Counter first; the line is the bottleneck." | scroll to the card | any shop upgrade bought |
| Offline | first Welcome Back panel | nothing: the panel is its own explanation | — | — |

Rules for the table:
- A note fires only if its subject is visible or one click away. The Pirates note at depth 5 with the camera
  underground waits until the camera is up (raids only roll with the shop on screen anyway).
- A note whose done-condition was already met before it fired never shows (a player who promotes before the note
  arrives has nothing to learn from it).
- Triggers are detected from existing signals and polls (`OnContractsChanged`, `OnUpgradePurchased`, `OnEventsChanged`,
  `OnCaravanChanged`, `OnStarEarned`, the promote-ready count the bookmark already computes). No new signals.

## 6. Delivery: how it is built

Three visual tools, all reused:

1. **World pointer** — the existing LMB/RMB TMP glyph, sorting 10000, rendering layer bit 8 (over outlines), bobbing
   above a target transform. One instance, retargeted. For props (workshop, caravan) add the hover outline bit 2 pulse
   on the prop for the note's lifetime.
2. **Note card** — a `MilestoneTracker` card variant: torn paper, one label, optional Show me, ✕. Pinned at index 0.
   Part-one card lines are the *same* component rendered *inside* the active 1★ card under its progress bar.
3. **Button cue** — the glyph placed beside a top-bar button (hiring sign, a bookmark). The wiggle already exists.

**Data:** `TutorialStepDef` ScriptableObject in `Resources/Tutorial/`: `id` (asset name = save key), `copy`, `trigger`
(enum), `trigger arg` (upgrade / tab / star), `showMe` (enum: none / camera target / book tab / board tab / workshop),
`done` (enum + arg). ~22 assets. Part one's six are tied to the 1★ milestone names so the card line knows which card
to live in.

**Runtime:** `TutorialManager`, scene singleton under `System/`. Owns the queue, the one pointer, the one note.
Subscribes to the signals above; polls the two counts (promote-ready, kills/pickups by click) through the same dirty
flags their owners already use. `showTips` bool in settings (default on) gates everything in part two; part one's
pointers are gated by it too, but the Show me buttons on 1★ cards stay because they are useful to anyone.

**Save:** `SaveData.tutorial = { done: [ids], tipsOff: bool }`. Collected and restored like every other chunk; nothing
else persists (a half-shown note just re-fires on its next trigger). New Guild wipes it with the save.

**Cost:** part one is small (camera ease, one glyph retarget loop, six card lines, Show me buttons on milestone cards,
one board string). Part two is medium: the note card, the queue, 16 trigger hooks, the defs. No art beyond what exists.

## 7. Copy rules

- One line. Twelve words is a good line; twenty is the ceiling.
- Verb first when it is an instruction ("Click the drops."), noun first when it is a fact ("Stars pay Guild Seals.").
- Name the thing the way the UI names it: Roster, Guild page, Contracts page, KEEP, Show me.
- No "you can", no "feel free", no exclamation marks except "Pirates."
- Numbers only when the player will act on them ("about a thousand", "two minutes").

## 8. Alternatives considered

- **Mentor character (the shopkeeper talks).** More charm, and it would carry the game's voice. Rejected for now: every
  line becomes a portrait + bubble + dismiss click, the shopkeeper is a 16 px sprite with no face, and it pulls the
  first-contact notes toward paragraphs. Revisit if the game gets a cast in the hall.
- **Better affordances only, no tutorial.** The hiring sign and bookmarks already badge. This fails the cold open:
  nothing can badge "click a creature" without being a pointer, which is the tutorial.
- **A separate Help page in the book.** Useful as a reference, useless for onboarding: nobody opens Help before they
  are stuck, and the stuck moment here is minute one. Could be added later from the same copy table.
- **Gate systems until the tutorial reaches them.** Hiding contracts and the caravan until "taught" would simplify the
  queue, but the game's pacing of unlocks is already the gate (stars). Double-gating adds nothing.

## 9. Playtest checklist

Measure on a fresh save, no prompting, three people who have not seen the game:
- Seconds to first creature click. Target under 10.
- Did they find the shop before the first customer left? If the Show me button is never pressed, the card line is
  doing the work and the button can go.
- Did anyone open the hiring board before they could afford a hire? (Fine, but tells us the sign cue fires too early.)
- Which first-contact notes were ✕-dismissed unread? Those get shorter or go.
- Which systems did they still not touch after 20 minutes? Those need a stronger done-condition or a second trigger.

## 10. Open questions

1. Should the 4.0 camera ease also happen on the first boot of an *old* save that predates the tutorial? Proposed: no;
   `SaveData.tutorial` missing marks every step done.
2. Part one's pointer counts (3 kills, 3 pickups) match the existing serve/inspect glyphs. Confirm 3 feels right for
   pickups, where drops come in bursts.
3. The Workshops note points at the prop in the hall, not the book page. That is a bet that the prop is the better
   mental model. The 3b spec agrees; the first playtest decides.
