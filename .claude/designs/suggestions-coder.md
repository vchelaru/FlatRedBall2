# ARCHITECTURE.md -- Coder Suggestions

After reviewing ARCHITECTURE.md against FRB1 (`C:\git\FlatRedBall\`) and Gum (`C:\git\Gum\`) from an implementation perspective, here are technical coding concerns, API design issues, and practical gotchas. Each item is tagged:

- **[Critical]** -- Will cause compilation errors, crashes, or fundamental design problems
- **[Important]** -- Will cause significant friction during implementation or usage
- **[Minor]** -- Small improvement or clarification
- **[Nitpick]** -- Style or preference issue

---

## Critical Issues

### 1. Entity.Children Typed as IReadOnlyList<object> -- Type Safety Loss

**[Critical]** The Entity class declares:
```csharp
public IReadOnlyList<object> Children { get; }  // entities or any IAttachable
```

Using `object` instead of `IAttachable`:
- Forces casting on every access: `((IAttachable)Children[i]).AbsoluteX`
- Prevents LINQ queries without casting: `Children.OfType<Sprite>()`
- Causes boxing if value types ever implement IAttachable
- Loses compile-time checks -- you can add anything to the backing list

**Fix**: Use `IReadOnlyList<IAttachable>` instead.

### 2. ICollidable Double-Dispatch Problem

**[Critical]** The proposed `ICollidable` interface:
```csharp
public interface ICollidable
{
    bool CollidesWith(ICollidable other);
    Vector2 GetSeparationVector(ICollidable other);
    void SeparateFrom(ICollidable other);
    void AdjustVelocityFrom(ICollidable other);
}
```

Each shape (AxisAlignedRectangle, Circle, Polygon) AND Entity must implement `CollidesWith(ICollidable other)`. This requires every shape to handle every other shape type internally -- classic N x N double dispatch problem:

```csharp
// AxisAlignedRectangle must handle:
public bool CollidesWith(ICollidable other)
{
    if (other is Circle c) return CollidesWithCircle(c);
    if (other is AxisAlignedRectangle r) return CollidesWithRect(r);
    if (other is Polygon p) return CollidesWithPolygon(p);
    if (other is Entity e) return CollidesWithEntity(e);
    throw new NotSupportedException(); // ← what about custom types?
}
```

Adding a new shape type requires modifying ALL existing shapes. This violates Open/Closed Principle.

**FRB1's approach is better**: `ICollidable` exposes a `ShapeCollection Collision { get; }` property, and centralized static methods handle shape-vs-shape dispatch. New shapes only need to implement their own collision against existing shapes in one place.

**Fix**: Have ICollidable expose its shapes rather than implementing collision logic directly:
```csharp
public interface ICollidable
{
    IReadOnlyList<ICollisionShape> Shapes { get; }
}
```

### 3. Gum Static State Contradicts "No Static State" Goal

**[Critical]** Gum uses heavy static state internally:
- `SystemManagers.Default` -- global singleton
- `ISystemManagers.Default` -- global interface reference
- `GraphicalUiElement.CanvasWidth` / `CanvasHeight` -- static properties
- `FormsUtilities.Cursor` -- static input state
- `ObjectFinder.Self` -- static singleton
- `LoaderManager.Self` -- static singleton

The ARCHITECTURE.md says "No static state except `FlatRedBallService.Default`" but integrating Gum brings a significant amount of static state. This breaks the multi-instance testing story.

**Impact**: You cannot run two `FlatRedBallService` instances simultaneously with different Gum states. Tests that use Gum UI will share global Gum state.

**Suggestion**: Document this limitation explicitly. Consider whether Gum initialization should be optional (lazy) so non-UI tests can avoid touching Gum state.

---

## Important Issues

### 4. CollisionRelationship Takes IEnumerable -- Should Be IReadOnlyList

**[Important]** The `AddCollisionRelationship` method signature:
```csharp
public CollisionRelationship<A, B> AddCollisionRelationship<A, B>(
    IEnumerable<A> listA,
    IEnumerable<B> listB)
```

`IEnumerable<T>` is problematic for collision:
- No `Count` property -- can't pre-allocate or optimize
- No random access -- can't implement spatial partitioning efficiently
- Could be lazy/deferred -- collision needs snapshot semantics
- Multiple enumeration is common in collision (broad phase, then narrow phase)

FRB1 uses `PositionedObjectList<T>` which has indexed access.

**Fix**: Use `IReadOnlyList<T>` or a custom `IEntityList<T>` interface.

### 5. Generic Constraints Too Loose on CollisionRelationship

**[Important]** The collision response methods (`MoveFirstOnCollision`, `BounceOnCollision`, etc.) need to modify position and velocity of the colliding objects. But the generic constraint is only `where A : ICollidable`. `ICollidable` doesn't have position or velocity properties.

To implement `MoveFirstOnCollision()`, the code needs to:
1. Get the separation vector (from ICollidable)
2. Move the first object (needs position access -- not on ICollidable)
3. Adjust velocity (needs velocity access -- not on ICollidable)

**How will this work?** Either:
- ICollidable needs position/velocity (makes it too broad)
- The constraint needs an additional interface (e.g., `where A : ICollidable, IPositionable`)
- Runtime casting with `if (first is Entity e) e.X += ...` (fragile)

### 6. GumBatch Bridging Is Architecturally Complex

**[Important]** Gum has its own complete rendering pipeline (`SystemManagers.Renderer`) with its own sprite batching, draw ordering, and render targets. Making `GumBatch` implement `IRenderBatch` to integrate into FRB2's Z-sorted render list means:
- Gum's internal draw order must be preserved while being "slotted" into FRB2's Z order
- Gum renders everything in its visual tree in one call -- it can't easily be interleaved with FRB2 sprites at different Z values
- Gum manages its own SpriteBatch calls internally

In practice, `GumBatch.Begin/End` would likely just call Gum's full render pass, meaning all Gum elements render at one Z position in the render list, not individually sorted among game sprites. This should be documented as a limitation.

### 7. FrameTime Constructor Not Shown -- Test Sample Won't Compile

**[Important]** The unit test example:
```csharp
var frameTime = new FrameTime(delta: TimeSpan.FromSeconds(1/60.0));
```

The `FrameTime` struct definition shows only properties, no constructor. This code won't compile. Additionally, `SinceScreenStart` and `SinceGameStart` aren't provided.

**Fix**: Add a constructor to the FrameTime definition:
```csharp
public FrameTime(TimeSpan delta, TimeSpan sinceScreenStart = default, TimeSpan sinceGameStart = default);
```

### 8. Entity.CustomDraw Mixes Concerns

**[Important]** Entity has `public virtual void CustomDraw(SpriteBatch spriteBatch, Camera camera)` but Entity does NOT implement `IRenderable`. This creates two rendering paths:
1. IRenderable objects (Sprites, shapes) drawn by the render system
2. Entity.CustomDraw called by... whom? When?

If it's called during the render loop for every entity, entities are now implicitly renderable (defeating the purpose of separating IRenderable). If it's not called by the engine, it's dead code.

**Fix**: Remove CustomDraw from Entity. Entities that need custom rendering should create an IRenderable child and attach it.

### 9. Sprite Missing Source Rectangle / Texture Region

**[Important]** The Sprite class shows `Texture`, `Width`, `Height` but no source rectangle for sprite sheets:
```csharp
// Missing:
public Rectangle? SourceRectangle { get; set; }
// Or texture coordinates:
public float TopTextureCoordinate { get; set; }
public float BottomTextureCoordinate { get; set; }
public float LeftTextureCoordinate { get; set; }
public float RightTextureCoordinate { get; set; }
```

Without source rectangles, you can't render individual frames from a sprite sheet (the most common use case for game sprites). The animation system (AnimationChain) presumably sets these, but manual sprite sheet usage needs this too.

### 10. Sprite Missing Alpha, Flip, and Rotation

**[Important]** The Sprite class is missing common properties:
- **Alpha / Opacity** -- For fade effects
- **FlipHorizontal / FlipVertical** -- For character direction
- **Rotation** -- Sprites need to rotate (they're IAttachable but IAttachable doesn't include rotation)
- **Origin / Pivot** -- For rotation center and positioning
- **SpriteEffects** -- MonoGame's SpriteEffects for flipping

### 11. IAttachable Missing Rotation

**[Important]** The `IAttachable` interface has position (X, Y, Z) and absolute position but NO rotation:
```csharp
public interface IAttachable
{
    Entity Parent { get; set; }
    float X { get; set; }
    float Y { get; set; }
    float Z { get; set; }
    float AbsoluteX { get; }
    float AbsoluteY { get; }
    float AbsoluteZ { get; }
    void Destroy();
}
```

If a Sprite is attached to a rotating Entity, should the sprite rotate too? The interface doesn't support this. FRB1's `IAttachable` has rotation because it extends through `PositionedObject`.

### 12. Factory<T> new() Constraint Prevents DI

**[Important]** The Factory class:
```csharp
public class Factory<T> where T : Entity, new()
```

The `new()` constraint means T must have a parameterless constructor. This prevents:
- Constructor injection of dependencies (Engine, ContentManager, etc.)
- Entities that require initialization parameters
- Factory creating entities with proper screen association

**Workaround needed**: Either use a `Func<T>` factory delegate, or use a post-construction initialization pattern.

### 13. CollisionOccurred Is an Event on a Return Value

**[Important]** The collision example subscribes to an event on a method chain return:
```csharp
AddCollisionRelationship(bulletFactory, enemyFactory)
    .MoveSecondOnCollision()
    .CollisionOccurred += (bullet, enemy) => { ... };
```

If `CollisionOccurred` is declared as `event Action<A, B>`, the `+=` on a returned value won't be stored unless the caller keeps a reference. This is fine if `AddCollisionRelationship` returns a reference that the Screen holds, but the example doesn't show storing the reference.

Also: subscribing to an event in a fluent chain and then discarding the reference means you can never unsubscribe.

### 14. No Entity Registration Mechanism

**[Important]** The update loop says it runs "all entity CustomActivity(frameTime) calls top-down" and applies physics to entities. But Entity has no mechanism to register with the engine's update loop. How does the engine know which entities exist?

Options (none shown):
- Screen maintains a list of active entities
- Factory registers entities with the screen
- Entities self-register via FlatRedBallService.Default
- Users manually call entity.Update() in Screen.CustomActivity

This is fundamental plumbing that needs to be documented.

---

## Minor Issues

### 15. Drag Formula Unspecified

**[Minor]** Entity has `public float Drag { get; set; }  // reduces velocity each frame`. The formula matters:
- Multiplicative: `vel *= (1 - drag * dt)` -- FRB1's approach
- Subtractive: `vel -= drag * dt * sign(vel)` -- different behavior
- Exponential: `vel *= Math.Pow(1 - drag, dt)` -- frame-rate independent

Without specifying, implementers will guess differently.

### 16. Angle.Normalized() Terminology

**[Minor]** The doc says `Normalized()` "clamps to [-pi, pi]". This is actually "wrapping" not "clamping." Clamping would cap the value at the bounds; wrapping maps it to the equivalent angle in range. Use "wraps" instead.

### 17. Camera Zoom Not Explicit

**[Minor]** Camera has TargetWidth/TargetHeight but no Zoom property. To zoom in by 2x, do you halve TargetWidth/TargetHeight? This should be explicit:
```csharp
public float Zoom { get; set; } = 1f;
// EffectiveWidth = TargetWidth / Zoom
```

### 18. Screen Transitions -- What Happens to Active Entities?

**[Minor]** `Screen.MoveToScreen<T>()` transitions to a new screen. But:
- Are all entities on the current screen automatically destroyed?
- What about entities created outside a factory?
- Is there a transition animation mechanism?
- Can data be passed between screens?

### 19. DebugRenderer Font Dependency

**[Minor]** `DebugRenderer.DrawText()` needs a font. How is it loaded? Does DebugRenderer have a built-in font, or must the user provide one? If built-in, it's an embedded resource. If user-provided, where is the font specified?

### 20. ContentManagerService.CreateNull() -- What Does It Return?

**[Minor]** `public static ContentManagerService CreateNull()` -- does this return a null object that returns default(T) for all Load<T> calls? Throws on Load? Returns empty textures? The behavior of the null implementation matters for testing.

### 21. Entity Has No Scale but Sprite Does

**[Minor]** "No Scale property" on Entity, but Sprite has Width/Height. FRB1's PositionedObject also has no scale, but its Sprite has ScaleX/ScaleY (half-dimensions). The doc's Sprite uses Width/Height (full dimensions). This is a naming change from FRB1 where Width = ScaleX * 2.

### 22. Render List Insert Performance

**[Minor]** "Objects are inserted in sorted position on add (binary search, O(log N) find + O(N) shift)." For a `List<T>`, insertion is O(N) due to array shifting. With thousands of renderables being added/removed per frame (particle systems), this could be a bottleneck. Consider a linked list or skip list for the render list.

### 23. LayerManager.Get(string name) Returns Null?

**[Minor]** What happens if `LayerManager.Get("nonexistent")` is called? Does it return null? Throw? The error handling behavior should be specified. Returning null risks NullReferenceException; throwing gives a clear error.

### 24. MonoGame.Extended Tiled -- Is This Correct?

**[Minor]** The doc says Tiled integration is "Via MonoGame.Extended.Tiled." But FRB1 has its own Tiled loading code (TMX parsing, TileShapeCollection, etc.). MonoGame.Extended.Tiled is a completely different library with different APIs. If this is intentional, the migration from FRB1's Tiled API will be significant.

### 25. AudioManager.PlaySoundEffect Returns Nothing

**[Minor]** `public void PlaySoundEffect(string name, float volume = 1f)` returns void. Common needs:
- Stopping a specific playing sound effect
- Checking if a sound is still playing
- Adjusting volume/pitch of a playing sound

Returning a `SoundEffectInstance` or handle would enable these.

---

## Nitpicks

### 26. Angle Operator * -- What Does It Multiply?

**[Nitpick]** The Angle struct shows `// operators: +, -, *, ==, !=`. What does `Angle * Angle` mean? That's not mathematically meaningful. It should be `Angle * float` (scaling) and `float * Angle`. Clarify which overloads exist.

### 27. IRenderBatch.End -- Does It Need SpriteBatch?

**[Nitpick]** `IRenderBatch.End(SpriteBatch spriteBatch)` -- MonoGame's SpriteBatch.End() takes no parameters. Why does the batch's End method need the SpriteBatch reference? It could just call `spriteBatch.End()`, but it already has the reference from Begin. Consider removing the parameter.

### 28. WorldSpaceBatch / ScreenSpaceBatch -- Singleton or Instance?

**[Nitpick]** The built-in batches are listed as type names:
```csharp
WorldSpaceBatch   // SpriteBatch.Begin with camera transform matrix
ScreenSpaceBatch  // SpriteBatch.Begin with identity matrix
GumBatch          // delegates to Gum's render pass
```

Are these singletons? Static instances? Created per-layer? Per-camera? The lifecycle of batch objects matters for cleanup and multi-camera rendering.
