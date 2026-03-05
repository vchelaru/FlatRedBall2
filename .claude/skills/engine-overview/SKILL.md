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
- **Input polling**: `InputManager` updates keyboard, mouse, and gamepad state each frame
- **Gum UI updates**: Click/hover/focus events routed to all active Gum elements
- **Screen transitions**: Old screen torn down, new screen initialized — entities, factories, Gum elements all cleaned up automatically
- **Camera**: Initialized from window viewport; transforms world coordinates to screen

## What Game Code Must Implement

- **Entity subclasses** — override `CustomInitialize` (add shapes, input) and `CustomActivity` (per-frame logic)
- **Screen subclasses** — override `CustomInitialize` (create factories, entities, collision relationships, UI)
- **Collision relationships** — call `AddCollisionRelationship` in screen's `CustomInitialize`
- **Game1.cs** — initialize `FlatRedBallService.Default`, call `Update`/`Draw` each frame

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

```csharp
// Game1.cs
protected override void Initialize()
{
    base.Initialize();
    FlatRedBallService.Default.Initialize(this);
    FlatRedBallService.Default.Start<GameplayScreen>();
}

protected override void Update(GameTime gameTime)
{
    if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
    FlatRedBallService.Default.Update(gameTime);
    base.Update(gameTime);
}

protected override void Draw(GameTime gameTime)
{
    FlatRedBallService.Default.Draw();
    base.Draw(gameTime);
}
```

## Key Design Rules

- **Y+ is up** in world space. Camera flips Y for screen rendering.
- **Always use `Factory<T>`** to create entities — never `new MyEntity()`. Factory sets `Engine`, registers with the screen, and enables `GetFactory<T>()`.
- **No static state** — only `FlatRedBallService.Default` is static. Everything else is accessed via `Engine` on entities or directly on screens.
- **Shapes default to `Visible = false`** — always set `Visible = true`.
- **`Entity.Engine`**: Use `CustomInitialize`, not the constructor — `Engine` is null until Factory injects it.

## Complete Screen Example

A minimal but complete screen showing factory + entity + collision + input wired together:

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

        // Bottom wall
        var wall = _wallFactory.Create();
        wall.X = 0; wall.Y = -Camera.TargetHeight / 2f;
        wall.Rectangle.Width = Camera.TargetWidth; wall.Rectangle.Height = 40;

        AddCollisionRelationship<Player, Wall>(_playerFactory, _wallFactory)
            .MoveFirstOnCollision();
    }

    public override void CustomActivity(FrameTime time)
    {
        // Screen-level logic here — runs after entities and collision
    }
}
```

## Sub-Systems (accessed via `Engine.*`)

| Property | Type | Purpose |
|----------|------|---------|
| `InputManager` | `InputManager` | Keyboard, cursor, gamepads |
| `ContentManager` | `ContentManagerService` | Load textures, fonts via `.mgcb` pipeline |
| `Random` | `GameRandom` | Seeded random with helpers (`Between`, `RadialVector2`) |
| `TimeManager` | `TimeManager` | Frame timing, async delays |
| `AudioManager` | `AudioManager` | **Stub — throws NotImplementedException** |
| `DebugRenderer` | `DebugRenderer` | **Stub — all draw methods are no-ops** |

## Which Skill to Read Next

| Task | Skill |
|------|-------|
| Create a new sample project | `sample-project-setup` |
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
