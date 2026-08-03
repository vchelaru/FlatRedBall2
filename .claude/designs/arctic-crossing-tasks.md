# Arctic Crossing -- Implementation Task Breakdown

> Source GDD: `.claude/designs/arctic-crossing-design.md`
> Sample project: `samples/ArcticCrossingSample/`
> Reference project: `samples/PlatformerSample/` (platformer movement pattern)

---

## Phase 1 -- Project Setup and Boilerplate

**Goal:** Empty project that compiles, runs, and shows a blank screen.

### 1.1 Project scaffolding

Create `samples/ArcticCrossingSample/` following the `sample-project-setup` skill:

- `ArcticCrossingSample.csproj` (reference `PlatformerSample.csproj` for structure -- no content pipeline assets needed since everything is shapes)
- `Program.cs` -- boot into `Game1`
- `Game1.cs` -- landscape window (e.g. 1280x720), initialize FlatRedBallService, start `TitleScreen`
- Run `dotnet build` to verify clean compile

**Files to create:**
- `samples/ArcticCrossingSample/ArcticCrossingSample.csproj`
- `samples/ArcticCrossingSample/Program.cs`
- `samples/ArcticCrossingSample/Game1.cs`
- `samples/ArcticCrossingSample/Screens/TitleScreen.cs` (empty shell -- just a colored background)

### 1.2 Game constants and color palette

Create a static `GameColors` class and a `GameConstants` class to centralize:

- **Colors:** Ocean blue, ice white, sky gradient blues, checkpoint flag colors, player warm tones, NPC palettes (polar bear white, penguin black/white/orange, unicorn pastel purple/pink, seal gray)
- **Constants:** Screen dimensions, gravity, default player speed, lives per phase (3), max lives (5), points per platform, checkpoint spacing guidelines

**Files to create:**
- `samples/ArcticCrossingSample/GameColors.cs`
- `samples/ArcticCrossingSample/GameConstants.cs`

**Build and run checkpoint.** The project compiles and shows a colored screen.

---

## Phase 2 -- Core Player Entity

**Goal:** A blocky humanoid that runs and jumps with snappy platformer controls.

**Depends on:** Phase 1

### 2.1 Player entity with shape-built character

Create `Player` entity (see `entities-and-factories` + `shapes` skills):

- Composed of multiple shapes: body rectangle (torso), leg rectangle, arm rectangles, head (circle or square)
- All shapes attached via `Add()` so they move with the entity
- Collision shape: single `AxisAlignedRectangle` encompassing the character (used for platform collision -- separate from visual shapes)
- Default color palette (warm tones to contrast arctic environment)

**Files to create:**
- `samples/ArcticCrossingSample/Entities/Player.cs`

### 2.2 Platformer movement

Wire up `PlatformerBehavior` on the Player (reference `PlatformerSample/Entities/Player.cs` for the exact pattern):

- Ground movement: quick acceleration, quick stop (snappy, not floaty). Use short `AccelerationTimeX`/`DecelerationTimeX`.
- Air movement: moderate air control. Longer deceleration than ground, but player can still adjust.
- Jump: precise, variable height via `JumpApplyByButtonHold = true`
- Gravity and max fall speed tuned for "ice platformer" feel
- Input: arrow keys / WASD for movement, Space for jump (see `input-system` skill)
- No wall jumps

### 2.3 Character variants (male/female)

Create a `CharacterVariant` enum (Male, Female) and a method or config that applies different visual properties:

- Different color palettes
- Slightly different proportions or head shape (circle vs square, or different size ratios)
- No gameplay difference -- purely cosmetic
- The selected variant is stored in a `GameState` class and passed to the Player entity on creation

**Files to create:**
- `samples/ArcticCrossingSample/Models/CharacterVariant.cs`
- `samples/ArcticCrossingSample/Models/GameState.cs` (holds selected character, current phase, lives, score, unlocked phases)

### 2.4 Basic test screen

Create a `TestScreen` (temporary) with:

- A few static platforms (rectangles) to jump between
- Ocean below (blue rectangle filling the bottom)
- `Factory<Player>` to spawn the player
- Verify: player moves, jumps, lands on platforms, falls into water

