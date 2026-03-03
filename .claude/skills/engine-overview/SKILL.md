# FlatRedBall2 — Engine Cheat Sheet

A quick-reference for AI agents and new contributors. Tells you what the engine does for you, what you must do yourself, what is broken/stubbed, and the most important gotchas.

For full API details see `ARCHITECTURE.md`. For deferred items see `TODOS.md`.

---

## Core Concepts

**Screen** — represents one game state (menu, gameplay, game-over). Only one screen is active at a time. It owns factories, collision relationships, and layers. Lifecycle: `CustomInitialize` → repeated `CustomActivity` → `CustomDestroy`. Navigate between screens with `MoveToScreen<T>()`.

**Entity** — the base class for every game object (player, enemy, bullet, wall). Holds position, velocity, and acceleration. Owns child shapes for rendering and collision. Game logic lives in `CustomActivity(FrameTime time)`.

**Factory\<T\>** — creates, tracks, and destroys entity instances. Always create entities through a factory — never `new`. Factories are created in `Screen.CustomInitialize` and destroyed automatically when the screen exits. Pass a factory directly to `AddCollisionRelationship` since `Factory<T>` implements `IEnumerable<T>`.

---

## What the Engine Does Automatically

You do not need to implement these — the engine handles them every frame.

| Concern | What happens |
|---|---|
| **Physics integration** | Position, velocity, acceleration, and drag are integrated each frame using second-order kinematics: `pos += vel*dt + acc*(dt²/2)`, `vel += acc*dt`, `vel -= vel*drag*dt`. Just set the properties; physics runs automatically. |
| **Collision detection** | `CollisionRelationship` checks all pairs each frame, fires `CollisionOccurred`, and calls the built-in response (move/bounce). You only write the event handler. |
| **Entity update order** | Physics → Collision → each entity's `CustomActivity` → `Screen.CustomActivity`. Entities run first (context-free); the screen runs after so it can react to updated entity state. You never invoke these yourself. |
| **Child registration** | `entity.AddChild(sprite)` auto-adds the sprite to the render list and, if it is a shape, to the collision list. No manual manager calls. |
| **Destroy cascade** | `entity.Destroy()` calls `CustomDestroy()`, removes from factory, recursively destroys all children, and removes them from the render list. |
| **Render sorting** | Objects are sorted by Layer (index) then Z value each frame. You do not sort manually. |
| **SpriteBatch management** | `Begin`/`End` calls are grouped per batch type. You never call `SpriteBatch.Begin` yourself. |
| **Screen lifecycle** | `CustomInitialize` → repeated `CustomActivity` → `CustomDestroy`. Content manager is unloaded on screen exit. |
| **Input polling** | `InputManager.Update()` is called before `CustomActivity`. `WasKeyPressed`/`WasJustPressed` work correctly without manual tracking. |
| **Time scaling** | `TimeManager.TimeScale` applies to all `FrameTime` values. Slow motion is free. |
| **Camera Y-flip** | World space is Y-up. Camera transform converts to screen space (Y-down). You work in Y-up throughout. |
| **PlatformerBehavior** | Gravity, jump sustain, fall speed clamping, variable jump by button hold, ground detection from `LastReposition`, acceleration/deceleration ramping — all in `PlatformerBehavior.Update(entity, time)`. |
| **`GameRandom`** | Available via `Engine.Random`. Provides `Between`, `In`, `NextAngle`, `RadialVector2`, `PointInCircle`, etc. |

---

## What Game Code Must Implement

These are **not** in the engine. Every game needs to provide them.

### Entities

- **Subclass `Entity`** for each game object type.
- **`CustomInitialize`**: create sprites/shapes with `new`, wire them with `AddChild`, load textures via `Engine.ContentManager.Load<Texture2D>(...)`, configure input adapters.
- **`CustomActivity(FrameTime time)`**: all game logic — movement, shooting, AI, state machines.
- **`CustomDestroy`**: call `child.Destroy()` on each sprite/shape you created. (Children are destroyed automatically by the engine cascade, but you should also null refs to avoid stale access.)

### Screens

