---
name: screens
description: "Screens in FlatRedBall2. Use when working with screen lifecycle, screen transitions, MoveToScreen, passing data between screens, CustomInitialize/CustomActivity/CustomDestroy, starting the first screen, pause menu, or pausing/resuming a screen. Trigger on any screen management or game state transition question."
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

## Returning Data from a Sub-Screen

When a sub-screen (e.g., a battle screen) needs to pass results back to the parent, use the `configure` callback on the *return* transition. The configure runs before the destination screen's `CustomInitialize`, so the data is ready immediately:

```csharp
// In BattleScreen — when battle ends, navigate back with the result:
MoveToScreen<ExplorationScreen>(s => s.ReturnedBattleResult = result);

// In ExplorationScreen — declare the property and read it in CustomInitialize:
public BattleResult? ReturnedBattleResult { get; set; }  // set via configure

public override void CustomInitialize()
{
    if (ReturnedBattleResult != null) { /* resume world state based on result */ }
}
```

If the return data must survive a full screen teardown (e.g., the destination is always freshly initialized), use a static field cleared in `CustomInitialize`. This is a stopgap until `Screen.PushScreen`/`PopScreen` ships:

```csharp
public class ExplorationScreen : Screen
{
    public static BattleResult? PendingReturn;  // BattleScreen sets this before transitioning back

    public override void CustomInitialize()
    {
        var result = PendingReturn;
        PendingReturn = null;    // clear — stale value on next entry otherwise
        if (result != null) { /* apply */ }
    }
}
```

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

## Observability — Screen.Entities and SceneSnapshot

`Screen.Entities` exposes all managed entities as `IReadOnlyList<Entity>` (read-only — mutation still goes through `Factory<T>`). Combined with `SceneSnapshot.Capture(screen)`, this lets test code inspect the full game state:

```csharp
var harness = TestHarness.Create<GameScreen>();
harness.Screen.CustomInitialize();
harness.Step(10);
var snap = harness.Snapshot();
// Query by type, name, or proximity
snap.OfType<Player>().ShouldHaveSingleItem();
snap.Named("coin").Count.ShouldBe(5);
snap.NearPoint(new Vector2(0, 0), 100f).ShouldNotBeEmpty();
```

## Gotchas

- **Delay before transitioning** — to pause before a screen change (e.g., a flash effect), `await Engine.Time.DelaySeconds(t, Token)` first, then call `MoveToScreen<T>()`. Never use `Thread.Sleep` — it freezes the render thread and blocks the game loop entirely.
- **`MoveToScreen` is deferred** — the transition does not happen immediately. Code after `MoveToScreen<T>()` in the same frame still runs. If you want to stop processing, `return` after the call.
- **Do not cache entity references across `MoveToScreen`** — the source screen's entities are destroyed on transition. Singletons and static fields must store data values only, not object references to entities.
- **Save data is not a content asset** — player save files (`PlayerData`, inventory, progress) cannot be loaded via `ContentManager.Load<T>()`. Use `System.IO.File.ReadAllText` + `System.Text.Json.JsonSerializer` directly.
- **All entities and factories are destroyed automatically** on screen change. You do not need to manually destroy them in `CustomDestroy`.
- **Gum elements are also cleared automatically** — no teardown needed for elements added via `Add`.
- **`CustomDestroy` is for external resources only** — e.g., file handles or network connections you opened yourself.
- **Do not call `MoveToScreen` from `CustomInitialize`** — the screen hasn't finished initializing yet. Use `CustomActivity` or an event callback.
- **Restarting the current screen:** call `RestartScreen()`. The engine recreates a fresh instance of the same type and replays the most recently retained configure callback. Use this for death/retry. Like `MoveToScreen`, the transition is deferred to the next frame.

```csharp
// Death/retry — replays the retained configure
RestartScreen();

// Restart with a tweak — replaces the retained configure for this and future restarts
this.RestartScreen(s => s.LevelIndex++);
```

  For "advance to next level" you can use either `MoveToScreen<SameType>` or the typed `RestartScreen` overload — both fully tear down and recreate. Use whichever reads more clearly at the call site.

  Do **not** manually destroy entities or collision relationships before calling either — the engine handles all teardown automatically.

- **One configure slot, last-writer-wins.** The engine retains a single configure callback per session. `Start<T>(d)` and `MoveToScreen<T>(d)` set it. `RestartScreen(d)` (typed extension) replaces it. Plain `RestartScreen()` replays whatever is currently in the slot.

- **Closure gotcha — prefer literals to captured locals.** Because the retained callback is replayed against its current closure environment (not a snapshot), any mutable local the callback closed over will be re-read at restart time. Write `s => s.LevelIndex = 3` rather than `s => s.LevelIndex = level` if `level` may have changed by restart time.

## Hot-Reload Restart

When a content file changes on disk and you want to restart the screen *without* losing session state (player position, score, timer), use the hot-reload restart mode:

```csharp
RestartScreen(RestartMode.HotReload);
```

This is identical to a death/retry restart with two extra calls bracketing the teardown:

1. `SaveHotReloadState(state)` runs on the OLD instance, before teardown — live game state is still intact, so you can read your fields and stuff them into the typed bag.
2. `RestoreHotReloadState(state)` runs on the NEW instance, after `CustomInitialize` — the level has been freshly built, then your restore patches saved values on top.

```csharp
public override void SaveHotReloadState(HotReloadState state)
{
    state.Set("score", _score);
    state.Set("timeRemaining", _timeRemaining);
}

public override void RestoreHotReloadState(HotReloadState state)
{
    _score = state.Get<int>("score");
    _timeRemaining = state.Get<float>("timeRemaining");
}
```

`HotReloadState` is a typed key/value bag: `Set<T>(key, value)`, `Get<T>(key)` (throws if missing), `TryGet<T>(key, out value)`.

**Plain `RestartScreen()` is `RestartMode.DeathRetry` and never calls these hooks** — by design, so death/retry can't accidentally preserve stale state across a death.

**Restore runs after `CustomInitialize` intentionally.** `CustomInitialize` builds the level from scratch, then restore patches saved values on top. The reverse order would let `CustomInitialize` clobber whatever restore set.

**The engine does not auto-preserve anything.** Hot-reload preservation is entirely user-driven via `Save`/`RestoreHotReloadState`. In particular, **preserve player position to avoid a jarring camera pop**: if the player is restored to their pre-reload position, any `CameraControllingEntity` will follow them on the first frame and the camera lands correctly automatically. If you don't restore the player, the player respawns at the spawn marker and the camera snaps to the spawn point, even if you tried to preserve `Camera.X/Y` directly (the controller overwrites it on frame 1).

Canonical recipe:

```csharp
public override void SaveHotReloadState(HotReloadState state)
{
    state.Set("playerX", _player.X);
    state.Set("playerY", _player.Y);
    state.Set("playerVx", _player.VelocityX);
    state.Set("playerVy", _player.VelocityY);
    state.Set("score", _score);
}

public override void RestoreHotReloadState(HotReloadState state)
{
    _player.X = state.Get<float>("playerX");
    _player.Y = state.Get<float>("playerY");
    _player.VelocityX = state.Get<float>("playerVx");
    _player.VelocityY = state.Get<float>("playerVy");
    _score = state.Get<int>("score");
}
```

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
