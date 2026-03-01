# FlatRedBall 2.0 — Architecture Document (Proposed)

> The single source of truth for API design. AI reads this document to generate correct, compilable game code. Product managers read this document to generate build plans.

## Philosophy

- **Code-first, AI-friendly**: No editor required. Everything is defined in C#. Consistent naming conventions, standard types, minimal magic.
- **No static state**: Except `FlatRedBallService.Default`. Multiple engine instances can coexist.
- **Lightweight entities**: No single heavy base class that everything inherits from.
- **Offload to existing libraries**: MonoGame, MonoGame.Extended, Gum, System.Numerics. Do not reimplement what already exists well.
- **Minimal wrappers**: Let external systems (Gum, Tiled) exist as close to raw as possible.
- **Unit testable**: Engine runs headless. Logic is separated from rendering. Time and input are injectable.
- **Avoid unnecessary systems**: No damage system, no built-in game-specific logic. Keep the engine lean.
- **Center-based positioning**: All objects (entities, sprites, shapes) are positioned from their center point, matching existing FRB behavior.
- **Examples compile**: Every code example in this document is valid C# that compiles against the described API. No elided type bodies (`{ ... }`) for types that AI needs to construct or use.
- **Auto-registration via AddChild**: Attaching a child to an entity automatically registers it for update, rendering, and collision. No manual manager registration required.

---

## Complete End-to-End Example

A minimal but complete game demonstrating the core systems working together. This example compiles against the API defined in subsequent sections.

```csharp
using FlatRedBall2;
using FlatRedBall2.Math;
using FlatRedBall2.Input;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

// --- Game entry point ---
public class MyGame : Game
{
    protected override void Initialize()
    {
        FlatRedBallService.Default.Initialize(this);
        FlatRedBallService.Default.ScreenManager.Start<GameplayScreen>();
    }

    protected override void Update(GameTime gameTime) => FlatRedBallService.Default.Update(gameTime);
    protected override void Draw(GameTime gameTime) => FlatRedBallService.Default.Draw();
}

// --- Player entity ---
public class Player : Entity
{
    private const float MoveSpeed = 200f;

    // Called by PlayerFactory after Create()
    public void Setup(Texture2D texture, Layer layer)
    {
        var sprite = new Sprite
        {
            Texture = texture,
            Width = 32,
            Height = 48,
            Layer = layer
        };
        AddChild(sprite); // auto-registers sprite for rendering

        var hitbox = new AxisAlignedRectangle { Width = 28, Height = 44 };
        AddChild(hitbox); // entity is now collidable
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.InputManager.Keyboard;

        VelocityX = 0;
        VelocityY = 0;
        if (kb.IsKeyDown(Keys.Right)) VelocityX = MoveSpeed;
        if (kb.IsKeyDown(Keys.Left)) VelocityX = -MoveSpeed;
        if (kb.IsKeyDown(Keys.Up)) VelocityY = MoveSpeed;
        if (kb.IsKeyDown(Keys.Down)) VelocityY = -MoveSpeed;
    }
}

// --- Bullet entity ---
public class Bullet : Entity
{
    public void Setup(Texture2D texture, Layer layer)
    {
        var sprite = new Sprite
        {
            Texture = texture,
            Width = 8,
            Height = 8,
            Layer = layer
        };
        AddChild(sprite);

        var shape = new Circle { Radius = 4 };
        AddChild(shape);

        VelocityY = 400f; // constant upward movement
    }
}

// --- Enemy entity ---
public class Enemy : Entity
{
    private int _health = 3;
    public bool IsDead => _health <= 0;

    public void Setup(Texture2D texture, Layer layer)
    {
        var sprite = new Sprite
        {
            Texture = texture,
            Width = 32,
            Height = 32,
            Layer = layer
        };
        AddChild(sprite);

        var hitbox = new AxisAlignedRectangle { Width = 28, Height = 28 };
        AddChild(hitbox);
    }

    public void TakeDamage(int amount) => _health -= amount;
}

// --- Factories ---
public class PlayerFactory : Factory<Player>
{
    private readonly Texture2D _texture;
    private readonly Layer _layer;

    public PlayerFactory(Screen screen, Layer layer) : base(screen)
    {
        _texture = screen.ContentManager.Load<Texture2D>("Sprites/player");
        _layer = layer;
    }

    public Player Create(Vector2 position)
    {
        var player = Create();
        player.Position = position;
        player.Setup(_texture, _layer);
        return player;
    }
}

public class BulletFactory : Factory<Bullet>
{
    private readonly Texture2D _texture;
    private readonly Layer _layer;

    public BulletFactory(Screen screen, Layer layer) : base(screen)
    {
        _texture = screen.ContentManager.Load<Texture2D>("Sprites/bullet");
        _layer = layer;
    }

    public Bullet Create(Vector2 position)
    {
        var bullet = Create();
        bullet.Position = position;
        bullet.Setup(_texture, _layer);
        return bullet;
    }
}

public class EnemyFactory : Factory<Enemy>
{
    private readonly Texture2D _texture;
    private readonly Layer _layer;

    public EnemyFactory(Screen screen, Layer layer) : base(screen)
    {
        _texture = screen.ContentManager.Load<Texture2D>("Sprites/enemy");
        _layer = layer;
    }

    public Enemy Create(Vector2 position)
    {
        var enemy = Create();
        enemy.Position = position;
        enemy.Setup(_texture, _layer);
        return enemy;
    }
}

// --- Gameplay screen ---
public class GameplayScreen : Screen
{
    private PlayerFactory _playerFactory;
    private BulletFactory _bulletFactory;
    private EnemyFactory _enemyFactory;

    private Layer _gameplayLayer;

    public override void CustomInitialize()
    {
        // Set up layers (per-screen)
        _gameplayLayer = new Layer("Gameplay");
        var hud = new Layer("HUD") { IsScreenSpace = true };
        Layers.Add(_gameplayLayer);
        Layers.Add(hud);

        // Create factories (pass layer so factories can set it on sprites)
        _playerFactory = new PlayerFactory(this, _gameplayLayer);
        _bulletFactory = new BulletFactory(this, _gameplayLayer);
        _enemyFactory = new EnemyFactory(this, _gameplayLayer);

        // Create instances
        _playerFactory.Create(Vector2.Zero);

        for (int i = 0; i < 5; i++)
            _enemyFactory.Create(new Vector2(-200 + i * 100, 200));

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
    }

    public override void CustomActivity(FrameTime time)
    {
        var player = _playerFactory.Instances[0];
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.Space))
            _bulletFactory.Create(new Vector2(player.AbsoluteX, player.AbsoluteY + 24));

        if (_enemyFactory.Instances.Count == 0)
            MoveToScreen<GameplayScreen>(); // restart
    }

    public override void CustomDestroy()
    {
        _playerFactory.DestroyAll();
        _bulletFactory.DestroyAll();
        _enemyFactory.DestroyAll();
    }
}
```

