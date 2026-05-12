---
name: camera
description: "Camera in FlatRedBall2. Use when working with camera setup, background color, world bounds, window resolution, scrolling, screen shake, coordinate conversion between world and screen space, AspectPolicy / ResizeMode / DominantAxis, or Camera.OrthogonalWidth/OrthogonalHeight. Trigger on any camera-related question including viewport, following a player, letterboxing, or screen boundaries."
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

`Camera.OrthogonalWidth` and `Camera.OrthogonalHeight` are the visible world extents at `Zoom = 1`. They are computed by the engine from `DisplaySettings` — do not assign directly.

World coordinates are **centered at the origin**:

- X ∈ [−OrthogonalWidth/2, OrthogonalWidth/2]
- Y ∈ [−OrthogonalHeight/2, OrthogonalHeight/2]

Y+ is **up** (see `physics-and-movement`).

```csharp
wall.Y = -Camera.OrthogonalHeight / 2f;  // bottom of screen
```

Prefer `Camera.Left`/`Right`/`Top`/`Bottom` for edges — they account for `Zoom` and camera position.

> **Screen-edge boundaries** (keeping entities in bounds) are a collision concern, not a camera concern — use wall entities or `TileShapes`. See the `collision-relationships` and `shapes` skills.

## DisplaySettings — Resolution, Aspect, Zoom

`FlatRedBallService.Default.DisplaySettings` controls how the camera is configured at each screen start. Set these before calling `Start<T>()` or between screens.

```csharp
var ds = FlatRedBallService.Default.DisplaySettings;
ds.ResolutionWidth  = 1280;          // design world width  in world units
ds.ResolutionHeight = 720;           // design world height in world units
ds.AspectPolicy     = AspectPolicy.Locked;       // default: pillar/letterbox to aspect
ds.FixedAspectRatio = null;          // default: derive from Resolution; set to override
ds.DominantAxis     = DominantAxis.Height;       // default: pin design height when aspect differs
ds.ResizeMode       = ResizeMode.StretchVisibleArea; // default: same world always visible
ds.LetterboxColor   = Color.Black;
ds.WindowMode       = WindowMode.Windowed;       // or FullscreenBorderless
ds.PreferredWindowWidth  = 1280;     // startup window pixel size (null = leave as-is)
ds.PreferredWindowHeight = 720;
ds.AllowUserResizing = false;        // default: fixed canvas; set true to let the player resize
```

## Three Knobs: AspectPolicy × ResizeMode × DominantAxis

Three orthogonal settings decide what the player sees on resize. The defaults are the safe choice — pillarbox to design aspect, world stays put, no distortion ever.

### AspectPolicy

- **`Locked`** (default) — viewport is centered inside the window with letterbox/pillarbox bars to enforce the effective aspect ratio. Aspect comes from `FixedAspectRatio` if set, else `ResolutionWidth/ResolutionHeight`.
- **`Free`** — viewport fills the window. The visible world's aspect follows the window's; resizing changes how much world is visible.

### ResizeMode

- **`StretchVisibleArea`** (default) — the dominant-axis world extent is fixed at its `Resolution*` value. A larger window just rescales pixels. Combined with `Locked` aspect, the entire design world is always exactly visible.
- **`IncreaseVisibleArea`** — pixels-per-world-unit is fixed at `Zoom`. A larger window reveals more world (proportionally on both axes under Locked, on the non-dominant axis under Free + Stretch — see below).

### DominantAxis

Consulted under `Free`, or under `Locked` if `FixedAspectRatio` is set to a value that differs from the design ratio.

- **`Height`** (default) — design height stays at `ResolutionHeight`; design width tracks the effective aspect.
- **`Width`** — design width stays at `ResolutionWidth`; design height tracks.

## Recipes

**Default — locked aspect, no surprises (recommended for fixed-camera games):**
```csharp
ds.ResolutionWidth  = 240;   ds.ResolutionHeight = 320;
ds.PreferredWindowWidth = 720; ds.PreferredWindowHeight = 960;  // 3× scale
// AspectPolicy.Locked, ResizeMode.StretchVisibleArea, FixedAspectRatio=null are all defaults
ds.AllowUserResizing = true;
```
Result: 240×320 world always visible. Resize freely — the rendered area pillarboxes/letterboxes to keep 0.75 aspect; the playfield never grows.

