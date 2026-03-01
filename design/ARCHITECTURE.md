# FlatRedBall 2.0 — Architecture Document

## Philosophy

- **Code-first, AI-friendly**: No editor required. Everything is defined in C#. Consistent naming conventions, standard types, minimal magic.
- **No static state**: Except `FlatRedBallService.Default`. Multiple engine instances can coexist.
- **Lightweight entities**: No single heavy base class that everything inherits from.
- **Offload to existing libraries**: MonoGame, MonoGame.Extended, Gum, System.Numerics. Do not reimplement what already exists well.
- **Minimal wrappers**: Let external systems (Gum, Tiled) exist as close to raw as possible.
- **Unit testable**: Engine runs headless. Logic is separated from rendering. Time and input are injectable.
- **Avoid unnecessary systems**: No damage system, no built-in game-specific logic. Keep the engine lean.
- **Center-based positioning**: All objects (entities, sprites, shapes) are positioned from their center point, matching existing FRB behavior.

---

## Core Value Types

### `Angle` (readonly struct)

Explicit construction — no implicit float conversion to avoid degree/radian mistakes.

```csharp
public readonly struct Angle
{
    public static Angle FromDegrees(float degrees);
    public static Angle FromRadians(float radians);

    public float Degrees { get; }
    public float Radians { get; }

    public Angle Normalized();  // clamps to [-π, π]
    public Vector2 ToVector2(); // unit vector in this direction

    public static Angle Between(Angle a, Angle b);
    public static Angle Lerp(Angle a, Angle b, float t);

    // operators: +, -, *, ==, !=
}
```

### `FrameTime` (struct)

Passed into all update methods. Reflects TimeManager scaling (slow motion etc.).

```csharp
public readonly struct FrameTime
{
    public TimeSpan Delta { get; }             // time since last frame
    public TimeSpan SinceScreenStart { get; }  // time since current screen started
    public TimeSpan SinceGameStart { get; }    // time since engine started
    public float DeltaSeconds => (float)Delta.TotalSeconds; // convenience
}
```

### Vector types

Use `System.Numerics.Vector2` and `System.Numerics.Vector3` throughout. Float precision — no custom double-based vectors.

---

## `FlatRedBallService`

The root engine object. Not static — can be instantiated multiple times (e.g. for testing or split-screen). A default instance is pre-created and available for convenience; call `Initialize(game)` to wire it up before use.

```csharp
public class FlatRedBallService
{
    public static FlatRedBallService Default { get; }  // pre-created; call Initialize(game) before use

    public FlatRedBallService();           // empty constructor
    public void Initialize(Game game);     // wires into MonoGame Game instance

    // Sub-systems (all instance-based, injected into screens/entities)
    public ScreenManager ScreenManager { get; }
    public InputManager InputManager { get; }
    public AudioManager AudioManager { get; }
    public ContentManagerService ContentManager { get; }  // global-scoped
    public TimeManager TimeManager { get; }
    public DebugRenderer DebugRenderer { get; }
    public RenderDiagnostics RenderDiagnostics { get; }

    // Called from MonoGame's Update/Draw — user is responsible for calling these
    public void Update(GameTime gameTime);
    public void Draw();
}
```

Users wire the engine into their own `Game` subclass — no base class required:

```csharp
protected override void Initialize()
{
    FlatRedBallService.Default.Initialize(this);
    FlatRedBallService.Default.ScreenManager.Start<MainMenuScreen>();
}

protected override void Update(GameTime gameTime) => FlatRedBallService.Default.Update(gameTime);
protected override void Draw(GameTime gameTime) => FlatRedBallService.Default.Draw();
```

---

## `ScreenManager`

Manages screen transitions. Accessible via `FlatRedBallService.ScreenManager`.

```csharp
public class ScreenManager
{
    public Screen CurrentScreen { get; }

    public void Start<T>() where T : Screen, new();  // creates and activates the first screen
}
```

When a new screen starts, the previous screen's `CustomDestroy` is called and its scoped `ContentManager` is unloaded.

---

## Update Loop (per frame)

Driven by `FlatRedBallService.Update`, which delegates to the active Screen:

1. **Read input** — `InputManager.Update()`
2. **Apply physics** — velocity → position, acceleration → velocity, drag applied; traverses entity hierarchy top-down
3. **Run collision relationships** — collision events fire inline
4. **Flush async synchronization context** — pending continuations run
5. **CustomActivity** — `Screen.CustomActivity(frameTime)`, then all entity `CustomActivity(frameTime)` calls top-down

