# Phase 5 — Gum Integration

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Not started |
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

### G56 — The Gum API this phase depends on is unverified

`frb-skills/gum-integration/SKILL.md:211-213` documents
`ObjectFinder.Self.GumProjectSave.Screens.Find(...)` + `ToGraphicalUiElement()`. That recipe has
**zero call sites anywhere in FRB2** — not in `src/`, `samples/`, `templates/`, or `tests/`.

FRB1's equivalent is `GumRuntime.ElementSaveExtensions.CreateGueForElement`, which lives in the
external Gum package, not in either repo. **Whether the Gum version FRB2 references exposes an
equivalent lookup by element name is unconfirmed, and it is the single biggest risk in this phase.**

**How we tackle it.** Spike this first, before writing any of §6. If the lookup is not available,
the phase's shape changes entirely and the plan needs rewriting rather than patching.

---

## 6. Tasks

Test-first throughout. **Do §6.0 before planning the rest.**

### 6.0 — De-risk

- [ ] Spike: confirm the referenced Gum version can instantiate a `GraphicalUiElement` from an
      element name in a loaded `.gumx` (G56). If not, stop and redesign.
- [ ] Verify the skill's documented recipe against the real API, and fix the skill either way.

### 6.1 — Project load

- [ ] Failing test: a `.gumx` in `GlobalFiles` is located and loaded.
- [ ] Failing test: a `.gusx` referenced before the `.gumx` has loaded produces an ordered load, not
      a failure.
- [ ] Failing test: `strippedName` drops the gum-project folder and the `screens/` prefix.

### 6.2 — Per-Screen association

- [ ] Failing test: DoorsDemo's `GameScreen` gets `GameScreenGum` added as a `GraphicalUiElement`.
- [ ] Failing test: `RuntimeType` is **not** used for lookup (G54) — a project whose `RuntimeType`
      names a nonexistent class still resolves.
- [ ] Failing test: a `GumIdb` `RuntimeType` warns as an unsupported legacy model.

### 6.3 — Inheritance

- [ ] Failing test: `Level1` — which declares no `.gusx` — shows its base's Gum screen (G53).

### 6.4 — Gum NamedObjects

- [ ] Failing test: a `SourceType.Gum` object resolves by type name, not by treating `SourceFile` as
      a path (G52).
- [ ] Failing test: a `.gucx`-sourced `SourceType.File` object resolves by element name.
- [ ] Add a `GlueTypeMap` classification for Gum runtime types (G51).

### 6.5 — Fixtures and wrap-up

- [ ] Vendor `FormsSampleProject` (`FileVersion` 61) — Phase 1's D4 already nominated it as the Gum
      fixture, and it is the newest sample in the repo.
- [ ] Vendor the `.gumx` and `.gusx` content alongside it (Phase 4 G49 — no content is vendored yet).
- [ ] XML docs; update this document and `plan/plan.md`.

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D50 | Depend on Phase 6, or walk `BaseScreen` for referenced files only? | **Depend on Phase 6 if it has landed; otherwise walk the base chain for `ReferencedFiles` alone.** The narrow walk is ~20 lines and unblocks the primary fixture (G53). Do not let it grow into a partial inheritance implementation — if it needs `CustomVariables` too, stop and do Phase 6. |
| D51 | Wire Gum Forms controls? | **No.** `Forms = rfs.FormsControl ?? new <FormsType>(rfs)` needs a generated `<FormsType>` per component, which is exactly the codegen this epic removes. Revisit only alongside the "future partial codegen" direction in the issue's ground rules. |
| D52 | Honour `ShowMouse`? | **No.** It emits `Game.IsMouseVisible = true` into generated GlobalContent. One line, but it is a game-shell concern rather than a loader one, and FRB2 games set it themselves. Warn if set. |

---

## 8. Definition of done

- [ ] `dotnet build` clean; `dotnet test` green.
- [ ] A real `PublishTrimmed` emits no IL warnings from `src/Glue` (Phase 2 G26).
- [ ] A vendored Gum-bearing project loads its `.gumx` and shows a Screen's `.gusx` UI.
- [ ] `Level1`'s inherited Gum screen resolves (G53).
- [ ] `RuntimeType` is never used to resolve a `.gusx` — proven by a test with a bogus value (G54).
- [ ] The Gum-API spike in §6.0 is recorded here, with what was confirmed and what was not.
- [ ] Every gotcha in §5 is covered by a test or explicitly deferred.