Wire collision: Player vs platforms using `AddCollisionRelationship` with platformer-appropriate collision response (see `collision-relationships` skill). The `PlatformerBehavior` needs to know when the player is grounded.

**Files to create:**
- `samples/ArcticCrossingSample/Screens/TestScreen.cs`

**Build and run checkpoint.** Player character runs and jumps on static platforms with snappy controls.

---

## Phase 3 -- Platform Types

**Goal:** All platform behaviors that make the game interesting.

**Depends on:** Phase 2

### 3.1 Base Platform entity

Create a `Platform` entity with a base rectangle shape. All platform types either extend this or use a `PlatformType` enum to switch behavior:

- Static ice block (white/light blue rectangle) -- no special behavior
- Collision shape for player landing

**Files to create:**
- `samples/ArcticCrossingSample/Entities/Platform.cs`

### 3.2 Moving platforms

Platforms that drift along a path (left/right or up/down):

- Movement defined by start position, end position, and speed
- Smooth oscillation between endpoints
- Player rides the platform (position updates carry the player) -- this may need manual position adjustment in the collision handler or use of `MoveFirstOnCollision` (check `collision-relationships` skill for the right pattern)

**Key risk:** Moving platforms carrying the player is a classic platformer challenge. Test that the player doesn't slide off or jitter. May need to parent player position to the platform while grounded on it.

### 3.3 Crumbling platforms

Platforms that break after the player stands on them:

- Timer starts when player lands (e.g. 2 seconds)
- Visual feedback: crack lines appear (additional thin rectangles overlaid), color shifts from white to gray
- After timer: platform falls (accelerates downward) and is destroyed
- Should feel fair -- enough time to react and jump off

Uses `timing` skill for the crumble countdown.

### 3.4 Tilting platforms

Platforms that rock back and forth:

- Oscillating rotation (use `Polygon` instead of `AxisAlignedRectangle` since AABBs cannot rotate)
- Player slides toward the low side when standing on a tilting platform (apply horizontal velocity based on tilt angle)
- Introduced in Phase 4 (The Storm) but built here for reuse

**Key risk:** Rotated collision with the platformer behavior. May need to use polygon collision or fake the tilt with visual-only rotation + horizontal force on the player.

### 3.5 One-shot falling platforms

Platforms that exist once and fall after being touched:

- Immediate drop on player contact (no delay, or very short ~0.3s delay)
- Falls downward, destroyed off-screen
- Used in Phase 5's vertical climbing section
- The player must jump off immediately

### 3.6 Icy surfaces (optional per-platform)

Some platforms have a "slippery" modifier:

- Increases `DecelerationTimeX` while the player stands on them (player slides more)
- Visual: slightly different color tint (more blue/translucent look)
- Applied sparingly -- not all platforms, just specific ones for flavor

**Build and run checkpoint.** Test screen has one of each platform type. All behave correctly with the player.

---

## Phase 4 -- Level Infrastructure

**Goal:** Screen system supports multiple phases, checkpoints, lives, scoring, and progression.

**Depends on:** Phase 2, Phase 3

### 4.1 GameplayScreen

Create the main `GameplayScreen` that replaces TestScreen (see `screens` skill):

- Receives the current phase number from GameState
- Loads level data for that phase
- Manages all entity factories: `Factory<Player>`, `Factory<Platform>`, `Factory<Checkpoint>`, etc.
- Camera follows player horizontally (see `camera` skill), with vertical adjustment for Phase 5
- Ocean background at the bottom, sky color based on phase

**Files to create:**
- `samples/ArcticCrossingSample/Screens/GameplayScreen.cs`

### 4.2 Level data format

Create a `LevelData` class that defines a phase's layout (see `levels` skill):

- List of platform definitions: type, position, size, movement parameters
- List of checkpoint positions
- List of NPC spawn positions and types
- List of collectible positions
- Player start position
- Phase exit position (rightward for phases 1-4, upward for phase 5)
- Background color/mood settings (for storm darkening in Phase 4)

