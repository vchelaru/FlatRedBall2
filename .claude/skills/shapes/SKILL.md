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
var rect = new AxisAlignedRectangle
{
    X = 0, Y = 0,
    Width = 64, Height = 64,
};

var circle = new Circle
{
    X = 100, Y = 0,
    Radius = 32,
};

// Polygon factory methods:
var poly = Polygon.CreateRectangle(64, 64);         // rotatable rectangle
var custom = Polygon.FromPoints(new[] {             // arbitrary shape
    new Vector2(0, 0),
    new Vector2(50, 0),
    new Vector2(25, 40),
});
```

## Step 2: Make the Shape Visible

Shapes default to `Visible = false`. **Always set `Visible = true`** or the shape won't render.

```csharp
rect.Visible = true;
```

## Step 3: Add to the Render Pipeline

Shapes must be added to `Screen.RenderList` to be drawn each frame.

### Option A — Directly on the Screen

Add the shape to `RenderList` inside `CustomInitialize`:

```csharp
public class GameScreen : Screen
{
    public override void CustomInitialize()
    {
        var rect = new AxisAlignedRectangle
        {
            X = 0, Y = 0,
            Width = 120, Height = 80,
            Visible = true,
        };

        RenderList.Add(rect);   // <-- required
    }
}
```

### Option B — Attached to an Entity

Calling `entity.AddChild(shape)` automatically adds the shape to `RenderList` (as long as the entity is registered with a screen). The shape's position is relative to the entity.

```csharp
public class Player : Entity
{
    public Circle Hitbox { get; } = new Circle { Radius = 24, Visible = true };

    public override void CustomInitialize()
    {
        AddChild(Hitbox);  // registered to RenderList automatically
    }
}
```

> **Note:** `AddChild` only auto-registers if the entity already has an `Engine` reference (i.e., it was registered via `Screen.Register` or created via `Factory` before `AddChild` is called). If you call `AddChild` in an entity's constructor before registration, add the shape to `RenderList` manually.

## Visual Properties

All shape types share these visual properties:

```csharp
var rect = new AxisAlignedRectangle
{
    // Color: RGBA, 0-255 each. Default is semi-transparent white.
    Color = new Color(220, 60, 60, 200),   // red, mostly opaque

    // IsFilled: true = solid fill, false = outline only
    IsFilled = false,

    // OutlineThickness: line width in pixels (used when IsFilled = false,
    // or for Polygon where fill = thicker outline)
    OutlineThickness = 3f,

    Visible = true,
};
```

> **Polygon note:** `Polygon` always draws as an outline (no fill triangulation yet). Setting `IsFilled = true` on a Polygon doubles the `OutlineThickness` to approximate a filled look.

## Cleanup

Call `Destroy()` when you're done with a shape. This removes it from its parent entity and clears references.

```csharp
rect.Destroy();
```

For shapes added directly to `RenderList` (not via `AddChild`), also remove them manually:

```csharp
RenderList.Remove(rect);
rect.Destroy();
```

## RepositionDirections (AxisAlignedRectangle only)

`RepositionDirections` controls which sides of a rectangle act as solid collision surfaces. Collisions that would push an object in a suppressed direction are ignored entirely.

Default is `RepositionDirections.All` (all four sides solid).

```csharp
// One-way platform: only the top surface is solid.
platform.Rectangle.RepositionDirections = RepositionDirections.Up;

// Remove interior sides from adjacent tiles to prevent "snagging" at seams.
// (Ball grazing the edge between two side-by-side bricks gets deflected into the seam
//  instead of bouncing off the top. Removing interior sides fixes this.)
leftTile.Rectangle.RepositionDirections  = RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Left;
rightTile.Rectangle.RepositionDirections = RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Right;
```

Values can be combined with `|` and removed with `&= ~`:
```csharp
brick.Rectangle.RepositionDirections &= ~RepositionDirections.Right; // remove right side
brick.Rectangle.RepositionDirections |= RepositionDirections.Right;  // restore right side
```

> **Naming conflict:** `Gum.Forms.Controls` also defines a `RepositionDirections` type (for UI layout). If you import both namespaces, add a using alias to resolve the ambiguity:
> ```csharp
> using RepositionDirections = FlatRedBall2.Collision.RepositionDirections;
> ```

**Dynamic tile grids:** When a tile is destroyed, its neighbors' RepositionDirections must be updated to expose the sides that now face open space. Track the grid in a `Dictionary<(int col, int row), AxisAlignedRectangle>` and restore directions on destroy — see `GameScreen.RestoreNeighborDirections` in the sample for a reference implementation.

## Common Pitfalls

- **Shape is invisible** — forgot `Visible = true`. Default is `false`.
- **Shape is not drawn** — forgot `RenderList.Add(shape)` (or `AddChild` before entity was registered).
- **Shape position looks wrong** — remember Y+ is **up** in world space. A shape at `Y = 100` is *above* center.
- **Polygon not rotating** — use `Polygon`, not `AxisAlignedRectangle`. Only `Polygon` has a `Rotation` property.