Key patterns demonstrated:
- **Layers are per-screen** — created in `CustomInitialize`, added to `Layers` list
- **Factories load content** via screen-scoped `ContentManager` and pass it to entities via `Setup()`
- **AddChild auto-registers** children for update, render, and collision
- **Factory passed directly** to `AddCollisionRelationship` (Factory implements `IEnumerable<T>`)
- **Entity.Destroy()** auto-removes from Factory.Instances and the render list
- **CustomDestroy** cleans up factories explicitly

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

    public Angle Normalized();  // wraps to [-pi, pi]
    public Vector2 ToVector2(); // unit vector in this direction

    public static Angle Between(Angle a, Angle b);
    public static Angle Lerp(Angle a, Angle b, float t);

    // operators: +, -, unary -, Angle * float, float * Angle, ==, !=
}
```

### `FrameTime` (struct)

Passed into all update methods. Reflects TimeManager scaling (slow motion etc.).

```csharp
public readonly struct FrameTime
{
    public FrameTime(TimeSpan delta, TimeSpan sinceScreenStart, TimeSpan sinceGameStart);

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
    public List<IRenderable> RenderList { get; }  // the global sorted render list

    // Called from MonoGame's Update/Draw — user is responsible for calling these
    public void Update(GameTime gameTime);
    public void Draw();  // no GameTime needed — rendering uses state set during Update
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

When a new screen starts, the previous screen's `CustomDestroy` is called and its scoped `ContentManager` is unloaded. The new screen gets a fresh `Camera`.

---

## Update Loop (per frame)

Driven by `FlatRedBallService.Update`, which delegates to the active Screen:

1. **Read input** — `InputManager.Update()`
2. **Apply physics** — acceleration → velocity, velocity → position, then drag applied. Traverses entity hierarchy top-down.
   - Drag formula: `velocity *= (1 - drag * deltaTime)` applied per-axis after integration
3. **Run collision relationships** — collision events fire inline
4. **Flush async synchronization context** — pending continuations run
5. **CustomActivity** — `Screen.CustomActivity(frameTime)` first, then all entity `CustomActivity(frameTime)` calls in hierarchy order (parent before children)

Draw pass driven by `FlatRedBallService.Draw`:

6. **Draw** — walk sorted render list, call `Begin`/`Draw`/`End` per batch group, sorted by Layer then Z

**Render list registration**: Objects implementing `IRenderable` are added to the render list automatically when attached to an entity via `AddChild`. Removing or destroying removes them. For standalone renderables (Tiled map layers, Gum UI), add them directly via `Engine.RenderList.Add()`.

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

`FrameTime` values already reflect `TimeScale`. Physics and CustomActivity receive scaled time. The `FrameTime` constructor is public so tests can create instances directly.

---

## Entity

The base class for all game objects. Lightweight — position, velocity, acceleration, rotation, drag, visibility, and parent/child hierarchy. Implements both `IAttachable` and `ICollidable`. Positioned from its center.

```csharp
public class Entity : IAttachable, ICollidable
{
    // Position — relative to parent when attached, world when not.
    // Use AbsoluteX/AbsoluteY for guaranteed world position regardless of attachment state.
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
    public float Drag { get; set; }            // applied as: velocity *= (1 - drag * deltaTime) per frame

    // Hierarchy
    public Entity Parent { get; }
    public IReadOnlyList<IAttachable> Children { get; }
    public void AddChild(IAttachable child);
    public void RemoveChild(IAttachable child);

    // Visibility — hierarchical; invisible parent hides all children
    public bool IsVisible { get; set; }

    // Engine reference (injected by the framework when entity enters the hierarchy)
    public FlatRedBallService Engine { get; }

    // Lifecycle
    public virtual void CustomInitialize() { }
    public virtual void CustomActivity(FrameTime time) { }
    public virtual void CustomDestroy() { }
    public virtual void CustomDraw(SpriteBatch spriteBatch, Camera camera) { }

    public void Destroy();  // calls CustomDestroy, destroys all children recursively
}
```

No `Scale` property. Children Z is relative to entity Z.

**Entity registration**: Entities enter the update/physics/collision loop when created via `Factory.Create()` (which registers with the screen automatically). Standalone entities created with `new` must be registered manually.

**Destroy lifecycle**: `Destroy()` calls `CustomDestroy()`, removes this entity from its parent, recursively destroys all children, removes from `Factory.Instances` if factory-created, and removes all attached `IRenderable` children from the render list.

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

    // Collision — list vs list (Factory implements IEnumerable<T>)
    public CollisionRelationship<A, B> AddCollisionRelationship<A, B>(
        IEnumerable<A> listA,
        IEnumerable<B> listB)
        where A : ICollidable
        where B : ICollidable;

    // Collision — single vs list
    public CollisionRelationship<A, B> AddCollisionRelationship<A, B>(
        A single,
        IEnumerable<B> list)
        where A : ICollidable
        where B : ICollidable;

    // Collision — entities vs static geometry
    public CollisionRelationship<A, ShapeCollection> AddCollisionRelationship<A>(
        IEnumerable<A> entities,
        ShapeCollection staticGeometry)
        where A : ICollidable;
}
```

**Screen creation flow**: Screens are created by `ScreenManager`. The constructor is called internally; `Engine`, `Camera`, and `ContentManager` are injected by the framework. Users override lifecycle methods but never call `new Screen()` directly (except in tests — see Unit Testing section). Use `MoveToScreen<T>()` from within a Screen or `ScreenManager.Start<T>()` for the initial screen.

**Entity content scope**: Factories should load content using the screen-scoped `ContentManager` (as shown in the examples). For content that should persist across screens, use `screen.Engine.ContentManager` instead.

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

    public Color BackgroundColor { get; set; }  // Microsoft.Xna.Framework.Color

    // Resolution / scaling — this IS the zoom mechanism.
    // Smaller values = zoom in (fewer world units visible).
    // Larger values = zoom out. No separate Zoom property.
    public int TargetWidth { get; set; }
    public int TargetHeight { get; set; }

    // Coordinate conversion
    public Vector2 WorldToScreen(Vector2 worldPosition);
    public Vector2 ScreenToWorld(Vector2 screenPosition);
    public Matrix GetTransformMatrix();  // used by world-space IRenderBatch
}
```

