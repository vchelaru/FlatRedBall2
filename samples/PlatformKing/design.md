# PlatformKing — Game Design Document

## One-Sentence Pitch
A classic 2D platformer where the player jumps across cloud platforms, climbs ladders, swims through water, smashes breakable boxes, and avoids patrolling enemies across two interconnected levels.

## Core Mechanics

### Movement
- **Ground movement**: left/right with acceleration and deceleration.
- **Jump**: variable-height jump with button hold (Space or Up arrow). Double-jump allowed — a second jump in the air resets and launches again. Double jump refreshes on landing.
- **Cloud platforms (one-way)**: tiles of class `JumpThroughCollision` — player can jump up through from below, land on top, and drop through with Down + Jump.
- **Ladders**: tiles of class `Ladder` — player presses Up to grab, Up/Down to climb, Left/Right moves (slowly) while on the ladder, Jump to leap off. Ladder detection handled by `PlatformerBehavior.Ladders`.
- **Swimming (water zones)**: tiles of class `Water`. When the player overlaps a water zone, a `IsSwimming` flag is set in game code. While swimming: gravity is greatly reduced, Up/Down input controls vertical position, horizontal speed is slightly reduced, double-jump is disabled. Pressing Jump while submerged gives a small vertical boost instead of a full platformer jump. Leaving the water restores normal physics.

### Entities
- **Player**: Single player entity. Loses a life on enemy contact or touching `Death` tiles. Respawns at the spawn point in the current level. No health bar — game just restarts the current level on death. Uses animations from `PlatformerAnimations.achx` template.
- **Box (breakable)**: Entity with `BreakableCollision` class marker in TMX. Solid (player bounces off sides and top). Destroyed when the player jumps on top (player's velocity is downward at contact AND the separation pushes the player upward). On break: entity is destroyed.
- **Enemy**: Entity with `EnemyFlag` class marker in TMX. Simple horizontal patrol — moves back and forth between walls/edges. Player dies on contact. Enemy uses a rectangular shape (cyan placeholder). Enemy reverses on solid collision or on reaching a floor edge (raycasts downward).

### Level Progression
- Two levels: `Level1.tmx` and `Level2.tmx`. Same structure, different layouts.
- Each level has a `Door` tile (class `Door`) that serves as the exit trigger. Touching the door transitions to the other level (`MoveToScreen<GameScreen>` with `LevelIndex` toggled between 0 and 1).
- Player position at entry is determined by the `PlayerSpawn` tile in the destination level. The spawn must be placed near the door side the player would logically emerge from — human will design this in Tiled.
- No overall win state — the player can travel back and forth indefinitely.

### No HUD
No health bar, score, lives counter, or any UI elements. Pure gameplay screen.

## Controls
| Input | Action |
|---|---|
| A / Left Arrow | Move left |
| D / Right Arrow | Move right |
| Space / Up Arrow | Jump (hold for higher jump) |
| Space / Up Arrow (in air) | Double jump (once per airborne phase) |
| Up Arrow (at ladder) | Grab and climb up |
| Down Arrow (on ladder) | Climb down |
| Down + Jump (on cloud) | Drop through |
| Up / Down (swimming) | Swim vertically |

## Win / Lose
- **Lose**: contact with an Enemy or `Death` tile restarts the current level.
- **Win**: none — the game loops between the two levels indefinitely.

## Entities and TMX Tile Classes Used

| Class | Used by |
|---|---|
| `SolidCollision` | Walls, floors, ceilings |
| `JumpThroughCollision` | Cloud / one-way platforms (IDs 3–5) |
| `Ladder` | Climbable ladder tiles (ID 96) |
| `Water` | Swimming zones (ID 32) |
| `BreakableCollision` | Box entity spawn marker (ID 33) |
| `PlayerSpawn` | Player spawn point (ID 29) |
| `EnemyFlag` | Enemy spawn marker (ID 66) |
| `Door` | Level-exit trigger (ID 64) |
| `Death` | Instant-kill zones (ID 69) |

## Content Scaffold Rule
Each TMX must contain exactly one of each class the code references — the human designs the real levels in Tiled. No procedural generation of tile data.

## Scope Boundary
- No audio
- No HUD or UI of any kind
- No score or collectibles
- No moving platforms
- No slopes
- No boss enemies
- No save/load
- No title or game-over screen
