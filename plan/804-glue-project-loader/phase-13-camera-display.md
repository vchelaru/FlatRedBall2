# Phase 13 — Camera / Display Setup

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — see §9. `RangedAspectRatio` and `TextureFilter` diagnosed rather than supported. |
| **Depends on** | Phase 2 (the camera controller is a NamedObject), Phase 10 (its `Map` reference) |
| **Blocks** | Nothing |
| **Suggested branch** | `804-phase-13-camera-display` |

---

## 1. The problem

DoorsDemo is authored at 256×224 with an 8:7 aspect lock and a 300 % window scale. Loaded today it
runs at FRB2's default 1280×720 with no lock — so the pixel art is the wrong size and the framing is
wrong. Its camera also never follows the player, because `CameraControllingEntityInstance` is one of
the thirteen unmapped types.

The issue calls this "a translation layer, not new camera engine work," and that holds. Both sides
are small and mostly line up.

**Progress metric.** Drives DoorsDemo's unmapped count down by **1**.

---

## 2. Scope

### In scope

1. Glue's project-level `DisplaySettings` → FRB2's `DisplaySettings`.
2. `CameraControllingEntity` NamedObjects.
3. Resolving that controller's `Map` and `Targets` references.

### Out of scope

- `RangedAspectRatio` — no FRB2 equivalent, zero usage (G133).
- `TextureFilter` — no FRB2 knob; hardcoded `PointClamp` (G134).
- `ScaleGum` / `ResizeBehaviorGum` — redundant or unmappable (G135).
- The project-root display members — dead (G130).
- `ResolutionPresets` / `AllDisplaySettings` — editor-only, empty everywhere.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | The game is the right size | DoorsDemo runs at 256×224 in a 768×672 window. | §6.2 |
| F2 | Aspect is honoured | The 8:7 lock letterboxes instead of stretching. | §6.2 |
| F3 | The camera follows | The player stays framed as they move. | §6.3 |
| F4 | The camera stays in bounds | It stops at the map edges. | §6.3 |

---

## 4. Proposed resolution

### The mapping

| Glue `DisplaySettings` | FRB2 | Kind |
|---|---|---|
| `ResolutionWidth` / `ResolutionHeight` | same | direct |
| `Scale` (percent) | `PreferredWindowWidth` / `Height` | `Resolution × Scale/100` |
| `RunInFullScreen` | `WindowMode` | `true` → `FullscreenBorderless` |
| `AllowWindowResizing` | `AllowUserResizing` | renamed |
| `ResizeBehavior` | `ResizeMode` | **same member names and ordinals** |
| `DominantInternalCoordinates` | `DominantAxis` | **same member names and ordinals** |
| `AspectRatioBehavior == NoAspectRatio` | `AspectPolicy.Free` | + null `FixedAspectRatio` |
| `AspectRatioBehavior == FixedAspectRatio` | `AspectPolicy.Locked` + `FixedAspectRatio` | `(float)(W/H)` |
| `AspectRatioWidth` / `Height` | `FixedAspectRatio` | **only when behaviour ≠ None** — G132 |
| `RangedAspectRatio` | — | G133 |
| `TextureFilter` | — | G134 |
| `ScaleGum`, `ResizeBehaviorGum` | — | G135 |
| `Is2D`, `GenerateDisplayCode`, `Name`, `SupportLandscape/Portrait` | — | G136 |

`RunInFullScreen` → `FullscreenBorderless` is an exact match, not an approximation: FRB1's generated
code also uses borderless and never switches display mode
(`CameraSetupCodeGenerator.cs:753-788`).

### Worked example — DoorsDemo

`256×224`, `Scale: 300`, `AspectRatioBehavior: 1`, `8:7`, `DominantInternalCoordinates: 1` →
`ResolutionWidth = 256`, `ResolutionHeight = 224`, `PreferredWindowWidth = 768`,
`PreferredWindowHeight = 672`, `AspectPolicy.Locked`, `FixedAspectRatio ≈ 1.142857f`,
`DominantAxis.Height`, `ResizeMode.StretchVisibleArea` (absent → default), `Windowed`,
`AllowUserResizing = false`.

