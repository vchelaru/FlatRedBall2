# Phase 2 — NamedObjects

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — see §9 |
| **Depends on** | Phase 1 (loader, model, type map) — landed |
| **Suggested branch** | `804-phase-2-namedobjects` |

---

## 1. The problem

Phase 1 reads a Glue project into data and boots an empty screen. **Nothing appears.** This phase
turns the parsed `NamedObjectSave` entries into real FRB2 objects that exist, are positioned, and
render — the first phase whose output a person can see.

Because Phase 2 also applies `InstructionSaves` (initial values), objects land in the right place at
the right size, not stacked at the origin. That is what makes this phase worth shipping on its own.

---

## 2. Scope

### In scope

1. Construct instances for `SourceType.FlatRedBallType`: `Sprite`, `AxisAlignedRectangle`, `Circle`,
   `Polygon`.
2. Apply `InstructionSaves` as initial property values, via reflection.
3. `AttachToContainer` parenting, including `RelativeX`/`RelativeY`/`RelativeZ` offsets.
4. `ContainedObjects` (nested members) and `IsList` / `PositionedObjectList<T>`.
5. Register constructed objects so they render.

### Out of scope

- `SourceType.File` — needs Phase 4 (assets). A `Sprite` with no texture is still constructed.
- `SourceType.Entity` — needs Phase 6 (inheritance) for full fidelity; nested entities are recorded
  as unbuildable for now.
- `CustomVariables` (Phase 3), states (Phase 7), collision relationships (Phase 9), tile types
  (Phase 10), camera (Phase 13).
- `ShapeCollection` and `Text` — **FRB2 has neither type.** See D12.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | Objects exist | A loaded screen's shapes and sprites are real instances, not just data. | §6.1 |
| F2 | Objects are configured | They have the size, colour, and position the author gave them in Glue. | §6.2 |
| F3 | Objects are attached | A shape authored at an offset inside an entity sits at that offset and follows its parent. | §6.3 |
| F4 | Lists and nesting hold | An object inside a list or inside another object keeps that relationship. | §6.4 |
| F5 | Objects render | Loading a project and running it draws something. | §6.5 |

---

## 4. Proposed resolution

### Construction

A small factory keyed off the Phase 1 `GlueTypeMap`. Only the four mapped types construct; anything
else keeps reporting as unbuildable exactly as Phase 1 does. No new type-string logic — `GlueTypeName`
already parses, and this phase consumes it.

### Property assignment

`InstructionSaves` carry a `Member` name and a raw JSON value. Assignment is reflection over the
target instance's public properties, converting the `JsonElement` to the property's declared type.
Unknown members and unconvertible values become diagnostics, never exceptions — the Phase 1 posture.

### Attachment — the mapping is simpler than it looks

FRB1 gives an attached object both an absolute `X` and a `RelativeX`. **FRB2 has one property**:
`ISpatialAttachable.X` is *already* the offset from `Parent` when attached, and world space when not
(`src/IAttachable.cs`). `AbsoluteX`/`AbsoluteY`/`AbsoluteZ` are the computed world values, read-only.

So both Glue members collapse onto the same FRB2 property, selected by attachment state:

Both Glue members map to FRB2's `X`/`Y`/`Z` in every case — see G21 for why the attached-plus-absolute
case is not the exception it looks like.

Assignment order does not matter: FRB2 stores `X` and computes `AbsoluteX` on read, so setting
position before or after `Parent` gives the same result. FRB1 needs care here; FRB2 does not.

### Registration

`Entity.Add(IAttachable child, Layer?)` for objects owned by an entity — it parents and registers in
one call. `Screen.Add(IRenderable renderable, Layer?)` for screen-level objects.

---

## 5. Gotchas

Carried forward from Phase 1 where still live; new ones numbered from G21.

### G21 — An absolute position on an attached object *is* the offset

Glue can author an `X` instruction on an object that also has `AttachToContainer = true`. It is
tempting to treat that as ambiguous, or to assume attachment overwrites it. Both readings are wrong.

Glue's code generator picks between the two **at assignment time**. For any member with a relative
counterpart, it emits a branch (`CustomVariableCodeGenerator.cs:1536`, `:1551`, `:1587`):

```csharp
if (obj.Parent == null) obj.X = value; else obj.RelativeX = value;
```

FRB2's `X` **is** that branch — an offset from `Parent` when one is set, world space when not
(`src/IAttachable.cs`). So **both Glue members map to the same FRB2 property, and nothing is
dropped.**

*Corrected 2026-08-02:* this gotcha originally credited `CopyAbsoluteToRelative()`
(`NamedObjectSaveCodeGenerator.cs:1129`) for the behaviour. That call is real, but it runs at attach
time inside `PostInitialize` — **before** any authored value is assigned — so it copies zeros and
explains nothing. The conclusion was right for the wrong reason; the real mechanism above is a
stronger justification, because it is FRB2's own semantics expressed in FRB1's generated C#.

