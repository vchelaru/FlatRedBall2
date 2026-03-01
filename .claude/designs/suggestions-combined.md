# ARCHITECTURE.md — Combined Suggestions for Vic (v2)

**Primary lens**: FRB2 exists so AI can read ARCHITECTURE.md and write correct game code on the first try.
**Reviewed by**: Product Manager, Coder, Refactoring Specialist, Documentation Writer

Individual agent files:
- [suggestions-product-manager.md](.claude/designs/suggestions-product-manager.md)
- [suggestions-coder.md](.claude/designs/suggestions-coder.md)
- [suggestions-refactoring-specialist.md](.claude/designs/suggestions-refactoring-specialist.md)
- [suggestions-docs-writer.md](.claude/designs/suggestions-docs-writer.md)

---

## CRITICAL — AI Will Generate Broken Code

These gaps mean AI-generated code will not compile or will silently fail.

### 1. No Complete End-to-End Example
**All 4 agents flagged this.**

This is the single most impactful addition. AI needs ONE compilable program showing Game class → Screen → Entity with Sprite → input → collision. Without it, AI stitches fragments from 15 sections and gets the wiring wrong every time.

### 2. Entity Registration Is Undefined
**Flagged by: PM, Coder, Docs Writer**

The update loop says physics and CustomActivity run on entities, but nothing shows how entities get into the loop. No `Screen.Entities` list, no `Screen.Add()`, no auto-registration. AI creates `new Player()` and wonders why nothing happens.

Related: No API shown for adding objects to the render list. AI creates a Sprite but has no idea how to make it appear on screen.

### 3. Code Examples Have Compile Errors
**Flagged by: PM, Coder, Docs Writer**

AI copies examples verbatim. Three will break:
- **FrameTime**: No constructor shown, test uses `new FrameTime(delta: ...)`. Also `1/60` is integer division = 0.
- **CollisionRelationship**: Passes `Factory` objects but API takes `IEnumerable`. Factory doesn't implement IEnumerable.
- **Draw**: `_frb.Draw()` takes no args while `_frb.Update(gameTime)` does. Inconsistency looks like a mistake.

### 4. Entity X/Y Dual Meaning Is an AI Trap
**Flagged by: PM, Coder**

`X` is "relative to parent when attached, world when not." AI writes `entity.X = 100` and gets world position OR parent offset depending on runtime state. No compile-time signal. This is the #1 source of silent bugs.

**Options**: Add `LocalX`/`WorldX` properties, or keep dual-meaning but document prominently with examples of both cases.

### 5. Sprite Missing SourceRectangle — Can't Do Sprite Sheets
**Flagged by: PM, Coder, Docs Writer**

No way to display a sub-region of a texture. Sprite sheets are universal in 2D. AI cannot render individual frames. Must add `SourceRectangle` or equivalent.

### 6. Collision Shapes Have No Properties Shown
**Flagged by: Docs Writer, PM**

`AxisAlignedRectangle`, `Circle`, `Polygon` shown as `{ ... }`. AI can't create them because it doesn't know the properties (Width/Height? ScaleX/ScaleY? Radius? Points?). Need properties + a "create shape, set size, attach to entity" example.

### 7. AnimationChain Types Undefined
**Flagged by: Docs Writer**

Sprite has `PlayAnimation(string name)` but: How to load animations? How to assign them to a Sprite? What does AnimationChain contain? AI cannot write animation code from this.

---

## IMPORTANT — AI Will Write Incorrect Code

### 8. Sprite Missing Rotation, Flip, Alpha
**Flagged by: PM, Coder, Refactoring Specialist**

`FlipHorizontal` (character direction), `Rotation` (spinning projectiles), `Alpha` (fade effects) are missing. Nearly every 2D game needs these. AI will try them and fail.

### 9. No Entity-vs-Static-Geometry Collision Path
**Flagged by: Refactoring Specialist**

`CollisionRelationship` requires two `IEnumerable<ICollidable>` lists. But the most common collision is entity-vs-walls/tiles. No overload for entity-vs-ShapeCollection or entity-vs-TileMap. AI building a platformer is stuck on day one.

Related: Missing single-vs-list overload. AI writing "player vs enemies" must wrap player in `new[] { player }`.

### 10. ICollidable Leaks Internal Plumbing
**Flagged by: Refactoring Specialist, Coder**

`SeparateFrom()` and `AdjustVelocityFrom()` on ICollidable are implementation details used by CollisionRelationship. AI should never call these directly — it should use `.MoveFirstOnCollision()`. Consider making them internal.

Also: double-dispatch problem — each shape must handle every other shape type. If the engine handles all dispatch internally and AI never implements ICollidable, document that.

### 11. InputManager Structure Undefined
**Flagged by: Docs Writer**

`Screen.Engine.InputManager` exists but its properties aren't shown. Is keyboard access `.Keyboard`? `.GetKeyboard()`? AI can't write input code without this.

