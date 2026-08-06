# Phase 4 — Referenced Files / Assets

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — see §9. Content scope (D40) deferred to Phase 14. |
| **Depends on** | Phase 2 (objects exist to receive assets) |
| **Blocks** | Phase 5 (Gum), Phase 10 (TMX), Phases 11–12 (movement CSVs) |
| **Suggested branch** | `804-phase-4-referenced-files` |

---

## 1. The problem

Every sprite in every loaded project is currently textureless. Phase 2 constructs a `Sprite` and
positions it correctly; nothing gives it an image. **This is the phase that makes a loaded project
look like the game it is.**

It is also the phase three later phases wait on: Gum (`.gumx`), Tiled (`.tmx`), and both movement
phases (CSV) all arrive as `ReferencedFileSave` entries and cannot start until this lands.

---

## 2. Scope

### In scope

1. `GlobalFiles` (project-level) and per-element `ReferencedFiles`.
2. Resolving a `SourceType.File` `NamedObjectSave` against a `ReferencedFileSave`.
3. Resolving an **instruction value** that names an RFS by instance name (G43 — the case that
   actually matters for sprites).
4. Loading textures, `.achx` animation chain lists, and CSVs.
5. A decided story for content-manager scope (D40).

### Out of scope

- `.gumx` / `.gucx` / `.gusx` → Phase 5.
- `.tmx` → Phase 10.
- Parsing CSV *rows* into movement values → Phases 11–12. This phase loads the file and makes it
  addressable.
- `.scnx`, `.shcx`, `.emix`, `.splx`, `.nntx` — FRB2 has none of these formats (G48).
- Wildcard `GlobalFiles` (zero occurrences repo-wide; guard and warn).
- `ProjectSpecificFiles`, `ConditionalCompilationSymbols` — a runtime loader has no `#if`.

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | Sprites have textures | DoorsDemo's door and player are visible, not blank. | §6.3 |
| F2 | Animations play | `CurrentChainName: "Closed"` puts the door in its closed pose. | §6.4 |
| F3 | Content is scoped | Screen assets unload on transition; global ones do not. | §6.5 |
| F4 | CSVs are addressable | Phase 11/12 have a loaded table to read rows from. | §6.6 |
| F5 | Missing assets are survivable | A project with one bad path still loads everything else. | §6.7 |

---

## 4. Proposed resolution

### Path resolution

`ReferencedFileSave.Name` is **relative to the project's `Content` folder**, forward-slashed
(`GlueProjectSave.FixReferencedFileBackSlash`, `GlueProjectSave.cs:521-529`). So
`Entities/Door/AnimationChainListFile.achx` resolves to
`<glujDir>/Content/Entities/Door/AnimationChainListFile.achx`.

All reads go through `ContentLoader.StreamProvider` (`src/ContentLoader.cs:50`), never `File.*` —
the same WASM discipline Phase 1 established for the project files.

### Type dispatch is `(extension, RuntimeType)`, not either alone

`ReferencedFileSaveExtensionMethods.cs:314-337`: try the pair, then `RuntimeType` alone, then the
extension. The pair is necessary because `.scnx` maps to three different types, `.wav` to two, and
`.achx` to both an FRB and a Gum type. See G46.

### The dispatch table

Ported from `FRBDK/Glue/Glue/Content/ContentTypes.csv` plus the plugin-added rows, trimmed to what
FRB2 can actually load:

| Ext | `RuntimeType` | FRB2 |
|---|---|---|
| `.png` `.bmp` `.jpg` `.gif` `.tga` | `Microsoft.Xna.Framework.Graphics.Texture2D` | `ContentLoader.Load<Texture2D>` |
| `.achx` | `FlatRedBall.Graphics.Animation.AnimationChainList` | `ContentLoader.LoadAnimationChainList` |
| `.csv` | *(absent or `""`)* | Phase 4 loads text; 11/12 parse |
| `.wav` | `…Audio.SoundEffect` | `ContentLoader.Load<SoundEffect>` |
| `.mp3` `.wma` | `…Media.Song` | `ContentLoader.Load<Song>` — pipelined, extension stripped |
| `.tmx` | `FlatRedBall.TileGraphics.LayeredTileMap` | Phase 10 |
| `.gumx` `.gusx` `.gucx` | *(various)* | Phase 5 |
| `.scnx` `.shcx` `.emix` `.splx` `.nntx` `.fnt` | — | **no FRB2 equivalent** (G48) |

---

## 5. Gotchas

