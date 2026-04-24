# ShmupSpace — Design Notes

Minimal vertical shmup, deliberately scoped down to showcase core FRB2 features without incidental complexity.

## Locked Decisions

- **Resolution:** 240x320 design, Zoom 3x, portrait. `IncreaseVisibleArea` keeps pixels-per-unit fixed at Zoom so art stays crisp.
- **Combat:** Player shoots; enemies do not. `EnemyShot` chain is unused (room to grow the sample later).
- **Player vs enemy contact:** Player dies; enemy survives. Keeps the sample's failure mode unambiguous.
- **Screens:** `TitleScreen` -> `GameScreen`. On 0 lives, return to `TitleScreen`.
- **UI:** Code-only Gum (no `.gumx`). `Label` + `StackPanel`. Score + Lives only.

## Features Showcased

| Feature | Where |
|---|---|
| `Factory<T>` with offscreen cleanup | `PlayerBullet.CustomActivity`, `Enemy.CustomActivity` |
| Collision relationships (event, no physics) | `GameScreen.CustomInitialize` |
| `.achx` animation load | every entity's `CustomInitialize` |
| Non-looping + `AnimationFinished` self-destroy | `Explosion` |
| Gum HUD + `StackPanel` | `GameScreen.BuildHud`, `TitleScreen` |
| `PreferredDisplaySettings` via `FlatRedBallService.Default.DisplaySettings` | `Game1` |

## Intentional Non-Goals

- **No powerups, no waves, no bosses.** Single enemy type, single bullet type. Adding these would obscure what the sample teaches.
- **No Tiled maps.** The "level" is empty space with a flat background color.
- **No audio.** Engine `AudioManager` is a stub; the sample doesn't pretend otherwise.
- **No score persistence.**