Draw pass driven by `FlatRedBallService.Draw`:

6. **Draw** — walk sorted render list, call `Begin`/`Draw`/`End` per batch group, sorted by Layer then Z

---

## `TimeManager`

```csharp
public class TimeManager
{
    public float TimeScale { get; set; }  // 1.0 = normal, 0.5 = half speed
    public FrameTime CurrentFrameTime { get; }
    public void ResetScreen();  // restarts SinceScreenStart
}
```

`FrameTime` values already reflect `TimeScale`. Physics and CustomActivity receive scaled time.

---

## Entity

The base class for all game objects. Lightweight — position, velocity, acceleration, rotation, drag, visibility, and parent/child hierarchy. Implements both `IAttachable` and `ICollidable`. Positioned from its center.

```csharp
public class Entity : IAttachable, ICollidable
{
    // Position — relative to parent when attached, world when not
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public Vector2 Position { get; set; }  // convenience wrapper for (X, Y)

    // Always world position regardless of attachment
    public float AbsoluteX { get; }
    public float AbsoluteY { get; }
    public float AbsoluteZ { get; }

    // Rotation — relative to parent when attached, world when not
    public Angle Rotation { get; set; }
    public Angle AbsoluteRotation { get; }

    // Physics
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public Vector2 Velocity { get; set; }      // convenience wrapper for (VelocityX, VelocityY)
    public float AccelerationX { get; set; }
    public float AccelerationY { get; set; }
    public Vector2 Acceleration { get; set; }  // convenience wrapper for (AccelerationX, AccelerationY)
    public float Drag { get; set; }            // reduces velocity each frame

    // Hierarchy
    public Entity Parent { get; }
    public IReadOnlyList<IAttachable> Children { get; }
    public void AddChild(IAttachable child);
    public void RemoveChild(IAttachable child);

    // Visibility — hierarchical; invisible parent hides all children
    public bool IsVisible { get; set; }

    // Injected by the framework before CustomInitialize is called.
    // Provides access to ContentManager, Layers, Engine, etc.
    protected Screen Screen { get; }

    // Lifecycle
    public virtual void CustomInitialize() { }
    public virtual void CustomActivity(FrameTime time) { }
    public virtual void CustomDestroy() { }
    public virtual void CustomDraw(SpriteBatch spriteBatch, Camera camera) { }

    public void Destroy();  // calls CustomDestroy, destroys all children recursively
}
```

No `Scale` property. Children Z is relative to entity Z.

Entity implements `ICollidable` by aggregating all attached collision shapes. When `CollidesWith` is called on an entity, it checks all of its shapes against all shapes of the other `ICollidable`.

### `IAttachable`

Anything that can be a child of an Entity:

```csharp
public interface IAttachable
{
    Entity Parent { get; set; }
    float X { get; set; }
    float Y { get; set; }
    float Z { get; set; }
    float AbsoluteX { get; }
    float AbsoluteY { get; }
    float AbsoluteZ { get; }
    void Destroy();
}
```

---

## Screen

Similar lifecycle to Entity but owns the scene. Not an Entity subclass — a separate root concept. Each Screen manages its own ordered layer list; layers are torn down with the screen.

```csharp
public class Screen
{
    public Camera Camera { get; }
    public ContentManagerService ContentManager { get; }  // screen-scoped
    public FlatRedBallService Engine { get; }             // injected

    // Layers — ordered list; earlier index = drawn first = behind.
    // Modify directly: add, remove, or reorder as needed.
    public List<Layer> Layers { get; }

    // Lifecycle
    public virtual void CustomInitialize() { }
    public virtual void CustomActivity(FrameTime time) { }
    public virtual void CustomDestroy() { }

    // Navigation
    public void MoveToScreen<T>() where T : Screen, new();

    // Collision
    public CollisionRelationship<A, B> AddCollisionRelationship<A, B>(
        IEnumerable<A> listA,
        IEnumerable<B> listB)
        where A : ICollidable
        where B : ICollidable;
}
```

When a screen is destroyed, its scoped `ContentManager` is unloaded. The new screen gets a fresh `Camera`.

### `Layer`

```csharp
public class Layer
{
    public Layer(string name);
    public string Name { get; }
    public bool IsScreenSpace { get; init; }  // true = ignores camera transform
}
```

Example layer setup:

```csharp
// In Screen.CustomInitialize:
var Gameplay = new Layer("Gameplay");
var Foreground = new Layer("Foreground");
var Hud = new Layer("HUD") { IsScreenSpace = true };

Layers.Add(Gameplay);
Layers.Add(Foreground);
Layers.Add(Hud);
```

---

## Camera

```csharp
public class Camera
{
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float AccelerationX { get; set; }
    public float AccelerationY { get; set; }

    public Color BackgroundColor { get; set; }

    // Resolution / scaling
    public int TargetWidth { get; set; }
    public int TargetHeight { get; set; }

    // Coordinate conversion
    public Vector2 WorldToScreen(Vector2 worldPosition);
    public Vector2 ScreenToWorld(Vector2 screenPosition);
    public Matrix GetTransformMatrix();  // used by world-space IRenderBatch
}
```

World space: Y+ up. Screen space: Y+ down. Camera transform handles conversion.

---

## Rendering System

### `IRenderable`

```csharp
public interface IRenderable
{
    float Z { get; }
    Layer Layer { get; }
    IRenderBatch Batch { get; }
    string? Name { get; }  // optional; used by RenderDiagnostics for batch break reporting
    void Draw(SpriteBatch spriteBatch, Camera camera);
}
```

### `IRenderBatch`

```csharp
public interface IRenderBatch
{
    void Begin(SpriteBatch spriteBatch, Camera camera);
    void End(SpriteBatch spriteBatch);
}
```

### Render List

- A single persistent `List<IRenderable>` maintained by the engine
- Objects are **inserted in sorted position** on add (binary search, O(log N) find + O(N) shift)
- Each frame: **insertion sort** (O(N) for nearly-sorted data — the common case)
- Sort key: Layer order (primary), Z (secondary)
- Sort is **stable** — equal-Z objects preserve insertion order
- Batching does **not** affect sort order; a batch may be called Begin/End multiple times if interrupted by a different batch at an intermediate Z

### Built-in Batches

```csharp
WorldSpaceBatch   // SpriteBatch.Begin with camera transform matrix
ScreenSpaceBatch  // SpriteBatch.Begin with identity matrix
GumBatch          // delegates to Gum's render pass
```

Custom batches (FontStashSharp, Apos.Shapes, etc.) implement `IRenderBatch` — no engine changes required.

---

## Sprite

Implements `IRenderable` and `IAttachable`. Positioned from its center. Animation chains can adjust the sprite's relative X/Y offset per frame (e.g. for walk cycles or root-motion offsets baked into an ACHX file).

```csharp
public class Sprite : IRenderable, IAttachable
{
    public float X { get; set; }
    public float Y { get; set; }
    public float AbsoluteX { get; }
    public float AbsoluteY { get; }
    public float Z { get; set; }
    public Layer Layer { get; set; }
    public IRenderBatch Batch { get; set; }  // defaults to WorldSpaceBatch

    public Texture2D Texture { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public Color Color { get; set; }
    public bool IsVisible { get; set; }

    // Animation
    public AnimationChain CurrentAnimation { get; }
    public void PlayAnimation(string name);
    public void PlayAnimation(AnimationChain chain);

    public void Draw(SpriteBatch spriteBatch, Camera camera);
    public void Destroy();
}
```

Animation chains are loaded from external files (ACHX format, ported from FRB).

---

## Collision Shapes

All shapes implement `IAttachable`, `IRenderable` (for debug drawing), and `ICollidable`. Rotation is not modified by collision responses. All shapes are positioned from their center.

```csharp
public interface ICollidable
{
    bool CollidesWith(ICollidable other);
    Vector2 GetSeparationVector(ICollidable other);

    // thisMass and otherMass control how separation is split between the two objects.
    // A mass of 0 means the object is immovable (effectively infinite mass).
    void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f);

    // Velocity adjustment after collision. elasticity controls the fraction of velocity
    // exchanged (0 = no response, 1 = full exchange). Response is mass-weighted rather
    // than energy-conserving — intentionally game-friendly, not physically realistic.
    void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f);
}

// Shapes implement ICollidable directly
public class AxisAlignedRectangle : IAttachable, IRenderable, ICollidable { ... }
public class Circle : IAttachable, IRenderable, ICollidable { ... }
public class Polygon : IAttachable, IRenderable, ICollidable { ... }

// Entity implements ICollidable by aggregating its attached shapes
public class Entity : IAttachable, ICollidable
{
    // CollidesWith checks all attached shapes against all shapes of the other ICollidable
}
```

This means collision relationships work with any `ICollidable` — entities, individual shapes, or custom types.