**How we tackle it.** `X` and `RelativeX` both resolve to `X`, regardless of attachment.

This one was originally decided the other way — drop the value and warn — and that was a real bug,
not a cosmetic one. Every attached object with an authored absolute position would have been
misplaced, including DoorsDemo's player collision box (authored `Y: 11`, which raises the box to
stand on the sprite) and all four Beefball score labels. Caught by checking Glue's codegen rather
than reasoning from the property names. Covered by a regression test on the real fixture.

### G22 — A `Sprite` with no texture is not a failure

`SourceType.File` sprites get their texture in Phase 4. Constructing a textureless `Sprite` now is
correct: it exists, is positioned, and will draw once Phase 4 supplies the texture. It must not be
reported as an error, or Phase 2's diagnostics drown in noise for every art-bearing project.

---

### G23 — FRB2 shapes are invisible by default; Glue shapes are not

`AARect`, `Circle` and `Polygon` default `IsVisible = false` because in FRB2 they are primarily
collision volumes. `Sprite` defaults `true`. A shape authored in Glue is meant to be seen.

**How we tackle it.** Shapes are made visible on construction, before instructions are applied, so an
explicit `Visible` instruction still wins. Without this the phase would "work" and still draw
nothing - the exact failure it exists to fix. Note the rename: Glue's `Visible` is FRB2's `IsVisible`.

### G24 - `IsList` is computed by Glue and never written to disk

FRB1 declares `IsList` as `[JsonIgnore]` and derives it from
`SourceType == FlatRedBallType && SourceClassType is PositionedObjectList<T>`
(`NamedObjectSave.cs:609-619`). **It never appears in a `.glsj`/`.glej`.** Measured across all three
vendored fixtures: 36 `NamedObjects`, **zero** with `IsList` set, seven with `ContainedObjects`.

A mirror that binds `IsList` to JSON therefore reads `false` for every list in every real project,
and any code branching on it is dead on real data while passing tests built from hand-written JSON.
That is exactly what happened here - see section 9.

**How we tackle it.** Compute it, the way FRB1 does. More generally: **a computed or bag-backed FRB1
member must not be mirrored as a JSON-bound property**, and a test that hand-writes such a value is
testing fiction. Phase 1's G4 is the same lesson from the other direction.

### G25 - Nesting is not list-only

Glue nests `ContainedObjects` under any object, not just lists. Beefball's six arena walls are
children of a `ShapeCollection` - a type FRB2 has no equivalent for (D12). Recursing only into lists
drops them, and an unbuildable container silently takes its perfectly buildable children with it.
FRB1's generator recurses unconditionally (`NamedObjectSaveCodeGenerator.cs:268-271`).

**How we tackle it.** Recurse into `ContainedObjects` for every object, registering children on the
same owner the parent would have used. A container this build cannot construct is not a reason to
discard what is inside it.

### G26 - Trim and AOT warnings only appear at *publish*

`dotnet build` on the engine can be completely clean while the same code fails under
`PublishTrimmed` or `PublishAot`. The engine sets `IsAotCompatible=true`, but the dataflow warnings
that matter (`IL2067` on `Activator.CreateInstance`, `IL2036` on an unresolvable
`DynamicDependency`) surface only when a *consuming app* is published.

**How we tackle it.** Any change that reflects over a type must be checked with a real trimmed
publish, not a build. Two consequences already bitten by this are recorded in section 9:

- **Rooting properties does not root the constructor.** `PublicProperties` and
  `PublicParameterlessConstructor` are separate member kinds. Prefer a `Func<object>` factory that
  directly `new`s the type over `Activator.CreateInstance` - then there is nothing to root.
- **`DynamicDependency` on a type from another assembly may not resolve.** The XNA `Color` type lives
  in `MonoGame.Framework` on desktop and an `nkast.Xna.Framework` assembly on web, so no single
  attribute works on both targets. Prefer data over reflection there.

## 6. Tasks

Test-first throughout. Each group is roughly one commit.

### 6.1 — Construction

- [x] Failing test: a `NamedObjectSave` with `SourceClassType` `FlatRedBall.Math.Geometry.Circle`
      produces a real `Circle`.
- [x] Failing test: all four mapped types construct; an unmapped type produces no instance and one
      warning naming the object and its type.
- [x] Implement the object factory over `GlueTypeMap`.

### 6.2 — Property assignment

- [x] Failing test: an `InstructionSaves` entry `Radius = 16.0` lands as `Circle.Radius == 16f`.
- [x] Failing test: assignment covers float, int, bool, and string-named colour members.
- [x] Failing test: an unknown member name warns and does not throw.
- [x] Failing test: a value that cannot convert to the property type warns and leaves the default.
- [x] Implement reflection-based assignment, reusing any existing engine helper rather than writing a
      parallel one.

