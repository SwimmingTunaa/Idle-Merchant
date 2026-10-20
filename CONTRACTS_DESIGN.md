# Guild Contracts & Guild Seals — Design Doc

Status: **phase 1 built** (`ContractManager`, `ContractsPage`, 2026-10-19) · phases 2–3 planned · Owner: Tuan
Player-facing summary of what shipped: GDD §10.6. "As built" notes below mark where phase 1 settled a detail.
Reference feel: Against the Storm's order system — chosen offers, fulfil from stock.

## Why

- **Cadence:** milestones dry up between star tiers; contracts give a claimable beat every 5–15 minutes forever.
- **Late-game stock sink:** production outruns customer throughput, so stock trends to infinity; contracts are the wholesale channel that eats surplus.
- **Inflation-proof reward:** gold supply scales with production and eventually buys nothing; Guild Seals are time-gated (per contract completed), so they hold value at any economy size.
- **Strategic axis:** hold stock vs liquidate; later, sell vs fulfil vs equip (ties into the equipment system).

## The loop

1. The contract board offers **3 contracts**; the player may **accept 1** at a time (a second active slot is sold later as a guild upgrade).
2. Offers **rotate on a timer** (hiring-board rhythm, pocket-watch affordance — see Cadence). Accepted contracts **never expire**.
3. Fulfilment is **active**: deliverable contracts are completed by handing goods over from inventory (a Fulfil button, enabled when stock covers the ask). Activity contracts (kills/clears/serves) tick counters from their accept time.
4. Complete → **Claim**: gold premium (coin shower) + Guild Seals. A new offer fills the empty slot on the next rotation.

## Contract generation

Templates parameterized by live player state; only roll what the player can actually do:

| Template | Example | Completion |
|---|---|---|
| Deliver raw goods | "A villager needs 12 rat fangs" | hand over from stock |
| Deliver crafted goods | "The garrison orders 8 iron swords" | hand over from stock (demands production chains) |
| Bulk provisioning (late) | "Royal provisioning: 4,200 mixed goods" | hand over from stock |
| Hunt | "Cull 40 mobs on layer 3" | counter from accept |
| Patronage | "Serve 25 customers" | counter from accept |

Tiered by star level for flavor and template pool; same math underneath.

### Target scaling — the core formula

