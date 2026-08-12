# Phase 12 — Platformer Movement

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — values reach the behaviour and input is bound; climbing deferred, see §9. |
| **Depends on** | Phase 4 (CSV files), Phase 11 (the CSV reader), Phase 3 (the slots are CustomVariables) |
| **Blocks** | Nothing |
| **Suggested branch** | `804-phase-12-platformer` |

---

## 1. The problem

DoorsDemo's player is a platformer character whose six movement profiles live in
`PlatformerValuesStatic.csv`. This is the phase that makes the primary fixture *playable*.

Same CSV shape as Phase 11, but the slot model is completely different: top-down has one current
value set chosen at init; platformer has **three named slots** (`GroundMovement`, `AirMovement`,
`AfterDoubleJump`) assigned by CustomVariable, plus a state machine that picks between them.

The issue flags this as "the one phase most likely to need new engine plumbing." It is not — FRB2's
`PlatformerBehavior` is a good fit. The real work is four unmappable columns and one that is **dead
in FRB1 but live in FRB2** (G124).

---

## 2. Scope

### In scope

1. `PlatformerValuesStatic.csv` → FRB2 `PlatformerValues`.
2. The `"<Row> in <File>.csv"` CustomVariable syntax that fills the three slots.
3. Climbing as FRB2's separate slot.
4. `IPlatformerEntity` on a loaded entity.

### Out of scope

- `CurrentMovementType` as an enum — FRB2 selects implicitly (G122).
- `IsUsingCustomDeceleration` / `CustomDecelerationValue` — no platformer equivalent in FRB2 (G123).
- `CloudFallThroughDistance` — FRB2's drop-through is distance-free (G123).
- Movement-swap-on-collision (`GroundPlatformerVariableName` etc. on a relationship) → Phase 9 G96.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | Values load | The player has the authored jump height and gravity. | §6.1 |
| F2 | Slots fill | Ground, air, and double-jump profiles land in the right slots. | §6.2 |
| F3 | Climbing works | The ladder profile is reachable. | §6.3 |
| F4 | The entity is a platformer | Collision dispatches ground-snap and slope probes to it. | §6.4 |

---

## 4. Proposed resolution

### Filling the slots

Unlike top-down's "first row wins", the three slots are explicit `CustomVariable`s. DoorsDemo's
`Player.glej`:

```json
{ "Name": "GroundMovement", "DefaultValue": "Ground in PlatformerValuesStatic.csv",
  "SetByDerived": true, "CreatesEvent": true }
{ "Name": "AirMovement",    "DefaultValue": "Air in PlatformerValuesStatic.csv", … }
{ "Name": "AfterDoubleJump" /* no DefaultValue */ … }
```

`GetAssignmentToCsvItem` (`CustomVariableCodeGenerator.cs:1286-1370`) decodes the string: strip
quotes, split on **`LastIndexOf(" in ")`**, use the suffix as a file hint. `"<NULL>"` → null.

`AfterDoubleJump` with no `DefaultValue` stays null, and FRB1 falls back to `AirMovement`
(`:362-369`) — which FRB2 does too (`PlatformerBehavior.cs:407-409`). Good parity.

### Field mapping

