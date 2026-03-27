@design/TODOS.md

# Repository Guidelines

## What Is This?

FlatRedBall2 is a 2D game engine/framework written in C# on .NET, built on top of MonoGame. It integrates Gum (UI) and Tiled (level editing) as dependencies.

## Key Files

- Main project: `src/FlatRedBall2.csproj` (MonoGame.Framework.DesktopGL 3.8.*)
- Code style: `.claude/code-style.md`
- Deferred items: `design/TODOS.md`
- Test project: `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj`

## Build & Test

```
dotnet build src/FlatRedBall2.csproj
dotnet test tests/FlatRedBall2.Tests/
```

## Available Skills

Invoke these with the Skill tool when working on specific topics:
- `entities-and-factories` — Entity lifecycle, Add (shapes/Gum), Factory<T>, spawning
- `collision-relationships` — AddCollisionRelationship, move/bounce semantics
- `physics-and-movement` — Y+ up, gravity, Drag, GameRandom
- `timing` — Cooldown gates, repeating timers, entity lifetimes, FrameTime.DeltaSeconds
- `shapes` — AxisAlignedRectangle, Circle, Polygon, visual properties
- `input-system` — Keyboard, gamepad, input binding
- `camera` — Camera setup and transforms
- `screens` — Screen lifecycle and transitions
- `gumcli` — **Ask first** before any Gum UI code: use gumcli tool or code-only? Covers gumcli new, .csproj content includes, codegen
- `gum-integration` — UI with Gum (runtime usage; use `gumcli` skill first if user chose Gum tool)
- `content-and-assets` — Asset loading
- `engine-overview` — **Start here.** What the engine does automatically, what game code must implement, what is stubbed, and critical gotchas
- `levels` — Level data layout and progression
- `tmx` — TMX map file creation/editing: base template, StandardTileset tile IDs, layer conventions, CSV data
- `top-down-movement` — Top-down movement with `TopDownBehavior`/`TopDownValues`, 4/8-way directions, speed multiplier
- `path-and-pathfollower` — `Path` (line/arc segments, rendering) and `PathFollower` (entity movement, FaceDirection, waypoint events)
- `tile-node-network` — A* pathfinding: `TileNodeNetwork`, `TileNode`, grid setup aligned with `TileShapeCollection`, enemy navigation pattern
- `animation` — Sprite animation: AnimationChain, AnimationChainList, .achx loading, PlayAnimation, looping/non-looping, AnimationFinished
- `audio` — AudioManager, loading SoundEffect/Song, music, volume
- `sample-project-setup` — How to create a new sample `.csproj` (dotnet-tools.json, mgcb, project structure)

## Key Architecture Decisions

- **Physics**: Second-order kinematic — `pos += vel*dt + acc*(dt²/2)`, `vel += acc*dt`, `vel -= vel*drag*dt`
- **Y-axis**: World space Y+ up; Camera transform flips Y for screen-space rendering
- **No static state**: Only `FlatRedBallService.Default` is static
- **Entity.Engine**: `internal set` — injected by Factory before `CustomInitialize`; throws `InvalidOperationException` if accessed before injection
- **InternalsVisibleTo**: `FlatRedBall2.Tests` accesses internal members (PhysicsUpdate, AddEntity, etc.)
- **CollisionDispatcher**: `internal static` class — shape-pair resolution uses concrete type matching

## Known Stubs (Not Yet Implemented)

Do not attempt to use these — they exist as API placeholders:
- DebugRenderer: All draw methods are no-ops
- Tiled integration: `TileMapCollisionGenerator` uses MonoGame.Extended 6.0 preview

## AI-Usability Goals

This project serves dual purposes: building a game engine AND evaluating how well AI assistants can work with it. **Game samples are not just games — they are AI usability tests for FlatRedBall2.**

Three layers of AI-usability (in priority order):

1. **API design** — Is the API clear, intuitive, and hard to misuse?
2. **XML documentation** — Is it succinct, adds clarification beyond the name, avoids redundancy, and calls out gotchas?
3. **Skill files** — Do they guide to the right location, explain high-level concepts, and flag gotchas?

### Post-Task Reflection (Required for Game Dev Tasks)

After completing a task where you are **using the engine as an end user** (building a game, writing a sample, implementing a game mechanic), reflect and suggest concrete improvements:
- Did completing this task require excessive context or guesswork?
- Would a cleaner API design have prevented confusion?
- Are there missing, unclear, or redundant XML doc comments?
- Should a skill file be created or updated?

**Do not give this reflection when working on the engine itself** (fixing engine bugs, implementing engine features, writing engine tests). Those tasks are about the internals, not about the end-user experience of the API.

Make suggestions even if minor. **High churn on docs and skills is expected and desired — we want it perfect.**

### Keeping Docs and Skills Accurate (Critical)

Because churn is high, XML docs and skill files can easily become out-of-date. **If you ever encounter anything inaccurate or outdated in XML docs or skill files while working on any task, flag it immediately and fix it.** Stale guidance is worse than no guidance — it actively misleads future AI sessions.

### Responding to Friction Points

When you hit friction working on any task, respond at the appropriate scope:

1. **Skill files** — Fix immediately. No need to ask.
2. **XML doc comments** — Fix immediately. No need to ask.
3. **API design issues** — Flag and suggest; don't unilaterally change the API.

### Skill File Quality Bar

Skill files are loaded into a limited context window — every line costs budget. Keep them lean and generalizable.

**Include:**
- Engine behaviors that are non-obvious or contradict intuition (gotchas, footguns)
- API patterns that apply across many game types
- Correct order-of-operations for lifecycle hooks

**Do not include:**
- Game-specific logic (score systems, enemy AI, wave spawning, upgrade trees) — the agent should implement these without guidance
- Anything that is obvious from the method name or standard C# patterns
- Step-by-step walkthroughs for common programming tasks

**Calibration:** A reasonable amount of implementation work is expected from the agent. If the friction was "I had to write a state machine" or "I had to calculate screen bounds arithmetic" — that is normal work, not a gap in documentation. Only document things the engine makes unexpectedly hard or behaves unexpectedly.

## Agent Workflow

**Step 0 — Scope the task first**: Before invoking any agent or reading skill files, determine what kind of task this is:
- **Game creation** → game-designer agent (then product-manager, then coder)
- **Engine feature or bug** → identify which subsystem (collision, rendering, input, etc.) to know which skill files are relevant
- **Docs or refactor** → docs-writer or refactoring-specialist agent

This scoping step keeps context lean — only load the skills and files that are actually needed.

**Step 0b — Load all relevant skills before touching any source files.** Decompose the task into every concern it touches, and load a skill for each one. A task that involves creating an entity, giving it a shape, and setting up collision requires `entities-and-factories` + `shapes` + `collision-relationships` — all three, up front. Skills are cheap to load and save enormous amounts of time; reading source to compensate for a missing skill is always the wrong trade. If in doubt, load the skill.

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
