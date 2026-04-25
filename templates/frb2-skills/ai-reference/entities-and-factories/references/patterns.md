# Entity Patterns Reference

## Configuring Entities After Create()

`Create()` returns the entity instance. Set position and shape dimensions after creation:

```csharp
private void SpawnWall(float x, float y, float w, float h)
{
    var wall = _wallFactory.Create();
    wall.X = x; wall.Y = y;
    wall.Rectangle.Width = w;
    wall.Rectangle.Height = h;
}
```

## Destroy and Spawn (Death Effects)

When an entity should trigger a visual effect on destruction, destroy it immediately and spawn a separate effect entity at the same position. Do not try to play an effect on the dying entity — once destroyed it is removed from the game.

```csharp
AddCollisionRelationship<Bullet, Enemy>(_bullets, _enemies)
    .CollisionOccurred += (bullet, enemy) =>
    {
        var explosion = Engine.GetFactory<Explosion>().Create();
        explosion.X = enemy.X;
        explosion.Y = enemy.Y;
        enemy.Destroy();
        bullet.Destroy();
    };
```

The effect entity manages its own lifetime — see the `timing` skill for the self-destruct pattern.

## Particle Effects

> **Future:** A dedicated particle tool is planned. For now, spawn short-lived entities using the entity lifetime pattern from the `timing` skill.

Spawn a burst from any entity:

```csharp
var factory = Engine.GetFactory<Particle>();
for (int i = 0; i < 12; i++)
{
    var p = factory.Create();
    p.X = X; p.Y = Y;
    p.Velocity = Engine.Random.RadialVector2(60f, 180f);
    p.Launch(lifetimeSeconds: Engine.Random.Between(0.3f, 0.8f));
}
```

Visual variety comes from randomizing velocity, color, size, and lifetime.
