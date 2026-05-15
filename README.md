# FlatRedBall2

[![NuGet](https://img.shields.io/nuget/vpre/FlatRedBall2.MonoGame?label=NuGet)](https://www.nuget.org/packages/FlatRedBall2.MonoGame)

> **Early Preview** — This engine is in active development. APIs will change between releases.

FlatRedBall2 is the next generation of [FlatRedBall](https://github.com/vchelaru/FlatRedBall)  — a 2D game engine with 20+ years of iteration behind it, rebuilt from the ground up on modern .NET. It runs on two backends: [MonoGame](https://monogame.net) for desktop and [KNI](https://github.com/kniEngine/kni) for browser (via Blazor WASM), sharing a single codebase.

## Samples

Each sample is a complete runnable game built on the engine — open the source to see real usage patterns.

| Sample | Description | Play |
|--------|-------------|------|
| [ShmupSpace](samples/ShmupSpace/) | Shoot-em-up | [▶ Play in browser](https://vchelaru.github.io/FlatRedBall2/ShmupSpace/) |
| [PlatformKing](samples/PlatformKing/) | Platformer | [▶ Play in browser](https://vchelaru.github.io/FlatRedBall2/PlatformKing/) |
| [Solitaire](samples/Solitaire/) | Klondike solitaire | [▶ Play in browser](https://vchelaru.github.io/FlatRedBall2/Solitaire/) |

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

## Prerequisites

FlatRedBall2 requires the **.NET 10 SDK**. Before running any `dotnet` command below, verify it is installed and on your PATH:

```
dotnet --version
```

You should see a version starting with `10.` (e.g. `10.0.100`). If you instead see:

- `'dotnet' is not recognized as the name of a cmdlet...` (PowerShell) or `dotnet: command not found` (bash) — the SDK is not installed, or its install directory is not on your PATH.
- A version older than `10.` — you have an older SDK; install .NET 10 alongside it (side-by-side installs are supported).

### Installing .NET 10

Pick whichever method fits your platform:

**Windows** — either run the installer from https://dotnet.microsoft.com/download/dotnet/10.0, or use winget:

```
winget install Microsoft.DotNet.SDK.10
```

**macOS** — installer from the same download page, or via Homebrew:

```
brew install --cask dotnet-sdk
```

**Linux** — use Microsoft's install script (works on any distro):

```
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
```

Or follow the distro-specific package instructions at https://learn.microsoft.com/dotnet/core/install/linux.

> **Important — restart your terminal after installing.** Every install method above (winget, Homebrew, the GUI installers, the Linux script) adds `dotnet` to PATH, but existing terminal sessions cache the old PATH and will not see it. Close the terminal and open a fresh one before continuing. This includes the integrated terminal in VS Code / Visual Studio / Rider — restart the editor too.

After restarting, run `dotnet --version` to confirm you see `10.x`.

## Quick Start

Install the project template once (re-run before each new project to pick up template updates):

```
dotnet new install FlatRedBall2.Templates
```

Scaffold a new game. Pick a name (we'll use `YourGameName` below) and run from any directory you want the project folder created in:

```
dotnet new frb2-desktop -n YourGameName
```

> **Targeting the browser too?** Use `frb2-multiplatform` instead of `frb2-desktop`. It adds a `YourGameName.BlazorGL/` head (Blazor WebAssembly via KNI) alongside the desktop one, sharing the same `Common` project. Slightly more complex layout — only worth it if you actually plan to ship to the web. See [Multi-platform setup](#multi-platform-desktop--web) below for what changes.

By default the new project's `Content/` folder is pre-populated with starter assets (animation chains, a base `.tmx` map and `StandardTileset`, and platformer/topdown JSON configs) so an AI assistant can reference them immediately. To start with an empty `Content/` folder instead:

```
dotnet new frb2-desktop -n YourGameName --IncludeStarterContent false
```

This creates a `YourGameName/` folder containing two C# projects:

- `YourGameName.Common/` — your game code (screens, entities), shared across all targets.
- `YourGameName.Desktop/` — the desktop entry point that references `Common` and configures MonoGame.

Build and run:

```
cd YourGameName/YourGameName.Desktop
dotnet tool restore
dotnet run
```

> **Why `cd YourGameName.Desktop`?** `dotnet tool restore` installs the MGCB content pipeline tool, and the tool manifest (`.config/dotnet-tools.json`) only lives in that subdirectory. Running `dotnet tool restore` from the solution root silently does nothing.

A window should open showing the text "Hello from FlatRedBall 2" centered on a black background. If you see that, everything works.

### Next steps

- Open `YourGameName.Common/Screens/GameScreen.cs` — this is where your game code goes. `CustomInitialize` runs once when the screen starts (it's where the placeholder label is created — delete that block once you start building your own game); `CustomActivity` runs every frame.
- For complete examples of real games, browse the [`samples/`](samples/) directory of **this repository** (not your project). Each sample is a runnable project demonstrating different engine features.
- For task-specific guidance (entities, collision, animation, etc.), see [`frb-skills/`](frb-skills/) — Markdown guides written for AI assistants but readable by humans too.

### Multi-platform (Desktop + Web)

If you also want to ship to browsers, use `frb2-multiplatform` instead of `frb2-desktop`:

```
dotnet new frb2-multiplatform -n YourGameName
```

This produces three projects sharing one `Common`:

- `YourGameName.Common/` — game code, multi-targets `net8.0` (KNI/web) and `net10.0` (MonoGame/desktop).
- `YourGameName.Desktop/` — desktop entry point (MonoGame, `net10.0`).
- `YourGameName.BlazorGL/` — Blazor WebAssembly entry point (KNI, `net8.0`). Contains its own `App.razor`, `Pages/Index.razor`, `wwwroot/frb-host.js`, etc. — edit them freely; they're yours.

Run the desktop head as before. To run the web head:

```
cd YourGameName/YourGameName.BlazorGL
dotnet run
```

This launches a local dev server (default `https://localhost:5001`) — open the URL and the game runs in the browser canvas. Apos.Shapes' precompiled shader XNB ships in `wwwroot/Content/` so neither Wine nor a shader compiler is needed on macOS/Linux.

`Game1.cs` uses `#if KNI` to pick `GraphicsProfile.FL10_0` on the web target (`GraphicsProfile.HiDef` on desktop). Keep that pattern if you add any code that diverges between backends. Save data and other `System.IO.File`-based code should be gated behind `#if !KNI` since browsers have no filesystem.

### Manual setup (reference)

The template above is the supported install path. If you have a reason to wire FlatRedBall2 into an existing project (e.g. you're integrating with a MonoGame project you already have), here's the minimum API surface — but note this snippet alone is **not** a complete working setup. You'll also need a configured MonoGame project (Content pipeline / MGCB tool, `Content/` folder, etc.) which the template handles for you.

Inside an existing .NET project directory (one that already contains a `.csproj`):

1. Install the NuGet package:

   ```
   dotnet add package FlatRedBall2.MonoGame   # desktop (.NET 10)
   # or
   dotnet add package FlatRedBall2.Kni        # browser / Blazor WASM (.NET 8)
   ```

   > Running `dotnet add package` outside a project folder fails with `Could not find any project in <directory>` — it needs a `.csproj` in the working directory.

2. Set up `Game1.cs`:

```csharp
using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        FlatRedBallService.Default.PrepareWindow<GameScreen>(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBallService.Default.Initialize(this);
        FlatRedBallService.Default.Start<GameScreen>();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        FlatRedBallService.Default.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlatRedBallService.Default.Draw();
        base.Draw(gameTime);
    }
}
```

3. Create a `GameScreen` class:

```csharp
using FlatRedBall2;

public class GameScreen : Screen
{
    public override void CustomInitialize()
    {
        // set up your entities and shapes here
    }

    public override void CustomActivity(FrameTime time)
    {
        // called every frame; put game logic here
    }

    public override void CustomDestroy()
    {
        // called when the screen is removed
    }
}
```

See the `samples/` directory for complete working examples.

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
