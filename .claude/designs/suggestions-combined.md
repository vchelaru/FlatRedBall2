# ARCHITECTURE.md — Combined Suggestions for Vic

**Reviewed by**: Product Manager, Coder, Refactoring Specialist, Documentation Writer
**Sources**: FlatRedBall codebase (`C:\git\FlatRedBall\`), Gum codebase (`C:\git\Gum\`), ARCHITECTURE.md

This document consolidates findings from all four agent reviews into a single prioritized list. Items are deduplicated and organized by theme. Each item notes which agents flagged it.

Individual agent files are available for deeper detail:
- [suggestions-product-manager.md](.claude/designs/suggestions-product-manager.md)
- [suggestions-coder.md](.claude/designs/suggestions-coder.md)
- [suggestions-refactoring-specialist.md](.claude/designs/suggestions-refactoring-specialist.md)
- [suggestions-docs-writer.md](.claude/designs/suggestions-docs-writer.md)

---

## HIGH PRIORITY — Design Issues

These items represent fundamental design concerns that should be resolved before implementation.

### 1. ICollidable Interface — Needs Major Rethink
**Flagged by**: Product Manager, Coder, Refactoring Specialist

The proposed ICollidable interface is the most concerning element:

```csharp
// Proposed (problematic):
public interface ICollidable
{
    bool CollidesWith(ICollidable other);
    Vector2 GetSeparationVector(ICollidable other);
    void SeparateFrom(ICollidable other);
    void AdjustVelocityFrom(ICollidable other);
}
```

**Problems:**
- **N x N double dispatch**: Every shape must handle every other shape type internally. Adding a new shape requires modifying ALL existing shapes. (Coder)
- **Mixes detection and response**: `CollidesWith` is detection; `SeparateFrom`/`AdjustVelocityFrom` are response. A wall needs detection but not response. Violates Interface Segregation Principle. (Refactoring Specialist)
- **Missing collision history**: FRB1 tracks `ItemsCollidedAgainst` and `LastFrameItemsCollidedAgainst` — essential for "just landed," "just left ground," and other state-change detection. Not present. (Product Manager)
- **Missing mass parameters**: FRB1's collision responses use mass ratios for physics. The new API has no mass concept. (Product Manager)
- **Shapes implementing ICollidable couples them to entity hierarchy**: `SeparateFrom` "moves parent entity if attached, else moves self" — collision shapes shouldn't know about entities. (Product Manager)

**FRB1's approach (for reference)**:
```csharp
public interface ICollidable : INameable
{
    ShapeCollection Collision { get; }
    HashSet<string> ItemsCollidedAgainst { get; }
    HashSet<string> LastFrameItemsCollidedAgainst { get; }
    // ...
}
```

**Recommendation**: Consider FRB1's approach where ICollidable exposes shapes and centralized dispatch handles collision logic. Split detection from response.

---

### 2. Entity Design — Still a God-Object Despite "Lightweight" Goal
**Flagged by**: Refactoring Specialist, Coder, Product Manager

The philosophy says "Lightweight entities" and "No single heavy base class," but Entity still aggregates:
- Spatial transform (position, rotation)
- Physics (velocity, acceleration, drag)
- Scene graph (parent/child hierarchy)
- Collision (ICollidable)
- Rendering hooks (CustomDraw)
- Game logic hooks (CustomInitialize, CustomActivity, CustomDestroy)
- Visibility control

**Additional concerns:**
- Entity.CustomDraw exists but Entity is NOT IRenderable — who calls it? (Coder, Refactoring Specialist)
- Entity forces ICollidable on everything — even decorative objects. FRB1 keeps ICollidable opt-in. (Refactoring Specialist)
- `Children` typed as `IReadOnlyList<object>` — loses type safety, should be `IReadOnlyList<IAttachable>`. (All agents)
- Missing `Name` property — used everywhere in FRB1 for debugging and collision tracking. (Product Manager, Coder, Refactoring Specialist)
- X/Y dual-meaning (relative when parented, world when not) differs from FRB1's explicit `RelativeX`/`X` split and needs explanation. (Product Manager, Docs Writer)

**Recommendation**: Consider composition (Transform + PhysicsBody components). At minimum, remove CustomDraw, make ICollidable opt-in, type Children as `IReadOnlyList<IAttachable>`, and add Name.

---

### 3. Gum Integration — Too Sparse for a Core Dependency
**Flagged by**: All agents

The Gum section is 4 bullet points with no code examples, despite Gum being listed as a required dependency that's "pulled automatically."

**Missing information:**
- Who initializes Gum? Does `FlatRedBallService` call `GumService.Initialize(game)`, or does the user?
- Gum has its own `SystemManagers`, `Renderer`, `Cursor`, `Keyboard` — how do these coexist with FRB2's managers?
- Gum uses heavy static state (`SystemManagers.Default`, `GraphicalUiElement.CanvasWidth`, etc.) — this contradicts the "no static state" philosophy. (Coder)
- `GumBatch` can't easily interleave Gum elements among game sprites at different Z values — Gum renders its entire visual tree at once. (Coder)
- Gum's Root/PopupRoot/ModalRoot visual tree hierarchy is not mentioned. (Product Manager)
- Gum ships with a full Forms control library (Button, TextBox, etc.) — worth mentioning. (Product Manager)
- No code example showing how to add a UI element to a screen. (Docs Writer)

**Recommendation**: Expand to include initialization flow, coordinate system relationship, a usage example, and document the static state limitation.

---

### 4. CollisionRelationship — Missing Critical Features
**Flagged by**: Product Manager, Coder

The FRB1 CollisionRelationship has significant features not mentioned:
- **Spatial partitioning** — Without it, any game with >100 entities is O(N^2). This is a must-have. (Product Manager)
- **Mass parameters** — FRB1 uses `moveFirstMass`/`moveSecondMass` for physics ratios. The doc's `MoveFirstOnCollision()` has no mass concept.
- **CollisionLimit** — `All`, `First`, `Closest` for controlling which collisions are checked.
- **IsActive** — Enable/disable relationships at runtime.
- **FrameSkip** — Performance optimization for expensive checks.
- **Generic constraints are too loose** — Response methods need position/velocity access but `where A : ICollidable` doesn't provide that. (Coder)
- **Takes `IEnumerable<T>`** — Should be `IReadOnlyList<T>` for indexing and partitioning. (Coder)

---

### 5. Static State Migration — Undocumented Breaking Change
**Flagged by**: Product Manager, Docs Writer

FRB1 is almost entirely static (`TimeManager`, `InputManager`, `ScreenManager`, `AudioManager`, `SpriteManager`, `Camera.Main`). FRB2 moves to instance-based. This is the biggest architectural delta and is never acknowledged.

**Recommendation**: Add a "Relationship to FRB1" or "Key Differences from FRB1" section that explicitly lists what changed and why.

---

### 6. Screen System — Missing Fundamental Features
**Flagged by**: Product Manager, Refactoring Specialist, Docs Writer

Missing from Screen:
- **Pause support** — `IsPaused`, `PauseAdjustedCurrentTime`. Pausing is universal in games.
- **Async loading** — Loading next screen while current is active.
- **Screen leak detection** — FRB1's `CheckAndWarnIfNotEmpty` (400+ lines) catches orphaned objects. One of FRB1's most valuable debugging features. (Refactoring Specialist)
- **Constructor/injection wiring** — How does Engine/Camera/ContentManager get into a Screen? Not shown. (Docs Writer)
- **Entity registration** — How does the engine know which entities exist for update/physics/collision? (Coder)

---

## MEDIUM PRIORITY — API Design & Missing Features

### 7. IAttachable Interface Issues
**Flagged by**: Coder, Refactoring Specialist

- **Parent typed as Entity** — Prevents Sprite-to-Sprite or Shape-to-Shape parenting that FRB1 supports. Should be `IAttachable`. (Refactoring Specialist)
- **Missing rotation** — If a Sprite is attached to a rotating Entity, should it rotate? IAttachable has no rotation. (Coder, Docs Writer)

### 8. Sprite — Missing Essential Properties
**Flagged by**: Coder

The Sprite class is missing standard 2D sprite features:
- Source rectangle / texture region (for sprite sheets)
- Alpha / Opacity (for fade effects)
- FlipHorizontal / FlipVertical (for character direction)
- Rotation and origin/pivot
- Color operations / Blend operations

### 9. Factory<T> Design Issues
**Flagged by**: Coder, Product Manager

- `new()` constraint prevents dependency injection and constructor parameters. (Coder)
- No mechanism for Factory to know when an entity is destroyed (to remove from Instances). (Product Manager)
- No explanation of how Factory registers entities with the Screen or collision relationships. (Product Manager)

### 10. Camera — Missing Zoom and Split-Screen
**Flagged by**: Product Manager, Coder, Refactoring Specialist

- No explicit Zoom property — TargetWidth/TargetHeight might serve this purpose but it's not clear. (All)
- No split-screen viewport support (FRB1 has `SplitScreenViewport` enum). (Product Manager)
- Default position and coordinate system setup not documented. (Product Manager)

### 11. Tiled Integration — Unclear Dependency
**Flagged by**: Product Manager, Coder

The doc says "Via MonoGame.Extended.Tiled" but FRB1 has its own TMX loading code. MonoGame.Extended is a different library entirely. Clarify whether this is intentional.

Also missing: **TileShapeCollection** — FRB1's optimized tile collision. Critical for level collision performance.

### 12. Missing Manager Classes
**Flagged by**: Product Manager

FRB1 has SpriteManager, ShapeManager, TextManager — who manages object lifecycle in FRB2? How are objects registered for update/rendering?

### 13. Rendering System Gaps
**Flagged by**: Coder, Refactoring Specialist

- Collision shapes implement `IRenderable` "for debug drawing" — this puts every shape in the render list always, even in release. Should be opt-in. (Refactoring Specialist)
- No text rendering system mentioned — only through Gum? (Product Manager)
- No particle system mentioned — intentionally dropped? (Product Manager)
- WorldSpaceBatch/ScreenSpaceBatch/GumBatch lifecycle (singleton? per-layer?) unclear. (Coder)

---

## MEDIUM PRIORITY — Documentation Quality

### 14. Code Examples Have Errors
**Flagged by**: Docs Writer, Coder

- **FrameTime constructor**: The unit test example uses `new FrameTime(delta: ...)` but FrameTime has no constructor shown. Won't compile.
- **CollisionRelationship example**: Uses `bulletFactory` directly but `AddCollisionRelationship` takes `IEnumerable<A>`. Factory doesn't show it implements IEnumerable.
- **Draw method**: `_frb.Draw()` takes no args but MonoGame's `Draw(GameTime)` does — looks like a mistake without a comment.

### 15. No End-to-End Example
**Flagged by**: Docs Writer

The document has scattered code snippets but no single complete, compilable program showing how all the pieces fit together. A "Minimal Game" section would anchor the entire document.

### 16. ContentManager Scope — Ambiguous "Promotes" Language
**Flagged by**: Docs Writer

"Loading something globally that is already in screen scope promotes it automatically" — does "promotes" mean the content moves to global scope? Or that the global load returns the same instance? Both Screen and FlatRedBallService expose `ContentManagerService` as the same type — how does scoping work?

### 17. Undefined Jargon
**Flagged by**: Docs Writer

Terms used without definition: ACHX, TMX, XNB, "batch break," "batch group," "screen-space vs world-space." Add inline definitions or a glossary.

### 18. Audience Not Stated
**Flagged by**: Docs Writer

The document mixes high-level design rationale with API surface code. A brief statement about who should read this would help calibrate expectations.

---

## LOW PRIORITY — Polish & Minor Issues

### 19. Drag Formula Unspecified
**Flagged by**: Product Manager, Coder

Is Drag multiplicative (`vel *= 1 - drag * dt`), subtractive, or exponential? FRB1 uses multiplicative. Without specifying, implementers will guess differently.

### 20. Angle.Normalized() Terminology
**Flagged by**: Coder

"Clamps to [-pi, pi]" should say "wraps" — clamping caps values at bounds, wrapping maps them to equivalent in-range values.

### 21. Naming Inconsistencies
**Flagged by**: Product Manager, Docs Writer

- `ContentManagerService` has "Service" in the name but is a child of `FlatRedBallService` — double "Service." Consider just `ContentManager`.
- Naming conventions section missing private field, constant, enum value, and parameter conventions.

### 22. AudioManager — Incomplete
**Flagged by**: Docs Writer

Missing: pause/resume song, volume control, is-playing query, stopping specific sounds. `IAudioBackend` mentioned but unexplained.

### 23. Formatting Inconsistencies
**Flagged by**: Docs Writer

- Inconsistent backtick usage for type names in prose
- Heading hierarchy inconsistent (IAttachable vs IRenderable at different levels)
- Update Loop should visually separate Update and Draw phases

### 24. Entity — "No Scale Property" and "Should Also Have" Language
**Flagged by**: Docs Writer

"No Scale property" is unexplained — why? "Entities should also have a Z value that can control children Z" reads like a TODO, not documentation.

### 25. Debug Renderer vs Shape Debug Drawing — Two Systems
**Flagged by**: Docs Writer

Collision shapes implement IRenderable "for debug drawing" AND there's a separate DebugRenderer. When to use which? Are shapes always visible?

### 26. Missing Dispose/IDisposable
**Flagged by**: Refactoring Specialist

`FlatRedBallService` holds MonoGame resources but doesn't implement IDisposable. Important for testing cleanup.

---

## PATTERNS FROM FRB1 WORTH PRESERVING

These FRB1 patterns were identified as valuable and should not be lost in the rewrite:

1. **Collision history tracking** (`ItemsCollidedAgainst`, `LastFrameItemsCollidedAgainst`) — Essential for state-change detection in platformers and other games.
2. **Screen leak detection** (`CheckAndWarnIfNotEmpty`) — Catches orphaned objects between screen transitions. One of FRB1's best debugging features.
3. **Two-way list membership** (`ListsBelongingTo`) — Objects know which lists contain them, enabling proper cleanup.
4. **ShapeCollection as collision aggregate** — Cleaner than putting collision logic on individual shapes.
5. **Input clearing between screens** — Prevents button presses from "leaking" across transitions.

## PATTERNS FROM FRB1 TO AVOID

These FRB1 patterns were identified as problematic and the new design correctly avoids them:

1. **Everything being static** — The move to instance-based is correct.
2. **Public Vector3 fields** — Properties are better for change notification and encapsulation.
3. **Manager-based object creation** (`SpriteManager.AddSprite()`) — Direct construction is better.
4. **PositionedObject having 40+ fields** — The "lightweight" goal is correct.
5. **Instruction system** (`IInstructable`/`InstructionManager`) — Better served by modern alternatives.
