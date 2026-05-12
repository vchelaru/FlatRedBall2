# SolidSides Reference (AARect only)

`SolidSides` controls which sides of a rectangle act as solid collision surfaces. Collisions that would push an object in a suppressed direction are ignored entirely.

Default is `SolidSides.All` (all four sides solid).

```csharp
// One-way platform: only the top surface is solid.
platform.Rectangle.SolidSides = SolidSides.Up;

// Remove interior sides from adjacent tiles to prevent "snagging" at seams.
leftTile.Rectangle.SolidSides  = SolidSides.Up | SolidSides.Down | SolidSides.Left;
rightTile.Rectangle.SolidSides = SolidSides.Up | SolidSides.Down | SolidSides.Right;
```

Values can be combined with `|` and removed with `&= ~`:
```csharp
brick.Rectangle.SolidSides &= ~SolidSides.Right; // remove right side
brick.Rectangle.SolidSides |= SolidSides.Right;  // restore right side
```

> **Naming conflict:** `Gum.Forms.Controls` also defines a `SolidSides` type (for UI layout). If you import both namespaces, add a using alias:
> ```csharp
> using SolidSides = FlatRedBall2.Collision.SolidSides;
> ```

## Dynamic Tile Grids

When a tile is destroyed, its neighbors' SolidSides must be updated to expose the sides that now face open space. Track the grid in a `Dictionary<(int col, int row), AARect>` and restore directions on destroy — see `GameScreen.RestoreNeighborDirections` in the sample for a reference implementation.
