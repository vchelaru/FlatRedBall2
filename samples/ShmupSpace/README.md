# ShmupSpace

A minimal vertical shoot-'em-up sample for FlatRedBall2. Portrait 240x320 design resolution, scaled 3x to a 720x960 window.

## Controls

- **Arrow keys / WASD** — move
- **Space** — fire (hold to auto-fire)
- **Escape** — quit

## What it Shows

- **Entities and Factories** — `PlayerShip`, `PlayerBullet`, `Enemy`, `Explosion` each created through `Factory<T>`.
- **Collision Relationships** — bullet vs. enemy (both die + explosion + score), player vs. enemy (player dies, enemy survives).
- **Offscreen cleanup** — bullets self-destruct past the top of the screen; enemies self-destruct past the bottom. Keeps factory pools bounded.
- **`.achx` animation loading** — `AnimationChainListSave.FromFile(...).ToAnimationChainList(Engine.Content)`.
- **Non-looping animation with `AnimationFinished`** — `Explosion` plays once and self-destroys.
- **Code-only Gum HUD** — `StackPanel` title UI and anchored `Label`s for score/lives.

## Build and Run

```
dotnet build samples/ShmupSpace/ShmupSpace.csproj
dotnet run --project samples/ShmupSpace/ShmupSpace.csproj
```