**Pixel-art crisp scaling that grows with the window:**
```csharp
ds.AspectPolicy = AspectPolicy.Free;
ds.ResizeMode   = ResizeMode.IncreaseVisibleArea;
// Pixels-per-unit comes from PreferredWindowWidth/Height vs ResolutionWidth/Height,
// or from a CustomInitialize override of Camera.Zoom for non-default starts.
```
No bars; bigger window reveals more world on both axes. Sprites stay at native pixel size.

**Side-scroller — fixed camera height, world width tracks window aspect:**
```csharp
ds.AspectPolicy = AspectPolicy.Free;
ds.DominantAxis = DominantAxis.Height;
ds.ResizeMode   = ResizeMode.StretchVisibleArea;
```
Window grows wider → more level visible horizontally. Window grows taller → height stays at `ResolutionHeight`, pixels just bigger.

**Explicit display ratio different from design (e.g. 320×240 design always rendered 16:9):**
```csharp
ds.ResolutionWidth = 320; ds.ResolutionHeight = 240;
ds.AspectPolicy     = AspectPolicy.Locked;
ds.FixedAspectRatio = 16f / 9f;        // override; design gets letterboxed inside 16:9
ds.DominantAxis     = DominantAxis.Height;
```

## Runtime Camera Zoom

```csharp
Camera.Zoom = 2f;   // zoom in: shows half the world area
Camera.Zoom = 0.5f; // zoom out: shows double the world area
```

`Camera.Zoom` is reset to `1f` at the start of each new screen. Screens that want a non-default starting zoom assign `Camera.Zoom` in `CustomInitialize`. Most games leave runtime zoom at 1 — the on-screen scale comes from the window-vs-resolution ratio, not from `Zoom`.

## Window Resolution and Fullscreen

`Camera.OrthogonalWidth/Height` do **not** control the actual window pixel size. Window size is set via `DisplaySettings` and applied in two ways:

**Startup** — call `PrepareWindow<T>` from the `Game1` constructor (before `Initialize`) so the window opens at the right size with no flicker:
```csharp
public Game1()
{
    _graphics = new GraphicsDeviceManager(this);
    var ds = FlatRedBallService.Default.DisplaySettings;
    ds.PreferredWindowWidth  = 1280;
    ds.PreferredWindowHeight = 720;
    FlatRedBallService.Default.PrepareWindow<MyStartScreen>(_graphics);
}
```

**Runtime** (settings menu, F11 toggle) — call `ApplyWindowSettings` at any time:
```csharp
var newMode = Engine.DisplaySettings.WindowMode == WindowMode.Windowed
    ? WindowMode.FullscreenBorderless
    : WindowMode.Windowed;
Engine.ApplyWindowSettings(new DisplaySettings { WindowMode = newMode });
```

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
// Slide camera right one screen width per second
Camera.VelocityX = Camera.OrthogonalWidth;
```

For a timed one-shot slide, use an async delay to stop it:

```csharp
float targetX = Camera.X + Camera.OrthogonalWidth;
Camera.VelocityX = Camera.OrthogonalWidth;
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

    var mapBounds = new BoundsRectangle(2560f, 1440f); // centered at origin

    var cam = _cameraFactory.Create();
    cam.Target = player;
    cam.Map = mapBounds;               // clamps camera; null = no bounds
    cam.TargetApproachStyle = TargetApproachStyle.Smooth;
    cam.TargetApproachCoefficient = 8f;
}
```

**When using a `TileMap`, derive bounds from the map instead of hardcoding:**
```csharp
cam.Map = new BoundsRectangle(map.X + map.Width / 2f, map.Y - map.Height / 2f, map.Width, map.Height);
```

**Approach styles:** `Immediate`, `Smooth`, `ConstantSpeed`.

**Deadzone:**
```csharp
cam.ScrollingWindowWidth  = 200f;
cam.ScrollingWindowHeight = 120f;
```

**Pixel-perfect snapping** — on by default (`SnapToPixel = true`). Uses `Camera.PixelsPerUnit`.

**Screen shake** (async; pass `Token` to cancel on screen transition):
```csharp
_ = cam.ShakeScreen(radius: 8f, durationInSeconds: 0.4f, Token);
```

**Multi-target auto-zoom** (frames all targets in view):
```csharp
cam.EnableAutoZooming(defaultZoom: Camera.Zoom, furthestMultiplier: 3f);
```

## Coordinate Conversion

```csharp
System.Numerics.Vector2 screenPos = Camera.WorldToScreen(worldPos);
System.Numerics.Vector2 worldPos  = Camera.ScreenToWorld(screenPos);
```

## Free-Roaming Camera (No Entity to Follow)

Drive the camera directly from screen `CustomActivity`. Set velocity each frame (no `Drag` — zero it explicitly):

```csharp
const float PanSpeed = 400f;
const float MapHalfW = 128 * 16 / 2f;
const float MapHalfH = 128 * 16 / 2f;

