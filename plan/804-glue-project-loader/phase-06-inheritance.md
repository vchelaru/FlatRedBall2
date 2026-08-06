# Phase 6 — Inheritance

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — see §9. `SetByContainer` and engine-type bases deliberately out of scope. |
| **Depends on** | Phase 2 (objects), Phase 3 (variables) |
| **Blocks** | Phase 5 (inherited Gum screens), Phase 8 (abstract entities get no factory) |
| **Suggested branch** | `804-phase-6-inheritance` |

---

## 1. The problem

DoorsDemo's start-up screen is `Screens\Level1`, and `Level1` sets
`BaseScreen: "Screens\\GameScreen"`. **Phase 1 boots it un-merged, and that is currently wrong in a
way a person can see**: `Level1` declares four NamedObjects, its base declares nine. Every collision
relationship, the door list, and the camera controller live only on the base.

This phase makes a derived element behave as the union of its chain. It is the phase that turns
"a screen loads" into "the game's screens load".

It is also the hardest phase in the epic, because **FRB1 expresses inheritance as C# class
inheritance and this epic has exactly one CLR type per element kind.** Everything the compiler did
for free has to become an explicit data rule.

---

## 2. Scope

### In scope

1. Resolve `BaseScreen` / `BaseEntity` and walk the chain.
2. Merge NamedObjects with correct definition-vs-delta semantics.
3. Merge CustomVariables, including the inherit/override tri-state.
4. Merge `ReferencedFiles`, `States`, and `Events`.
5. Refuse to instantiate an abstract element.

### Out of scope

- Entities inheriting from an **engine type** (`BaseEntity: "FlatRedBall.Sprite"`) — G65, D62.
- `SetByContainer` — 4 occurrences repo-wide, all in FRB1's test project (G66).
- Inherited *states* ordering beyond a straight prepend → Phase 7 owns state application.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | The chain resolves | A derived screen knows its ancestors, in order. | §6.1 |
| F2 | Objects merge correctly | `Level1` gets all nine of `GameScreen`'s objects plus its own overrides. | §6.2 |
| F3 | Variables merge correctly | A derived variable with no value inherits; with a value, overrides. | §6.3 |
| F4 | Abstract elements are refused | A base with an unfilled slot cannot be instantiated. | §6.4 |
| F5 | Files and events merge | An inherited Gum screen and collision event survive. | §6.5 |

---

## 4. Proposed resolution

### The finding that shapes everything: the file is *already partially merged*

FRB1's editor physically copies base entries into the derived element's own lists at design time
(`InheritanceManager.UpdateFromBaseType`, `:404`), stamping `DefinedByBase = true`. Then a
save-time pass **deletes most of the copies again**
(`GlueProjectSaveExtensions.RemoveRedundantDerivedData`, `:193`).

So a derived file contains:

- its own new objects,
- *some* base-derived objects marked `DefinedByBase` — those that survived stripping because they
  carry `InstructionSaves`, `ContainedObjects`, `SetByDerived`, or a differing property,
- and **nothing at all** for the majority of base content.

**A derived file is neither self-contained nor a pure delta.** It is a partial overlay, and the
merge has to treat it as one.

Only base entries that are `SetByDerived` or `ExposedInDerived` are ever copied down
(`InheritanceManager.cs:479-480`); only `SetByDerived` variables are (`:716`). Everything else —
plain objects, plain variables, files, events, states — lives solely on the base.

The strip pass is gated on `FileVersion >= RemoveRedundantDerivedData` (38), so older projects carry
full unstripped copies. **Both shapes must load.**

### The merge table

Definition-vs-delta is decided per NamedObject by the flag pair, and **it is not "derived wins"**:

| Base flag | Derived flag | Instantiated by | Derived contributes |
|---|---|---|---|
| `SetByDerived` | `DefinedByBase`, `InstantiatedByBase = false` | **derived** | everything — a full definition |
| `ExposedInDerived` | `DefinedByBase`, `InstantiatedByBase = true` | **base** | `InstructionSaves` only |
| `ExposedInDerived` | *(absent — stripped)* | **base** | nothing |
| *(plain)* | *(absent)* | **base** | nothing |
| `ExposedInDerived`, `SourceType.File` | `DefinedByBase`, `InstantiatedByBase = true`, **different `SourceFile`** | **derived** (re-instantiate) | everything — G62 |

### Ordering

