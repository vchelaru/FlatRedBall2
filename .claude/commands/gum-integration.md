# Gum Integration in FlatRedBall2

## Overview

Gum is FlatRedBall2's UI system, backed by the `Gum.MonoGame` NuGet package. It is **automatically initialized** by `FlatRedBallService.Initialize` — no setup required in `Game1`. Gum elements render interleaved with game-world objects via the same Layer/Z sort used by sprites and shapes.

Key types live in `FlatRedBall2.UI`:
- `GumRenderable` — wraps a Gum `GraphicalUiElement` as an `IRenderable`
- `GumRenderBatch` — the `IRenderBatch` that drives Gum's draw pass (internal, singleton)

## Two Levels of Gum API

Choose the right level for your use case:

**FrameworkElement controls (high-level, preferred)**: `Button`, `Label`, `TextBox`, `CheckBox`, `StackPanel`, `Panel`, etc. from `Gum.Forms.Controls`. These have built-in functionality — click events, hover, focus, keyboard navigation, layout. Pass directly to `AddGum(control)`.

**GraphicalUiElement visuals (low-level)**: The raw visual tree. Must be wrapped in `GumRenderable`. Use only when Forms controls do not expose what you need — for example, `TextRuntime` for a custom `FontSize` that `Label` does not surface.

**Rule: prefer FrameworkElement. Drop to visuals only when necessary.**

## Quick Start

```csharp
// In Screen.CustomInitialize():
using Gum.Forms.Controls;

var button = new Button();
button.Text = "Click Me";
button.Click += (_, _) => Debug.WriteLine("clicked");

AddGum(button);   // pass FrameworkElement directly — no GumRenderable needed
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

`GumRenderable` wraps any `GraphicalUiElement`. Use it only when working at the visual level (e.g., `TextRuntime` with `FontSize`):

```csharp
var text = new TextRuntime { Text = "Score", FontSize = 48 };
AddGum(new GumRenderable(text));
```

## Screen.AddGum / RemoveGum

Always use `AddGum` instead of adding directly to `RenderList`:

```csharp
// FrameworkElement overload — returns the GumRenderable wrapper:
var renderable = AddGum(button);

// GumRenderable overload — for low-level visuals:
AddGum(new GumRenderable(text));

// Remove by the GumRenderable handle:
RemoveGum(renderable);
```

**Do not call `button.AddToRoot()`** — this bypasses the FRB2 render-order system.

## Render Ordering (Layer / Z)

`GumRenderable` sorts into `Screen.RenderList` by Layer + Z, exactly like sprites and shapes.

| Setting | Effect |
|---|---|
| `Layer = null` (default) | Drawn last — on top of all world objects. Right for HUDs and menus. |
| `hud.Z = 50f` | Control ordering among multiple GumRenderables or between Gum and sprites |
| `hud.Layer = someLayer` | Place on a named Layer for explicit interleaving with game world |

When using `AddGum(FrameworkElement)`, set Z on the returned `GumRenderable`:
```csharp
AddGum(scoreLabel).Z = 100;
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
AddGum(new GumRenderable(scoreText));
```

**Coordinate gotcha**: Gum X/Y are screen pixels, Y-down from the top-left corner — opposite of the game world (Y-up, centered). Use `Anchor`/`Dock` to avoid hard-coding pixel positions.

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
- **Initialize order**: Do not create `GumRenderable` before `FlatRedBallService.Initialize` is called.
- **`AddToRoot()` is NOT the FRB2 pattern**. Use `AddGum` instead.

## TODO: World-Space Attachment

Currently all `GumRenderable` instances render in screen space. Future work: if a `GumRenderable` is attached to an `Entity`, project `AbsoluteX/Y` through `camera.WorldToScreen` to follow the entity (e.g., a health bar above a character). See the `TODO` comment in `GumRenderable.Draw`.
