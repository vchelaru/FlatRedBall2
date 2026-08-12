# Phase 11 — Top-Down Movement

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — values reach TopDownBehavior, first row is the default, input is bound. |
| **Depends on** | Phase 4 (the values are a referenced CSV file) |
| **Blocks** | Nothing |
| **Suggested branch** | `804-phase-11-topdown` |

---

## 1. The problem

A Glue entity marked top-down gets its tuning from a **per-entity CSV**, not from anything in the
`.glej`. FRB2 has `TopDownValues` and `TopDownBehavior` already, so this phase is a CSV reader plus a
field mapping.

The mapping is close to 1:1 but has three real traps: two type changes, two **inverted defaults**,
and one column whose absence silently changes how the character feels (G112).

**FRB2 has no CSV support of any kind** — the first task is writing one. It is shared with Phase 12.

---

## 2. Scope

### In scope

1. A CSV reader adequate for Glue's dialect (shared with Phase 12).
2. `TopDownValuesStatic.csv` → a name-keyed set of FRB2 `TopDownValues`.
3. Selecting the initial values.
4. `EntitySave.InputDevice` → FRB2 input wiring.
5. Unknown columns preserved.

### Out of scope

- `InheritOrOverwriteAsInt` — editor-side CSV generation only, never read at runtime.
- CSV inheritance across a derived entity's own CSV → Phase 6 territory; diagnose here.
- `GroundVelocity` / moving-platform frames — no FRB2 equivalent on the top-down path.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | CSVs parse | Glue's dialect — quoted headers, typed columns, comments — reads correctly. | §6.1 |
| F2 | Values map | A loaded character moves at its authored speed and acceleration. | §6.2 |
| F3 | The right set is chosen | The character starts in the movement the author intended. | §6.3 |
| F4 | Input is wired | The character responds to a gamepad, or a keyboard if none. | §6.4 |

---

## 4. Proposed resolution

### Finding the CSV

The path is derivable — `Content/<ElementName>/TopDownValuesStatic.csv`
(`TopDownPlugin/DataGenerators/CsvGenerator.cs:22-46`, with `TopDownValues.csv` below
`CsvInheritanceSupport`) — **but do not derive it.** It is present verbatim as a
`ReferencedFileSave` on the entity, and Phase 4 already resolves those.

The discriminator for "this entity is top-down" is a bag entry:
`element.Properties.GetValue<bool>("IsTopDown")` (`TopDownEntityPropertyLogic.cs:13-17`). Platformer's
is `"IsPlatformer"`.

### Header syntax

`Name (type[, required])`, parsed by `RuntimeCsvRepresentation` (`:1208-1242`, `:94-136`):

- The member name is the text before the first `(`, **with all whitespace removed first** — so
  `Max HP (int)` is member `MaxHP`.
- The type is the word after `(`; if that word is `required`, the *next* word is the type. So
  `Name (required, string)` parses too.
- The required column is the **dictionary key**; deserializing to a dictionary with none marked
  throws.

Both spellings occur in the wild — top-down writes `"Name (string, required)"`, platformer writes
`"Name (System.String, required)"`. Handle both.

### Field mapping

| CSV column | FRB2 `TopDownValues` | Note |
|---|---|---|
| `Name` | — | the dictionary key |
| `MaxSpeed` | `MaxSpeed` | 1:1 |
| `AccelerationTime` | `AccelerationTime` | **`float` seconds → `TimeSpan`** |
| `DecelerationTime` | `DecelerationTime` | **`float` seconds → `TimeSpan`** |
| `UpdateDirectionFromInput` | `UpdateDirectionFromInput` | **defaults invert** — G111 |
| `UpdateDirectionFromVelocity` | `UpdateDirectionFromVelocity` | **defaults invert** — G111 |
| `IsUsingCustomDeceleration` | `IsUsingCustomDeceleration` | 1:1 |
| `CustomDecelerationValue` | `CustomDecelerationValue` | FRB1 defaults `100`, FRB2 `0` |
| `UsesAcceleration` | — | **G112** |
| `InheritOrOverwriteAsInt` | — | editor-only |
| *(unknown)* | preserved | G113 |

Behaviour surface:

| FRB1 generated | FRB2 `TopDownBehavior` |
|---|---|
| `CurrentMovement` | `MovementValues` — **one slot, no dictionary** |
| `TopDownSpeedMultiplier` | `SpeedMultiplier` |
| `InputEnabled` | `IsInputEnabled` |
| `PossibleDirections` (forced `FourWay`) | `DirectionSnap` (defaults `EightWay`) — G114 |
| `MovementInput` | `MovementInput` |
| `DirectionFacing` | `DirectionFacing` |

