# Phase 1 — Foundations + Screens/Entities Skeleton

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — see §9. |
| **Depends on** | Nothing — this is the base of the epic |
| **Blocks** | Every other phase (2–14) |
| **Suggested branch** | `804-phase-1-glue-loader-foundations` |

---

## 1. The issue

Today Glue generates C# that FRB1 compiles. Issue #804 flips that relationship: **FRB2 reads the
JSON project files Glue already produces and builds Screens/Entities from them directly, at
runtime, via reflection — no codegen step.**

Phase 1 delivers the foundation everything else stands on: read the files, resolve the element
graph, decode the value-bag primitives, and boot the start-up Screen. It deliberately produces
*empty* Screens and Entities — no `NamedObjects` are instantiated (Phase 2), no `CustomVariables`
are applied (Phase 3), no files are loaded (Phase 4). "It loaded and the right Screen is active"
is the whole bar.

### Ground rules inherited from the epic

- **Latest `FileVersion` only.** No replication of `GluxVersions` gating history. FRB1's current
  `LatestVersion` is **68** (`GluxVersions.GumHasFrbRuntimeInterfaces`, July 26 2026) — see
  `FRBDK/Glue/GlueCommon/SaveClasses/GlueProjectSave.cs:219`.

  **The issue's phrasing of this rule rests on a mistaken premise** and should be read for intent,
  not literally. It says projects "must be opened/re-saved in current Glue first" — but re-saving
  does **not** bump `FileVersion` (proven in G1). The workable intent is: *the FRB2 reader
  implements one schema — the current one — and carries no version-gated branches.* That is
  satisfied by choosing fixtures above the last file-shape-affecting gate, not by upgrading anything.
- **Fully data-driven, no required user base class.** Loaded elements are generic objects;
  `CustomVariables` are a reflected property bag. Behavior hooks in via external registration, not
  subclassing.
- **No `CustomClasses` support.** Explicitly out of scope for the whole epic.
- **Package boundary: core.** The loader lives in `src/FlatRedBall2.csproj`, not a separate opt-in
  package — consistent with Gum/Tiled integration.
- **Test fixtures are vendored.** Commit a snapshot of representative FRB1 sample files into this
  repo rather than reading live from the sibling `FlatRedBall` checkout.
- **FRB1 changes: zero-impact only, and none are required.** Both repos are ours, but FRB1 has real
  users. **Zero-impact** changes — comments, docs, issues, and fixes that are already semantically
  no-ops (G2's duplicate key: last-one-wins means deleting the stale line changes nothing anyone
  observes) — land freely as standalone PRs. **User-visible** changes — behavior, regenerated
  samples, `FileVersion` bumps, anything altering what Glue writes — get reported, not landed here.

  Every problem in §5 is fixable FRB2-side or resolvable by fixture choice, so nothing in §7 blocks
  on an upstream merge. Glue also keeps targeting FRB1 `.csproj` projects throughout; this epic never
  changes what Glue *generates*.

### Where FRB1 lives

`C:\git\flatredball` — a sibling checkout, not a submodule. Reference files for this phase:

| What | Path (relative to the FRB1 repo) |
|---|---|
| Every save class being mirrored | `FRBDK/Glue/GlueCommon/SaveClasses/` — `GlueProjectSave`, `GlueElement`, `ScreenSave`, `EntitySave`, `NamedObjectSave`, `PropertySave`, `CustomVariable`, … |
| **The load path to mirror** | `FRBDK/Glue/Glue/Extensions/GlueProjectSaveExtensions.cs:514` (`Load`) and `:567` (`LoadReferencedScreensAndEntities`) |
| Version enum / skill | `FRBDK/Glue/.claude/skills/gluj-versions/SKILL.md` |

Line numbers throughout this document were verified at FRB1 commit `7346ae1cd`. The checkout moved
once mid-planning (that is G17) — spot-check before trusting any of them cold.

---

## 2. Scope

### In scope

1. JSON read pipeline for `.gluj` / `.glsj` / `.glej` on a FRB2-side POCO mirror.
2. `ScreenReferences` / `EntityReferences` resolution → load the matching element files.
3. `PropertySave` value decoding (the `GetValue<T>` equivalent), including raw-int enums.
4. Type-mapping table: Glue type strings → FRB2 types, with a decided fail-fast/skip policy.
5. `StartUpScreen` boots into FRB2's screen system as an empty `GlueScreen`.
6. Vendored test fixtures + the `tests/FlatRedBall2.Tests/Glue/` convention.

### Out of scope (later phases, do not creep)

- Instantiating `NamedObjects` (Phase 2) — Phase 1 only *parses and retains* them.
- Applying `CustomVariables` / `InstructionSaves` (Phase 3).
- Loading any referenced file, `.gumx` included (Phases 4–5).
- Inheritance merge (Phase 6) — `BaseScreen`/`BaseEntity` are parsed and retained, not resolved.
- States (Phase 7), factories (Phase 8), collision (Phase 9), TMX (Phase 10), movement (11–12),
  display settings (Phase 13), the name-based navigation API (Phase 14).
- `CustomClasses`, `Events`/`EventResponseSave`, `SyncedProjects`, `PerformanceSettingsSave`,
  `ResolutionPresets`, historical `FileVersion` support.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | Load a project from a path | Point the loader at a `.gluj` and get the whole element graph resolved, so later phases have something to walk. Never touches `System.IO` directly, so it runs in the browser too. | §7.4 |
| F2 | Read the value bag without guessing | One helper returns the right CLR type, so no call site writes `is long ? (int)` casts. | §7.3 |
| F3 | Know what failed, and keep going | An unmapped type reports a diagnostic instead of an exception that hides the 90% that loaded fine — the difference between a usable loader and an unusable one during a 14-phase build. | §7.4 |
| F4 | Boot the start-up Screen | One call with a `.gluj` path starts the game on the right Screen, so "does the loader work?" is answerable by running it. | §7.6 |
| F5 | A repeatable fixture convention | Phase 2 adds a fixture instead of inventing a scheme. | §7.1 |

---

## 4. Proposed resolution (high level)

### Reader

Mirror FRB1's `Load` flow exactly, because the file layout is defined by that code:

1. Deserialize `<name>.gluj` into `GlueProjectSave`.
2. For each `ScreenReferences[i].Name`, read `<glujDirectory>/<Name>.glsj` → `ScreenSave`.
3. For each `EntityReferences[i].Name`, read `<glujDirectory>/<Name>.glej` → `EntitySave`.
4. Populate `Screens` / `Entities`; clear the reference lists.

`Name` is a project-relative, **backslash-separated** path with no extension
(`"Screens\\MenuScreen"`), so the file is `<glujDir>/Screens/MenuScreen.glsj`.

### Serializer choice: `System.Text.Json` with source generation

