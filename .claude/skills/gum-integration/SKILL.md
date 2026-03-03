# Gum Integration in FlatRedBall2

## Overview

Gum is FlatRedBall2's UI system, backed by the `Gum.MonoGame` NuGet package. It is **automatically initialized** by `FlatRedBallService.Initialize` — no setup required in `Game1`. Gum elements render interleaved with game-world objects via the same Layer/Z sort used by sprites and shapes.

Key types live in `FlatRedBall2.UI`:
- `GumRenderable` — wraps a Gum `GraphicalUiElement` as an `IRenderable` (internal implementation detail — users do not construct this directly)
- `GumRenderBatch` — the `IRenderBatch` that drives Gum's draw pass (internal, singleton)

## Two Levels of Gum API

Choose the right level for your use case:

**FrameworkElement controls (high-level, preferred)**: `Button`, `Label`, `TextBox`, `CheckBox`, `StackPanel`, `Panel`, etc. from `Gum.Forms.Controls`. These have built-in functionality — click events, hover, focus, keyboard navigation, layout. Pass directly to `AddGum(control)`.

**GraphicalUiElement visuals (low-level)**: The raw visual tree. Pass directly to `AddGum(visual)`. Use only when Forms controls do not expose what you need — for example, `TextRuntime` for a custom `FontSize` that `Label` does not surface.

**Rule: prefer FrameworkElement. Drop to visuals only when necessary.**

## Quick Start

```csharp
// Always import BOTH — Forms controls and Wireframe (Anchor/Dock) live in different namespaces:
using Gum.Forms.Controls;  // Label, Panel, StackPanel, Button, etc.
using Gum.Wireframe;        // Anchor, Dock

// In Screen.CustomInitialize():
var button = new Button();
button.Text = "Click Me";
button.Click += (_, _) => Debug.WriteLine("clicked");

AddGum(button);   // pass FrameworkElement directly
```

## Layout

Gum has a full layout engine. Prefer layout over hard-coded pixel coordinates so your UI adapts to different sizes and content.

### Positioning Units (XUnits / YUnits)

`X` and `Y` default to pixels from the top-left corner of the parent (or screen). Change `XUnits`/`YUnits` to reposition relative to other edges or the center:

| Unit | Meaning |
|---|---|
| `PixelsFromSmall` (default) | Pixels from left (X) or top (Y) |
| `PixelsFromLarge` | Pixels inward from right (X) or bottom (Y) |
| `PixelsFromMiddle` | Pixels from horizontal/vertical center |
| `Percentage` | Percentage of parent size (0–100) |

```csharp
label.XUnits = GeneralUnitType.PixelsFromLarge;
label.X = 20;   // 20px from the right edge
```

### Size Units (WidthUnits / HeightUnits)

| Unit | Meaning |
|---|---|
| `Absolute` (default) | Fixed pixel size |
| `RelativeToParent` | Parent size + offset (0 = match parent; -20 = 20px smaller) |
| `PercentageOfParent` | Percentage of parent size (100 = full parent) |
| `RelativeToChildren` | Sizes to wrap children + offset (0 = tight wrap) |

```csharp
label.WidthUnits = DimensionUnitType.RelativeToParent;
label.Width = 0;   // same width as parent
```

### Anchor and Dock

`Anchor` and `Dock` are convenience helpers that set units for you:

```csharp
// Anchor — pins to a corner/edge/center of the parent
label.Anchor(Anchor.BottomRight);   // bottom-right corner; X/Y offset inward from there

// Dock — stretches to fill the parent in one or both dimensions
panel.Dock(Dock.Fill);             // fill parent entirely
panel.Dock(Dock.FillHorizontally); // full width, auto height
panel.Dock(Dock.SizeToChildren);   // shrink-wrap content (default for Panel)
```

`Anchor` and `Dock` are in `Gum.Wireframe` namespace.

### StackPanel — Automatic Stacking

`StackPanel` stacks children vertically (default) or horizontally with optional spacing. No manual X/Y positioning needed for children.

```csharp
using Gum.Forms.Controls;

var menu = new StackPanel();
menu.Spacing = 8;
menu.Anchor(Anchor.Center);   // center the panel on screen

menu.AddChild(new Button { Text = "Start" });
menu.AddChild(new Button { Text = "Options" });
menu.AddChild(new Button { Text = "Quit" });

AddGum(menu);
```

For horizontal layout:
```csharp
var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
```

### Panel — Absolute Positioning Container

`Panel` is the base of `StackPanel`. Use it to group controls with manual positioning, or as a root for mixed layouts. It defaults to `Dock.SizeToChildren`.

```csharp
var hud = new Panel();
hud.Dock(Dock.Fill);

var scoreLabel = new Label { Text = "0" };
scoreLabel.Anchor(Anchor.TopRight);
scoreLabel.X = -20;   // 20px from right
scoreLabel.Y = 20;

hud.AddChild(scoreLabel);
AddGum(hud);
```

## GumRenderable (Low-Level)

`GumRenderable` is an internal implementation detail — the screen creates it for you. You never construct it directly. Simply pass any `GraphicalUiElement` to `AddGum`:

```csharp
var text = new TextRuntime { Text = "Score", FontSize = 48 };
AddGum(text);
```

## Screen.AddGum / RemoveGum

Always use `AddGum` instead of adding directly to `RenderList`:

```csharp
AddGum(button);          // FrameworkElement
AddGum(textRuntime);     // GraphicalUiElement (low-level visual)
RemoveGum(button);
RemoveGum(textRuntime);
```

