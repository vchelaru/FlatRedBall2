# Screens in FlatRedBall2

A `Screen` is the top-level container for a game state — a level, a menu, a game-over sequence. Each screen owns its entities, collision relationships, Gum UI, and camera. Only one screen is active at a time.

## Creating a Screen

Subclass `Screen` and override the lifecycle hooks you need:

```csharp
public class GameScreen : Screen
{
    private Factory<Player> _playerFactory = null!;

    public override void CustomInitialize()
    {
        // Create factories, entities, collision relationships, Gum UI
        _playerFactory = new Factory<Player>(this);
        _playerFactory.Create();
    }

    public override void CustomActivity(FrameTime time)
    {
        // Per-frame game logic (after physics and collision have already run)
    }

    public override void CustomDestroy()
    {
        // Optional cleanup before the screen is torn down
        // Factories and entities are destroyed automatically — only needed
        // for external resources you opened manually
    }
}
```

## Lifecycle Order Each Frame

1. **Physics** — entity positions updated from velocity/acceleration
2. **Collision** — registered relationships resolved; positions corrected
3. **`CustomActivity`** — game logic runs with corrected state
4. **`CustomActivity` per entity** — each entity's own `CustomActivity` runs

`CustomInitialize` runs once when the screen is activated, before the first frame.

## What's Available on a Screen

| Property | Type | Notes |
|----------|------|-------|
| `Camera` | `Camera` | This screen's camera — set background color, world bounds, position |
| `Engine` | `FlatRedBallService` | Access to `Random`, `InputManager`, `AudioManager`, etc. |
| `ContentManager` | `ContentManagerService` | Load textures, fonts, and other content |
| `RenderList` | `List<IRenderable>` | All renderables sorted and drawn each frame |
| `Layers` | `List<Layer>` | Named layers for render ordering |

## Navigating Between Screens

Call `MoveToScreen<T>()` from anywhere inside a screen (e.g., `CustomActivity`, a collision event, a Gum button click). The transition is deferred to the start of the next frame so the current frame completes cleanly.

```csharp
// Navigate with no data
MoveToScreen<MainMenuScreen>();

// Navigate with data — configure runs before CustomInitialize
MoveToScreen<GameOverScreen>(s =>
{
    s.Winner = "Player 1";
    s.FinalScore = _score;
});
```

The old screen's `CustomDestroy` runs, its content is unloaded, and then the new screen's `CustomInitialize` runs — all in that order.

## Passing Data to a Screen

Declare public properties on the destination screen. The `configure` callback receives the new screen instance *before* `CustomInitialize` is called, so the data is available immediately:

```csharp
public class GameOverScreen : Screen
{
    // Set by the caller via MoveToScreen configure
    public string Winner { get; set; } = "";
    public int FinalScore { get; set; }

    public override void CustomInitialize()
    {
        var label = new Label();
        label.Text = $"{Winner} wins! Score: {FinalScore}";
        AddGumRenderable(new GumRenderable(label.Visual));
    }
}
```

Caller:

```csharp
// From inside another screen's CustomActivity or a collision event:
MoveToScreen<GameOverScreen>(s =>
{
    s.Winner = _winner;
    s.FinalScore = _score;
});
```

## Starting the First Screen

In `Game1.Initialize`, after `FlatRedBallService.Default.Initialize(this)`:

```csharp
FlatRedBallService.Default.Start<MainMenuScreen>();
```

`Start<T>` activates the first screen and accepts the same optional configure callback as `MoveToScreen`:

```csharp
FlatRedBallService.Default.Start<GameScreen>(s => s.DebugMode = true);
```

## Gotchas

- **`MoveToScreen` is deferred** — the transition does not happen immediately. Code after `MoveToScreen<T>()` in the same frame still runs. If you want to stop processing, `return` after the call.
- **All entities and factories are destroyed automatically** on screen change. You do not need to manually destroy them in `CustomDestroy`.
- **Gum elements are also cleared automatically** — no teardown needed for `AddGumRenderable` elements.
- **`CustomDestroy` is for external resources only** — e.g., file handles or network connections you opened yourself.
- **Do not call `MoveToScreen` from `CustomInitialize`** — the screen hasn't finished initializing yet. Use `CustomActivity` or an event callback.
