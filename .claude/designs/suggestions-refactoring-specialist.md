# ARCHITECTURE.md -- Refactoring Specialist Suggestions

After reviewing ARCHITECTURE.md against FRB1 (`C:\git\FlatRedBall\`) and Gum (`C:\git\Gum\`), here are structural design concerns, interface segregation issues, dependency patterns, and architectural improvement suggestions. Each item is tagged:

- **[High]** -- Fundamental design concern that will cause significant problems
- **[Medium]** -- Design concern that will cause friction or confusion
- **[Low]** -- Minor improvement or polish item

---

## High Priority

### 1. Entity Is Still a God-Object

**[High]** The proposed `Entity` class combines too many responsibilities:
- Spatial transform (position, rotation)
- Physics (velocity, acceleration, drag)
- Scene graph (parent/child hierarchy)
- Collision (ICollidable implementation)
- Rendering hooks (CustomDraw)
- Game logic hooks (CustomInitialize, CustomActivity, CustomDestroy)
- Visibility control

This is the same fundamental problem as FRB1's `PositionedObject` -- it violates the Single Responsibility Principle. The philosophy section says "No single heavy base class" and "Lightweight entities" but the proposed Entity still aggregates 6+ concerns.

**Suggestion**: Consider a composition-based approach where Entity is a thin container:
- `Transform` component (position, rotation, parent/child)
- `PhysicsBody` component (velocity, acceleration, drag)
- Collision shapes are already separate (ICollidable via aggregation)
- Rendering is already separate (IRenderable on Sprite)

Even without full ECS, breaking Transform and Physics into composable structs/components would make Entity significantly lighter and more testable.

### 2. Entity Forces ICollidable on Everything

**[High]** The doc states "Entity implements `ICollidable` by aggregating all attached collision shapes." This means every Entity is collidable, whether or not it has collision shapes. In FRB1, `ICollidable` is opt-in -- only entities that need collision implement it (via Glue code generation).

Making all entities collidable:
- Adds overhead for entities that don't need collision (checking empty shape lists)
- Muddies the type system (a decorative particle is "collidable" even if it has no shapes)
- Makes collision relationship generic constraints less meaningful

**Suggestion**: Keep ICollidable as opt-in. Entity should NOT implement ICollidable by default. Let users add collision capability by either inheriting from a `CollidableEntity` subclass or implementing `ICollidable` themselves.

### 3. ICollidable Interface Is Too Broad -- Violates ISP

**[High]** The proposed `ICollidable` interface mixes collision detection with collision response:

```csharp
public interface ICollidable
{
    bool CollidesWith(ICollidable other);          // detection
    Vector2 GetSeparationVector(ICollidable other); // detection
    void SeparateFrom(ICollidable other);           // response (mutates position)
    void AdjustVelocityFrom(ICollidable other);     // response (mutates velocity)
}
```

Detection and response are separate concerns. A wall needs to be collidable (detection) but should never be moved (response). A trigger zone needs detection but no response at all.

**Suggestion**: Split into two interfaces:
```
ICollidable -- detection only (CollidesWith, GetSeparationVector)
ICollisionResponder -- response only (SeparateFrom, AdjustVelocityFrom)
```

CollisionRelationship can then work with `ICollidable` for detection and optionally `ICollisionResponder` for physics responses.

### 4. IAttachable.Parent Typed as Entity -- Too Restrictive

**[High]** The `IAttachable` interface specifies:
```csharp
public interface IAttachable
{
    Entity Parent { get; set; }
    ...
}
```

Typing `Parent` as `Entity` prevents:
- Sprite-to-Sprite parenting (a Sprite is IAttachable but not Entity)
- Shape-to-Shape parenting
- Any future IAttachable type that isn't an Entity

FRB1 solved this by having `IAttachable.ParentAsIAttachable` return `IAttachable`, while `PositionedObject.Parent` returned `PositionedObject`. The FRB2 design should either:
- Type Parent as `IAttachable` on the interface
- Or introduce a separate property like `ParentEntity` for the typed version

### 5. Screen Cleanup Validation Is Missing

**[High]** FRB1's `ScreenManager` has 400+ lines of `CheckAndWarnIfNotEmpty` code that detects resource leaks between screen transitions. It checks for orphaned sprites, shapes, texts, sounds, cameras, and other managed objects. This is one of FRB1's most valuable debugging features.

The FRB2 architecture has no mention of this. Without leak detection:
- Memory leaks will be silent and hard to track down
- Entities that aren't properly destroyed will accumulate
- Content that should be unloaded will persist

**Suggestion**: Add a leak detection system or at least document the intent. Even a simple count check ("were there 0 entities when the screen was destroyed?") would be valuable.

---

## Medium Priority

### 6. Entity.CustomDraw Conflicts with IRenderable

**[Medium]** Entity has `public virtual void CustomDraw(SpriteBatch spriteBatch, Camera camera)` but Entity does NOT implement `IRenderable`. This means:
- The CustomDraw method won't be called by the render system (which iterates IRenderable objects)
- It's unclear who calls CustomDraw and when
- It creates a parallel rendering path outside the batched render pipeline

Either Entity should implement IRenderable (and participate in Z-sorted rendering), or CustomDraw should be removed in favor of attaching IRenderable children.

### 7. Collision Shapes Always in the Render List

**[Medium]** Collision shapes implement `IRenderable` "for debug drawing." This means every collision shape is always in the render list, even in release builds. For a game with thousands of collision tiles (common with Tiled maps), this adds significant overhead to the render list.

**Suggestion**: Debug rendering for shapes should be opt-in (a flag), not default. Consider having shapes implement `IRenderable` only when debug mode is active, or have the DebugRenderer handle shape visualization separately.

### 8. Manager Proliferation

**[Medium]** `FlatRedBallService` exposes 7 managers:
- ScreenManager
- LayerManager
- InputManager
- AudioManager
- ContentManagerService
- TimeManager
- DebugRenderer

Some of these could be combined or simplified:
- `DebugRenderer` is more of a utility than a manager
- `LayerManager` might not need to be a separate manager if Layer registration happens on the render system
- `TimeManager` could be a value type or simple property bag rather than a full manager

### 9. FlatRedBallService.Default Risks Becoming a Static Service Locator

**[Medium]** The single static accessor `FlatRedBallService.Default` is a necessary convenience, but it risks becoming a service locator anti-pattern if code throughout the engine accesses subsystems through `FlatRedBallService.Default.InputManager` etc.

**Suggestion**:
- Make `Default` settable only once (throw on second assignment)
- Prefer constructor injection for engine internals
- Reserve `Default` for game code convenience only

### 10. Entity.Children Typed as IReadOnlyList<object>

**[Medium]** Using `IReadOnlyList<object>` for Children loses type safety and forces boxing for value types. FRB1 uses `AttachableList<PositionedObject>` which is fully typed.

**Suggestion**: Use `IReadOnlyList<IAttachable>` instead:
```csharp
public IReadOnlyList<IAttachable> Children { get; }
```

### 11. Camera Missing Zoom Property

**[Medium]** The Camera class has `TargetWidth` and `TargetHeight` but no explicit zoom mechanism. In FRB1, Camera has `OrthogonalWidth`/`OrthogonalHeight` which effectively control zoom. The relationship between TargetWidth/TargetHeight and zoom should be explicit.

**Suggestion**: Either add a `Zoom` property that scales TargetWidth/TargetHeight, or document that changing TargetWidth/TargetHeight IS the zoom mechanism.

### 12. Entity Missing Name Property

**[Medium]** FRB1's PositionedObject has a `Name` property used extensively for debugging, collision tracking (`ItemsCollidedAgainst` stores names), and object lookup. The FRB2 Entity has no Name. IRenderable has an optional `Name` but Entity doesn't.

### 13. No Lifecycle Diagram for Entity/Screen Interaction

**[Medium]** The document shows Entity and Screen lifecycle methods separately but doesn't explain:
- Who calls Entity.CustomInitialize? (the user? the factory? the screen?)
- When in the update loop does Entity.CustomActivity get called relative to physics and collision?
- Does Entity.Destroy remove it from the screen's render list automatically?
- What order are entity CustomActivity calls made in? (creation order? hierarchy order?)

---

## Low Priority

### 14. Render List Scaling Concerns

**[Low]** The render list uses "insertion sort (O(N) for nearly-sorted data)" which is good for the common case but could degrade for large scenes. With thousands of renderables (common in tile-based games), consider:
- A skip list or tree structure for better worst-case performance
- Dirty-flag approach: only re-sort if something actually changed Z or layer

### 15. Input Clearing Between Screens

**[Low]** FRB1's InputManager has `ClearInput` and `mIgnorePushesNextFrame`/`mIgnorePushesThisFrame` to prevent input from "leaking" across screen transitions (e.g., a button press on screen A firing in screen B). The doc doesn't mention this.

### 16. Per-Entity Time Scale

**[Low]** Some games need per-entity time scaling (e.g., bullet time that doesn't affect UI). The current design has only a global TimeScale. Consider whether Entity could have a local time scale multiplier.

### 17. GumBatch Rendering Order

**[Low]** The doc says Gum objects should be placed on screen-space layers, but doesn't explain what happens if someone puts a Gum element on a world-space layer. Does it render correctly? Does it ignore the camera transform? This edge case should be documented.

### 18. Missing Dispose/IDisposable Pattern

**[Low]** `FlatRedBallService` holds references to MonoGame resources but doesn't implement `IDisposable`. For proper cleanup (especially in testing where multiple instances are created), consider implementing `IDisposable`.

### 19. Factory-Screen Coupling

**[Low]** The Factory section says it's "Associated with the current Screen at construction time" but doesn't explain the lifecycle implications. When the screen is destroyed, are all factory instances automatically destroyed? Is the factory itself destroyed? Can a factory outlive its screen?

---

## Patterns from FRB1 Worth Preserving

These are good patterns in FRB1 that the FRB2 architecture should not lose:

1. **Two-way list membership** (`ListsBelongingTo`) -- Objects know which lists contain them. This enables proper cleanup and prevents orphaned references.

2. **Collision history tracking** (`ItemsCollidedAgainst`, `LastFrameItemsCollidedAgainst`) -- Essential for detecting "just started colliding" vs "still colliding" vs "just stopped colliding."

3. **Screen leak detection** (`CheckAndWarnIfNotEmpty`) -- Catches resource leaks between screen transitions.

4. **Relative vs Absolute separation** -- Having explicit `RelativeX`/`X` properties avoids the ambiguity of dual-meaning properties.

5. **ShapeCollection as collision aggregate** -- FRB1's approach of having ICollidable expose a ShapeCollection is cleaner than putting collision logic on each shape individually.

---

## Patterns from FRB1 to Avoid

These are problematic patterns in FRB1 that FRB2 should NOT carry forward:

1. **Everything being static** -- The move to instance-based is correct and important.

2. **Public Vector3 fields** -- FRB1's `public Vector3 Position` as a public field (not property) allows direct mutation and prevents change notification. Properties are better.

3. **Manager-based object creation** -- FRB1 requires `SpriteManager.AddSprite()` to create visible sprites. Direct construction (`new Sprite()`) is better.

4. **PositionedObject having 40+ fields** -- The "lightweight entity" goal is correct.

5. **Instruction system** -- FRB1's `IInstructable`/`InstructionManager` is an overcomplicated tween/scheduling system that is better served by modern alternatives.
