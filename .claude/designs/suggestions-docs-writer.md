# ARCHITECTURE.md -- Documentation Writer Suggestions

After reviewing the ARCHITECTURE.md against the FRB1 codebase (`C:\git\FlatRedBall\`) and Gum codebase (`C:\git\Gum\`), here are documentation quality findings organized by topic. Each item is tagged:

- **[Clarity]** -- Concept is unclear or could be misread
- **[Completeness]** -- Information is missing or only partially covered
- **[Consistency]** -- Terminology, formatting, or style conflicts with itself
- **[Error]** -- Something that appears incorrect or would not compile
- **[Structure]** -- Document organization or readability issue
- **[Missing]** -- An entire section or topic that should exist but does not

---

## 1. Audience Is Never Stated

**[Missing]** The document does not declare its audience. Is it for:
- Engine users (game developers building games with FRB2)?
- Engine developers (contributors to FRB2 itself)?
- FRB1 users migrating to FRB2?

The content mixes high-level design rationale (useful for contributors) with API surface code blocks (useful for users). A brief "Audience" statement at the top would help readers calibrate expectations. The tone currently leans toward an experienced C# developer who may or may not know FRB1 -- making that explicit would help.

---

## 2. Philosophy Section -- Unexplained Assertions

**[Clarity]** Several philosophy bullets make claims without explaining *why* or *what problem they solve*:

- **"No single heavy base class that everything inherits from"** -- A reader unfamiliar with FRB1 will not understand what "heavy base class" means. A one-sentence nod to FRB1's `PositionedObject` (2,000+ lines, 40+ fields) would justify this bullet.
- **"Flush async synchronization context"** (in the Update Loop section) -- This phrase is jargon. What does it mean in practice? What async patterns does it enable? A sentence explaining "this allows `async`/`await` inside `CustomActivity` to resume on the game thread" would suffice.
- **"Offload to existing libraries"** -- The list `MonoGame, MonoGame.Extended, Gum, System.Numerics` appears here and again in Package Structure. Pick one canonical location and cross-reference from the other.

---

## 3. Entity Section -- Dual-Meaning Position Properties

**[Clarity]** The Entity section says:

> `// Position -- relative to parent when attached, world when not`

This is a significant semantic change from FRB1, where `X` is always absolute and `RelativeX` is a separate property. The doc states this in a code comment but never calls it out as a design decision or explains the tradeoff. A developer coming from FRB1 (or from Unity, where `transform.position` is world and `transform.localPosition` is local) will find this confusing.

**Suggestion**: Add a short paragraph below the Entity code block explaining:
1. That X/Y are contextual (relative when parented, absolute when not).
2. That AbsoluteX/AbsoluteY always return world position.
3. Why this was chosen over FRB1's explicit RelativeX/X split.

---

## 4. Entity Section -- "No Scale Property" Statement Is Orphaned

**[Clarity]** The sentence "No `Scale` property" appears after the code block with no explanation. Why was Scale excluded? Is scaling handled per-Sprite instead? Should users set Width/Height on Sprite directly? A one-sentence rationale would prevent confusion.

The same paragraph also says "Entities should also have a Z value that can control children Z" -- this reads like a TODO or design note rather than documentation. Either state the behavior definitively ("Children Z values are relative to their parent Entity's Z") or remove the hedging language ("should also have").

---

## 5. Entity Section -- ICollidable Aggregation Explained Twice

**[Consistency]** The text explaining Entity's ICollidable behavior appears in two places:

1. Just below the Entity code block: "Entity implements `ICollidable` by aggregating all attached collision shapes."
2. In the Collision Shapes section: "Entity implements ICollidable by aggregating its attached shapes" (with a code block).

This duplication risks going out of sync. Move the full explanation to the Collision Shapes section and add a brief forward reference in the Entity section: "See Collision Shapes for how Entity implements `ICollidable`."

---

## 6. Code Example -- Draw Method Signature Mismatch

**[Error]** In the `FlatRedBallService` usage example:

```csharp
protected override void Draw(GameTime gameTime) => _frb.Draw();
```

The MonoGame `Game.Draw` method signature is `Draw(GameTime gameTime)`, but `FlatRedBallService.Draw()` takes no parameters. This is technically correct (the gameTime parameter is ignored), but it looks like a mistake. Either:
- Add a brief comment: `// gameTime not needed; engine uses TimeManager`
- Or have `FlatRedBallService.Draw` accept a `GameTime` for consistency with MonoGame's pattern.

---

## 7. Code Example -- FrameTime Constructor in Unit Test

**[Error]** The unit testing example shows:

```csharp
var frameTime = new FrameTime(delta: TimeSpan.FromSeconds(1/60.0));
```

But the `FrameTime` struct shown earlier has no constructor -- only properties `Delta`, `SinceScreenStart`, `SinceGameStart`, and `DeltaSeconds`. If FrameTime is a `readonly struct`, it would need an explicit constructor or factory method. The test example implies a constructor with named parameter `delta:`, but `SinceScreenStart` and `SinceGameStart` are not provided. This would not compile as shown.

**Suggestion**: Either add the constructor to the FrameTime definition, or update the test example to match the actual API.

---

## 8. Code Example -- CollisionRelationship Event Subscription

**[Error]** The collision usage example:

```csharp
AddCollisionRelationship(bulletFactory, enemyFactory)
    .MoveSecondOnCollision()
    .CollisionOccurred += (bullet, enemy) => { ... };
```

This chains `.MoveSecondOnCollision()` (which returns `CollisionRelationship<A,B>`) and then subscribes to `.CollisionOccurred` with `+=`. The types are unclear: `bulletFactory` and `enemyFactory` are `Factory<T>` objects, but `AddCollisionRelationship` accepts `IEnumerable<A>` and `IEnumerable<B>`. Does `Factory<T>` implement `IEnumerable<T>`? This is not stated in the Factory section. The parameter names `listA`/`listB` also suggest lists, not factories.

**Suggestion**: Either show that Factory implements IEnumerable, or use `bulletFactory.Instances` in the example.

---

## 9. Collision Shapes -- Missing Shape Properties

**[Completeness]** The collision shapes section shows the class declarations:

```csharp
public class AxisAlignedRectangle : IAttachable, IRenderable, ICollidable { ... }
public class Circle : IAttachable, IRenderable, ICollidable { ... }
public class Polygon : IAttachable, IRenderable, ICollidable { ... }
```

But uses `{ ... }` for all three, showing no properties at all. These are core types that users will interact with constantly. At minimum, show the defining properties:
- `AxisAlignedRectangle`: Width, Height
- `Circle`: Radius
- `Polygon`: Points (or however vertices are defined)

Without these, a reader cannot understand how to create or configure a collision shape.

---

## 10. Screen Section -- Constructor/Initialization Not Shown

**[Completeness]** The Screen class shows a `CustomInitialize` lifecycle method but does not show how a Screen is constructed or what parameters it receives. The usage example shows:

```csharp
_frb.ScreenManager.Start<MainMenuScreen>();
```

But the Screen class shows:

```csharp
public FlatRedBallService Engine { get; }  // injected
```

How is `Engine` injected? Through the constructor? Through `ScreenManager.Start`? The Screen has no constructor shown. FRB1's Screen constructor takes a `contentManagerName` string. The FRB2 Screen apparently receives an `Engine` reference, a `Camera`, and a `ContentManagerService` -- but the wiring is invisible.

**Suggestion**: Show the constructor or explain that `ScreenManager.Start<T>()` handles injection.

---

## 11. LayerManager -- Missing Explanation of Layer-Renderable Binding

**[Completeness]** The LayerManager section explains how layers are registered and ordered, but never explains how a renderable gets assigned to a layer. The Sprite class shows `public Layer Layer { get; set; }` and `IRenderable` has `Layer Layer { get; }`, but there is no example of assigning a sprite to a layer.

A brief example would close this gap:

```csharp
var sprite = new Sprite();
sprite.Layer = frb.LayerManager.Get("Foreground");
```

---

## 12. AudioManager -- Incomplete API

**[Completeness]** The AudioManager section shows `PlaySong`, `StopSong`, and `PlaySoundEffect` but is missing basic operations that any game needs:
- Pause/resume song
- Volume control (global and per-song)
- Is a song currently playing?
- Stop a specific sound effect

The mention of `IAudioBackend` is good but unexplained. What does it abstract? Why would a user swap it?

---

## 13. ContentManagerService -- Scope Confusion

**[Clarity]** The ContentManagerService section says:

> "Loading something globally that is already in screen scope promotes it automatically. Loading something screen-scoped that is already global returns the global instance."

This is confusing. The word "promotes" is ambiguous -- does it mean the content is moved from screen scope to global scope (and therefore won't be unloaded when the screen is destroyed)? Or does it mean the global load returns the same instance?

Also, the section says the Screen has a "screen-scoped" ContentManager but the Screen class definition shows:

```csharp
public ContentManagerService ContentManager { get; }  // screen-scoped
```

while FlatRedBallService has:

```csharp
public ContentManagerService ContentManager { get; }  // global-scoped
```

Both are the same type (`ContentManagerService`) with the same API. How does a screen-scoped instance know it is screen-scoped? Is it a separate instance with internal scoping state? This distinction is critical for understanding content lifecycle but is not explained.

---

## 14. Render Diagnostics -- Jargon: "Batch Break"

**[Clarity]** The Render Diagnostics section uses the term "batch break" without defining it. A developer new to 2D rendering may not know that changing SpriteBatch state (texture, blend mode, shader) forces a new draw call batch, which hurts performance.

Add a one-sentence definition: "A batch break occurs when the rendering pipeline must start a new SpriteBatch.Begin/End pair because adjacent renderables use different batches -- for example, a world-space sprite between two screen-space sprites."

---

## 15. Gum Integration -- Too Sparse to Be Useful

**[Completeness]** The Gum integration section is four bullet points with no code examples. For a system that is a core dependency (pulled in automatically per Package Structure), this is insufficient. At minimum:
- Show how Gum is initialized (does FlatRedBallService do it, or does the user?)
- Show how to add a Gum element to the scene
- Explain the coordinate system relationship (Gum uses screen-space Y-down; FRB2 uses world-space Y-up)
- Mention that Gum has a Forms control library (Button, TextBox, etc.)

The statement "Gum objects use screen-space coordinates; place them on a screen-space Layer" is the most important sentence in the section but is buried as the third bullet. Lead with it.

---

## 16. Tiled Integration -- Too Sparse to Be Useful

**[Completeness]** Same problem as Gum. Four bullet points, no code examples. Show:
- How to load a TMX file
- How to get it rendering on a layer
- How collision shapes are generated from tile properties

The bullet "No entity spawning from object layers" is a design constraint that deserves a rationale sentence (e.g., "to keep level loading deterministic and code-visible").

---

## 17. Update Loop -- Missing Diagram or Flowchart

**[Missing]** The Update Loop section lists six numbered steps. This is a prime candidate for an ASCII flowchart or separation into two clearly labeled phases. The linear numbering obscures the split between Update and Draw phases.

Suggested structure:

```
Update Phase (FlatRedBallService.Update):
  1. Read input
  2. Apply physics
  3. Run collision relationships
  4. Flush async continuations
  5. CustomActivity (screen, then entities)

Draw Phase (FlatRedBallService.Draw):
  6. Walk render list, sorted by Layer then Z
```

Even this simple reformatting would improve readability.

---

## 18. Formatting Inconsistencies

**[Consistency]** Several formatting issues:

- **Horizontal rules**: Used between every section (good), but also between subsections inconsistently. The Collision Shapes section has no rule between it and CollisionRelationship, but both are top-level sections.
- **Code comment style**: Some code blocks use `//` inline comments to explain properties (e.g., `// time since last frame`), others use no comments at all. Pick one approach and apply it consistently.
- **Section headings**: Most use `##` for top-level, `###` for sub-concepts. But `IAttachable` is `###` under Entity, while `IRenderable` and `IRenderBatch` are `###` under Rendering System. The hierarchy is inconsistent -- `IAttachable` is conceptually at the same level as `IRenderable`.
- **Backtick usage**: Some types are backticked in prose (`` `ICollidable` ``), others are not ("Entity implements ICollidable"). Apply backticks consistently to all type names and member names in prose.

---

## 19. Missing Glossary of Terms

**[Missing]** The document uses several terms that are never defined:
- **ACHX** -- Animation Chain XML format. Mentioned once in the Sprite section ("ACHX format, ported from FRB") with no explanation of what it is or how to create one.
- **TMX** -- Tiled Map XML. Mentioned in Tiled Integration. Readers who haven't used Tiled will not know this.
- **XNB** -- MonoGame content pipeline binary format. Mentioned in ContentManagerService. Not explained.
- **Batch group** -- Used in the Draw step description. Not defined.
- **Screen-space vs world-space** -- Used throughout but defined only implicitly via the Layer.IsScreenSpace property and a brief Camera note.

A glossary section (or inline definitions on first use) would help.

---

## 20. No "Getting Started" or "Minimal Example"

**[Missing]** The document has code snippets scattered across sections but no single end-to-end example. A "Minimal Game" section showing a complete, compilable program would anchor the entire document:

```csharp
// Program.cs
using var game = new MyGame();
game.Run();

// MyGame.cs
public class MyGame : Game
{
    FlatRedBallService _frb;

    protected override void Initialize()
    {
        _frb = new FlatRedBallService(this);
        _frb.ScreenManager.Start<GameScreen>();
    }

    protected override void Update(GameTime gt) => _frb.Update(gt);
    protected override void Draw(GameTime gt) => _frb.Draw();
}

// GameScreen.cs
public class GameScreen : Screen
{
    Sprite player;

    public override void CustomInitialize()
    {
        player = new Sprite();
        player.Texture = ContentManager.Load<Texture2D>("player");
        // ... attach to layer, set position, etc.
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.InputManager.Keyboard.IsKeyDown(Keys.Right))
            player.VelocityX = 100;
    }
}
```

This would tie together FlatRedBallService, Screen, Sprite, Input, Content, and Layers in one readable block.

---

## 21. Naming Conventions Section -- Incomplete Rules

**[Completeness]** The naming conventions section covers methods, properties, events, interfaces, and structs, but omits:
- **Private fields** -- underscore prefix (`_name`) or no prefix (`name`)?
- **Constants** -- PascalCase or UPPER_SNAKE?
- **Enum values** -- PascalCase?
- **Parameters** -- camelCase?
- **Type parameters** -- single letter (`T`) or descriptive (`TEntity`)?

For a document that says "Consistent naming conventions" is a philosophy pillar, the conventions section should be comprehensive enough to enforce that consistency.

---

## 22. IAttachable -- Rotation Not Included

**[Completeness]** The `IAttachable` interface includes X, Y, Z, AbsoluteX, AbsoluteY, AbsoluteZ, and Destroy, but does not include rotation. The Entity class has `Rotation` and `AbsoluteRotation` properties. If a Sprite (which implements IAttachable) is attached to a rotating entity, does the sprite rotate with it? The interface does not require rotation support, which means some attachables may rotate and others may not. This ambiguity should be documented.

---

## 23. Debug Renderer vs Collision Shape Debug Drawing

**[Clarity]** The document says collision shapes implement `IRenderable` "(for debug drawing)" and separately has a DebugRenderer class. These are two different debug visualization systems:
1. Collision shapes render themselves as IRenderable objects within the normal render pipeline (sorted by layer and Z).
2. DebugRenderer draws on top of everything with no Z sorting.

When should a developer use which? Are collision shapes always visible, or only when a debug flag is set? The relationship between these two systems is unclear.

---

## 24. FRB1 Migration Context Is Missing Throughout

**[Missing]** For a document titled "FlatRedBall 2.0", there is no acknowledgment anywhere that FlatRedBall 1 exists or that this is a rewrite. A developer finding this document needs to know:
- Is this a continuation of FRB1 or a clean break?
- Can FRB1 code be ported? How much will change?
- Which FRB1 concepts are deliberately dropped vs. not-yet-documented?

A "Relationship to FRB1" section (even a few sentences) would set context. Without it, FRB1 users will spend time trying to figure out which omissions are intentional and which are gaps.

---

## 25. Code Blocks -- Mixing API Surface with Implementation Detail

**[Structure]** Some code blocks show the public API surface (method signatures only), while others mix in implementation hints. For example:

The `ICollidable` block shows full method signatures. The Entity block shows property declarations mixed with comments about behavior. The `Sprite` block shows a full class with `IRenderable, IAttachable` in the declaration, properties, and methods -- almost implementation-level detail.

**Suggestion**: Standardize on one format. For an architecture doc, showing the public API surface (interfaces and key classes with properties/methods, no implementation) is the right level. Use prose or a separate "Behavior" paragraph for implementation details like "defaults to WorldSpaceBatch" rather than embedding them in code comments.

---

## Summary by Priority

### High Priority (blocks understanding)
1. **Audience statement** -- readers cannot calibrate expectations
2. **Entity X/Y dual-meaning** -- most likely source of confusion for anyone coming from FRB1 or Unity
3. **FrameTime constructor error** -- test example will not compile
4. **Getting Started example** -- no way to see the full picture
5. **Gum integration completeness** -- core dependency with almost no documentation

### Medium Priority (causes confusion)
6. **Collision shape properties missing** -- readers cannot create shapes
7. **ContentManager scope explanation** -- "promotes" is ambiguous
8. **Screen construction/injection** -- critical wiring is invisible
9. **Batch break definition** -- jargon used without definition
10. **FRB1 migration context** -- readers from FRB1 are likely the primary audience

### Low Priority (polish)
11. **Formatting consistency** -- backticks, heading levels, comment style
12. **Naming conventions completeness** -- missing common cases
13. **Glossary of terms** -- ACHX, TMX, XNB
14. **Update loop diagram** -- readability improvement
15. **Duplicate ICollidable explanation** -- minor consistency fix