FRB2 has no Newtonsoft dependency and already uses STJ with source-generated contexts —
`src/Movement/TopDownConfig.cs:40` and `src/Movement/PlatformerConfig.cs:43` are the pattern to
copy. Source generation is not optional: `src/FlatRedBall2.csproj` sets `IsAotCompatible=true`, and
reflection-based STJ would break that.

Two of the epic's stated landmines soften under STJ:

- **Boxed `long`/`double`.** Newtonsoft boxes `object`-typed values as `long`/`double` even for
  int/float fields, which is why FRB1 needs the cast ladder in
  `PropertySaveListExtensions.GetValue<T>` (`PropertySave.cs:72`). STJ deserializes `object` to
  `JsonElement` instead. **Type the mirror's `Value` as `JsonElement`** and the ambiguity becomes an
  explicit, testable decode step rather than a silent cast.
- **Raw-int enums.** STJ converts an int to an enum natively for enum-typed properties, so
  `"SourceType": 2` binds correctly with no custom converter. The int→enum work is only needed
  inside the value bag, where the target type is not known statically.

### Namespace and file layout

```
src/Glue/
  GlueProjectLoader.cs        entry point + the read seam
  GlueLoadResult.cs           loaded project + diagnostics
  GlueLoadDiagnostic.cs       severity, element name, message
  GlueTypeMap.cs              Glue type string -> FRB2 Type
  GlueScreen.cs               Screen subclass built from a ScreenSave (skeleton this phase)
  GlueEntity.cs               Entity subclass built from an EntitySave (skeleton this phase)
  PropertySaveExtensions.cs   GetValue<T> over JsonElement
  GlueJsonContext.cs          [JsonSerializable] source-gen context
  Model/                      POCO mirror: GlueProjectSave, ScreenSave, EntitySave, GlueElement,
                              NamedObjectSave, CustomVariable, ReferencedFileSave, PropertySave,
                              InstructionSave, StateSave, StateSaveCategory,
                              GlueElementFileReference, DisplaySettings
```

**Keep FRB1's type names verbatim** in `Model/` (`GlueProjectSave`, not `GlueProjectData`). The
JSON property names are fixed by FRB1 anyway, and matching names make the two codebases
cross-referenceable during a 14-phase port. `namespace FlatRedBall2.Glue.Model` prevents collision.

The mirror is **trimmed**: only fields Phases 1–14 actually consume. Editor-only metadata
(`IsHiddenInTreeView`, `Bookmarks`, `PluginData`, `Tags`) is omitted. STJ ignores unknown JSON
members by default, so omission is safe.

### The read seam

The engine already has this pattern: `TileMap.TmxLoader` (`src/Tiled/TileMap.cs:52`) is an injectable
delegate specifically so tests never touch disk and WASM can route through `TitleContainer` (see the
comment at `tests/FlatRedBall2.Tests/Tiled/TileMapLoadingTests.cs:9`). `GlueProjectLoader` needs the
same seam: a `Func<string, string> TextLoader` defaulting to `File.ReadAllText`, plus a
`Func<string, bool> FileExists`.

**But do not copy its shape uncritically.** `TmxLoader` is `internal static ... { get; set; }` —
mutable static state, which CLAUDE.md's architecture rules forbid ("No static state: only
`FlatRedBallService.Default` is static") and which leaks across tests unless each restores it in a
`finally`. See D9.

### Failure policy: collect, don't throw

`GlueProjectLoader.Load(path)` returns a `GlueLoadResult` carrying the project plus a diagnostic
list. Missing element files, unmapped type strings, and unparseable elements each add a diagnostic
and continue. A `GlueLoadOptions.Strict` flag throws on the first `Error` for callers who want
fail-fast.

Rationale in G19: under fail-fast, Phase 1 could not load its own primary fixture. FRB1 already
tolerates missing and corrupt element files (`GlueProjectSaveExtensions.cs:574`, `:584`) — this is
the same posture with better reporting.

### Boot

`GlueProjectLoader.Start(FlatRedBallService, glujPath)` loads, resolves `StartUpScreen` against the
loaded `Screens`, and calls the service's existing start path with a `GlueScreen` configured from
that `ScreenSave`. `GlueScreen` this phase is a `Screen` subclass holding its `ScreenSave` and
nothing else — later phases fill in `CustomInitialize`.

---

## 5. Gotchas and problems — and how we tackle each

Everything below was verified against FRB1 source or real committed sample files, not inferred.
**Fix column** says which repo owns the resolution: `FRB2` = handle it in the loader, `FRB1` = fix
it at the source in the sibling `FlatRedBall` checkout (see the ground rule in §1), `Both` = needs a
change on each side.

**No entry below requires a user-visible FRB1 change.** Two are worth a zero-impact upstream fix
(G2's corrupt file, G11's append-only comment); the rest are FRB2-side or resolved by fixture choice.

| # | Problem | Severity | Fix |
|---|---|---|---|
| G1 | No FRB1 sample is at `LatestVersion`, and re-saving will not fix that | High | FRB2 (fixture choice) |
| G2 | `BeefballWeb.gluj` has a duplicate `FileVersion` key | High | FRB1 (zero-impact) |
| G17 | `LatestVersion` is a moving target — it advanced 67 → 68 mid-planning | Medium | FRB2 |
| G3 | Absent ≠ `false` — true-by-default members are omitted on write | **Blocker** | FRB2 |
| G4 | 31 semantically-important members are `[JsonIgnore]` bag-backed and never appear by name | **Blocker** | FRB2 |
| G5 | `[DefaultValue(true)]` disagrees with FRB1's own read path | Medium | FRB1 (report only) |
| G6 | `.gluj` and element files are written with different serializer settings | Medium | FRB2 |
| G7 | Element names are backslash-separated | High | FRB2 |
| G8 | Case sensitivity is unspecified | Medium | FRB2 |
| G9 | `PropertySave.Type` is not a CLR type name, and is sometimes absent | Medium | FRB2 |
| G10 | Same data lives in a typed property *and* the bag on the same object | Medium | FRB2 |
| G11 | Enum values are raw ints with no cross-repo compile-time link | High | FRB2 + a zero-impact FRB1 comment |
| G12 | FRB1 silently swallows missing and corrupt element files | Low | FRB2 |
| G13 | An element's `Name` field can disagree with its file path | Low | FRB2 |
| G14 | Display/camera data is duplicated at the project root and in `DisplaySettings` | Low | FRB2 |
| G15 | `ShouldSerialize*` and `[JsonIgnore]` are Newtonsoft-only | Low | FRB2 |
| G16 | Real files contain shapes this epic excludes | Low | FRB2 |
| G18 | Type strings are generic — exact-string matching cannot work | High | FRB2 |
| G19 | The Phase 1 fixture is mostly *un*mappable in Phase 1, by design | Medium | FRB2 |

---

### G1 — No FRB1 sample is at `LatestVersion`, and re-saving will not fix that · High · **fix by fixture choice, no FRB1 change**

