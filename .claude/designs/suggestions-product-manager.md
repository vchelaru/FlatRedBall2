# ARCHITECTURE.md — Product Manager Suggestions

## Overview

After reviewing the ARCHITECTURE.md against the existing FlatRedBall (FRB1) codebase at `C:\git\FlatRedBall\` and the Gum codebase at `C:\git\Gum\`, here are findings organized by topic. Each item is tagged with a category:

- **[Accuracy]** — Something that doesn't match FRB1 behavior and may be an intentional break or an oversight
- **[Gap]** — Missing concept/section that would strengthen the document
- **[Risk]** — Potential design pitfall or gotcha
- **[Suggestion]** — Improvement or clarification idea
- **[Naming]** — Naming inconsistency or concern

---

## 1. Static State — The Biggest Architectural Delta

**[Accuracy / Risk]** The doc says "No static state except `FlatRedBallService.Default`." In FRB1, virtually *everything* is static:
- `FlatRedBallServices` — static class
- `TimeManager` — static class
- `InputManager` — static class
- `ScreenManager` — static class
- `AudioManager` — static class
- `SpriteManager` — static class
- `Camera.Main` — static property

This is the most significant breaking change in the entire architecture. The document should explicitly acknowledge this delta and discuss the migration strategy or at least call it out as a conscious design decision. Anyone familiar with FRB1 will immediately wonder "how do I access the camera?" when there's no `Camera.Main`.

**Suggestion**: Add a short "Breaking Changes from FRB1" section (or "Key Differences from FRB1") that lists the major shifts so readers coming from FRB1 understand the scope.

---

## 2. Entity vs PositionedObject

**[Accuracy]** In FRB1, the base class is `PositionedObject`, not `Entity`. Entities in FRB1 are a Glue-level concept (code-generated classes that extend PositionedObject). The ARCHITECTURE.md uses `Entity` as the base class name.

This is likely an intentional rename, but it's worth noting:
- `Entity` implies "game object" which may be too specific for things like particle positions or helper nodes
- FRB1's `PositionedObject` is used for cameras, collision shapes, sprites — not just "entities"
- **Consider**: Is `Entity` the right name if collision shapes and sprites also need position/hierarchy? The doc already has shapes and sprites that are `IAttachable` but not `Entity`. This split is fine, but the naming might confuse FRB1 users who expect PositionedObject-like universality.

**[Gap]** The FRB1 `PositionedObject` has these features not mentioned in the ARCHITECTURE.md Entity:
- **Relative position/velocity/acceleration** — In FRB1, when attached, you set `RelativeX`, `RelativeY` etc. The ARCHITECTURE.md says `X`/`Y` are "relative to parent when attached, world when not" — this dual-meaning is different from FRB1's explicit `RelativeX`/`X` split. This needs clarification on whether it's intentional.
- **Rotation velocity** (`RotationZVelocity`) — FRB1 has rotational velocity. Not mentioned.
- **KeepTrackOfReal** — FRB1 can track real velocity/acceleration. Not mentioned (maybe intentionally dropped).
- **ParentRotationChangesPosition / ParentRotationChangesRotation** — Configurable attachment behavior. Not mentioned.
- **Instructions system** (`IInstructable`) — FRB1 has a built-in instruction/tween system. The doc doesn't mention this. Is it being dropped in favor of letting users use external tween libraries?
- **ListsBelongingTo** — Two-way list membership. Not mentioned.
- **Name property** — Entities in FRB1 have names. The ARCHITECTURE.md Entity doesn't show a `Name` property.

---

## 3. ICollidable Interface — Significant Redesign

**[Accuracy / Risk]** The FRB1 `ICollidable` interface is:
```csharp
public interface ICollidable : INameable
{
    ShapeCollection Collision { get; }
    HashSet<string> ItemsCollidedAgainst { get; }
    HashSet<string> LastFrameItemsCollidedAgainst { get; }
    HashSet<object> ObjectsCollidedAgainst { get; }
    HashSet<object> LastFrameObjectsCollidedAgainst { get; }
}
```

The ARCHITECTURE.md proposes a very different interface:
```csharp
public interface ICollidable
{
    bool CollidesWith(ICollidable other);
    Vector2 GetSeparationVector(ICollidable other);
    void SeparateFrom(ICollidable other);
    void AdjustVelocityFrom(ICollidable other);
}
```

Key differences:
- FRB1 uses `ShapeCollection` as the backing data; the new design puts collision logic directly on the interface
- FRB1 tracks collision history (`ItemsCollidedAgainst`, `LastFrameItemsCollidedAgainst`) — important for platformer ground detection, "just landed" checks, etc. This is missing from the new design.
- FRB1 collision methods are `CollideAgainst`, `CollideAgainstMove`, `CollideAgainstBounce` with mass parameters. The new design doesn't show mass parameters anywhere on the interface.
- **The shapes themselves don't implement ICollidable in FRB1** — `AxisAlignedRectangle`, `Circle`, `Polygon` extend `PositionedObject` but implement separate collision methods. The ARCHITECTURE.md says shapes implement `ICollidable` directly.

**[Risk]** Having shapes implement `ICollidable` means each shape needs `SeparateFrom(ICollidable other)` and `AdjustVelocityFrom(ICollidable other)` which modify the "parent entity if attached, else self." This creates a tight coupling between collision shapes and the entity hierarchy inside a low-level interface.

---

## 4. CollisionRelationship — Missing Features

**[Gap]** The FRB1 `CollisionRelationship` has significant features not in the doc:
- **CollisionType enum**: `EventOnlyCollision`, `MoveCollision`, `BounceCollision`, `MoveSoftCollision` — the "soft" collision type (spring-like separation) is not mentioned
- **Mass parameters** — FRB1 uses `moveFirstMass`/`moveSecondMass` for controlling which object moves more. The doc shows `MoveFirstOnCollision()` / `MoveSecondOnCollision()` without mass ratios.
- **Spatial partitioning** — FRB1 has built-in partitioning support (`Partitions`, `firstPartitioningSize`, `secondPartitioningSize`). This is critical for performance with large entity counts. Not mentioned.
- **CollisionLimit** — `All`, `First`, `Closest` — controls whether collision checks stop after first hit. Not mentioned.
- **FrameSkip** — For performance, skip N frames between checks. Not mentioned.
- **IsActive** — Enable/disable relationships. Not mentioned.
- **SubObject collision** — Ability to collide against specific sub-shapes of an entity. Not mentioned.
- **CollidedThisFrame** — Boolean query. Not mentioned.

**[Suggestion]** At minimum, document spatial partitioning plans. Without it, any game with >100 entities will have O(N^2) collision performance.

---

## 5. Camera — Missing Features

**[Gap]** The FRB1 Camera extends PositionedObject and has many features not in the doc:
- **Zoom/Orthogonal settings** — FRB1 Camera has `Orthogonal`, `OrthogonalWidth`, `OrthogonalHeight`. The doc's Camera has `TargetWidth`/`TargetHeight` which seem different.
- **Split-screen viewport** — FRB1 has `SplitScreenViewport` enum and `DestinationRectangle`. Not mentioned.
- **DrawsWorld / DrawsCameraLayer / DrawsShapes** — Layer-selective rendering. Not mentioned.
- **ClearsDepthBuffer** — Not mentioned.
- **Zoom property** — How does zoom work? `TargetWidth`/`TargetHeight` might serve this purpose but it's not explicit.

**[Risk]** The doc says "Y+ up" for world space and "Y+ down" for screen space. This is correct and matches FRB1, but the Camera section should explicitly document the default Camera position and how the coordinate system is set up for a new screen.

---

## 6. Gum Integration — Needs More Detail

**[Gap]** The Gum integration section is very sparse. Based on reviewing `GumService.cs`:
- **GumService.Default** uses a static singleton pattern just like the proposed `FlatRedBallService.Default`. How do these two coexist? Who initializes Gum? Does `FlatRedBallService` call `GumService.Initialize(game)` internally?
- **Gum has its own SystemManagers** — renderer, input, content loading. How does this interact with FRB2's managers?
- **Gum has its own Cursor and Keyboard** — `FormsUtilities.Cursor`, `FormsUtilities.Keyboard`. Does FRB2's `InputManager` feed into these, or does Gum run its own input?
- **Root/PopupRoot/ModalRoot** — Gum has a visual tree hierarchy. The doc doesn't mention this.
- **CanvasWidth/CanvasHeight** — Gum has its own coordinate system. How does this relate to the Camera's `TargetWidth`/`TargetHeight`?
- **GumBatch** — The doc mentions `GumBatch` as a built-in batch but doesn't explain how it delegates. In practice, Gum's rendering goes through `SystemManagers.Renderer` which has its own sprite batching separate from MonoGame's `SpriteBatch`.
- **Forms controls** — Gum ships with a full Forms control set (Button, TextBox, CheckBox, ComboBox, ListBox, etc.). Worth mentioning these are available.

**[Suggestion]** Add a subsection showing how to add a Gum UI element to a screen:
```csharp
// How would this look in FRB2?
var button = new Button();
button.Text = "Start";
// Where does it go? Which layer? How is it rendered?
```

---

## 7. Tiled Integration — Incorrect Dependency

**[Accuracy]** The doc says "Via MonoGame.Extended.Tiled." However, FRB1 does NOT use MonoGame.Extended for Tiled support — it has its own TMX loading/rendering code. MonoGame.Extended is a separate library.

**[Suggestion]** Clarify whether FRB2 actually intends to use MonoGame.Extended.Tiled (which would be a new dependency) or will port/rewrite FRB1's Tiled support. If using MonoGame.Extended, document that this is a change from FRB1.

Also missing:
- **TileShapeCollection** — FRB1's optimized tile-based collision. Critical for performant level collision. Not mentioned.
- **TMX layer properties** — How are tile properties exposed for game logic?

---

## 8. Screen System — Missing Features

**[Gap]** FRB1's Screen has features not mentioned:
- **Pause support** — `IsPaused`, `AccumulatedPauseTime`, `PauseAdjustedCurrentTime`. Pausing is a fundamental game feature.
- **Async loading** — `AsyncLoadingState`, loading next screen while current is still active. Not mentioned.
- **CancellationTokenSource** — FRB1 Screen has this for async operations. Not mentioned.
- **Restart support** — `RestartVariables`, `RestartVariableValues`. Not mentioned.
- **Screen timing** — Time since screen started (the doc has `FrameTime.SinceScreenStart` but doesn't show how Screen connects to this).

**[Risk]** The doc says "When a screen is destroyed, its scoped ContentManager is unloaded. The new screen gets a fresh Camera." — What happens to entities that were on the old screen? Are they automatically destroyed? FRB1 has a check (`WarnIfNotEmptyBetweenScreens`) that warns if objects leak between screens.

---

## 9. Factory — Missing Key Details

**[Gap]** The Factory section mentions "Optional pooling (future version)" but doesn't discuss:
- How does Factory register entities with the Screen for lifecycle management?
- How does Factory register entities with collision relationships?
- In FRB1, factories are auto-generated by Glue with entity list management. How will FRB2 handle this without an editor?
- **Destroy callback** — When an entity is destroyed, how does the Factory know to remove it from its `Instances` list?

---

## 10. Rendering — Missing Details

**[Gap]** Missing from the rendering section:
- **Color operations / Blend operations** — FRB1 Sprite has `ColorOperation` and `BlendOperation` enums. Not mentioned.
- **FlipHorizontal / FlipVertical** — Common sprite feature. Not mentioned.
- **Source rectangle / texture coordinates** — For sprite sheets. Not mentioned.
- **Text rendering** — FRB1 has a `Text` class. How will text be rendered in FRB2? Through Gum only?
- **Particle system** — FRB1 has `Emitter`/`EmissionSettings`. Not mentioned. Intentionally dropped?

---

## 11. Input System — Missing Features

**[Gap]** Missing from the input section:
- **IInputDevice** — The doc shows this interface with action mapping (`IsActionDown(string action)`) but doesn't explain how actions are defined or mapped. This is a significant feature that needs more detail.
- **Touch input** — FRB1 has `TouchScreen`. The doc's `ICursor` says "handles mouse + touch unified" but doesn't detail touch-specific features (multi-touch, gestures).
- **Input receiving** — FRB1 has `IInputReceiver` for focused text input. Important for UI integration with Gum.

---

## 12. Naming Inconsistencies

**[Naming]** Several naming items:
- `FlatRedBallService` (singular) vs FRB1's `FlatRedBallServices` (plural) — intentional but worth noting
- `ContentManagerService` — the name includes "Service" which conflicts with the "Service" in `FlatRedBallService`. In FRB1 it's just `ContentManager`. Consider just `ContentManager` or `ContentService`.
- `CollisionOccurred` event on `CollisionRelationship` — this is past tense which is good, but FRB1 uses present tense for some events. The naming conventions section should clarify: are events past-tense (thing happened) or present-tense (thing happening)?
- `MoveToScreen<T>()` — in FRB1 this is handled by `ScreenManager` statically. Making it a method on Screen is cleaner but changes the pattern.

---

## 13. Missing Sections

**[Gap]** Concepts from FRB1 that have no mention in the ARCHITECTURE.md:
- **SpriteManager** — Who manages sprite lifecycle? Automatic management vs manual?
- **ShapeManager** — Who manages shape lifecycle?
- **Emitter / Particle system** — Dropped?
- **Text / TextManager** — How is text rendered?
- **IDestroyable** — FRB1 has this interface. How does `Entity.Destroy()` relate?
- **IVisible** — FRB1 has this interface with `AbsoluteVisible` (hierarchical). Mentioned implicitly but not as an interface.
- **Threading / async** — The update loop mentions "Flush async synchronization context" but doesn't explain what this means or how async code works in screens.
- **Error handling** — What happens when a screen transition fails? When content fails to load?

---

## 14. The `IAttachable` Interface — Children Type

**[Risk]** The doc shows:
```csharp
public IReadOnlyList<object> Children { get; }  // entities or any IAttachable
```

Using `object` loses type safety. FRB1 uses `AttachableList<PositionedObject>` which is typed. Consider:
```csharp
public IReadOnlyList<IAttachable> Children { get; }
```

---

## 15. Drag Behavior — Clarification Needed

**[Gap]** The doc says `public float Drag { get; set; }  // reduces velocity each frame` but doesn't specify:
- Is this a multiplier (0-1 range) or a flat subtraction?
- Is it applied before or after acceleration?
- FRB1 applies drag as: `Velocity *= (1 - Drag * SecondDifference)` — essentially a deceleration coefficient per second

---

## 16. Package Structure — MonoGame.Extended Concern

**[Risk]** The doc lists `MonoGame.Extended` as a dependency for Tiled rendering. MonoGame.Extended is a large library with many features. If you only need Tiled support, pulling in the entire library adds significant dependency weight. Consider:
- Using just `MonoGame.Extended.Tiled` if it's available as a separate package
- Or rolling a minimal Tiled loader (FRB1 does this)

---

## Summary of Priority Items

1. **High**: Static state migration strategy — document the FRB1 delta
2. **High**: ICollidable redesign — the new interface is very different from FRB1; collision history tracking is missing
3. **High**: Gum integration — needs significantly more detail on initialization, coordinate systems, and rendering pipeline
4. **High**: Spatial partitioning — needed for any real game
5. **Medium**: Tiled integration — clarify dependency (MonoGame.Extended vs custom)
6. **Medium**: Screen pause/async loading — fundamental game features
7. **Medium**: Missing Manager classes — who manages sprites, shapes, text?
8. **Low**: Entity naming (vs PositionedObject)
9. **Low**: Children list type safety
10. **Low**: Drag behavior specification
