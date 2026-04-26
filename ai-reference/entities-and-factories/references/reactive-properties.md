# Reactive Properties on Entities

The rule: **never write a property on an Entity whose only effect happens inside `CustomInitialize`.** Such a property looks configurable in IntelliSense but silently does nothing when assigned after `Create()` returns — which is when callers naturally configure things.

Two cases cover what to do instead.

## Case A — Pure forwarding: don't write the property, expose the child

If the property would just read or write a child shape's already-reactive property (`Color`, `Radius`, `Width`, etc.), delete it and expose the child:

```csharp
// Wrong — FillColor does nothing post-init
public class Pop : Entity
{
    public Color FillColor { get; set; }
    Circle _circle = null!;
    public override void CustomInitialize()
    {
        _circle = new Circle { Radius = 20, IsVisible = true, Color = FillColor };
        Add(_circle);
    }
}
```

Caller does `pop.FillColor = Red` after `Create()` — too late, the Circle was built with the default. Silent failure.

```csharp
// Right — expose the Circle directly
public class Pop : Entity
{
    public Circle Circle { get; private set; } = null!;
    public override void CustomInitialize()
    {
        Circle = new Circle { Radius = 20, IsVisible = true };
        Add(Circle);
    }
}
```

Caller writes `pop.Circle.Color = Red`. Works at any time — `Circle.Color` is read every frame by the renderer.

The wrapper bought nothing. It only manufactured a footgun.

## Case B — Derived state: write a reactive setter

If the property encodes a value that drives *multiple* visual properties or doesn't have a 1:1 child correspondence, it earns a real property — but the setter must apply changes immediately:

```csharp
public class Asteroid : Entity
{
    Circle _shape = null!;
    AsteroidSize _size = AsteroidSize.Large;

    public AsteroidSize Size
    {
        get => _size;
        set { _size = value; _shape.Radius = value == AsteroidSize.Small ? 10f : 30f; }
    }

    public override void CustomInitialize()
    {
        _shape = new Circle { Radius = 30f, IsVisible = true };
        Add(_shape);
    }
}
```

Timing doesn't matter — `asteroid.Size = Small` resizes the shape immediately, whether called before or after the first frame.

## Why FRB2 differs from Unity / Godot

In Unity, "reactive forwarding" can be expensive — re-instantiating child GameObjects, rebinding renderers, tearing down subscriptions. The ecosystem tolerates init-only public fields because the alternative has real cost. FRB2's renderer reads shape properties every frame; there is no rebuild cost. Init-only properties have no engineering justification here. They only manufacture footguns.

## Test before adding any property to an Entity

1. Does it map 1:1 to a child shape's existing reactive property? → Delete it. Expose the child.
2. Does it encode derived state (one input → multiple outputs, or a value with no direct child)? → Make the setter reactive. Never read it from `CustomInitialize` and store the result without also re-applying it on assignment.

If neither applies, the property probably shouldn't exist.