### `CameraControllingEntity`

FRB2 has its own (`src/Entities/CameraControllingEntity.cs`) and the properties are close to 1:1 —
but **Glue can only author nine of them**, and two use obsolete names (G137).

---

## 5. Gotchas

### G130 — `DisplaySettings` wins outright; the project-root members are dead

`CameraSetupCodeGenerator.cs:156` — `bool shouldGenerateNew = DisplaySettings != null;` — then a hard
branch (`:164-172`). The new path reads **only** `displaySettings.*`; the old path reads **only** the
root members.

**All 14 FRB1 `.gluj` and all three fixtures have a non-null `DisplaySettings`.**

Phase 1's G14 said `DisplaySettings` is "the authoritative one where the two disagree." That
understates it: **they are never merged; the root is simply unread.** Read `DisplaySettings` only,
and emit a diagnostic if it is null rather than falling back.

`OrthogonalWidth`/`OrthogonalHeight` are read only under the legacy path and only when
`SetOrthogonalResolution` is true — which no sample sets. Dead.

### G131 — FRB2's Glue-side `DisplaySettings` mirror has no constructor · **Blocker**

FRB1's `SetDefaults()` (`DisplaySettings.cs:78`) sets `Scale = 100`, `ScaleGum = 100`,
`DominantInternalCoordinates = Height`, `TextureFilter = Linear`, `GenerateDisplayCode = true`.
Newtonsoft omits members equal to their default.

`src/Glue/Model/DisplaySettings.cs` has **no constructor and no field initializers**, so an omitted
`Scale` reads `0` — and `Resolution × 0/100` is a zero-sized window — and an omitted
`DominantInternalCoordinates` reads `Width` when it should be `Height`.

Verified absent from **every** FRB1 `.gluj`: `ResizeBehavior`, `ResizeBehaviorGum`,
`RunInFullScreen`, `SupportPortrait`, `AspectRatioWidth2`, `AspectRatioHeight2`. So the omitted case
is the *normal* case.

The mirror is also missing `GenerateDisplayCode`, `AspectRatioWidth2`, `AspectRatioHeight2`,
`ResizeBehaviorGum`, `SupportLandscape`, `SupportPortrait`.

This is the **fourth** class where a member was modelled on the wrong side or without its default
(see Phase 4 G40, Phase 6 G60, Phase 7 G73). Phase 6's §6.0 audit should have caught it; if this
phase lands first, do the audit here.

### G132 — `AspectRatioWidth`/`Height` are meaningless unless the behaviour says otherwise

FRB1 only reads them when `AspectRatioBehavior != NoAspectRatio` (`:469-473`).

Beefball, ChickenClicker, and DialogBoxDemo all carry non-1:1 ratios (16:9, 16:9, 4:3) **with
`NoAspectRatio`** — stale UI leftovers. Reading them unconditionally letterboxes three projects that
should fill the window.

### G133 — `RangedAspectRatio` has no FRB2 equivalent, and nothing uses it

Semantics from the generated `EffectiveAspectRatio` (`:539-581`): with `min`/`max` from the two
width/height pairs, the effective ratio is `clamp(clientW/clientH, min, max)` — free-fill inside a
band, letterboxed outside.

FRB2's `AspectPolicy` is two-valued with no band.

**Across all 14 FRB1 `.gluj`: `AspectRatioBehavior` is absent (→ `NoAspectRatio`) in 12 and `1`
(`FixedAspectRatio`) in 2. `AspectRatioWidth2`/`Height2` never appear.**

**How we tackle it.** Map to `Locked` using the first pair, warn, and **do not add a third policy on
zero evidence.**

### G134 — `TextureFilter` has no FRB2 knob, but the practical gap is one project

FRB1 stores it as an **`int`** (`DisplaySettings.cs:76`) holding an XNA `TextureFilter` ordinal —
`Linear = 0`, `Point = 1`. It is applied globally at startup.

