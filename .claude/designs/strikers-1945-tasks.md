# Strikers 1945 -- Implementation Task Breakdown

> Source GDD: `.claude/designs/strikers-1945-design.md`
> Sample project: `samples/Strikers1945Sample/`
> Assets: `e:\ai_assets\kenney\2D assets\Pixel Shmup\`

---

## Phase 1 -- Minimum Playable Core

**Goal:** Player ship on screen, movement, shooting, bullets fly. The game compiles and runs.

### 1.1 Project scaffolding

Create `samples/Strikers1945Sample/` following the `sample-project-setup` skill exactly:

- `Strikers1945Sample.csproj` (copy structure from `ShmupSample.csproj`, change namespace)
- `.config/dotnet-tools.json` (copy from `ShmupSample/.config/`)
- `Program.cs` -- boot into `Game1`
- `Game1.cs` -- 480x720 portrait window (vertical shmup), initialize FlatRedBallService, start `GameplayScreen`
- Run `dotnet tool restore` then `dotnet build` to verify clean compile

**Window size note:** Strikers 1945 is portrait-oriented. Use 480x720 or 540x800. The narrow width creates the dense bullet-dodging feel.

**Files to create:**
- `samples/Strikers1945Sample/Strikers1945Sample.csproj`
- `samples/Strikers1945Sample/.config/dotnet-tools.json`
- `samples/Strikers1945Sample/Program.cs`
- `samples/Strikers1945Sample/Game1.cs`
- `samples/Strikers1945Sample/Screens/GameplayScreen.cs` (empty shell)

### 1.2 Load player ship sprite via content pipeline

Add the MonoGame content pipeline (.mgcb) file. Import `ship_0000.png` (P-38 Lightning) as the initial player sprite.

**Key detail:** Ship sprites are 32x32 pixels. Scale 2-3x at render time so they read well at 480x720.

Reference the `content-and-assets` skill for how FlatRedBall2 loads textures.

**Files to create:**
- `samples/Strikers1945Sample/Content/Content.mgcb`

**Assets to import:**
- `Ships/ship_0000.png` (player ship)
- `Tiles/tile_0000.png` through `tile_0003.png` (bullet sprites)

### 1.3 Player entity with sprite and movement

Create a `PlayerShip` entity (see `entities-and-factories` skill). Key behaviors:

- Uses a `Sprite` displaying the loaded ship texture instead of shape primitives
- Keyboard input for movement: WASD or arrow keys (see `input-system` skill)
- **Immediate movement -- no acceleration ramp.** Unlike the existing ShmupSample which uses acceleration/drag, Strikers 1945 movement is instant. Set velocity directly from input direction * speed. Zero velocity on release. Speed ~350 pixels/sec (cross 480px screen in ~1.3 seconds).
- Clamp position to screen bounds
- Collision shape: `AxisAlignedRectangle` or `Circle` sized smaller than the sprite (shmup hitbox convention -- roughly 6x6 pixel hitbox)

**Files to create:**
- `samples/Strikers1945Sample/Entities/PlayerShip.cs`

### 1.4 Player bullet entity and firing

Create a `PlayerBullet` entity:

- Sprite using `tile_0000.png` (yellow bullet bar)
- Moves upward at high speed (~700 px/sec)
- Destroyed when off-screen (check Y > camera top + margin)

PlayerShip fires bullets on Z/Space held. Fire rate: ~0.10s cooldown. Initial pattern: single centered bullet stream. This is power level 1 for the P-38 (twin streams come at level 2+, but start simple).

**Files to create:**
- `samples/Strikers1945Sample/Entities/PlayerBullet.cs`

### 1.5 Wire up GameplayScreen

Set up `GameplayScreen` with:

- `Factory<PlayerShip>` and `Factory<PlayerBullet>`
- Spawn player at bottom center
- Dark background color
- Verify: player moves, shoots, bullets fly upward and despawn

**Build and run checkpoint.** The game should compile and be playable at this point: a ship that moves and shoots into empty space.

---

## Phase 2 -- Enemies, Collision, Scrolling Background

**Goal:** Things to shoot, things that shoot back, something to look at behind the action.

### 2.1 Scrolling background

Create a `ScrollingBackground` class that tiles 16x16 terrain tiles vertically. For Phase 2, use water/ocean tiles (`tile_0072.png` through `tile_0083.png`) to create a repeating ocean background that scrolls downward.

Approach: Render a grid of tile sprites that covers the screen plus one extra row. Scroll all tiles downward. When a row scrolls off the bottom, recycle it to the top. Scroll speed ~60-80 px/sec (leisurely scroll, not distracting).

**Files to create:**
- `samples/Strikers1945Sample/Screens/ScrollingBackground.cs`

**Assets to import into Content.mgcb:**
- `Tiles/tile_0072.png` through `tile_0083.png` (water tiles -- pick 3-4 for variety)

### 2.2 Fodder enemy entity

Create `FodderEnemy` entity (see `entities-and-factories`):

- Sprite using `ship_0008.png` (rendered rotated 180 degrees -- facing down)
- Follows waypoint paths (same pattern as existing ShmupSample `FodderEnemy`)
- 1 HP
- Collision shape (Circle, ~12px radius)
- Fires `Escaped` event if it completes path without dying
- Drops nothing initially (pickups come in Phase 3)

**Files to create:**
- `samples/Strikers1945Sample/Entities/FodderEnemy.cs`

**Assets to import:**
- `Ships/ship_0008.png` through `ship_0011.png` (fodder sprites -- use one initially, vary later)

### 2.3 Shooter enemy entity

Create `ShooterEnemy` entity:

- Sprite using `ship_0012.png` (rotated 180)
- Enters from top, descends to a hold position, fires aimed single bullets at the player, then exits downward
- 2-3 HP, takes multiple hits
- Needs access to the player position for aiming

**Files to create:**
- `samples/Strikers1945Sample/Entities/ShooterEnemy.cs`

### 2.4 Enemy bullet entity

Create `EnemyBullet` entity:

- Sprite using `tile_0002.png` or `tile_0003.png` (different color from player bullets for readability)
- Moves in a set direction at a set speed (direction assigned at spawn time by the shooter)
- Destroyed when off-screen

**Files to create:**
- `samples/Strikers1945Sample/Entities/EnemyBullet.cs`

### 2.5 Collision relationships

Wire up in `GameplayScreen` (see `collision-relationships` skill):

- PlayerBullet vs FodderEnemy -- bullet destroyed, enemy destroyed, spawn death particles
- PlayerBullet vs ShooterEnemy -- bullet destroyed, enemy takes 1 damage
- EnemyBullet vs PlayerShip -- bullet destroyed, player loses a life (Phase 4 adds lives; for now, respawn in place with brief invincibility)
- FodderEnemy vs PlayerShip -- enemy destroyed, player takes hit
- ShooterEnemy vs PlayerShip -- both take damage

### 2.6 Death particles

Create `DeathParticle` entity (same pattern as ShmupSample): small colored shapes that scatter on enemy death. Brief lifetime, velocity-based scatter.

**Files to create:**
- `samples/Strikers1945Sample/Entities/DeathParticle.cs`

### 2.7 Wave spawner

Create `WaveSpawner` (same architecture as ShmupSample):

- Define 6-8 wave patterns mixing fodder and shooters
- Overlapping waves (Strikers pacing -- new wave starts before previous clears)
- Drip-spawn fodder along paths
- Use shorter wave breath duration than ShmupSample (~2.5s vs 4.0s) for faster pacing

**Files to create:**
- `samples/Strikers1945Sample/Screens/WaveSpawner.cs`

### 2.8 Score tracking

Add basic score tracking to GameplayScreen:

- Score variable, displayed as text (Gum Label, top of screen)
- Points awarded per GDD: fodder 100-200, shooters 300-500
- No multiplier system yet (that is specific to medal scoring in Phase 3)

**Build and run checkpoint.** Enemies appear in waves, player shoots them, score increases, death particles fly. Enemy bullets threaten the player.

---

## Phase 3 -- Power-ups, Charge Shots, Weapon Levels

**Goal:** The signature Strikers 1945 mechanics. The game starts feeling distinctive.

### 3.1 Weapon power level system

Add to `PlayerShip`:

- `WeaponLevel` property (1-4)
- Shot pattern changes per level. For the P-38 Lightning (the first playable plane):
  - Level 1: single centered stream
  - Level 2: twin parallel streams (the classic P-38 look)
  - Level 3: twin streams + narrow center stream (3 total)
  - Level 4: twin wide + twin narrow + center (5 streams, visually dense)
- Dying resets to level 1

### 3.2 Pickup entities

Create a `Pickup` entity with a `PickupType` enum (Power, Bomb, Medal):

- Sprite: Power uses `tile_0006.png`, Bomb uses `tile_0004.png`, Medal uses `tile_0007.png`
- Drifts downward slowly (~40 px/sec)
- Destroyed when off-screen or collected
- Collision with player triggers effect:
  - Power: increase `WeaponLevel` (if at max, award 1000 score)
  - Bomb: add super attack stock (Phase 4)
  - Medal: award score based on Y position (top 25% = 2000, middle 50% = 1000, bottom 25% = 200)

**Files to create:**
- `samples/Strikers1945Sample/Entities/Pickup.cs`

**Assets to import:**
- `Tiles/tile_0004.png`, `tile_0006.png`, `tile_0007.png`

### 3.3 Enemy drop tables

Modify enemy destruction logic:

- Fodder: small chance (~15%) to drop a Medal
- Shooters: ~25% chance to drop a Medal, ~10% chance to drop a Power
- Heavy enemies (Phase 2.x or add here): guaranteed Power or Bomb drop
- Formation clear bonus: if all enemies in a wave are destroyed before any escape, drop a bonus Medal

### 3.4 Charge shot system

This is the core Strikers 1945 mechanic. Add to `PlayerShip`:

- While fire button is held, a charge timer fills over ~1.5 seconds
- Normal shots continue firing while charging (hold fire = shoot AND charge simultaneously)
- Visual feedback: player sprite flashes/pulses faster as charge builds. At full charge, a distinct glow or aura.
- **On fire button release:** if charge is full, fire the charge attack. If not full, nothing special happens.
- P-38 charge attack (Fork Lightning): spawn two bullet streams at wide angles that converge. Use a `ChargeProjectile` entity that moves in an arc pattern.

**Files to create:**
- `samples/Strikers1945Sample/Entities/ChargeProjectile.cs`

### 3.5 Medal scoring HUD

Update HUD to show:

- Score (large, top center -- Strikers style)
- Weapon level indicator
- Bomb stock count (placeholder for Phase 4)

**Build and run checkpoint.** Player can power up by collecting drops, charge shot works with visual feedback, medals reward aggressive positioning.

---

## Phase 4 -- Bosses, Level Progression, Lives System

**Goal:** A complete game loop -- levels with beginnings and endings, bosses, stakes.

### 4.1 Lives system

Replace the current "take a hit and keep going" with proper Strikers 1945 lives:

- 3 lives, each hit kills (one-hit death)
- On death: explosion effect, 1-second pause, respawn at bottom center with 2 seconds of invincibility (flashing sprite)
- Dying resets weapon level to 1, removes 1 bomb stock
- 0 lives remaining = game over

### 4.2 Super attack system

Add bomb/super attack mechanic:

- Starts with 2 super stocks, max 3
- Super button: X key or gamepad button
- P-38 super (Bombing Run): for MVP, a screen-clearing flash that damages all enemies and clears all enemy bullets. Full visual spectacle can be iterated.
- Brief invincibility during super activation (~1 second)

### 4.3 Heavy enemy entity

Create `HeavyEnemy` entity:

- Sprite using `ship_0016.png` or `ship_0017.png` (rotated 180)
- Slow, tanky (8-12 HP)
- Fires spread patterns (3-5 bullets in a fan)
- Guaranteed drop (Power or Bomb) on death

**Files to create:**
- `samples/Strikers1945Sample/Entities/HeavyEnemy.cs`

### 4.4 Dive bomber enemy

Create `DiveBomberEnemy` entity:

- Sprite using `ship_0014.png` or `ship_0015.png` (rotated 180)
- Swoops in an arc, fires a burst at the bottom of the arc, flies off-screen
- 3-4 HP

**Files to create:**
- `samples/Strikers1945Sample/Entities/DiveBomberEnemy.cs`

### 4.5 Boss entity framework

Create a `Boss` base class or a single `BossEntity` class with phase support:

- Large sprite (ship_0020.png scaled 3x for Level 1 boss)
- Health bar displayed below the boss
- Two phases: Phase 1 (military vehicle) and Phase 2 (transformed)
- At 50% HP: transformation sequence (sprite flash, brief screen flash, swap to phase 2 sprite arrangement and attack pattern)
- Phase 1 attacks: aimed triple shots at the player
- Phase 2 attacks: sweeping bullet fans, faster firing
- Targetable components (optional for MVP -- can be a stretch goal)
- Award 10000 per phase, 20000 bonus for full destruction

**Files to create:**
- `samples/Strikers1945Sample/Entities/Boss.cs`

**Assets to import:**
- `Ships/ship_0020.png` through `ship_0023.png` (boss sprites)

### 4.6 Level progression system

Create a `LevelManager` or extend `WaveSpawner` into a `LevelDefinition` system:

- Each level is a sequence of waves ending with a boss
- 5 levels defined per the GDD, each 2-4 minutes
- Mid-boss encounter at the midpoint of each level (use `ship_0018.png` or `ship_0019.png`)
- Level complete when boss is destroyed
- Brief transition between levels (screen fade or text overlay: "Level 2 -- European Countryside")
- Background tiles change per level (water -> grass -> desert -> runway -> mixed)

**Architecture:** Could be a `LevelData` class holding wave definitions and boss config, with `GameplayScreen` loading the current level's data. Or separate screen instances per level -- follow whichever pattern feels cleaner with the engine's screen system (see `screens` skill).

**Files to create:**
- `samples/Strikers1945Sample/Screens/LevelDefinition.cs` (or similar)

**Assets to import per level:**
- Level 1: water tiles (already imported)
- Level 2: grass/forest tiles (`tile_0024` - `tile_0047`)
- Level 3: desert tiles (`tile_0048` - `tile_0071`)
- Level 4: runway/base tiles (`tile_0096` - `tile_0119`)
- Level 5: mixed

### 4.7 Game over and continue

Create `GameOverScreen`:

- Display final score
- "Continue?" option (restarts current level, resets score)
- "Title Screen" option (returns to title -- built in Phase 5)

**Files to create:**
- `samples/Strikers1945Sample/Screens/GameOverScreen.cs`

**Build and run checkpoint.** A full level 1 is playable: waves of enemies, mid-boss, end boss with transformation, level complete. Death has consequences. The game has stakes.

---

## Phase 5 -- Plane Select, UI, Title Screen, Polish

**Goal:** The game feels complete. Multiple planes, menus, visual polish.

### 5.1 Plane data architecture

Create a `PlaneData` class or record that defines each plane's properties:

- Sprite asset reference
- Normal shot pattern per weapon level (1-4)
- Charge attack type/behavior
- Super attack type/behavior
- Display name

Define 4 planes per the GDD:
- P-38 Lightning (`ship_0000.png`): twin vulcan, Fork Lightning charge, Bombing Run super
- Spitfire (`ship_0001.png`): focused triple, Piercing Lance charge, Firestorm super
- Mosquito (`ship_0002.png`): wide spread, Homing Salvo charge, Carpet Bomb super
- Zero (`ship_0003.png`): rapid vulcan, Blade Wave charge, Divine Wind super

**Files to create:**
- `samples/Strikers1945Sample/Entities/PlaneData.cs`

**Assets to import:**
- `Ships/ship_0001.png` through `ship_0003.png`

### 5.2 Implement unique shot patterns

For each of the 4 planes, implement:

- 4 weapon level shot patterns (increasingly dense/wide)
- Charge attack projectile behavior (each is unique -- homing missiles, piercing beam, crescent wave, converging streams)
- Super attack effect (screen clear variants with different visuals)

This is the largest single implementation task. Each plane's charge attack and super are effectively mini-features.

### 5.3 Plane select screen

Create `PlaneSelectScreen`:

- Display all 4 planes with their sprites and names
- Arrow keys or D-pad to highlight a plane
- Show brief description of shot pattern and charge attack
- Confirm selection starts the game with that plane
- Passes selected `PlaneData` to `GameplayScreen`

**Files to create:**
- `samples/Strikers1945Sample/Screens/PlaneSelectScreen.cs`

### 5.4 Title screen

Create `TitleScreen`:

- Game title text
- "Press Start" / "Press Enter"
- High score display
- Transitions to `PlaneSelectScreen` on confirm

**Files to create:**
- `samples/Strikers1945Sample/Screens/TitleScreen.cs`

### 5.5 HUD polish

Finalize HUD using Gum UI (see `gum-integration` skill):

- Large score display, top center
- Lives indicator (ship icons, top left)
- Bomb stock (bomb icons, top left below lives)
- Weapon level meter
- Boss health bar (appears during boss fights)

### 5.6 Visual polish

- Player invincibility flashing after respawn
- Charge-up glow effect (sprite color modulation or overlay)
- Boss transformation visual sequence (flash, break apart, reassemble)
- Screen shake on boss hits and super attacks
- Explosion sprite effects using `tile_0008.png` / `tile_0009.png`

### 5.7 Remaining bosses (levels 2-5)

Implement boss encounters for levels 2-5 per the GDD:

- Level 2: Flying Fortress -> Spinning Fortress (spiral bullets, homing missiles)
- Level 3: Giant Bomber -> Mech Walker (horizontal bullet lines, arm cannons)
- Level 4: Stealth Fighter -> Twin Drones (two targets, crossing streams)
- Level 5: Super Fortress -> Final Mech (4 targetable limbs + core)

Each boss needs its own attack patterns. This is significant work -- 4 boss fights with 2 phases each.

### 5.8 Difficulty tuning

- Level-by-level enemy count and density escalation
- Bullet speed increases per level
- Wave overlap timing per level
- Ensure level 1 is approachable, level 5 requires near-perfect play

**Build and run checkpoint.** The complete game: title screen, plane select, 5 levels with distinct backgrounds, 4 playable planes with unique mechanics, bosses with transformations, lives, scoring, game over.

---

## Risks and Dependencies

| Risk | Impact | Mitigation |
|---|---|---|
| Sprite loading via content pipeline may need iteration | Blocks Phase 1 | Start with shape primitives as fallback (like ShmupSample), swap to sprites once pipeline works |
| Portrait window (480x720) may interact oddly with camera system | Blocks Phase 1 | Test early. The camera skill explains Y-up coordinate system. Verify coordinate math works at non-standard aspect ratio |
| Charge shot "hold to charge while also firing" is unusual input | Blocks Phase 3 | Prototype early -- track fire-button hold duration alongside fire cooldown. These are independent timers on the same button |
| Boss transformation compositing multiple sprites | Complicates Phase 4 | Start with simple sprite swap (flash, swap texture). Multi-sprite composite form is a polish item |
| 4 unique plane shot patterns x 4 weapon levels = 16 distinct patterns | Phase 5 scope risk | Implement P-38 fully first. Other planes can start with simpler variations and be refined |
| Scrolling background tile rendering performance | Phase 2 | Keep tile count small. Recycle off-screen tiles rather than creating/destroying |

---

## Skill References

Tasks should reference these skills when implementing:

- **Project setup:** `sample-project-setup`
- **Entities (PlayerShip, enemies, bullets, pickups):** `entities-and-factories`
- **Collision wiring:** `collision-relationships`
- **Movement (player, bullets, enemies):** `physics-and-movement`
- **Input (keyboard, fire button, charge detection):** `input-system`
- **Screens (gameplay, title, plane select, game over):** `screens`
- **Timing (fire cooldown, charge timer, invincibility duration):** `timing`
- **Asset loading (sprites, textures via .mgcb):** `content-and-assets`
- **UI/HUD (score, lives, boss health bar):** `gum-integration`
- **Camera (portrait aspect ratio, coordinate system):** `camera`

---

## Implementation Order Summary

```
Phase 1 (core):      1.1 -> 1.2 -> 1.3 -> 1.4 -> 1.5
Phase 2 (enemies):   2.1 -> 2.2 -> 2.4 -> 2.5 -> 2.3 -> 2.6 -> 2.7 -> 2.8
Phase 3 (mechanics): 3.1 -> 3.2 -> 3.3 -> 3.4 -> 3.5
Phase 4 (bosses):    4.1 -> 4.2 -> 4.3 -> 4.4 -> 4.5 -> 4.6 -> 4.7
Phase 5 (polish):    5.1 -> 5.2 -> 5.3 -> 5.4 -> 5.5 -> 5.6 -> 5.7 -> 5.8
```

Each phase ends with a build-and-run checkpoint. The game should compile and be playable at the end of every phase.

**Hand off to coder agent for implementation, starting with Phase 1.**
