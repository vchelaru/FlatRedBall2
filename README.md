# FlatRedBall2

> **Early Preview** — This engine is in active development. APIs will change between releases.

FlatRedBall2 is the next generation of [FlatRedBall](https://github.com/vchelaru/FlatRedBall)  — a 2D game engine with 20+ years of iteration behind it, rebuilt from the ground up on modern .NET. It runs on two backends: [MonoGame](https://monogame.net) for desktop and [KNI](https://github.com/kniEngine/kni) for browser (via Blazor WASM), sharing a single codebase.

## Features

- **Screens & Entities** — structured game object model with lifecycle hooks (`CustomInitialize`, `CustomActivity`, `CustomDestroy`)
- **Collision relationships** — declarative move/bounce collision between entity groups; one call to wire up an entire system
- **Shapes & physics** — built-in `AxisAlignedRectangle`, `Circle`, and `Polygon` with kinematic physics
- **Platformer & top-down movement** — first-class built-in behaviors; no custom physics code required
- **Gum UI integration** — full [MonoGame Gum](https://github.com/vchelaru/Gum) support for menus, HUDs, and in-game UI
- **Input system** — keyboard, gamepad, and input interfaces for action binding
- **Camera** — configurable 2D camera with world/screen coordinate transforms
- **Async support** — async/await compatible throughout the game loop
- **Hot reload** — all content files reload at runtime without restarting
- **Extensive XML documentation** — every public API documented; IntelliSense covers everything
- **AI assistant support** — ships with skill files in `/ai-reference/` for any AI coding tool

## Packages

Install via NuGet:

```
dotnet add package FlatRedBall2.MonoGame   # desktop (.NET 10)
dotnet add package FlatRedBall2.Kni        # browser / Blazor WASM (.NET 8)
```

## Quick Start

Install the project template (re-run this before each new project to get the latest):

```
dotnet new install FlatRedBall2.Templates
```

Then scaffold a new project:

```
dotnet new frb2-desktop -n YourGameName
cd YourGameName/YourGameName.Desktop
dotnet tool restore
cd ..
dotnet build YourGameName.Desktop/YourGameName.Desktop.csproj
```

Your project is ready. `YourGameName.Common/Screens/GameScreen.cs` is where your game code goes.

### Manual setup (advanced)

If you prefer to wire things up yourself, install the NuGet package and set up `Game1.cs` as follows:

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
        // your game logic here
    }
}
```

See the `samples/` directory for complete working examples.

## Samples

| Sample | Description | Play |
|--------|-------------|------|
| [ShmupSpace](samples/ShmupSpace/) | Shoot-em-up | [▶ Play in browser](https://vchelaru.github.io/FlatRedBall2/ShmupSpace/) |
| [PlatformKing](samples/PlatformKing/) | Platformer | Source only |

## Working with AI Assistants

FlatRedBall2 ships with skill files in [`/ai-reference/`](ai-reference/) — plain Markdown guides covering common engine tasks (entities, collision, physics, animation, audio, and more). Any AI coding assistant can use them; paste the relevant file into your context before starting a task.

**Claude Code** users get slash-command integration automatically:

```
/entities-and-factories    — entity lifecycle, spawning, factories
/collision-relationships   — move/bounce collision setup
/physics-and-movement      — gravity, drag, velocity
/animation                 — sprite animation chains
/screens                   — screen lifecycle and transitions
```

See the full list in [`ai-reference/`](ai-reference/).

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
