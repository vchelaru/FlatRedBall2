---
name: glue-project-loading
description: "Run an FRB1/Glue project's .gluj/.glsj/.glej data directly in FRB2, with no Glue codegen. Triggers: GlueProject, GlueScreen, GlueEntity, EngineInitSettings.GlueProjectFile, GlueTypeMap, .gluj, CustomVariables by name."
---

# Loading Glue Projects

FRB2 reads the JSON files Glue writes and builds Screens and Entities from them at runtime. There is
no generated C#: `src/Glue/` is the whole implementation, `GlueProject` is the entry point.

Booting one is a single call in `Game1.Initialize`, after `base.Initialize()`:

```csharp
FlatRedBallService.Default.Initialize(this, "Content/FrbEditor/MyGame.gluj");
```

The project names its own start-up screen, Gum project and resolution, so nothing else is passed —
the engine wires the screen's `Save`/`Project` itself. **The path must be relative**: it resolves
through `TitleContainer`, which throws on a rooted path, so an absolute one loads the project and
then fails every asset it references. To load a project but boot your own screen instead, use
`Initialize<MyScreen>(game, new EngineInitSettings { GlueProjectFile = … })`.

## Where to start

| Task | Go to |
|---|---|
| Boot a project | `Initialize(game, "Content/FrbEditor/MyGame.gluj")` |
| Load without booting | `GlueProject.Load(glujPath, content)` |
| Find/create an element | `GlueProject.FindScreen`/`FindEntity`/`CreateScreen`/`CreateEntity` |
| Move between screens | `Screen.MoveToScreen(string glueName)` |
| Read/write an authored variable | the indexer on `GlueScreen`/`GlueEntity`, or `Get<T>` |
| Claim a Glue type for FRB2 | `GlueTypeMap` |
| Bind an entity to your own class | `FlatRedBallService.GlueEntities.Register<T>(name)` |

## Every loaded element shares one C# type

`GlueScreen` and `GlueEntity` are what a loaded element *is*; only its `Save` data distinguishes it.
So `MoveToScreen<T>()` and `Factory<T>` cannot tell two loaded screens apart — the by-name overloads
(`MoveToScreen(string)`, `GlueProject.CreateEntity(name, screen)`) exist for exactly this reason.
Both directions work: a loaded screen can still `MoveToScreen<AHandWrittenScreen>()`.

Names are Glue's own, with the folder: `Screens\Level1`, `Entities\Player`. Either separator is
accepted and case is ignored, but the prefix is required — a bare leaf is ambiguous, since a screen
and an entity can share one.

## Landmines

**Assign `Save` inside the `configure` callback, never before it.** The engine retains that callback
and replays it on `RestartScreen`; assigning outside means a restarted screen rebuilds with no data —
a silently *empty* screen rather than an error. Only applies when you construct a `GlueScreen`
yourself: `Initialize(game, glujPath)` and `MoveToScreen(string)` both do it correctly.

**Loading is tolerant by design: it collects diagnostics instead of throwing.** A project that
references a type this build cannot construct still loads, minus that object. Check
`GlueLoadResult.HasErrors` and `Diagnostics`, and `GlueScreen.BuildDiagnostics` for per-element
problems — otherwise a half-built screen looks like a working one. `GlueLoadOptions.Strict` turns
the first diagnostic into a throw when you would rather fail loudly.

**Adding a row to `GlueTypeMap` requires a matching `DynamicDependency`.** The reflected set is
closed and the engine publishes AOT/trimmed; without it the type's properties are trimmed away and
every assignment to them silently does nothing — in published builds only, never in `dotnet build`.

**A Glue project's Gum UI must be loaded through `GlueProjectFile`, not `GumProjectFile`.** Gum only
resolves elements from a project its own `GumService` loaded, and the `.gumx` path is not known until
the `.gluj` has been read. See `gum-integration` for what goes wrong otherwise.

**Abstract elements cannot be created.** Glue marks an element abstract when it leaves an object for
a derived element to supply, so it is incomplete by construction. `CreateScreen`/`CreateEntity`
throw; this is normal for a `GameScreen` base that levels derive from.

