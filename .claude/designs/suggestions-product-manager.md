# ARCHITECTURE.md — Product Manager Suggestions (v2 — AI-Friendly Lens)

## Core Premise

FRB2's #1 goal is **AI-friendliness**: an AI reads ARCHITECTURE.md and writes correct game code on the first try. Every suggestion below is evaluated against: **"Does this help AI write better code, or does it add complexity that makes AI's job harder?"**

---

## HIGH PRIORITY — AI Will Generate Broken Code Without These

### 1. Code Examples Must Compile — They Don't Currently

AI will copy code examples verbatim. Three examples in the doc will produce broken code:

**FrameTime** — No constructor shown, but the test example uses one:
```csharp
// Doc shows this, but FrameTime has no constructor defined:
var frameTime = new FrameTime(delta: TimeSpan.FromSeconds(1/60.0));
```
**Fix**: Add the constructor to the FrameTime struct definition.

**CollisionRelationship** — Uses Factory directly but AddCollisionRelationship takes IEnumerable:
```csharp
// Does Factory<T> implement IEnumerable<T>? Not shown.
AddCollisionRelationship(bulletFactory, enemyFactory)
```
**Fix**: Either show that Factory implements IEnumerable, or use `bulletFactory.Instances`.

**Draw** — `_frb.Draw()` takes no args but MonoGame's Draw(GameTime) does:
```csharp
protected override void Draw(GameTime gameTime) => _frb.Draw();
```
**Fix**: Add a comment explaining why gameTime is unused, or accept GameTime.

### 2. X/Y Dual-Meaning Will Cause AI Bugs

The Entity says `X` is "relative to parent when attached, world when not." This is the #1 source of AI confusion. When AI writes:
```csharp
entity.X = 100; // Is this world position or offset from parent?
```
It can't know without checking if the entity has a parent. AI will write bugs here constantly.

**Options for Vic:**
- Keep the dual-meaning but document it VERY explicitly with examples of both cases
- Or use separate properties (`LocalX`/`WorldX` or `X`/`AbsoluteX` where X is always local)

### 3. No Complete Example — AI Can't See How Pieces Connect

This is the single biggest gap for AI. The doc has fragments across 15 sections but no complete program. AI needs ONE example showing:
- Game class setup
- Screen with entities
- Sprite on a layer
- Input handling
- Collision
- Screen transition

Without this, AI has to assemble pieces from different sections and guess at the wiring. It will guess wrong.

### 4. Entity Registration — How Does the Engine Know Entities Exist?

The update loop says it runs "all entity CustomActivity calls" and "applies physics." But there's no mechanism shown for how entities register with the engine. AI will create an Entity and wonder why Update/physics/collision aren't running on it.

**AI needs to know**: Do you add entities to a Screen list? Does Factory handle this? Is it automatic?

### 5. Gum Section — AI Can't Write UI Code From This

The Gum section is 4 bullet points. AI asked to "add a health bar" or "add a start button" has zero information about:
- How to initialize Gum
- How to create a UI element
- How to respond to button clicks
- Where UI elements go (which layer, which root)

At minimum, show one working UI example.

---

## MEDIUM PRIORITY — AI Will Write Suboptimal Code

### 6. Sprite Missing Source Rectangle

AI writing sprite sheet code will fail. Every 2D game uses sprite sheets. The Sprite class has no `SourceRectangle` property. AI will try to render individual frames and have no way to do it (other than through AnimationChain, which only covers the animation case, not static sprite sheet frames).

### 7. Sprite Missing Flip, Alpha, Rotation

AI writing a platformer will immediately need:
- `FlipHorizontal` — character faces left/right
- Alpha/opacity — fade effects
- Rotation — projectile rotation

These are missing from the Sprite definition.

### 8. Collision Shapes — No Properties Shown

AI can't create collision shapes because the doc shows `{ ... }` for all three shapes. It needs to see:
- `AxisAlignedRectangle`: Width, Height (or is it ScaleX/ScaleY like FRB1?)
- `Circle`: Radius
- `Polygon`: how to define vertices

### 9. ICollidable Double-Dispatch — Will Be Hard to Extend

Each shape implementing `CollidesWith(ICollidable other)` means each shape must handle every other shape type. AI writing a custom collidable type would need to modify all existing shapes. FRB1's approach (expose a ShapeCollection, let centralized code handle dispatch) is actually simpler for AI because there's one collision entry point.

**But**: The current design is fine if the engine handles all shape-vs-shape combinations internally and AI never needs to add new shape types. Document this constraint.

### 10. Tiled Section — AI Can't Load a Level From This

Same problem as Gum. AI asked to "load a Tiled map" has 4 bullet points and no code. Show:
```csharp
var map = ContentManager.Load<TiledMap>("level1");
// How to render it? How to get collision from it?
```

### 11. Factory Destroy Lifecycle — How Do Dead Entities Leave?

AI will create entities via Factory, then call `entity.Destroy()`. Does the Factory's `Instances` list automatically update? If not, AI code will have stale references. This needs to be explicit.

---

## LOW PRIORITY — Polish

### 12. Drag Behavior Unspecified
AI will set `Drag = 0.5f` and not know if that's a multiplier or flat subtraction. One sentence fixes this.

### 13. Camera Zoom
AI writing a zoom feature will look for a Zoom property and not find one. Document that TargetWidth/TargetHeight IS the zoom mechanism (if that's the intent).

### 14. AudioManager — Minimal but Functional
The API shown is enough for AI to play sounds. Could add pause/resume but it's not blocking.

### 15. Children Type Safety
`IReadOnlyList<object>` means AI has to cast. `IReadOnlyList<IAttachable>` lets AI use the children directly. Small but AI will write cleaner code with the typed version.

---

## THINGS THE DOC GETS RIGHT FOR AI

These are good decisions that should NOT change:

1. **Entity as a single class with position+velocity+rotation** — Simple. One type to learn. AI loves this.
2. **Screen lifecycle (CustomInitialize/CustomActivity/CustomDestroy)** — Clear, predictable pattern.
3. **FlatRedBallService as the single entry point** — AI knows where everything is.
4. **Layer system** — Simple concept, easy for AI to use.
5. **CollisionRelationship with fluent API** — AI can chain `.MoveFirstOnCollision()` naturally.
6. **Factory pattern** — Simple creation/destruction. AI gets this immediately.
7. **No static state (mostly)** — Makes the API predictable through the service instance.
8. **Naming conventions** — PascalCase, no abbreviations. AI relies on consistent naming.
