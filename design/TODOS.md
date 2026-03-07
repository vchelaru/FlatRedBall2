# FlatRedBall2 — Todo

See `Done.md` for completed items.

## Gum Project File Loading (.gumx)
**Priority: High**

FRB2 runs Gum in code-only mode (`DefaultVisualsVersion.V3`). Full `.gumx` project support lets designers build UI in the Gum tool and have it loaded at runtime.

**MonoGameGum entry point**: `GumService.Initialize(Game game, string gumProjectFile)` — already exists. The `gumProjectFile` is a path to the `.gumx` file. Returns the loaded `GumProjectSave`, or null if no project was loaded.

**What FRB2 must add**:
1. **Initialization path** — Done. `EngineInitSettings.GumProjectFile` passed to `FlatRedBallService.Initialize`.
2. **Content/font loading** — Gum loads fonts (`.fnt` + texture atlas) and textures from disk directly, not through the MonoGame content pipeline. Verify the correct `Content.RootDirectory` is set and that `.fnt`/`.png` assets are copied to output (not processed by MGCB). **Blocked** — waiting on Gum command-line / starter project work.
3. **Animation loading** — Done. `_gum.LoadAnimations()` called automatically when a project file is provided.

## Gum — Default Starter Project
Provide a minimal empty `.gumx` project checked into the repo (e.g., `tools/Gum/StarterProject/`) that Claude and developers can use as a starting point when a game needs Gum UI. Should include the bare minimum: a valid project file, empty screens folder, and any required default font assets.

## Gum — Tool Location for Claude
Record the path to the Gum tool `.exe` in a known location (e.g., `tools/Gum/` or a `CLAUDE.md` / skill file entry) so Claude can reference it without searching. Include the version pinned to the repo.

## Gum — Codegen via Gum Tool
Document and expose how to invoke the Gum tool in codegen mode so Claude can generate C# code from a `.gumx`/`.gucx` file. Record the CLI invocation (flags, input/output paths) in the `gum-integration` skill file so Claude can run it as part of a workflow.

## Gum — Tool Error Reporting
The Gum tool should have a well-defined error reporting mechanism (e.g., non-zero exit code + structured stderr output) so that Claude can detect failures and surface them clearly. Define what a failed codegen run looks like and how errors are communicated back to the caller.

## Audio System
**Priority: Medium** — All `AudioManager` methods throw `NotImplementedException`.

- `IAudioBackend` implementation using MonoGame's `SoundEffect` and `Song` APIs
- `ContentManagerService` integration for loading `.wav` / `.mp3` / `.ogg` assets
- Volume control, looping, fade in/out

## ACHX Animation Format
**Priority: Medium** — `Sprite.PlayAnimation` is a no-op.

- Parser for `.achx` XML format (from FRB1)
- `AnimationChain` and `AnimationFrame` runtime types
- Per-frame texture region, timing, and relative X/Y offset
- `Sprite.CurrentAnimation` state machine: playing, looped, completed events

## Async Synchronization Context
`// TODO: flush async sync context` comment in update loop.

- Custom `SynchronizationContext` that queues continuations
- Flush pending continuations during update loop (after collision, before CustomActivity)
- Allows `await` in entity/screen code to resume on the game thread

## Spatial Partitioning
**Design decision needed before building any dense entity-vs-entity collision sample.**

Current `AddCollisionRelationship` does O(n×m) broad-phase — fine for small lists, bottleneck in shmups or crowd games. Candidates: uniform grid, quadtree, or Apos.Shapes partitioning. Decision should be transparent to game code (no API change to `AddCollisionRelationship`).

## Polygon — Concave Collision
**Priority: Low / Deferred** — SAT only works for convex polygons. Concave polygons produce incorrect results.

- At `FromPoints`/`AddPoint` time, decompose into convex sub-polygons (e.g., Hertel-Mehlhorn on top of existing ear-clip triangulation)
- `CollisionDispatcher` would need a `PolygonVsX` path that iterates sub-polygons
- Consider exposing `Polygon.ConvexParts` for debugging

## Tiled Integration — TMX → TileShapeCollection
**Blocked** — waiting on MonoGame.Extended supporting the target MonoGame version.

- `TiledMapLayerRenderable.Draw` renders a tile layer
- `TiledCollisionGenerator.Generate` reads tile properties and calls `TileShapeCollection.AddTileAtCell`
- When this lands, `CameraControllingEntity.Map` should become a `MapBounds` struct/interface instead of `AxisAlignedRectangle`

## Multi-Backend Support (MonoGame / FNA / KNI) and Native AOT
**Priority: Eventual** — currently targets MonoGame.Framework.DesktopGL only.

- Identify abstraction points for graphics init, fullscreen APIs, input, audio, content pipeline
- AOT blockers: reflection-based code (`Activator.CreateInstance`, `MakeGenericMethod`, etc.) must be replaced
- Flag any new reflection-heavy or AOT-hostile code for future cleanup

## .NET 10 Upgrade
- Update `<TargetFramework>` in `src/FlatRedBall2.csproj` and `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj`
- Verify NuGet dependencies (MonoGame, Gum, Apos.Shapes) have compatible builds
- Run full test suite and sample builds after upgrade
