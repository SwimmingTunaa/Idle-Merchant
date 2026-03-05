# Game Design Foundations Reference

## Core Loop Template

Use this sentence format:
"Player performs [action] to gain [resource/progress], spends it on [upgrade/choice], enabling [new action/risk]."

If a system cannot fit this pattern, clarify the loop before coding.

## Decision Quality Heuristics

A choice is meaningful when it has:
- Distinct outcomes.
- Noticeable opportunity cost.
- Relevance within 1-3 minutes of play.

## Progression and Pacing

- Early game: rapid feedback, low penalty, clear goals.
- Mid game: layered choices, synergies, specialization.
- Late game: mastery expression and higher-stakes tradeoffs.

## 2D Feel and Readability

- Prioritize silhouette readability and motion clarity over effect density.
- Tune jump arcs, acceleration, and stop friction as first-class design parameters.
- Ensure enemy telegraphs are readable at gameplay zoom level.
- Keep UI and damage feedback visible without obscuring player movement space.

## Balance Workflow

1. Identify dominant and weakest strategies.
2. Tune numbers in small steps (5-15%).
3. Re-test against at least 3 player archetypes (safe, greedy, experimental).
4. Prefer buffing underused options before hard nerfs.

## Telemetry Suggestions

Track lightweight metrics where possible:
- Pick rates by build/ability/item.
- Time-to-first-success and time-to-failure.
- Resource inflow/outflow per minute.
- Retry/quit points.

Use telemetry to validate assumptions; do not tune solely from intuition.