**Do not call `button.AddToRoot()`** — this bypasses the FRB2 render-order system.

## Render Ordering (Layer / Z)

Gum elements sort into `Screen.RenderList` by Layer + Z, exactly like sprites and shapes.

| Setting | Effect |
|---|---|
| `Layer = null` (default) | Drawn last — on top of all world objects. Right for HUDs and menus. |
| `z: 50f` parameter | Control ordering among multiple Gum elements or between Gum and sprites |
| `Layer` on a named Layer | Place on a named Layer for explicit interleaving with game world |

Pass Z at the call site:
```csharp
AddGum(bgPanel, z: 0f);
AddGum(hudPanel, z: 100f);
```

## Displaying Text (HUD, Score, Labels)

**Option A — `Label` (preferred FrameworkElement)**:

```csharp
var scoreLabel = new Label();
scoreLabel.Text = "0";
scoreLabel.Anchor(Anchor.TopRight);
scoreLabel.X = -20;
scoreLabel.Y = 20;
AddGum(scoreLabel);

// Update at runtime:
scoreLabel.Text = _score.ToString();
```

**Option B — `TextRuntime` (low-level visual)**: Use only when you need `FontSize`, `FontScale`, etc. not exposed by `Label`.

```csharp
using MonoGameGum.GueDeriving;   // TextRuntime lives here, NOT Gum.Wireframe

var scoreText = new TextRuntime { Text = "0", FontSize = 48 };
AddGum(scoreText);
```

**Coordinate gotcha (screen-space)**: Gum X/Y are screen pixels, Y-down from the top-left corner — opposite of the game world (Y-up, centered). Use `Anchor`/`Dock` to avoid hard-coding pixel positions.

**Coordinate gotcha (world-space)**: When using `entity.AddGum`, the entity's `X/Y` are world coordinates (Y-up). The conversion to screen pixels happens automatically. Do not manually set `Visual.X/Y` on a world-space Gum element — it will be overwritten each frame.

## Input / Interactivity

`AddGum` automatically registers the element for per-frame input updates (cursor, clicks, hover, keyboard). All Gum Forms controls work out of the box.

```csharp
var button = new Button();
button.Click += (_, _) => MoveToScreen<GameScreen>();
AddGum(button);
```

## Screen Transitions

Gum is fully cleaned up on every screen transition — no manual teardown needed:
- `Screen._gumRenderables` is abandoned with the old Screen object
- `GumService.Default.Root.Children` is cleared in `FlatRedBallService.ActivateScreen`

**There is no way to persist Gum elements across screen transitions.** Add them fresh in each screen's `CustomInitialize`.

## Gotchas

- **Namespace**: `TextRuntime` is in `MonoGameGum.GueDeriving` — not `Gum.Wireframe`. Forms controls are in `Gum.Forms.Controls`. `Anchor`/`Dock` enums are in `Gum.Wireframe`. FRB2's wrapper types are in `FlatRedBall2.UI`.
- **`GumRenderBatch`** is the FRB2 `IRenderBatch` wrapper. Do not confuse it with Gum's own `RenderingLibrary.Graphics.GumBatch`.
- **Initialize order**: Do not create Gum elements before `FlatRedBallService.Initialize` is called.
- **`AddToRoot()` is NOT the FRB2 pattern**. Use `AddGum` instead.

## Screen-Space vs World-Space

### Screen-Space (default)

`Screen.AddGum` places the element in screen space — Gum's native coordinate system (pixels, Y-down, origin top-left). Use this for HUDs, menus, and any UI that should stay fixed on screen regardless of where the camera is.

```csharp
// In Screen.CustomInitialize():
var scoreLabel = new Label { Text = "0" };
scoreLabel.Anchor(Anchor.TopRight);
AddGum(scoreLabel);   // screen-space — stays in corner as camera moves
```

### World-Space (entity-attached)

`Entity.AddGum` places a Gum element at the entity's world position. Each frame, `AbsoluteX/Y` is converted through the camera to screen pixels — so the visual follows the entity as it moves, and shifts when the camera pans.

```csharp
// In a custom Entity.CustomInitialize():
var label = new TextRuntime { Text = "Enemy", FontSize = 24 };
AddGum(label);   // world-space — follows this entity
```

The visual is automatically removed when the entity is destroyed. `RemoveGum` is available if you need to detach it earlier.

### Pattern: Transient World-Space Text (e.g., Floating Score)

For text that spawns at a world position, animates, and then disappears (like "+100" after hitting an enemy), create a short-lived entity that owns the animation:

```csharp
// User-defined in game code — not a FlatRedBall2 type:
class ScoreFloater : Entity
{
    private float _lifetime;
    private TextRuntime _label = new TextRuntime { FontSize = 32 };

    public int Score { set => _label.Text = $"+{value}"; }

    public override void CustomInitialize()
    {
        VelocityY = 80f;
        AddGum(_label);   // world-space — floats up with the entity
    }

    public override void CustomActivity(FrameTime time)
    {
        _lifetime += time.DeltaSeconds;
        if (_lifetime > 1.2f) Destroy();
    }
}

// Spawn at the hit position:
var floater = Factory<ScoreFloater>.Create();
floater.X = hitX;
floater.Y = hitY;
floater.Score = 100;
```

Physics (velocity, drag, acceleration) move the entity in world space, and the Gum visual follows automatically. No manual screen-coordinate math is needed.
