# Phase 9 — Collision Relationships

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — see §10. Damage model, soft collision and always-colliding deliberately out. |
| **Depends on** | Phase 2 (objects exist), Phase 6 (entity instances exist to collide), Phase 10 for the tile variants |
| **Blocks** | Nothing — but nothing in a real project *behaves* until this lands |
| **Suggested branch** | `804-phase-9-collision-relationships` |

---

## 1. The problem

A Glue project's gameplay lives in its collision relationships. Beefball is a puck-and-paddle game
whose entire rule set is six relationships; DoorsDemo is a platformer whose floor, clouds, and door
triggers are four. Phase 2 makes the shapes exist. **This phase makes them interact.**

Collision relationships have **no save class of their own.** They are `NamedObjectSave` entries whose
`SourceClassType` matches one of seventeen patterns, with every setting in the object's `Properties`
bag. That makes this phase mostly a decoding problem, and the decoding has more traps than any phase
so far.

---

## 2. Scope

### In scope

1. Recognise a `NamedObjectSave` as a collision relationship.
2. Determine its **variant** — list-vs-list, list-vs-single, list-vs-tiles, self, always-colliding.
3. Decode the settings bag: response kind, masses, elasticity, sub-collisions, active flag.
4. Register the equivalent FRB2 relationship in the right order.
5. Report, and skip, every relationship this build cannot express.

### Out of scope

- The damage model (`IsDealDamageChecked` and friends) — FRB2 has no `IDamageable`. G96.
- `MoveSoftCollision`, `CollisionLimit`, per-relationship `IsActive`, manual physics, `FrameSkip` —
  no FRB2 equivalent. G96.
- `ShapeCollection` as a collidable side — FRB2 has no such type (D12, still open).
- Stacking collision.
- `Events` wiring beyond parsing. Naming a C# method that does not exist in a data-driven model is
  Phase 3/14 territory; this phase parses the `Events` array and reports it.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | Relationships are recognised | The loader knows which objects are rules rather than things. | §6.1 |
| F2 | The variant is derived, not read | A project saved by an older Glue still gets the right relationship. | §6.2 |
| F3 | Physics responses apply | The puck bounces off the paddle with the authored masses. | §6.3 |
| F4 | Ordering is guaranteed | A relationship never references an object that has not been built. | §6.4 |
| F5 | Bad data is reported, not fatal | A relationship naming a missing object warns and is skipped. | §6.5 |

**Progress metric.** This phase drives the unmapped-type warning count down by **3 in DoorsDemo
(13 → 10)** and **6 in Beefball (15 → 9)**.

---

## 4. Proposed resolution

### Recognition

Port `NamedObjectSaveExtensionMethods.IsCollisionRelationship`
(`FRBDK/Glue/Glue/SaveClasses/NamedObjectSaveExtensionMethods.cs:792-816`) — one exact match plus
sixteen `StartsWith` prefixes. The full list is in G90.

### Variant selection — derive it, never read `SourceClassType`

This is the phase's central design decision, and it is forced by G91: **`SourceClassType` on a
relationship is a derived cache that Glue rewrites on load, and real sample files contain stale
values.** Mirror `AssetTypeInfoManager.GetCollisionRelationshipSourceClassType`
(`FRBDK/Glue/OfficialPlugins/CollisionPlugin/Managers/AssetTypeInfoManager.cs:121-285`) instead:
resolve `FirstCollisionName` and `SecondCollisionName` against the containing element's
`NamedObjects`, and pick the variant from what those objects *are* plus `CollisionType`.

| First | Second | FRB2 call |
|---|---|---|
| list | same list | `AddSelfCollisionRelationship<A>(list)` |
| list | list | `AddCollisionRelationship<A, B>(listA, listB)` |
| single | list | `AddCollisionRelationship<A, B>(single, list)` |
| list | `TileShapeCollection` | `AddCollisionRelationship<A>(list, tiles)` |
| list | absent | *always-colliding* — no FRB2 equivalent (G96) |
| single | single | no FRB2 overload (G96) |

### Response mapping