---

## 5. Gotchas

### G110 — FRB2 has no CSV reader, and FRB1's is not portable

`grep -niE "csv" src/` finds one unrelated comment. FRB1's reader is `CsvFileManager` +
`RuntimeCsvRepresentation`, roughly 1300 reflection-driven lines that deserialize into codegen'd row
types. **Do not port it** — there are no codegen'd types to reflect onto.

What Glue's dialect actually needs:

- quote `"`, escape `""` (doubled, **not** backslash), comment `#` (`CsvReader.cs:86-96`);
- rows where every column is empty are dropped (`CsvFileManager.cs:152-167`);
- a row whose **first cell starts with `//`** is a comment (`RuntimeCsvRepresentation.cs:944-951`);
- a row whose **required cell is empty** is skipped (`:921`);
- rows are fixed-width — extra trailing columns are dropped, missing ones read `""`.

Note `CsvFileManager.Delimiter` is a **mutable public static** (`:21`). Do not reproduce that; it is
exactly the hazard `design/TODOS.md` already tracks for `TileMap.TmxLoader`.

### G111 — Two booleans have opposite defaults on the two sides

| Column | FRB1 default | FRB2 default |
|---|---|---|
| `UpdateDirectionFromInput` | `false` | **`true`** |
| `UpdateDirectionFromVelocity` | `true` | **`false`** |

Both are always written by Glue's generator, so the divergence only bites for a hand-edited or
truncated CSV — but that is precisely when a silent wrong default is hardest to spot.

**How we tackle it.** Set both explicitly from the CSV, always. Never rely on either side's default,
and add a test with a CSV that omits both columns.

### G112 — `UsesAcceleration` has no FRB2 field and changes how movement feels

FRB1: `!UsesAcceleration` forces instant velocity (`TopDownPlugin/CodeGenerators/EntityCodeGenerator.cs:254`).
FRB2 expresses the same thing as `AccelerationTime == TimeSpan.Zero`
(`src/Movement/TopDownBehavior.cs:100`).

So the mapping is: **`UsesAcceleration == false` → zero both time fields.** Ignoring the column
silently produces smoothed movement where FRB1 snapped — a feel change, not a crash, and therefore
one nobody notices in a test suite.

### G113 — `AdditionalValues` is an editor mechanism, and the FRB2 equivalent is a design choice

FRB1's `TopDownValues.AdditionalValues` (`TopDownPlugin/Models/TopDownValues.cs:37-40`) is a
`Dictionary<string, object>` holding columns not in the required set. **`PlatformerValues` has no
such member** — this is top-down-only.

But at *runtime* in FRB1 an extra column is not a dictionary entry at all: `CsvCodeGenerator`
generates a real property per column (`:605-626`), so game code writes `values.MyExtraColumn`.

FRB2 has no codegen, so a dictionary is the right shape — **but it is a design choice, not a port**,
and it should be presented as such rather than as fidelity.

### G114 — `PossibleDirections` is force-set on init and the FRB2 default differs

FRB1's generated `Initialize` hard-sets `PossibleDirections = FourWay` regardless of the CSV
(`EntityCodeGenerator.cs:136-158`). FRB2's `DirectionSnap` defaults to `EightWay`.

**How we tackle it.** Set `DirectionSnap = FourWay` explicitly on load, matching FRB1. Do not
inherit FRB2's default — the two disagree and FRB1's is what the project was authored against.

### G115 — "First dictionary entry" is FRB1's rule, but do not implement it that way

Generated selection (`EntityCodeGenerator.cs:136-158`):

```csharp
if (TopDownValues?.Count > 0) { mCurrentMovement = TopDownValues.Values.FirstOrDefault(); }
```

`Dictionary<string,T>` enumeration order happens to be insertion order here because
`CsvDeserializeDictionary` inserts in row order with no removals — but that is an implementation
detail of the CLR, not a guarantee.

**How we tackle it.** Select **CSV row 0** explicitly. Same result, no reliance on dictionary
ordering.

Note `ApplyMovementInput` early-outs when the current movement is null (`:187-190`), and FRB2's
`Update` early-outs on `MovementValues == null` (`TopDownBehavior.cs:53`). Good parity — an entity
with an empty CSV does not move rather than throwing.

### G116 — Column order in the file differs from the declared required-header order

`TopDownValuesCreationLogic.FillRequiredCsvHeaders` (`:31-71`) declares
`UpdateDirectionFromVelocity` **last**; the actual file has it third, because
`RuntimeCsvRepresentation.FromList` reflects property-declaration order.

**Parse by header name. Never by index.**