FRB2 hardcodes `SamplerState.PointClamp` at four call sites
(`ScreenSpaceBatch.cs:18`, `WorldSpaceBatch.cs:20`, `ScreenSpaceAddColorBatch.cs:22`,
`WorldSpaceAddColorBatch.cs:30`) with no field, property, or setting exposing it.

**13 of 14 FRB1 `.gluj` have `TextureFilter: 1` (Point) — already what FRB2 does.** One omits it.

**How we tackle it.** Warn when it is not `1`. Threading a `SamplerState` through `IRenderBatch.Begin`
is engine surgery that a loader should not drive; file it separately.

### G135 — FRB2's Gum canvas sync already covers `ResizeBehaviorGum`; `ScaleGum` has no equivalent

FRB1 generates `ResetGumResolutionValues()` (`:217-323`) setting the **global**
`GraphicalUiElement.CanvasWidth/Height` and the Gum renderer's zoom, branching on
`ResizeBehaviorGum` and dividing by `ScaleGum/100`.

FRB2 deliberately never touches the Gum global — the comment at `src/FlatRedBallService.cs:1147-1152`
says so — and instead sizes each root every draw frame from the camera's orthogonal extents.

So **`ResizeBehaviorGum` is redundant**: FRB2's per-root sizing already tracks the camera, which
already honours `ResizeMode`. **`ScaleGum` has no equivalent** — FRB2 has no independent Gum scale.

`ScaleGum == 100` in **14 of 14** projects and 3 of 3 fixtures, so the gap is theoretical. Ignore
both; warn if `ScaleGum != 100`.

### G136 — Three members are inert, and one is a real switch

`Name` (always `"Custom"`), `SupportLandscape`/`SupportPortrait` (mobile orientation, never reach
codegen) — inert.

`Is2D` — FRB2 is 2D-only; there is no perspective camera to configure. `true` in 14 of 14. Warn if
false.

**`GenerateDisplayCode: false` is a real behaviour switch** — it means "Glue emits nothing; the game
hand-writes its own camera setup" (`:197-201`). It should map to "this phase applies nothing."
`true` in 14 of 14, and **missing from FRB2's mirror.**

### G137 — Glue can author only nine camera properties, and two use obsolete names

The issue lists `TargetApproachStyle`, `IsActive`, `ViewableAreaMultiplier`, `LerpSmoothZoom`,
`IsKeepingTargetsInView` as "near-1:1". **None of them is authorable in Glue and none appears in any
`.glsj`.**

The complete authorable set is `MainCameraControllingEntityPlugin.cs:50-132` — exactly nine
`VariableDefinition`s: `Targets`, `Map`, `ExtraMapPadding`, `ScrollingWindowWidth`,
`ScrollingWindowHeight`, **`LerpSmooth`**, **`LerpCoefficient`**, `SnapToPixel`, `SnapToPixelOffset`.

`LerpSmooth` and `LerpCoefficient` are FRB1's **obsolete** names, kept deliberately — the source
comment at `:95-96` reads *"we cannot swap this because it would break existing projects."*

**How we tackle it.** Map `LerpSmooth: true → TargetApproachStyle.Smooth`, `false → Immediate`, and
`LerpCoefficient → TargetApproachCoefficient`. `ConstantSpeed` is unreachable from Glue data.

### G138 — `Targets` is a live alias in FRB1 and a get-only list in FRB2

FRB1: `public System.Collections.IList Targets { get; set; }` — **settable**, and Glue's generated
code *aliases the actual list*: `instance.Targets = PlayerList;`
(`MainCameraControllingEntityPlugin.cs:198`). Entities spawned later are followed automatically.

FRB2: `public List<Entity> Targets { get; } = new();` — get-only, so no aliasing. Copying at build
time means later-spawned entities are never followed.

The single-object case is already fine — FRB1 emits `Targets.Clear(); Targets.Add(x);` (`:202`),
which FRB2's `Target` setter matches exactly.