| Glue `CollisionType` | Value | FRB2 |
|---|---|---|
| `NoPhysics` | 0 | call no response method — event only |
| `MoveCollision` | 1 | `.MoveBothOnCollision(first, second)` |
| `BounceCollision` | 2 | `.BounceOnCollision(first, second, elasticity)` |
| `PlatformerSolidCollision` | 3 | `SlopeMode = PlatformerFloor` + `.BounceFirstOnCollision(0f)` |
| `PlatformerCloudCollision` | 4 | `OneWayDirection = Up`, `CanDropThrough = true` |
| `DelegateCollision` | 5 | event only — the function was user C# |
| `StackingCollision` | 6 | no equivalent |
| `MoveSoftCollision` | 7 | no equivalent |

Masses carry across unchanged: FRB1's `amountToMoveThis = otherMass / (thisMass + otherMass)`
(`AxisAlignedRectangle.cs:857-861`) is the same formula as FRB2's
`CollisionDispatcher.ComputeSeparationOffset` (`src/Collision/CollisionDispatcher.cs:28-34`). Mass 0
means "this side takes the full separation" on both sides of the port. No inversion, no scaling.

### Ordering

Build in two passes: every non-relationship object first, then relationships. FRB1 does exactly this
and says why — `NamedObjectSaveCodeGenerator.cs:299-310`, *"Relationships need to be assigned after
all other objects"*. See G92 for why the current single pass appears to work and must not be trusted.

---

## 5. Gotchas

### G90 — Recognition is seventeen patterns, and three need a trailing `<`

`IsCollisionRelationship` is one `==` plus sixteen `StartsWith`. The five `Delegate*` entries and the
bare `CollisionRelationship<` entry match **with the angle bracket included**, so a type named
`DelegateCollisionRelationshipHelper` correctly fails to match. Port the trailing `<` — dropping it
looks harmless and silently widens the match.

Note `PositionedObjectVsShapeCollection` has **no** `Relationship` suffix; the runtime class really
is named that way. And there is a near-duplicate copy of this method under
`GameCommunicationPlugin/GlueControl/Embedded/Models/` — do not assume the two are identical.

### G91 — `SourceClassType` is a derived cache, and real files disagree with it · **Blocker**

`CollisionRelationshipViewModelController.TryFixSourceClassType`
(`Controllers/CollisionRelationshipViewModelController.cs:570-584`) recomputes `SourceClassType`
from the `Properties` bag every time Glue loads or selects the object. A project last saved by an
older Glue keeps whatever was written then.

`SourceFile` is worse. In `Samples/Platformer/LadderDemo/.../GameScreen.glsj`, `PlayerVsLadderCollision`
has:

```
SourceClassType : ...CollidableListVsTileShapeCollectionRelationship<Entities.Player>
SourceFile      : ...DelegateListVsSingleRelationship<Entities.Player, ...TileShapeCollection>
```

Two different relationship classes on one object. MovingPlatformDemo has the same divergence.

**How we tackle it.** Derive the variant from `(FirstCollisionName, SecondCollisionName,
CollisionType)` and the container's `NamedObjects`. Never switch on the type string; never read
`SourceFile` for a relationship. Add a test that pins the LadderDemo divergence so nobody
"simplifies" this back into a string match.

### G92 — Ordering works by luck today

Relationships appear last in file order in every sample, so `GlueElementBuilder.Build`'s single
file-order pass (`src/Glue/GlueScreen.cs:64`) happens to see its referents already built. Nothing in
the format guarantees it, and FRB1 explicitly does not rely on it.

**How we tackle it.** Explicit two-pass split. Add a test with a hand-authored fixture that declares
the relationship *first* — the case no real sample covers and the one that will eventually appear.

### G93 — Absent keys do not mean zero, and three of them invert behaviour

Newtonsoft omits defaults, so the settings bag is mostly holes. Getting any of these wrong is silent
and severe:

| Key | Absent means | Getting it wrong |
|---|---|---|
| `CollisionType` | `0` = `NoPhysics`, event only | Defaulting to a physics response makes DoorsDemo's `PlayerVsDoor` shove the player |
| `FirstCollisionMass` | **`1.0f`**, not `0f` | `ComputeSeparationOffset` returns `Vector2.Zero` when both masses are 0 — "bounce off wall" becomes "pass through wall" |
| `SecondCollisionMass` | **`1.0f`** | as above |
| `CollisionElasticity` | **`1.0f`** | dead bounces |
| `SoftCollisionCoefficient` | **`1.0f`** | n/a (unsupported) |
| `IsCollisionActive` | **`true`** | reading it as `GetValue<bool>` disables every relationship in every project that omits it |
| `IsAutomaticallyApplyPhysicsChecked` | **`true`** | same shape |

