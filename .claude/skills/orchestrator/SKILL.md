---
name: orchestrator
description: "Minigame orchestrator for FlatRedBall2. Designs a small retro game (user-provided or random), delegates implementation to a coder sub-agent, builds it, and reports friction. Use when the user says 'make a random game', 'use the orchestrator', or similar."
---

# Minigame Orchestrator

> **See `content-boundary` skill first.** Orchestrated games must scaffold placeholder content files (TMX, Gum screens, coefficients JSON) and close with an explicit handoff telling the user which files to open in which tools. Do not hardcode levels, UI composition, or tunable values in C#.

Test the FlatRedBall2 engine's AI-usability by designing and implementing a small retro game, then reporting friction.

**Critical constraint:** Do NOT read existing samples (`samples/`), unit tests (`tests/`), or any game code outside `src/` and the current project being built. The only resources available are: engine source code (`src/`), XML docs, skill files (`.claude/skills/`), and templates (`.claude/templates/`). This applies to you AND to the coder agent you delegate to. The whole point of this evaluation is to test whether the engine's docs and skills are sufficient — looking at other samples defeats the purpose.

# Pipeline

Execute these steps in order. Do not skip any step.

## Step 1: Pick a Game

Determine which starting condition applies:

**A. User provided a game design** — Accept it as-is and skip to Step 3. Do not re-design or second-guess it.

**B. User specified a game name/title** — Use that game and proceed to Step 2.