`GlueProjectSave.LatestVersion` is **68** (`GlueProjectSave.cs:219`). Every committed sample is
below it:

| Version | Projects |
|---|---|
| 37 | `AdMobFrb` |
| 42 | `Beefball`, `BeefballKni`, `ChickenClicker` |
| 53 | `SkiaSampleProject` |
| 54 | `Tests/TestProjectDesktopNet6` |
| 55 | `BeefballWeb` |
| 60 | all six `Platformer/*Demo` projects |
| 61 | `FormsSampleProject` (the newest anything gets) |

The two fixtures the epic names — ChickenClicker and Beefball — are **26 versions stale**.

**Re-saving does not upgrade a project.** The only `FileVersion` assignment in the entire FRBDK
outside tests is `ProjectLoader.cs:157`, and it sits in the `!glueProjectFile.Exists()` branch —
*new projects only*. Open a version 42 project in current Glue, change something, save: it is still
version 42. Raising it is a deliberate manual edit, and `GlueProjectFileVersionPlugin` raises an
error when a `.gluj`'s version exceeds the linked engine's `EngineDllSyntaxVersion`, so bumping a
sample's version without also upgrading its engine reference **puts a visible error in front of any
user who opens that sample**. Upgrading the samples is not a re-save; it is an invasive,
user-visible change to FRB1, and this epic does not need it.

**How we tackle it: pick better fixtures.** Most `GluxVersions` entries gate engine and codegen
capability, not file shape. The ones that actually change what lands on disk are
`GlueSavedToJson = 9`, `SeparateJsonFilesForElements = 11`, `RemoveRedundantDerivedData = 38`,
`VariantsInsteadOfTypes = 53`, and `CaseSensitiveLoading = 55` — **all ≤ 55**. So any sample at 60
or 61 is already above every file-shape-affecting gate, and reads identically to a version 68 file
for everything Phase 1 touches.

`Samples/Platformer/DoorsDemo` (version 60) is the right primary fixture — and it is strictly better
than what the epic proposed:

| | ChickenClicker (v42) | DoorsDemo (v60) |
|---|---|---|
| Version vs. schema gates | below `RemoveRedundantDerivedData`, `VariantsInsteadOfTypes`, `CaseSensitiveLoading` | above all of them |
| Entities | none | `Entities/Door.glej`, `Entities/Player.glej` |
| Screens | 3, all near-empty | `Screens/GameScreen.glsj`, `Screens/Level1.glsj` |
| Inheritance | none | `Level1` sets `BaseScreen` — the Phase 6 case the epic asks for |

Vendor `DoorsDemo` as the primary fixture and `FormsSampleProject` (61, screens-only, Gum-heavy) as
the Phase 5 fixture. Keep ChickenClicker and Beefball out of the fixture set entirely, or vendor one
deliberately as a *legacy-tolerance* fixture — a v42 file is useful precisely because it sits below
those three gates and proves the loader degrades with a diagnostic rather than misreading.

Zero FRB1 changes. Nothing blocks on an upstream merge.

---

### G2 — `BeefballWeb.gluj` has a duplicate `FileVersion` key · High · fix in FRB1 (zero-impact)

`Samples/BeefballWeb/BeefballWeb/BeefballWeb.gluj` lines 37–38:

```json
  "FileVersion": 42,
  "FileVersion": 55,
```

A committed, corrupt file — almost certainly a bad merge. Newtonsoft takes last-one-wins silently,
which is why nobody noticed.

**How we tackle it.** This is the model zero-impact FRB1 fix: because Newtonsoft already resolves it
to `55`, deleting the stale `42` line changes nothing any consumer observes, and it removes a file
that would confuse the next person to read it. Land it upstream as a one-line PR, independent of
this epic — nothing here waits on it.

On the FRB2 side, add a test pinning what `System.Text.Json` does with a duplicate key rather than
assuming it matches Newtonsoft. Also do the cheap thing once: sweep every `.gluj`/`.glsj`/`.glej` in
FRB1 for well-formedness and duplicate keys. Report what turns up; fix only what is provably a
no-op, and leave anything with observable consequences to the maintainer.

---

### G17 — `LatestVersion` is a moving target · Medium · fix in FRB2

`LatestVersion` advanced from **67** to **68** during the few hours this plan was being written —
`GumHasFrbRuntimeInterfaces` (July 26 2026) landed in the FRB1 checkout mid-research, and an earlier
section of this very document had to be corrected. The `gluj-versions` skill documents adding
versions as routine maintenance, so this cadence is normal, not exceptional.

**How we tackle it.** Never hardcode a version constant in loader logic. `LatestVersion` belongs in
exactly one place in FRB2 — a single constant next to the mirrored `GluxVersions`, used only for the
"below current version" diagnostic (D6) and nothing else. Because the reader is version-agnostic by
design (one schema, no gated branches), a version bump upstream should require **zero** FRB2 changes
beyond optionally refreshing that constant.

This is also the strongest argument against blocking on version (D6): a hard gate would have turned
this routine upstream bump into total test failure with no useful diagnostic. Test the diagnostic
against a synthetic fixture rather than a real sample, so the corpus drifting does not break tests.

---

### G3 — Absent ≠ `false` · **Blocker** · fix in FRB2

Element files are written with `DefaultValueHandling.Ignore` (`GlueProjectSaveExtensions.cs:434`),
so any member equal to its default is **omitted from disk**. `NamedObjectSave` restores its
true-by-default members in the **constructor** (`NamedObjectSave.cs:828`): `Instantiate`,
`AddToManagers`, `IncludeInICollidable`, `IncludeInIClickable`, `CallActivity`, `GenerateTimedEmit`.

A naive POCO with `bool Instantiate { get; set; }` therefore reads back `false` for **every object in
every real project** — and the failure is silent, producing an empty scene rather than an exception.

**How we tackle it.** Reproduce every constructor default in the mirror's parameterless constructor.
STJ calls it, so the behavior matches Newtonsoft exactly. Cover it with a test that deserializes JSON
which *omits* the member and asserts the default survived — a test asserting the default on a
`new NamedObjectSave()` would pass while the real bug shipped.

Keep the counter-example straight: `AttachToContainer` is *deliberately* left out of the constructor
(see the comment at `NamedObjectSave.cs:838`) and is written explicitly — `PlayerBall.glej:189` has
`"AttachToContainer": true`. Copying the constructor wholesale is right; extrapolating "all bools
default true" is wrong.

---

### G4 — Bag-backed members never appear by name · **Blocker** · fix in FRB2

A large share of the semantically important members are `[JsonIgnore]` accessors over the
`Properties` bag, so **they do not exist in the JSON under their own names at all**:

| Save class | Bag-backed getters |
|---|---|
| `EntitySave` | 9 |
| `NamedObjectSave` | 8 |
| `CustomVariable` | 7 |
| `ReferencedFileSave` | 5 |
| `ScreenSave` / `GlueElement` | 1 each |

