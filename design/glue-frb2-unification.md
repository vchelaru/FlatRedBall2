# Glue + FRB2 unification — FRB2-side work log

Making the Glue editor a first-class editor for FRB2 games. Most of the remaining work is in the
Glue repo; this file records what has been done **on the FRB2 side** so it can be resumed later.

Loading `.gluj`/`.glsj`/`.glej` as runtime data was finished earlier — see
`plan/804-glue-project-loader/`, all 14 phases implemented. Everything below is what running a real
Glue project through that loader turned up.

## Hot reload (issues #827, #828)

Glue writes into the project's source tree; the game reads the build output. Nothing bridged the
two, so a save in Glue was invisible until rebuild and relaunch.

`GlueScreen` now registers one content watcher rooted at the `.gluj`'s own directory. A change there
copies to the build output, reparses the project, and restarts onto the same Glue screen rebuilt
from the new data. Dev-only by construction — it registers only when `SourceContentRoots` is
non-empty, which is never true in a shipping build. `FlatRedBallService.IsGlueHotReloadEnabled` opts
out.

Four things this needed beyond the existing `WatchContentDirectory` machinery:

- **Reparse happens in the restart's configure, not the file callback.** One save in Glue writes
  several files and the watcher copies them one at a time, so reparsing on the first callback reads
  a half-updated tree. The queued restart runs after the whole batch has landed.
- **The hot-reload restart supplies its own configure.** The one the game passed to
  `Start<GlueScreen>` closes over the project loaded at boot, so a plain `RestartScreen` would
  rebuild from pre-edit data. The replacement re-resolves the screen by Glue name against the
  reloaded project. *This is the sharp edge: anything else that callback did — a difficulty, a seed,
  a hand-built object — is lost from the first Glue edit onward.*
- **The `.gluj` resolves against `OutputContentRoot`**, not the process working directory, which is
  the project folder under `dotnet run` and the output folder under a debugger. This retired the
  `Directory.SetCurrentDirectory` workaround in the scratch sample's `Program.cs`.
- **`bin`/`obj`/`.vs`/`.git` are dropped before the dirty set** (`ContentDirectoryWatcher.IgnoredDirectories`).
  A watch rooted above `Content/` otherwise restarts on every background build; `Content/obj` is
  MGCB's intermediate folder and hits this too.

Glue-authored file types (`.gluj`, `.glsj`, `.glej`, `.tmx`, `.csv`, `.achx`) are on the watcher's
auto-copy allowlist, so adding something in Glue is visible without a rebuild. Gum's file types are
excluded — Gum patches those in place, and restarting underneath that pass would tear down what it
just updated. That also closed #827: the watch root sits above `Content/`, so the existing `.gumx`
scan now auto-enables Gum hot reload for a Glue project's UI.

`WatchContentDirectoryContaining` picks the single source root holding the `.gluj` rather than every
root containing the directory. Running a sample from source needs this: the engine's own project is
a detected source root (`src/Content/` is a namespace folder, not content), and would otherwise get
a recursive watcher over the whole engine tree.

## Shape defaults matching the editor (issue #829)

An unstyled Glue shape drew nothing like the editor shows it. `.gluj` omits these values precisely
*because* they are defaults, so the fixes went into the engine rather than the deserializer —
otherwise "unset in Glue" and "unset in FRB2 code" drift apart and every future default change needs
a matching patch in `GlueObjectBuilder`.

| Property | Was | Now |
|---|---|---|
| `Color` | semi-transparent white (alpha 128) | opaque white |
| `IsFilled` | `true` | `false` |
| `OutlineThickness` | `2f` | `1f` |

Applied to `Circle`, `AARect`, `Polygon`, and `TileShapes` — a mixed set would just be the next
discrepancy.

## An entity on a screen appeared twice (issue #829)

`GlueProject.CreateEntity` registers the instance on the screen and builds its contents, and
`GlueObjectBuilder.AddTo(Screen, …)` then registered and `CustomInitialize`d it a second time. One
authored instance landed in the entity list twice and re-added its children to the render list, so it
updated and drew as two. `AddTo` now matches `GlueEntity` ahead of the generic `Entity` case and
leaves it alone — a `GlueEntity` is only ever constructed by `CreateEntity`, so it always arrives
already owned. Engine entities such as `CameraControllingEntity` still take the
register-and-initialize path.

Every fixture buries its entity instances inside a `PositionedObjectList`, which builds through
`BuildList` → `Create` and never double-registers; nothing covered an entity sitting straight on a
screen, which is the shape Glue produces by default.

`Screen.Register` is an unguarded `List.Add`, so the next caller to register twice gets the same
silent doubling. Making it idempotent (or throwing) is undecided.

## A top-down entity loaded its movement values and never moved (issue #829)

Nothing ticked the behaviour. FRB2 makes the per-frame call the entity's own job — a hand-written
entity does `TopDown.Update(this, time)` in its `CustomActivity` — and `GlueEntity` did not override
`CustomActivity` at all. The values loaded, the input bound, and both fed a behaviour that never ran.
The same hole sat under `Platformer`.

FRB1 hides this by generating the call into each entity's `Activity`
(`TopDownPlugin/CodeGenerators/EntityCodeGenerator.cs:344`, and the platformer plugin's equivalent),
gated on `IsTopDown` / `IsPlatformer` and on nothing else. The two generators do not know about each
other, so an element marked both gets both calls — which is why `GlueEntity.CustomActivity` ticks
both rather than picking a winner. The flags are read once per build, not per frame, and read before
`BuildObjects`' `Save`-null check so a pooled shell stops ticking what its previous life used.

The keyboard fallback was wrong in the same area: `GlueInputBinder` bound arrow keys, but FRB1's
generated `InitializeInput` uses `Keyboard.Default2DInput`, which is **WASD** and nothing else
(`FlatRedBall/Input/Keyboard.cs:110`). It now binds WASD, keeping arrows on top — FRB1 gives arrows
no default meaning, so accepting both is parity plus a superset rather than a behaviour change.

Nothing caught this because every movement test asserted on the loaded values and the bound input,
never on an entity that had been through a frame. The two that cover it now pump `Screen.Update` and
assert the entity actually moved.

## How to resume the sweep

Run `samples/GlueLoaderScratch` (deliberately near-empty — the round trip is the point, not a real
project's worth of content), compare against what the Glue editor shows, and fix the engine default
whenever the `.gluj` is silent about the value. Each discrepancy is one commit on issue #829's branch.

Keep `EngineInitSettings.GlueProjectFile` **relative**: `GlueContentSource` resolves through
`TitleContainer`, which throws on a rooted path, so an absolute `.gluj` loads and then fails every
asset it references.

## Known gaps, not yet worked

- **Deletion.** Removing an element in Glue leaves the stale copy in the build output. Harmless
  today because the reload reads the `.gluj`'s element list, but a rename leaving both files behind
  has not been exercised.
- **PNG dimension changes across a reload.** The engine-level texture cache is not cleared on a Glue
  reload, so a resized texture keeps the old dimensions until the game restarts.
- Not wired from FRB1: pooling (`PooledByFactory`), `SortAxis` partitioning, `CustomClasses`,
  `Events`. `plan/804-glue-project-loader/` records why for each.

## Pointers

| What | Where |
|---|---|
| Loader design, phase by phase | `plan/804-glue-project-loader/` |
| Using the loader, incl. the configure-replacement landmine | `glue-project-loading` skill |
| The watch/copy machinery underneath | `content-hot-reload` skill |
| Live test bed | `samples/GlueLoaderScratch` |
| Discrepancy sweep | issue #829, PR #830 |
