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
ds.ResolutionWidth  = 1280;          // design world width at Zoom=1 (see "Sizing" below)
ds.ResolutionHeight = 720;           // design world height at Zoom=1
ds.Zoom             = 1f;            // initial camera zoom — read "Sizing" before changing
ds.ResizeMode       = ResizeMode.StretchVisibleArea;   // or IncreaseVisibleArea
ds.FixedAspectRatio = 16f / 9f;     // null = fill window; set to add letterbox/pillarbox bars
ds.LetterboxColor   = Color.Black;   // bar color when FixedAspectRatio is set
ds.WindowMode       = WindowMode.Windowed;             // or FullscreenBorderless
ds.PreferredWindowWidth  = 1280;     // startup window pixel size (null = leave as-is)
ds.PreferredWindowHeight = 720;
ds.AllowUserResizing = true;         // whether the player can drag window borders
```

**`Zoom` initializes `Camera.Zoom`** at each screen start. After that, the camera is independent — game code can modify `Camera.Zoom` freely during gameplay.

## Sizing — Resolution × Window × Zoom

Three knobs decide what's on screen. Get this wrong and entities silently fall off-screen.

| Knob | Units | What it controls |
|---|---|---|
| `ResolutionWidth/Height` | world units | Design world size at `Zoom = 1` |
| `PreferredWindowWidth/Height` | screen pixels | Actual window size |
| `Zoom` | multiplier | Camera zoom on top of the above |

**Effective screen-pixels per world-unit and visible world width:**

| ResizeMode | Pixels per world unit | Visible world width |
|---|---|---|
| `StretchVisibleArea` (default) | `(window / resolution) × Zoom` | `resolution / Zoom` |
| `IncreaseVisibleArea` | `Zoom` | `window / Zoom` |

**Critical footgun (StretchVisibleArea):** `Zoom` multiplies *on top of* the window-vs-resolution stretch. If you set window = 2× resolution to scale up pixel art, the stretch already doubles everything. Setting `Zoom = 2` zooms in **4×**, hiding three-quarters of your design world.

### Recipes

**Pixel-art game — small native resolution, large window:**
```csharp
ds.ResolutionWidth  = 640;           // design world
ds.ResolutionHeight = 352;
ds.PreferredWindowWidth  = 1280;     // window 2× resolution → automatic 2× scale
ds.PreferredWindowHeight = 704;
ds.Zoom = 1f;                        // ← LEAVE AT 1; the window stretch does the scaling
ds.ResizeMode = ResizeMode.StretchVisibleArea;
```
Result: full 640×352 world visible, scaled crisply 2× to fill 1280×704.

**Window matches resolution, Zoom for cinematic effects:**
```csharp
ds.ResolutionWidth  = 1280;
ds.ResolutionHeight = 720;
ds.PreferredWindowWidth  = 1280;
ds.PreferredWindowHeight = 720;
ds.Zoom = 1f;                        // shows full 1280×720 world
// Game code can later set Camera.Zoom = 2 to zoom in on a boss, etc.
```

**Crisp pixel ratio that grows with window:**
```csharp
ds.ResizeMode = ResizeMode.IncreaseVisibleArea;
ds.Zoom = 2f;                        // exactly 2 screen pixels per world unit
// No stretching, no fuzz. Bigger window reveals more world.
```

### ResizeMode summary

- `StretchVisibleArea` (default) — same `ResolutionWidth × ResolutionHeight / Zoom` world area always visible, stretched to fill window. Use for fixed-screen games where the level should never reveal more or less of itself based on window size.
- `IncreaseVisibleArea` — pixels-per-world-unit stays fixed at `Zoom`; a larger window reveals more world. Use for variable-window games where you want crisp art and don't mind the playfield growing.

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

    var mapBounds = new BoundsRectangle(2560f, 1440f); // centered at origin; use BoundsRectangle(x, y, w, h) for maps not centered at origin

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

## Free-Roaming Camera (No Entity to Follow)

City builders, strategy games, and level editors require a camera that the player directly drives — there is no player entity to follow. Do **not** use `CameraControllingEntity` for this pattern; drive the camera directly from screen `CustomActivity`.

### WASD Pan + Clamp to Map Bounds

```csharp
// In Screen.CustomActivity:
const float PanSpeed = 400f;           // world units/sec
const float MapHalfW = 128 * 16 / 2f; // half the world width  (tileCount * tileSize / 2)
const float MapHalfH = 128 * 16 / 2f; // half the world height

var kb = Engine.Input.Keyboard;

Camera.VelocityX = 0f;
Camera.VelocityY = 0f;

if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left))  Camera.VelocityX = -PanSpeed;
if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right)) Camera.VelocityX =  PanSpeed;
if (kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down))  Camera.VelocityY = -PanSpeed;
if (kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up))    Camera.VelocityY =  PanSpeed;

// Clamp after the engine has applied velocity (do it next frame via properties):
Camera.X = Math.Clamp(Camera.X, -MapHalfW + Camera.TargetWidth  / 2f,
                                  MapHalfW - Camera.TargetWidth  / 2f);