| CSV column | FRB2 `PlatformerValues` | Note |
|---|---|---|
| `MaxSpeedX` | `MaxSpeedX` | 1:1 |
| `AccelerationTimeX` | `AccelerationTimeX` | **→ `TimeSpan`** |
| `DecelerationTimeX` | `DecelerationTimeX` | **→ `TimeSpan`** |
| `Gravity` | `Gravity` | 1:1, positive-magnitude both sides |
| `MaxFallSpeed` | `MaxFallSpeed` | 1:1 |
| `JumpVelocity` | `JumpVelocity` | 1:1 |
| `JumpApplyLength` | `JumpApplyLength` | **→ `TimeSpan`** |
| `JumpApplyByButtonHold` | `JumpApplyByButtonHold` | 1:1 |
| `UphillFullSpeedSlope` | `UphillFullSpeedSlope` | `decimal` → `float` |
| `UphillStopSpeedSlope` | `UphillStopSpeedSlope` | `decimal` → `float` |
| `CanFallThroughCloudPlatforms` | `CanFallThroughOneWayCollision` | **renamed** |
| `MaxClimbingSpeed` | `ClimbingSpeed` | **renamed** |
| `CanClimb` | — | selects the `ClimbingMovement` slot (G125) |
| `UsesAcceleration` | — | zero both time fields (Phase 11 G112) |
| `DownhillFullSpeedSlope` | `DownhillFullSpeedSlope` | **G124** |
| `DownhillMaxSpeedSlope` | `DownhillMaxSpeedSlope` | **G124** |
| `DownhillMaxSpeedBoostPercentage` | `DownhillMaxSpeedMultiplier` | **renamed AND rescaled** — G124 |
| `MoveSameSpeedOnSlopes` | — | dead in FRB1 (G124) |
| `IsUsingCustomDeceleration` | — | G123 |
| `CustomDecelerationValue` | — | G123 |
| `CloudFallThroughDistance` | — | G123 |
| `InheritOrOverwriteAsInt` | — | editor-only |
| — | `SlopeSnapDistance`, `SlopeSnapMaxAngleDegrees`, `CoyoteTime`, `JumpInputBufferDuration` | FRB2-only |

---

## 5. Gotchas

### G120 — The two CSV name headers are spelled differently

Platformer writes `"Name (System.String, required)"`; top-down writes `"Name (string, required)"`.
Different generators — one splices `", required)"` onto the reflected type text
(`PlatformerPlugin/CodeGenerators/CsvGenerator.cs:90`), the other hardcodes the string
(`TopDownPlugin/DataGenerators/CsvGenerator.cs:186`).

Phase 11's reader already handles both. Pin it with a test here too, since this is the phase that
would break.

### G121 — There is no `Climbing` movement type

DoorsDemo's CSV has a `Climbing` row, and it is tempting to read that as a fourth state. It is not.

FRB1 models climbing as a **`CanClimb` flag on the ground slot's values**: `ApplyClimbingInput` runs
when `CurrentMovement.CanClimb` (`:531-534`), and while `CanClimb` the entity stays in
`MovementType.Ground` — the transition to `Air` is explicitly refused (`:661`). The `Climbing` row is
a *ground-slot* value set the game swaps in.

FRB2 has a real `ClimbingMovement` slot plus an `IsClimbing` bool
(`PlatformerBehavior.cs:61`, `:72`).

**How we tackle it.** A row with `CanClimb == true` becomes FRB2's `ClimbingMovement`. Cleaner than
FRB1, and it is what FRB2's model already assumes.

### G122 — `CurrentMovementType` is an enum in FRB1 and an expression in FRB2

FRB1 has `MovementType { Ground, Air, AfterDoubleJump }`, a property whose setter calls
`UpdateCurrentMovement()` (`:223-229`), and a per-frame `DetermineMovementValues()` state machine
(`:648-678`).

FRB2 has no enum and no state machine. Slot selection is one expression
(`PlatformerBehavior.cs:407-409`):

```csharp
IsClimbing ? ClimbingMovement!
           : (IsOnGround ? (GroundMovement ?? AirMovement)
                         : (_usedAirJumpSlot ? (AfterDoubleJump ?? AirMovement) : AirMovement))
```

**How we tackle it.** Do **not** port the enum. Fill the three slots and let FRB2 select. The
observable behaviour is the same, and porting a state machine into a data-driven loader would be
importing FRB1's implementation rather than its semantics.

Note the one side effect worth reproducing: FRB1's `UpdateCurrentMovement` sets
`YAcceleration = -Gravity`, or `0` when `CanClimb`. FRB2 handles gravity inside `Update`, so nothing
to do — but confirm it rather than assuming.

### G123 — Three columns have no FRB2 equivalent and change how the character feels