`CustomVariable.Type` is the sharpest example (`CustomVariable.cs:62-69`): `[XmlIgnore]`,
`[JsonIgnore]`, `get => Properties.GetValue<string>("Type")`. A variable's **declared type — the
thing Phase 3 needs most — is a string inside a nested property bag**, not a field. Same story for
`Scope`, `OverridingPropertyType`, `TypeConverter`, `CreatesProperties`, and for
`NamedObjectSave.AssociateWithFactory` (`NamedObjectSave.cs:683`) and `EntitySave.InputDevice`.

**How we tackle it.** The mirror reproduces the bag-backed accessor pattern rather than only the
JSON-visible fields — the value bag is the primary storage, not a side-channel. This is also why §7.3
(the `GetValue<T>` helper) is a Phase 1 dependency of the model layer rather than a convenience: the
POCOs cannot expose their own properties without it. Build the helper first, then the POCOs on top.

---

### G5 — `[DefaultValue(true)]` disagrees with FRB1's own read path · Medium · report only, do **not** fix

`AddToManagers`, `IncludeInICollidable`, `IncludeInIClickable` and `CallActivity` are annotated
`[DefaultValue(true)]`, which Newtonsoft uses on *write* to omit them. But element files are read
back with **no settings at all** (`GlueProjectSaveExtensions.cs:578`, `:599`), so nothing consumes
the attribute on the way in — FRB1 relies purely on the constructor. The annotation and the reader
agree by coincidence, not by construction: change the constructor and the round-trip breaks silently.

**How we tackle it: file the issue, change nothing.** Every available fix here is user-visible.
Dropping the `[DefaultValue(true)]` annotations makes Newtonsoft start *writing* those members, so
the next save churns every element file in every user project. Adding `DefaultValueHandling.Populate`
on read changes load behavior for existing projects. Current behavior is correct-by-accident — this
is fragility, not breakage, and it is the FRB1 maintainer's call whether the cleanup is worth the
churn.

Report it with the round-trip test that demonstrates it (the epic explicitly welcomes FRB1 bugs
found this way) and move on. FRB2 does not wait on any of it: G3's approach — mirror the constructor
— stays correct no matter how, or whether, FRB1 resolves this.

---

### G6 — `.gluj` and element files use different serializer settings · Medium · fix in FRB2

| File | Write settings | Read settings |
|---|---|---|
| `.gluj` | `NullValueHandling.Ignore` + `DefaultValueHandling.IgnoreAndPopulate` (`:127`) | `JsonConvert.DeserializeObject<GlueProjectSave>(text)`, no settings (`:532`) |
| `.glsj` / `.glej` | `DefaultValueHandling.Ignore` (`:434`) | no settings (`:578`, `:599`) |

So the two file kinds have genuinely different omission rules, and neither is read back with the
settings it was written with.

**How we tackle it.** Do not try to model "settings" in FRB2 — STJ has no equivalent knob and does
not need one. The only observable consequence is *which members get omitted*, and G3's approach
(constructor defaults) handles omission uniformly for both file kinds. Record the asymmetry here so
the next person does not rediscover it, and cover both file kinds in the round-trip tests rather than
assuming `.gluj` behavior generalizes to `.glsj`.

---

### G7 — Backslash-separated element names · High · fix in FRB2

