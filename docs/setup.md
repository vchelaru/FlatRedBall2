# Setup

This page covers everything needed to get a FlatRedBall2 project running: prerequisites, the
supported quick-start path, and a manual reference for wiring the engine into an existing
project.

## Prerequisites

FlatRedBall2 requires the **.NET 10 SDK**. Before running any `dotnet` command below, verify it is installed and on your PATH:

```
dotnet --version
```

You should see a version starting with `10.` (e.g. `10.0.100`). If you instead see:

- `'dotnet' is not recognized as the name of a cmdlet...` (PowerShell) or `dotnet: command not found` (bash) — the SDK is not installed, or its install directory is not on your PATH.
- A version older than `10.` — you have an older SDK; install .NET 10 alongside it (side-by-side installs are supported).

See [Installing .NET 10](#installing-net-10) below for platform-specific instructions.

### Installing .NET 10

FlatRedBall2 requires the **.NET 10 SDK**. If `dotnet --version` isn't found or shows an older version, install it:

**Windows** — installer from https://dotnet.microsoft.com/download/dotnet/10.0, or via winget:

```
winget install Microsoft.DotNet.SDK.10
```

**macOS** — same download page, or via Homebrew:

```
brew install --cask dotnet-sdk
```

**Linux** — Microsoft's install script (works on any distro):

```
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
```

Or follow distro-specific instructions at https://learn.microsoft.com/dotnet/core/install/linux.

> **Restart your terminal after installing** — installers add `dotnet` to PATH, but existing terminal sessions won't see it. Close and reopen your terminal (or restart the editor if using VS Code / Rider). Then run `dotnet --version` to confirm `10.x`.

## Quick Start

### Step 1 — Install the project template

Run this once (and again before any new project to pick up template updates):

```
dotnet new install FlatRedBall2.Templates
```

### Step 2 — Create your game

Pick a name and run from the directory where you want the project folder created:

```
dotnet new frb2-desktop -n YourGameName
```

This creates a `YourGameName/` folder with two projects inside:

- `YourGameName.Common/` — your game code (screens, entities), shared across all targets.
- `YourGameName.Desktop/` — the desktop entry point that references `Common` and configures MonoGame.

> **Targeting the browser too?** Use `frb2-multiplatform` instead of `frb2-desktop`. See [Multi-platform (Desktop + Web)](#multi-platform-desktop--web) below.

The new project's `Content/` folder is pre-populated with starter assets (animation chains, a base `.tmx` map and `StandardTileset`, and platformer/topdown JSON configs). To start with an empty `Content/` folder instead:

```
dotnet new frb2-desktop -n YourGameName --IncludeStarterContent false
```

### Repo setup (`.gitignore`)

Use `frb2-desktop` or `frb2-multiplatform` templates — they already include the recommended `.gitignore`.

Manual baseline:

```
bin/
obj/
Content/obj/
.vs/
*.user
*.suo
```

For web projects, keep the template's `wwwroot/Content/.gitignore` (that folder is build output).

### Step 3 — Build and run

```
cd YourGameName/YourGameName.Desktop
dotnet tool restore
dotnet run
```

> **Why `cd YourGameName.Desktop`?** `dotnet tool restore` installs the MGCB content pipeline tool, and the tool manifest (`.config/dotnet-tools.json`) only lives in that subdirectory. Running `dotnet tool restore` from the solution root silently does nothing.

A window opens showing "Hello from FlatRedBall 2" centered on a black background. If you see that, everything works.

### Step 4 — Start building

Open `YourGameName.Common/Screens/GameScreen.cs`. The template creates a centered label to confirm rendering works — delete that block and replace it with your own code.

**Core concepts:**

- **Screen** — a game state (level, menu, game-over). `GameScreen` is your first one.
- **Entity** — a game object (player, enemy, bullet). Create them through `Factory<T>(this)` — never `new T()` directly.

The engine handles rendering, physics, and collision automatically. Override `CustomInitialize` for one-time setup (factories, content, collision wiring) and `CustomActivity(FrameTime time)` for per-frame logic. For complete lifecycle rules see [`frb-skills/engine-overview/SKILL.md`](../frb-skills/engine-overview/SKILL.md).

Browse the [`samples/`](../samples/) directory for complete games. See [`frb-skills/`](../frb-skills/) for task-specific guides (entities, collision, animation, physics, and more).

### Multi-platform (Desktop + Web)

```
dotnet new frb2-multiplatform -n YourGameName
```

This produces three projects sharing one `Common`:

- `YourGameName.Common/` — game code, shipped as two `net10.0` projects: one for the MonoGame/desktop backend, and one for the KNI/web backend in a `Kni/` subfolder (`YourGameName.Common/Kni/YourGameName.Common.Kni.csproj`) that compiles the same source.
- `YourGameName.Desktop/` — desktop entry point (MonoGame, `net10.0`).
- `YourGameName.BlazorGL/` — Blazor WebAssembly entry point (KNI, `net10.0`). Contains its own `App.razor`, `Pages/Index.razor`, `wwwroot/frb-host.js`, etc. — edit them freely; they're yours.

Run the desktop head as before. To run the web head:

```
cd YourGameName/YourGameName.BlazorGL
dotnet run
```

This launches a local dev server (default `https://localhost:5001`) — open the URL and the game runs in the browser canvas. Apos.Shapes' shader is embedded directly in the package assembly, so neither Wine nor a shader compiler is needed on macOS/Linux.

`Game1.cs` uses `#if KNI` to pick `GraphicsProfile.FL10_0` on the web target (`GraphicsProfile.HiDef` on desktop). Keep that pattern if you add any code that diverges between backends. Save data and other `System.IO.File`-based code should be gated behind `#if !KNI` since browsers have no filesystem.

## Manual setup (reference)

The template above is the supported install path. If you have a reason to wire FlatRedBall2 into an existing project (e.g. you're integrating with a MonoGame project you already have), here's the minimum API surface — but note this snippet alone is **not** a complete working setup. You'll also need a configured MonoGame project (Content pipeline / MGCB tool, `Content/` folder, etc.) which the template handles for you.

Inside an existing .NET project directory (one that already contains a `.csproj`):

1. Install the NuGet package:

   ```
   dotnet add package FlatRedBall2.MonoGame   # desktop (.NET 10)
   # or
   dotnet add package FlatRedBall2.Kni        # browser / Blazor WASM (.NET 10)
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

## Troubleshooting

**`FileNotFoundException: Could not load file or assembly 'MonoGame.Framework, ...'`** — usually a stale package restore where a referenced project resolved a different MonoGame version than what's copied to the output folder. Try this first:

```
dotnet clean
dotnet restore
```

If it persists, delete `bin/` and `obj/` in both the failing project and any project it references, then rebuild.
