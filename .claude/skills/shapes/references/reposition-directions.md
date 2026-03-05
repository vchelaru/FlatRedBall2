# RepositionDirections Reference (AxisAlignedRectangle only)

`RepositionDirections` controls which sides of a rectangle act as solid collision surfaces. Collisions that would push an object in a suppressed direction are ignored entirely.

Default is `RepositionDirections.All` (all four sides solid).

```csharp
// One-way platform: only the top surface is solid.
platform.Rectangle.RepositionDirections = RepositionDirections.Up;

// Remove interior sides from adjacent tiles to prevent "snagging" at seams.
leftTile.Rectangle.RepositionDirections  = RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Left;
rightTile.Rectangle.RepositionDirections = RepositionDirections.Up | RepositionDirections.Down | RepositionDirections.Right;
```

Values can be combined with `|` and removed with `&= ~`:
```csharp
brick.Rectangle.RepositionDirections &= ~RepositionDirections.Right; // remove right side
brick.Rectangle.RepositionDirections |= RepositionDirections.Right;  // restore right side
```

> **Naming conflict:** `Gum.Forms.Controls` also defines a `RepositionDirections` type (for UI layout). If you import both namespaces, add a using alias:
> ```csharp
> using RepositionDirections = FlatRedBall2.Collision.RepositionDirections;
> ```

## Dynamic Tile Grids

When a tile is destroyed, its neighbors' RepositionDirections must be updated to expose the sides that now face open space. Track the grid in a `Dictionary<(int col, int row), AxisAlignedRectangle>` and restore directions on destroy — see `GameScreen.RestoreNeighborDirections` in the sample for a reference implementation.
