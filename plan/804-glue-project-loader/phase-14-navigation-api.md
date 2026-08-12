# Phase 14 — Name-Based Navigation and Instantiation API

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented |
| **Depends on** | Phase 3 (variables), Phase 6 (a screen is complete), Phase 8 (spawning) |
| **Blocks** | Nothing — this is the epic's last phase |
| **Suggested branch** | `804-phase-14-navigation-api` |

---

## 1. The problem

Every phase before this one is loader plumbing. This is the phase where a **game developer** touches
the result: navigating to a loaded screen, spawning a loaded entity, and reading or writing its
variables.

FRB2's existing API is generic-typed — `MoveToScreen<T>()`, `Factory<T>.Create()` — which assumes a
compile-time C# type. Every loaded element shares one type, so the fix is string overloads on the
existing names.

**There is a blocker that must be solved first, and it is not the API shape.** Nothing outside
`src/Glue/` references `GlueProjectLoader`, `GlueLoadResult`, `GlueScreen`, or `GlueEntity` — grep
returns zero hits. `GlueScreen` holds only a `ScreenSave`; `GlueLoadResult` is returned to the caller
and dropped. **There is no project context anywhere in the engine**, so nothing can resolve a name
to an element. See G140.

---

## 2. Scope

### In scope

1. A project context the engine can hold.
2. `MoveToScreen(string, Action<GlueScreen>?)`.
3. `Factory<GlueEntity>.Create(string, Action<GlueEntity>?)` — or whatever Phase 8's D80 produced.
4. An indexer for variables and objects.
5. `Get<T>(name)`.

### Out of scope

- Generated name constants — the issue rules this out ("no codegen this pass").
- `dynamic` — rejected in the issue and reaffirmed here.
- A `SetState` API → Phase 7 D70 owns the machinery; this phase names it if it lands first.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | Navigate by name | `MoveToScreen(@"Screens\Level2")` from a Gum button. | §6.2 |
| F2 | Navigate back | A loaded screen can move to a hand-written one, and vice versa. | §6.2 |
| F3 | Spawn by name | Game code spawns `Entities\Bullet` at runtime. | §6.3 |
| F4 | Read and write variables | `entity["Health"] = 100`, `entity.Get<int>("Health")`. | §6.4 |

---

## 4. Proposed resolution

### The project context

`RequestScreenRestart` (`src/FlatRedBallService.cs:488`) already takes a non-generic
`Action<Screen>?` and constructs via `Activator.CreateInstance(_lastScreenType!)` (`:507`) — so a
non-generic construction path exists. `RequestScreenChange<T>` is generic but its body is only
`new T()` plus bookkeeping.

So the string overload is thin:

```csharp
public void MoveToScreen(string glueScreenName, Action<GlueScreen>? configure = null)
    => Engine.RequestScreenChange<GlueScreen>(s => {
           s.Save = ResolveScreenSave(glueScreenName);   // <- the only new work
           configure?.Invoke(s);
       });
```

`Save` must be set **before** `configure` runs, so a caller can override it. Ordering is already
correct: `ActivateScreen` invokes `configure` (`:483`) before `CustomInitialize` (`:592`), and
`Factory.CreateCore` does the same (`:309-310`) — which matters because both `GlueScreen` and
`GlueEntity` call `BuildObjects()` from `CustomInitialize`.

### The indexer

Three name sources, and they are **not** one namespace:

1. tunneled CustomVariable → `Objects[SourceObject].{SourceObjectProperty}`
2. exposed CustomVariable → a real CLR property on `this`, else the backing bag
3. NamedObject → `Objects[name]`

Resolution order is 1, 2, 3, with a throw on genuine ambiguity — see G143.

---

## 5. Gotchas

### G140 — There is no project context, and nothing outside `src/Glue/` knows the loader exists · **Blocker**

Verified by grep across `src/` and `samples/`: zero non-`src/Glue/` references to
`GlueProjectLoader`, `GlueLoadResult`, `GlueScreen`, or `GlueEntity`.