**How we tackle it.** D131.

### G139 — `Map` is a live reference in FRB1 and a snapshot in FRB2

FRB1's `Map` is `IPositionedSizedObject` and is re-read every frame, so moving the map moves the
clamp. FRB2's is a `BoundsRectangle?` — a readonly record struct, snapshotted.

Acceptable for TMX maps, which do not move. Record it rather than discover it.

Conversion: `TileMap.Bounds` (`src/Tiled/TileMap.cs:303`) already returns a `BoundsRectangle`.

Also gone: `SnapToPixelOffset` (FRB1 default `.25f`, added after rounding, `:698-699`) and
`CustomSnapToPixelZoom`. `SnapToPixelOffset` **is** authorable (G137), so a non-default value is a
visible half-pixel shift with nowhere to go. Warn.

**Ordering:** `CameraControllingEntityInstance` is declared **last** in all six platformer samples,
after `Map` and `PlayerList` — but nothing in the format requires it. Resolve references in a second
pass, exactly as Phase 9 does for relationships.

---

## 6. Tasks

Test-first throughout.

### 6.1 — Model completion

- [ ] Add the constructor defaults from `SetDefaults()` and the six missing members (G131).
- [ ] Failing test: a `DisplaySettings` deserialized from JSON that **omits** `Scale` reports `100`.
- [ ] Failing test: an omitted `DominantInternalCoordinates` reports `Height`.

### 6.2 — Display mapping

- [ ] Failing test: DoorsDemo maps to the worked example in §4, field by field.
- [ ] Failing test: `ResizeBehavior` and `DominantInternalCoordinates` cast safely, with both
      ordinals pinned on each side.
- [ ] Failing test: `AspectRatioWidth/Height` are ignored under `NoAspectRatio` — Beefball stays
      unlocked despite carrying 16:9 (G132).
- [ ] Failing test: `RangedAspectRatio` maps to `Locked` from the first pair, with a warning (G133).
- [ ] Failing test: `TextureFilter != 1` warns (G134).
- [ ] Failing test: `ScaleGum != 100` warns; `ResizeBehaviorGum` is silently ignored (G135).
- [ ] Failing test: `GenerateDisplayCode: false` applies nothing (G136).
- [ ] Failing test: a null `DisplaySettings` diagnoses rather than falling back to the root (G130).
- [ ] Apply via `FlatRedBallService.DisplaySettings` at load — **project-global, not per-screen.**
      Window properties are applied only on `Start`, not on `MoveToScreen`
      (`src/FlatRedBallService.cs:563-564`).

### 6.3 — Camera controller

- [ ] Failing test: `CameraControllingEntity` constructs and registers.
- [ ] Failing test: `LerpSmooth: true` → `TargetApproachStyle.Smooth`; `false` → `Immediate` (G137).
- [ ] Failing test: `LerpCoefficient` → `TargetApproachCoefficient`.
- [ ] Failing test: `Targets: "PlayerList"` resolves the named list, not a literal string.
- [ ] Failing test: `Map: "Map"` resolves the named TMX object to `BoundsRectangle` (G139).
- [ ] Failing test: a controller declared **before** its referents still resolves (G139).
- [ ] Failing test: a non-default `SnapToPixelOffset` warns (G139).
- [ ] Failing test: `ExtraMapPadding`, `ScrollingWindowWidth/Height`, `SnapToPixel` map directly.

### 6.4 — Wrap-up

