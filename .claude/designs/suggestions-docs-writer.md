# ARCHITECTURE.md — Documentation Writer Suggestions (v2 — AI-Friendly Lens)

Reviewed as: **"If an AI reads this document as its sole reference, can it generate correct, compilable FRB2 game code?"**

---

## Critical — AI Cannot Generate Working Code

### 1. [Critical] No API to add objects to the render list

The doc describes a render list but never shows how to add things to it. AI creating a Sprite will write `new Sprite()` and have no idea how to make it visible. Is it `entity.AddChild(sprite)`? `screen.AddRenderable(sprite)`? `engine.RenderList.Add(sprite)`? This is the first thing every game does and it's missing.

### 2. [Critical] Collision shapes have no properties shown

`AARect`, `Circle`, `Polygon` are shown as `{ ... }` with zero properties. AI cannot create a collision shape because it doesn't know:
- Is it `Width`/`Height` or `ScaleX`/`ScaleY`?
- Is it `Radius` on Circle?
- How to define Polygon vertices?
- How to attach a shape to make an entity collidable

Need at minimum: essential properties + one "create shape, set size, attach to entity" example.

### 3. [Critical] No complete end-to-end example

AI needs ONE compilable program showing: Game class, Screen, Entity with Sprite, input, collision. Without it, AI stitches fragments from 15 sections and gets wiring wrong. This is the single most impactful addition.

### 4. [Critical] Entity lifecycle registration undefined

When is `CustomInitialize` called? Who calls it? How do entities get into the update loop? How does destruction remove from render list / collision? AI will create `new Player()` and expect everything to work automatically.

### 5. [Critical] CollisionRelationship example has type mismatch

Passes `Factory` objects to a method taking `IEnumerable`. Factory doesn't implement IEnumerable. AI copies this verbatim → compile error.

### 6. [Critical] Sprite missing SourceRectangle for sprite sheets

No way to display a sub-region of a texture. Sprite sheets are standard. AI asked to "display frame 3 of a sprite sheet" has no API.

### 7. [Critical] AnimationChain types undefined

`CurrentAnimation`, `PlayAnimation(string name)` but no explanation of:
- How to load animations (`ContentLoader.Load<AnimationChainList>("player")`?)
- How to assign animations to a Sprite
- What AnimationChain contains (frames? textures? durations?)

### 8. [Critical] FrameTime constructor won't compile

No constructor in struct definition. Test example uses named parameter `delta:` that doesn't exist. Also `1/60` is integer division = 0.

---

## Important — AI Will Write Incorrect Logic

### 9. [Important] Camera ownership vs Screen unclear

"New screen gets a fresh Camera" — but who creates it? Can AI set `Camera.TargetWidth` in `CustomInitialize`? Where do defaults come from?

### 10. [Important] ICollidable on Entity vs shapes contradictory

Entity implements ICollidable "by aggregating shapes." But shapes also implement ICollidable individually. If AI calls `shape.SeparateFrom(otherShape)`, does it move the parent entity? This behavior is documented only in a comment.

### 11. [Important] Drag formula unspecified

`Drag` comment just says "reduces velocity each frame." Multiplicative, subtractive, or exponential? AI will guess wrong.

### 12. [Important] How do sprites get assigned to layers?

Sprite has `Layer` property, `IRenderable` has `Layer`, but no example shows `sprite.Layer = frb.LayerManager.Get("Foreground")`. AI doesn't know the assignment pattern.

### 13. [Important] ContentLoader load path format unknown

`Load<T>(string path)` — is path a MonoGame content pipeline name (`"player"` without extension), a file path (`"Content/player.png"`), or relative? AI will guess wrong.

### 14. [Important] InputManager internal structure undefined

`Screen.Engine.InputManager` exists but its properties aren't shown. Is it `.Keyboard`? `.GetKeyboard()`? `.Keyboards[0]`? AI can't write input code.

### 15. [Important] AudioManager load vs play ambiguity

`PlaySong(string name)` — does this load and play? Or must content be loaded first via ContentLoader? If load-first, show the step.

### 16. [Important] Sprite.Color type ambiguous

Is this `Microsoft.Xna.Framework.Color`? The doc says "use System.Numerics" for vectors — AI may assume Color needs a special import. State the full type once.

### 17. [Important] Gum section has no code example

AI asked to "add a health bar" has zero information. Need at minimum: create element, set text, add to HUD layer.

### 18. [Important] Tiled section has no code example

AI asked to "load a level" has zero information. Need: load TMX, render on layer, get collision from tiles.

---

## Minor

### 19. [Minor] RenderDiagnostics not on FlatRedBallService class listing

Doc says "Accessed via `FlatRedBallService.RenderDiagnostics`" but the class definition doesn't include it.

### 20. [Minor] MoveToScreen vs ScreenManager.Start — two navigation methods

Are these the same? Can Start be called after init? AI may mix them up.

### 21. [Minor] Children type is IReadOnlyList<object> but AddChild takes IAttachable

AI iterating children gets `object` and must cast. Use `IReadOnlyList<IAttachable>`.

### 22. [Minor] No Rotation on Sprite or IAttachable

Entity has Rotation. Sprites don't. If attached sprite rotates with entity, that's not obvious. If it doesn't rotate, AI will file a bug.

### 23. [Minor] Camera missing Zoom property

AI writing zoom will look for `Zoom` property. If TargetWidth/TargetHeight IS the zoom mechanism, state it.

### 24. [Minor] No `using` directives shown anywhere

AI needs to know namespaces. At minimum show them once in the end-to-end example:
```csharp
using FlatRedBall2;
using Microsoft.Xna.Framework;
```

### 25. [Minor] Polygon has no construction API shown

How to define vertices? FRB1 uses `CreateRectangle(scaleX, scaleY)` and point lists. Without this, AI can't use polygons.

### 26. [Minor] Factory constructor — how does it know which Screen?

"Associated with the current Screen at construction time" — but no constructor shown. Does AI write `new EnemyFactory()` with no args? Does it need `new EnemyFactory(this)`?

### 27. [Minor] Sprite missing FlipHorizontal/FlipVertical/Alpha

Essential for platformers (character direction), particles (alpha), and effects. AI will try these and fail.

### 28. [Minor] Angle operator * should specify operand types

`Angle * Angle` is meaningless. Should be `Angle * float`. AI may try angle multiplication.