### 12. Gum Section Has No Code Example
**Flagged by: PM, Docs Writer**

AI asked to "add a health bar" or "add a button" has zero information. Need at minimum: create element, set properties, add to HUD layer.

### 13. Tiled Section Has No Code Example
**Flagged by: PM, Docs Writer**

AI asked to "load a level" has zero information. Need: load TMX, render on layer, get collision.

### 14. ContentManager Load Path Format Unknown
**Flagged by: Docs Writer**

`Load<T>(string path)` — is path a content pipeline name (`"player"`)? File path (`"Content/player.png"`)? AI will guess wrong.

### 15. Entity.CustomDraw Exists But Entity Is Not IRenderable
**Flagged by: Coder, Refactoring Specialist**

Nothing calls CustomDraw. AI overriding it has a silent no-op. Remove it or explain who calls it.

### 16. IAttachable.Parent Typed as Entity
**Flagged by: Coder**

Prevents Sprite-to-Sprite parenting. AI writing `sprite.Parent = otherSprite` gets a type error.

### 17. CollisionRelationship Response Methods Can't Work With Just ICollidable
**Flagged by: Coder**

`MoveFirstOnCollision()` needs position/velocity, but `where A : ICollidable` doesn't have those. Runtime casting will be needed.

### 18. Drag Formula Unspecified
**Flagged by: PM, Coder, Docs Writer**

Multiplicative vs subtractive produces different gameplay. Specify: `vel *= (1 - drag * dt)`.

### 19. AudioManager — Load vs Play Ambiguity
**Flagged by: Docs Writer**

Does `PlaySong("bgm")` load and play? Or load first via ContentManager? Show the pattern.

---

## MINOR — Polish for AI

### 20. Children Should Be IReadOnlyList<IAttachable> Not object
**Flagged by: All agents**

Using `object` means AI must cast. Simple fix, high AI benefit.

### 21. Camera Missing Zoom Property
**Flagged by: PM, Coder, Refactoring Specialist**

AI writing zoom will look for `Zoom`. If TargetWidth/TargetHeight IS the zoom mechanism, state it.

### 22. RenderDiagnostics Not Listed on FlatRedBallService
**Flagged by: Refactoring Specialist, Docs Writer**

Doc says "Accessed via FlatRedBallService.RenderDiagnostics" but class listing doesn't include it.

### 23. Factory.Destroy Redundant With Entity.Destroy
**Flagged by: Refactoring Specialist**

Two ways to destroy creates ambiguity. Entity.Destroy() should auto-remove from Factory. One path.

### 24. MoveToScreen vs ScreenManager.Start — Two Navigation Methods
**Flagged by: Docs Writer**

Are these the same? Can Start be called after init? AI may mix them up.

### 25. IAudioBackend Is Premature Abstraction
**Flagged by: Refactoring Specialist**

Adds a type AI will wonder about. Games use MonoGame audio. Remove from initial architecture.

### 26. Angle Operator * Should Specify Operand Types
**Flagged by: Coder**

`Angle * Angle` is meaningless. Should be `Angle * float`.

### 27. No Using Directives Shown
**Flagged by: Docs Writer**

AI needs namespace imports. Show once in the end-to-end example.

### 28. Entity No Path to Access Camera
**Flagged by: Refactoring Specialist**

AI writing "camera follow player" code needs Camera access from Entity. No documented path.

---

## THINGS THE DOC GETS RIGHT — Do Not Change

All agents agreed these are good for AI:

1. **Entity as single class with position/velocity/rotation** — One type. AI loves this.
2. **Screen lifecycle (CustomInitialize/CustomActivity/CustomDestroy)** — Predictable 3-method pattern.
3. **FlatRedBallService as single entry point** — AI knows where everything is.
4. **CollisionRelationship fluent API** — `.MoveFirstOnCollision()` is natural to chain.
5. **Factory with typed instances** — Simple creation pattern.
6. **No static state** — Predictable through service instance.
7. **Consistent naming** — PascalCase, no abbreviations. AI relies on this.
8. **Auto-registration via AddChild** — Eliminates FRB1's manual manager boilerplate.
9. **Recursive Destroy** — Eliminates FRB1's manual cleanup bugs.
10. **Generic screen navigation** — `MoveToScreen<T>()` is type-safe.
11. **Layer system** — Simple concept (string name + draw order). AI gets it immediately.
12. **I2DInput / IPressableInput patterns from FRB1** — Consider preserving these device-independent input abstractions.

---

## PATTERNS FRB1 GOT WRONG — Correctly Avoided

1. Everything static — can't test or run multiple instances
2. Manual manager registration in every constructor
3. Manual Destroy cleanup — miss one and get silent leaks
4. ICollidable 5-property boilerplate on every entity
5. Screen navigation by string name
6. Vector3 for 2D positions
