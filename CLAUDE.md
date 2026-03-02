# Repository Guidelines

## What Is This?

FlatRedBall2 is a 2D game engine/framework written in C# on .NET, built on top of MonoGame. It integrates Gum (UI) and Tiled (level editing) as dependencies. The project is currently in the architecture/design phase — see `ARCHITECTURE.md` for the full design.

## Key Files

- Main project: `src/FlatRedBall2.csproj` (MonoGame.Framework.DesktopGL 3.8.*)
- Architecture spec: `design/ARCHITECTURE.md`
- Code style: `.claude/code-style.md`
- Deferred items: `design/TODOS.md`
- Test project: `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj`

## Build & Test

```
dotnet build src/FlatRedBall2.csproj
dotnet test tests/FlatRedBall2.Tests/
```

## Engine Structure

```
src/
  Math/Angle.cs
  FrameTime.cs, IAttachable.cs, Entity.cs, Screen.cs, Factory.cs
  TimeManager.cs, ContentManagerService.cs, FlatRedBallService.cs
  Rendering/    IRenderable.cs, IRenderBatch.cs, Layer.cs, Camera.cs, Sprite.cs
  Rendering/Batches/   WorldSpaceBatch.cs, ScreenSpaceBatch.cs
  Collision/    ICollidable.cs, AxisAlignedRectangle.cs, Circle.cs, Polygon.cs
               ShapeCollection.cs, CollisionDispatcher.cs, CollisionRelationship.cs
  Input/        (15 files: interfaces + implementations)
  Audio/        AudioManager.cs (stub — throws NotImplementedException)
  Diagnostics/  BatchBreakInfo.cs, RenderDiagnostics.cs, DebugRenderer.cs (stub)
  UI/           GumRenderBatch.cs, GumRenderable.cs (implemented)
  Tiled/        TiledMapLayerRenderable.cs, TiledCollisionGenerator.cs (stubs)
  Game1.cs      (wired to FlatRedBallService.Default)
tests/FlatRedBall2.Tests/
  BounceTests.cs
```

## Available Skills

Invoke these with the Skill tool when working on specific topics:
- `entities-and-factories` — Entity lifecycle, AddChild, Factory<T>, spawning
- `collision-relationships` — AddCollisionRelationship, move/bounce semantics
- `physics-and-movement` — Y+ up, gravity, Drag, GameRandom
- `shapes` — AxisAlignedRectangle, Circle, Polygon, visual properties
- `input-system` — Keyboard, gamepad, input binding
- `camera` — Camera setup and transforms
- `screens` — Screen lifecycle and transitions
- `gum-integration` — UI with Gum
- `content-and-assets` — Asset loading
- `levels` — Level data layout and progression

## Key Architecture Decisions

- **Physics**: Second-order kinematic — `pos += vel*dt + acc*(dt²/2)`, `vel += acc*dt`, `vel -= vel*drag*dt`
- **Y-axis**: World space Y+ up; Camera transform flips Y for screen-space rendering
- **No static state**: Only `FlatRedBallService.Default` is static
- **Entity.Engine**: `internal set` — injected by Factory or Screen.Register before `CustomInitialize`; throws `InvalidOperationException` if accessed before injection
- **InternalsVisibleTo**: `FlatRedBall2.Tests` accesses internal members (PhysicsUpdate, AddEntity, etc.)
- **CollisionDispatcher**: `internal static` class in `FlatRedBall2.Collision` namespace

## Known Stubs (Not Yet Implemented)

These APIs exist but are not functional — do not attempt to use them:
- Audio: All methods throw `NotImplementedException`
- ACHX animations: `Sprite.PlayAnimation` is a no-op
- DebugRenderer: All draw methods are no-ops
- Tiled integration: `TiledMapLayerRenderable`/`TiledCollisionGenerator` are stubs
- `GamepadPressableInput.WasJustPressed/Released`: always returns false

## AI-Usability Goals

This project serves dual purposes: building a game engine AND evaluating how well AI assistants can work with it. **Game samples are not just games — they are AI usability tests for FlatRedBall2.**

Three layers of AI-usability (in priority order):

1. **API design** — Is the API clear, intuitive, and hard to misuse?
2. **XML documentation** — Is it succinct, adds clarification beyond the name, avoids redundancy, and calls out gotchas?
3. **Skill files** — Do they guide to the right location, explain high-level concepts, and flag gotchas?

### Post-Task Reflection (Required for Game Dev Tasks)

After completing any game development task, reflect and suggest concrete improvements:
- Did completing this task require excessive context or guesswork?
- Would a cleaner API design have prevented confusion?
- Are there missing, unclear, or redundant XML doc comments?
- Should a skill file be created or updated?

Make suggestions even if minor. **High churn on docs and skills is expected and desired — we want it perfect.**

### Keeping Docs and Skills Accurate (Critical)

Because churn is high, XML docs and skill files can easily become out-of-date. **If you ever encounter anything inaccurate or outdated in XML docs or skill files while working on any task, flag it immediately and fix it.** Stale guidance is worse than no guidance — it actively misleads future AI sessions.

### Responding to Friction Points

When you hit friction working on any task, respond at the appropriate scope:

1. **Skill files** — Fix immediately. No need to ask.
2. **XML doc comments** — Fix immediately. No need to ask.
3. **API design issues** — Flag and suggest; don't unilaterally change the API.

## Agent Workflow

For every task, invoke the appropriate agent from `.claude/agents/` before proceeding. The agent's instructions provide guidelines for how the task should be performed. Before doing any work, announce which agent you are using such as "Invoking coder agent for this task..."

Available agents:
- **game-designer** — **Invoke first** whenever the user wants to make a game (e.g., "I want to make a game like X", "let's build a platformer"). Leads a feel-first design conversation and produces a Game Design Document before any code is written.
- **coder** — Writing or modifying code and unit tests for new features or bugs
- **qa** — Reviewing production code for correctness, edge cases, and regressions (does not write tests); also assists with manual testing and playtest checklists
- **refactoring-specialist** — Refactoring and improving code structure
- **docs-writer** — Writing or updating documentation
- **product-manager** — Breaking down tasks and tracking progress
- **security-auditor** — Security reviews and vulnerability assessments

Select the agent that best matches the task at hand. For tasks that span multiple concerns (e.g., implement a feature and write tests), invoke the relevant agents in sequence.

**Game creation rule**: If the user says they want to make a game — even if they give a reference title or genre — always invoke the **game-designer** agent before the product-manager or coder. Do not skip straight to technical planning.

## Code Style

See `.claude/code-style.md` for all code style rules. Read that file before writing or editing any code.