Camera.Y = Math.Clamp(Camera.Y, -MapHalfH + Camera.TargetHeight / 2f,
                                  MapHalfH - Camera.TargetHeight / 2f);
```

> **Note:** Camera has no `Drag` — set velocity to `0` explicitly each frame when the key is not held. Setting velocity each frame (not accumulating) keeps movement instant and frame-rate independent.

### Edge Scrolling

When the cursor is near the screen edge, add pan velocity in that direction:

```csharp
const float EdgeThreshold = 20f; // screen pixels from edge
var screenPos = Engine.Input.Cursor.ScreenPosition;

if (screenPos.X < EdgeThreshold)                          Camera.VelocityX -= PanSpeed;
if (screenPos.X > Engine.DisplaySettings.Width - EdgeThreshold) Camera.VelocityX += PanSpeed;
if (screenPos.Y < EdgeThreshold)                          Camera.VelocityY += PanSpeed; // Y-down screen → Y-up world
if (screenPos.Y > Engine.DisplaySettings.Height - EdgeThreshold) Camera.VelocityY -= PanSpeed;
```

Combine freely with WASD — velocities accumulate. Apply the same clamp after.

### Middle-Mouse Drag Pan

```csharp
private Vector2 _dragStartCamera;
private Vector2 _dragStartScreen;

// In CustomActivity:
var cursor = Engine.Input.Cursor;
if (cursor.MiddlePressed)
{
    _dragStartCamera = new Vector2(Camera.X, Camera.Y);
    _dragStartScreen = cursor.ScreenPosition;
}
if (cursor.MiddleDown)
{
    var delta = cursor.ScreenPosition - _dragStartScreen;
    // Screen pixels → world units (screen pixel = 1 world unit / Zoom * PixelsPerUnit)
    float ppu = Camera.PixelsPerUnit * Camera.Zoom;
    Camera.X = _dragStartCamera.X - delta.X / ppu;
    Camera.Y = _dragStartCamera.Y + delta.Y / ppu; // screen Y-down → world Y-up
    Camera.VelocityX = 0f;
    Camera.VelocityY = 0f;
}
```

### Scroll Wheel Zoom with Clamp

```csharp
const float ZoomStep  = 0.1f;
const float ZoomMin   = 0.5f;
const float ZoomMax   = 3.0f;

int scroll = Engine.Input.Cursor.ScrollWheelDelta;
if (scroll != 0)
{
    float direction = scroll > 0 ? 1f : -1f;
    Camera.Zoom = Math.Clamp(Camera.Zoom + direction * ZoomStep, ZoomMin, ZoomMax);
}
```

`Camera.Zoom` is reset on every screen transition (see Gotchas). If you need a persistent zoom level, store it in a field and re-apply it in `CustomInitialize`.

## Gotchas

- **Zoom multiplies the window-vs-resolution stretch in StretchVisibleArea mode.** If your window is 2× your resolution to scale pixel art, leave `Zoom = 1` — the window stretch already scales 2×. Setting Zoom = 2 there zooms in 4× and hides three-quarters of the level. See "Sizing — Resolution × Window × Zoom" above for the full math and recipes.
- **Use `DisplaySettings.Zoom` for setup; use `Camera.Zoom` for dynamic zoom effects.** `TargetWidth/Height` are computed at screen-start from `DisplaySettings.Zoom`. Setting `Camera.Zoom` in `CustomInitialize` zooms the rendered image but not the coordinate space, so edge-placed objects fall off-screen. Use `DisplaySettings.Zoom` (or per-screen `PreferredDisplaySettings`) for the baseline; reserve `Camera.Zoom` for runtime tweens (zoom-in on a boss, etc.).
- **`Camera.Zoom` is reset on every screen transition.** The engine sets `Camera.Zoom = DisplaySettings.Zoom` when a new screen starts. Any direct assignment to `Camera.Zoom` is lost on screen change. Use `DisplaySettings.Zoom` (or a per-screen `PreferredDisplaySettings` override) for the baseline zoom that should apply from the moment the screen appears.
- **Gum coordinates are independent of Camera.** Gum X/Y are screen pixels, Y-down from the top-left — they do not shift when the camera moves. Only world-space objects (entities, shapes) are affected by camera position.
- **TargetWidth/Height ≠ window pixel size.** The camera scales world units to fill whatever window resolution MonoGame uses. A 1280×720 world still renders correctly in an 800×480 window — it just appears smaller.
- **Do not set `TargetWidth`/`TargetHeight` directly.** They have `internal set` and are managed by the engine from `DisplaySettings`. Use `Camera.Zoom` for runtime zoom effects.
- **Viewport edge coordinates**: Use `Camera.Left`, `Camera.Right`, `Camera.Top`, `Camera.Bottom` for world-space edges — these are Zoom-correct. Do not compute edges from `Camera.X ± Camera.TargetWidth / 2f` — that formula is wrong when `Zoom ≠ 1`.