World space: Y+ up. Screen space: Y+ down. Camera transform handles conversion. Camera defaults to the game window's resolution for TargetWidth/TargetHeight — no explicit configuration is required for basic games.

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

- A single persistent `List<IRenderable>` maintained by the engine, accessible via `Engine.RenderList`
- Entity children implementing `IRenderable` are added automatically when attached via `AddChild`. Removal/destruction automatically deregisters.
- Standalone renderables (Tiled map layers, Gum UI) are added directly via `Engine.RenderList.Add()`
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
    // Position (IAttachable)
    public Entity Parent { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float AbsoluteX { get; }
    public float AbsoluteY { get; }
    public float Z { get; set; }
    public float AbsoluteZ { get; }

    // Rotation
    public Angle Rotation { get; set; }
    public Angle AbsoluteRotation { get; }

    // Rendering (IRenderable)
    public Layer Layer { get; set; }
    public IRenderBatch Batch { get; set; }  // defaults to WorldSpaceBatch
    public string? Name { get; set; }

    // Visual
    public Texture2D Texture { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public Color Color { get; set; }           // Microsoft.Xna.Framework.Color
    public float Alpha { get; set; }           // 0.0 = fully transparent, 1.0 = fully opaque (default)
    public bool IsVisible { get; set; }

    // Sprite sheets
    public Rectangle? SourceRectangle { get; set; }  // null = full texture

    // Flip
    public bool FlipHorizontal { get; set; }
    public bool FlipVertical { get; set; }

    // Animation
    public AnimationChain? CurrentAnimation { get; }
    public void PlayAnimation(string name);
    public void PlayAnimation(AnimationChain chain);

    public void Draw(SpriteBatch spriteBatch, Camera camera);
    public void Destroy();
}
```

Sprites are drawn rotated around their center point. The origin is always `(Width/2, Height/2)`.

Animation chains are loaded from external files (ACHX format, ported from FRB).

---

## AnimationChain

Defines sprite animation sequences. Loaded from `.achx` files via ContentManager.

```csharp
public class AnimationChain
{
    public string Name { get; }
    public IReadOnlyList<AnimationFrame> Frames { get; }
    public float TotalDuration { get; }
}

public class AnimationFrame
{
    public Texture2D Texture { get; }
    public Rectangle SourceRectangle { get; }
    public float Duration { get; }  // seconds this frame is shown
    public bool FlipHorizontal { get; }
    public bool FlipVertical { get; }
}

public class AnimationChainList
{
    public IReadOnlyList<AnimationChain> Chains { get; }
    public AnimationChain this[string name] { get; }
}
```

Loading and usage:

```csharp
var animations = ContentManager.Load<AnimationChainList>("Sprites/player");
sprite.PlayAnimation(animations["Walk"]);
// or by name, if animations are pre-assigned to the sprite:
sprite.PlayAnimation("Walk");
```

---

## Collision Shapes

All shapes implement `IAttachable`, `IRenderable` (for debug drawing), and `ICollidable`. Rotation is not modified by collision responses. All shapes are positioned from their center.

### `ICollidable`

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
```

Game code typically uses `CollisionRelationship` responses (`.MoveFirstOnCollision()`, etc.) rather than calling `SeparateFrom`/`AdjustVelocityFrom` directly.

### Shape Classes

```csharp
public class AxisAlignedRectangle : IAttachable, IRenderable, ICollidable
{
    public float Width { get; set; }   // full width, not half
    public float Height { get; set; }  // full height, not half

    // IAttachable: Parent, X, Y, Z, AbsoluteX, AbsoluteY, AbsoluteZ, Destroy()
    // IRenderable: Layer, Batch, Name, Draw() — used for debug visualization
}

public class Circle : IAttachable, IRenderable, ICollidable
{
    public float Radius { get; set; }

    // IAttachable: Parent, X, Y, Z, AbsoluteX, AbsoluteY, AbsoluteZ, Destroy()
    // IRenderable: Layer, Batch, Name, Draw()
}

public class Polygon : IAttachable, IRenderable, ICollidable
{
    public IReadOnlyList<Vector2> Points { get; }

    // Factory methods
    public static Polygon CreateRectangle(float width, float height);
    public static Polygon FromPoints(IEnumerable<Vector2> points);

    // IAttachable: Parent, X, Y, Z, AbsoluteX, AbsoluteY, AbsoluteZ, Destroy()
    // IRenderable: Layer, Batch, Name, Draw()
}
```

### `ShapeCollection`

Used for static level geometry (walls, platforms, tile-based collision):

```csharp
public class ShapeCollection : ICollidable
{
    public void Add(AxisAlignedRectangle rect);
    public void Add(Circle circle);
    public void Add(Polygon polygon);

    public bool CollidesWith(ICollidable other);
    public Vector2 GetSeparationVector(ICollidable other);
    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f);
    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f);
}
```

### Attaching shapes to entities

```csharp
var hitbox = new AxisAlignedRectangle { Width = 32, Height = 48 };
player.AddChild(hitbox); // entity is now collidable via this shape
```

The engine handles all shape-vs-shape collision dispatch internally (AABB-AABB, AABB-Circle, Circle-Circle, Polygon-AABB, etc.). Game code should not implement `ICollidable` on custom types.

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

### Usage examples

Factory vs Factory (Factory implements `IEnumerable<T>`):
```csharp
AddCollisionRelationship(bulletFactory, enemyFactory)
    .MoveSecondOnCollision()
    .CollisionOccurred += (bullet, enemy) =>
    {
        bullet.Destroy();
        enemy.TakeDamage(1);
    };
```

Single entity vs Factory:
```csharp
AddCollisionRelationship(player, enemyFactory)
    .MoveBothOnCollision();
```

Entities vs static geometry:
```csharp
AddCollisionRelationship(playerFactory, levelWalls)
    .MoveFirstOnCollision(); // entities separate from static walls
```

---

## Factory

A built-in generic base class. Associated with the current Screen at construction time. Implements `IEnumerable<T>` so it can be passed directly to `AddCollisionRelationship`.

```csharp
public class Factory<T> : IEnumerable<T> where T : Entity, new()
{
    public Factory(Screen screen);

    public IReadOnlyList<T> Instances { get; }

    public T Create();         // creates instance, registers with screen
    public void Destroy(T instance);
    public void DestroyAll();

    // Optional pooling (future version)
}
```

Users can inherit to add custom Create logic. Factories should load content in the constructor using the screen's `ContentManager`:

```csharp
public class EnemyFactory : Factory<Enemy>
{
    private readonly Texture2D _texture;
    private readonly Layer _layer;

    public EnemyFactory(Screen screen, Layer layer) : base(screen)
    {
        // Screen-scoped: texture is unloaded when the screen is destroyed.
        // For content that should persist across screens, use screen.Engine.ContentManager instead.
        _texture = screen.ContentManager.Load<Texture2D>("Enemies/goblin");
        _layer = layer;
    }

    public Enemy Create(Vector2 position)
    {
        var enemy = Create();  // base Factory.Create() allocates and registers
        enemy.Position = position;
        enemy.Setup(_texture, _layer);
        return enemy;
    }
}
```

**Destroy lifecycle**: `Entity.Destroy()` automatically removes the entity from its Factory's `Instances` list. `Factory.Destroy(entity)` calls `Entity.Destroy()` and then removes from `Instances`. Both paths produce the same result — use whichever is more natural (prefer `entity.Destroy()`).

---

## Input

Hard classes for direct access, interfaces for testability and injection.

### `InputManager`

```csharp
public class InputManager
{
    public IKeyboard Keyboard { get; }
    public ICursor Cursor { get; }
    public IGamepad GetGamepad(int index);  // player index 0-3

    internal void Update();  // called by FlatRedBallService.Update
}
```

### Core Input Interfaces

```csharp
public interface IKeyboard
{
    bool IsKeyDown(Keys key);
    bool WasKeyPressed(Keys key);
}

public interface ICursor
{
    Vector2 WorldPosition { get; }
    Vector2 ScreenPosition { get; }
    bool PrimaryDown { get; }
    bool PrimaryPressed { get; }
}

public interface IGamepad
{
    bool IsButtonDown(Buttons button);
    float GetAxis(GamepadAxis axis);
}

public enum GamepadAxis
{
    LeftStickX, LeftStickY,
    RightStickX, RightStickY,
    LeftTrigger, RightTrigger
}

public interface IInputDevice
{
    bool IsActionDown(string action);
    bool WasActionPressed(string action);
}
```

### Concrete Implementations

```csharp
public class Keyboard : IKeyboard { }
public class Cursor : ICursor { }    // handles mouse + touch unified
public class Gamepad : IGamepad { }
```

Accessed via `Screen.Engine.InputManager` or injected directly in tests.

### Device-Independent Input Abstractions

For entities that should work with any input device (keyboard, gamepad, or test stubs):

```csharp
public interface I2DInput
{
    float X { get; }  // -1 to 1 horizontal
    float Y { get; }  // -1 to 1 vertical
}

public interface IPressableInput
{
    bool IsDown { get; }
    bool WasJustPressed { get; }
    bool WasJustReleased { get; }
}
```

Usage on an entity:

```csharp
public class Player : Entity
{
    public I2DInput MovementInput { get; set; }
    public IPressableInput ShootInput { get; set; }

    public override void CustomActivity(FrameTime time)
    {
        VelocityX = MovementInput.X * MoveSpeed;
        VelocityY = MovementInput.Y * MoveSpeed;
        if (ShootInput.WasJustPressed) Shoot();
    }
}
```

### Built-in Input Adapters

```csharp
public class KeyboardInput2D : I2DInput
{
    public KeyboardInput2D(IKeyboard keyboard, Keys left, Keys right, Keys up, Keys down);
    public float X { get; }
    public float Y { get; }
}

public class KeyboardPressableInput : IPressableInput
{
    public KeyboardPressableInput(IKeyboard keyboard, Keys key);
    public bool IsDown { get; }
    public bool WasJustPressed { get; }
    public bool WasJustReleased { get; }
}

public class GamepadInput2D : I2DInput
{
    public GamepadInput2D(IGamepad gamepad, GamepadAxis xAxis, GamepadAxis yAxis);
    public float X { get; }
    public float Y { get; }
}

public class GamepadPressableInput : IPressableInput
{
    public GamepadPressableInput(IGamepad gamepad, Buttons button);
    public bool IsDown { get; }
    public bool WasJustPressed { get; }
    public bool WasJustReleased { get; }
}
```

Wiring in Screen:

```csharp
player.MovementInput = new KeyboardInput2D(engine.InputManager.Keyboard,
    Keys.Left, Keys.Right, Keys.Up, Keys.Down);
player.ShootInput = new KeyboardPressableInput(engine.InputManager.Keyboard, Keys.Space);
```

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

Audio assets are loaded automatically on first use via ContentManager. Paths follow ContentManager conventions (see ContentManagerService section).

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

**Path format**: Content paths use MonoGame content pipeline format — forward slashes, no file extension. Example: `ContentManager.Load<Texture2D>("Sprites/player")` loads the corresponding `.xnb` asset. For raw file loading (PNG, WAV, etc.), use the full relative path with extension: `ContentManager.Load<Texture2D>("Sprites/player.png")`.

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

Example setup:

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

`GumRenderable` wraps a Gum screen/component as an `IRenderable` that delegates drawing to the `GumBatch`.

---

## Tiled Integration

Via MonoGame.Extended.Tiled. The engine provides:

- TMX loading through `ContentManagerService`
- Per-tile-layer rendering as `IRenderable` via `TiledMapLayerRenderable`
- Collision shape generation from tile properties via `TiledCollisionGenerator`
- **No entity spawning from object layers** — entities are created manually in `Screen.CustomInitialize`

Engine-provided types:

```csharp
public class TiledMapLayerRenderable : IRenderable
{
    public TiledMapLayerRenderable(TiledMap map, TiledMapTileLayer tileLayer, Camera camera);

    public Layer Layer { get; set; }
    public float Z { get; set; }
    // IRenderable: Batch, Name, Draw()
}

public static class TiledCollisionGenerator
{
    public static void Generate(TiledMap map, string propertyName, Screen screen);
}
```

`TiledMap` and `TiledMapTileLayer` are from MonoGame.Extended (`MonoGame.Extended.Tiled`).

Example usage:

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

    // Generate static collision shapes from tile properties (e.g. "Solid" = true)
    TiledCollisionGenerator.Generate(map, propertyName: "Solid", screen: this);
}
```

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
var frameTime = new FrameTime(
    delta: TimeSpan.FromSeconds(1.0 / 60.0),
    sinceScreenStart: TimeSpan.Zero,
    sinceGameStart: TimeSpan.Zero);

for (int i = 0; i < 3; i++)
    screen.CustomActivity(frameTime);

// Assert game state
Assert.Equal(expectedX, player.AbsoluteX);
```