var kb = Engine.Input.Keyboard;

Camera.VelocityX = 0f;
Camera.VelocityY = 0f;

if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left))  Camera.VelocityX = -PanSpeed;
if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right)) Camera.VelocityX =  PanSpeed;
if (kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down))  Camera.VelocityY = -PanSpeed;
if (kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up))    Camera.VelocityY =  PanSpeed;

Camera.X = Math.Clamp(Camera.X, -MapHalfW + Camera.OrthogonalWidth  / 2f,
                                  MapHalfW - Camera.OrthogonalWidth  / 2f);
Camera.Y = Math.Clamp(Camera.Y, -MapHalfH + Camera.OrthogonalHeight / 2f,
                                  MapHalfH - Camera.OrthogonalHeight / 2f);
```

### Scroll Wheel Zoom with Clamp

```csharp
const float ZoomStep = 0.1f, ZoomMin = 0.5f, ZoomMax = 3.0f;

int scroll = Engine.Input.Cursor.ScrollWheelDelta;
if (scroll != 0)
{
    float direction = scroll > 0 ? 1f : -1f;
    Camera.Zoom = Math.Clamp(Camera.Zoom + direction * ZoomStep, ZoomMin, ZoomMax);
}
```

`Camera.Zoom` is reset on every screen transition. For a persistent zoom, store it in a field and re-apply in `CustomInitialize`.

## Gotchas

- **`Camera.OrthogonalWidth/Height` ≠ window pixel size.** Under default `Locked` + `Stretch`, OrthogonalWidth/Height equal `ResolutionWidth/Height` regardless of window size — the window is just rescaled to fit. Under `Free` + `Stretch`, the non-dominant axis tracks the window aspect; under `IncreaseVisibleArea`, both axes track viewport pixels.
- **Express on-screen scale via window-vs-resolution, not zoom.** A 426×240 design rendered to a 1280×720 window auto-scales 3×. Setting `Camera.Zoom = 3` on top of that zooms in 3× (showing 1/3 of the design world) — almost always wrong. Reserve `Camera.Zoom` for runtime cinematic effects.
- **Do not set `OrthogonalWidth`/`OrthogonalHeight` directly.** They have `internal set` and are managed by the engine from `DisplaySettings`. Use `Camera.Zoom` for runtime zoom.
- **Viewport edge coordinates**: Use `Camera.Left`, `Camera.Right`, `Camera.Top`, `Camera.Bottom` — these are Zoom- and position-correct. Do not compute edges from `Camera.X ± Camera.OrthogonalWidth / 2f`.
- **Gum coordinates are independent of Camera.** Gum X/Y are screen pixels, Y-down from the top-left — they do not shift when the camera moves.
- **`AllowUserResizing` defaults to `false`.** Set to `true` opt-in. The default `Locked` aspect policy means resize is safe (pillarboxes), but you must opt in for the player to be able to drag window borders.
