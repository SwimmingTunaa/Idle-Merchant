# Unity OOP Architecture Reference

## SOLID in Unity Practice

- Single Responsibility: split input, decision, execution, and presentation.
- Open/Closed: extend with new strategies/states, avoid modifying core dispatchers.
- Liskov: derived behavior must preserve base guarantees.
- Interface Segregation: prefer small contracts (`IDamageable`, `IMovable`, `IInteractable`).
- Dependency Inversion: domain depends on interfaces, adapters implement details.

## Practical Class Split

- `*Config` ScriptableObject: authorable constants and curves.
- Domain class (`*Model`/`*Rules`): pure calculations and state transitions.
- Controller/service (`*Service`): orchestrates use cases.
- MonoBehaviour (`*View`/`*Presenter`): Unity lifecycle, visuals, and player input wiring.

## 2D Architecture Notes

- Isolate a `CharacterMotor2D` (physics application) from `CharacterController` (intent/state).
- Keep collision and damage handling in dedicated services/components, not animation scripts.
- Model combat windows (startup/active/recovery) in data/config objects for balance iteration.
- Use layer-based filtering through one adapter service to avoid scattered physics masks.

## Refactor Triggers

Refactor when you see:
- `if/else` chains growing with each new feature.
- Methods exceeding one screen length.
- Bi-directional dependencies between systems.
- Frequent bug regressions after small edits.

## Safe Refactor Sequence

1. Add characterization tests around current behavior.
2. Extract interfaces around unstable boundaries.
3. Move pure rules out of MonoBehaviours.
4. Replace conditional branches with strategy/state objects.
5. Remove dead code and simplify ownership.

## Anti-Patterns to Correct

- God Manager: split by use case and domain.
- Static mutable globals: replace with scoped services/event channels.
- ScriptableObject runtime mutation: move live state into runtime model instances.
- Premature generic systems: keep concrete until reuse is proven.
