# ShmupSpace

A minimal vertical shoot-'em-up sample for FlatRedBall2. Portrait 240x320 design resolution, scaled 3x to a 720x960 window.

## Controls

- **Arrow keys / WASD** — move
- **Space** — fire (hold to auto-fire)
- **Escape** — quit

## What it Shows

- **Entities and Factories** — `PlayerShip`, `PlayerBullet`, `Enemy`, `Explosion` each created through `Factory<T>`.
- **Top-down movement** — `PlayerShip` uses `TopDownBehavior` with values loaded from `Content/player.topdown.json`.
- **Collision Relationships** — bullet vs. enemy (both die + explosion + score), player vs. enemy (player dies, enemy survives).
- **Offscreen cleanup** — bullets and enemies self-destruct past the camera bounds.
- **Non-looping animation with `AnimationFinished`** — `Explosion` plays once and self-destroys.
- **Shared `AnimationChainList`** — `GameScreen` loads the `.achx` once; every entity references the same list. This is what makes `.achx` hot-reload work — patching the shared list updates every live sprite.
- **Hot-reload** — `GameScreen.HandleContentChanged` dispatches by extension:
  - `.json` → `GameConfig.CopyFrom` or `TopDownConfig.ApplyTo` on live objects
  - `.png` → engine auto-reloads the `Texture2D` before the callback fires
  - `.achx` → `AnimationChainList.TryReloadFrom` patches chains by name in place
  - anything else → `RestartScreen(RestartMode.HotReload)`
- **Config files (engine-shaped vs. sample-shaped).** `Content/player.topdown.json` follows the engine's `TopDownConfig` schema. `Content/shmupspace.game.json` is bespoke — parsed by `GameConfig` in this project. Edit either while the game runs and the changes land live.
- **Code-only Gum HUD** — `StackPanel` title UI and anchored `Label`s for score/lives.

## Build and Run

```
dotnet build samples/ShmupSpace/ShmupSpace.csproj
dotnet run --project samples/ShmupSpace/ShmupSpace.csproj
```