Key testability affordances:
- `FlatRedBallService` is instantiable without a real window
- `ContentManagerService.CreateNull()` for asset-free tests
- Input interfaces (`IKeyboard`, `ICursor`, `IGamepad`) are mockable
- `I2DInput` and `IPressableInput` can be stubbed for deterministic input testing
- `FrameTime` is a value type with a public constructor — inject any time step
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

## Namespace Structure

```
FlatRedBall2                    — FlatRedBallService, ScreenManager, Entity, Screen,
                                   Camera, Factory<T>, IAttachable
FlatRedBall2.Math               — Angle, FrameTime
FlatRedBall2.Rendering          — IRenderable, IRenderBatch, Sprite, Layer
FlatRedBall2.Rendering.Batches  — WorldSpaceBatch, ScreenSpaceBatch, GumBatch
FlatRedBall2.Collision          — ICollidable, AxisAlignedRectangle, Circle, Polygon,
                                   ShapeCollection, CollisionRelationship<A,B>
FlatRedBall2.Input              — InputManager, IKeyboard, ICursor, IGamepad, GamepadAxis,
                                   IInputDevice, I2DInput, IPressableInput,
                                   Keyboard, Cursor, Gamepad,
                                   KeyboardInput2D, KeyboardPressableInput,
                                   GamepadInput2D, GamepadPressableInput
FlatRedBall2.Audio              — AudioManager, IAudioBackend
FlatRedBall2.Content            — ContentManagerService, AnimationChain, AnimationFrame,
                                   AnimationChainList
FlatRedBall2.Diagnostics        — DebugRenderer, RenderDiagnostics, BatchBreakInfo
FlatRedBall2.Gum                — GumBatch, GumRenderable
FlatRedBall2.Tiled              — TiledMapLayerRenderable, TiledCollisionGenerator
```

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

