---
name: engine-overview
description: "Engine overview for FlatRedBall2. Start here for any game development task. Covers what the engine does automatically vs what game code must implement, the frame loop, bootstrapping, and known stubs. Trigger when starting a new game, needing to understand the engine architecture, or unsure how FlatRedBall2 works."
---

# FlatRedBall2 Engine Overview

FlatRedBall2 is a 2D game engine built on MonoGame. It provides physics, collision, rendering, input, and UI (via Gum) out of the box. Game code creates Screens, Entities, and wires them together.

## What the Engine Does Automatically

- **Physics**: `pos += vel*dt + acc*(dt^2/2)`, `vel += acc*dt`, `vel -= vel*drag*dt` — every frame, for every entity
- **Collision resolution**: All registered `CollisionRelationship` pairs are tested and resolved after physics
- **Rendering**: Everything added via `screen.Add(renderable)` is drawn, sorted by Layer + Z
- **Input polling**: `Input` updates keyboard, mouse, and gamepad state each frame
- **Gum UI updates**: Click/hover/focus events routed to all active Gum elements
- **Screen transitions**: Old screen torn down, new screen initialized — entities, factories, Gum elements all cleaned up automatically
- **Camera**: Initialized from window viewport; transforms world coordinates to screen

## What Game Code Must Implement

- **Entity subclasses** — override `CustomInitialize` (add shapes, input) and `CustomActivity` (per-frame logic)
- **Screen subclasses** — override `CustomInitialize` (create factories, entities, collision relationships, UI)
- **Collision relationships** — call `AddCollisionRelationship` in screen's `CustomInitialize`
- **Game1.cs** — initialize `FlatRedBallService.Default`, call `Update`/`Draw` each frame
- **Save/load** — the engine offers nothing here; use standard .NET (`System.IO` + `System.Text.Json`, or your serializer of choice). There is no FRB2 save API to search for.

## Frame Loop Order

Each frame runs in this order:

1. **Screen transition** (if pending) — old screen destroyed, new screen initialized
2. **Input update** — keyboard, mouse, gamepad polled
3. **Gum update** — UI input events routed
4. **Physics** — entity positions updated from velocity/acceleration/drag
5. **Collision** — registered relationships resolved; positions corrected
6. **Entity `CustomActivity`** — each entity's per-frame logic
7. **Screen `CustomActivity`** — screen-level logic (sees post-collision, post-entity state)
8. **Draw** — all registered renderables drawn

## Bootstrapping a Game

`Game1.cs` wires the MonoGame loop to FRB: `FlatRedBallService.Default.Initialize<TScreen>(this)` in `Initialize` (sizes the window, initializes, starts the screen — or `Initialize(this, "…gluj")` to boot a whole Glue project), then `Update`/`Draw` each frame. The complete template — including the `GraphicsProfile.HiDef` setup that crashes at startup if omitted — lives in `sample-project-setup`; don't hand-roll it.

## Key Design Rules

- **Y+ is up** in world space. Camera flips Y for screen rendering.
- **Always use `Factory<T>`** to create entities — never `new MyEntity()`. Factory sets `Engine`, registers with the screen, and enables `GetFactory<T>()`.
- **No static state** (engine infrastructure only) — only `FlatRedBallService.Default` is static. Everything else is accessed via `Engine` on entities or directly on screens. Game code may use static singletons for global game data (e.g., `GameData.Current` holding a monster roster or player save state) — this rule prohibits engine-layer statics, not application-layer ones.
- **Shapes default to `IsVisible = false`** — always set `IsVisible = true`.
- **`Entity.Engine`**: Use `CustomInitialize`, not the constructor — `Engine` is null until Factory injects it.
- **Use Gum for all UI** — HUD, health bars, menus, win/lose screens, any text display. Shapes are for world-space game objects (collision geometry, projectiles, platforms). If you reach for a shape to build UI, stop and use Gum instead.

## What a Screen Looks Like

A screen creates its `Factory<T>` instances in `CustomInitialize`, spawns entities with `Create()`, and wires `AddCollisionRelationship` over those factories — the gestalt the per-topic skills show piecewise:

<!-- skill-creator: allow-long-csharp reason="canonical screen gestalt — factory fields + Create + collision wiring composed in one place; the composition is the point and isn't shown together elsewhere" -->
```csharp
public class GameScreen : Screen
{
    private Factory<Player> _playerFactory = null!;
    private Factory<Wall> _wallFactory = null!;

    public override void CustomInitialize()
    {
        _playerFactory = new Factory<Player>(this);
        _wallFactory = new Factory<Wall>(this);
        _playerFactory.Create();

        AddCollisionRelationship<Player, Wall>(_playerFactory, _wallFactory)
            .MoveFirstOnCollision();
    }
}
```

## Sub-Systems (accessed via `Engine.*`)

| Property | Type | Purpose |
|----------|------|---------|
| `Input` | `InputManager` | Keyboard, cursor, gamepads |
| `Content` | `ContentLoader` | Load textures, fonts via `.mgcb` pipeline |
| `Random` | `GameRandom` | Seeded random with helpers (`Between`, `RadialVector2`) |
| `Time` | `TimeManager` | Frame timing, async delays |
| `Audio` | `AudioManager` | Load/play `SoundEffect` and `Song`, music, volume (see `audio`) |

## Which Skill to Read Next

| Task | Skill |
|------|-------|
| Set up screens and transitions | `screens` |
| Create entities with shapes | `entities-and-factories` |
| Load textures and use sprites | `content-and-assets` |
| Set up collision | `collision-relationships` |
| Handle input | `input-system` |
| Physics and movement | `physics-and-movement` |
| Platformer mechanics | `platformer-movement` |
| Camera setup | `camera` |
| UI/HUD with Gum | `gum-integration` |
| Timers and cooldowns | `timing` |
| Level layouts | `levels` |
| Shapes (no-art visuals) | `shapes` |