Level data can be hardcoded initially (static methods returning `LevelData`) -- no need for JSON files for 5 hand-designed levels.

**Files to create:**
- `samples/ArcticCrossingSample/Models/LevelData.cs`
- `samples/ArcticCrossingSample/Levels/` (one file per phase, e.g. `Phase1Data.cs`)

### 4.3 Checkpoint entity

Create `Checkpoint` entity:

- Visual: tall thin rectangle "flag" on a stable platform, bright color
- State: inactive (default color) / active (changed color + flash on activation)
- Collision with player activates it and records the respawn position in GameState
- Checkpoints persist within a phase attempt but reset if all lives are lost

**Files to create:**
- `samples/ArcticCrossingSample/Entities/Checkpoint.cs`

### 4.4 Lives system

Implement in GameState and GameplayScreen:

- 3 lives per phase start (displayed on HUD -- Phase 7)
- Falling into water (Y below ocean threshold): lose a life, splash effect, respawn at last checkpoint
- 0 lives: phase restarts from beginning, lives reset to 3, checkpoints reset
- Brief "Try Again?" prompt (can be simple text, no need for elaborate game-over screen)
- Unicorn grants +1 life (max 5)

### 4.5 Scoring system

Implement in GameState:

- Points for each new platform landed on (small amount, e.g. 10-50)
- Collectible diamonds/stars on platforms (100-500 points each)
- High score saved per phase (persist to GameState, optionally to file)

**Files to create:**
- `samples/ArcticCrossingSample/Entities/Collectible.cs` (small diamond/star shape)

### 4.6 Death and respawn flow

When the player dies (falls below ocean Y threshold):

1. Brief splash visual (blue particles or expanding circle)
2. Short delay (~0.5s)
3. Decrement lives
4. If lives > 0: respawn player at last checkpoint position with brief invincibility
5. If lives == 0: show "Try Again?" prompt, restart phase

### 4.7 Phase completion and transitions

When player reaches the phase exit:

- Transition effect (fade or flash -- see `screens` skill for screen transitions)
- Unlock next phase in GameState
- Return to PhaseSelectScreen (or advance directly -- design choice)

**Build and run checkpoint.** A phase can be played start to finish with checkpoints, lives, scoring, and completion.

---

## Phase 5 -- NPC Entities

**Goal:** Arctic animals populate the world and add personality.

**Depends on:** Phase 3 (platforms), Phase 4 (GameplayScreen)

### 5.1 Polar Bear

Create `PolarBear` entity:

- Shape construction: large white rectangle body, two small white circle ears, circle snout
- Behavior: stands still on stable platforms, faces the player when nearby
- Interaction: when player is close, display a short text hint (Gum text label or simple positioned text)
- Appears at phase starts and near tricky sections (hint-giver role)

**Files to create:**
- `samples/ArcticCrossingSample/Entities/PolarBear.cs`

### 5.2 Penguin

Create `Penguin` entity:

- Shape construction: small black rectangle body, white rectangle belly overlay, orange triangle beak, orange triangle feet
- Behavior: waddles back and forth on platforms (oscillates between two X positions)
- Collision with player: slight nudge (apply small horizontal velocity to player) -- does not damage
- Special variant: belly-slide penguin that zooms across a platform at speed (surprise obstacle). Triggered by proximity or timer.

**Files to create:**
- `samples/ArcticCrossingSample/Entities/Penguin.cs`

### 5.3 Seal

Create `Seal` entity:

- Shape construction: rounded rectangle body (use `AxisAlignedRectangle` with wide aspect ratio), small circle flippers, circle nose
- Behavior: pops up from below platforms (rises from water), occupies space on ice blocks
- Effect: platforms with seals sink slightly (visual -- lower the platform Y a bit)
- Occasional bark animation (brief scale pulse, purely cosmetic)

**Files to create:**
- `samples/ArcticCrossingSample/Entities/Seal.cs`

### 5.4 Unicorn

Create `Unicorn` entity:

- Shape construction: rectangle body (pastel purple/pink), triangle horn on head, small rectangle legs
- Placement: hidden or hard-to-reach platforms off the main path
- Interaction: collision grants +1 life (up to max 5) and bonus points
- After interaction: plays a sparkle effect and disappears
- Rare -- one per phase (or not in every phase)