FRB1 tests key *presence* for the last two rather than defaulting
(`CollisionCodeGenerator.cs:113-122`). `GetValue<bool>` returning `default` is exactly the wrong
behaviour here. This is Phase 1's G3 in a new place — and the fix is the same: reproduce the
defaults deliberately, and test against JSON that *omits* the key.

### G94 — `CollisionType` is two different enums with disagreeing ordinals

The persisted value is the **Glue plugin** enum
(`OfficialPlugins/CollisionPlugin/ViewModels/CollisionRelationshipViewModel.cs:21-37`, eight members).
The FRB1 **runtime** enum (`Math/Collision/CollisionRelationship.cs:22-28`) has four. They collide
confusingly: `BounceCollision` is 2 in both, but `MoveSoftCollision` is 7 in one and 3 in the other.

**How we tackle it.** Mirror the plugin enum, with explicit numeric values and a pinning test, per
Phase 1's G11. Name it so nobody reaches for the wrong one.

### G95 — Sub-collision names a member inside the entity, and FRB2 needs a delegate

`FirstSubCollisionSelectedItem: "TalkCollision"` means "collide using the entity's `TalkCollision`
shape, not its whole self". FRB1 generates `item => item.TalkCollision`
(`CollisionCodeGenerator.cs:232`). FRB2 wants `WithFirstShape(Func<A, ICollidable>)`.

The loader has a **string**, and a `GlueEntity`'s children live in `Objects`, not as properties. So
the bridge is `WithFirstShape(a => (ICollidable)((GlueEntity)a).Objects["TalkCollision"])`. That is
untested territory.

`"<Entire Object>"` is a sentinel meaning *no* sub-collision and must be nulled out.

**How we tackle it.** Implement the `Objects` lookup, and vendor `DialogBoxDemo` — it is the **only**
project in the entire FRB1 tree with a non-sentinel sub-collision.

### G96 — Thirteen FRB1 features have no FRB2 equivalent

Verified absent from `src/Collision/` and `src/Screen.cs`:

| # | Missing | Consequence |
|---|---|---|
| 1 | `SetMoveSoftCollision` / `SoftCollisionCoefficient` | no soft separation |
| 2 | `CollisionLimit` (`All`/`First`/`Closest`) | FRB2 always does `All` |
| 3 | relationship `IsActive` | `CollisionSystem.Remove` is internal; "inactive" can only mean "never registered", which game code cannot undo |
| 4 | `ArePhysicsAppliedAutomatically = false` | no way to get the event without the response |
| 5 | `FrameSkip` | — |
| 6 | relationship `Name` | `screen.Objects["PlayerVsDoor"]` cannot resolve to a relationship |
| 7 | `AlwaysCollidingListCollisionRelationship<T>` | FRB2 relationships are inherently two-sided |
| 8 | `ShapeCollection` as a side | Beefball's `Walls` has no counterpart (D12) |
| 9 | single-vs-single overload | wrap one side in a one-element list, or add the overload |
| 10 | partitioning triple | FRB2 partitions per-`Factory`, not per-relationship |
| 11 | damage model | `IsDealDamageChecked` and friends have nowhere to go |
| 12 | `PlatformerValuesStatic[...]` swap on collision | needs Phase 12 |
| 13 | stacking | — |

**How we tackle it.** Each is a `Warning` naming the feature and the relationship, never a silent
drop. #6 and #9 are small enough to be worth fixing engine-side; the rest are deliberate gaps.
Decisions D90–D92 cover the three worth arguing about.

### G97 — Events live on the element, not on the relationship

The relationship carries no event data. The containing element has a top-level `Events` array of
`{ EventName, SourceObject, SourceObjectEvent }`, where `SourceObject` is the relationship's
`InstanceName` and `SourceObjectEvent` is always the literal `"CollisionOccurred"`. Beefball's
`GameScreen.glsj:1072-1079` is the example.

**FRB2's `GlueElement` model does not parse `Events` at all.** Add it here. Wiring an event to
anything is Phase 3/14 — a data-driven model has no C# method to bind to — but parsing and reporting
it belongs to whoever first needs to know the array exists.

