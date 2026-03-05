---
name: shapes
description: "Working with Shapes in FlatRedBall2. Use when working with AxisAlignedRectangle, Circle, Polygon, shape creation, visibility, color, IsFilled, OutlineThickness, or visual properties of shapes. Also covers RepositionDirections for one-way platforms and tile grids. Trigger on any shape-related question."
---

# Working with Shapes in FlatRedBall2

FlatRedBall2 has three built-in shape types: `AxisAlignedRectangle`, `Circle`, and `Polygon`. All shapes implement both `IRenderable` and `ICollidable`, so they handle both drawing and collision.

## Shape Types

| Type | Key Properties | Notes |
|------|---------------|-------|
| `AxisAlignedRectangle` | `Width`, `Height` | Cannot rotate |
| `Circle` | `Radius` | |
| `Polygon` | `Points`, `Rotation` | Use factory methods to create |

## Step 1: Create a Shape

```csharp
var rect = new AxisAlignedRectangle { X = 0, Y = 0, Width = 64, Height = 64 };
var circle = new Circle { X = 100, Y = 0, Radius = 32 };

// Polygon factory methods:
var poly = Polygon.CreateRectangle(64, 64);         // rotatable rectangle
var custom = Polygon.FromPoints(new[] {
    new Vector2(0, 0), new Vector2(50, 0), new Vector2(25, 40),
});
```

## Step 2: Make the Shape Visible

Shapes default to `Visible = false`. **Always set `Visible = true`** or the shape won't render.

## Step 3: Add to the Render Pipeline

**Option A — Directly on the Screen**: Add to `RenderList` in `CustomInitialize`:
```csharp
RenderList.Add(rect);
```

**Option B — Attached to an Entity**: `entity.AddChild(shape)` auto-adds to `RenderList` (as long as `Engine` is set).

> **Note:** `AddChild` only auto-registers if the entity already has an `Engine` reference (via `Factory` or `Screen.Register`).

## Visual Properties

```csharp
var rect = new AxisAlignedRectangle
{
    Color = new Color(220, 60, 60, 200),   // RGBA
    IsFilled = false,                       // true = solid fill, false = outline
    OutlineThickness = 3f,                  // line width in pixels
    Visible = true,
};
```

> **Polygon note:** `Polygon` always draws as an outline (no fill triangulation yet). `IsFilled = true` doubles `OutlineThickness` to approximate a filled look.

## Cleanup

```csharp
rect.Destroy();   // removes from parent entity and clears references
```

For shapes added directly to `RenderList` (not via `AddChild`), also call `RenderList.Remove(rect)`.

## Common Pitfalls

- **Shape is invisible** — forgot `Visible = true`. Default is `false`.
- **Shape is not drawn** — forgot `RenderList.Add(shape)` or `AddChild` before entity was registered.
- **Shape position looks wrong** — Y+ is up (see `physics-and-movement`).
- **Polygon not rotating** — use `Polygon`, not `AxisAlignedRectangle`.

## RepositionDirections (AxisAlignedRectangle only)

Controls which sides of a rectangle act as solid collision surfaces. Use for one-way platforms (`RepositionDirections.Up` = only top is solid) and removing interior sides from adjacent tiles to prevent seam snagging.

Default is `RepositionDirections.All`. Values combine with `|` and `&= ~`.

For detailed usage and dynamic tile grids, see:
- `references/reposition-directions.md` — Full examples, dynamic grids, Gum naming conflict workaround