**A list belongs to the screen or entity instance that declares it.** `owner.GetList("Bullets")` returns that instance's own live `GlueEntityList`, so each instance of an entity has a separate one and an entity list never merges with a screen list of the same name. Add and remove members directly. `CreateEntity` joins the spawn to `project.ListsToAddTo(entity)`, FRB1's factory list set. A screen fills it at init, for each `AssociateWithFactory` list, with the list's own entity if it has a factory (`CreatedByOtherEntities`, not abstract), plus every such descendant when the list's entity is the most base one. A list typed with an in-between entity does not reach the entities below it. This follows FRB1's docs on purpose; FRB1's generator reaches all descendants. `AddList`/`RemoveList` act on exactly the entity named, with no cascade. Entity lists are never associated by default; a screen change clears the set. Members leave every list when destroyed, and an entity's list members are destroyed with it.

## Hot reload

A `GlueScreen` watches the source tree the `.gluj` sits in and restarts itself when Glue writes
there: copy the changed file to the build output, reparse the project, rebuild the same screen from
the new data. Nothing to call — it registers in `CustomInitialize` whenever
`FlatRedBallService.SourceContentRoots` is non-empty, so it is dev-only by construction. Opt out with
`FlatRedBallService.IsGlueHotReloadEnabled` before the first screen starts.

**The reload restart replaces the retained `configure` callback.** `Save` and `Project` survive —
the replacement reassigns them — but anything else a callback you passed did, a difficulty or a seed
or a hand-built object, is gone from every restart after the first Glue edit. Put that setup in a
`GlueScreen` subclass's `CustomInitialize` or in `RestoreHotReloadState`.

Gum files are left to Gum's own in-place pipeline, and `bin`/`obj` are filtered by
`ContentDirectoryWatcher.IgnoredDirectories`. `content-hot-reload` covers the watch/copy machinery
underneath.

## Variables: two surfaces, two intents

`Objects` is the typed dictionary of built objects — `(Circle)screen.Objects["CooldownCircle"]`. The
indexer is the variable bag: `entity["Health"] = 100`, `entity.Get<int>("Health")`.

The indexer resolves in a fixed order, and the order is the point: a name matching a real CLR member
writes that member (`entity["X"] = 5f` moves the entity), a tunneling variable reaches the contained
object it targets, and only an unclaimed name is held by name. `Get<T>` is driven by `T`, not by the
variable's declared type — Glue's declared types are frequently not CLR types at all.

## Inheritance is resolved at load, once

`GlueProjectLoader` flattens every element into the union of its chain before anything inspects it,
so a derived screen already carries its base's objects, variables, states, and referenced files.
Code reading a `ScreenSave` never walks `BaseScreen` itself.

## Code-behind: your class for a Glue entity

```csharp
FlatRedBallService.Default.GlueEntities.Register<Player>(@"Entities\Player");
FlatRedBallService.Default.Initialize(this, "Content/FrbEditor/MyGame.gluj");
```

`Player` derives from `GlueEntity`, and everything Glue authored (objects, variables, states, lists,
movement) is applied to it. `GlueEntityRegistry` belongs to the engine, so it works before
`Initialize` and survives a hot reload. A derived Glue entity with no registration of its own uses
its nearest registered ancestor's class.

**`CustomInitialize` runs once per instance and `Reset()` is the per-life hook,** as for a
hand-written pooled entity. FRB1's `CustomInitialize` ran on every reuse, so move that code to
`Reset()`. Both run after the Glue data and the spawner's overrides are applied.

**A pooled registered entity is not rebuilt on reuse.** Its objects are kept (so references taken in
`CustomInitialize` stay valid) and put back to their authored values. A pooled entity with no
registration is still rebuilt each time.

A Glue variable or per-instance override is written to the class's property of the same name. A
variable with no property is kept by name (`Get<T>`); an override with none is a build diagnostic.

## What is not wired

`SortAxis` partitioning is parsed and not applied. `CustomClasses` and `Events` are out of
scope entirely. `plan/804-glue-project-loader/` records why for each.
