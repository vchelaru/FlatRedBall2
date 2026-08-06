# Phase 7 — States & Categories

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — see §9. `NamedObjectSave.CurrentState` waits on nested entities. |
| **Depends on** | Phase 3 (a state *is* a set of CustomVariable values) |
| **Blocks** | Nothing |
| **Suggested branch** | `804-phase-7-states` |

---

## 1. The problem

A Glue **state** is a named snapshot of an element's variables. `PlayerBall` has a `DashCategory`
with `Tired` and `Rested`; setting the category applies every variable the category covers.

The persisted shape is small — `StateSave` is `Name` + `InstructionSaves`, and `StateSaveCategory`
adds `ExcludedVariables`. **FRB2 already mirrors all of it faithfully**
(`src/Glue/Model/StateSave.cs`), so this phase is entirely about *application*, not parsing.

The trap is that the obvious reading of the data is wrong in two ways — see G70 and G71.

---

## 2. Scope

### In scope

1. Apply a named state: set every variable the state covers.
2. Category semantics, driven by `ExcludedVariables`.
3. The element's initial state, expressed as a `CustomVariable`.
4. `NamedObjectSave.CurrentState` — an initial state on a nested instance.

### Out of scope

- Interpolation (`InterpolateToState`, `InterpolateBetween`) — `Time` is `0.0` in **all 726
  instructions** across FRB1, so nothing exercises it. D71.
