# Zelda Rooms -- Game Design Document

## One-Sentence Pitch

A top-down action game where the player fights through a linear sequence of single-screen rooms with NES Zelda-style combat and room transitions.

## Player Experience Goals

- The player should feel the satisfying crunch of NES-era combat: commit to a sword swing, time it right, and enemies pop.
- Room transitions should feel classic and polished -- the camera slides to reveal each new room, giving a sense of progression through a dungeon.
- Failure should feel fair. The player always knows why they got hit (they mistimed an attack or walked into an enemy).

## Tone and Mood

- **Visual**: Simple geometric shapes -- rectangles for the player, walls, and enemies. No textures. Color conveys meaning (player is one color, enemies another, walls neutral).
- **Audio**: Not scoped for prototype. Silent or placeholder.
- **Atmosphere**: Focused and clean. The prototype is about feel, not atmosphere. Think "playable wireframe."

## Core Loop

1. Enter a room.
2. Defeat all enemies to unlock the exit (first time only -- revisited rooms don't require re-clearing).
3. Move to the right edge of the screen to transition to the next room.
4. Repeat until all 5 rooms are cleared or the player dies.

## Movement and Controls Feel

- **Movement**: 8-directional (4 cardinal + 4 diagonal when holding two keys). Snappy and responsive -- no acceleration ramp, immediate full speed on press, immediate stop on release. The player always faces the last direction they moved. Uses TopDownBehavior which supports 8-way out of the box.
- **Attack**: Press attack key to swing the sword. The sword hitbox appears in front of the player (in the direction they face) for a brief duration (~0.2-0.3 seconds). The player is locked in place during the attack animation -- no movement until the sword disappears. This commitment is the core tension of combat.
- **Knockback**: When the player touches an enemy, they take damage and get knocked back away from the enemy. Brief invincibility frames after taking a hit (player flashes or changes color).

### Controls (Keyboard Only)

| Action | Key |
|--------|-----|
| Move Up | W or Up Arrow |
| Move Down | S or Down Arrow |
| Move Left | A or Left Arrow |
| Move Right | D or Right Arrow |
| Attack | Space |

## Enemies

**Wanderer** -- the only enemy type.

- Moves in a random cardinal direction for a short distance, pauses briefly, picks a new direction, repeats.
- Damages the player on contact. No projectiles, no lunging, no attack animation.
- Takes 3 sword hits to kill. No visible health bar -- just a brief flash/color change on hit to confirm damage.
- Does not leave the room boundaries (collides with walls).
- Does not chase the player -- purely random movement.

## Win / Lose / Progression

- **Room progression**: Linear sequence of 5 rooms, left-to-right. The exit is always on the right edge. The player must defeat all enemies in a room to unlock the exit the first time. Once cleared, the room stays "cleared" -- if the player re-enters, enemies respawn but the exit remains open.
- **Death**: When the player runs out of hearts, show a game over screen. No save, no continue -- restart from room 1.
- **Win condition**: Clearing the 5th room displays a win screen.
- **No persistent progression**: Each playthrough starts fresh. No upgrades, no unlocks.

## Health System

- **Player health**: 3 hearts. Each enemy contact costs 1 heart.
- **HUD**: Display hearts as filled circles (or rectangles) at the top of the screen using Gum. Filled shape = health remaining, empty/outline shape = health lost.
- **No healing**: No pickups or recovery in this prototype.
- **Invincibility frames**: After taking damage, ~1 second of invincibility. Visual feedback: player shape flickers or changes color during invincibility.

## Room Structure

- Each room is exactly one screen (no scrolling). The camera is fixed per room.
- Rooms contain walls (axis-aligned rectangles) and enemies. Wall layouts vary per room to create simple navigational challenges.
- **Room transitions**: When the player walks to a room exit (an opening in the wall at the screen edge), the camera slides smoothly in that direction to reveal the next room, NES Zelda style. The player slides with the camera into the new room. During the transition, gameplay is paused (no enemy movement, no damage).
- **Room data**: Each room defines its wall layout and enemy spawn positions. Enemies respawn if the player re-enters a room, but a cleared room's exit stays open (no need to re-kill to progress).

## Room Progression Plan (5 Rooms)

1. **Room 1**: Empty room, 1 enemy. Teaches movement and combat.
2. **Room 2**: A few wall segments creating a simple corridor. 2 enemies.
3. **Room 3**: More complex wall layout (L-shaped walls or a central pillar). 3 enemies.
4. **Room 4**: Tighter corridors with 3-4 enemies. Forces the player to time attacks carefully.
5. **Room 5**: Open arena, 4-5 enemies. Final challenge.

## Pacing

- **Session length**: Under 5 minutes for a successful run.
- **Difficulty**: Moderate. Contact damage plus attack commitment means careless play is punished, but enemies are slow and predictable.
- **Tempo**: Room-by-room escalation. Each room adds slightly more enemies or tighter spaces.

## Scope

- **Prototype**. Game feel is the priority -- the sword attack, knockback, and room transitions should feel good.
- **No textures**: Everything is geometric shapes with solid colors.
- **No audio**: Out of scope for this prototype.
- **Keyboard only**: No gamepad support needed.

## Moments to Design For

- The satisfying "commit and swing" of the sword attack -- the brief lock-in-place followed by an enemy popping.
- The NES Zelda room transition slide -- smooth, clean, and nostalgic.
- The tension of being in a tight corridor with an enemy wandering toward you, deciding when to commit to the attack.
- Getting hit, knockback, invincibility flicker -- the classic damage feedback loop.

## Out of Scope

- Gamepad / mouse input
- Audio / music / sound effects
- Textures or sprites (shapes only)
- Enemy AI beyond random wandering
- Pickups, items, keys, or doors
- Save / continue system
- Scrolling rooms or rooms larger than one screen