### G98 — Five of the eleven variants have no test data anywhere

Exhaustive sweep of every `.glsj`/`.glej` in FRB1. Present: `ListVsListRelationship`,
`ListVsShapeCollectionRelationship`, `CollidableListVsTileShapeCollectionRelationship`,
`DelegateListVsSingleRelationship`, `DelegateListVsListRelationship`.

**Absent from all sample data:** `AlwaysCollidingListCollisionRelationship`, `PositionedObjectVs*`,
`ListVsPositionedObjectRelationship`, `CollidableVsTileShapeCollectionRelationship`,
`DelegateCollisionRelationship(Base)`, `PositionedObjectVsShapeCollection`.

**How we tackle it.** Hand-author fixtures for the variants that map to something FRB2 can express,
and record the rest as code-verified-but-not-data-verified. Do not claim coverage the corpus does not
support.

### G99 — Two more skip conditions, one of which FRB2 cannot even see

FRB1 suppresses generation when `IsDisabled`, `SetByDerived`, `!IsFullyDefined`, or `!Instantiate`
(`NamedObjectSaveCodeGenerator.cs:1677-1707`). **`IsDisabled` is not on FRB2's `NamedObjectSave`
model at all**, and `Instantiate` is parsed but never consulted — see the Phase 2 follow-up in
§6.0. Both must be fixed before this phase's skip logic can be correct.

Also: an empty `FirstCollisionName` makes FRB1 generate **nothing at all**, silently
(`CollisionCodeGenerator.cs:40-44`), and a null second-side TileShapeCollection is wrapped in an
`if (second != null)` guard (`:192`). Port Glue's own four editor-side validations from
`Errors/CollisionRelationshipErrorViewModel.cs:34-71` as diagnostics: empty first collidable; empty
second when first is not a list; first names a missing object; second names a missing object.

---

## 6. Tasks

Test-first throughout.

### 6.0 — Prerequisites carried in from Phase 2

- [ ] Add `IsDisabled` to `src/Glue/Model/NamedObjectSave.cs` (real field, `[DefaultValue(false)]`,
      `NamedObjectSave.cs:527`) and honour it in `GlueObjectBuilder`.
- [ ] Honour `Instantiate` and `AddToManagers`, which are currently parsed and ignored.
- [ ] Add `IsFullyDefined` as a **computed** member (never JSON-bound — G24), mirroring
      `NamedObjectSave.cs:624`.

### 6.1 — Recognition

- [ ] Failing test: each of the seventeen patterns is recognised; a near-miss without the trailing
      `<` is not (G90).
- [ ] Failing test: a `Sprite` is not a relationship.

### 6.2 — Variant derivation

- [ ] Failing test: the variant is derived from the two instance names, not from `SourceClassType` —
      pinned against LadderDemo's stale-`SourceFile` object (G91).
- [ ] Failing test: `FirstCollisionName == SecondCollisionName` selects self-collision, from
      Beefball's `PlayerBallVsPlayerBall`.