### 6.3 — Attachment

- [x] Failing test: an object with `AttachToContainer` gets `Parent` set to its owning entity.
- [x] Failing test: `RelativeX`/`RelativeY` land on FRB2's `X`/`Y`, and `AbsoluteX` reflects the
      parent's position plus the offset.
- [x] Failing test: attachment is applied before offsets, so no frame renders at the wrong position.
- [x] Failing test: an absolute `X` on an attached object is ignored with a warning (G21).

### 6.4 — Nesting and lists

- [x] Failing test: `ContainedObjects` are constructed and owned by their container.
- [x] Failing test: an `IsList` object produces a list whose members are the contained objects.
- [x] Failing test: a nested `SourceType.Entity` is recorded as deferred, not errored (Phase 6).

### 6.5 — Wire into the loaded screen

- [x] Failing test: a `GlueScreen` built from a `ScreenSave` has its objects constructed and
      registered.
- [x] Failing test: a `GlueEntity` built from an `EntitySave` likewise.
- [x] Failing test: the Beefball fixture builds its shapes with zero errors.
- [x] **Correction:** DoorsDemo's unmapped count stays at 13, and that is right. This phase added no
      new rows to `GlueTypeMap` — it made the already-mapped types actually construct. The count
      drops when Phases 9/10/13 claim tile collision, relationships, and the camera.
- [x] Vendor the Beefball fixture — shapes-only and tile-free, so it is the first project that can
      visibly work end to end.

### 6.6 — Wrap-up

- [x] XML docs on new public types.
- [x] Update this document's checkboxes and `plan/plan.md`.
- [x] Record what Phase 3 must pick up (§9).

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D12 | `ShapeCollection` and `Text` have no FRB2 equivalent | **Report as unbuildable and move on.** Adding two engine types to satisfy a loader is the tail wagging the dog; do it when a real project needs them, as its own scoped change. Lower priority than it looks: most real `ShapeCollection` usage in Glue projects is a shape list attached to an `ICollidable` (Phase 9) or a `TileShapeCollection` (Phase 10), both already covered. Only an explicit standalone `ShapeCollection` added directly to a screen/entity hits this gap. |
| D13 | Where does object construction live? | **A separate `GlueObjectBuilder`, not on `GlueScreen`.** Screens and entities both need it, and keeping it separate keeps it testable without a running engine. |
| D14 | Reflection vs. a hand-written property table | **Reflection**, matching the epic's data-driven ground rule. A hand-written table for four types would be faster but would have to grow for every type every later phase adds. |

---

## 8. Definition of done

- [x] `dotnet build` clean, `dotnet test` green (1293).
- [x] **A real `PublishTrimmed` emits no IL warnings from `src/Glue`.** A clean build does not prove
      this and never did — see G26. This belongs in the bar for every phase that reflects.
- [x] Beefball's `PlayerBall` builds both its circles, at their authored radii, attached to the entity.
- [x] Beefball's `GameScreen` builds all six arena walls, nested inside an unbuildable container.
- [x] DoorsDemo loads with zero errors. Its unmapped count stays at 13 — correct, since this phase
      added no rows to the type map.
- [x] Every gotcha in §5 is covered by a test or explicitly deferred.
- [x] Someone has watched it draw — Beefball's six arena walls render, captured from the back
      buffer of a real device. See Phase 1 §9.

---

## 9. What landed

Eight commits. 33 new tests, full suite **1293 green**, zero build warnings from `src/Glue`, and
**verified clean under a real `PublishTrimmed`** (see G26 for why a build alone proves nothing).

| Piece | File |
|---|---|
| Construct + configure + attach one object | `src/Glue/GlueObjectBuilder.cs` |
| Glue name to a construction factory | `src/Glue/GlueTypeMap.cs` |
| JSON value to CLR property type | `src/Glue/GlueValueConverter.cs` |
| Build a whole element, including nesting | `src/Glue/GlueElementBuilder.cs` |
| Screens/entities that build themselves | `src/Glue/GlueScreen.cs` |

**The payoff:** Beefball's `PlayerBall` loads from `.glej` and becomes two `Circle`s at radius 16,
attached and visible; its `GameScreen` builds the six arena walls. DoorsDemo's player gets its sprite
and a collision box at the authored offset. No hand-written C# in that path.

### Five defects found after the code "worked"

Every one of these was live with a green test suite. Four were invisible to tests because the tests
used data Glue does not actually produce; one was invisible because it only appears at publish.

