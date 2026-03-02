# Camera in FlatRedBall2

Every `Screen` has a `Camera` property. Access it directly — no initialization required.

```csharp
Screen.Camera   // type: FlatRedBall2.Rendering.Camera
```

## Background Color

```csharp
Camera.BackgroundColor = Color.Black;       // default
Camera.BackgroundColor = Color.CornflowerBlue;
```

Set this in `CustomInitialize`. It applies immediately.

## World Bounds

`Camera.TargetWidth` and `Camera.TargetHeight` define the world coordinate space (default: 1280 × 720).

World coordinates are **centered at the origin**:

- X ∈ [−TargetWidth/2, TargetWidth/2]  →  [−640, 640]
- Y ∈ [−TargetHeight/2, TargetHeight/2]  →  [−360, 360]

Y+ is **up**. The top of the screen is Y = +360, the bottom is Y = −360.

```csharp
// Place a wall at the bottom of the screen
wall.Y = -Camera.TargetHeight / 2f;  // -360
```

## Window Resolution

`Camera.TargetWidth/Height` define world units — they do **not** control the actual window pixel size. To set the window resolution, configure `GraphicsDeviceManager` in the `Game1` constructor, **before** `Initialize()` runs:

```csharp
public Game1()
{
    _graphics = new GraphicsDeviceManager(this);
    _graphics.PreferredBackBufferWidth  = 1280;
    _graphics.PreferredBackBufferHeight = 720;
    Content.RootDirectory = "Content";
    IsMouseVisible = true;
}
```

`FlatRedBallService.Initialize` reads `GraphicsDevice.Viewport` after `base.Initialize()` commits these settings, so no further configuration is needed — `Camera.TargetWidth/Height` are set automatically to match the window.

## Camera Position (Scrolling)

Move the camera by setting `Camera.X` and `Camera.Y`. Entities in world space shift accordingly on screen.

```csharp
// Follow a player (centered)
Camera.X = player.X;
Camera.Y = player.Y;
```

For a fixed-screen game like Pong, leave `Camera.X = 0` and `Camera.Y = 0` (the defaults).

## Screen Shake

Two approaches work for screen shake:

- **Velocity-based**: Set `Camera.VelocityX`/`VelocityY` to a random impulse each frame. Reset to 0 when done, and also explicitly reset `Camera.X/Y = 0` since velocity accumulates position drift.
- **Direct assignment**: Set `Camera.X`/`Camera.Y` directly each frame to a random offset that decays to zero. No drift; resets cleanly. Preferred for simple timed shakes.

## Coordinate Conversion

```csharp
// World → screen pixels
System.Numerics.Vector2 screenPos = Camera.WorldToScreen(worldPos);

// Screen pixels → world
System.Numerics.Vector2 worldPos = Camera.ScreenToWorld(screenPos);
```

Useful when placing Gum HUD elements relative to world objects, or for click-to-move logic where you receive screen-space mouse coordinates.

## Gotchas

- **Gum coordinates are independent of Camera.** Gum X/Y are screen pixels, Y-down from the top-left — they do not shift when the camera moves. Only world-space objects (entities, shapes) are affected by camera position.
- **TargetWidth/Height ≠ window pixel size.** The camera scales world units to fill whatever window resolution MonoGame uses. A 1280×720 world still renders correctly in an 800×480 window — it just appears smaller.