`GlueScreen` holds `ScreenSave? Save` and nothing else (`src/Glue/GlueScreen.cs:27`). `GlueEntity`
holds `EntitySave? Save` (`:82`). `GlueLoadResult` is handed back and forgotten.

**Neither API in this phase is buildable until something holds the loaded project.** D140.

### G141 — Element names carry a literal backslash

`"Screens\\Level1"`, `"Entities\\Player"`. In C# that is `@"Screens\Level1"` or `"Screens\\Level1"`.
A user who writes `"Screens/Level1"` or bare `"Level1"` gets a runtime miss with no compiler help.

`ElementNameToRelativePath` (`src/Glue/GlueProjectLoader.cs:77`) converts `\`→`/` for **paths only**,
and its doc is explicit that the backslash form is the identity for comparison.

**How we tackle it.** D141. Whatever is decided, the error message must show the accepted form —
this is the single most likely thing to waste a user's afternoon.

### G142 — Case sensitivity is already inconsistent across three places

| Site | Comparison |
|---|---|
| `ResolveStartUpScreen` (`GlueProjectLoader.cs:164`) | `OrdinalIgnoreCase` |
| `GlueScreen.Objects` (`GlueScreen.cs:20`) | ordinal (**case-sensitive**) |
| file resolution (`:130-137`) | case-insensitive with a warning |

A string API has to pick one, and picking a *fourth* would be worse than any of the three.

**How we tackle it.** Match `ResolveStartUpScreen` — `OrdinalIgnoreCase` — and change `Objects` to
agree. Glue authored these names on Windows; a case-sensitive lookup is a Linux-and-web-only failure,
which is the worst kind.

### G143 — CustomVariables and NamedObjects are two namespaces, and Glue only guards one direction

Glue's own rules are **asymmetric**:

- **CustomVariable → NamedObject is blocked.** `NameVerifier.IsCustomVariableNameValid` (`:789-794`)
  rejects a variable whose name matches an existing NamedObject.
- **NamedObject → CustomVariable is NOT blocked.** `ScreenSave.HasMemberWithName` (`:216-235`)
  checks `ReferencedFiles` and `NamedObjects` only — never `CustomVariables`.

So a collision is creatable by ordering: add variable `Health`, then add object `Health`.

**Empirically: 0 collisions across 273 FRB1 element files and 12 fixture files.** But it is not an
invariant, so **throw on ambiguity** rather than silently picking.

Note `ReferencedFiles` shares the NamedObject namespace — a third source an indexer might want.

### G144 — Most CustomVariables are exposed engine properties, so a pure bag indexer is wrong

Across all 590 CustomVariables in FRB1:

| Name | Count |
|---|---|
| `X` | 116 |
| `Y` | 107 |
| `Z` | 105 |
| `DefaultLayer` | 39 |
| `GroundMovement` / `AirMovement` / `AfterDoubleJump` | 10 each |
| `Drag` | 6 |
| `Visible`, `CurrentState` | 5 each |

**`X`/`Y`/`Z` are 328 of 590 — 56 %** — and they name properties that already exist on FRB2's
`Entity` (`src/Entity.cs:64`, `:70`, `:86`). Same for `Visible` and `Drag`.

So `entity["X"] = 5` **must write `Entity.X`**, not a side dictionary — otherwise the two disagree
and gameplay reads the wrong one.

87 of 590 are tunneled, and they must route through `Objects[SourceObject]` with
`OverridingPropertyType` coercion (Phase 3 G33).

There are no existing indexers on `Entity`, `Screen`, `GlueScreen`, or `GlueEntity`, so the syntax is
free — but the semantics are not.

### G145 — `Get<T>` must be driven by the caller's `T`, not by the declared type

Declared `Type` values across all 590: `float` 396, `string` 60, `bool` 12, `VariableState` 11,
`Color` 8, `int` 8, a CSV path 7, a generated CSV class 6, a state-category name 5, `Texture2D` 4,
`Scene` 3, `double` 3, `AnimationChainList` 3, `byte` 3.

Some are CLR types, some are file paths, some are Glue state categories. And `Type` is bag-backed and
absent on many entries.

**`PropertySaveExtensions.GetValue<T>` already establishes the right precedent**
(`src/Glue/PropertySaveExtensions.cs:22-26`) — the caller's `T` is authoritative. Follow it.

### G146 — Restart replays one configure slot, and a string overload's closure occupies it

`RequestScreenRestart` replays `_lastScreenConfigure` against a fresh instance
(`FlatRedBallService.cs:507-508`), and there is exactly **one** configure slot, replaced by whoever
set it last (`:504-505`).

The string overload's synthesized closure occupies that slot. So user code calling
`screen.RestartScreen(s => …)` on a `GlueScreen` **discards the `Save` assignment** — producing a
screen with `Save == null`, which `BuildObjects` silently treats as "nothing to build"
(`GlueScreen.cs:61-62`).

**A live footgun that fails silently as an empty screen.** Either compose the configures or make
`Save` survive independently of the slot.

### G147 — A name that resolves is not necessarily loadable

`GlueProjectLoader.Load` tolerates a missing or unparseable `.glsj` with a Warning and simply does
not add it (`:47-53`). So `MoveToScreen(@"Screens\Level3")` for a screen that failed to load must
**not** silently no-op.

### G148 — Navigating to a derived screen before Phase 6 builds it half-populated

`Level1`'s own NamedObjects are a strict subset of `GameScreen`'s. `GlueScreenTests.cs:50-52` already
comments this: *"Booting it un-merged is correct only because this phase builds empty screens; Phase
6 has to apply the base before anything is constructed."*

**Phase 6 is a hard dependency**, not a nice-to-have.

### G149 — `ScreenSave.NextScreen` is parsed and unused

`src/Glue/Model/ScreenSave.cs:13`. Glue's own "advance to the next screen" flow. A `MoveToNextScreen()`
would be a natural addition and needs the same project context — worth deciding here rather than
leaving a parsed-but-dead member.

---

## 6. Tasks

Test-first throughout. **§6.1 first — it is the blocker.**

### 6.1 — Project context

- [x] Decide D140 and record the reasoning.
- [x] Failing test: after loading, the engine can resolve `"Screens\\Level1"` to its `ScreenSave`.
- [x] Failing test: an unknown name produces a clear error naming the accepted form (G141).
- [x] Failing test: a name that resolves but whose file failed to load errors, not no-ops (G147).

### 6.2 — Navigation

- [x] Failing test: `MoveToScreen(@"Screens\Level1")` activates a `GlueScreen` with that `Save`.
- [x] Failing test: `Save` is set before `configure` runs.
- [x] Failing test: a loaded screen can `MoveToScreen<AHandWrittenScreen>()`.
- [x] Failing test: `RestartScreen` on a `GlueScreen` keeps its `Save` (G146) — the test that would
      have caught the silent-empty-screen bug.
- [x] Failing test: matching is `OrdinalIgnoreCase`, consistent with `ResolveStartUpScreen` (G142).
- [x] Decide D142 on `MoveToNextScreen` (G149).

### 6.3 — Spawning

- [x] Failing test: creating `"Entities\\Player"` produces a `GlueEntity` with that `Save` and its
      objects built.
- [x] Failing test: `configure` runs before `CustomInitialize`.
- [x] Failing test: spawning an abstract entity errors (Phase 6 D63).
- [x] Failing test: a null `configure` is accepted — `Create(Action<T>)` throws on null
      (`Factory.cs:263`), so route through `CreateCore` rather than delegating.

### 6.4 — The indexer

- [x] Failing test: `entity["X"] = 5` writes `Entity.X`, not a bag (G144).
- [x] Failing test: `entity["MovementSpeed"] = 300` writes the bag — no CLR member exists.
- [x] Failing test: `entity["CooldownCircleRadius"] = 8` routes through `Objects["CooldownCircle"]`.
- [x] Failing test: `entity["Score1"] = 3` coerces int → string (Phase 3 G33).
- [x] Failing test: `Get<T>` is driven by `T`, not the declared type (G145).
- [x] Failing test: an ambiguous name throws rather than picking (G143).
- [x] Failing test: `Objects` and the indexer agree on case (G142).

### 6.5 — Wrap-up

- [x] XML docs on every public member — this is the epic's only user-facing surface.
- [x] Write the `glue-project-loading` skill. Phase 1 §7.7 deferred the decision until there was
      enough surface to justify the context budget; this phase is that surface. Consult
      `skill-creator` first.
- [x] Update this document and `plan/plan.md`; mark the epic complete.

### 6.6 — What differed from the plan

- **`MoveToNextScreen()` was not added; `GlueProject.NextScreenOf(ScreenSave)` was.** D142 argued a
  parsed-but-dead `NextScreen` reads as an oversight, and that still holds — but a `MoveToNextScreen`
  on `Screen` would be a second navigation convention on the engine's own base class serving one
  loader concept. Resolving the name and passing it to `MoveToScreen(string)` is one line at the call
  site and keeps the engine surface to a single by-name overload.
- **No vendored fixture sets `NextScreen`** (all four checked), so the resolving case is covered with
  a synthetic `ScreenSave` pointed at a real one. What is under test is the lookup, not the parse.
- **The runtime bag is separate from the authored one.** `_variables` holds authored `JsonElement`s;
  the indexer writes `_runtimeVariables`, a plain `Dictionary<string, object?>` read first. Storing a
  runtime value in the JSON bag would mean serializing on every set — reflection-based and so
  AOT-hostile, for a value that is about to be read back as an object anyway.
- **`FlatRedBallService.GlueProject` gained a public setter.** `Initialize` sets it from
  `GlueProjectFile`, but a game that loads a project itself has no other way to hand it over, and
  every by-name navigation call needs it. Tests use the same door.

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D140 | Where does the loaded project live? | **A `GlueProject` object held by `FlatRedBallService`, with a back-pointer on `GlueScreen`/`GlueEntity`.** It is the only shape that serves both APIs plus Phase 8's factory registry, and it keeps `GlueLoadResult` as a pure load report. Rejected: a `GlueLoadResult`-scoped facade (a second way to reach the engine) and a static (forbidden by the architecture rules). |
| D141 | Does the string API normalize separators and accept short names? | **Accept `\` and `/`; require the full `Screens\Name` form.** Normalizing separators costs one `Replace` and removes the most likely user error. Accepting a bare leaf name is tempting but ambiguous — `Entities\Player` and `Screens\Player` can coexist — so require the prefix and say so in the error. |
| D142 | Add `MoveToNextScreen()`? | **Yes.** `NextScreen` is already parsed, it is Glue's own idiom, and leaving a parsed-but-dead member is the kind of thing that reads as an oversight. One method, same context. |
| D143 | Keep `Objects` as a separate surface once the indexer exists? | **Yes.** `Objects` is the typed dictionary a loader-aware caller wants (`(Circle)screen.Objects["X"]`); the indexer is the variable-bag view. Two intents, two members. Change `Objects` to `OrdinalIgnoreCase` for consistency (G142). |

---

## 8. Definition of done

- [ ] `dotnet build` clean; `dotnet test` green.
- [ ] A real `PublishTrimmed` emits no IL warnings from `src/Glue` (Phase 2 G26).
- [ ] A loaded project navigates between its screens by name, in both directions with hand-written
      screens.
- [ ] `RestartScreen` on a `GlueScreen` preserves its `Save` (G146).
- [ ] `entity["X"]` and `entity.X` are the same value (G144) — the test that proves the indexer is
      not a parallel universe.
- [ ] An ambiguous name throws (G143).
- [ ] Every public member has an XML doc.
- [ ] The skill decision from Phase 1 §7.7 is closed, either way, with the reason.
- [ ] Every gotcha in §5 is covered by a test or explicitly deferred.