- **`CustomInitialize`**: create layers, create factories, spawn starting entities, call `AddCollisionRelationship`.
- **`CustomActivity`**: game rules (score, win/lose), camera follow, spawner timers, etc.
- **`CustomDestroy`**: only needed for external resources you manage yourself (file handles, network connections). Factories and entities are destroyed automatically.
- **Layer setup**: Layers must be created and added to `Layers` explicitly. Common pattern:
  ```csharp
  var gameplay = new Layer("Gameplay");
  var hud = new Layer("HUD") { IsScreenSpace = true };
  Layers.Add(gameplay);
  Layers.Add(hud);
  ```

### Collision event handling

The engine fires `CollisionOccurred` but does not know game semantics. You write the handler:

```csharp
AddCollisionRelationship(bulletFactory, enemyFactory)
    .MoveSecondOnCollision()
    .CollisionOccurred += (bullet, enemy) =>
    {
        bullet.Destroy();
        enemy.TakeDamage(1);
    };
```

### Camera follow

There is no built-in camera tracking. Set `Camera.X/Y` each frame in `CustomActivity`:

```csharp
public override void CustomActivity(FrameTime time)
{
    Camera.X = _player.X;
    Camera.Y = _player.Y;
}
```

### AI / behavior

The engine has no AI system. Implement AI in `CustomActivity`:
- Patrol: flip velocity at boundary.
- Chase: set velocity toward target.
- State machines: use fields + a switch/if chain.

### Win/lose/score

No built-in game state. Track health, score, lives yourself. Check conditions in `CustomActivity` and call `MoveToScreen<T>()` to transition.

### Spawning

Call `factory.Create()` (or your custom `Create(position)` override) from `CustomInitialize` or later in `CustomActivity`. The factory injects the engine and calls `CustomInitialize` for you — do not call `CustomInitialize` manually.

### Platformer ground detection

`PlatformerBehavior.Update()` reads `entity.LastReposition.Y` to detect ground. This only works if the player-vs-platform collision relationship uses `BounceOnCollision(firstMass:0, secondMass:1, elasticity:0)`. Move-only relationships do **not** set `LastReposition` correctly for ground detection.

---

## Stubbed / Not Working — Do Not Use

These APIs exist but are not functional.

| Feature | Status | Notes |
|---|---|---|
| **Audio** | All methods throw `NotImplementedException` | Do not call `AudioManager` methods. |
| **`Sprite.PlayAnimation`** | No-op | Animations never play. |
| **`DebugRenderer`** | All draw methods are no-ops | `DrawCircle`, `DrawLine`, `DrawText`, etc. do nothing. |
| **Tiled integration** | Stubs | `TiledMapLayerRenderable` and `TiledCollisionGenerator` exist but render/generate nothing. |
| **`GamepadPressableInput.WasJustPressed` / `WasJustReleased`** | Always returns `false` | Use keyboard input for actions that require press detection. |
| **`TopDownBehavior`** | Does not exist yet | Implement top-down movement manually in `CustomActivity`. |
| **Same-list collision** | Not implemented | Cannot add a collision relationship between a list and itself. |
| **`RotationVelocity` / `RotationAcceleration`** | Not on `Entity` | Implement rotation change manually in `CustomActivity`. |
| **Camera pixel-perfect mode** | Not implemented | Sub-pixel shimmer may occur in pixel-art games. |

---

## Critical Gotchas

**Y-axis is UP.** `VelocityY = 200f` moves the entity upward. Gravity should be negative: `AccelerationY = -900f`. This is the opposite of raw MonoGame/screen coordinates.

**All positions are center-based.** A sprite with `Width=32, Height=32` at `X=0, Y=0` spans -16 to +16 in both axes. There is no top-left anchor.

**`Entity.Engine` throws before registration.** Accessing `Engine` before the entity is registered (via `Factory.Create()` or `Screen.Register()`) throws `InvalidOperationException`. Never create entities with `new` and call `CustomInitialize` manually.

**Shape `Width`/`Height` are full dimensions.** `AxisAlignedRectangle { Width = 32 }` is 32 units wide, not 16. (FRB1 used half-dimensions; FRB2 does not.)

**`PlatformerBehavior.Update()` must be called after collision.** It reads `entity.LastReposition` which is set during the collision phase. Calling it in `CustomActivity` (which runs after collision) is correct. Do not call it in a pre-collision hook.

