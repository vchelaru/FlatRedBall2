---
name: shapes
description: "Working with Shapes in FlatRedBall2. Use when working with AxisAlignedRectangle, Circle, Polygon, TileShapeCollection, shape creation, visibility, color, IsFilled, OutlineThickness, or visual properties of shapes. Also covers RepositionDirections for one-way platforms and tile grids. Trigger on any shape-related question."
---

# Working with Shapes in FlatRedBall2

All shape types and `TileShapeCollection` are in the `FlatRedBall2.Collision` namespace — add `using FlatRedBall2.Collision;` in any file that uses them.

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

**Option A — Directly on the Screen**: Call `Add` in `CustomInitialize`:
```csharp
Add(rect);
```

**Option B — Attached to an Entity**: `entity.Add(shape)` auto-adds to the render pipeline (as long as `Engine` is set).

> **Note:** `entity.Add(child)` only auto-registers if the entity already has an `Engine` reference (via `Factory` or `Screen.Register`).

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

> **Polygon fill:** `IsFilled = true` ear-clip triangulates and fills the interior (works for concave polygons). `IsFilled = false` renders outline only. Set `OutlineThickness = 0` to suppress the border when filled.

## Cleanup

```csharp
rect.Destroy();   // removes from parent entity and clears references
```

For shapes added directly to the screen (not via `entity.Add`), also call `Remove(rect)`.

## Common Pitfalls

- **Shape is invisible** — forgot `Visible = true`. Default is `false`.
- **Shape is not drawn** — forgot `Add(shape)` on screen, or `entity.Add(shape)` before entity was registered.
- **Shape position looks wrong** — Y+ is up (see `physics-and-movement`).
- **Polygon not rotating** — use `Polygon`, not `AxisAlignedRectangle`.

## TileShapeCollection

`TileShapeCollection` is a grid-based static collision structure for tile maps. Set `X`, `Y`, and `GridSize` before adding tiles — positions are computed at insertion time and not updated if those properties change later.

```csharp
var tiles = new TileShapeCollection { X = 0f, Y = 0f, GridSize = 16f };

// X, Y are the world position of the bottom-left corner of cell (0, 0)
tiles.AddTileAtCell(int col, int row);      // by grid index
tiles.AddTileAtWorld(float x, float y);    // snapped to nearest cell

tiles.RemoveTileAtCell(col, row);
tiles.GetTileAtCell(col, row);             // returns AxisAlignedRectangle? for inspection

tiles.Visible = true;                      // debug visualization
```

`RepositionDirections` are maintained automatically on every add/remove — interior shared edges between adjacent tiles are cleared to prevent seam snagging. No manual refresh needed.

Integrates with `AddCollisionRelationship` — see `collision-relationships` skill.

## RepositionDirections (AxisAlignedRectangle only)

Controls which sides of a rectangle act as solid collision surfaces. Use for one-way platforms (`RepositionDirections.Up` = only top is solid) and removing interior sides from adjacent tiles to prevent seam snagging.

Default is `RepositionDirections.All`. Values combine with `|` and `&= ~`.

For detailed usage and dynamic tile grids, see:
- `references/reposition-directions.md` — Full examples, dynamic grids, Gum naming conflict workaround