### G40 — Four `ReferencedFileSave` defaults are `true` in FRB1 and `false` in FRB2 · **Blocker**

`ReferencedFileSave` sets these in its constructor (`ReferencedFileSave.cs:514-535`):
`LoadedAtRuntime`, `DestroyOnUnload`, `IsSharedStatic`, `AddToManagers` — all **`true`**. Glue writes
with `DefaultValueHandling.Ignore`, so **`true` never lands on disk**; only `false` is written
explicitly.

FRB2's `src/Glue/Model/ReferencedFileSave.cs` **has no constructor**, so every one reads `false`.
Today that is inert because nothing consults them. The moment this phase honours `LoadedAtRuntime`,
**every asset in every project stops loading** — and the fixtures would agree, because the flag is
absent from all of them.

This is Phase 1's G3 recurring in a class nobody re-audited. The lesson from G24 applies exactly:
**test against JSON that omits the member.**

Also missing from the mirror entirely: `LoadedOnlyWhenReferenced`, `UseContentPipeline`,
`CreatesDictionary`, `TreatAsCsv`, `CsvDelimiter`, `IncludeDirectoryRelativeToContainer`,
`ConditionalCompilationSymbols`, `AddToManagers`, `IsManuallyUpdated`, `IsDatabaseForLocalizing`,
`SourceFile`, `BuildTool`, `UniformRowType`.

### G41 — `LoadedOnlyWhenReferenced` is written *twice*, and is bag-backed without `[JsonIgnore]`

`ReferencedFileSave.cs:251-260` has a getter and setter that go through `Properties` — but the
property is **not** `[JsonIgnore]`, so Newtonsoft also writes it as a top-level member. Verified: 15
top-level occurrences and 15 bag occurrences, always agreeing.

