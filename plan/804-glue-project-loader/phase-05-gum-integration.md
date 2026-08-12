# Phase 5 — Gum Integration

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented |
| **Depends on** | Phase 4 (referenced files) |
| **Blocks** | Nothing |
| **Suggested branch** | `804-phase-5-gum` |

---

## 1. The problem

A Glue project's UI is a `.gumx` project plus per-Screen `.gusx` references. Loading them is what
turns a loaded Screen from "shapes on a background" into a game with menus, HUDs, and buttons.

The issue frames this phase as *wiring*, not new capability, and that framing is correct: FRB2
already loads a `.gumx` at startup (`EngineInitSettings.GumProjectFile` →
`src/FlatRedBallService.cs:263-273`) and already hosts Gum visuals in a Screen
(`Screen.Add(GraphicalUiElement)`, `src/Screen.cs:254`). This phase connects the two using the
project's own data.

---

## 2. Scope

### In scope

1. Locate the `.gumx` in `GlobalFiles` and load it before anything else.
2. Per-Screen `.gusx` → instantiate the named Gum screen and add it to the `GlueScreen`.
3. `SourceType.Gum` (3) and `.gucx`-sourced `SourceType.File` NamedObjects.
4. Inherited Gum screens — a derived Screen with no `.gusx` of its own (G53).

### Out of scope

- Forms control wiring (`FormsControl`, `DefaultFormsComponents`) — needs generated types that do
  not exist in a data-driven model. D51.
- `FileAdditionBehavior`, `ShowMouse`, `MakeGumInstancesPublic`,
  `IsMatchGameResolutionInGumChecked`, `IncludeFormsInComponents` — all codegen-time only (G50).