`"Screens\\MenuScreen"` is a project-relative path with `\` separators and no extension. FRB2 targets
Linux and WASM, where `\` is a legal filename character rather than a separator — so an unnormalized
join does not throw, it looks for a file literally named `Screens\MenuScreen.glsj` and reports it
missing.

**How we tackle it.** Normalize `\` → `/` at the single point where a `Name` becomes a path, and test
it explicitly. One helper, one test, one place — resist scattering `Replace('\\','/')` through the
loader. Note that `Name` is also the element's identity for `StartUpScreen`, `BaseScreen`, and
`BaseEntity` lookups, where it must stay in its original backslash form: normalize for **paths only**,
never for identity comparisons.

---

### G8 — Case sensitivity is unspecified · Medium · fix in FRB2

`GluxVersions.CaseSensitiveLoading = 55` exists because this already bit FRB1 once. Glue authored
these names on Windows, so a project whose `.gluj` says `Screens\MenuScreen` and whose file is
`Screens/Menuscreen.glsj` works on Windows and fails on Linux and in the browser.

**How we tackle it.** Match case-insensitively and emit a diagnostic when the match required ignoring
case — the project loads everywhere, and the author still finds out. Take care that this does not
mask a genuinely missing file: a case-insensitive miss must still be a `Warning`, not silence.

---

### G9 — `PropertySave.Type` is not a CLR type name · Medium · fix in FRB2

Values observed in `ChickenClicker.gluj` alone: `"int"`, `"Boolean"`, `"String"`, `"SourceType"` — a
mix of C# keywords, CLR simple names, and Glue enum names. Some entries omit `Type` entirely
(`IncludeFormsInComponents`, `IncludeComponentToFormsAssociation`).

**How we tackle it.** Drive decoding from the **requested `T`**, exactly as
`PropertySaveListExtensions.GetValue<T>` does (`PropertySave.cs:72`) — never from the `Type` string.
Keep `Type` on the mirror for diagnostics and for Phase 3, but never let it steer a conversion. This
also means `Type` being absent is not an error condition.

---

### G10 — Same data in two places on the same object · Medium · fix in FRB2

`PlayerBall.glej:164-190` shows `SourceType` as both a real property (`"SourceType": 2`) **and** a
bag entry (`{"Name":"SourceType","Value":2,"Type":"SourceType"}`) on the same `NamedObjectSave`.

**How we tackle it.** Decide authority per member and write it down as you go — do not assume the
strongly-typed field wins. Where FRB1 declares the property as bag-backed (G4), the bag is
authoritative by definition and the mirror should expose only the accessor. Where both genuinely
exist, prefer the typed field and add a diagnostic when the two disagree; a disagreement is a
corruption signal worth surfacing, not a tie to break silently.

---

### G11 — Raw-int enums with no cross-repo compile-time link · High · FRB2 + a zero-impact FRB1 comment

Enums serialize as bare ints (`"SourceType": 2`) with no string converter. FRB2's mirrored enums must
therefore match FRB1's **numeric values** exactly — and nothing enforces that. If someone inserts a
member into `SourceType` in FRB1, every FRB2 project silently misreads every object of that type. No
compiler error, no test failure, just wrong behavior.

**How we tackle it.** Two parts, neither user-visible. In FRB2: assign explicit numeric values to
every mirrored enum member (`FlatRedBallType = 2`, never bare ordinals) and add a test pinning each
value, so drift surfaces as a failing assert naming the member. In FRB1: add a comment at each
mirrored enum noting that FRB2 pins its values and that members must be appended, never inserted or
reordered — comments compile to nothing, so this is safely in the zero-impact bucket. Together they
are the cheapest durable guard short of code-sharing the enums, which the epic's package boundary
rules out.

Note the asymmetry: **the FRB2 test is the actual guard.** The FRB1 comment only helps someone who
reads it. If the upstream comment is unwelcome for any reason, the guard still works.

---

### G12 — FRB1 silently swallows missing and corrupt element files · Low · fix in FRB2

`LoadReferencedScreensAndEntities` skips a reference whose file is absent (`:574`) and skips a
`null` deserialization result (`:584`) with a comment explaining this is deliberate corruption
tolerance. The project loads with a screen quietly missing.

**How we tackle it.** Keep the tolerance, drop the silence — this is the direct motivation for
`GlueLoadResult` carrying diagnostics (§4). Same behavior, but the caller can see what was lost.

---

### G13 — Element `Name` can disagree with its file path · Low · fix in FRB2

`ElementReference.cs:109-114` carries a commented-out check for exactly this, with a note that it
"can cause errors at runtime" — so it is a real observed condition, not hypothetical.

**How we tackle it.** Compare the resolved reference name against the loaded element's `Name` and
emit a `Warning` on mismatch. Cheap, and it turns a class of confusing downstream failures into one
clear message at load time. Keep the file path authoritative for lookup.

---

### G14 — Display data duplicated at root and in `DisplaySettings` · Low · fix in FRB2

`DoorsDemo.gluj` carries `In2D`, `ResolutionWidth`, `ResolutionHeight`, `OrthogonalWidth`,
`OrthogonalHeight` at the root **and** a `DisplaySettings` block with its own `ResolutionWidth` /
`ResolutionHeight`. `GlueProjectSave.cs:226` labels the root copies "April 2017 - adding replacement
for these, eventually should get removed."

**How we tackle it.** Phase 1 parses both and applies neither — Phase 13 owns the mapping. Record
here that `DisplaySettings` is the newer, authoritative one so Phase 13 does not have to re-derive
that. Do not delete the root fields from the mirror; a real file still contains them, and Phase 13
may want to diagnose disagreement between the two.

---

### G15 — `ShouldSerialize*` and `[JsonIgnore]` are Newtonsoft-only · Low · fix in FRB2

The save classes are littered with `ShouldSerializeXxx()` methods and Newtonsoft `[JsonIgnore]`
attributes. STJ honors neither: it has no `ShouldSerialize` convention, and
`Newtonsoft.Json.JsonIgnoreAttribute` is a different type from
`System.Text.Json.Serialization.JsonIgnoreAttribute`.

**How we tackle it.** Irrelevant for Phase 1, which is read-only — flagged so it does not ambush
anyone later. **If write-back support is ever added, this becomes a blocker**, because FRB2 would
silently emit members FRB1 omits and produce `.gluj` diffs that churn on every save. Any future write
support needs its own design pass and a byte-comparison round-trip test against Glue's output.

---

### G16 — Real files contain shapes this epic excludes · Low · fix in FRB2

The chosen fixture proves this is the norm, not the exception: `DoorsDemo.gluj:53` has a populated
`CustomClasses` array (`TileMapInfo`, `PlatformerValues`) — the one thing the epic excludes outright
— plus `PluginData`, `ResolutionPresets`, and `SyncedProjects`. Excluded shapes are not rare corner
cases you can avoid by picking a tidy fixture; they are in the first real file you open.

**How we tackle it.** Omit them from the mirror entirely — STJ ignores unknown JSON members by
default, so their presence costs nothing. "Out of scope" must mean the loader is *unbothered* by
them, not that it rejects files containing them. The DoorsDemo fixture covers this by construction;
assert a clean load explicitly so nobody later mistakes tolerance for an oversight.

---

### G18 — Type strings are generic; exact-string matching cannot work · High · fix in FRB2

`SourceClassType` is not a flat set of type names. Across the DoorsDemo fixture alone:

```
FlatRedBall.Sprite
FlatRedBall.Math.Geometry.AxisAlignedRectangle
FlatRedBall.TileCollisions.TileShapeCollection
FlatRedBall.TileGraphics.LayeredTileMap
FlatRedBall.Math.PositionedObjectList<T>                                    <- literal "<T>"
FlatRedBall.Math.Collision.ListVsListRelationship<Entities.Player, Entities.Door>
FlatRedBall.Math.Collision.DelegateListVsSingleRelationship<Entities.Player, FlatRedBall.TileCollisions.TileShapeCollection>
FlatRedBall.Entities.CameraControllingEntity
Entities\Player                                                             <- element ref, not a CLR type
```

Three separate shapes hide in there: an **unresolved generic placeholder** (`<T>` literally, on
lists), **closed generics whose arguments are themselves Glue element names** (`Entities.Player`),
and **element references in backslash form** (`Entities\Player`) used where a type name would go.

**How we tackle it.** The type map cannot be a dictionary keyed on the whole string. Phase 1 needs a
small parser that splits a `SourceClassType` into (open type name, type arguments) before lookup, and
classifies the result as: FRB2-native type, Glue element reference, or unmapped. Phases 2/8/9 then
consume the parsed form rather than re-parsing strings. Get this right in Phase 1 — every later phase
inherits it, and retrofitting a parser under a string-keyed map means touching all of them.

The `Entities\Player`-as-type case also means G7's backslash normalization applies to type strings,
not just to element names and paths. Same helper, one more caller.

---

### G19 — The Phase 1 fixture is mostly unmappable in Phase 1, by design · Medium · fix in FRB2

Of DoorsDemo's nine top-level `NamedObjects` in `GameScreen.glsj`, **Phase 1 can map none of them**:
two `TileShapeCollection` and one `LayeredTileMap` belong to Phase 10, three collision relationships
to Phase 9, `CameraControllingEntity` to Phase 13, and the two `PositionedObjectList<T>` to Phase 2.
Only the entities' `Sprite` and `AxisAlignedRectangle` fall in Phase 1's declared target set.

This is the correct outcome, but it looks like failure if nobody says so in advance: **loading the
primary fixture in Phase 1 emits roughly a dozen "unmapped type" warnings, and that is success.**

**How we tackle it.** Say it explicitly here and in the Definition of Done: the Phase 1 bar is *zero
`Error` diagnostics*, not zero diagnostics. Assert the expected warning count in a test so the number
is pinned and later phases visibly drive it down — each phase that lands should shrink that count,
which turns it into a free progress metric for the whole epic rather than noise to ignore.

It is also the strongest possible validation of D1 (skip-with-diagnostic over fail-fast): under
fail-fast, Phase 1 could not load its own primary fixture at all.

---

## 6. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D1 | Fail-fast vs skip-with-warning for unmapped types | **Skip + diagnostic**, with an opt-in `Strict` mode. Reasoning in §4. |
| D2 | Mirror POCO names | **Keep FRB1 names verbatim** under `FlatRedBall2.Glue.Model`. |
| D3 | Fixture location | **`tests/FlatRedBall2.Tests/Glue/Fixtures/<ProjectName>/`**, copied to output via `<None ... CopyToOutputDirectory="PreserveNewest" />` — matches the existing `Animation\Content\Corpus\**` rows in `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj:24`. |
| D4 | Which sample to vendor first | **`Samples/Platformer/DoorsDemo` as-is** (G1) — version 60, above every file-shape-affecting gate, and it carries two entities, two screens, and the `BaseScreen` inheritance case in one small project. `FormsSampleProject` (61) for Phase 5. **Not** ChickenClicker or Beefball: at version 42 they sit below three schema gates, and upgrading them is a user-visible FRB1 change we do not need. |
| D5 | Case-sensitivity rule for element-name lookup | **Case-insensitive match with a diagnostic on case mismatch** (G8). Glue authored these on Windows; a silent miss on Linux is the worse failure. Confirm this does not mask a genuinely missing file. |
| D6 | `FileVersion` enforcement | **Warn below `LatestVersion`, never block** (G17). A hard gate would have failed every test the day `LatestVersion` went 67 → 68 mid-planning, with no useful diagnostic. Exercise the warning with a synthetic fixture, not a real sample, so corpus drift cannot break tests. |
| D7 | Do we change FRB1 at all? | **Decided: no — leave FRB1 untouched for now.** Not even the zero-impact fixes. They are someone else's repo, nothing here needs them, and opening PRs on another project mid-epic adds coordination cost for no gain to this work. G2 and G5 stay documented here; revisit once Phase 1 has landed and there is a reason to engage upstream. |
| D10 | Correct the tracking issue's mistaken premise upstream? | **Decided: no.** The correction lives in §1 of this document. Vic reviews the PR, which carries the reasoning — a separate comment on #804 duplicates it. Revisit if someone starts a later phase straight from the issue without reading this doc. |
| D11 | Vendor the deliberately-outdated fixture? | **Decided: yes — `ChickenClicker` at version 42 is vendored** alongside DoorsDemo. It is the only fixture below the three file-shape gates, so it is the only one that proves the loader warns instead of misreading. The outdated-file problem already invalidated one round of this plan (G1); leaving it untested would be betting it never recurs. |
| D8 | Authority when a typed field and a bag entry disagree | **Typed field wins, disagreement emits a diagnostic** (G10) — except where FRB1 declares the member bag-backed (G4), in which case the bag is authoritative by definition. |

| D9 | Read-seam shape: static like `TmxLoader`, or an instance member? | **Instance member set through `GlueLoadOptions`.** `TileMap.TmxLoader` is `internal static` mutable state, which CLAUDE.md's "no static state" rule forbids and which leaks across tests unless every one restores it. The loader already takes an options object, so the seam costs nothing to carry there. Keep it `internal` for Phase 1 — public is a Phase 14 API decision. |
---

## 7. Tasks

Repo rule: **a failing test comes first, or the commit body explains why one was not feasible.**
Load the `engine-tdd` skill before touching `src/`. Each group below is roughly one commit.

### 7.0 — FRB1-side work: none (D7)

**Deliberately empty.** FRB1 is left untouched by this phase. The two candidate upstream changes —
G2's duplicate `FileVersion` key and G11's append-only enum comment — stay documented in §5 rather
than landing as PRs on another team's repo mid-epic. G5 stays a report-only finding.

Do not bump any sample's `FileVersion`: re-saving does not do it, and hand-editing is user-visible
(G1). Fixture choice solves that instead.

### 7.1 — Fixtures and conventions

- [x] Create `tests/FlatRedBall2.Tests/Glue/` (mirrors `src/Glue/`, per `.claude/code-style.md` §Test Organization).
- [x] Vendor `Samples/Platformer/DoorsDemo` **as-is** into
      `tests/FlatRedBall2.Tests/Glue/Fixtures/DoorsDemo/` — `DoorsDemo.gluj`, `Screens/GameScreen.glsj`,
      `Screens/Level1.glsj`, `Entities/Door.glej`, `Entities/Player.glej` (D4).
- [x] Optionally vendor `Samples/ChickenClicker` (v42) as a deliberate **legacy-tolerance** fixture —
      it sits below three schema gates, which is exactly what makes it useful for proving the loader
      degrades with a diagnostic rather than misreading (G1).
- [x] Cover the cases no real sample does — a below-version `.gluj` (D6), a missing element
      reference (G12), a name/path mismatch (G13), a case-mismatched reference (G8). Landed as
      in-memory JSON through the read seam rather than as a `Fixtures/Synthetic/` folder: same
      coverage, no files to keep in sync, and it doubles as proof the seam is actually used.
- [x] Add a `Fixtures/README.md` recording the source repo, sample path, FRB1 commit, and sync date,
      so a future re-sync knows what it is re-syncing from.
- [x] Add the `<None Include="Glue\Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" />` row to
      `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj`.

### 7.2 — POCO mirror

- [x] Failing test: deserializing the DoorsDemo `.gluj` fixture yields `FileVersion == 60`,
      `StartUpScreen == "Screens\\Level1"`, two `ScreenReferences`, and two `EntityReferences`.
- [x] Add `src/Glue/Model/` POCOs for `GlueProjectSave`, `GlueElementFileReference`, `GlueElement`,
      `ScreenSave`, `EntitySave`, `PropertySave`, `InstructionSave`, `NamedObjectSave`,
      `CustomVariable`, `ReferencedFileSave`, `StateSave`, `StateSaveCategory`, `DisplaySettings`.
- [x] Type `PropertySave.Value` and `InstructionSave.Value` as `JsonElement` (see §4).
- [x] Add `src/Glue/GlueJsonContext.cs` with `[JsonSerializable]` entries for every root shape and
      `PropertyNameCaseInsensitive = true`, mirroring `src/Movement/TopDownConfig.cs:40`.
- [x] Failing test: a `NamedObjectSave` deserialized from JSON that **omits** `Instantiate` reports
      `Instantiate == true` (G3) — assert on deserialized JSON, not on `new NamedObjectSave()`.
- [x] Reproduce every `NamedObjectSave` constructor default from `NamedObjectSave.cs:828`, and
      confirm `AttachToContainer` is *not* among them (G3).
- [x] Expose the 31 bag-backed members as accessors over `Properties` rather than as fields (G4) —
      `CustomVariable.Type`, `Scope`, `OverridingPropertyType`, `TypeConverter`,
      `NamedObjectSave.AssociateWithFactory`, `EntitySave.InputDevice`, and the rest.
- [x] Assign explicit numeric values to every mirrored enum member, and add a test pinning each one
      against the FRB1 value it mirrors (G11).
- [x] Failing test: JSON with a duplicate object key resolves last-one-wins under STJ (G2).
- [x] Failing test: a `.gluj` with a populated `CustomClasses` array loads cleanly with the member
      absent from the mirror (G16).
- [x] Verify the build stays AOT-clean: `dotnet build src/FlatRedBall2.csproj` emits no
      `IL2026`/`IL3050` trim or AOT warnings from the new code.

### 7.3 — Value-bag decoding

Build this **before** the POCOs in 7.2 — the mirror's bag-backed accessors depend on it (G4).

- [x] Failing test: `GetValue<int>` on `{"Value": 1}` returns `1`; `GetValue<float>` on
      `{"Value": 16.0}` returns `16f`; `GetValue<bool>` on `{"Value": true}` returns `true`;
      `GetValue<string>` on `{"Value": "White"}` returns `"White"`.
- [x] Failing test: `GetValue<SourceType>` on `{"Value": 2}` returns the enum member for `2`
      (G9, G11).
- [x] Failing test: `GetValue<T>` for a name not present returns `default(T)` and does not throw —
      matching `PropertySaveListExtensions.GetValue<T>` (`PropertySave.cs:120`).
- [x] Failing test: decoding is driven by the requested `T`, not by the entry's `Type` string —
      `GetValue<int>` succeeds on an entry whose `Type` is absent, and on one whose `Type` reads
      `"Boolean"` while the value is numeric (G9).
- [x] Implement `src/Glue/PropertySaveExtensions.cs` over `JsonElement`, covering
      `int`/`float`/`bool`/`string`/enum and their nullable forms.
- [x] Failing test: nullable requests (`GetValue<int?>`) on an absent name return `null`, not `0`.

### 7.4 — Reference resolution and the read seam

- [x] Failing test: `GlueProjectLoader` routes every read through its injectable `TextLoader`
      delegate and never touches `System.IO` (mirrors `TileMapLoadingTests.cs:15`, but as an
      instance seam rather than static state — D9).
- [x] Failing test: loading the DoorsDemo fixture populates `Screens.Count == 2` and
      `Entities.Count == 2`, and clears both reference lists, matching
      `LoadReferencedScreensAndEntities` (`GlueProjectSaveExtensions.cs:567`).
- [x] Failing test: `"Screens\\MenuScreen"` resolves to `Screens/MenuScreen.glsj` with forward
      slashes on non-Windows (G7).
- [x] Failing test: `\` normalization applies to path building only — `StartUpScreen` and
      `BaseScreen` identity comparisons still match on the original backslash form (G7).
- [x] Failing test: a reference that matches a file only when case is ignored loads, and emits one
      `Warning`; a reference matching nothing still emits a `Warning` and does not silently pass (G8).
- [x] Failing test: a `ScreenReferences` entry whose file is absent produces one `Warning`
      diagnostic and leaves the other screens loaded (G12).
- [x] Failing test: an element whose internal `Name` disagrees with the reference that loaded it
      emits one `Warning`, and the file path stays authoritative (G13).
- [x] Implement `GlueProjectLoader.Load`, `GlueLoadResult`, `GlueLoadDiagnostic`, `GlueLoadOptions`.
- [x] Failing test: `GlueLoadOptions.Strict` throws on the first `Error` diagnostic.
- [x] Failing test: DoorsDemo's `Screens/GameScreen.glsj` retains **nine** top-level `NamedObjects`
      parsed but un-applied, and `PlayerList` retains **one** `ContainedObjects` child whose
      `SourceClassType` is `"Entities\\Player"` — nesting must not be flattened into the top level.
- [x] Failing test: `Level1.BaseScreen == "Screens\\GameScreen"` is parsed and retained with **no**
      merge — `Level1` keeps exactly its own four `NamedObjects` (`Map`, `SolidCollision`,
      `CloudCollision`, `PlayerList`), not the nine from its base. Inheritance is Phase 6.
- [x] Failing test: those four carry `DefinedByBase == true` (and `PlayerList` also
      `InstantiatedByBase` and `ExposedInDerived`) — a derived element redeclares its base's objects
      even above the `RemoveRedundantDerivedData` gate, and Phase 1 must not dedupe them.
- [x] Failing test: `Player.glej` yields two `NamedObjects`, six `CustomVariables`, and five
      `ReferencedFiles` parsed and retained — files are Phase 4, so none is loaded.
- [x] Failing test: the `.glsj`/`.glej` omission rules are covered independently of `.gluj`'s — do
      not assume one generalizes to the other (G6).
- [x] Failing test: a `.gluj` below `LatestVersion` loads and emits one `Info` diagnostic, asserted
      against the **synthetic** fixture so corpus drift cannot break it (D6, G17).

### 7.5 — Type mapping

- [x] Failing test: `GlueTypeMap` maps `"FlatRedBall.Math.Geometry.AxisAlignedRectangle"` and
      `"FlatRedBall.Sprite"` to their FRB2 equivalents (both appear in `Player.glej`).
- [x] Failing test: the type-string parser splits `"FlatRedBall.Math.PositionedObjectList<T>"` into
      an open type plus an unresolved argument, and
      `"...ListVsListRelationship<Entities.Player, Entities.Door>"` into an open type plus two
      **element-reference** arguments — not CLR type names (G18).
- [x] Failing test: `"Entities\\Player"` in a `SourceClassType` position classifies as an element
      reference, with backslash normalization shared with G7's helper (G18).
- [x] Failing test: an unmapped type string returns no type and yields one `Warning` diagnostic
      naming both the element and the type string (D1).
- [x] Failing test: loading DoorsDemo emits the expected count of unmapped-type `Warning`s and
      **zero** `Error`s — pin the number so later phases visibly drive it down (G19).
- [x] Implement `src/Glue/GlueTypeMap.cs` over the parsed form, covering only Phase 1's declared
      set: `Sprite`, `AxisAlignedRectangle`, `Circle`, `Polygon`, `ShapeCollection`, `Text`.
      Everything else — `TileShapeCollection`, `LayeredTileMap`, `PositionedObjectList<T>`, the
      collision relationships, `CameraControllingEntity` — warns and is owned by a later phase.
- [x] No rows for Gum runtime types. The issue lists them under Phase 1, but none appears in a
      `SourceClassType` position in any fixture (the `.gumx` is a `GlobalFile`) — deferred to Phase 5.
- [x] Make the map extensible — later phases add rows without editing a `switch`.

### 7.6 — Boot into FRB2's screen system

- [x] Failing test: `GlueScreen` constructed from a `ScreenSave` exposes that save and its `Name`.
- [x] Failing test: resolving `StartUpScreen` picks the `ScreenSave` whose `Name` matches, and an
      unresolvable `StartUpScreen` yields an `Error` diagnostic rather than a `NullReferenceException`.
- [x] Implement `src/Glue/GlueScreen.cs` and `src/Glue/GlueEntity.cs` as skeletons — hold the save,
      no `NamedObject` construction.
- [x] Implement the boot entry point that hands the resolved `GlueScreen` to
      `FlatRedBallService` (`src/FlatRedBallService.cs:465` is the existing `Start<T>` path; a
      non-generic seam is needed because every loaded screen shares one CLR type — this is the
      Phase 14 API in embryo, so keep it `internal` for now rather than committing to public shape).
- [x] Manual check: done, and automated rather than left to a human. A scratch host boots a
      fixture through the real engine, runs frames and screenshots the back buffer. DoorsDemo starts
      on `Level1` with no exception. (`Level1` is *derived*; Phase 6 now merges it.)

### 7.7 — Documentation and wrap-up

- [x] XML docs on every public type in `src/Glue/` — CS1591 is a tracked metric (see the comment in
      `src/FlatRedBall2.csproj`); do not add to the count.
- [x] Log any further FRB1 bug found during implementation as an issue on the FRB1 repo (G2 and G5
      are already known; the epic explicitly welcomes more).
- [x] Update this document's checkboxes and flip its **Status** row.
- [x] Update the Phase 1 row in [`plan/plan.md`](../plan.md).
- [ ] Decide whether a `glue-project-loading` skill is warranted yet, or whether it should wait
      until Phase 2 gives it enough surface to be worth the context budget. Consult
      `skill-creator` before writing one.

---

## 8. Definition of done

- [ ] `dotnet build src/FlatRedBall2.csproj` succeeds with no new warnings and no AOT/trim warnings.
- [ ] `dotnet test tests/FlatRedBall2.Tests/` passes.
- [ ] Every vendored fixture loads with zero `Error` diagnostics. **Warnings are expected and are
      not a failure** — DoorsDemo emits roughly a dozen unmapped-type warnings in Phase 1 by design
      (G19), and the count is pinned by a test so later phases visibly drive it down.
- [ ] DoorsDemo boots to `Level1` from its `.gluj` with no hand-written screen class.
- [ ] DoorsDemo's `Player.glej` and `GameScreen.glsj` round-trip into POCOs with `NamedObjects`,
      `CustomVariables`, `ReferencedFiles`, and `BaseScreen` all populated but un-applied, and with
      `ContainedObjects` nesting preserved rather than flattened.
- [ ] No Newtonsoft.Json reference was added anywhere.
- [ ] Every open decision in §6 is either implemented as recommended or amended in place with the
      reason it changed.
- [ ] Every gotcha in §5 is either covered by a test, reported upstream, or explicitly deferred to a
      named later phase — none silently dropped.
- [ ] `Fixtures/README.md` names the FRB1 commit it vendored from.
- [ ] **No user-visible FRB1 change was made or required.** Any upstream PR opened along the way was
      zero-impact and merged (or not) independently of this phase.

---

## 9. What landed

Implemented across six commits on `804-glue-loader-plan`. 49 new tests, full suite 1264 green, no
new build warnings and no AOT/trim warnings from `src/Glue/`.

| Piece | File | Covers |
|---|---|---|
| Value-bag reader | `src/Glue/PropertySaveExtensions.cs` | §7.3, G4, G9 |
| POCO mirror | `src/Glue/Model/` (11 types) | §7.2, G3, G4, G11 |
| Serializer context | `src/Glue/GlueJsonContext.cs` | §7.2 |
| Loader + diagnostics | `src/Glue/GlueProjectLoader.cs`, `GlueLoadResult.cs`, `GlueLoadOptions.cs` | §7.4, G7, G8, G12, G13 |
| Type-string parser | `src/Glue/GlueTypeName.cs` | §7.5, G18 |
| Type map | `src/Glue/GlueTypeMap.cs` | §7.5, G19 |
| Screen/Entity skeletons | `src/Glue/GlueScreen.cs` | §7.6 |

### Deviations from the plan, and why

- **No `Fixtures/Synthetic/` folder.** The cases no real sample covers are exercised as in-memory
  JSON through the read seam instead. Same coverage, nothing to keep in sync, and it doubles as
  proof the seam is genuinely used rather than bypassed.
- **No new engine API.** §7.6 anticipated needing a non-generic entry point on `FlatRedBallService`
  because every loaded screen shares one CLR type. It turned out not to: `Start<GlueScreen>(s =>
  s.Save = …)` already satisfies the existing `where T : Screen, new()` constraint. Nothing was
  added to `Screen` or `FlatRedBallService`, which keeps the Phase 14 API question fully open.
- **`GlueVersions.Latest` is 68, not 67.** It moved upstream mid-planning. That is G17, and it is
  why nothing branches on version.

### Found while building

- **A crash on input Glue actually writes.** `JsonElement`'s numeric `TryGet*` methods throw rather
  than returning false when the element is not a number — including on the `Undefined` element that
  a property with no `Value` key deserializes to. Glue saves with `NullValueHandling.Ignore`, so
  null-valued properties are written exactly that way, and one anywhere in a project would have
  killed the whole load. Fixed with a single `ValueKind` gate; two tests reproduce it.
- **FRB1's `GetValue<T>` is not tolerant.** It ends in a bare unboxing cast and throws
  `InvalidCastException` on a type mismatch. §5 and the XML docs previously described this loader as
  mirroring a tolerance that does not exist; it is a deliberate widening, now documented as one.
- **FRB2 has no `ShapeCollection` and no `Text` type.** Two of the six types §7.5 named as Phase 1's
  target set do not exist in the engine. The other four map cleanly (note `AxisAlignedRectangle` →
  `AARect`). **Phase 2 has to decide** whether to add them or map them onto something else.
- **Element names appear in two forms.** `Entities\Player` standing alone, but `Entities.Player` as
  a generic argument, where it is the generated C# class name. Handled structurally by
  `ToElementNameCandidate` — deliberately with no prefix heuristic, since
  `FlatRedBall.Entities.CameraControllingEntity` is an *engine* type whose namespace contains
  "Entities" and would fool one.
- **The test project cannot enforce reflection-free JSON yet.**
  `JsonSerializerIsReflectionEnabledByDefault=false` would make an AOT-unsafe serializer call fail
  the build rather than only failing after publish. It is assembly-wide and `AutomationMode`
  deliberately uses reflection, so it fails ~12 automation tests. Giving `AutomationMode` its own
  serializer context would unlock it. Reason recorded in the test `.csproj`.

### The boot check, and a wrong assumption

This originally read: *"Running the game to watch DoorsDemo start on `Level1` needs a display and a
real game loop; it cannot run headless here."*

**That was asserted, never tested, and it is wrong.** A `Game` plus `GraphicsDeviceManager` and
repeated `RunOneFrame()` creates a real device and renders, without entering a blocking message
loop. The back buffer can then be read with `GetBackBufferData` and saved as a PNG — so the check is
not only possible, it automates.

DoorsDemo boots to `Level1` with no exception, and Beefball's `GameScreen` renders its six arena
walls with the goal gaps in the right places. DoorsDemo itself still draws blank, which is correct:
everything visible in it is tile-based (Phase 10) or textured (Phase 4).

The lesson is the same one the trimmed-publish runs keep teaching — a claim about what cannot be
verified is itself a claim that needs verifying.