**Files to create:**
- `samples/ArcticCrossingSample/Entities/Unicorn.cs`

### 5.5 NPC spawning in level data

Extend `LevelData` to include NPC spawn definitions:

- Type, position, optional parameters (e.g. penguin waddle range, polar bear hint text)
- `GameplayScreen` spawns NPCs from level data on phase load

**Build and run checkpoint.** NPCs appear in the test level, waddle, stand, pop up, and interact with the player.

---

## Phase 6 -- Menu Screens

**Goal:** Full menu flow from title to gameplay and back.

**Depends on:** Phase 1 (project), Phase 4 (GameState, phase infrastructure)

### 6.1 Title Screen

Create `TitleScreen` (see `screens` + `gum-integration` skills):

- "Arctic Crossing" title text (large, centered)
- Background: ocean blue with a few drifting white rectangle "ice blocks" and a distant mountain shape (triangle/polygon)
- A penguin or two on the title screen (cosmetic, using Penguin entity or simple shapes)
- Menu options: Start Game, Phase Select (visible if any phase completed), Character Select
- Input: up/down to navigate, Enter/Space to select

### 6.2 Character Select Screen

Create `CharacterSelectScreen`:

- Display male and female character shapes side by side
- Left/right input to highlight one
- Confirm selection stores variant in GameState
- Visual: selected character is slightly larger or has a highlight border
- Could show character names if desired

**Files to create:**
- `samples/ArcticCrossingSample/Screens/CharacterSelectScreen.cs`

### 6.3 Phase Select Screen

Create `PhaseSelectScreen`:

- Horizontal row of 5 phase icons (rectangles with phase number)
- Locked phases: gray color, cannot be selected
- Completed phases: checkmark indicator, high score displayed below
- Unlocked (next available) phase: full color, selectable
- Mountain graphic that fills in as phases are completed (optional visual flourish)
- Left/right to browse, Enter to select and start phase

**Files to create:**
- `samples/ArcticCrossingSample/Screens/PhaseSelectScreen.cs`

### 6.4 Pause Menu

Create pause overlay within `GameplayScreen`:

- Triggered by Escape key
- Pauses game update (stop entity activity)
- Options: Resume, Restart Phase, Quit to Menu
- Semi-transparent dark overlay with menu text
- Resume unpauses; Restart reloads the phase; Quit transitions to TitleScreen

**Build and run checkpoint.** Full menu flow: Title -> Character Select -> Phase Select -> Gameplay -> Pause -> back to menus.

---

## Phase 7 -- HUD

**Goal:** In-game UI showing lives, score, and phase info.

**Depends on:** Phase 4 (lives, scoring), Phase 6 (Gum integration established)

### 7.1 Lives display

Top-left of screen:

- Small heart or character-head shapes (one per life)
- Updates when lives change (lose or gain)
- Use Gum elements or positioned shape entities pinned to camera

### 7.2 Score display

Top-right of screen:

- Numeric score, updates in real-time
- Gum text label

### 7.3 Phase indicator

Top-center of screen:

- "Phase X: [Name]" text (e.g. "Phase 2: Open Water")
- Fades out after 3 seconds using a timer (see `timing` skill)

**Files to create:**
- `samples/ArcticCrossingSample/UI/GameHUD.cs` (or integrated into GameplayScreen)

**Build and run checkpoint.** HUD visible during gameplay with lives, score, and phase name.

---

## Phase 8 -- Phase 1-5 Level Designs

**Goal:** All 5 phases designed and playable.

**Depends on:** All previous phases (entities, platforms, NPCs, infrastructure)

### 8.1 Phase 1: The Departure (Tutorial)

- Large, stable, closely spaced platforms
- Teach: movement, jumping, checkpoints
- NPCs: polar bear with hints, a few penguins
- Pacing: very easy, almost impossible to fail
- 2-3 checkpoints
- Horizontal scrolling, short phase (~2 minutes)

**Files to create/update:**
- `samples/ArcticCrossingSample/Levels/Phase1Data.cs`

