# Gum Integration in FlatRedBall2

## Overview

Gum is FlatRedBall2's UI system, backed by the `Gum.MonoGame` NuGet package. It is **automatically initialized** by `FlatRedBallService.Initialize` — no setup required in `Game1`. Gum elements render interleaved with game-world objects via the same Layer/Z sort used by sprites and shapes.

Key types live in `FlatRedBall2.UI`:
- `GumRenderable` — wraps a Gum `GraphicalUiElement` as an `IRenderable`
- `GumRenderBatch` — the `IRenderBatch` that drives Gum's draw pass (internal, singleton)

## Quick Start

```csharp
// In Screen.CustomInitialize():
using FlatRedBall2.UI;
using Gum.Forms.Controls;

var button = new Button();
button.X = 100;
button.Y = 50;
button.Text = "Click Me";
button.Click += (_, _) => Console.WriteLine("clicked");

var hud = new GumRenderable(button.Visual);
AddGumRenderable(hud);   // Screen helper — handles rendering AND input
```

## GumRenderable

`GumRenderable` wraps any `GraphicalUiElement`. Pass the `.Visual` property of a Forms control, a `ContainerRuntime`, or any `GraphicalUiElement`:

```csharp
// Single Forms control:
new GumRenderable(button.Visual)

// Container holding multiple controls:
var panel = new StackPanel();
panel.AddChild(new Button());
panel.AddChild(new Label());
new GumRenderable(panel.Visual)
```

## Screen.AddGumRenderable / RemoveGumRenderable

Always use `AddGumRenderable` instead of adding directly to `RenderList`:

```csharp
AddGumRenderable(hud);      // adds to RenderList + registers for input updates
RemoveGumRenderable(hud);   // removes from both
```

**Do not call `button.AddToRoot()`** — this bypasses the FRB2 render-order system. Use `AddGumRenderable` instead.

## Render Ordering (Layer / Z)

`GumRenderable` sorts into `Screen.RenderList` by Layer + Z, exactly like sprites and shapes.

| Setting | Effect |
|---|---|
| `Layer = null` (default) | Drawn last — on top of all world objects. Right for HUDs and menus. |
| `hud.Z = 50f` | Control ordering among multiple GumRenderables or between Gum and sprites |
| `hud.Layer = someLayer` | Place on a named Layer for explicit interleaving with game world |

```csharp
// Multiple layers example:
var background = new GumRenderable(bgPanel.Visual) { Z = 0f };
var foreground = new GumRenderable(hudPanel.Visual) { Z = 100f };
AddGumRenderable(background);
AddGumRenderable(foreground);
```

## Displaying Text (HUD, Score, Labels)

Two options for non-interactive text:

**Option A — `Label` (Gum Forms)**: High-level, inherits the default Forms style. Pass `.Visual` to `GumRenderable`.

```csharp
using Gum.Forms.Controls;

var scoreLabel = new Label();
scoreLabel.Text = "0";
scoreLabel.X = 20;   // pixels from left edge of screen
scoreLabel.Y = 20;   // pixels from TOP — Gum is Y-down, origin top-left
AddGumRenderable(new GumRenderable(scoreLabel.Visual));
```

**Option B — `TextRuntime` (lower-level)**: Direct text element. Extend it, pass it directly (no `.Visual` — it IS a `GraphicalUiElement`). Provides `FontSize`, `FontScale`, `HorizontalAlignment`, and `Color`.

```csharp
using MonoGameGum.GueDeriving;   // TextRuntime lives here, NOT Gum.Wireframe

var scoreText = new TextRuntime { Text = "0", X = 20, Y = 20, FontSize = 48 };
AddGumRenderable(new GumRenderable(scoreText));

// Update text at runtime:
scoreText.Text = _score.ToString();
```

**Coordinate gotcha**: Gum X/Y are screen pixels, Y-down from the top-left corner. This is the opposite of the game world (Y-up, centered). Use small Y values to put text near the top of the screen.

## Input / Interactivity

`AddGumRenderable` automatically registers the element for per-frame input updates (cursor position, clicks, hover, keyboard). All Gum Forms controls (`Button`, `TextBox`, `ListBox`, `CheckBox`, etc.) work out of the box.

```csharp
var button = new Button();
button.Click += (_, _) => MoveToScreen<GameScreen>();
AddGumRenderable(new GumRenderable(button.Visual));
```

## Screen Transitions

Gum is fully cleaned up on every screen transition — no manual teardown needed:
- `Screen._gumRenderables` is abandoned with the old Screen object
- `GumService.Default.Root.Children` is cleared in `FlatRedBallService.ActivateScreen`

**There is no way to persist Gum elements across screen transitions.** Add them fresh in each screen's `CustomInitialize`.

## Gotchas

- **Namespace**: `TextRuntime` is in `MonoGameGum.GueDeriving` — not `Gum.Wireframe`. Other Gum types come from `Gum.Forms.Controls` or `Gum.Wireframe`. FRB2's wrapper types are in `FlatRedBall2.UI`. These are separate namespaces and do not conflict.
- **`GumRenderBatch`** is the FRB2 `IRenderBatch` wrapper. Do not confuse it with Gum's own `RenderingLibrary.Graphics.GumBatch` — that is an internal implementation detail.
- **Initialize order**: `GumRenderBatch.Instance` must be initialized before any `GumRenderable` draws. This happens automatically inside `FlatRedBallService.Initialize`. Do not create `GumRenderable` before `FlatRedBallService.Initialize` is called.
- **`AddToRoot()` is NOT the FRB2 pattern**. Elements added via `button.AddToRoot()` are NOT in the render-order system and will not receive FRB2-managed input updates.

## TODO: World-Space Attachment

Currently all `GumRenderable` instances render in screen space (Gum's native coordinate system, Y-down, origin top-left). Future work: if a `GumRenderable` is attached to an `Entity`, project `AbsoluteX/Y` through `camera.WorldToScreen` to follow the entity (e.g., a health bar above a character). See the `TODO` comment in `GumRenderable.Draw`.