| Column | FRB1 behaviour | FRB2 |
|---|---|---|
| `IsUsingCustomDeceleration` / `CustomDecelerationValue` | runtime-active (`:500-503`) | **absent** — only `TopDownValues` has it |
| `CloudFallThroughDistance` | sets `cloudCollisionFallThroughY = Y - dist` and re-enables cloud collision below it (`:558-561`) | drop-through is distance-free (`IsSuppressingOneWayCollision`) |

DoorsDemo's `Ducking` row sets `IsUsingCustomDeceleration: True` with a value of `200`, so this is
exercised by the primary fixture.

**How we tackle it.** Warn by name. These are fidelity losses, not crashes, and warning is what makes
them visible instead of mysterious.

### G124 — Four columns are dead in FRB1 and one of them is live in FRB2 · **the sharp edge**

Exhaustive grep: `DownhillFullSpeedSlope`, `DownhillMaxSpeedSlope`,
`DownhillMaxSpeedBoostPercentage`, and `MoveSameSpeedOnSlopes` appear **only** in the model, the
view-model, the WPF view, and the predefined-values table. The only slope math in FRB1's generated
runtime is uphill (`EntityCodeGenerator.cs:456-472`).

**They are serialized but never read.**

FRB2's `DownhillMaxSpeedMultiplier` and `Downhill*Slope` **are** read. And the units differ:
FRB1 stores a *percentage* (DoorsDemo's `Ground` row: `50`), FRB2 a *multiplier* (default `1.5`).

So mapping `50 → 1.5` gives the loaded character a 50 % downhill speed boost **FRB1 never applied.**

**How we tackle it.** D120. Whichever way it goes, put a comment on it — this is the kind of
difference that gets "fixed" back the wrong way by someone reading only the field names.

### G125 — Gravity sign and fall-speed sign agree; do not "correct" them

FRB1 stores `Gravity` as a positive magnitude and applies `YAcceleration = -Gravity` (`:382`). FRB2
stores it positive too. `MaxFallSpeed` is a positive magnitude on both sides.

Recorded because FRB2 is Y-up and FRB1 is Y-up, so there is nothing to flip — and a reader who
half-remembers "the engines differ on Y" will introduce a sign bug looking for one.

### G126 — `CustomClasses` is dropped by FRB2's model, and it names the variable's type

The variable's declared type is `DoorsDemo.DataTypes.PlatformerValues`, which comes from the
`CustomClasses` array in the `.gluj`:

```json
{ "Name": "PlatformerValues",
  "CsvFilesUsingThis": [ "Entities/Player/PlatformerValuesStatic.csv" ],
  "RequiredProperties": [], "GenerateCode": true }
```

`src/Glue/Model/GlueProjectSave.cs` has **no `CustomClasses` member**, so this is dropped on read —
deliberately, since the epic excludes `CustomClasses` outright.

That exclusion is fine here **because the `" in <file>.csv"` suffix names the file directly.**
Resolve off the suffix and treat the `Type` string as opaque. Do not reintroduce `CustomClasses` to
resolve a type name this phase does not need.

Without a `CustomClassSave` the type name would be
`<Namespace>.DataTypes.<CsvFileNameWithoutExtension>` with a trailing `"File"` stripped
(`ReferencedFileSaveExtensionMethods.cs:93-100`) — recorded so nobody has to re-derive it.

### G127 — `IPlatformerEntity` is one property, and "never null" is the constraint

`src/Movement/IPlatformerEntity.cs:10-16` is `PlatformerBehavior Platformer { get; }`, whose XML doc
says it must never be null once registered, because collision dereferences it during ground-snap and
slope-probe dispatch.

With one CLR type, either every `GlueEntity` carries a `PlatformerBehavior` (cheap, but every
non-platformer entity has one), or collision learns to tolerate null.

**How we tackle it.** D121. This is the one place the issue's "most likely to need new engine
plumbing" prediction is nearly right — though the plumbing is an interface decision, not new
behaviour.

---

## 6. Tasks

Test-first throughout. The CSV reader comes from Phase 11.

### 6.1 — Value mapping

- [ ] Failing test: DoorsDemo's `Ground` row maps every 1:1 column.
- [ ] Failing test: the three time columns become `TimeSpan`.
- [ ] Failing test: `CanFallThroughCloudPlatforms` → `CanFallThroughOneWayCollision`.
- [ ] Failing test: `MaxClimbingSpeed` → `ClimbingSpeed`.
- [ ] Failing test: `UsesAcceleration: False` zeroes both time fields.
- [ ] Failing test: the `System.String` name header parses (G120).
- [ ] Failing test: each G123 column warns by name.

### 6.2 — Slots

- [ ] Failing test: `"Ground in PlatformerValuesStatic.csv"` fills `GroundMovement`.
- [ ] Failing test: the split is on the **last** `" in "`, so a row named `X in Y` still resolves.
- [ ] Failing test: `"<NULL>"` fills the slot with null.
- [ ] Failing test: `AfterDoubleJump` with no `DefaultValue` stays null and falls back to air.
- [ ] Failing test: the `Type` string is treated as opaque — a project with no `CustomClasses` entry
      still resolves (G126).

### 6.3 — Climbing

- [ ] Failing test: the `Climbing` row — `CanClimb: True` — becomes `ClimbingMovement`, not a fourth
      state (G121).
- [ ] Failing test: no `MovementType` enum is introduced (G122).

### 6.4 — The entity

- [ ] Decide D121 and record it.
- [ ] Failing test: a loaded platformer entity satisfies `IPlatformerEntity` with a non-null
      `Platformer`.
- [ ] Failing test: a non-platformer loaded entity does not break collision dispatch.
- [ ] Failing test: `IsPlatformer` in the bag is the discriminator.

### 6.5 — Wrap-up

- [ ] Vendor DoorsDemo's `PlatformerValuesStatic.csv` (Phase 4 G49).
- [ ] Decide D120 and comment the result in the mapping code.
- [ ] XML docs; update this document and `plan/plan.md`.
- [ ] Report FRB1's four dead CSV columns upstream (G124) — they are authored in the Glue UI and do
      nothing.

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D120 | Map `DownhillMaxSpeedBoostPercentage`, given it is dead in FRB1? | **Map it (`1 + pct/100`), and comment why.** FRB2's default is already `1.5` and DoorsDemo authors `50`, so the observable delta for the fixture is nil — and honouring the author's stated intent is the better default when FRB1's silence is a bug rather than a decision. Revisit if a project tuned around the dead behaviour. |
| D121 | How does a loaded entity satisfy `IPlatformerEntity`? | **`GlueEntity` implements it, with `Platformer` lazily created on first access.** Every entity pays one reference; only platformer entities pay a behavior. Rejected: teaching collision to tolerate null (weakens a contract that hand-written games rely on) and a separate `GluePlatformerEntity` type (a second loaded-entity CLR type undoes the epic's central premise). |
| D122 | Add `IsUsingCustomDeceleration` to FRB2's `PlatformerValues`? | **No.** `TopDownValues` has it because top-down needs it; the platformer's deceleration is already expressible through `DecelerationTimeX`. Warn (G123) rather than growing the config type to match a field-for-field port. |
| D123 | Port `CloudFallThroughDistance`? | **No — warn.** FRB2's `IsSuppressingOneWayCollision` is a cleaner model of the same intent. Revisit only if a real project's drop-through feels wrong. |

---

## 8. Definition of done

- [ ] `dotnet build` clean; `dotnet test` green.
- [ ] A real `PublishTrimmed` emits no IL warnings from `src/Glue` (Phase 2 G26).
- [ ] DoorsDemo's `Player` loads all six rows, with ground and air in the right slots.
- [ ] The `Climbing` row lands in `ClimbingMovement` (G121).
- [ ] No `MovementType` enum was introduced (G122).
- [ ] Every unmappable column warns by name (G123, G124).
- [ ] D120's choice is implemented **and commented in the code**, not only here.
- [ ] Every gotcha in §5 is covered by a test or explicitly deferred.

---

## 9. What landed

DoorsDemo's `Player` loads all six CSV rows and fills its ground and air movement slots from the
`"<Row> in <File>.csv"` variable syntax. `AfterDoubleJump` has no value, which is deliberate — it
falls back to air movement exactly as FRB1 does.

| Piece | File |
|---|---|
| CSV → `PlatformerValues` / `TopDownValues` | `src/Glue/GlueMovementValues.cs` |
| Slot filling from the row reference | `src/Glue/GlueVariableApplier.cs` |
| `IPlatformerEntity` on the loaded entity | `src/Glue/GlueScreen.cs` |

D121 resolved as recommended: `GlueEntity` implements `IPlatformerEntity` with a lazily created
behavior. Every loaded entity shares one type, so a non-platformer entity would otherwise carry a
behavior it never uses — but the interface promises non-null once registered, and collision
dereferences it during ground-snap dispatch, so it materialises rather than returning null.

Three mappings that are easy to get silently wrong, each with a test:

- **Durations become `TimeSpan`.** Glue stores plain seconds.
- **`DownhillMaxSpeedBoostPercentage` → `DownhillMaxSpeedMultiplier`** is renamed *and rescaled*:
  50 becomes 1.5. D120 chose to honour the author's intent even though the columns are dead in FRB1;
  the conversion is commented at the call site.
- **`UsesAcceleration: False` zeroes both durations**, because FRB2 has no such flag. Ignoring the
  column smooths movement the author wanted to snap — a feel change no assertion would catch.

**Input is now bound**, and the design decision it was waiting on resolved by reading Glue rather
than inventing a scheme. `EntitySave.InputDevice` is a three-value enum from Glue's
`EntityInputMovementPlugin` (`MainViewModel.InputDevice`), and its ordinals are the on-disk format:

| | | What FRB2 binds |
|---|---|---|
| `GamepadWithKeyboardFallback` | 0 — **the absent default** | gamepad left stick / A, combined with arrows / Space |
| `None` | 1 | nothing; the game wires its own |
| `ZeroInputDevice` | 2 | `I2DInput.Zero` / `IPressableInput.Zero` |

**The default is not "none".** An entity whose file says nothing about input still expects to be
driven, so treating an absent key as "no input" would leave most real entities motionless.

**FRB2 combines the two devices where Glue picks one.** Glue's generated `InitializeInput` tests
`Xbox360GamePads[0].IsConnected` once and binds a single device for the entity's life. FRB2's
`IGamepad` has no connection concept at all — a disconnected pad reads zero — so `Or`-combining
gamepad and keyboard reproduces the intent and improves on it: a controller plugged in mid-game takes
over without recreating the entity.

Binding happens in `GlueProject.CreateEntity`, and is skipped when there is no engine behind the
screen, since a test can build an entity with no input manager.

**Climbing is now wired, and the earlier "needs game logic" verdict was wrong about FRB2.**

FRB1 has no climbing *slot*: `CanClimb` is a per-row bool, and its generated code expects game code
to swap that row into the ground slot while the character is on a ladder — DoorsDemo's hand-written
`Player.cs` does exactly that by testing ladder overlap. Reading only FRB1 made this look like game
logic.

**FRB2 already owns the ladder state**, and more richly than FRB1: `PlatformerBehavior` has `Ladders`,
`Fences`, `IsClimbing`, `TopOfLadderY`, `ClimbingShape` and a real climb gate/exit state machine, and
it reads `ClimbingMovement` whenever `IsClimbing`. So the row identified by `CanClimb` simply fills
that slot — `GlueMovementValues.FindClimbingRow`. Filling it eagerly is safe: the slot is read only
while climbing, and `Update` throws if it ever climbs *without* one, so assigning it removes a
failure mode rather than adding behaviour.

What is still the game's call is handing the behaviour its `Ladders` — Glue records no association
between an entity and a ladder collection, so there is nothing to map.