For deliverables of item X: `target = max(rateTerm, stockTerm)`
- `rateTerm` = measured production rate of X (SaveManager's passive-rate machinery) × 5–10 minutes — the floor; keeps early/mid contracts producible.
- `stockTerm` = **30–40% of current stock of X** — the late-game bite; contracts always take a real chunk of the hoard.
- **Snapshot the target at offer time** (not accept time) so dumping stock can't shrink an offer.
- Counter contracts scale on their measured rates alone (kills/min, customers/min).
- *As built:* every target also has a floor (deliver 5, hunt 10, patronage 5) so a fresh guild with unmeasured rates still gets sensible asks. Deliver offers are raw items only (from the unlocked layers' drop tables); Hunt picks a layer that has adventurers. Hand-ins ignore the item's keep-N floor.

A player who never stockpiles sees small deliverable contracts — intended; contracts reward holding inventory, a new decision that doesn't exist today.

## Rewards

- **Gold:** `units × sellValue × 1.6` (premium over selling to customers, since the goods leave stock). Inherits scaling automatically — late bulk contracts pay monsoons, and the coin shower already scales with amount.
- **Guild Seals:** **flat per contract, scaled only by tier — never by size** (or seals inherit gold's inflation):
  - common contracts: **10 seals**
  - star-tiered / crafted / bulk: **20–30 seals**
  - *As built:* tier = depth — Deliver/Hunt on layer 3+ pay 20, +5 per layer beyond, max 30; Patronage always 10. Hunt/Patronage gold (no goods leave stock) = 50% of passive gold/min × the minutes the target takes at the measured rate (capped at 30 min; 30 gold/min assumed before rates exist).
- Seals are **active-play currency by design**: contracts need active fulfilment, so offline time never mints seals. (Welcome-back screen stays gold+items only.)

## Guild Seals — the shop

Launch rule: a currency with nothing to buy is noise — ship with at least two evergreen sinks:

| Item | Cost | Notes |
|---|---|---|
| Recruitment Favor | ~50 | guaranteed high-rank candidate appears in the hiring pool; reuses the candidate system; desirable forever |
| Rush Order | ~20 | instantly refresh the hiring pool (or contract offers) |
| *(reserved)* Armory vendor | 30–150 | equipment materials + rare recipes when the equipment system lands — the long-term seal sink |

Fiction: gold buys goods; seals are the Crown's stamp of guild standing and buy *favors*.

Earn rhythm check: ~1 contract per 5–15 min → a 50-seal favor ≈ 30–50 minutes of active play. Healthy.

### Seals from progression (one-time injections)

Milestones are one-time, so seal grants here are a fixed lifetime supply — they can't inflate the currency, and they onboard the player to seals at their first star, long before the contract board matters.

- **Star awards pay seals**: 30 / 50 / 80 / 120 / 200 for 1★–5★ (granted with the star fanfare; ~480 lifetime total ≈ a handful of favors).
- **A few Crown-flavored milestones** additionally carry 10–30 seals (e.g. TaxCollectorNoticed, CrownSentLetter) — at most one or two per tier; everything else stays gold-only so a seal on a card stays special.
- Cards/claim UI show the seal icon + amount next to the coin line only when present.
- Rule: never put seals on every milestone — if both currencies appear on every card, seals read as recolored gold.

## Cadence & UI

- **Offer rotation:** default **10 minutes** (weightier than the 5-min hiring pool; serialized, tune in playtest). Same affordance family: rotation pocket-watch, "!"-style new-offer badge if the board is closed.
- **Board:** a Contracts book tab — 3 torn-card offers (ask, reward preview in gold + seal icon, Accept button), the active contract card with progress bar + green Fulfil/Claim button. All existing components (torn cards, Button variants, claim coin shower, BindCard-style refresh off signals).
- **HUD:** the active contract renders as one extra card in the milestone tracker (the tracker already draws exactly this shape), sorted with claimables.
- **Seal display:** seal count + icon next to the gold counter (small plate), with a new pixel seal sprite (wax-stamp motif, HUD 5px scale).

## Save & signals

- New SaveData chunk per convention: active contract (template id + params + progress + snapshot target), current offers + rotation timer, lifetime seals earned/spent, seal balance.
- New signals: OnContractAccepted / OnContractCompleted / OnSealsChanged (Raise* convention).
- Measured rates already persist; per-item rates for rateTerm need a small extension of the rate tracker (per-item EMA or a cheaper windowed count — implementation's choice).
- *As built:* no per-item tracker — an item's rate is the overall loot/min × its share of the unlocked drop tables. SaveManager gained kills/min per layer and customers/min (saved in meta) for the counter contracts.

## Phases

1. ✅ **Contracts core (built):** board tab + offer gen (deliver-raw, hunt, patronage) + accept/fulfil/claim + gold premium + seal balance + HUD card + save chunk. Seal shop with Recruitment Favor + Rush Order. Star seals and milestone `sealReward` shipped with it. The board shows a rotation countdown on the page; the bookmark badge/watch is still phase 2.
2. **Depth:** crafted/bulk templates, second active slot as a guild upgrade, new-offer badge + rotation watch on the tab/board, contract stats in GDD/debugger.
3. **With equipment:** armory vendor in the seal shop.

## Resolved defaults (tunable)

1. Offer rotation: **10 minutes** (serialized; weightier than the 5-min hiring pool).
2. Ignoring offers is **free** — the rotation timer is the cost; no reroll fee (Rush Order covers impatience).
3. Seal icon: **wax stamp** motif, HUD 5px scale.
