---
name: content-boundary
description: "Content boundary philosophy for FlatRedBall2. Defines what AI produces vs what the human produces (content, feel, placement). Trigger before adding a new level, UI screen, sprite, platformer entity, or any asset the engine loads at runtime — and when designing engine APIs that expose tunable values."
---

# The AI / Human Content Boundary

FlatRedBall2 assumes a **soft split of labor** between AI and human. The split exists because AI has hard limits on a few things, and hiding those limits behind "AI does everything" produces worse games than embracing the split.

## What AI Produces

- **Code and structure** — entities, screens, factories, collision wiring, state machines, input handling.
- **Placeholders and scaffolding** — valid-but-minimal TMX files, flat Gum screens, default coefficients, shape-based "programmer art" in place of sprites.
- **Logic and integration** — loading assets by known path, wiring coefficients from JSON, responding to collision events.

## What the Human Produces

- **Raster art** — PNG sprites, backgrounds, UI art. AI cannot create these.
- **Level design and placement** — where platforms go, where enemies spawn, pacing, difficulty curve. AI cannot *see* a rendered level or *play* it to judge flow.
- **UI composition** — where controls sit on screen, visual hierarchy, typography. AI cannot see the rendered result.
- **Feel tuning** — jump height, run speed, friction, drag, attack timing. AI cannot feel gameplay.

AI and human can both edit code when needed, but the asymmetry is real: AI writing code is fast and reliable; AI composing art or tuning feel is slow and unreliable. Design around that.

## Engine Design Implication — Externalize What the Human Tunes

When designing or reviewing an engine API, ask: *will a human want to tune this without recompiling?*

- **Yes** → the API must accept externalized data (JSON, TMX, .gumx, .achx). Example: `PlatformerValues` are consumed from JSON at runtime so designers can iterate in a text editor.
- **No** → code-only is fine.

This is the lens behind decisions like JSON-driven platformer coefficients, TMX-driven level geometry, and `.gumx`-driven UI layouts. Avoid hardcoding anything a designer would reasonably want to tune by hand.

## Operational Rule — Always Scaffold the Placeholder

When a game task adds a new piece of content, AI **must create a placeholder file** rather than hardcoding the content in C#. After scaffolding, tell the user which file to open in which tool.

| Adding... | Scaffold | Template source | Human opens in... |
|-----------|----------|-----------------|--------------------|
| A new level | Minimal TMX with collision layer, one spawn marker (see `tmx` skill) | `.claude/templates/Tiled/base.tmx` | Tiled |
| A new UI screen | Gum screen with named controls in a flat list (see `gum-integration` / `gumcli` skills) | — | Gum Tool |
| A new platformer entity | `player.platformer.json` with movement coefficients (see `platformer-movement` skill) | `.claude/templates/PlatformerConfig/player.platformer.json` | Text editor (JSON) |
| A new animated entity | `.achx` referencing a placeholder spritesheet path | `.claude/templates/AnimationChains/` | Aseprite / FRB animation editor |
| A new sprite-bearing entity | Code expects `EntityName.png` at a documented size/path | — | Any image editor |

Templates live in `.claude/templates/` — copy from there into the project's `Content/` folder, then adjust values. Add the appropriate `<Content Include="Content/*.json" CopyToOutputDirectory="PreserveNewest" />` to the `.csproj` for JSON-based content.

The scaffold must be *valid and runnable* — the game should build and play immediately, using shape-based stand-ins for missing art. The human then iterates on content without the AI being in the loop.

## Scope of the Placeholder — Keep It Tiny

The placeholder exists to exercise the mechanics, nothing more. The human will redesign the content anyway, so effort spent on "polished" or "sized-to-spec" content is wasted.

**Hard rules for AI-authored content:**

- **TMX levels:** copy the template, add a floor row, 1–3 platforms, a player spawn, and 1–3 enemy spawns. That's it. One screen wide is fine. Do not attempt multi-screen levels, detailed layouts, or decorative tiles — the human will widen and redesign in Tiled.
- **Gum screens:** a flat list of named controls. No polish, no alignment tuning, no theming — the human opens it in the Gum tool.
- **PlatformerConfig JSON:** copy the template, change nothing unless the mechanic flat-out doesn't work without a change. The human tunes feel.
- **AnimationChains:** use the template chain as-is where one fits. Don't invent new chains.

**If the user's spec asks for content size or polish** ("3 screens wide," "a detailed forest level," "a menu with these 8 buttons arranged like X"), the AI scaffolds the **minimum mechanics-exercising placeholder** and tells the user to expand it in the appropriate tool. The gameplay code supports any level size / any number of buttons — the placeholder does not need to.

**If you feel the urge to write a generator script** (Python, shell, C# one-shot tool, ASCII-to-TMX converter, anything that produces content from code), you've already gone too far. Stop, scaffold smaller, and hand it off. There is no scenario in which a generator script for content is the right answer in this codebase.

## Handoff Communication

After scaffolding, close the loop with the user explicitly. A good handoff looks like:

> I added `Content/Tiled/Level2.tmx` with a collision layer and a player-spawn tile. Open it in Tiled to lay out the level. I also added `Entities/Boss.cs` expecting `Content/Boss.png` (64×64) — drop that PNG in when you have art.

Do not bury this in a summary. The user needs to know exactly which files to open and which tools to use, because that is the half of the work AI can't do.

## Anti-Patterns

- **Hardcoding level geometry in C#** instead of a TMX — the human now has to edit code to move a platform.
- **Hardcoding `PlatformerValues` in C#** (`new PlatformerValues { MaxSpeedX = 150f, ... }`) instead of a `player.platformer.json` — every tuning pass is a recompile. Use `PlatformerConfig.FromJson(...).ApplyTo(behavior)` instead.
- **Generating sprites procedurally "to avoid needing art"** — it is almost always better to use a shape placeholder and have the human drop real art in later.
- **Silently skipping the handoff** — finishing a task without telling the user which files they need to touch.

## When the Rule Bends

- **One-off prototypes** where the human explicitly says "just hardcode it, I'm throwing this away" — fine, skip the scaffold.
- **Values that are truly engine-internal** (collision epsilon, physics integration constants) — these are not "designer tunables"; hardcode them.
- **Tiny UI** (a single debug label) — a Gum project file is overkill; inline is fine. Graduate to a project file once there's a second control.

If in doubt, scaffold. The cost of an extra file is trivial; the cost of unscaffolded content is the human editing code to tune a jump.
