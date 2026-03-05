# Gum Layout Reference

## Positioning Units (XUnits / YUnits)

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

## Size Units (WidthUnits / HeightUnits)

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

## Anchor and Dock

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

## StackPanel — Automatic Stacking

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

## Panel — Absolute Positioning Container

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
