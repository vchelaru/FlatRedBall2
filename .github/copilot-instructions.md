# FlatRedBall2 — Copilot Instructions

FlatRedBall2 is a 2D game engine written in C# on .NET 10, built on MonoGame. It integrates Gum (UI toolkit) and Tiled (level editor) as first-class dependencies. The engine is designed to be AI-usable: game samples are AI usability tests, not just demos.

## Build & Test

```bash
# Build the engine
dotnet build src/FlatRedBall2.csproj

# Run all tests
dotnet test tests/FlatRedBall2.Tests/

# Run a single test
dotnet test tests/FlatRedBall2.Tests/ --filter "FullyQualifiedName~MethodName"

# Run all tests in one class
dotnet test tests/FlatRedBall2.Tests/ --filter "ClassName=FlatRedBall2.Tests.EntityTests"
```

## Architecture

The engine has four core types that work together:

**`FlatRedBallService`** — singleton via `FlatRedBallService.Default`. Owns the game loop, sub-systems (`Input`, `Audio`, `Time`, `Camera`, `Gum`, `Content`), the factory registry, and screen transitions. Called from MonoGame's `Update`/`Draw` each frame.

**`Screen`** — base class for game screens. Override `CustomInitialize`, `CustomActivity`, `CustomDestroy`. The per-frame update order is fixed: (1) physics, (2) collision, (3) entity `CustomActivity`, (4) screen `CustomActivity`. Screen `CustomActivity` always runs even when paused, so pause-menu input works there.

**`Entity`** — base class for all game objects. Has `Position`, `Velocity`, `Acceleration`, `Drag`, `Rotation`. Physics is second-order kinematic: `pos += vel*dt + acc*(dt²/2)`, `vel += acc*dt`, `vel -= vel*drag*dt`. Only root entities drive physics — children move with their parent.

**`Factory<T>`** — the standard way to create and track entities. Declared as a field on the screen, constructed in `CustomInitialize`. `Factory<T>.Create()` injects `Engine`, adds the entity to the screen's update loop, and calls `CustomInitialize()` on the entity. Destroyed automatically on screen exit.

### Key wiring rules

- `Entity.Engine` is `internal set` — injected by `Factory<T>` or `Screen.Register` *before* `CustomInitialize` is called. Accessing it before registration throws `InvalidOperationException`.
- Only `FlatRedBallService.Default` is static. No other global state.
- `Screen.Register(entity)` is for entities created with `new` (not `Factory`). Calling `Register` on a factory-created entity adds it to the update loop twice.
- `Screen.Token` is a `CancellationToken` that fires on screen transition. Pass it to `Time.DelaySeconds` / `Time.DelayUntil` to avoid tasks running on the wrong screen.

### Coordinate system

World space is Y+ up. The camera transform flips Y for screen-space rendering. Shapes and entity positions are all in world space.

### Collision

`CollisionRelationship<A, B>` is registered via `Screen.AddCollisionRelationship(...)`. Fluent API: `.MoveFirstOnCollision()`, `.MoveBothOnCollision()`, `.BounceOnCollision()`, `.CollisionOccurred += handler`. The relationship runs every frame automatically — no manual call needed.

Common mistake: `AddCollisionRelationship<Enemy>(_enemies, _players)` — one type arg selects the *self-collision* overload. Use two type args: `AddCollisionRelationship<Enemy, Player>(_enemies, _players)`.

### Rendering

`IRenderable` objects are sorted by `Layer` (index) then `Z`, then optionally by parent Y (`SortMode.ZSecondaryParentY` for top-down depth). `Entity.Z` affects its children's sort position but does not directly control draw order — set Z on individual shapes/sprites. Lower Z = drawn behind; higher Z = drawn in front within the same layer.

### Gum (UI)

Two integration paths:
- **Screen-level**: `screen.Add(element)` — Gum element is drawn in screen space with no world-space tracking.
- **Entity-level**: `entity.Add(visual)` — Gum element tracks the entity's `AbsoluteX/Y` each frame. Call from `CustomInitialize` or later.

Gum canvas size is kept in sync with the viewport automatically; don't set it manually.

### Known stubs

Do not attempt to use:
- `DebugRenderer` — all draw methods are no-ops
- `TilemapCollisionGenerator` — uses a preview MonoGame.Extended dependency; Tiled collision generation is not functional

## Conventions

### Testing

- Framework: xUnit + **Shouldly** for all assertions. Never use `Assert.*`.
- For floats: `value.ShouldBe(expected, tolerance: 0.001f)`.
- Test naming: `MethodOrProperty_Scenario_ExpectedResult` — e.g., `CollidesWith_AARectVsCircle_NotOverlapping_ReturnsFalse`.
- Tests within a class stay in **alphabetical order** by method name.
- Test files mirror the engine namespace: `FlatRedBall2.Collision` → `tests/.../Collision/` with `namespace FlatRedBall2.Tests.Collision`.
- Use **AARect** in test names — not AABB, BoundingBox, or Aabb.
- Every asserted value must be declared explicitly in the test's Arrange section — no shared constants for expected values.
- `InternalsVisibleTo` is configured so tests can access `internal` members (`PhysicsUpdate`, `AddEntity`, etc.).

### Diagnostics

Use `System.Diagnostics.Debug.WriteLine` for all debug output — never `Console.WriteLine`. Debug output appears in Visual Studio's Output window and is stripped from Release builds.

### XML documentation

Document public members only when behavior is non-obvious from the name. Don't document things that the name and signature already make clear. Document: gotchas, ordering constraints, side effects, parameter semantics. Stale or redundant docs are worse than no docs.