---

## CollisionRelationship

Defined on the Screen, runs during the collision phase of the update loop.

```csharp
public class CollisionRelationship<A, B>
    where A : ICollidable
    where B : ICollidable
{
    public event Action<A, B> CollisionOccurred;

    // Built-in responses (chainable).
    // Mass values control how separation is split; 0 = immovable.
    // Default values move only one side — the non-zero mass object moves entirely.
    public CollisionRelationship<A, B> MoveFirstOnCollision(float firstMass = 1f, float secondMass = 0f);
    public CollisionRelationship<A, B> MoveSecondOnCollision(float firstMass = 0f, float secondMass = 1f);
    public CollisionRelationship<A, B> MoveBothOnCollision(float firstMass = 1f, float secondMass = 1f);

    // elasticity: 0 = absorb collision (no velocity change), 1 = full mass-weighted velocity exchange.
    // Mass-based, not energy-conserving — makes it easier to tune game feel.
    public CollisionRelationship<A, B> BounceOnCollision(float firstMass = 1f, float secondMass = 1f, float elasticity = 1f);
}
```

Usage:

```csharp
// In Screen.CustomInitialize:
AddCollisionRelationship(bulletFactory, enemyFactory)
    .MoveSecondOnCollision()
    .CollisionOccurred += (bullet, enemy) =>
    {
        bullet.Destroy();
        enemy.TakeDamage(1);
    };
```

---

## Factory

A built-in generic base class. Associated with the current Screen at construction time. No subclassing required for the common case — entities load their own content in `CustomInitialize` using the injected `Screen` reference.

```csharp
public class Factory<T> where T : Entity, new()
{
    public Factory(Screen screen);

    public IReadOnlyList<T> Instances { get; }

    // Creates the instance, injects Screen, calls CustomInitialize, registers with screen
    public T Create();
    public void Destroy(T instance);
    public void DestroyAll();

    // Optional pooling (future version)
}
```

Basic usage — no subclass needed:

```csharp
var enemyFactory = new Factory<Enemy>(this);
var enemy = enemyFactory.Create();
enemy.Position = new Vector2(200, 100);
```

Subclass only when you need a typed convenience method (e.g. a multi-parameter Create):

```csharp
public class EnemyFactory : Factory<Enemy>
{
    public EnemyFactory(Screen screen) : base(screen) { }

    public Enemy Create(Vector2 position, EnemyType type)
    {
        var enemy = base.Create();
        enemy.Position = position;
        enemy.Type = type;
        return enemy;
    }
}
```

---

## Input

Hard classes for direct access, interfaces for testability and injection:

```csharp
public interface IKeyboard { bool IsKeyDown(Keys key); bool WasKeyPressed(Keys key); }
public interface ICursor { Vector2 WorldPosition { get; } Vector2 ScreenPosition { get; } bool PrimaryDown { get; } bool PrimaryPressed { get; } }
public interface IGamepad { bool IsButtonDown(Buttons button); float GetAxis(GamepadAxis axis); }
public interface IInputDevice { bool IsActionDown(string action); bool WasActionPressed(string action); }

public class Keyboard : IKeyboard { ... }
public class Cursor : ICursor { ... }    // handles mouse + touch unified
public class Gamepad : IGamepad { ... }
```

Accessed via `Screen.Engine.InputManager` or injected directly in tests.

---

## AudioManager

```csharp
public class AudioManager
{
    public void PlaySong(string name, bool loop = true);
    public void StopSong();
    public void PlaySoundEffect(string name, float volume = 1f);

    // Abstracted backend (MonoGame audio, NAudio, etc.)
    public IAudioBackend Backend { get; set; }
}
```

---

## ContentManagerService

Global content persists for the game lifetime. Screen-scoped content is unloaded on screen destroy. Loading something globally that is already in screen scope promotes it automatically. Loading something screen-scoped that is already global returns the global instance.

```csharp
public class ContentManagerService
{
    // Same syntax for XNB (content pipeline) and raw files
    public T Load<T>(string path);
    public void Unload(string path);
    public void UnloadAll();  // for screen-scoped managers on destroy

    // Stub implementation available for unit tests
    public static ContentManagerService CreateNull();
}
```

---

## Debug Renderer

Immediate-mode debug drawing. Auto-cleared each frame. Toggled with a flag.

