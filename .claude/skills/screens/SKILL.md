---
name: screens
description: "Screens in FlatRedBall2. Use when working with screen lifecycle, screen transitions, MoveToScreen, passing data between screens, CustomInitialize/CustomActivity/CustomDestroy, or starting the first screen. Trigger on any screen management or game state transition question."
---

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

See `engine-overview` for the full 8-step frame loop. Key ordering for screens:

- Entity `CustomActivity` runs **before** Screen `CustomActivity` — the screen always sees post-entity, post-collision state.
- `CustomInitialize` runs once when the screen is activated, before the first frame.

## What's Available on a Screen

| Property | Type | Notes |
|----------|------|-------|
| `Camera` | `Camera` | This screen's camera — set background color, world bounds, position |
| `Engine` | `FlatRedBallService` | Access to `Random`, `Input`, `Audio`, etc. |
| `ContentManager` | `ContentManagerService` | Load textures, fonts, and other content |
| `RenderList` | `IReadOnlyList<IRenderable>` | All renderables sorted and drawn each frame; use `Add`/`Remove` to modify |
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
using Gum.Forms.Controls;

public class GameOverScreen : Screen
{
    // Set by the caller via MoveToScreen configure
    public string Winner { get; set; } = "";
    public int FinalScore { get; set; }

    public override void CustomInitialize()
    {
        var label = new Label();
        label.Text = $"{Winner} wins! Score: {FinalScore}";
        Add(label);
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
- **Gum elements are also cleared automatically** — no teardown needed for elements added via `Add`.
- **`CustomDestroy` is for external resources only** — e.g., file handles or network connections you opened yourself.
- **Do not call `MoveToScreen` from `CustomInitialize`** — the screen hasn't finished initializing yet. Use `CustomActivity` or an event callback.
- **Restarting the current screen:** call `RestartScreen()`. The engine recreates a fresh instance of the same type and replays the original configure callback passed to `Start<T>` / `MoveToScreen<T>`. Use this for death/retry. Like `MoveToScreen`, the transition is deferred to the next frame.

```csharp
// Death/retry — replays the original configure
RestartScreen();
```

  For "advance to next level" or any case where configure params should change, use `MoveToScreen` against the same type:

```csharp
MoveToScreen<GameScreen>(s => s.LevelIndex = LevelIndex + 1);
```

  Do **not** manually destroy entities or collision relationships before calling either — the engine handles all teardown automatically.

- **Configure callbacks are retained and replayed.** The engine stores the configure callback you pass to `Start<T>` / `MoveToScreen<T>` so `RestartScreen()` can replay it. C# closures capture variables by reference, so if your callback closes over a mutable local and that local later changes, restart will see the new value. Prefer literals or stable values in configure (`s => s.LevelIndex = 3`), not captured locals that may be mutated later.

## Pausing

`Screen` has built-in pause support:

```csharp
PauseThisScreen();    // freeze entities
UnpauseThisScreen();  // resume
bool paused = IsPaused;
```

**What pauses:** entity physics, entity `CustomActivity`, collision processing.
**What keeps running:** `Screen.CustomActivity`, Gum UI, input — so pause-menu logic lives in `CustomActivity` and Gum overlays still update.

Typical pattern:

```csharp
public override void CustomActivity(FrameTime time)
{
    if (Engine.Input.Keyboard.WasKeyPressed(Keys.Escape))
    {
        if (IsPaused) { UnpauseThisScreen(); _pauseOverlay.IsVisible = false; }
        else          { PauseThisScreen();   _pauseOverlay.IsVisible = true;  }
    }
}
```

`await`-based delays from `Engine.Time.DelaySeconds` and `DelayUntil` **are** suspended while the screen is paused — screen time freezes, so timed tasks don't fire. `DelayFrames` is not pause-aware; frames always advance. See the `timing` skill for full async delay details.
