# ARCHITECTURE.md — Refactoring Specialist Suggestions (v2 — AI-Friendly Lens)

Reviewed through the lens: **"Is this design simple enough for AI to use correctly? Does every type/interface earn its keep?"**

---

## High Priority

### 1. [High] ICollidable Has Methods That Are Internal Plumbing

`SeparateFrom(ICollidable)` and `AdjustVelocityFrom(ICollidable)` are low-level primitives that AI should never call directly. In practice, AI uses `CollisionRelationship` to set up responses (`.MoveFirstOnCollision()`, `.BounceOnCollision()`). These methods are implementation details leaking into the public interface.

**Suggestion**: Consider making `SeparateFrom`/`AdjustVelocityFrom` internal. ICollidable should only need `CollidesWith` and `GetSeparationVector` for AI-facing code.

### 2. [High] No Support for Entity-vs-Static-Geometry Collision

`CollisionRelationship<A, B>` requires two `IEnumerable<ICollidable>` lists. But the most common collision in games is entity-vs-level-geometry (walls, platforms, tiles). In FRB1, you collide an entity list against a `ShapeCollection` (walls). The FRB2 architecture has no path for this.

AI will need this on day one for any platformer or top-down game. Either CollisionRelationship needs overloads for static shapes, or there needs to be a clear alternative.

### 3. [High] FRB2 Correctly Eliminates Manager Registration Boilerplate (Preserve This)

FRB1 required every entity constructor to manually register with SpriteManager, ShapeManager, etc. FRB2's auto-registration through `AddChild` eliminates this. This is the single biggest AI-friendliness improvement. **Do not regress.**

### 4. [High] CollisionRelationship Missing Single-vs-List Overload

`AddCollisionRelationship(IEnumerable<A>, IEnumerable<B>)` only handles list-vs-list. AI will frequently need "player vs enemies" (single vs list). Without an overload, AI must write `new[] { player }` which is non-obvious.

---

## Medium Priority

### 5. [Medium] IInputDevice String-Based Actions Lack Discoverability

`IsActionDown(string action)` means AI must guess action names. It will write `"jump"` when the game uses `"Jump"`. Consider typed constants or an enum instead of raw strings. Or drop `IInputDevice` entirely — `IKeyboard`/`IGamepad` directly are more explicit and AI can see exactly what's available.

### 6. [Medium] Missing I2DInput / IPressableInput Equivalents from FRB1

FRB1's most AI-friendly pattern:
```csharp
public I2DInput MovementInput { get; set; }
public IPressableInput BoostInput { get; set; }
```
These let entities be input-device-independent. AI writes entity logic once, and input source is swapped externally. This is a great pattern that makes AI code more correct. Consider preserving.

### 7. [Medium] Sprite Missing Rotation — Inconsistency with Entity

Entity has `Angle Rotation`. Sprite attached to a rotating Entity should rotate too. But `IAttachable` has no rotation, so it's unclear if rotation propagates. Either add `Rotation` to `IAttachable` or document how rotation propagation works through the parent-child transform.

### 8. [Medium] Children Typed as IReadOnlyList<object>

Should be `IReadOnlyList<IAttachable>`. Using `object` means AI gets no type info when iterating children. This is a simple fix with high AI benefit.

### 9. [Medium] RenderDiagnostics Not Listed on FlatRedBallService

The doc says "Accessed via `FlatRedBallService.RenderDiagnostics`" but the FlatRedBallService class listing doesn't include it. AI will try to access it and get a compile error.

### 10. [Medium] Entity Has No Path to Access Camera

Entity doesn't reference Screen or Camera. AI writing "keep entity on screen" or "camera follow" code needs Camera access from an Entity. How? Through `FlatRedBallService.Default`? Through some injected reference? This is a common operation with no documented path.

---

## Low Priority

### 11. [Low] Factory.Destroy Is Redundant With Entity.Destroy

Two ways to destroy: `factory.Destroy(enemy)` vs `enemy.Destroy()`. AI won't know which to use. `Entity.Destroy()` should auto-remove from Factory. One path, not two.

### 12. [Low] Camera Missing Drag for Consistency

Camera has VelocityX/Y and AccelerationX/Y (same as Entity) but no Drag. If Camera has velocity/acceleration, should it also have drag? Either add for consistency or remove acceleration if camera physics aren't a real use case.

### 13. [Low] IAudioBackend Is Premature Abstraction

`public IAudioBackend Backend { get; set; }` adds a type for no AI benefit. Games use MonoGame audio. AI will wonder "do I need to set this?" Remove from initial architecture — add later if needed.

### 14. [Low] Collision Shapes Should Use Width/Height Not ScaleX/ScaleY

FRB1's `ScaleX`/`ScaleY` (half-dimensions) was a constant confusion source. `Width`/`Height` (full dimensions) is more AI-predictable. Document that shapes use Width/Height to match Sprite.

### 15. [Low] IAttachable.Destroy() Semantics Unclear for Shapes

What does destroying a Circle do? Remove it from parent Entity's collision shapes? Remove from render list? AI might call `circle.Destroy()` expecting shape removal and get unexpected behavior.

---

## Patterns FRB1 Got Right for AI (Preserve These)

1. **Flat property surface**: `X`, `Y`, `VelocityX`, `VelocityY`, `Drag` all directly on the object. No `.Transform.Position.X`. FRB2 preserves this — good.
2. **CollisionRelationship event**: `CollisionOccurred += (a, b) => { }`. One-line subscription. FRB2 preserves this — good.
3. **Screen lifecycle**: `CustomInitialize`, `CustomActivity`, `CustomDestroy`. Three methods, always the same. FRB2 preserves this — good.
4. **Factory with typed list**: `Factory<T>.Instances`. Simple creation + typed iteration. FRB2 preserves this — good.
5. **I2DInput / IPressableInput**: Device-independent input abstractions. FRB2 should preserve these.

## Patterns FRB1 Got Wrong (Correctly Avoided)

1. **Everything static** — Can't test, can't run multiple instances. FRB2 correctly uses instance-based.
2. **Manual manager registration** — `SpriteManager.AddPositionedObject(this)` in every constructor. FRB2's `AddChild` auto-registration eliminates this.
3. **Manual Destroy cleanup** — Miss one unregister and you get a silent leak. FRB2's recursive destroy eliminates this.
4. **ICollidable 5-property boilerplate** — Copy-paste into every entity. FRB2 putting it on Entity base eliminates this.
5. **Screen navigation by string** — `MoveToScreen(typeof(T).FullName)`. FRB2's `MoveToScreen<T>()` is type-safe.
