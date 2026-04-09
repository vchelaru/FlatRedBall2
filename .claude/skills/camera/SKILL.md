---
name: camera
description: "Camera in FlatRedBall2. Use when working with camera setup, background color, world bounds, window resolution, scrolling, screen shake, coordinate conversion between world and screen space, or Camera.TargetWidth/TargetHeight. Trigger on any camera-related question including viewport, following a player, or screen boundaries."
---

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

`Camera.TargetWidth` and `Camera.TargetHeight` define the world coordinate space. They are managed by the engine — do not set them directly.

World coordinates are **centered at the origin**:

- X ∈ [−TargetWidth/2, TargetWidth/2]  →  [−640, 640] at default 1280×720
- Y ∈ [−TargetHeight/2, TargetHeight/2]  →  [−360, 360] at default 1280×720

Y+ is **up** (see `physics-and-movement`). Top = +360, bottom = −360.

```csharp
wall.Y = -Camera.TargetHeight / 2f;  // bottom of screen
```

> **Screen-edge boundaries** (keeping entities in bounds) are a collision concern, not a camera concern — use wall entities or `TileShapeCollection`. See the `collision-relationships` and `shapes` skills.

## DisplaySettings — Resolution, Zoom, and Letterboxing

`FlatRedBallService.Default.DisplaySettings` controls how the camera is configured at each screen start. Set these before calling `Start<T>()` or between screens.

```csharp
var ds = FlatRedBallService.Default.DisplaySettings;
ds.ResolutionWidth  = 1280;          // world units visible at Zoom=1
ds.ResolutionHeight = 720;
ds.Zoom             = 2f;            // initial camera zoom (2 = 2px per world unit)
ds.ResizeMode       = ResizeMode.StretchVisibleArea;   // or IncreaseVisibleArea
ds.FixedAspectRatio = 16f / 9f;     // null = fill window; set to add letterbox/pillarbox bars
ds.LetterboxColor   = Color.Black;   // bar color when FixedAspectRatio is set
ds.WindowMode       = WindowMode.Windowed;             // or FullscreenBorderless
ds.PreferredWindowWidth  = 1280;     // startup window pixel size (null = leave as-is)
ds.PreferredWindowHeight = 720;
ds.AllowUserResizing = true;         // whether the player can drag window borders
```

**`ResizeMode`:**
- `StretchVisibleArea` (default): the same `ResolutionWidth × ResolutionHeight` world area is always visible, scaled to fill the window.
- `IncreaseVisibleArea`: pixels-per-world-unit stays fixed at `DisplaySettings.Zoom`; a larger window shows more of the world.

**`Zoom` initializes `Camera.Zoom`** at each screen start. After that, the camera is independent — game code can modify `Camera.Zoom` freely during gameplay.

## Runtime Camera Zoom

```csharp
Camera.Zoom = 2f;   // zoom in: shows half the world area
Camera.Zoom = 0.5f; // zoom out: shows double the world area
```

`Camera.Zoom` is reset to `DisplaySettings.Zoom` at the start of each new screen.

## Window Resolution and Fullscreen

`Camera.TargetWidth/Height` do **not** control the actual window pixel size. Window size is set via `DisplaySettings` and applied in two ways:

**Startup** — call `PrepareWindow<T>` from the `Game1` constructor (before `Initialize`) so the window opens at the right size with no flicker:
```csharp
public Game1()
{
    _graphics = new GraphicsDeviceManager(this);
    FlatRedBallService.Default.DisplaySettings.PreferredWindowWidth  = 1280;
    FlatRedBallService.Default.DisplaySettings.PreferredWindowHeight = 720;
    FlatRedBallService.Default.PrepareWindow<MyStartScreen>(_graphics);
}
```

**Runtime** (settings menu, F11 toggle, etc.) — call `ApplyWindowSettings` at any time:
```csharp
// Toggle fullscreen
var newMode = Engine.DisplaySettings.WindowMode == WindowMode.Windowed
    ? WindowMode.FullscreenBorderless
    : WindowMode.Windowed;
Engine.ApplyWindowSettings(new DisplaySettings { WindowMode = newMode });

// Change windowed size without touching fullscreen state
Engine.ApplyWindowSettings(new DisplaySettings
{
    WindowMode           = WindowMode.Windowed,
    PreferredWindowWidth  = 1920,
    PreferredWindowHeight = 1080,
});
```

`Engine.DisplaySettings.WindowMode` always reflects the current active mode after `ApplyWindowSettings` returns.

## Camera Position (Scrolling)

For fixed-screen games (Pong, etc.), leave `Camera.X = 0` and `Camera.Y = 0` (the defaults).

For manual scrolling, set `Camera.X`/`Camera.Y` each frame from `CustomActivity`:
```csharp
Camera.X = player.X;
Camera.Y = player.Y;
```

## Camera Physics — Smooth Movement and Transitions

`Camera` has the same velocity and acceleration properties as `Entity`:

```csharp
Camera.VelocityX    // world units/sec — applied each frame by the engine physics loop
Camera.VelocityY
Camera.AccelerationX
Camera.AccelerationY
```

The camera is updated by the same physics loop as entities — set velocity or acceleration and it moves automatically each frame. **Do not lerp `Camera.X`/`Camera.Y` manually** — use velocity instead.

```csharp
// Slide camera right one screen width
Camera.VelocityX = Camera.TargetWidth;  // moves one screen-width per second
```

For a timed one-shot slide, use an async delay to stop it:

```csharp
float targetX = Camera.X + Camera.TargetWidth;
Camera.VelocityX = Camera.TargetWidth;
await Engine.Time.DelaySeconds(1.0, Token);
Camera.VelocityX = 0f;
Camera.X = targetX;   // snap to exact position to eliminate drift
```

> **Note:** Camera has no `Drag` property — velocity must be zeroed explicitly.

## CameraControllingEntity — Automatic Following

`CameraControllingEntity` (in `FlatRedBall2.Entities`) is an `Entity` subclass that handles following, map clamping, deadzone, pixel-snapping, and screen shake automatically.

**Always create it via `Factory<CameraControllingEntity>`** — Factory calls `CustomInitialize`, which wires up the camera. `Screen.Register` does NOT call `CustomInitialize`.

```csharp
private Factory<CameraControllingEntity> _cameraFactory = null!;

public override void CustomInitialize()
{
    _cameraFactory = new Factory<CameraControllingEntity>(this);

    var mapBounds = new AxisAlignedRectangle { Width = 2560f, Height = 1440f }; // centered at origin

    var cam = _cameraFactory.Create(); // sets cam.Camera = Screen.Camera
    cam.Target = player;               // or cam.Targets.Add(p1); cam.Targets.Add(p2);
    cam.Map = mapBounds;               // clamps camera; null = no bounds
    cam.TargetApproachStyle = TargetApproachStyle.Smooth;
    cam.TargetApproachCoefficient = 8f; // higher = faster
}
```

**Approach styles:**
- `Immediate` — locks to target each frame (no lag)
- `Smooth` — exponential ease; speed = coefficient × distance (default 5)
- `ConstantSpeed` — moves at fixed world-units/sec, snaps when close

**Deadzone** — camera only pans when the target leaves the window:
```csharp
cam.ScrollingWindowWidth  = 200f;
cam.ScrollingWindowHeight = 120f;
```

**Pixel-perfect snapping** — on by default (`SnapToPixel = true`). Uses `Camera.PixelsPerUnit` for exactness across window resizes.

**Screen shake** (async, pass `Token` to cancel on screen transition):
```csharp
_ = cam.ShakeScreen(radius: 8f, durationInSeconds: 0.4f, Token);
```

**Debug overlay** — shows the deadzone window:
```csharp
cam.ShowDebugOverlay = true;
```

**Multi-target auto-zoom** (frames all targets in view):
```csharp
cam.EnableAutoZooming(defaultZoom: Camera.Zoom, furthestMultiplier: 3f);
```

## Screen Shake (manual, no CameraControllingEntity)

Set `Camera.X`/`Camera.Y` directly each frame to a random offset that decays to zero. No drift; resets cleanly.

## Coordinate Conversion

```csharp
// World → screen pixels
System.Numerics.Vector2 screenPos = Camera.WorldToScreen(worldPos);

// Screen pixels → world
System.Numerics.Vector2 worldPos = Camera.ScreenToWorld(screenPos);
```

Useful when placing Gum HUD elements relative to world objects, or for click-to-move logic where you receive screen-space mouse coordinates.

## Gotchas

- **Manual map-clamping: guard against small maps.** `Math.Clamp(x, min, max)` throws `ArgumentException` if `min > max`. This happens when the map is smaller than the viewport — `mapHalfW - halfW` goes negative. Always guard:
  ```csharp
  Camera.X = mapHalfW > halfW
      ? Math.Clamp(_player.X, -mapHalfW + halfW, mapHalfW - halfW)
      : 0f;  // map fits in viewport — center it
  ```
  This applies to both X and Y axes. **Use `CameraControllingEntity` (with `Map` set) to avoid writing this yourself.**
- **Use `DisplaySettings.Zoom` for setup; use `Camera.Zoom` for dynamic zoom effects.** `TargetWidth`/`TargetHeight` (the world coordinate extents) are calculated by the engine at screen-start time using `DisplaySettings.Zoom`. If you only set `Camera.Zoom` directly at setup time (e.g. in `CustomInitialize`), `TargetWidth`/`TargetHeight` were already sized for Zoom=1 — the rendered image is zoomed but the coordinate space is not, so objects placed at the edges of the intended world fall off-screen. Set the baseline zoom in `DisplaySettings` before `Start<T>()`:
  ```csharp
  FlatRedBallService.Default.DisplaySettings.Zoom = 2f;
  FlatRedBallService.Default.Start<MyScreen>();
  ```
  `Camera.Zoom` is the right tool for dynamic zoom during gameplay (zoom-in on a boss, zoom-out to show the full arena, etc.) — just be aware that zoom is relative to the `TargetWidth`/`TargetHeight` established at screen start.
- **`Camera.Zoom` is reset on every screen transition.** The engine sets `Camera.Zoom = DisplaySettings.Zoom` when a new screen starts. Any direct assignment to `Camera.Zoom` is lost on screen change. Use `DisplaySettings.Zoom` (or a per-screen `PreferredDisplaySettings` override) for the baseline zoom that should apply from the moment the screen appears.
- **Gum coordinates are independent of Camera.** Gum X/Y are screen pixels, Y-down from the top-left — they do not shift when the camera moves. Only world-space objects (entities, shapes) are affected by camera position.
- **TargetWidth/Height ≠ window pixel size.** The camera scales world units to fill whatever window resolution MonoGame uses. A 1280×720 world still renders correctly in an 800×480 window — it just appears smaller.
- **Do not set `TargetWidth`/`TargetHeight` directly.** They have `internal set` and are managed by the engine from `DisplaySettings`. Use `Camera.Zoom` for runtime zoom effects.