### G117 — `InputDevice` is bag-backed and one of its values is a latent FRB1 crash

`EntitySave.InputDevice` reads `Properties.GetValue<int>("InputDevice")`
(`EntityInputMovementPlugin/CodeGenerators/EntityCodeGenerator.cs:44`). Enum:
`GamepadWithKeyboardFallback = 0`, `None = 1`, `ZeroInputDevice = 2`.

FRB2's model is already correct (`src/Glue/Model/EntitySave.cs:39-40`), including the absent case —
`default(int) == 0` matches FRB1's `[DefaultValue]`.

Both typed and untyped forms occur on disk: Beefball writes `{"Name":"InputDevice","Value":0}` with
no `Type` key; DialogBoxDemo's `Npc` writes `"Type": "int"`. FRB2's `GetValue<T>` ignores `Type`
entirely — correct, and a fourth independent confirmation of Phase 1's G9.

Generated wiring (`:42-101`):

- `GamepadWithKeyboardFallback` — gamepad 0 if connected, else keyboard.
- `ZeroInputDevice` — a null-object input.
- `None` — an **empty** `InitializeInput()`. Top-down still appends `InputEnabled = true`;
  platformer does not.

**Latent FRB1 bug worth reporting:** `InitializePlatformerInput` (`:396-405`) does not null-guard,
so `InputDevice.None` plus platformer is a null-reference waiting to happen — FRB1 documents this
with a `#if DEBUG` throw at `:415-418` rather than fixing it.

---

## 6. Tasks

Test-first throughout. §6.1 is shared with Phase 12 — build it once.

### 6.1 — A CSV reader

- [ ] Failing test: quoted headers, `""` escaping, and `#` comments parse (G110).
- [ ] Failing test: `Name (string, required)` and `Name (System.String, required)` both yield a
      required key column.
- [ ] Failing test: `Name (required, string)` parses — the type is the second word.
- [ ] Failing test: `Max HP (int)` yields member `MaxHP` — whitespace stripped first.
- [ ] Failing test: an all-empty row, a `//` first cell, and an empty required cell are each
      skipped.
- [ ] Failing test: a row with too few columns pads; too many truncates.
- [ ] Failing test: no required column produces a clear error.
- [ ] Implement in `src/IO/` or similar — **not** in `src/Glue/`. It is a general reader.

### 6.2 — Value mapping

- [ ] Failing test: `AccelerationTime` seconds become a `TimeSpan`.
- [ ] Failing test: both direction booleans are set explicitly, from a CSV that omits them (G111).
- [ ] Failing test: `UsesAcceleration: False` zeroes both time fields (G112).
- [ ] Failing test: parsing is by header name, proven with a reordered CSV (G116).
- [ ] Failing test: an unknown column is preserved (G113).

### 6.3 — Selection

- [ ] Failing test: CSV row 0 becomes the initial values (G115).
- [ ] Failing test: `DirectionSnap` is `FourWay`, not FRB2's default (G114).
- [ ] Failing test: an empty CSV leaves the entity motionless without throwing.

### 6.4 — Input

- [ ] Failing test: `InputDevice: 0` wires gamepad-with-keyboard-fallback.
- [ ] Failing test: `InputDevice: 1` wires nothing and does not throw.
- [ ] Failing test: an absent `InputDevice` key reads `0` (G117).

### 6.5 — Fixtures and wrap-up

- [ ] Vendor `TopDownMovementEntity.glej` + its CSV from FRB1's test project — **no sample uses
      top-down**, so this is the only source. Note the short-form `SourceClassType` caveat in
      `plan/plan.md`.
- [ ] Vendor `TopDownMovementEntityDerived` for the CSV-inheritance case, and diagnose it if Phase 6
      has not landed.
- [ ] XML docs; update this document and `plan/plan.md`.
- [ ] Report FRB1's `InitializePlatformerInput` null-guard gap upstream (G117).

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D110 | Where does the CSV reader live? | **`src/IO/`, public.** It is a general-purpose reader that hand-written games can use for their own data tables, and burying it in `src/Glue/` would make it look loader-specific. Shared with Phase 12. |
| D111 | Reproduce `AdditionalValues`? | **Yes, as a dictionary — and say plainly it is a design choice.** FRB1's runtime shape is a generated property, which this epic cannot reproduce. A dictionary is the honest data-driven equivalent (G113). |
| D112 | Add `UsesAcceleration` to FRB2's `TopDownValues`? | **No.** `AccelerationTime == Zero` already expresses it, and adding a second way to say the same thing is how a config type rots. Map it at load (G112). |
| D113 | Support per-derived-entity CSV inheritance? | **Defer to Phase 6.** `InheritOrOverwriteAsInt` is editor-side, but the *file* resolution for a derived entity is a real inheritance question. Diagnose until Phase 6 lands. |