**`CustomDestroy` is for external resources only.** Factories and their entities are destroyed automatically when the screen exits — no `DestroyAll()` call needed.

**`Vector2` ambiguity.** `Microsoft.Xna.Framework` and `System.Numerics` both define `Vector2`. Any file that imports both will get a compile error on bare `Vector2`. Resolve with a type alias at the top of the file: `using Vector2 = System.Numerics.Vector2;`

**`CollisionOccurred` fires once per overlapping pair per frame.** Do not rely on it for continuous effects. Use it to trigger one-time events (damage, destroy, sound).

**Render list is sorted by Layer index, then Z.** If two objects share Z, their draw order is their insertion order (stable sort). Use `Z` to control overlap within a layer.

**`AddChild` requires `Engine` to be set.** Calling `AddChild` before the entity is registered will not add the child to the render list (silently). Always call `AddChild` from `CustomInitialize` or later.

**`FrameTime.SinceGameStart` is a `TimeSpan`, not a float.** Use `.TotalSeconds` to get a float: `time.SinceGameStart.TotalSeconds`.

---

## Minimal Working Game Skeleton

```csharp
// Game1.cs
protected override void Initialize()
{
    FlatRedBallService.Default.Initialize(this);
    FlatRedBallService.Default.Start<GameScreen>();
}
protected override void Update(GameTime gt) => FlatRedBallService.Default.Update(gt);
protected override void Draw(GameTime gt) => FlatRedBallService.Default.Draw();

// GameScreen.cs
public class GameScreen : Screen
{
    private Factory<Player> _players = null!;
    private Factory<Enemy> _enemies = null!;

    public override void CustomInitialize()
    {
        var gameplay = new Layer("Gameplay");
        Layers.Add(gameplay);

        _players = new Factory<Player>(this);
        _enemies = new Factory<Enemy>(this);

        var player = _players.Create();
        player.Position = Vector2.Zero;

        AddCollisionRelationship(_players, _enemies)
            .CollisionOccurred += (p, e) => { /* handle hit */ };
    }

    public override void CustomActivity(FrameTime time)
    {
        Camera.X = _players.Instances[0]?.X ?? 0f;
    }

    public override void CustomDestroy()
    {
        // Factories and entities are destroyed automatically.
        // Only needed for external resources (file handles, network, etc.).
    }
}

// Player.cs
public class Player : Entity
{
    private AxisAlignedRectangle _rect = null!;

    public override void CustomInitialize()
    {
        _rect = new AxisAlignedRectangle { Width = 32, Height = 32, Visible = true };
        AddChild(_rect);
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.InputManager.Keyboard;
        VelocityX = (kb.IsKeyDown(Keys.Right) ? 1f : 0f) - (kb.IsKeyDown(Keys.Left) ? 1f : 0f);
        VelocityX *= 200f;
    }

    public override void CustomDestroy() => _rect.Destroy();
}
```

---

## Namespaces at a Glance

```
FlatRedBall2                    — FlatRedBallService, Entity, Screen, Factory<T>, FrameTime
FlatRedBall2.Math               — Angle
FlatRedBall2.Rendering          — Sprite, Layer, IRenderable, IRenderBatch
FlatRedBall2.Rendering.Batches  — WorldSpaceBatch, ScreenSpaceBatch
FlatRedBall2.Collision          — AxisAlignedRectangle, Circle, Polygon, Line,
                                   ShapeCollection, CollisionRelationship<A,B>,
                                   RepositionDirections
FlatRedBall2.Input              — InputManager, IKeyboard, ICursor, IGamepad,
                                   KeyboardInput2D, KeyboardPressableInput,
                                   GamepadInput2D, GamepadPressableInput,
                                   I2DInput, IPressableInput
FlatRedBall2.Movement           — PlatformerBehavior, PlatformerValues, HorizontalDirection
FlatRedBall2.Utilities          — GameRandom
FlatRedBall2.Audio              — AudioManager  ← STUBBED
FlatRedBall2.Diagnostics        — DebugRenderer ← STUBBED, RenderDiagnostics
FlatRedBall2.Gum                — GumBatch, GumRenderable
FlatRedBall2.Tiled              — TiledMapLayerRenderable, TiledCollisionGenerator  ← STUBBED
```