- [ ] Failing test: DoorsDemo's unmapped count drops by 1.
- [ ] Decide D131 and implement.
- [ ] XML docs; update this document and `plan/plan.md`.
- [ ] Correct Phase 1's G14, which understates the root-vs-`DisplaySettings` relationship (G130).

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D130 | Where do display settings get applied? | **`FlatRedBallService.DisplaySettings` at load time.** Glue's model is project-global, and FRB2 applies window properties only on `Start`. A per-screen `PreferredDisplaySettings` override would need `GlueScreen` to carry project context it does not have — see Phase 14 §1. |
| D131 | Make FRB2's `Targets` settable so the loader can alias a live list? | **Yes — change it to accept an `IList<Entity>`.** Copying at build time silently breaks camera-follow for every spawned entity, which is the common case in a platformer. The engine change is small and benefits hand-written games. Rejected: pushing into the existing list on each spawn (couples the camera to the factory). |
| D132 | Add `RangedAspectRatio` support to FRB2? | **No** (G133). Zero projects use it. Adding a third `AspectPolicy` value on zero evidence is speculative API growth. |
| D133 | Expose `TextureFilter` in FRB2's `DisplaySettings`? | **Not in this phase.** File it as its own engine change — it means threading a `SamplerState` through four batch types. 13 of 14 projects already match FRB2's hardcoded behaviour (G134). |

---

## 8. Definition of done

- [ ] `dotnet build` clean; `dotnet test` green.
- [ ] A real `PublishTrimmed` emits no IL warnings from `src/Glue` (Phase 2 G26).
- [ ] DoorsDemo runs at 256×224 in a 768×672 window with an 8:7 lock.
- [ ] Beefball is **not** aspect-locked despite carrying a 16:9 ratio (G132).
- [ ] The camera follows `PlayerList` and clamps to the map.
- [ ] Every `DisplaySettings` default is asserted against JSON that omits it (G131).
- [ ] Every gotcha in §5 is covered by a test or explicitly deferred.

---

## 9. What landed

6 new tests, full suite **1372 green**. DoorsDemo now boots at its authored 256×224 scaled to
768×672, with the camera following the player through the level — confirmed by screenshot.

| Piece | File |
|---|---|
| Display block → FRB2 `DisplaySettings` | `src/Glue/GlueDisplayMapper.cs` |
| Camera controller wiring | `src/Glue/GlueCameraBuilder.cs` |
| Constructor defaults on the Glue mirror | `src/Glue/Model/DisplaySettings.cs` |
| `ApplyDisplaySettings` entry point | `src/Glue/GlueProject.cs` |

G131 was the blocker it was predicted to be: the mirror had no constructor, so an omitted `Scale`
read `0` and produced a zero-sized window, and an omitted `DominantInternalCoordinates` picked the
wrong axis. Both are now asserted against JSON that omits them.

G132 is covered too — Beefball carries a stale 16:9 ratio alongside `NoAspectRatio`, and reading it
unconditionally would letterbox a game that should fill the window.

G137 held exactly: Glue writes only `LerpSmooth` and `LerpCoefficient`, FRB1's obsolete names, and
`TargetApproachStyle` never appears on disk at all.

### Two ordering bugs found by running it

Neither would have been caught by the tests, which assert on `Objects` rather than on behaviour.

- **An `Entity`-typed object on a screen was never registered.** `AddTo(Screen)` only handled
  `IRenderable`, so the camera controller was built, correctly targeted — and never ran. Every
  engine entity on a screen had the same problem.
- **`Screen.Register` does not initialise.** The engine's own `Factory` does that as a separate
  step, so registering without it leaves an engine entity half-built: the camera resolves its
  `Camera` in `CustomInitialize` and threw a `NullReferenceException` on the screen's first frame.
  The call is guarded, because a test can build a screen with no engine behind it and an entity's
  initialiser is entitled to expect one.

### Diagnosed rather than approximated

`RangedAspectRatio` (no FRB2 band — locks to the first ratio and says so), `TextureFilter` other
than point (FRB2 hardcodes point sampling), `ScaleGum` other than 100%, and a project not authored
as 2D. Each warns by name. D132 and D133 stand: adding a third `AspectPolicy` value or threading a
`SamplerState` through four batch types on zero fixture evidence is speculative API growth.

**Not done:** D131's proposal to make FRB2's `Targets` settable so the loader can alias a live list.
Targets are copied at build time, so an entity spawned later is not followed. Recorded here rather
than silently accepted — it is a real divergence from FRB1, and the engine change is small when a
project needs it.
