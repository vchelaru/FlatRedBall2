---
name: physics-and-movement
description: "Physics and Movement in FlatRedBall2. Use when working with velocity, acceleration, drag, gravity, kinematic movement, projectiles, rotation-based thrust, or coordinate system (Y+ up). Also covers GameRandom helpers for randomized spawning. Trigger on any physics or movement question."
---

# Physics and Movement in FlatRedBall2

## Coordinate System

**Y+ is UP.** This is the opposite of screen-space pixels.

- A value of `Y = 100` places an entity *above* center.
- Gravity is a **negative** AccelerationY.
- The camera applies a Y-flip when converting world → screen, so everything renders correctly.

## Physics Properties on Entity

```csharp
entity.X           // world position (Y+ up)
entity.Y
entity.VelocityX   // units per second
entity.VelocityY
entity.AccelerationX  // units per second²
entity.AccelerationY
entity.Drag        // fraction of velocity removed per second (0 = no drag)
```

## Gravity Pattern

Set a negative `AccelerationY` to simulate gravity. Spawn the entity with a positive `Y` so it has room to fall:

```csharp
public class Ball : Entity
{
    public override void CustomInitialize()
    {
        var circle = new Circle { Radius = 8f, IsVisible = true };
        Add(circle);

        AccelerationY = -200f;   // gravity pulls downward (Y- direction)
    }
}

// At spawn time:
var ball = _ballFactory.Create();
ball.X = 0f;
ball.Y = Engine.Random.Between(50f, 150f);            // start above the floor
ball.VelocityX = Engine.Random.Between(-150f, 150f);  // random horizontal launch
```

## Kinematic Formula (Second-Order)

Each frame, `PhysicsUpdate` applies:

```
position += velocity * dt + acceleration * (dt² / 2)
velocity += acceleration * dt
velocity -= velocity * drag * dt
```

This is second-order (Verlet-style), so acceleration is smoothly integrated even at low frame rates.

## Drag

`Drag` is a multiplier applied to velocity each frame:

```csharp
entity.Drag = 1f;   // removes 100% of velocity per second (stops fast)
entity.Drag = 0.5f; // removes 50% per second (gentle air resistance)
entity.Drag = 0f;   // no drag (default)
```

Drag does *not* affect acceleration — only velocity. A falling entity with drag will reach terminal velocity when gravity equals drag deceleration.

## Update Order Each Frame

See `engine-overview` for the full 8-step frame loop. The key point: **Physics → Collision → CustomActivity** — game logic sees already-corrected positions.

## Common Patterns

### Horizontal movement with deceleration

```csharp
// In CustomActivity:
VelocityX = input.X * 200f;  // direct velocity set — no drag needed
// or
AccelerationX = input.X * 500f;
Drag = 4f;                    // decelerates when input stops
```

### Jump

```csharp
// AccelerationY = -400f set in CustomInitialize
if (jumpPressed && IsOnGround)
    VelocityY = 350f;   // upward impulse; gravity brings it back down
```

### Rotation-based thrust (top-down ship)

`Rotation.ToVector2()` returns the unit vector the entity is facing. Use it directly for thrust.
Set acceleration in the forward direction each frame and use `Drag` to decelerate naturally when thrust stops:

```csharp
// CustomInitialize:
Drag = 3f;

// CustomActivity:
const float ThrustForce = 400f;
var forward = Rotation.ToVector2();

if (kb.IsKeyDown(Keys.Up))
{
    AccelerationX = forward.X * ThrustForce;
    AccelerationY = forward.Y * ThrustForce;
}
else
{
    AccelerationX = 0f;
    AccelerationY = 0f;
    // Drag continues to bleed off existing velocity
}
```

### Projectile

```csharp
var bullet = _bulletFactory.Create();
bullet.X = X;
bullet.Y = Y;
bullet.VelocityX = facingDirection * 600f;
// No AccelerationY — bullet travels in a straight line
```

## GameRandom — Randomized Spawning

`FlatRedBallService.Random` (type `GameRandom`, a subclass of `System.Random`) provides game-friendly helpers:

```csharp
Engine.Random.Between(-150f, 150f)   // float in range
Engine.Random.Between(50f, 100f)     // float in range
Engine.Random.NextSign()             // returns +1f or -1f
Engine.Random.NextBool()             // true or false
Engine.Random.RadialVector2(50f, 100f) // random direction, length 50–100
Engine.Random.PointInCircle(80f)     // uniformly distributed point inside circle
Engine.Random.NextAngle()            // random Angle (0 to 2π)
Engine.Random.In(list)               // random element from a list
```

In unit tests, create `new GameRandom(seed: 42)` for deterministic results.
