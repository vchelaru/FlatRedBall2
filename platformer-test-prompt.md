# Platformer Test — SMB-Style Level Navigation

Build a platformer sample inspired by Super Mario Bros. The focus is **level design and navigation only** — no enemies, no combat, no collectibles. This is a test of whether the engine (and you) can produce well-designed platformer levels.

## Requirements

- **Movement feel**: Similar to Super Mario Bros — snappy horizontal movement, a satisfying variable-height jump arc, gravity that feels right. Use `SetJumpHeights` to define min/max jump heights based on tile size.
- **Tile size**: 16px tiles.
- **3 levels with increasing complexity**:
  - Level 1: Flat ground with simple gaps and low platforms. Teaches the player that they can jump and how far.
  - Level 2: Introduces vertical platforming — staircase structures, higher platforms, longer gaps that require full-hold jumps.
  - Level 3: Combines horizontal and vertical challenge — requires the player to understand both their min and max jump capabilities.
- **Levels created in TMX**: Each level is a `.tmx` file. Collision geometry is generated from tile layers using `TileShapeCollection` — not hand-placed shapes.
- **Scrolling camera**: Camera follows the player horizontally. The level should be wider than the screen.
- **Level progression**: Reaching the right edge of a level advances to the next. After level 3, loop back or show a simple "done" state.
- **No enemies, no collectibles, no score**. The only gameplay is navigating the terrain.

## What This Tests

- Can you translate "feels like SMB" into concrete `PlatformerValues`?
- Can you design levels where platform spacing is derived from the player's actual jump capabilities?
- Do the TMX levels load correctly with collision?
- Does the camera scroll smoothly?
