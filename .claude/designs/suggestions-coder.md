# ARCHITECTURE.md — Coder Suggestions (v2 — AI-Friendly Lens)

Reviewed through the lens: **"Will AI generate correct, compilable game code from this document?"**

---

## Critical -- AI-Generated Code Will Not Compile

### 1. [Critical] CollisionRelationship example passes Factory but API takes IEnumerable

```csharp
AddCollisionRelationship(bulletFactory, enemyFactory) // Factory is not IEnumerable
```
`Factory<T>` does not implement `IEnumerable<T>`. Fix: use `bulletFactory.Instances` or make Factory implement IEnumerable.

### 2. [Critical] CollisionOccurred — event vs delegate ambiguity

The doc declares `public event Action<A, B> CollisionOccurred` but the fluent chain usage works differently depending on whether this is an `event` or a delegate field. FRB1 uses plain `Action` fields, not events. Clarify which, because AI will generate different code for each.

### 3. [Critical] FrameTime has no constructor — unit test won't compile

```csharp
var frameTime = new FrameTime(delta: TimeSpan.FromSeconds(1/60.0));
```
No constructor shown in the struct definition. Also `1/60` is integer division = 0. Fix: add constructor to definition, use `1.0/60.0`.

### 4. [Critical] Entity.X dual meaning is an AI trap

`X` is "relative to parent when attached, world when not." AI setting `player.X = 100` will get world position if unparented, offset if parented — silent bug. This is the #1 source of AI-generated bugs. Either add `RelativeX`/`RelativeY` as separate properties, or document prominently with examples of both cases.

### 5. [Critical] No entity registration mechanism documented

The update loop says physics/CustomActivity run on entities, but nothing shows how the engine knows entities exist. No `Screen.Entities` list, no `Screen.Add()`, no auto-registration. AI will create entities and wonder why physics/update isn't running.

### 6. [Critical] Sprite missing SourceRectangle — can't do sprite sheets

No `SourceRectangle` or texture coordinate properties. Sprite sheets are universal in 2D games. AI asked to "display frame 3 of a sprite sheet" has no API.

---

## Important — AI Will Write Incorrect Logic

### 7. [Important] Sprite missing Rotation, Flip, Alpha

No `Rotation`, `FlipHorizontal`/`FlipVertical`, or `Alpha`. These are needed in virtually every 2D game (character direction, spinning projectiles, fade effects). AI will try `sprite.FlipHorizontal = true` and get a compile error.

### 8. [Important] ICollidable double-dispatch — shapes must handle all other shape types

Each shape implementing `CollidesWith(ICollidable other)` means each must handle every concrete type. FRB1's approach (ICollidable exposes `ShapeCollection`, centralized dispatch) is simpler and more extensible. If the engine handles all dispatch internally and AI never implements ICollidable, document that constraint.

### 9. [Important] CollisionRelationship response methods can't work with just ICollidable

`MoveFirstOnCollision()` needs position/velocity access. `where A : ICollidable` doesn't provide that. Implementation must runtime-cast to Entity, meaning the type system can't guarantee it works.

### 10. [Important] Entity.CustomDraw exists but Entity is not IRenderable — orphan method

Nothing in the render pipeline calls `CustomDraw`. AI overriding it will have a silent no-op. Remove it or explain who calls it.

### 11. [Important] IAttachable.Parent typed as Entity — prevents Sprite-to-Sprite parenting

AI writing `sprite.Parent = otherSprite` gets a type error. Should be `IAttachable` or documented that only Entity can be a parent.

### 12. [Important] Draw method takes no GameTime but Update does — inconsistency

```csharp
protected override void Update(GameTime gameTime) => _frb.Update(gameTime);
protected override void Draw(GameTime gameTime) => _frb.Draw(); // no gameTime?
```
AI will wonder if it should pass gameTime to Draw. Add a comment or make consistent.

### 13. [Important] No complete "minimal game" example

Fragments across 15 sections, no compilable program. AI must stitch them together and will get wiring wrong. One 30-line Hello World would anchor everything.

---

## Minor

### 14. [Minor] Angle.Normalized() says "clamps" but means "wraps"

AI may implement clamping instead of wrapping. Use "wraps to [-pi, pi]".

### 15. [Minor] Camera missing Zoom property

AI trying to zoom will look for `Zoom` and not find it. Document that TargetWidth/TargetHeight IS the zoom mechanism.

### 16. [Minor] Drag formula unspecified

Multiplicative vs subtractive vs exponential produces different gameplay feel. Specify: `vel *= (1 - drag * dt)`.

### 17. [Minor] Children typed as IReadOnlyList<object>

Should be `IReadOnlyList<IAttachable>`. Using `object` means AI must cast on every access.

### 18. [Minor] Screen constructor/injection not shown

AI will try `new GameScreen()` and not know how Engine/Camera/ContentLoader get injected.

### 19. [Minor] AudioManager takes string names — load path format unknown

Is `"explosion"` a content pipeline path or file path? AI will guess wrong.

### 20. [Minor] Angle operator `*` is ambiguous

`Angle * Angle` is meaningless. Should be `Angle * float`. Specify operand types.