- Inherited states (base element's states prepended) → Phase 6.
- States as a *runtime* API surface → Phase 14.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | A state applies | Setting `Rested` restores the cooldown circle. | §6.2 |
| F2 | Categories are independent | Setting a colour state does not disturb a size state. | §6.3 |
| F3 | The initial state applies | An element loads in the state its author chose. | §6.4 |
| F4 | Nested instances get their state | A `StateEntity` instance placed in a screen starts in `Left`. | §6.5 |

---

## 4. Proposed resolution

### A state is a full snapshot, not a delta

The single most important behaviour, from `StateCodeGenerator.CreatePredefinedStateInstances`
(`:144-152`):

```csharp
foreach (var variable in includedVariables) {
    var instruction  = state.InstructionSaves.FirstOrDefault(i => i.Member == variable.Name);
    var valueToSet   = instruction?.Value ?? variable.DefaultValue;
    ...
}
```

Iteration is over **included variables**, not over the instruction list. So applying a state means:
*for every variable the category covers, set it to the state's instruction if there is one, otherwise
to the variable's own `DefaultValue`.*

### What "included" means

`ShouldIncludeVariable` (`StateCodeGenerator.cs:91-131`):

- **Categorized**: every element `CustomVariable` **not** in `category.ExcludedVariables`.
- **Uncategorized**: every element `CustomVariable` that is not itself a state variable
  (`:96-101`). There is no exclusion list for the uncategorized set.

Verified against generated output: `PlayerBall`'s `DashCategory` carries exactly one field
(`CooldownCircleRadius`) — the one variable not among its nine `ExcludedVariables`.

### Application order

Phase 3 establishes the order. States slot in at step 3:

3. Per NamedObject: apply `CurrentState`, **then** its `InstructionSaves`.

FRB1 states the reason in a comment (`NamedObjectSaveCodeGenerator.cs:2402-2408`): *"a user can say
'I want state X, but then change variable Y and Z'."* `StateEntityWithoutCurrentStateVariable` in
FRB1's test project is authored specifically to prove it, and its `Summary` field says so.

---

## 5. Gotchas

### G70 — A state with `"InstructionSaves": []` still assigns everything · **Blocker**

`TextureScaleCategory.Small` in FRB1's test project has an empty instruction list. It is **not** a
no-op — it sets `SpriteInstanceTextureScale` to that variable's `DefaultValue` of `1.0`, which is
exactly how "small" is expressed.

A reader that iterates `InstructionSaves` produces a state that does nothing, and switching
`Big` → `Small` leaves the sprite big.

**How we tackle it.** Iterate included variables, per §4. Test with an empty-instruction state
specifically — it is the case a natural implementation gets wrong.

### G71 — Instructions naming an excluded or unknown variable are silently ignored

Because iteration is over included variables, an instruction whose `Member` matches nothing is
dropped. This is not hypothetical: `StateEntity.StateThatSetsOtherState` carries an instruction
assigning `CurrentCircleVisibilityState` — a **state-setting-a-state**, which the uncategorized rule
(`:96-101`) explicitly excludes. The instruction is on disk and FRB1 ignores it.

**How we tackle it.** Match FRB1 — ignore, but emit a diagnostic. FRB1's silence here is the kind of
thing the epic's "log FRB1 bugs as you find them" rule exists for; an author who wrote that
instruction expected it to work.

Related: `StateSave.SortInstructionSaves` (`StateSave.cs:59-102`) *removes* instructions matching no
CustomVariable — but it is **editor-side only**. Never assume on-disk order matches variable order,
and never assume every instruction has a match.

### G72 — The element's initial state is a `CustomVariable`, not a field

There is no `CurrentState` on `GlueElement`. The initial state is a variable:

| Kind | `Name` | `Type` | `DefaultValue` |
|---|---|---|---|
| uncategorized | `CurrentState` | `VariableState` | the state name |
| categorized | `Current<Category>State` | the category name | the state name |

So **it is applied by Phase 3's ordinary variable loop, in array order** — which means its position
decides whether later variables override what it set. `StateEntity` proves it: `CurrentState`
(setting `X = 8`) is index 2, `X` (default `0.0`) is index 4, so `X` ends at `0`.

Detection is `ObjectFinder.GetStateSaveCategory` (`:808-909`): `Type == "VariableState"` means
uncategorized; otherwise `Type` — stripped of a trailing `?` (`:900-903`) — is matched against the
element's `StateCategoryList` recursively.

### G73 — `NamedObjectSave.CurrentState` is not mirrored, and has a sentinel

`NamedObjectSave.cs:659-670` — a real serialized `string` whose setter maps `"<NONE>"` to `null`
(`:665-668`). **Absent from `src/Glue/Model/NamedObjectSave.cs` entirely.**

Two hard constraints from `WriteSetStateOnNamedObject` (`:974-990`):

1. It targets **only the uncategorized `CurrentState`**. There is no per-NamedObject categorized
   state.
2. It is **skipped entirely** if the referenced element has no uncategorized states.

There is exactly **one** occurrence in the whole FRB1 repo — `StateScreen.glsj`'s
`StateEntityWithoutCurrentStateVariableInstance`. Vendoring it is the only way to test this.

### G74 — `Current<Category>State` exists even with zero uncategorized states

`GenerateFields` (`:202`) emits the `VariableState` class and `CurrentState` property whenever the
element has **any** category. `PlayerBall` has zero uncategorized states and still gets both, with an
empty `AllStates` dictionary.

Harmless, but it means "has a `CurrentState`" does not imply "has uncategorized states", and code
that assumes otherwise will look for states that are not there.

### G75 — A variable typed as its own category is excluded from that category's setter

`:378-381` — to prevent infinite recursion. Cheap to miss, and the failure mode is a stack overflow
rather than a wrong value.

### G76 — Setting a state to null changes the current state but assigns nothing

`:365` guards the fan-out with `if (value != null)`. The backing field still updates. Reproduce both
halves.

### G77 — `States` and `StateCategoryList` are omitted when empty; `ExcludedVariables` never is

`GlueElement.cs:107`, `:165` have `ShouldSerialize` guards. `ChickenClicker/Screens/GameScreen.glsj`
has neither key — nor `CustomVariables`, nor `NamedObjects`.

Conversely, a serialized category **always** carries `ExcludedVariables`, including as `[]` (37 of
37). So an empty exclusion list means "every variable", not "no data".

Glue never writes explicit `null` for these arrays across all 273 element files — but STJ would bind
`null` to the property if it ever did, so initialise to empty collections rather than trusting.

### G78 — Values arrive as names, not ordinals, inside states

Across the 91 state instructions in FRB1: 53 float, 21 string, 10 bool, 7 int. Colours are names
(`"Red"`), enums are member names (`"CircleOn"`). **Nothing appears as an integer enum inside a
state** — the opposite of `CustomVariable.DefaultValue`, where enums are ints (Phase 3 G35).

`FixEnumerationTypes` / `ConvertEnumerationValuesToInts`
(`CustomVariableExtensionMethods.cs:368-428`) exist in FRB1 precisely because both forms occur.
FRB2's `GlueValueConverter` already handles both (`src/Glue/GlueValueConverter.cs:47-60`) — this is
a case where the existing code is already right, and the test should pin that.

---

## 6. Tasks

Test-first throughout.

### 6.1 — Model completion

- [x] Add `NamedObjectSave.CurrentState` with `"<NONE>"` → `null` (G73) — landed with Phase 6's
      model audit, reusing Phase 3's sentinel helper.
- [x] `States`/`StateCategoryList` absent deserialize to empty, not null (G77).

### 6.2 — Applying a state

- [x] Failing test: `DashCategory.Tired` sets `CooldownCircleRadius` to `0`, and `Rested` to `16`.
- [x] Failing test: a state with an **empty** instruction list still assigns, from the variable's
      `DefaultValue` (G70).
- [x] Failing test: an unknown state warns rather than throwing.
- [ ] **Not implemented:** a diagnostic for an instruction naming an excluded variable (G71). The
      instruction is correctly ignored — iteration is over covered variables — but nothing reports
      it. FRB1's `StateThatSetsOtherState` is the only known case and is not vendored.
- [ ] **Deferred:** setting a category to null (G76). There is no current-state slot to clear yet;
      it becomes meaningful when Phase 14 exposes the current state as readable.

### 6.3 — Categories

- [x] Failing test: `ExcludedVariables` defines the covered set — `Drag` survives a `DashCategory`
      change because the category excludes it.
- [x] Failing test: an empty `ExcludedVariables` covers every variable (G77).
- [x] A variable typed as its own category is excluded (G75) — without it the initial-state tests
      would recurse rather than fail.
- [ ] **Deferred:** an element with a category but no uncategorized states exposing a current state
      (G74). That is about a *readable* current state, which nothing exposes yet — Phase 14.

### 6.4 — Initial state

- [x] Failing test: an initial-state variable applies through Phase 3's variable loop, in list order.
- [x] Failing test: a variable declared **after** the initial-state variable overrides it (G72).
- [x] `Current<Category>State` resolves the category by `Type`, trailing `?` trimmed (G72).

### 6.5 — `NamedObjectSave.CurrentState`

- [ ] **Blocked.** The member is modelled and normalised, but nothing can exercise it: it applies to
      a *nested entity instance*, and `SourceType.Entity` objects are still unbuildable. This
      unblocks when nested entities instantiate (Phase 8 territory); the fixture to use is FRB1's
      `StateScreen.glsj`, the only `NamedObjectSave.CurrentState` in existence.

### 6.6 — Fixtures and wrap-up

- [x] Beefball's `PlayerBall` carries a real category with exclusions and covers the main paths.
      Vendoring `TestProjectDesktopNet6`'s state entities is still worthwhile for the uncategorized
      and inheritance cases — see the short-form `SourceClassType` caveat in `plan/plan.md`.
- [x] XML docs; update this document and `plan/plan.md`.

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D70 | How is a state exposed at runtime? | **A method taking category and state name**, e.g. `SetState("DashCategory", "Tired")`, with an uncategorized overload. Defer the final shape to Phase 14, which owns the loaded-element API — but build the application machinery here so Phase 14 only names it. |
| D71 | Implement interpolation? | **No.** `Time` is `0.0` in all 726 instructions repo-wide, so nothing exercises it, and `StateCodeGenerator.Interpolation.cs` is 775 lines. Parse `Time`, warn if non-zero. Revisit when a project actually uses it. |
| D72 | Report FRB1's silently-dropped state instruction (G71)? | **Yes — file it upstream.** An author wrote `StateThatSetsOtherState` expecting it to work; it does nothing. This is precisely the class of FRB1 bug the epic says to log. Report only; changing it is user-visible. |

---

## 8. Definition of done

- [x] `dotnet build` clean; `dotnet test` green (**1333**).
- [x] A real `PublishTrimmed` emits no IL warnings from `src/Glue`, and the trimmed binary runs
      correctly (Phase 2 G26).
- [x] Beefball's `PlayerBall` switches between `Tired` and `Rested` and the circle resizes.
- [x] An empty-instruction state assigns from defaults (G70) — the case a natural implementation
      gets wrong.
- [x] Initial-state ordering is proven: a variable declared after it wins (G72).
- [ ] `NamedObjectSave.CurrentState` ordering — blocked on nested entities, see §6.5.
- [x] Every gotcha in §5 is covered by a test or explicitly deferred.

---

## 9. What landed

7 new tests, full suite **1333 green**, build clean, trimmed publish verified by running it.

| Piece | File |
|---|---|
| State lookup, coverage rules, application | `src/Glue/GlueStateApplier.cs` |
| Routing a state-typed variable to the state | `src/Glue/GlueVariableApplier.cs` |
| `SetState` on both element types | `src/Glue/GlueScreen.cs` |

**The payoff:** `entity.SetState("DashCategory", "Tired")` resizes Beefball's cooldown circle to
zero and `"Rested"` restores it — and `Drag` is untouched by both, because the category excludes it.

### The phase is mostly reuse, which is why it is small

A state assigns a variable exactly the way an ordinary authored value does — same tunneling, same
overriding-type coercion, same three destinations. So applying one is: for each covered variable,
take the state's instruction if it has one and the variable's own default otherwise, then hand the
result to `GlueVariableApplier.ApplyOne`. The only genuinely new logic is deciding what a state
covers and which value each variable gets.

That reuse is what makes the empty-instruction case (G70) fall out correctly instead of needing to
be special-cased.

### Found while building

- **The initial state is not a separate mechanism.** It is an ordinary variable whose declared type
  names a category, so it applies inside Phase 3's loop, in list order. That is exactly what lets a
  variable declared after it override what it set — and it meant the state machinery had to be
  reachable *from* the variable applier rather than the other way round.
- **A variable typed as its own category has to be excluded**, or setting the state recurses into
  setting the state. FRB1 guards this explicitly; without it the failure is a stack overflow rather
  than a wrong value.

### New public API

`SetState(string)` and `SetState(string?, string)` on `GlueScreen` and `GlueEntity`, matching D70.
The final shape belongs to Phase 14, which owns the loaded-element API — this is the machinery with
a name attached, and the name is the part worth revisiting there.
