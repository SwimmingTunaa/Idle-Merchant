---
name: unity-game-design-oop
description: Design, implement, and refactor Unity gameplay systems with strong object-oriented architecture and game design grounding, including 2D-focused production workflows. Use when tasks involve Unity C# code, gameplay loops, 2D mechanics, systems design, balancing, ScriptableObject-driven data, scene/prefab architecture, or code quality improvements that require SOLID, composition-over-inheritance, and maintainable production patterns.
---

# Unity Game Design OOP

## Outcome

Build Unity features that are both fun and maintainable:
- Turn design goals into clear gameplay loops and constraints.
- Implement with testable C# architecture (SOLID, low coupling, high cohesion).
- Prefer data-driven authoring with ScriptableObjects and clean runtime boundaries.
- Support both general Unity gameplay and production 2D patterns (physics, camera, animation, tilemaps).

## Workflow

1. Define design intent before coding.
2. Map loop and system boundaries.
3. Select architecture pattern.
4. Implement minimal vertical slice.
5. Verify gameplay feel and technical quality.
6. Refactor only where measurable pain exists.

## 1) Define Design Intent

Capture these in 5-10 lines before implementation:
- Player fantasy: what role/power the player should feel.
- Core loop: repeatable action-reward-action cycle.
- Decision tension: meaningful tradeoffs.
- Failure/recovery: what happens on mistakes.
- Progression: short-term and long-term growth.

If a request is purely technical, infer intent from existing systems and state assumptions explicitly.

## 2) Map System Boundaries

Partition into layers:
- Domain logic: pure rules (damage, economy, cooldown, crafting rules).
- Application orchestration: use cases and flow coordination.
- Unity adapters: MonoBehaviours, animations, VFX/SFX, UI wiring, scene events.
- Data assets: ScriptableObjects for tunable parameters and content catalogs.

Rules:
- Keep domain logic free of Unity-specific API when practical.
- Keep MonoBehaviours thin: input/read state/dispatch commands/render.
- Avoid hidden cross-scene coupling and global mutable state.

## 3) Choose OOP Pattern Deliberately

Pick by problem shape, not preference:
- Composition + interfaces: default for gameplay behaviors.
- Strategy pattern: replace condition-heavy behavior variants.
- State pattern: complex entity lifecycle (idle/move/attack/stun/dead).
- Observer/event channel: decouple producer/consumer flows.
- Factory: controlled creation for enemies/items/abilities.

Avoid:
- Deep inheritance trees for gameplay entities.
- God managers with mixed responsibilities.
- Over-engineering abstractions before second use case exists.

## 4) Implementation Standards (Unity C#)

- One reason to change per class.
- Depend on interfaces at boundaries.
- Inject collaborators through constructors/setters where possible.
- Keep methods small and intention-revealing.
- Treat ScriptableObjects as authoring/config data, not mutable runtime state.
- Cache expensive lookups (`GetComponent`, scene queries) in lifecycle methods.

When editing existing code:
- Preserve serialized fields and prefab compatibility unless explicitly migrating.
- Keep public API stable unless user asked for breaking changes.
- Add migration notes when renaming serialized members.

## 5) Game Design Quality Checks

Validate each delivered feature against:
- Clarity: player can understand cause/effect.
- Agency: player has meaningful choices.
- Feedback: immediate visual/audio/state feedback.
- Pacing: no dead time without purpose.
- Balance: dominant strategy is not trivial or unavoidable.

If tuning is requested, propose parameter ranges and expected behavioral outcomes.

## 5.1) Unity 2D Checklist

Use when the feature is 2D gameplay/UI-heavy:
- Choose correct physics path (`Rigidbody2D`, `Collider2D`, 2D queries).
- Separate movement intent from physics resolution (input -> model -> motor).
- Keep hit detection deterministic and debug-friendly (layers, masks, gizmos).
- Use animation state transitions tied to gameplay state, not frame timing hacks.
- Validate camera behavior for readability (look-ahead, damping, bounds).
- Keep combat/resource feedback legible in small screens and zoomed-out views.

## 6) Testing and Verification

Minimum checks:
- Compile cleanly in Unity.
- No null-reference risks on common paths.
- Deterministic rule behavior for fixed inputs.
- Smoke test in scene for user-facing flow.

Prefer:
- Unit tests for pure domain logic.
- Lightweight play mode checks for integration-heavy behavior.

## Response Style for This Skill

When producing code or plans:
- Start with a short architecture summary.
- Show class responsibilities and boundaries.
- Explain why pattern choices fit this mechanic.
- Call out tradeoffs and future extension points.

## References

Use these files when deeper guidance is needed:
- [references/unity-oop-architecture.md](references/unity-oop-architecture.md): SOLID mapping, class templates, anti-pattern fixes.
- [references/game-design-foundations.md](references/game-design-foundations.md): gameplay loop framing, balance heuristics, progression design.
