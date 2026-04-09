---
name: isometric-rendering
description: "Isometric and trimetric rendering patterns in FlatRedBall2. Use when implementing world-to-screen projection, isometric tile placement, depth sorting, isometric cursor picking, or combining top-down gameplay logic with iso visuals."
---

# Isometric Rendering

Treat isometric or trimetric view as a rendering layer on top of FRB world logic.

## FRB Coordinate Rule

Keep canonical gameplay coordinates in world space (Y+ up), then project for visuals.

- Logic systems (collision, pathing, combat ranges) should stay in world/grid coordinates.
- Do not run collision or pathfinding in projected draw coordinates.

Camera rendering already flips world Y internally for screen-space drawing, so projection helpers must be consistent with that convention.

## Projection Formula

Define one static helper — never duplicated across entity code, UI code, or targeting code. Divergent formulas create targeting drift.

The two tunable constants (`XScale`, `YScale`) are the only numbers that change between true isometric (2:1) and Fallout-style trimetric:

```csharp
// ProjectionHelper.cs — one shared helper for the whole project
public static class ProjectionHelper
{
	// Tune these for your art:
	//   true isometric (2:1 tile ratio) = (1f, 0.5f)
	//   Fallout-style trimetric ≈ (1f, 0.4f)
	public const float XScale = 1f;
	public const float YScale = 0.5f;

	/// <summary>World (X, Y) → sprite draw offset relative to a flat origin.</summary>
	public static Vector2 Project(float worldX, float worldY)
		=> new Vector2(
			(worldX - worldY) * XScale,   // horizontal screen shift
			(worldX + worldY) * YScale);  // vertical shift (camera already flips Y)

	/// <summary>Screen position → approximate flat world position (cursor pick).</summary>
	public static Vector2 Unproject(Vector2 screenPos)
	{
		// Solve: screenX = (wx - wy) * XScale, screenY = (wx + wy) * YScale
		float wx = (screenPos.X / XScale + screenPos.Y / YScale) * 0.5f;
		float wy = (screenPos.Y / YScale - screenPos.X / XScale) * 0.5f;
		return new Vector2(wx, wy);
	}
}
```

**Apply projection to the Sprite offset, not to entity position.** Entity (X, Y) stays in world space for physics and collision; only the visual varies:

```csharp
// In entity CustomActivity — moves the sprite visually without disturbing physics
public override void CustomActivity(FrameTime time)
{
	var offset = ProjectionHelper.Project(X, Y);
	_sprite.X = offset.X;
	_sprite.Y = offset.Y;
	// X/Y unchanged — collision and pathfinding use world coordinates
}
```

If the sprite is attached as a child via `entity.Add(_sprite)`, its position is relative to the entity origin. Apply only the delta:

```csharp
var offset = ProjectionHelper.Project(X, Y);
_sprite.X = offset.X - X;  // relative to entity origin
_sprite.Y = offset.Y - Y;
```

## Cursor Picking

`cursor.WorldPosition` is in flat world space. Pass it through `Unproject` before comparing to entity world positions or grid coordinates:

```csharp
Vector2 flatPos = ProjectionHelper.Unproject(Engine.Input.Cursor.WorldPosition);
// flatPos is now in the same space as entity.X / entity.Y
```

## Depth Sorting

Stable draw-order key to avoid flicker. The key formula must match the projection formula exactly — mismatches cause entities to sort by a different order than they appear:

```csharp
// Lower projected Y = further back on screen → drawn behind
// Negate: higher world Y = higher on screen in FRB's Y+ up space = drawn on top
float primaryKey = -(X + Y) * ProjectionHelper.YScale;
float depthZ     = primaryKey - _spawnId * 0.0001f;  // stable tie-breaker

_sprite.Z = depthZ;
```

Set `_spawnId` to a monotonically increasing counter at spawn. Without a tie-breaker, two entities at the same projected row will flicker when they share the same Z.

If two entities can share identical depth keys, always apply a deterministic tie-breaker.

## FRB Integration Points

- Use the `camera` skill for camera bounds and follow behavior. Camera clamping must be in world space, not projected space.
- Use `cursor.WorldPosition` (input-system skill) then pass through `Unproject` before comparing to entity coordinates.
- Use `tile-node-network` for A* on world/grid data, not projected data.
- Use `top-down-movement` for movement intent and `DirectionFacing`.

## Common Pitfalls

- **Mixing projected coordinates into collision checks** — collision runs on world (X, Y), not projected offsets.
- **Different projection formula in targeting vs. rendering** — one `ProjectionHelper`, used everywhere.
- **Depth key formula differs from projection formula** — if you change `YScale`, update the depth key formula too.
- **Treating sprite art footprint as collision footprint** — the sprite may be taller than the entity due to perspective art.
- **Camera clamp in projected space** — clamp `Camera.X/Y` to world bounds, then let projection handle display.
- **Cursor pick one tile off near edges** — verify all systems use the same tile origin convention (cell center vs. cell corner).