---

## 8. Definition of done

- [ ] `dotnet build` clean; `dotnet test` green.
- [ ] A real `PublishTrimmed` emits no IL warnings from `src/Glue` (Phase 2 G26).
- [ ] A vendored top-down entity loads its CSV and moves at the authored speed.
- [ ] Both direction booleans are set explicitly (G111) and `UsesAcceleration` is honoured (G112).
- [ ] The CSV reader handles every dialect case in §6.1 and lives outside `src/Glue/`.
- [ ] Every gotcha in §5 is covered by a test or explicitly deferred.

---

## 9. What landed

The CSV reader (§6.1) and the value mapping (§6.2) are done and shared with Phase 12.
`src/IO/CsvTable.cs` is a general reader in the engine rather than a loader-private one, per D110 —
a game with its own data tables wants the same thing.

Covered by tests: both `Name (...)` spellings, either order of `type` and `required`, whitespace
stripped from member names before truncation, `#` and `//` comments, rows narrower or wider than the
header, and quoted fields with embedded commas and doubled quotes.

G111 and G112 are both implemented: the two direction booleans are read explicitly because they
default the *opposite* way on the two sides, and `UsesAcceleration: False` zeroes both durations
because FRB2 expresses "no easing" as a zero duration rather than a flag.

**Now wired.** `GlueEntity.TopDown` is a lazily created `TopDownBehavior`, for the same reason
`Platformer` is — every loaded entity shares one type, so an eager one would burden every
non-top-down entity with a behaviour it never uses.

**The first-row default is the first dictionary *entry*, because `TopDownValues` has no `Name`.**
The name lives in the key of the dictionary `ReadTopDown` returns, which is why
`SetTopDownMovementSet` takes a dictionary rather than a sequence. Glue's generated code defaults to
the first entry, so an entity with a movement CSV and no explicit selection still moves.

`SetTopDownMovement(name)` picks another by name, case-insensitively, and **leaves the current values
alone when the name is unknown** — a data-driven name has no compiler checking it, and a typo that
stopped the entity dead would read as a movement bug rather than a bad string.

**Input is bound from `EntitySave.InputDevice`; see Phase 12 §9 for the device mapping**, which both
phases share.

### Corrected after review — two errors in the first draft of this section

**1. The values were never actually loaded.** `ReadTopDown` and `ApplyTopDown` had *no production
call site*: only tests called them, and `GlueEntity.BuildObjects` never read `IsTopDown` at all. The
mapping and its tests were real, but nothing invoked them, so a real top-down entity loaded with no
movement values and nothing said so. `GlueEntity.LoadTopDownValues` now closes the path, and
`BuildObjects_ATopDownEntity_LoadsItsMovementValuesFromItsCsv` reads a real FRB1 entity from disk.

**Discovery has to start from the property, not from a variable.** The platformer slots arrive as
`CustomVariable`s naming a row (`"Ground in X.csv"`), so they resolve through the variable applier.
Top-down has no such variable — Glue records only `IsTopDown` and loads the whole CSV — which is
exactly why the top-down path was missed while the platformer one worked.

**2. `DirectionSnap` is not recorded per-entity, and this section previously claimed it was** —
contradicting §5 G114 on this same page, which was right. Glue's `TopDownPlugin` persists exactly one
per-entity key, `IsTopDown`; `PossibleDirections` appears only in codegen, where generated
`Initialize` hard-sets `FourWay` unconditionally, ignoring the CSV. FRB2 defaults to `EightWay`, so a
loaded entity keeping that default would move diagonally where the same project does not in FRB1.
`GlueEntity.TopDown` therefore constructs with `FourWay`; the engine default is untouched for
hand-written entities. Note the ordinals do not correspond (FRB1 `LeftRight`/`FourWay`/`EightWay` =
0/1/2, FRB2 `FourWay`/`EightWay` = 0/1) — irrelevant while nothing is serialized, and a trap if Glue
ever starts persisting it.

**A real top-down fixture is now vendored.** `tests/FlatRedBall2.Tests/Glue/Fixtures/TopDownProject/`
carries `TopDownMovementEntity.glej` and its `TopDownValuesStatic.csv` copied byte-for-byte from
FRB1's `Tests/TestProjectDesktopNet6`, with a `.gluj` whose only edit is trimming the reference lists
to the one vendored entity (the source project references 226 elements). That entity declares no
`NamedObjects`, so the short-form `SourceClassType` problem that blocks vendoring the rest of that
project does not arise here.