**C. No input** — Choose a random game from the Atari 2600 or NES era. Aim for variety — avoid games that are structurally identical to common genres (e.g., don't always pick platformers). Examples: Frogger, Asteroids, Breakout, Galaga, Pac-Man, Donkey Kong, Dig Dug, Missile Command, Centipede, Joust, Balloon Fight, Ice Climber, Excitebike, Burger Time, Q*bert, Spy Hunter, River Raid, Pitfall, Moon Patrol.

Do NOT pick a game that already has a sample in `samples/`. Check first. (If the user explicitly requested a re-do of an existing game, delete the old project directory first.)

## Complexity Levels

Default is **Complexity 2** unless the user requests otherwise.

### Complexity 1 — Micro Scope
- 1-2 core mechanics
- Single screen (gameplay only)
- 1-2 entity types
- No audio
- Basic HUD at most (a label or two)

### Complexity 2 — Standard Scope
- 3-4 core mechanics **if the game benefits from it** — don't force extra mechanics that don't serve the design
- Multiple screens — at minimum a title screen and a gameplay screen (tests screen transitions)
- 2-3 distinct enemy/NPC types with different behaviors, **if it makes sense for the game** — a puzzle game doesn't need enemy variety
- Level progression — multiple TMX maps or increasing difficulty waves (tests level transitions)
- Meaningful HUD — health bar, ammo count, wave indicator, or similar (pushes Gum beyond a single label)
- No audio

## Step 2: Design

Skip this step if the user provided a game design (Step 1A).

Write a brief design scoped to the current complexity level:

- **One sentence** defining the game concept
- **3-4 sentences** describing the core mechanics to implement

For **Complexity 1**: pick only 1-2 core mechanics. Strip away everything that isn't the core loop.

For **Complexity 2**: include the additional scope (multiple screens, level progression, HUD, entity variety) but only where it genuinely improves the game. Don't bolt on mechanics or enemy types just to hit a number.

No music, no sound at either complexity level. Polish the mechanics, not the presentation.

**Present the brief design to the user and wait for approval before proceeding.** If the user wants changes, revise and re-present. Only continue to Step 3 after the user confirms.

## Step 3: Expand into a Lightweight GDD

If the user provided a full design (Step 1A), adapt it into the GDD format below and save it. Fill in any missing sections yourself — do not ask the user. If the user's design is already comprehensive, preserve its content and just restructure into this format.

Otherwise, expand the brief design from Step 2 into a lightweight Game Design Document. Make all creative decisions yourself — do not ask the user.

Save the GDD to `samples/auto/<ProjectName>/design.md` (same directory as the game project).

The GDD should follow this structure (keep it concise — this is micro scope):

```markdown
# <Game Name> — Game Design Document

## One-Sentence Pitch
<from step 2>

## Core Mechanics
<from step 2, expanded with enough detail for implementation>

## Controls
<what keys/buttons do what>

## Win / Lose
<how does the player win or lose, if applicable>

## Scope Boundary
<what is explicitly OUT of scope for this micro evaluation>
```

## Step 4: Delegate to Coder Agent

**Do not implement the game yourself.** Use the Agent tool to spawn a `coder` sub-agent with the following prompt (fill in the blanks from your design):

```
Implement the game described in samples/auto/<ProjectName>/design.md as a new FlatRedBall2 sample project.

Project setup:
- Project directory: samples/auto/<ProjectName>/
- Project name: AutoEval<GameName>Sample
- The .csproj must reference the engine via ProjectReference:
  <ProjectReference Include="..\..\..\src\FlatRedBall2.csproj" />
- Create a .slnx solution file that includes both the sample and engine projects.
- Do NOT read existing samples, unit tests, or any code outside src/ and your own project. Simulate an end user with only the engine source, XML docs, and skill files.

Content mode decisions (do not ask — use these):
- Gum: Code-only mode for any UI (score, health bar, lives display, etc.)
- Tiled: Use .tmx files — copy the template from .claude/templates/Tiled/
- Animations: Use .achx files — copy the template from .claude/templates/AnimationChains/. Use template animations where an appropriate chain exists (e.g., character walk/jump, coin, enemy). For entities with no matching animation in the template, shapes are fine.

**Scaffolding rule (non-negotiable):** Follow the `content-boundary` skill's one-of-each rule. Place exactly one tile of each collision class the code references and exactly one marker of each entity type the code spawns — no more. Do not author a designed level, do not procedurally generate tile CSV, do not call out to Python or shell scripts to produce content. If you catch yourself writing a loop to fill tile data, stop — you have crossed from scaffolding into authoring. The human opens the TMX in Tiled and designs the real level.
```

Wait for the coder agent to complete before proceeding.

## Step 5: Build

Run `dotnet build` on the new sample project.

Use this decision tree:
- If build failures are in the new sample project, fix them and rebuild until success.
- If build failures are outside the sample (for example, pre-existing `src/` errors unrelated to your generated project), do **not** repair unrelated engine code in this workflow.
- In that blocked case, run an isolated sample compile (`dotnet build <sample.csproj> -p:BuildProjectReferences=false`) to validate the delegated game code, and record that full build was externally blocked.

Record the path to the resulting `.exe` file. It will be at:
`samples/auto/<ProjectName>/bin/Debug/net10.0/<ProjectName>.exe`

Smoke-run guidance:
- Preferred: `dotnet run` from the sample project when full build is healthy.
- Fallback: if `dotnet run` is blocked only by unrelated upstream compile failures, launch the prebuilt executable directly and record this fallback in the result file.

## Step 5b: Self-Audit — Delegation Check

Before writing the result file, honestly answer these questions:

1. **Did you spawn a `coder` agent in Step 4?** (Yes/No)
2. **Did the `coder` agent write all the game code?** (Yes/No)
3. **Did you write or edit any game source files yourself** (outside of build fixes in Step 5)? (Yes/No — if Yes, list which files)

If the answer to #1 or #2 is No, or #3 is Yes, you violated the pipeline. This is a **critical process failure** — the entire point of the orchestrator is to test how the *coder agent* performs with the engine's skills and docs. If you implemented the game yourself, the friction feedback is about *your* experience, not the coder's, which defeats the purpose of the evaluation.

Record your answers — they go in the result file.

## Step 6: Write Result File

Create a result file at `samples/auto/eval-results/<game-name>.md` with this exact structure:

```markdown
# <Game Name>

## Game
<A paragraph describing what the game is and what mechanics were implemented>

## Run
`samples/auto/<ProjectName>/bin/Debug/net10.0/<ProjectName>.exe`

## How to Play
<1-2 sentences: what keys to press, what the goal is>

## Delegation Audit
- Spawned coder agent: <Yes/No>
- Coder agent wrote all game code: <Yes/No>
- Orchestrator edited game source files (outside build fixes): <Yes/No — list files if Yes>
- **Verdict:** <PASS or FAIL — FAIL if coder was not used or orchestrator wrote game code>

## Feedback
<Friction points from implementation — things that were confusing, required excessive context, had unclear APIs, missing/misleading docs, or missing skill coverage. If everything went smoothly, just write "No concerns.">

## Failure Attribution
- Build/Run attribution: <Sample issue | Engine issue | Process ambiguity>
- Evidence: <1 sentence naming the file path(s) or command symptom that supports the attribution>
```

# Feedback Guidelines

The feedback section is the most important output. Be honest and specific.

## Validation — Apply Before Reporting Each Item

Before writing any feedback item, run this checklist. If an item fails validation, **drop it**.

1. **Did you actually get stuck?** If you found the answer (via XML docs, source, or skill) and used it correctly, the system worked. "I wish it was more prominent" is not friction — discovering an API by reading the class you were already using is the intended workflow.
2. **Re-read the XML docs.** Open the source file for the type in question and check whether the XML doc comments already explain the thing you're about to report. If they do, drop the item.
3. **Is the method/property name self-explanatory?** If you found `Color` on a shape type and it does what `Color` obviously means, that's not a gap — that's discoverability working as intended.
4. **Is this about a single member on a class you were already reading?** XML docs handle individual property/method documentation. Skills handle cross-cutting workflows and routing. Don't report "skill doesn't mention property X" when X is documented on the class you were already working with.

## What IS Worth Reporting

- **Couldn't find the right class at all** — no skill routed you there, name wasn't guessable → `[Skill]` gap (routing)
- **Multi-class workflow was unclear** — needed to coordinate 3+ types and the sequence wasn't documented anywhere → `[Skill]` gap (workflow)
- **Silent failure / wrong behavior** — API accepted bad input without error, produced wrong results → `[API]` issue
- **XML docs were wrong or misleading** — doc said X, actual behavior was Y → `[Docs]` issue
- **Skill was inaccurate or contradicted another skill** — inconsistent guidance → `[Skill]` issue
- **Guardrail missing** — you went down a completely wrong path that a "use X, not Y" note would have prevented → `[Skill]` gap (guardrail)
- **Non-obvious member that you couldn't guess existed** — something like `RepositionDirections` or `Raycast` on a tile collection, where the name or concept isn't predictable from the class → `[Skill]` gap (worth a brief mention in the skill to save future agents from reading the file)

## What Belongs Where (Do Not Suggest the Wrong Fix)

| Content type | Where it belongs | Why |
|---|---|---|
| Routing — "for task X, use class Y" | Skill | Prevents searching the codebase |
| Cross-cutting workflows (3+ classes) | Skill | Prevents reading multiple files to piece together a sequence |
| Guardrails — "use X, not Y" | Skill | Prevents going down wrong paths entirely |
| Non-obvious members the agent wouldn't guess exist | Skill (brief mention) | Saves a file read when the agent only needs that one thing |
| What a single property/method does | XML docs only | Agent sees it when reading the class — zero additional cost |
| Parameters, defaults, return values | XML docs only | Same reason |
| Obvious members on a class the agent is already reading | Neither — already discoverable | `Color` on a shape class needs no extra documentation |

## Missing XML Docs

If you encounter a public member with **no XML doc comment at all** (or one that is misleading/incomplete), report it as `[Docs]` with high confidence. This is distinct from "the skill doesn't mention it" — missing XML docs means the primary documentation layer has a gap. Include the type name, member name, and what the doc should say.

## Confidence Level

Every feedback item must include a confidence rating from 1 to 3:

- **1** — Low. Might be friction, might be normal work. Worth reviewing but could be noise.
- **2** — Medium. Genuinely slowed me down, but reasonable people could disagree on the fix.
- **3** — High. Clear gap — wrong docs, silent failure, or completely missing guidance that caused a wrong path.

## Format

Each feedback item should be formatted as: `- [Category] (Confidence N) Description`

Example: `- [API] (Confidence 3) TileShapeCollection silently accepts tiles before GridSize is set — positions are wrong with no error.`

- **Be specific.** "The collision API was confusing" is useless. "I expected `AddCollisionRelationship` to return the relationship object for chaining, but it returns void" is useful.
- **Prefix each item** with `[API]`, `[Docs]`, or `[Skill]` so the reader knows where to act.
- **Normal work is not friction.** Writing a state machine, calculating positions, implementing game logic — that's expected. Only report things the engine made unexpectedly hard or where guidance was wrong/missing.
- **If no friction after validation:** just write "No concerns." Don't pad with praise.
