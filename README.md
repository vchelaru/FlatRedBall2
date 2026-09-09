# FlatRedBall2


[![NuGet](https://img.shields.io/nuget/vpre/FlatRedBall2.MonoGame?label=NuGet)](https://www.nuget.org/packages/FlatRedBall2.MonoGame)

[![NuGet](https://img.shields.io/nuget/vpre/FlatRedBall.AnimationChain.MonoGame?label=AnimationEditor%20MonoGame)](https://www.nuget.org/packages/FlatRedBall.AnimationChain.MonoGame)

[![NuGet](https://img.shields.io/nuget/vpre/FlatRedBall.AnimationChain.KNI?label=AnimationEditor%20KNI)](https://www.nuget.org/packages/FlatRedBall.AnimationChain.KNI)

[![Join the chat](https://img.shields.io/discord/586997072373481494)](https://discord.gg/tG5RBgw)

> **Early Preview** — This engine is in active development. APIs will change between releases.

FlatRedBall2 is the next generation of [FlatRedBall](https://github.com/vchelaru/FlatRedBall)  — a 2D game engine with 20+ years of iteration behind it, rebuilt from the ground up on modern .NET. It runs on two backends: [MonoGame](https://monogame.net) for desktop and [KNI](https://github.com/kniEngine/kni) for browser (via Blazor WASM), sharing a single codebase.

## Samples

Each sample is a complete runnable game built on the engine — open the source to see real usage patterns.

| Sample | Description | Play |
|--------|-------------|------|
| [ShmupSpace](samples/ShmupSpace/) | Shoot-em-up | [▶ Play in browser](https://vchelaru.github.io/FlatRedBall2/ShmupSpace/) |
| [PlatformKing](samples/PlatformKing/) | Platformer | [▶ Play in browser](https://vchelaru.github.io/FlatRedBall2/PlatformKing/) |
| [Solitaire](samples/Solitaire/) | Klondike solitaire | [▶ Play in browser](https://vchelaru.github.io/FlatRedBall2/Solitaire/) |

## Tools

**Animation Editor** — author and preview sprite animation chains (`.achx`). Self-contained downloads (no .NET install required):

| Platform | Download |
|---|---|
| Windows (x64) | [AnimationEditor-win-x64.zip](https://github.com/vchelaru/FlatRedBall2/releases/latest/download/AnimationEditor-win-x64.zip) |
| macOS (Apple Silicon) | [AnimationEditor-osx-arm64.zip](https://github.com/vchelaru/FlatRedBall2/releases/latest/download/AnimationEditor-osx-arm64.zip) |
| macOS (Intel) | [AnimationEditor-osx-x64.zip](https://github.com/vchelaru/FlatRedBall2/releases/latest/download/AnimationEditor-osx-x64.zip) |
| Linux (x64) | [AnimationEditor-linux-x64.tar.gz](https://github.com/vchelaru/FlatRedBall2/releases/latest/download/AnimationEditor-linux-x64.tar.gz) |
| Web (browser, no install) | [Try it online](https://vchelaru.github.io/FlatRedBall2/AnimationEditor/) |

The downloads above always resolve to the latest published release. The web version is deployed manually and may lag behind; older release downloads are on the [Releases page](https://github.com/vchelaru/FlatRedBall2/releases).

Binaries are unsigned. Windows SmartScreen will warn on first run ("More info" → "Run anyway"); macOS Gatekeeper will refuse to open directly — right-click the executable, choose Open, then confirm.

## Features

- **Screens & Entities** — structured game object model with lifecycle hooks (`CustomInitialize`, `CustomActivity`, `CustomDestroy`)
- **Collision relationships** — declarative move/bounce collision between entity groups; one call to wire up an entire system
- **Shapes & physics** — built-in `AARect`, `Circle`, and `Polygon` with kinematic physics
- **Platformer & top-down movement** — first-class built-in behaviors; no custom physics code required
- **Gum UI integration** — full [MonoGame Gum](https://github.com/vchelaru/Gum) support for menus, HUDs, and in-game UI
- **Input system** — keyboard, gamepad, and input interfaces for action binding
- **Camera** — configurable 2D camera with world/screen coordinate transforms
- **Async support** — async/await compatible throughout the game loop
- **Hot reload** — all content files reload at runtime without restarting
- **Extensive XML documentation** — every public API documented; IntelliSense covers everything
- **AI assistant support** — ships with skill files in `/frb-skills/` for any AI coding tool

## Quick Start

Requires the **.NET 10 SDK** ([install guide](https://docs.flatredball.com/flatredball2/setup#prerequisites)).

```
dotnet new install FlatRedBall2.Templates
dotnet new frb2-desktop -n YourGameName
cd YourGameName/YourGameName.Desktop
dotnet tool restore
dotnet run
```

A window opens showing "Hello from FlatRedBall 2" — if you see that, everything works.

See the **[full setup guide](https://docs.flatredball.com/flatredball2/setup)** for prerequisites, multi-platform (desktop + web) projects, manually wiring FlatRedBall2 into an existing project, and troubleshooting.

## Working with AI Assistants

FlatRedBall2 ships with skill files in [`/frb-skills/`](frb-skills/) — plain Markdown guides covering common engine tasks (entities, collision, physics, animation, audio, and more). Copy them into your game repo so your AI coding assistant has engine context without you pasting anything manually.

Add the skill files to your project. Run these from your project's root folder (e.g. `YourGameName/`):

```
dotnet new install FlatRedBall2.Templates   # skip if already installed
dotnet new frb2-skills
```

This creates a `frb-skills/` folder in the current directory. Most AI tools can be pointed at that folder or configured to load files from it automatically.

**Claude Code** — copy the skills into `.claude/skills/` so they are picked up automatically. Run from the same project root:

```
# macOS / Linux
mkdir -p .claude/skills && cp -r frb-skills/. .claude/skills/

# Windows (PowerShell or cmd)
xcopy /E /I frb-skills .claude\skills
```

You can keep `frb-skills/` as the source of truth and gitignore `.claude/skills/`, or drop `frb-skills/` and commit `.claude/skills/` directly — either works.

**Other AI tools** — paste the relevant file from `frb-skills/` into your context window before starting a task. Each file is self-contained.

## FlatRedBall vs FlatRedBall2

FlatRedBall (FRB1) has been in active use since the early 2000s. FlatRedBall2 is a clean-slate rewrite that keeps the things that worked — the screen/entity model, collision relationships, shape-based physics — while fixing the things that didn't.

The biggest workflow change: FRB1 centered on Glue, a Windows-only visual editor that generated code and managed assets. FRB2 drops the editor entirely — everything is code. The API has been unified from scratch rather than grown organically, which eliminates a lot of the inconsistencies that accumulated in FRB1 over two decades. Third-party libraries (Gum, Tiled) use their standard MonoGame versions rather than FRB1's modified forks, so ecosystem updates flow in automatically.

FRB2 does not have a migration path from FRB1 projects. It is a fresh start with familiar concepts.

## Contributing

Contributions welcome. Before submitting a PR:

- Run `dotnet test tests/FlatRedBall2.Tests/` — all tests must pass
- Engine behavior changes require a failing test first (see `.claude/skills/engine-tdd`)
- Code style rules are in `.claude/code-style.md`

## License

[MIT](LICENSE)
