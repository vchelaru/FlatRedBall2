---
name: glue-project-loading
description: Run an FRB1/Glue project's .gluj/.glsj/.glej data directly in FRB2, with no Glue codegen. Triggers: GlueProject, GlueScreen, GlueEntity, EngineInitSettings.GlueProjectFile, GlueTypeMap, .gluj, CustomVariables by name.
---

# Loading Glue Projects

FRB2 reads the JSON files Glue writes and builds Screens and Entities from them at runtime. There is
no generated C#: `src/Glue/` is the whole implementation, `GlueProject` is the entry point.

## Where to start

| Task | Go to |
|---|---|
| Boot a project | `EngineInitSettings.GlueProjectFile` → `FlatRedBallService.GlueProject` |
| Load without booting | `GlueProject.Load(glujPath, content)` |
| Find/create an element | `GlueProject.FindScreen`/`FindEntity`/`CreateScreen`/`CreateEntity` |
| Move between screens | `Screen.MoveToScreen(string glueName)` |
| Read/write an authored variable | the indexer on `GlueScreen`/`GlueEntity`, or `Get<T>` |
| Claim a Glue type for FRB2 | `GlueTypeMap` |

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
and replays it on `RestartScreen`. Assigning outside means a restarted screen rebuilds with no data —
a silently *empty* screen rather than an error. `MoveToScreen(string)` already does this correctly;
match it when calling `Start<GlueScreen>` yourself.

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

## Hot reload

A `GlueScreen` watches the source tree the `.gluj` sits in and restarts itself when Glue writes
there: copy the changed file to the build output, reparse the project, rebuild the same screen from
the new data. Nothing to call — it registers in `CustomInitialize` whenever
`FlatRedBallService.SourceContentRoots` is non-empty, so it is dev-only by construction. Opt out with
`FlatRedBallService.IsGlueHotReloadEnabled` before the first screen starts.

**The reload restart replaces the retained `configure` callback.** Anything else your
`Start<GlueScreen>` callback did — a difficulty, a seed, a hand-built object — is gone from every
restart after the first Glue edit, because the replacement only reassigns `Save` and `Project`. Put
that setup in a `GlueScreen` subclass's `CustomInitialize` or in `RestoreHotReloadState`.

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

## What is not wired

Pooling (`PooledByFactory`), `SortAxis` partitioning, and input binding (`JumpInput`,
`MovementInput`, `EntitySave.InputDevice`) are parsed and not applied — a loaded platformer entity
has its authored physics but nothing telling it to move. `CustomClasses` and `Events` are out of
scope entirely. `plan/804-glue-project-loader/` records why for each.