| # | Defect | Why the suite missed it |
|---|---|---|
| 1 | **Every object failed to build under trimming/AOT.** Rooting properties does not root the constructor, so `Activator.CreateInstance` threw and every screen loaded empty. | `dotnet build` is clean; `IL2067` only appears when a consuming app is published (G26). |
| 2 | **Nested objects were dropped, so Beefball's arena rendered nothing.** Recursion only happened for lists — and `IsList` is never written to disk, so that branch was dead on real data. | Both list tests hand-wrote `"IsList": true`, a value Glue never emits (G24). |
| 3 | **Authored positions on attached objects were discarded**, misplacing DoorsDemo's collision box and all four Beefball score labels. | Tests asserted the wrong behaviour, because the semantics were reasoned from property names instead of read out of Glue's generator (G21). |
| 4 | **The colour table was empty under trimming**, so every named colour fell back to the engine default. | Same as 1 — publish-only, and `DynamicDependency` could not resolve `Color` across both backends. |
| 5 | **`IncludeInICollidable` was parsed and ignored**, so a shape excluded from collision was silently collidable. | Nothing asserted it. |

Defect 3 came from a research pass that enumerated every instruction in the fixtures and noticed
values being discarded. Defects 1, 2 and 4 came from an adversarial review that reproduced each with
running code. **The suite being green was not evidence of correctness at any point in this phase** —
that is the durable lesson, and it applies to every phase after this one.

### Corrections to this document

- Section 6.5 claimed DoorsDemo's unmapped count would drop from 13. It does not, and should not:
  this phase added no rows to the type map. The count falls when Phases 9/10/13 land.
- Section 4 said attachment must happen before offsets are assigned. Untrue in FRB2 — `X` is stored
  and `AbsoluteX` computed on read, so order is irrelevant.
- G21 was originally decided the opposite way. See G21.

### What Phase 3 picks up

- `CustomVariables` and their `DefaultValue`s, including tunnelled variables
  (`SourceObject`/`SourceObjectProperty`) that forward to a member of a `NamedObject` this phase
  built. Objects are addressable by Glue instance name via `Objects`, which is the hook.
- `CustomVariable.Type` is bag-backed (Phase 1 G4) — the only place a variable's declared type is
  recorded. `GlueValueConverter` already converts by target type and should be reusable.
- **Heed G24 when mirroring anything else.** Several `CustomVariable` members are computed or
  bag-backed in FRB1; binding them to JSON will read empty on real projects while synthetic tests
  pass.
- Decide whether `Objects` stays an `IReadOnlyDictionary<string, object>` or becomes the indexer
  Phase 14 sketches (`entity["Health"]`). Deliberately not pre-decided here.

### A sixth defect, found while planning Phases 3–14

**`Instantiate` and `AddToManagers` are parsed and then never consulted** — neither is referenced
anywhere in `src/Glue/` outside the model POCO. In FRB1:

- `Instantiate == false` means *declare the field, do not construct it* — something else will
  (`NamedObjectSaveCodeGenerator.cs:2016`, `:2084`, `:2183`).
- `AddToManagers == false` means *construct it, do not register it* (`:527`).

Zero occurrences across the three vendored fixtures, which is exactly why nothing caught it; five
files in `Tests/TestProjectDesktopNet6/` use them. **This is G24 for the third time** — a flag that is
dead on the current fixture set and live on real projects.

Three more `NamedObjectSave` members are missing from the mirror entirely and gate the same code
paths in FRB1: `IsDisabled` (a real field, `NamedObjectSave.cs:527`), `IsFullyDefined` (computed —
must **not** be JSON-bound), and `SourceClassGenericType`, which holds a list's element type and is
present on every list in the live fixtures. Phase 6 §6.0 owns the fix and the wider model audit.

### Known gaps left open

- `ShapeCollection` and `Text` are still unbuildable (D12) — FRB2 has neither type.
- `Sprite.CurrentChainName` has no FRB2 property; the equivalent is the method `PlayAnimation(string)`,
  which property reflection cannot reach. Phase 4 needs a member-to-action hook, not just a setter.
- `Sprite.AnimationChains` names an asset, so it needs Phase 4.
- **Polygon geometry is decodable and this document previously said it was not.** Points are an
  ordinary `InstructionSaves` entry — `Member: "Points"`, `Type: "List<Vector2>"`, value an array of
  `"x, y"` **strings** (`Tests/TestProjectDesktopNet6/.../Entities/PolygonEntity.glej`). FRB1 reads
  it from exactly there (`NamedObjectSaveCodeGenerator.cs:2231`) and falls back to a hardcoded
  4-point shape when absent (`:2254-2258`) — so a Glue polygon with no authored points is *not*
  invisible in FRB1. `GlueValueConverter` handles no array shape, which is the real reason it fails.
  The warning text has been corrected; Phase 3 §6.4 owns the decoder, shared with `List<Vector2>`
  CustomVariables.
- ~~Nobody has watched any of this draw.~~ Done — Beefball's arena renders correctly. The
  assumption that it needed a human at a keyboard was wrong; see Phase 1 §9.