FRB1 has no single order — it has four, because different generated methods emit `base.X()` in
different positions. The two that matter:

- **Object construction: derived-first**, then base (`CodeWriter.cs:985`, `:1208-1213`).
- **Variable assignment: base-first**, derived last and therefore winning
  (`CodeWriter.cs:2737`, `:2372-2375`).

Chain order comes from `ObjectFinder.GetInheritanceChain` (`ReferenceService.cs:739`) — *"most base
type first and the most derived type last, the argument element included as the last entry."* That
is the order a data merge wants for variables.

---

## 5. Gotchas

### G60 — `EntitySave.ImplementsICollidable` is bag-backed and FRB2 reads it as `false` · **Blocker**

FRB1 declares it `[XmlIgnore][JsonIgnore]` over `Properties` (`EntitySave.cs:66-79`). FRB2 models it
as a plain JSON property (`src/Glue/Model/EntitySave.cs:21`), so it **deserializes as `false` for
every project**.

Verified: it appears as a `Properties` entry in all five fixture entities that declare it
(`Beefball/{Goal,PlayerBall,Puck}.glej`, `DoorsDemo/{Door,Player}.glej`) and as a top-level member in
none.

Note the asymmetry that makes this easy to get wrong: `ImplementsIClickable` (`:82`),
`ImplementsIVisible` (`:93`), and `ImplementsITiledTileMetadata` (`:130`) **are** plain properties in
FRB1 and are correctly modelled. Only `ImplementsICollidable` is bag-backed.

This is Phase 1's G4 recurring in a class nobody re-audited — the third instance of that failure
(see also Phase 4 G40, Phase 7 G73). **Audit the whole mirror against FRB1's attributes rather than
fixing this one member.**

### G61 — Screens write the base twice, entities once

| | `BaseScreen` / `BaseEntity` | `BaseElement` |
|---|---|---|
| `.glsj` | written | **also written** (`ScreenSave.cs:132`, no `[JsonIgnore]`) |
| `.glej` | written | **never** (`EntitySave.cs:210-212`, explicitly `[JsonIgnore]`) |

Verified: `Level1.glsj:6-7` carries both; `DerivedEntity.glej` carries only `BaseEntity`.

`BaseElement` is missing from FRB2's model entirely. **Do not add it as a JSON member** — it is a
computed alias, and binding it would make the two disagree on `.glej`. Compute it, per G24.

### G62 — A derived level that points at its own TMX must re-instantiate

`ShouldReinstantiateDespiteInstantiatedByBase` (`NamedObjectSaveCodeGenerator.cs:1162-1173`): when
`SourceType == File` and the derived's `SourceFile` differs from the base's, the derived
re-instantiates **despite** `InstantiatedByBase = true`. The comment cites FRB1 issue #1770 and
explains why: `SourceFile` is only read at instantiation, so the override would otherwise be ignored.

**This is exactly DoorsDemo's `Map`.** The base declares it `SourceType: 2` + `SetByDerived`;
`Level1` declares it with **no `SourceType` key** (so `File`) and
`SourceFile: "Screens/Level1/Level1Map.tmx"`. Skipping this rule means **every derived level renders
the base's map** — or no map at all.

### G63 — `IsAbstract` is computed, is written to disk, and must not be trusted

`GlueElement.IsAbstract` (`:295`) is `=> AllNamedObjects.Any(item => item.SetByDerived)` — get-only
and **not** `[JsonIgnore]`, so Newtonsoft writes it. `GameScreen.glsj:368` has `"IsAbstract": true`;
`EntitySave` writes none, and it is absent when false.

So the key is present sometimes, computed always. **Recompute it** — G24 again.

The asymmetry that matters: only `AllNamedObjects` counts. `CustomVariable.SetByDerived` does
**not** make an element abstract. Every DoorsDemo and Beefball entity has `SetByDerived` variables
and all are concrete — `Player.glej` has six, all on variables, and gets a factory.

Also note `AllNamedObjects` is only **two levels deep** (`GlueElement.cs:130-144`) — top-level plus
each object's `ContainedObjects` — and its own doc says it does not reach base elements.

### G64 — Name collisions that inheritance made legal become illegal

`Level1.glsj:16-33` declares a `DefaultLayer` CustomVariable identical to `GameScreen.glsj:14-31`,
with **no `DefinedByBase` flag on either**. In FRB1 these are separate members on separate classes —
a C# member-shadowing warning at worst. In a flat dictionary they collide.