`Category` on `CustomVariable` has the identical shape (Phase 3 G38). Two independent instances of
the same pattern means it is deliberate, not a bug — treat the bag as authoritative (Phase 1's D8).

### G42 — Absence of `SourceType` means `File`, and it is the most common case

`SourceType.File` is `0`, so `DefaultValueHandling` drops it. Sweep of every NamedObject in FRB1:

| top-level `SourceType` | in `Properties` | count |
|---|---|---|
| absent | `0` | **70** |
| absent | absent | **9** |
| `1` (Entity) | `1` or absent | 175 |
| `2` (FlatRedBallType) | `2` or absent | 519 |
| `3` (Gum) | `3` or absent | 4 |

They never disagree. FRB2's `SourceType` property already defaults to `File`, so this is correct
today — but **"no `SourceType` key" must never be treated as an error**, and 9 objects have it in
neither place.

### G43 — A Sprite does not get its texture through `SourceType.File` · **the central finding**

There is **no `.achx`-sourced NamedObject anywhere in FRB1**. A Sprite is an ordinary
`SourceType.FlatRedBallType` object whose **`InstructionSaves` carry the RFS *instance name* as a
bare string**. DoorsDemo's `Door.glej`:

```
RFS  Name = "Entities/Door/AnimationChainListFile.achx"
NOS  SpriteInstance, SourceType=2, SourceClassType='FlatRedBall.Sprite'
     instr AnimationChains  (AnimationChainList) = "AnimationChainListFile"   <- instance name
     instr CurrentChainName (string)             = "Closed"
```

FRB1 emits the value as a **bare identifier**, quotes stripped
(`CustomVariableCodeGenerator.cs:688-696`), because `GetIsFile` recognises the declared type as a
file type (`CustomVariableExtensionMethods.cs:484-539`).

Repo-wide, file-typed instructions are: `Texture`/`Texture2D` ×17, `AnimationChains` ×15,
`CurrentChainName` ×15, `Font` ×2.

**How we tackle it.** Build an instance-name → loaded-asset map per element (plus GlobalContent), and
resolve any instruction whose declared type is a file type through it. The key transform is
`ReferencedFileSaveExtensionMethods.GetInstanceName` (`:145-214`): drop the extension, strip spaces
and parentheses, `-`→`_`, then **strip the whole path** unless
`IncludeDirectoryRelativeToContainer`, in which case make it container-relative with `/`→`_`. A
leading digit gets a `_` prefix.

That path-stripping means `Entities/A/Anim.achx` and `Entities/B/Anim.achx` collide within one
element. Three files repo-wide opt out. **Use the same transform or the lookup mis-resolves.**

### G44 — `CurrentChainName` is a method call, not a property

FRB2's `Sprite` has no `CurrentChainName` setter; the equivalent is `PlayAnimation(string)`
(`src/Rendering/Sprite.cs:324`). Property reflection cannot reach it, so Phase 2's
`ApplyInstructions` warns and skips — which is why the door never closes.

**How we tackle it.** Add a member-name → action hook alongside the property path. `AnimationChains`
must be assigned *before* `CurrentChainName` is invoked, so the hook has to respect ordering; FRB1
gets this free because `VariableDefinitions` reorder instructions
(`NamedObjectSaveCodeGenerator.cs:330-346`). Port that ordering, or the animation name resolves
against an empty list.

### G45 — Pipelined assets have their extension stripped, and the pipeline flag is per-file

`GetFileToLoadForRfs` (`:964-989`) strips the extension when the ATI says
`MustBeAddedToContentPipeline` **or** the file sets `UseContentPipeline`. That is `true` by ATI only
for `.mp3`/`.wma`/`.fx`/`.x`/`.fbx` — **`.png` is `false` by default** but flips per-file via the bag
key (18 occurrences observed).

So `RFS.Name` and the actual on-disk artifact diverge, and the divergence is not predictable from the
extension. FRB2's `ContentLoader.Load<T>` already keys on `Path.HasExtension`
(`src/ContentLoader.cs:95`), which happens to match FRB1's split — but FRB1 dispatches on the
requested `T` while FRB2 only special-cases `Texture2D`.

Related: FRB1 needs `ContentManager.FileAliases` (`ContentManager.cs:92-102`) precisely because a
pipelined `.png` is referenced *with* its extension by `.achx` and Gum files but must load *without*
it. If this phase strips extensions, that aliasing problem reappears.

### G46 — Case: FRB1 lowercases the whole path below `FileVersion` 55

`GetFileToLoadForRfs:986` calls `.ToLowerInvariant()` unless
`FileVersion >= GluxVersions.CaseSensitiveLoading` (55). **Two of three vendored fixtures are
version 42.**

Reading `RFS.Name` verbatim is *more* correct than FRB1, but diverges from a project authored against
the lowercased behaviour. `GlueLoadOptions.ResolveFilePath` (`src/Glue/GlueLoadOptions.cs:25`,
`:37-50`) already does case-insensitive fallback with a warning — **extend it to content rather than
inventing a second policy.**

### G47 — Content-manager semantics have no FRB2 analogue

FRB1 has named content managers, `AddUnloadMethod`, per-name `LoadedContentManagers`,
`IsLoaded<T>(name, manager)`, and `GlobalContent.X` aliasing — where an element that
`UseGlobalContent` and references a file already in `GlobalFiles` **does not reload it**, it aliases
(`ReferencedFileSaveCodeGenerator.cs:1238-1258`).

FRB2 has two `ContentLoader` *instances* — `Engine.Content` and `Screen.ContentLoader` — with no
naming, no "is it loaded" query, and `Engine.Content` is **never unloaded**.

**How we tackle it.** D40. The aliasing rule is the behaviour most likely to be missed silently.

### G48 — Six FRB1 asset formats have no FRB2 equivalent

`.scnx` (Scene), `.shcx` (ShapeCollection), `.emix` (EmitterList), `.splx` (SplineList), `.nntx`
(NodeNetwork), `.fnt` (BitmapFont — FRB2 generates fonts in memory via `KernSmithFontCreator`,
`src/FlatRedBallService.cs:282-283`).

Combined they account for a meaningful share of FRB1's test project but **none appears in the three
vendored fixtures**. Warn by name and move on; do not add five file formats to satisfy a loader.

### G49 — No content is vendored with the fixtures

`tests/FlatRedBall2.Tests/Glue/Fixtures/` holds only `.gluj`/`.glsj`/`.glej` — the README says so
explicitly. Every test in this phase that actually loads an asset needs content copied in.

Smallest useful end-to-end case: DoorsDemo's `Door.glej` (one `.achx` plus the Sprite instruction).
`Player.glej` adds the CSV-dictionary case.

### G4A — Texture loading *is* testable; the device question is settled

An open worry going in was that `ContentLoader.Load<Texture2D>` needs a `GraphicsDevice`, which a
test host might not be able to create — making the phase's headline outcome unverifiable.

**Measured rather than assumed: it works.** A real device is created inside the xUnit host via a
`Game` plus `GraphicsDeviceManager` and a single `RunOneFrame()`, which forces device creation
without a message loop the host cannot pump. `Texture2D` creation then succeeds, and the full suite
stays green with roughly 200 ms added.

`tests/FlatRedBall2.Tests/GraphicsDeviceFixture.cs` provides it as a shared collection fixture — one
device for the whole run, since creating one per class is slow and risks contending for the context.

**The caveat is CI, not this machine.** Device creation needs a GL context rather than merely a GPU,
so a headless agent gets `IsAvailable == false`. Tests that need a device must check it and skip;
the fixture carries its own guard test so that a regression in device creation surfaces instead of
turning every asset test silently green.

---

## 6. Tasks

Test-first throughout.

### 6.1 — Model completion

- [x] Failing test: an RFS deserialized from JSON that **omits** `LoadedAtRuntime` reports `true`
      (G40) — asserted on deserialized JSON, never on `new ReferencedFileSave()`.
- [x] Add the constructor defaults and the missing members (G40).
- [ ] Failing test: `LoadedOnlyWhenReferenced` agrees between its member and its bag entry (G41).

### 6.2 — Path and type resolution

- [x] Failing test: `Entities/Door/AnimationChainListFile.achx` resolves under `Content/`.
- [ ] Failing test: dispatch is `(extension, RuntimeType)` — `.wav` resolves to `SoundEffect` or
      `SoundEffectInstance` by `RuntimeType` (G46 table).
- [ ] Failing test: an RFS with `RuntimeType: ""` falls back to the extension — 9 CSVs do this.
- [x] Content reads route through `StreamProvider`, never `System.IO`.
- [ ] Failing test: a case-only mismatch resolves with one warning, reusing `ResolveFilePath` (G46).

### 6.3 — Textures

- [x] Failing test: a `Texture` instruction naming an RFS instance name sets `Sprite.Texture` (G43).
- [x] Failing test: the instance-name transform matches `GetInstanceName` — spaces and parens
      stripped, `-`→`_`, path dropped, leading digit prefixed (G43).
- [ ] Failing test: two same-named files in one element collide and warn (G43).
- [x] Vendor DoorsDemo's content (G49) — a focused 108 KB slice, not the whole 1.4 MB folder.
- [x] Settle whether a real `GraphicsDevice` is obtainable in tests — it is (G4A). Use
      `GraphicsDeviceFixture` via `[Collection(GraphicsDeviceCollection.Name)]`, and skip on
      `!IsAvailable` so headless CI stays green.

### 6.4 — Animation

- [x] Failing test: `AnimationChains` assigns a loaded `AnimationChainList`.
- [x] Failing test: `CurrentChainName` invokes `PlayAnimation` (G44).
- [ ] Failing test: `AnimationChains` is applied before `CurrentChainName` regardless of file order
      (G44).

### 6.5 — Content scope

- [ ] Failing test: a `GlobalFiles` asset outlives a screen transition.
- [ ] Failing test: a per-element asset in a screen is released on transition.
- [ ] Failing test: an element that `UseGlobalContent` and names a file already in `GlobalFiles`
      aliases rather than reloading (G47).
- [ ] Implement per D40.

### 6.6 — CSV

- [x] Failing test: a `.csv` RFS loads and is addressable by instance name.
- [x] `CreatesDictionary` is modelled and read (needed by Phases 11–12).
- [ ] Row parsing is **not** in this phase — assert only that the text is available.

### 6.7 — Diagnostics

- [x] Failing test: a missing content file warns and leaves the rest loaded.
- [ ] Failing test: each G48 format warns by name.
- [x] Failing test: `LoadedAtRuntime: false` skips loading, silently and correctly.
- [x] Failing test: an RFS `Name` containing `*` warns as unsupported.

### 6.8 — Wrap-up

- [ ] XML docs; update this document and `plan/plan.md`.
- [ ] Fix `FlatRedBallService.Content`'s XML doc — it claims "auto-recreated each screen change",
      which is false (`src/FlatRedBallService.cs:907`).
- [ ] Fix `frb-skills/content-and-assets/SKILL.md`: it says raw PNGs load via `Texture2D.FromFile`
      (the real path is `FromStream` over `StreamProvider`, `ContentLoader.cs:211-212`), and it says
      "each screen gets its own ContentLoader — assets unload on transition", which is true of
      `Screen.ContentLoader` but false of `Engine.Content`, which every example on the page uses.

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D40 | How do `UseGlobalContent` / `IsSharedStatic` map onto FRB2's two loaders? | **`GlobalFiles` → `Engine.Content`; per-element files → the owning `Screen.ContentLoader`; `IsSharedStatic` on a Screen-owned file → still the screen's.** This reproduces the observable lifetime without importing named content managers. Implement the GlobalContent aliasing rule (G47) explicitly — it is the one behaviour whose absence is invisible until memory grows. |
| D41 | Honour `LoadedOnlyWhenReferenced`? | **No — load eagerly, warn.** It exists to defer expensive loads, and FRB2 has no lazy-asset concept. 15 occurrences, none in a sample. Revisit if a real project stalls on load. |
| D42 | Add a `.fnt` / `BitmapFont` path? | **Defer to Phase 5.** Gum is the only consumer of bitmap fonts in FRB2, so the decision belongs where the requirement is. |
| D43 | Reproduce FRB1's lowercasing below version 55? | **No.** Read `RFS.Name` verbatim, with `ResolveFilePath`'s case-insensitive fallback as the safety net (G46). Reproducing a legacy bug on a case-sensitive host is worse than warning about it. |

---

## 8. Definition of done

- [x] `dotnet build` clean; `dotnet test` green (**1342**).
- [x] A real `PublishTrimmed` emits no IL warnings from `src/Glue`, and the trimmed binary runs.
- [x] **DoorsDemo's `Door` shows its texture and starts on the `Closed` animation** — confirmed by
      booting it through the engine and screenshotting the back buffer, not by test alone.
- [x] `Player.glej`'s CSV loads and is addressable, with `CreatesDictionary` modelled.
- [x] Every `ReferencedFileSave` default is asserted against JSON that **omits** it (G40).
- [x] Every gotcha in §5 is covered by a test or explicitly deferred.
- [x] No asset read touches `System.IO` — reads go through `ContentLoader.StreamProvider`.
- [ ] Content scope (F3 / D40) — deferred, see §9.

---

## 9. What landed

8 new tests, full suite **1342 green**, build clean, trimmed publish verified by running it, and the
result confirmed visually: DoorsDemo's door renders with its texture and authored animation.

| Piece | File |
|---|---|
| Asset loading and the instance-name map | `src/Glue/GlueContentSource.cs` |
| Constructor defaults + missing members | `src/Glue/Model/ReferencedFileSave.cs` |
| Asset-valued instructions, member-to-action hook | `src/Glue/GlueObjectBuilder.cs` |
| A real `GraphicsDevice` for tests | `tests/FlatRedBall2.Tests/GraphicsDeviceFixture.cs` |

### Found while building

- **A test that asserted nothing.** `sprite.CurrentAnimation?.Name.ShouldBe("Closed")` — the
  null-conditional short-circuits when the animation failed to play, so the assertion never runs and
  the test passes green. Caught by deliberately breaking the implementation and finding the test
  still passed. It now asserts non-null first. **Any `?.` before a `Should*` is a test that can only
  pass**, and this is the second time this session that "green" turned out to mean "not actually
  checked".
- **One bad asset took down the whole element.** The catch filter listed `IOException`,
  `InvalidOperationException` and `NotSupportedException`; an absolute content root makes
  `TitleContainer` throw `ArgumentException`, which escaped and killed the load. That breaks the
  loader's central promise — a bad asset should cost you that asset and nothing else. Now caught
  broadly, with the reasoning recorded at the catch site.
- **The content root resolves against the executable, not the working directory.** `TitleContainer`
  is the seam everything reads through, and it resolves relative to the title location. This cost a
  debugging cycle even knowing the codebase; the XML doc now says it outright.
- **The diagnostic chain proved its worth.** When the door did not appear, three warnings explained
  it in order: the `.achx` could not be loaded → the instruction naming it kept its default → the
  animation could not play without a list. Root cause was readable without a debugger.

### Deferred, with reasons

- **Content scope (F3, D40).** Mapping `UseGlobalContent`/`IsSharedStatic` onto FRB2's two loaders
  needs a project-level owner for global content, and there is none —
  `GlueContentSource` is per-caller by design. That is the same missing project context Phase 14
  G140 already owns; doing it here would mean inventing half of it twice.
- **`.wav` / `.mp3`.** The dispatch table covers them and `ContentLoader` can load them, but no
  vendored fixture has audio, so the path would ship untested.
- **`List<Vector2>` / polygon points**, carried over from Phase 3. Still needs the member-to-action
  hook to reach `Polygon.SetPoints` — that hook now exists for `CurrentChainName`, so this is a
  small follow-up rather than a design question.