### 8.2 Phase 2: Open Water

- Moving platforms introduced (left/right drift)
- Wider gaps between platforms
- First crumbling platform ("the moment" -- must feel surprising but fair)
- NPCs: penguins on platforms, one seal pops up
- 3-4 checkpoints
- Moderate length (~3 minutes)

**Files to create/update:**
- `samples/ArcticCrossingSample/Levels/Phase2Data.cs`

### 8.3 Phase 3: The Ice Field

- Dense cluster of small platforms, multiple paths
- Wider path = easier but longer; narrow path = short but risky
- Wind gusts: periodic horizontal force applied to player (visual: horizontal line particles before gust)
- NPCs: penguin belly-slide surprise, unicorn on a hard-to-reach platform
- 3-4 checkpoints

**Wind gust implementation:** Timer-based event that applies horizontal velocity to the player for ~1 second. Telegraphed by visual lines appearing ~1 second before. Create a `WindGust` class or integrate into GameplayScreen.

**Files to create/update:**
- `samples/ArcticCrossingSample/Levels/Phase3Data.cs`
- `samples/ArcticCrossingSample/Entities/WindGust.cs` (or a simpler approach)

### 8.4 Phase 4: The Storm

- Background darkens (darker blue/gray sky color)
- Snowfall particles increase in density and speed
- Platforms move faster, crumbling timers are shorter
- Tilting platforms introduced
- Falling icicle obstacles: telegraphed by a flash, then a thin triangle drops from above. Collision damages player (lose a life).
- NPCs: polar bears at checkpoints, seals everywhere
- 4-5 checkpoints, brief calm sections between intense segments

**Files to create/update:**
- `samples/ArcticCrossingSample/Levels/Phase4Data.cs`
- `samples/ArcticCrossingSample/Entities/Icicle.cs`

### 8.5 Phase 5: The Mountain Base (Finale)

- Vertical scrolling / upward progression (camera follows player upward instead of rightward)
- Mix of rocky ledges (brown/gray rectangles) and ice platforms
- One-shot falling platforms used for vertical climbing
- Alternating calm and intense sections
- NPCs: all animals appear near the summit for celebration scene
- Final checkpoint just before the summit
- Summit arrival triggers win screen

**Camera note:** This phase switches from horizontal to vertical camera following. The `camera` skill should clarify how to change the camera follow axis.

**Files to create/update:**
- `samples/ArcticCrossingSample/Levels/Phase5Data.cs`

**Build and run checkpoint.** All 5 phases are playable from Phase Select. Each introduces its unique mechanics and NPCs.

---

## Phase 9 -- Polish and Effects

**Goal:** The game feels finished and satisfying.

**Depends on:** Phase 8

### 9.1 Snowflake particles

Background snowflakes drifting down:

- Small white circles or dots at random positions
- Slow drift downward with slight horizontal wobble
- Density increases in Phase 4 (The Storm)
- Purely cosmetic, no collision

### 9.2 Water splash effect

When player falls into the water:

- Brief burst of blue circle/rectangle particles upward from the splash point
- Could also show a white "splash" shape briefly

### 9.3 Checkpoint activation effect

When player touches a checkpoint:

- Flag color changes (e.g. gray to green)
- Brief flash or pulse effect
- Placeholder for a chime sound

### 9.4 Mountain background

The mountain visible in the background, growing larger with each phase:

- Simple triangle/polygon shape, positioned far right or center-top
- Scale/size increases per phase (small in Phase 1, fills background in Phase 5)
- Parallax effect: mountain moves slower than foreground platforms as camera scrolls

### 9.5 Win/celebration screen

When player reaches the summit in Phase 5:

- All NPC animals appear in a celebration arrangement
- Shapes bounce/pulse
- "Congratulations!" text
- Display total score
- Option to return to title screen

**Files to create:**
- `samples/ArcticCrossingSample/Screens/WinScreen.cs`

### 9.6 Sound effect placeholders

Add `AudioManager` calls (see `audio` skill) at key moments. If AudioManager is stubbed, leave the calls in place as placeholders:

- Jump sound
- Landing sound
- Splash (death)
- Checkpoint chime
- Collectible pickup
- Phase complete
- Background music per phase

### 9.7 Phase transition polish

- Fade-to-black between phases
- Phase name card displayed briefly before gameplay starts
- Mountain progress visual on transition

**Build and run checkpoint.** Complete, polished game with all 5 phases, effects, and win condition.

---

## Risks and Dependencies

| Risk | Impact | Mitigation |
|---|---|---|
| Multi-shape character (body from multiple shapes) may have positioning complexity | Phase 2 | All child shapes are relative to entity position via `Add()`. Test early that they move together correctly. |
| Moving platform carrying the player | Phase 3 | Classic platformer problem. May need to manually offset player position by platform delta each frame, or rely on collision response. Test with `PlatformerBehavior` to see if it handles this. |
| Tilting platforms with polygon collision | Phase 3 | `AxisAlignedRectangle` cannot rotate. Must use `Polygon` for tilting platforms. Verify `PlatformerBehavior` grounding works with polygon collision. |
| Phase 5 vertical scrolling camera change | Phase 8 | The camera system may need different follow behavior for vertical vs horizontal. Plan a camera mode switch in GameplayScreen. |
| Wind gusts applying force to player mid-air | Phase 8 | Need to add external velocity to player without conflicting with `PlatformerBehavior`. May need to add velocity directly or use acceleration. |
| Gum UI availability (HUD, menus) | Phase 6-7 | If Gum integration is complex, fall back to positioned shape entities and text for all UI. Menus can work with simple shapes. |
| Audio is stubbed in the engine | Phase 9 | Leave audio calls as placeholders. The game is visual-only until AudioManager is implemented. |

---

## Skill References

Tasks should reference these skills when implementing:

- **Project setup:** `sample-project-setup`
- **Player and NPC entities:** `entities-and-factories`
- **Shapes (all visuals):** `shapes`
- **Platformer movement:** reference `PlatformerSample` code + `physics-and-movement` skill
- **Collision (player vs platforms, NPCs, checkpoints):** `collision-relationships`
- **Input (keyboard controls):** `input-system`
- **Screens (title, gameplay, phase select, win):** `screens`
- **Timing (crumble timers, wind gusts, invincibility, phase indicator fade):** `timing`
- **Camera (follow player, horizontal vs vertical, parallax):** `camera`
- **UI/HUD (lives, score, menus):** `gum-integration` (ask about `gumcli` first)
- **Audio (placeholder calls):** `audio`
- **Level data layout:** `levels`

---

## Implementation Order Summary

```
Phase 1 (setup):       1.1 -> 1.2
Phase 2 (player):      2.1 -> 2.2 -> 2.3 -> 2.4
Phase 3 (platforms):   3.1 -> 3.2 -> 3.3 -> 3.4 -> 3.5 -> 3.6
Phase 4 (levels infra): 4.1 -> 4.2 -> 4.3 -> 4.4 -> 4.5 -> 4.6 -> 4.7
Phase 5 (NPCs):        5.1 -> 5.2 -> 5.3 -> 5.4 -> 5.5
Phase 6 (menus):       6.1 -> 6.2 -> 6.3 -> 6.4
Phase 7 (HUD):         7.1 -> 7.2 -> 7.3
Phase 8 (levels):      8.1 -> 8.2 -> 8.3 -> 8.4 -> 8.5
Phase 9 (polish):      9.1 -> 9.2 -> 9.3 -> 9.4 -> 9.5 -> 9.6 -> 9.7
```

**Phase dependencies:**
- Phase 2 requires Phase 1
- Phase 3 requires Phase 2
- Phase 4 requires Phases 2 and 3
- Phase 5 requires Phases 3 and 4
- Phase 6 requires Phases 1 and 4 (GameState)
- Phase 7 requires Phases 4 and 6
- Phase 8 requires all previous phases
- Phase 9 requires Phase 8

Each phase ends with a build-and-run checkpoint. The game should compile and be playable at the end of every phase.

**Hand off to coder agent for implementation, starting with Phase 1.**