```csharp
public class DebugRenderer
{
    public bool IsEnabled { get; set; }

    public void DrawCircle(Vector2 center, float radius, Color color);
    public void DrawRectangle(float x, float y, float width, float height, Color color);
    public void DrawLine(Vector2 start, Vector2 end, Color color);
    public void DrawText(Vector2 position, string text, Color color);
}
```

Always drawn on top (last in render order). No Z sorting.

---

## Render Diagnostics

Batch breaks are tracked and reported so developers can identify and resolve unintended batch interruptions (which cause extra SpriteBatch.Begin/End calls).

```csharp
public class RenderDiagnostics
{
    public bool IsEnabled { get; set; }

    // Populated each frame when IsEnabled = true
    public int BatchBreakCount { get; }
    public IReadOnlyList<BatchBreakInfo> BatchBreaks { get; }
}

public readonly struct BatchBreakInfo
{
    public IRenderBatch PreviousBatch { get; }
    public IRenderBatch NextBatch { get; }
    public Layer Layer { get; }
    public float Z { get; }                    // Z value where the break occurred
    public string PreviousObjectName { get; }  // last object in previous batch
    public string NextObjectName { get; }      // first object in next batch
}
```

Accessed via `FlatRedBallService.RenderDiagnostics`. When enabled, the render loop records every batch transition. Developers can log or display this info to understand what is causing breaks and at what Z values they occur.

---

## Gum Integration

Gum NuGet is a referenced dependency. The engine provides minimal glue:

- `GumBatch` implements `IRenderBatch`, delegates to Gum's renderer
- Gum screens and components can be added as `IRenderable` children on any layer
- Gum objects use screen-space coordinates; place them on a screen-space Layer
- No wrapper classes around Gum's own types — use Gum's API directly

---

## Tiled Integration

Via MonoGame.Extended.Tiled. The engine provides:

- TMX loading through `ContentManagerService`
- Tile layer rendering as `IRenderable`
- Optional collision shape generation from tile properties
- **No entity spawning from object layers** — entities are created manually in `Screen.CustomInitialize`

---

## Unit Testing

The engine is designed to run headless:

```csharp
// Create a self-contained engine for a test
var engine = new FlatRedBallService();
engine.Initialize(mockGame);
var screen = new GameScreen(engine);
screen.CustomInitialize();

// Simulate 3 frames at 60fps
var frameTime = new FrameTime(delta: TimeSpan.FromSeconds(1/60.0));
for (int i = 0; i < 3; i++)
    screen.CustomActivity(frameTime);

// Assert game state
Assert.Equal(expectedX, player.AbsoluteX);
```

Key testability affordances:
- `FlatRedBallService` is instantiable without a real window
- `ContentManagerService.CreateNull()` for asset-free tests
- Input interfaces (`IKeyboard`, `ICursor`, `IGamepad`) are mockable
- `FrameTime` is a value type — inject any time step
- No static state to clean up between tests

---

## Naming Conventions

- **Methods**: `PascalCase` verbs — `AddChild`, `MoveToScreen`, `PlayAnimation`
- **Properties**: `PascalCase` nouns — `IsVisible`, `AbsoluteX`, `CurrentAnimation`
- **Events**: noun + past tense or `On` prefix — `CollisionOccurred`, `OnDestroy`
- **Interfaces**: `I` prefix — `IRenderable`, `IAttachable`, `IKeyboard`
- **Structs (value types)**: `PascalCase`, used for `Angle`, `FrameTime`
- **No abbreviations** in public API — prefer `AbsoluteX` over `AbsX`

---

## Package Structure

Single NuGet package: **FlatRedBall2**

Dependencies (referenced, not bundled):
- `MonoGame.Framework`
- `MonoGame.Extended` (Tiled rendering)
- `Gum` (UI)
- `System.Numerics` (built into .NET)

Tiled and Gum support are built in (dependencies pulled automatically). No separate add-on packages required.

---

## Examples

### Full Screen

Declaring a screen, creating factories, creating instances, declaring collision relationships, responding to collisions, and leaving the screen.

