---
name: grid-movement
description: "Grid Movement in FlatRedBall2. Use when implementing tile-by-tile (grid-snapped) movement where one key press = one tile step, input is locked during the movement animation, and the player cannot move into blocked tiles. Trigger on any dungeon-crawler, Pokémon-style, roguelike, or puzzle game with discrete grid movement. Do NOT trigger for analog/free top-down movement — use the top-down-movement skill for that."
---

# Grid Movement in FlatRedBall2

> **Not `TopDownBehavior`** — `TopDownBehavior` produces continuous analog velocity-based movement. For tile-by-tile grid movement, implement the pattern below directly. Do not load the `top-down-movement` skill for this use case.

Grid movement has three invariants:
1. The entity always sits on a tile boundary (position snapped to multiples of `TileSize`).
2. Input is locked while a move is in progress.
3. A pre-move collision check prevents entering blocked tiles.

## Minimal Implementation

```csharp
private const float TileSize = 16f;
private bool _moving = false;

public override void CustomInitialize()
{
    X = MathF.Round(X / TileSize) * TileSize;  // snap to grid on spawn
    Y = MathF.Round(Y / TileSize) * TileSize;
}

public override async void CustomActivity(FrameTime time)
{
    if (_moving) return;                          // gate: one move at a time

    var (dx, dy) = ReadDirectionalInput();
    if (dx == 0 && dy == 0) return;

    float targetX = X + dx * TileSize;
    float targetY = Y + dy * TileSize;

    if (IsBlocked(targetX, targetY)) return;      // pre-move collision check

    _moving = true;
    await TweenTo(targetX, targetY, 0.12f);       // animate the step
    X = targetX; Y = targetY;                     // snap to exact grid position
    _moving = false;
}
```

## Pre-Move Collision Check

Use `TileShapeCollection.GetTileAtWorld(x, y)` to test the target tile before committing:

```csharp
private bool IsBlocked(float x, float y)
    => _solidTiles.GetTileAtWorld(x, y) != null;
```

`TileShapeCollection` is injected from the screen after spawning via a method on the entity. See the `levels` skill for how to generate it from a TMX layer.

> **Namespace gotcha** — `TileShapeCollection` lives in `FlatRedBall2.Collision`, not `FlatRedBall2.Tiled`. Add `using FlatRedBall2.Collision;` to any file that declares a `TileShapeCollection` field.

## Tween Helper

A lerp tween using `Engine.Time.CurrentScreenTime` (accessible from any entity via `Engine`):

```csharp
private async Task TweenTo(float tx, float ty, TimeSpan duration)
{
    float startX = X, startY = Y;
    TimeSpan startTime = Engine.Time.CurrentScreenTime;
    await Engine.Time.DelayUntil(() =>
    {
        TimeSpan elapsed = Engine.Time.CurrentScreenTime - startTime;
        float t = (float)Math.Clamp(elapsed.TotalSeconds / duration.TotalSeconds, 0.0, 1.0);
        X = startX + (tx - startX) * t;
        Y = startY + (ty - startY) * t;
        return t >= 1f;
    }, CancellationToken.None);
}
```

> **`CancellationToken` scope** — `Token` is a property of `Screen`, not `Entity`. Pass `CancellationToken.None` from an entity. For tweens this short (<200 ms) that is safe; if you need cancellation on screen exit, thread a token in via a constructor parameter.

## Gotchas

- **Always snap on spawn** — entities loaded from a TMX object layer may have sub-pixel offsets. Round `X` and `Y` to the nearest tile in `CustomInitialize`.
- **`_moving` guard is required** — without it, holding a key queues multiple moves before the first frame completes.
- **Diagonal movement**: If 8-way is needed, check that both axis tiles are clear before committing. Diagonal into a corner with one axis blocked should push to the open axis or reject.