- [ ] Failing test: absent `SecondCollisionName` classifies as always-colliding and warns (G96 #7).
- [ ] Implement variant derivation over the container's `NamedObjects`.

### 6.3 — Settings decoding

- [ ] Failing test: absent `CollisionType` decodes as `NoPhysics`, from DoorsDemo's `PlayerVsDoor`
      (G93).
- [ ] Failing test: absent mass/elasticity keys decode as `1.0f`, not `0f` (G93).
- [ ] Failing test: absent `IsCollisionActive` decodes as `true` (G93).
- [ ] Failing test: the plugin `CollisionType` enum values are pinned (G94).
- [ ] Failing test: `"<Entire Object>"` decodes as no sub-collision (G95).

### 6.4 — Registration and ordering

- [ ] Failing test: a relationship declared *before* its referents still builds (G92).
- [ ] Failing test: Beefball's `PlayerBallVsPuck` registers a bounce with masses 1.0 / 0.3 and
      elasticity 1.0.
- [ ] Failing test: DoorsDemo's `PlayerVsSolidCollision` maps to the platformer-solid shape.
- [ ] Implement the two-pass build and the FRB2 registration calls.

### 6.5 — Diagnostics

- [ ] Failing test: each of Glue's four validation failures produces a `Warning` and skips (G99).
- [ ] Failing test: each unsupported feature in G96 warns by name.
- [ ] Failing test: the `Events` array parses, and a relationship's event is reported (G97).
- [ ] Failing test: DoorsDemo's unmapped count drops 13 → 10; Beefball's 15 → 9.

### 6.6 — Fixtures and wrap-up

- [ ] Vendor `DialogBoxDemo` — the only sub-collision fixture in existence (G95).
- [ ] Hand-author fixtures for the expressible variants with no sample data (G98).
- [ ] Add the three `FileVersion` constants to `src/Glue/GlueVersions.cs`:
      `SupportsNamedSubcollisions = 8`, `CollisionRelationshipManualPhysics = 23`,
      `CollisionRelationshipsSupportMoveSoft = 25`.
- [ ] XML docs; update this document and `plan/plan.md`.

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D90 | Add a `Name` to FRB2's `CollisionRelationship`? | **Yes.** Small, and without it a relationship cannot appear in `Objects`, which breaks the addressing model every other phase relies on. |
| D91 | Add a single-vs-single `AddCollisionRelationship` overload? | **Yes.** The alternative is wrapping one side in a one-element array at every call site, which is worse at the API level, not just here. |
| D92 | Add per-relationship `IsActive`? | **Defer.** No sample sets it false. Registering-or-not is a genuinely different semantic, so warn rather than pretend. Revisit if a real project needs it. |
| D93 | Port the damage model? | **No.** It is a gameplay framework, not a loader concern, and pulling `IDamageable` into FRB2 to satisfy a JSON reader is the tail wagging the dog. Warn and move on. |

---

## 8. Definition of done

- [ ] `dotnet build` clean; `dotnet test` green.
- [ ] A real `PublishTrimmed` emits no IL warnings from `src/Glue` (G26 — every phase that reflects).
- [ ] Beefball's six relationships register with the authored masses and elasticities.
- [ ] DoorsDemo's four register, with solid and cloud mapped distinctly.
- [ ] The unmapped count drops 13 → 10 (DoorsDemo) and 15 → 9 (Beefball).
- [ ] Every G96 gap warns by name; none is silently dropped.
- [ ] Every gotcha in §5 is covered by a test or explicitly deferred.
- [ ] The variant is derived from instance names — verified by a test that would fail under a
      string match on `SourceClassType`.

---

## 10. What landed

10 new tests, full suite **1366 green**. DoorsDemo's unmapped-type count fell from 18 to **6** across
this phase and Phase 10 together.

| Piece | File |
|---|---|
| Recognition, variant selection, registration | `src/Glue/GlueCollisionBuilder.cs` |
| Settings decoding with Glue's own defaults | `src/Glue/GlueCollisionSettings.cs` |

**The variant is derived, never read from the type string** — G91 held up, and the implementation
switches on what the two named objects actually *are* plus `CollisionType`. A project last saved by
an older Glue carries a stale `SourceClassType`, and LadderDemo has one whose `SourceFile` names a
different relationship class again.

The three inverted defaults (G93) are each covered by a test asserting against a bag that omits the
key: absent `CollisionType` is event-only, absent masses and elasticity are `1` rather than `0`, and
an absent active flag means active. Any of them read the obvious way is silent and severe.

### Reported rather than silently dropped

Each of these warns by name, because the alternative is a relationship that looks configured and
does nothing:

- **Always-colliding** (no second side) — FRB2's relationship is inherently two-sided.
- **`IsCollisionActive: false`** — FRB2 has no per-relationship enable flag, so "inactive" can only
  mean "never registered", which game code cannot switch back on. Worth saying out loud rather than
  quietly matching the first half of the behaviour.
- **Stacking, soft and delegate collision** — no FRB2 equivalent; the relationship still reports
  overlaps but applies no physics.
- **A side this build did not create** — Glue's own four editor-side validations, ported so a
  malformed relationship reports at load instead of failing to compile as it would in FRB1.

### Deliberately out of scope

The damage model (`IsDealDamageChecked` and friends) is a gameplay framework rather than a loader
concern — D93 stands. `CollisionLimit`, `FrameSkip` and manual physics have no FRB2 equivalent and no
fixture. Sub-collision *is* implemented, resolving the named shape through the entity's `Objects`;
only DialogBoxDemo exercises it and that fixture is not vendored, so it is code-verified rather than
data-verified.