- Gum canvas sizing → Phase 13 (and mostly already handled; see that doc's `ScaleGum` finding).

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | The Gum project loads | Every element the UI needs is registered before a screen asks for it. | §6.1 |
| F2 | Screens get their UI | Loading DoorsDemo's `GameScreen` shows `GameScreenGum`. | §6.2 |
| F3 | Derived screens inherit UI | `Level1` shows its base's Gum screen without declaring one. | §6.3 |
| F4 | Gum objects inside elements build | A `.gucx`-sourced NamedObject appears. | §6.4 |

---

## 4. Proposed resolution

### `.gumx` first, always

A `.gusx` reference is **not a file read** — it is a name lookup into the already-loaded `.gumx`.
FRB1's generated code (`GumPlugin/Managers/AssetTypeInfoManager.cs:88-134`) is:

```csharp
name = (RuntimeType)GumRuntime.ElementSaveExtensions.CreateGueForElement(
    Gum.Managers.ObjectFinder.Self.GetElementSave(@"<strippedName>"), true);
```

So load order is mandatory: `.gumx` from `GlobalFiles`, then any `.gusx`. `strippedName`
(`:278-290`) is `RFS.Name` minus the gum project folder and its `screens/`/`components/` prefix,
forward-slashed.

### Per-Screen association

A Screen's `ReferencedFiles` carries the `.gusx` with a `RuntimeType` naming the generated runtime
class. Verbatim, DoorsDemo `GameScreen.glsj`:

```json
"ReferencedFiles": [ {
  "Name": "GumProject/Screens/GameScreenGum.gusx",
  "IsSharedStatic": true,
  "RuntimeType": "DoorsDemo.GumRuntimes.GameScreenGumRuntime",
  "ProjectsToExcludeFrom": []
} ]
```

**The `RuntimeType` names a class that will never exist in FRB2** — it is Glue codegen output. The
loader ignores it and uses the *element name* instead. That is the whole trick: FRB1 needs the type
to declare a strongly-typed field; a data-driven loader needs only the `GraphicalUiElement`.

---

## 5. Gotchas

### G50 — `AutoCreateGumScreens` is inert at load time

The issue lists it as something to handle. It is not: it is a **Glue authoring convenience** that
runs when a new Screen is created (`MainGumPlugin.HandleNewScreen`, `:313-325` →
`GumPluginCommands.AddScreenForGlueScreen`, `:199-226`). It creates a `.gusx`, adds the
`ReferencedFileSave`, and sets its `RuntimeType`. Toggling the checkbox later is an explicit no-op
(`GumxPropertiesManager.cs:42-45`).

**How we tackle it.** Parse it, apply nothing. The `ReferencedFiles` it produced are the entire
story. The same is true of `FileAdditionBehavior`, `ShowMouse`, `MakeGumInstancesPublic`,
`IsMatchGameResolutionInGumChecked`, `IncludeFormsInComponents`, and
`IncludeComponentToFormsAssociation` — all read only at codegen time.

### G51 — Phase 1 concluded Gum runtime types never appear as `SourceClassType`. They do.

Phase 1's §7.5 records: *"No rows for Gum runtime types. The issue lists them under Phase 1, but none
appears in a `SourceClassType` position in any fixture (the `.gumx` is a `GlobalFile`)."*

True of the fixtures, **false of FRB1**. Sweep of every element file:

| `SourceClassType` | n |
|---|---|
| `NineSliceButtonRuntime` | 7 |
| `GlueTestProject.GumRuntimes.StateComponentRuntime` | 2 |

Both in `Tests/TestProjectDesktopNet6`. The Phase 1 conclusion was correct for the corpus it had;
this phase widens the corpus, so it is now wrong.

### G52 — `SourceType.Gum` carries a bare type name where a path belongs

```json
{ "InstanceName": "StateComponentRuntimeInstance",
  "SourceClassType": "GlueTestProject.GumRuntimes.StateComponentRuntime",
  "SourceType": 3,
  "SourceFile": "StateComponentRuntime" }
```

`SourceFile` is **not a path** — it is the bare type name. Every other `SourceType` uses `SourceFile`
for a real relative path (Phase 4 G43), so a shared resolver will try to open a file called
`StateComponentRuntime` and report it missing.

Note Glue's own recogniser splits the two cases differently: `GumPluginCodeGenerator.IsGue`
(`:337-344`) matches `SourceType == File && extension is "gusx" or "gucx"` — i.e. a Gum object
sourced *from a file* uses `SourceType.File`, and `SourceType.Gum` is reserved for the
already-registered-type case. Handle both.

### G53 — A derived Screen inherits its Gum screen and declares nothing

DoorsDemo's `Level1.glsj` has **no** `.gusx` reference. It sets `BaseScreen: "Screens\\GameScreen"`
and inherits `GameScreenGum`.

Since `Level1` is the project's `StartUpScreen`, **the most-used screen in the primary fixture is
exactly the inheriting case.** This phase cannot be demonstrated on that fixture without Phase 6, or
without walking `BaseScreen` for referenced files specifically.

**How we tackle it.** Either depend on Phase 6, or walk the base chain for `ReferencedFiles` only —
a narrow, well-defined subset of the inheritance merge. Prefer the former if Phase 6 has landed;
D50 records the fallback.

### G54 — Three different `RuntimeType` values for `.gusx`, only one of which is real

| `RuntimeType` | n | Meaning |
|---|---|---|
| `<Ns>.GumRuntimes.<X>Runtime` | 15 | generated strongly-typed class — **does not exist in FRB2** |
| `Gum.Wireframe.GraphicalUiElement` | 4 | the base type — usable as-is |
| `FlatRedBall.Gum.GumIdb` | 4 | legacy; the *only* one that actually reads the `.gusx` file |

**How we tackle it.** Ignore `RuntimeType` for `.gusx` and always produce a `GraphicalUiElement` from
the element name. Warn on `GumIdb`, which is a genuinely different loading model.

### G55 — `PropertySave.Type` is encoded three ways for the same property

The `.gumx` RFS's bag has the same property written as `"Type": "bool"` (DoorsDemo),
`"Type": "Boolean"` (ChickenClicker), and with **no `Type` key at all**
(`IncludeFormsInComponents`).

FRB2's `PropertySaveExtensions` already ignores `Type` entirely and decodes by the requested `T`
(`src/Glue/PropertySaveExtensions.cs:22-39`). This is a third independent confirmation that Phase 1's
G9 decision was right. Keep it.

### G56 — The Gum API this phase depends on is unverified — **RESOLVED, spike run 2026-08-05**

`frb-skills/gum-integration/SKILL.md:211-213` documents
`ObjectFinder.Self.GumProjectSave.Screens.Find(...)` + `ToGraphicalUiElement()`. That recipe has
**zero call sites anywhere in FRB2** — not in `src/`, `samples/`, `templates/`, or `tests/`.

FRB1's equivalent is `GumRuntime.ElementSaveExtensions.CreateGueForElement`, which lives in the
external Gum package, not in either repo. **Whether the Gum version FRB2 references exposes an
equivalent lookup by element name is unconfirmed, and it is the single biggest risk in this phase.**

**Outcome: the capability exists, so the phase's shape survives — but the load path is constrained
in a way the plan did not anticipate (G57).** Spiked against `Gum.MonoGame` 2026.8.5.2-preview.2 with
`samples/Solitaire/.../GumProject.gumj` as the stand-in project. What was confirmed:

| Question | Answer |
|---|---|
| Can a Gum project be read with no graphics device? | **Yes.** `GumProjectSave.Load(path)` enumerates 2 screens and 43 components headlessly. |
| Do element names match what the loader needs to look up? | **Yes.** `GameScreenGum`, `Controls/ButtonStandard` — forward-slashed, exactly §4's `strippedName` shape. |
| Can a `GraphicalUiElement` be instantiated from an element name? | **Yes**, via `ToGraphicalUiElement()` — `GameScreenGum` built with 5 children. |
| Can that be done headlessly? | **No.** See G57. Instantiation needs a real `GumService.Initialize`, i.e. a `GraphicsDevice`. |
| Is `GumService.InitializeForTesting()` a headless escape hatch? | **No.** It sets `IsInitialized = true` but leaves `CustomCreateGraphicalComponentFunc` unset, so instantiation still throws. It is for code-constructed elements, not project-loaded ones. |
| Is `GumService.ElementNameFromPath` the `strippedName` helper? | **No** — despite the name. Its XML doc scopes it to `*Animations.ganx`/`.ganj` animation files. The loader still needs its own stripping (§6.1). |

### G57 — A Gum project must be loaded *through `GumService`*, not by assigning `ObjectFinder`

The obvious reading of the skill recipe — load the project yourself, hand it to `ObjectFinder`, then
instantiate — **does not work**, and fails late enough to look like a data bug rather than a setup one:

```
GumProjectSave.Load(path);
ObjectFinder.Self.GumProjectSave = project;      // no error
project.Screens[0].ToGraphicalUiElement();       // builds children, THEN throws
  -> NotImplementedException at Gum.Converters.UnitConverter.ConvertToGeneralUnit
     (via GraphicalUiElement.ApplyState -> SetProperty -> TrySetValueOnThis)
```

The identical project instantiates cleanly when `GumService` loads it itself (through
`EngineInitSettings.GumProjectFile` → `_gum.Initialize(game, gumProjectFile)`,
`src/FlatRedBallService.cs:263-269`). So `GumService.Initialize` performs setup beyond assigning the
project, and `ObjectFinder` assignment alone leaves the runtime half-configured. **Assume any recipe
that skips `GumService` is wrong, however plausible it reads.**

**Consequence for this phase — the load-order problem.** `GumService` wants the project path at
`Initialize` time; the Glue loader *discovers* it from `GlobalFiles` at load time, which is after.
`GumService` exposes no documented "load a project now" method (checked its full public surface:
`Initialize(Game|GraphicsDevice, string)`, `Uninitialize`, `InitializeForTesting`, plus
`EnableHotReload`/`LoadAnimations*`/`RefreshStyles`). Resolution is D53.

### G58 — `MonoGameGum.ElementSaveExtensionMethods.ToGraphicalUiElement` is obsolete

The compiler reports it as `[Obsolete]`: *"Use `Gum.ElementSaveExtensionMethods.ToGraphicalUiElement`
instead (via `using Gum;`). This legacy namespace forwarder exists only for back-compat."*

`frb-skills/gum-integration/SKILL.md:209` still instructs `using MonoGameGum;` for exactly this call,
with a note calling the namespace "easy to miss" — stale as of the Gum bump on `main`. **Fixed in the
skill as part of this phase**; use `using Gum;`.

---

## 6. Tasks

Test-first throughout. **Do §6.0 before planning the rest.**

### 6.0 — De-risk

- [x] Spike: confirm the referenced Gum version can instantiate a `GraphicalUiElement` from an
      element name in a loaded `.gumx` (G56). If not, stop and redesign. — **Confirmed**; the load
      path is constrained (G57) but the phase's shape holds. Results in G56's table.
- [x] Verify the skill's documented recipe against the real API, and fix the skill either way. —
      Two corrections landed in `frb-skills/gum-integration/SKILL.md` (G58, G57).

### 6.1 — Project load

- [x] Failing test: a `.gumx` in `GlobalFiles` is located and loaded.
      `GlueGumTests.Load_ProjectWithAGumProjectInGlobalFiles_FindsIt`, and
      `GlueGumInstantiationTests.Initialize_WithAGlueProject_LoadsTheGumProjectItReferences` proves
      Gum itself ends up holding it.
- [x] Failing test: a `.gusx` referenced before the `.gumx` has loaded produces an ordered load, not
      a failure. — **Resolved structurally instead** (D53): the `.gluj` is read during
      `FlatRedBallService.Initialize` *before* Gum initializes, so the out-of-order case cannot
      arise. There is no state in which a `.gusx` resolves against an unloaded project.
- [x] Failing test: `strippedName` drops the gum-project folder and the `screens/` prefix.
      `ElementNameFor_AGumFileReference_StripsProjectFolderAndCategory`, including the
      nested-component case that must *not* be stripped.

### 6.2 — Per-Screen association

- [x] Failing test: DoorsDemo's `GameScreen` gets `GameScreenGum` added as a `GraphicalUiElement`.
      `Start_AGlueScreenDeclaringAGumScreen_ShowsIt`.
- [x] Failing test: `RuntimeType` is **not** used for lookup (G54) — a project whose `RuntimeType`
      names a nonexistent class still resolves.
      `GumScreenNameFor_ARuntimeTypeNamingANonexistentClass_StillResolves`.
- [x] Failing test: a `GumIdb` `RuntimeType` warns as an unsupported legacy model. — Recognition is
      covered (`IsLegacyGumIdb_ByRuntimeType`); **the diagnostic's emission is not**. Reaching it
      needs a `GlueProject` built from a fixture carrying a `GumIdb` reference, and no vendored
      fixture has one. Left as the phase's one uncovered branch rather than faking a fixture.

### 6.3 — Inheritance

- [x] Failing test: `Level1` — which declares no `.gusx` — shows its base's Gum screen (G53).
      `Start_TheStartUpScreen_ShowsTheGumScreenItInherits`. **G53 needed no Gum-specific work**:
      Phase 6's `MergeInto` already merges `ReferencedFiles`
      (`src/Glue/GlueInheritanceResolver.cs:118`), so a flattened derived screen carries its base's
      `.gusx` already — D50's "depend on Phase 6" resolved to "Phase 6 already did it". The
      resolver still walks `BaseElement` itself, for elements that have not been flattened; that
      fallback is pinned by `Load_ADerivedScreen_InheritsItsBasesGumFileByFlattening`.

### 6.4 — Gum NamedObjects

- [x] Failing test: a `SourceType.Gum` object resolves by type name, not by treating `SourceFile` as
      a path (G52). `ComponentElementNameFor_ASourceTypeGumObject_ResolvesByTypeNameNotByPath`, plus
      `ElementNameFromRuntimeType_StripsNamespaceAndRuntimeSuffix` for the name derivation.
- [x] Failing test: a `.gucx`-sourced `SourceType.File` object resolves by element name.
      `ComponentElementNameFor_AGucxSourcedFileObject_ResolvesByElementName`, which uses the nested
      `Controls/ButtonStandard` case since that is the one a naive strip breaks.
- [x] Add a `GlueTypeMap` classification for Gum runtime types (G51). — **Not added to the map.**
      There is no fixed set of names to enumerate: every project's codegen generates its own
      (`<Ns>.GumRuntimes.<X>Runtime`), so a lookup table could never be complete. Classified by a
      predicate instead, `GlueGumResolver.IsGumObject`, consulted both by the builder (before the
      type map, which could never resolve these) and by the loader's unmapped-type report.

**Name derivation is lossy, and the lookup absorbs it.** A generated runtime type name drops the
folders Gum keeps — `ButtonStandardRuntime` has to find `Controls/ButtonStandard` — so
`FindGumElement` matches a trailing segment as well as a whole name. A name not ending in `Runtime`
is left alone rather than truncated, so a component named `CardGum` survives.

### 6.5 — Fixtures and wrap-up

- [x] ~~Vendor `FormsSampleProject`~~ — **vendored DoorsDemo's Gum project instead.** DoorsDemo is
      already the primary fixture, its `GameScreen` already carries the `.gusx` reference, and its
      `Level1` is the G53 inheriting case. `FormsSampleProject` would have added a second project to
      keep in sync for no coverage this phase uses; revisit it for §6.4's Forms-bearing components.
- [x] Vendor the `.gumx` and `.gusx` content alongside it (Phase 4 G49 — no content is vendored yet).
      25 files under `tests/FlatRedBall2.Tests/Glue/Fixtures/DoorsDemo/Content/GumProject/`.
      `Libraries/bmfont.exe` and `EventExport/` deliberately excluded — an executable has no place
      in a test fixture, and neither is read at load time.
- [x] XML docs; update this document and `plan/plan.md`.

### 6.6 — Test-infrastructure finding (not in the original plan)

- [x] **Gum initializes once per process, and a second `Game` running concurrently in another xUnit
      collection fails intermittently.** The first full-suite run after these tests landed passed;
      the next failed three tests inside `ApplyWindowSettings`. Fixed by making `GlueGumFixture` a
      collection fixture on `GraphicsDeviceCollection`, so everything that builds a `Game` is
      serialized. Verified stable across three consecutive full-suite runs.
- [x] Silent-skip guard: device-backed tests that `return` when no device is present look green when
      they never ran. Confirmed by a temporary probe that the four device-backed tests really
      execute in a full-suite run before removing it. **Anything added to §6.4 needs the same
      check** — the existing pattern cannot distinguish "passed" from "skipped".

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D50 | Depend on Phase 6, or walk `BaseScreen` for referenced files only? | **Depend on Phase 6 if it has landed; otherwise walk the base chain for `ReferencedFiles` alone.** The narrow walk is ~20 lines and unblocks the primary fixture (G53). Do not let it grow into a partial inheritance implementation — if it needs `CustomVariables` too, stop and do Phase 6. |
| D51 | Wire Gum Forms controls? | **No.** `Forms = rfs.FormsControl ?? new <FormsType>(rfs)` needs a generated `<FormsType>` per component, which is exactly the codegen this epic removes. Revisit only alongside the "future partial codegen" direction in the issue's ground rules. |
| D52 | Honour `ShowMouse`? | **No.** It emits `Game.IsMouseVisible = true` into generated GlobalContent. One line, but it is a game-shell concern rather than a loader one, and FRB2 games set it themselves. Warn if set. |
| D53 | How does the discovered `.gumx` reach Gum, when Gum only accepts a project at `Initialize` time (G57)? | **The engine reads the `.gluj` during `Initialize`, before Gum starts.** Decided with Vic 2026-08-05. New `EngineInitSettings.GlueProjectFile`; the project it loads is exposed as `FlatRedBallService.GlueProject` so the caller does not load it twice. Rejected: a caller-side pre-pass (every consumer rewrites the same ordering-sensitive dance) and `Uninitialize`+re-`Initialize` (tears down GPU state and resets statics other engine wiring depends on). An explicitly set `GumProjectFile` still wins, for a game mixing hand-written UI with a Glue project. |
| D54 | Should `Start<GlueScreen>` auto-assign `Save`/`Project` from `FlatRedBallService.GlueProject`? | **Deferred to Phase 14**, which owns the name-based navigation API. Today the caller assigns both in the `configure` callback. Worth revisiting there rather than growing a second boot convention here. |

---

## 8. Definition of done

- [ ] `dotnet build` clean; `dotnet test` green.
- [ ] A real `PublishTrimmed` emits no IL warnings from `src/Glue` (Phase 2 G26).
- [ ] A vendored Gum-bearing project loads its `.gumx` and shows a Screen's `.gusx` UI.
- [ ] `Level1`'s inherited Gum screen resolves (G53).
- [ ] `RuntimeType` is never used to resolve a `.gusx` — proven by a test with a bogus value (G54).
- [ ] The Gum-API spike in §6.0 is recorded here, with what was confirmed and what was not.
- [ ] Every gotcha in §5 is covered by a test or explicitly deferred.