## Patterns to Preserve from FRB1

These patterns from the original FlatRedBall are intentionally carried forward. Do not regress:

1. **Flat property surface** — `entity.X`, `entity.VelocityX`, not `entity.Transform.Position.X`
2. **Auto-registration via AddChild** — no manual `SpriteManager.AddPositionedObject()` calls
3. **Recursive Destroy** — destroying a parent cleans up all children automatically
4. **ICollidable on Entity base class** — no per-entity boilerplate to opt into collision
5. **Type-safe screen navigation** — `MoveToScreen<T>()`, not string-based
6. **I2DInput / IPressableInput** — device-independent input abstractions that make entities testable
7. **Center-based positioning** — all objects positioned from center, not top-left

---

## Anti-Patterns Intentionally Avoided

These patterns from FRB1 are explicitly **not** carried forward:

1. **Static state everywhere** — FRB2 uses instance-based services, no `SpriteManager.AddXxx` statics
2. **Manual manager registration** — FRB2 auto-registers via `AddChild`
3. **Manual Destroy cleanup for each manager** — FRB2's `Destroy()` handles all cleanup recursively
4. **ScaleX/ScaleY half-dimensions** on collision shapes — FRB2 uses `Width`/`Height` (full dimensions)
5. **Vector3 for 2D positions** — FRB2 uses `System.Numerics.Vector2` (with separate `Z` for depth)
6. **Screen navigation by string name** — FRB2 uses generic `MoveToScreen<T>()`
