---
name: gum-integration
description: "Gum Integration in FlatRedBall2. Use when working with UI, HUD, menus, buttons, labels, text display, StackPanel, Panel, layout, Gum Forms controls, AddGum, screen-space vs world-space UI, or any Gum-related question. Also trigger when user asks about displaying text on screen."
---

# Gum Integration in FlatRedBall2

Gum is FlatRedBall2's UI system, backed by the `Gum.MonoGame` NuGet package. It is **automatically initialized** by `FlatRedBallService.Initialize` — no setup required.

## Two Levels of Gum API

**FrameworkElement controls (high-level, preferred)**: `Button`, `Label`, `TextBox`, `CheckBox`, `StackPanel`, `Panel`, etc. from `Gum.Forms.Controls`. These have built-in functionality — click events, hover, focus, keyboard navigation, layout.

**GraphicalUiElement visuals (low-level)**: The raw visual tree. Use only when Forms controls do not expose what you need — for example, `TextRuntime` for a custom `FontSize` that `Label` does not surface.

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

## AddGum / RemoveGum

`AddGum` registers the element for rendering **and** per-frame input updates (cursor, clicks, hover, keyboard). All Forms controls work out of the box — no additional input wiring needed.

Always use `AddGum` instead of adding directly to `RenderList`:

```csharp
AddGum(button);          // FrameworkElement
AddGum(textRuntime);     // GraphicalUiElement (low-level visual)
RemoveGum(button);
RemoveGum(textRuntime);
```

**Do not call `button.AddToRoot()`** — this bypasses the FRB2 render-order system.

## Displaying Text (HUD, Score, Labels)

**Option A — `Label` (preferred)**:

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

**Option B — `TextRuntime` (low-level)**: Use only when you need `FontSize`, `FontScale`, etc. not exposed by `Label`.

```csharp
using MonoGameGum.GueDeriving;   // TextRuntime lives here, NOT Gum.Wireframe

var scoreText = new TextRuntime { Text = "0", FontSize = 48 };
AddGum(scoreText);
```

## Render Ordering (Layer / Z)

| Setting | Effect |
|---|---|
| `Layer = null` (default) | Drawn last — on top of all world objects. Right for HUDs and menus. |
| `z: 50f` parameter | Control ordering among multiple Gum elements or between Gum and sprites |

```csharp
AddGum(bgPanel, z: 0f);
AddGum(hudPanel, z: 100f);
```

## Screen-Space vs World-Space

**Screen-space (default)**: `Screen.AddGum` places elements in Gum's native coordinate system (pixels, Y-down, origin top-left). Use for HUDs, menus.

**World-space**: `Entity.AddGum` places a Gum element at the entity's world position. It follows the entity and shifts when the camera pans. The visual is automatically removed when the entity is destroyed.

## Gotchas

- **Namespace**: `TextRuntime` is in `MonoGameGum.GueDeriving`. Forms controls are in `Gum.Forms.Controls`. `Anchor`/`Dock` enums are in `Gum.Wireframe`.
- **Gum coordinates are screen pixels, Y-down** — opposite of the game world (Y-up, centered). Use `Anchor`/`Dock` to avoid hard-coding pixel positions.
- **Initialize order**: Do not create Gum elements before `FlatRedBallService.Initialize`.
- **`AddToRoot()` is NOT the FRB2 pattern**. Use `AddGum` instead.
- **No persistence across screen transitions** — Gum elements are fully cleaned up. Add them fresh in each screen's `CustomInitialize`.
- **World-space Gum**: Do not manually set `Visual.X/Y` on an entity-attached Gum element — it will be overwritten each frame.

## Pattern: Transient World-Space Text (e.g., Floating Score)

Create a short-lived entity that owns a Gum visual at its world position:

```csharp
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

// Spawn: var floater = Engine.GetFactory<ScoreFloater>().Create();
```

Physics moves the entity; the Gum visual follows automatically.

## Layout Essentials

### Anchor and Dock

`Anchor` and `Dock` (in `Gum.Wireframe`) are the primary layout tools:

```csharp
label.Anchor(Anchor.BottomRight);   // pin to corner; X/Y offset inward from there
panel.Dock(Dock.Fill);              // fill parent entirely
panel.Dock(Dock.SizeToChildren);    // shrink-wrap content (default for Panel)
```

### StackPanel

Stacks children vertically (default) or horizontally with optional spacing:

```csharp
var menu = new StackPanel();
menu.Spacing = 8;
menu.Anchor(Anchor.Center);

menu.AddChild(new Button { Text = "Start" });
menu.AddChild(new Button { Text = "Quit" });
AddGum(menu);
```

For horizontal: `new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 }`.

## Reference Files

For detailed positioning/size units (XUnits, YUnits, WidthUnits, HeightUnits) and Panel documentation, see:
- `references/layout.md` — Full unit tables and additional layout patterns