**How we tackle it.** Derived wins for un-flagged duplicates, matching `AssignCustomVariables`
ordering. Emit a diagnostic — a duplicate with no `DefinedByBase` is a shape FRB1 tolerates and the
merge should surface.

### G65 — Twelve entities inherit from an engine type, and the issue never mentions it

`BaseEntity` is not always another element. Repo-wide:

| Base | n |
|---|---|
| `FlatRedBall.Sprite` / `Sprite` | 6 |
| `Text` / `FlatRedBall.Graphics.Text` | 2 |
| `SpriteFrame` | 2 |
| `Emitter` | 1 |

FRB1 generates `class SpriteInheritingEntity : Sprite` — the entity **is** a Sprite. Combined with
`IsContainer` (a bag-backed flag whose generated assignment is literally `objectName = this;`,
`NamedObjectSaveCodeGenerator.cs:1007-1010`), this is a whole second inheritance model.

With one `GlueEntity` CLR type there is no expression for it. **Out of scope** (D62): detect,
diagnose clearly, and skip. Two of the four base types do not exist in FRB2 anyway (`Text`,
`SpriteFrame`, `Emitter`).

### G66 — `SetByContainer` is real, undocumented, and barely used

A plain serialized property (`NamedObjectSave.cs:323`) with **no XML doc in FRB1**. Its meaning is
inferable from ~8 codegen consumers: the element does not instantiate the object; whoever contains
the element assigns it. In Entities only — Screens are exempt because a Screen has no container.

Four occurrences repo-wide, all in FRB1's test project, none in any sample.

**How we tackle it.** Add it to the mirror, diagnose it, do not implement it. Low confidence in the
semantics; drive it from a fixture if a real project ever needs it.

### G67 — `SourceClassGenericType` is missing, and Phase 8 cannot proceed without it

