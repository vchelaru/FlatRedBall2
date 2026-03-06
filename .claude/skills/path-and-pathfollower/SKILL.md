# Path and PathFollower in FlatRedBall2

Use when working with `Path`, `PathFollower`, patrol routes, scripted movement, arc geometry, or trajectory rendering.

## Key Types

- `FlatRedBall2.Math.Path` — defines the path geometry and renders it
- `FlatRedBall2.Movement.PathFollower` — moves an entity along a Path

## Building a Path

```csharp
var path = new Path()
    .MoveTo(-200, 0)         // pen-up move; sets starting point
    .LineTo(200, 0)          // straight segment
    .ArcTo(200, -100, -MathF.PI / 2)  // arc to endpoint, sweeping angle
    .LineBy(0, -50);         // relative line

path.IsLooped = true;        // closes back to start; affects TotalLength
```

All builder methods return `this` for chaining. `By` variants are relative; `To` variants are absolute.

## Arc Angle Convention (Gotcha)

`ArcTo(endX, endY, signedAngleRadians)`:
- Positive = CCW arc, Negative = CW arc (Y+ up world space)
- For a **CCW arc going left-to-right**, the path **bows downward** — the center is above the chord, and the minor arc sweeps below it. Negate the angle to bow upward.
- A full circle requires two `ArcTo(π)` calls — a single arc from a point to itself has zero chord length and degenerates to nothing.

## Querying Position and Tangent

```csharp
Vector2 pos     = path.PointAtLength(distance);   // by world-unit distance
Vector2 pos     = path.PointAtRatio(0.5f);        // by normalized ratio [0, 1]
Vector2 tangent = path.TangentAtLength(distance); // unit direction of travel
float   total   = path.TotalLength;               // includes closing segment when IsLooped
```

## Rendering

```csharp
path.Color         = Color.Cyan;
path.LineThickness = 3f;
path.Visible       = true;
screen.Add(path);  // registers for rendering like any other IRenderable
```

Rendering and following are independent — add to screen only when you want it visible.

## PathFollower

```csharp
var follower = new PathFollower(path)
{
    Speed        = 200f,   // world units per second
    Loops        = true,   // wraps at end (independent of Path.IsLooped)
    FaceDirection = true,  // sets entity.Rotation to face direction of travel
};

follower.WaypointReached += segmentIndex => { /* crossed into segment N */ };
follower.PathCompleted   += () => { /* reached end of open path */ };
```

Call in `CustomActivity`:
```csharp
_follower.Activity(this, time.DeltaSeconds);
```

`PathFollower` only sets `entity.X`, `entity.Y`, and optionally `entity.Rotation`. It does not affect velocity or physics. Call `follower.Reset()` to restart from the beginning.

## FaceDirection Rotation Convention

`FaceDirection = true` sets `entity.Rotation = Angle.FromRadians(atan2(tangent.X, tangent.Y))`, which maps the movement tangent to FlatRedBall2's angle convention (0 = facing up, positive = CW).