```csharp
public class GameScreen : Screen
{
    private Factory<Player> _playerFactory;
    private Factory<Bullet> _bulletFactory;
    private Factory<Enemy> _enemyFactory;

    public override void CustomInitialize()
    {
        // Set up layers — entities read these in their own CustomInitialize via Screen.Layers
        Layers.Add(new Layer("Gameplay"));
        Layers.Add(new Layer("HUD") { IsScreenSpace = true });

        // No custom factory subclasses needed — entities load their own content
        _playerFactory = new Factory<Player>(this);
        _bulletFactory = new Factory<Bullet>(this);
        _enemyFactory = new Factory<Enemy>(this);

        // Create instances
        _playerFactory.Create().Position = Vector2.Zero;

        var enemy1 = _enemyFactory.Create();
        enemy1.Position = new Vector2(200, 100);

        var enemy2 = _enemyFactory.Create();
        enemy2.Position = new Vector2(-150, 80);

        // Bullets destroy themselves and deal damage to enemies
        AddCollisionRelationship(_bulletFactory, _enemyFactory)
            .MoveSecondOnCollision()
            .CollisionOccurred += (bullet, enemy) =>
            {
                bullet.Destroy();
                enemy.TakeDamage(1);
                if (enemy.IsDead)
                    enemy.Destroy();
            };

        // Player takes damage on contact with enemies
        AddCollisionRelationship(_playerFactory, _enemyFactory)
            .CollisionOccurred += (player, enemy) =>
            {
                player.TakeDamage(1);
                if (player.IsDead)
                    MoveToScreen<GameOverScreen>();
            };
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_enemyFactory.Instances.Count == 0)
            MoveToScreen<WinScreen>();
    }

    public override void CustomDestroy()
    {
        _playerFactory.DestroyAll();
        _bulletFactory.DestroyAll();
        _enemyFactory.DestroyAll();
    }
}
```

### Full Entity

Loading content, creating collision, and displaying a sprite. Entities load their own content in `CustomInitialize` using the injected `Screen` reference — no custom factory needed.

```csharp
public class Enemy : Entity
{
    private Sprite _sprite;
    private AxisAlignedRectangle _collision;
    private int _health = 3;

    public bool IsDead => _health <= 0;

    public override void CustomInitialize()
    {
        // Screen-scoped content: unloads automatically when the screen is destroyed.
        // Use Screen.Engine.ContentManager for content that persists across screens.
        var texture = Screen.ContentManager.Load<Texture2D>("Enemies/goblin");

        var gameplayLayer = Screen.Layers.First(l => l.Name == "Gameplay");

        // Display a sprite (centered, as all objects are)
        _sprite = new Sprite
        {
            Texture = texture,
            Width = 32,
            Height = 32,
            Layer = gameplayLayer
        };
        AddChild(_sprite);
        _sprite.PlayAnimation("Walk");

        // Attach collision shape (slightly smaller than visual)
        _collision = new AxisAlignedRectangle { Width = 28, Height = 28 };
        AddChild(_collision);
    }

    public void TakeDamage(int amount) => _health -= amount;

    public override void CustomActivity(FrameTime time)
    {
        // Simple left-right patrol
        VelocityX = 50f;
        if (AbsoluteX > 300f) VelocityX = -50f;
    }

    public override void CustomDestroy()
    {
        _sprite.Destroy();
        _collision.Destroy();
    }
}
```

### Adding Tiled to a Screen

```csharp
public override void CustomInitialize()
{
    var gameplayLayer = new Layer("Gameplay");
    Layers.Add(gameplayLayer);

    // Load the TMX map (screen-scoped; unloads with screen)
    var map = ContentManager.Load<TiledMap>("Maps/level1");

    // Add each tile layer as an IRenderable on the gameplay layer
    foreach (var tileLayer in map.TileLayers)
    {
        var renderable = new TiledMapLayerRenderable(map, tileLayer, Camera)
        {
            Layer = gameplayLayer,
            Z = 0f
        };
        Engine.RenderList.Add(renderable);
    }

    // Optionally generate static collision shapes from tile properties (e.g. "Solid" = true)
    TiledCollisionGenerator.Generate(map, propertyName: "Solid", screen: this);
}
```

### Setting a Gum Screen

```csharp
public override void CustomInitialize()
{
    // Gum UI lives on a screen-space layer so it ignores camera transforms
    var hud = new Layer("HUD") { IsScreenSpace = true };
    Layers.Add(hud);

    // Load and show a Gum screen using Gum's own API directly
    var hudScreen = GumScreen.Load("HudScreen");
    hudScreen.Show();

    // Wrap in a GumRenderable so the engine draws it at the right point
    var gumRenderable = new GumRenderable(hudScreen)
    {
        Layer = hud,
        Z = 0f
    };
    Engine.RenderList.Add(gumRenderable);

    // Access Gum components directly — no FRB wrappers around Gum types
    var healthBar = (NineSliceRuntime)hudScreen.GetGraphicalUiElementByName("HealthBar");
    healthBar.Width = 100f;
}
```