Not in FRB2's model at all, and it is the **only** place a list's element type lives —
`SourceClassType` is just `PositionedObjectList<T>` with a literal unresolved `<T>` (Phase 1's G18).

Present on every list in the live fixtures: `Entities\Player` and `Entities\Door` in DoorsDemo,
four more in Beefball. Same `"<NONE>"` → null sentinel as `CurrentState`.

Needed by: `IsFullyDefined`, factory-list wiring (Phase 8), and `MatchDerivedToBase`
(`InheritanceManager.cs:832-835`, which copies *only* this field).

Phase 1's G18 identified the `<T>` placeholder as unresolvable without noticing the argument sits in
a sibling field. It is resolvable, and this is where it gets resolved.

### G68 — `DefaultValue == null` on a `DefinedByBase` variable is a distinct third state

`InheritanceManager.cs:776-783` deliberately nulls `DefaultValue` on a copied-down variable, with a
comment: *"the derived variable should not explicitly set its value. It should instead have a null
value to indicate that it is inheriting its value from the base element."*

So a `DefinedByBase` variable has three meanings:

| `DefaultValue` | Means |
|---|---|
| absent / null | **inherit** the base's value |
| present | **override** |
| *(not `DefinedByBase`)* | a new variable |

FRB2 types `DefaultValue` as a non-nullable `JsonElement`, so `Undefined` and `Null` are
distinguishable — but **`CustomVariable.DefinedByBase` is missing from the mirror**, so the
tri-state is currently unrepresentable.

### G69 — Lists are instantiated at field-declaration time for an ordering reason

`NamedObjectSaveCodeGenerator.cs:95-98` instantiates lists at declaration rather than in
`Initialize`, and the comment at `:83-94` says why: constructor order is base-first, so a base's
constructor could otherwise add to a derived's still-null list.

A merge that builds lists lazily in declaration order reproduces this. One that builds them during
`Initialize` does not.

---

## 6. Tasks

Test-first throughout.

### 6.0 — Model audit (do this first)

- [x] **Audit every member of `src/Glue/Model/` against FRB1's `[JsonIgnore]` attributes.** Done
      mechanically rather than by eye — see §9 for the result and the method.
- [x] Failing test: `ImplementsICollidable` reads `true` from a fixture entity (G60).
- [x] Add `SourceClassGenericType`, with `"<NONE>"` → null (G67).
- [x] Add `IsDisabled`, `SetByContainer`, bag-backed `IsContainer`, computed `IsFullyDefined`, and
      `CurrentState` (Phase 7's, added here because the audit was open).
      `CustomVariable.DefinedByBase` landed in Phase 3.
- [x] Add computed `BaseElement` and computed `IsAbstract` — **never JSON-bound** (G61, G63).

### 6.1 — Chain resolution

- [x] Failing test: `Level1` resolves against `GameScreen`.
- [x] Failing test: a three-level chain resolves most-base first.
- [x] Failing test: a `BaseScreen` naming a missing element warns and loads the element alone.
- [x] Failing test: a cycle terminates with an error rather than hanging.

### 6.2 — NamedObject merge

- [x] Failing test: `Level1` ends with **nine** objects, not four.
- [x] Failing test: `Level1`'s `Map` uses `Level1Map.tmx`, not the base's placeholder (G62).
- [x] Failing test: `CloudCollision` keeps both its base's authored properties and the derived
      instruction.
- [ ] **Not needed — the merge table collapsed.** §4's five-row definition-vs-delta table assumed a
      derived entry is a *delta*. It is not: Glue strips a redeclared entry unless it differs, so an
      entry that survives to disk carries its complete state. DoorsDemo's `CloudCollision` is
      byte-identical in both files apart from the derived flags. The merge is therefore
      replace-wholesale, and the flags do not need to be consulted at all. Recorded in §9.
- [ ] **Deferred:** an unstripped pre-version-38 file. Same shape (full copies), so
      replace-wholesale handles it by construction — but no fixture exists below the gate to prove
      it, and the epic's ground rule is latest-version-only.

### 6.3 — CustomVariable merge

- [x] Failing test: a `DefinedByBase` variable with no `DefaultValue` inherits (G68).
- [x] Failing test: one with a `DefaultValue` overrides.
- [ ] **Not implemented:** a diagnostic for an un-flagged duplicate (G64). `Level1`'s `DefaultLayer`
      is exactly this case and resolves derived-wins silently. Warning on it would fire for a shape
      Glue produces routinely, so it needs a way to tell "redeclared identically" from "genuinely
      conflicting" first.

### 6.4 — Abstract

- [x] Failing test: `IsAbstract` is computed from `SetByDerived` objects, and `GameScreen` is
      abstract while `Level1` is not.
- [x] Failing test: `SetByDerived` on a *variable* does **not** make it abstract (G63).
- [ ] **Deferred to Phase 8/14:** refusing to *instantiate* an abstract element. Nothing constructs
      an element by name yet — `GlueScreen.Save` is assigned directly — so there is no chokepoint to
      enforce it at. Phase 8 (no factory for an abstract entity) and Phase 14 (`MoveToScreen`) are
      where it becomes reachable, and both docs already carry it.

### 6.5 — Files, states, events

- [x] Failing test: `Level1` inherits `GameScreenGum.gusx` (unblocks Phase 5 G53).
- [x] Inherited states and categories merge base-first, via the same helper.
- [ ] **Blocked on Phase 9:** inheriting the `PlayerVsDoorCollided` event. `GlueElement` has no
      `Events` list yet — Phase 9 G97 owns adding it. The merge will pick it up for free once it
      exists.

### 6.6 — Fixtures and wrap-up

- [ ] **Not needed for this phase.** DoorsDemo covers screen inheritance end to end, and the
      entity/three-level/cycle cases are covered by in-memory fixtures through the read seam —
      matching Phase 1's deviation, which found the same trade favourable. Vendoring
      `TestProjectDesktopNet6` still matters for Phases 7, 8 and 11, and the short-form
      `SourceClassType` caveat in `plan/plan.md` still applies to them.
- [x] Failing test: `BaseEntity: "FlatRedBall.Sprite"` produces one clear diagnostic (G65).
- [x] XML docs; update this document and `plan/plan.md`.

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D60 | Merge eagerly at load, or resolve lazily through the chain? | **Merge eagerly into a flattened element at load.** Lazy resolution means every later phase has to know about inheritance; a flattened element means only this phase does. Cost is memory for a duplicate object graph, which is trivial at project scale. |
| D61 | Does the issue's "author a minimal `BaseEntity` fixture" still apply? | **No — it is based on a wrong premise.** The issue says no sample uses `BaseEntity`. True of `Samples/`, but there are **41** in `Tests/TestProjectDesktopNet6/`, including three-level chains and every flag combination. Vendor those instead of authoring fiction. |
| D62 | Support entities inheriting from an engine type? | **No — diagnose and skip** (G65). Two of the four base types do not exist in FRB2, and the concept has no data-driven expression when every entity shares one CLR type. Revisit only if a real project needs it. |
| D63 | What happens when an abstract element is the `StartUpScreen`? | **Error, not warning.** Every other failure in this epic degrades to "less appears"; this one would boot a screen that is missing its own content by construction. Fail loudly. |

---

## 8. Definition of done

- [x] `dotnet build` clean; `dotnet test` green (**1326**).
- [x] A real `PublishTrimmed` emits no IL warnings from `src/Glue`, and the trimmed binary runs
      correctly (Phase 2 G26).
- [x] DoorsDemo's `Level1` — the project's **start-up screen** — carries all nine of its objects and
      uses its own map.
- [x] The §6.0 model audit is complete and its result recorded in §9.
- [x] A three-level chain merges correctly.
- [x] §4's merge table is superseded rather than tested row by row — see §6.2 and §9 for why.
- [x] Every gotcha in §5 is covered by a test or explicitly deferred.

---

## 9. What landed

11 new tests, full suite **1326 green**, no new build warnings, trimmed publish verified by running
it.

| Piece | File |
|---|---|
| Chain resolution and the merge | `src/Glue/GlueInheritanceResolver.cs` |
| Computed `BaseElement`, `IsAbstract`, `AllNamedObjects` | `src/Glue/Model/GlueElement.cs` |
| Bag-backed `ImplementsICollidable` | `src/Glue/Model/EntitySave.cs` |
| `SourceClassGenericType`, `IsFullyDefined`, `IsDisabled`, `SetByContainer`, `IsContainer`, `CurrentState` | `src/Glue/Model/NamedObjectSave.cs` |

**The payoff:** DoorsDemo's start-up screen is no longer missing two thirds of itself. `Level1`
arrives with all nine objects — the door list, three collision relationships and the camera it
inherits, plus its own map pointing at its own `.tmx`.

### The merge is far simpler than planned, and that is a finding

§4 specified a five-row table deciding, per object, whether the base or the derived side owns it and
what the other contributes. **That table was unnecessary.** Glue's save-time strip pass removes a
redeclared entry from the derived file *unless it differs from the base*, so any entry that survives
to disk is a complete redeclaration rather than a delta. `CloudCollision` appears in both DoorsDemo
files byte-identical apart from the derived flags.

So the merge is: base entries in order, each replaced wholesale by the derived entry of the same
name, then whatever the derived element adds. `DefinedByBase`, `InstantiatedByBase` and
`ExposedInDerived` never need to be consulted — including G62's re-instantiate rule, which falls out
for free because the derived `Map` simply replaces the base's placeholder.

The one exception is variables, where Glue *does* write a genuine stub: it nulls `DefaultValue` to
mean "inherit" (G68). That is the single `keepBase` predicate in the implementation.

### Found while building

- **The audit script was wrong on its first run and said the codebase was clean.** It reported zero
  mismatches while `ImplementsICollidable` — a known bug — sat right there. FRB1 puts the opening
  brace on the line *after* the declaration, which the regex required. A tool that agrees with your
  hopes deserves to be tested against a defect you already know about before you trust its silence.
  With that fixed it found exactly one mismatch, which is now fixed.
- **Phase 6 raises the unmapped-type count rather than lowering it: 13 → 18.** That is progress, not
  regression. `Level1` previously declared four objects and now carries all nine it inherits, so the
  unmapped ones are honestly counted in both screens. The pinned test records the reason.
- **Two existing tests were encoding the absence of this phase.** `GlueScreenTests` asserted that
  booting a derived screen un-merged "is correct only because this phase builds empty screens"; it
  now asserts the nine merged objects. The Phase 1 model test that pins `Level1`'s own four objects
  still passes untouched, because it deserializes the file directly — the mirror stays faithful to
  disk and only the loader flattens.

### Deliberately not done

- **`SetByContainer`** (G66) is modelled but not implemented. Four occurrences repo-wide, all in
  FRB1's test project, and no XML doc in FRB1 to pin the semantics.
- **Engine-type bases** (G65) are diagnosed, not resolved — D62.
- **Refusing to instantiate an abstract element** has no chokepoint until Phase 8 or 14; both docs
  carry it.
- **Event inheritance** waits on Phase 9 adding `GlueElement.Events`; the merge will pick it up
  without further change.
