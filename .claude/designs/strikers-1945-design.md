# Strikers 1945 — Game Design Document

> This GDD is complete enough to begin implementation.

## One-Sentence Pitch
A fast, aggressive WW2 vertical-scrolling shmup where the player selects from multiple fighter planes -- each with unique shot patterns and devastating charge attacks -- and battles through waves of enemy aircraft and transforming bosses across a multi-level campaign.

---

## Player Experience Goals

- The player should feel like an ace pilot with a distinctive fighting style, chosen at the plane select screen and expressed through every shot fired.
- The charge shot system should create a constant tactical rhythm: tap-fire for control, hold for power, release at the perfect moment for maximum devastation.
- Boss transformations should be genuine "what the hell" moments -- a large bomber splits apart and reassembles into a mech, and the fight changes completely.
- The pacing should be relentless but readable. The screen is always busy, but the player can always find the path through.
- Medal chasing should create a secondary layer of engagement for skilled players without distracting newcomers.
- Death should sting -- you lose your power level -- but the game gives you tools to claw back quickly.

---

## Tone and Mood

**Arcade military spectacle.** Bold, colorful, fast. This is not a simulation -- it is a quarter-munching arcade game that happens to feature WW2 planes. The tone is confident and energetic.

**Visual feel:** Crisp pixel art from the Kenney Pixel Shmup pack. Colorful player planes against scrolling terrain -- green islands, desert expanses, ocean water, runways and airfields. The screen should feel alive and dense without being unreadable.

**Audio feel:** Audio is currently stubbed in FlatRedBall2. When available: rapid-fire gun sounds, satisfying explosion chains, a charge-up whine that builds tension, a release boom for the charge attack, and a triumphant pickup chime for medals. Deferred until audio is implemented.

---

## Asset Mapping (Kenney Pixel Shmup Pack)

All assets are located at `e:\ai_assets\kenney\2D assets\Pixel Shmup\`.

Ship sprites are 32x32 pixels. Tile sprites are 16x16 pixels.

### Player Planes (Selectable)

From the preview image, the top two rows contain colorful small planes suitable for players. The remaining rows are larger grey/dark military planes suitable for enemies and bosses.

| Plane Name | Sprite | Description (from preview) |
|---|---|---|
| **P-38 Lightning** | `Ships/ship_0000.png` | Red twin-boom fighter, top-left of preview |
| **Spitfire** | `Ships/ship_0001.png` | Yellow/orange fighter |
| **Mosquito** | `Ships/ship_0002.png` | Green fighter |
| **Zero** | `Ships/ship_0003.png` | Orange/yellow compact fighter |
| **P-51 Mustang** | `Ships/ship_0004.png` | Blue/cyan twin-engine |
| **Corsair** | `Ships/ship_0005.png` | Red compact fighter |
| **Focke-Wulf** | `Ships/ship_0006.png` | Green/lime fighter |
| **Shinden** | `Ships/ship_0007.png` | Yellow/orange with distinct wing shape |

### Enemy Planes

| Role | Sprite(s) | Notes |
|---|---|---|
| **Fodder (small)** | `Ships/ship_0008.png` through `ship_0011.png` | Small colorful planes from row 3 of preview -- render rotated 180 degrees (facing down) |
| **Fighter (medium)** | `Ships/ship_0012.png` through `ship_0015.png` | Grey medium military planes from row 4 |
| **Heavy bomber** | `Ships/ship_0016.png` through `ship_0019.png` | Large grey twin-engine planes from row 5 |
| **Boss base forms** | `Ships/ship_0020.png` through `ship_0023.png` | Largest grey/dark planes from row 6 -- used as boss first phase |

### Tiles (16x16)

From the tile preview, organized by row in the tilesheet (12 columns x 10 rows):

| Tile Range | Content | Game Use |
|---|---|---|
| `tile_0000.png` - `tile_0003.png` | Bullet sprites (thin vertical bars, yellow/orange) | Player bullets, enemy bullets |
| `tile_0004.png` - `tile_0005.png` | Bomb/missile sprites | Charge shot projectile, bomb visual |
| `tile_0006.png` - `tile_0007.png` | Power-up star / medal shapes | Medal pickups |
| `tile_0008.png` - `tile_0009.png` | Explosion / impact sprites | Hit effects |
| `tile_0010.png` - `tile_0011.png` | Crosshair / marker sprites | UI indicators |
| `tile_0012.png` - `tile_0023.png` | Numbers 0-9, period, special chars | Score display, HUD numbers |
| `tile_0024.png` - `tile_0035.png` | Grass terrain tiles (edges, corners, fills) | Level backgrounds: inland, airfield |
| `tile_0036.png` - `tile_0047.png` | More grass/forest tiles, trees | Level backgrounds: forest areas |
| `tile_0048.png` - `tile_0059.png` | Desert/sand terrain tiles | Level backgrounds: desert level |
| `tile_0060.png` - `tile_0071.png` | Desert structures, roads | Level backgrounds: desert structures |
| `tile_0072.png` - `tile_0083.png` | Water/ocean terrain tiles | Level backgrounds: ocean, coastal |
| `tile_0084.png` - `tile_0095.png` | Water edges, ice/snow tiles | Level backgrounds: arctic/water |
| `tile_0096.png` - `tile_0107.png` | Runway/airfield tiles, buildings | Level backgrounds: enemy base |
| `tile_0108.png` - `tile_0119.png` | More structures, fences, special tiles | Level backgrounds: military base details |

---

## Plane Select System

Before each game (or on continue), the player chooses one of **4 planes** (expandable to 8 using available sprites). Each plane has:
- A unique **normal shot pattern** (how bullets spread when tapping/holding fire)
- A unique **charge attack** (released after holding fire for ~1.5 seconds)
- A unique **super attack** (bomb equivalent, limited stock)
- Identical movement speed and health (balance through firepower, not stats)

### The Four Starting Planes

| Plane | Sprite | Normal Shot | Charge Attack | Super Attack |
|---|---|---|---|---|
| **P-38 Lightning** | `ship_0000.png` | Twin vulcan -- two parallel bullet streams | **Fork Lightning:** Two wide-angle streams that sweep inward, converging on targets | **Bombing Run:** A formation of small planes flies across the screen dealing damage to everything |
| **Spitfire** | `ship_0001.png` | Focused triple -- three bullets in a tight forward cluster | **Piercing Lance:** A single powerful beam that penetrates all enemies in a line | **Firestorm:** Ring of fire expands outward from the player, clearing bullets and damaging enemies |
| **Mosquito** | `ship_0002.png` | Wide spread -- five bullets in a fan pattern | **Homing Salvo:** Releases 6 homing missiles that seek the nearest enemies | **Carpet Bomb:** A line of explosions rolls up the screen from bottom to top |
| **Zero** | `ship_0003.png` | Rapid vulcan -- single stream, very high fire rate | **Blade Wave:** A large crescent projectile that sweeps across the screen width | **Divine Wind:** Player becomes invincible and deals contact damage for 3 seconds, then returns to normal |

### Charge Shot System

This is the signature mechanic that separates Strikers 1945 from simpler shmups.

- **Tap fire / hold fire:** Normal shot fires continuously. The player can hold the fire button and still shoot normally.
- **Charge gauge:** While the fire button is held, a charge gauge fills over approximately 1.5 seconds. Visually indicated by the player sprite flashing/glowing brighter as charge builds.
- **Release to fire charge attack:** Releasing the fire button when charged unleashes the charge attack. If released before full charge, nothing special happens -- just normal fire resumes.
- **Risk/reward:** Charging requires the player to commit to holding fire. The charge attack is powerful but demands timing -- releasing during a dense bullet pattern means you were not dodging with full attention.
- **Charge attacks cost nothing.** They recharge automatically. This means the player is always making tactical decisions about when to charge vs. when to tap-fire for safety.

---

## Core Loop

1. **Select your plane** -- choose your fighting style before the mission begins.
2. **Fly and shoot** -- the screen scrolls. Enemies pour in from the top, sides, and occasionally bottom. Hold fire for sustained damage, release charged shots for burst damage.
3. **Dodge** -- weave through enemy bullets and formations. No dedicated dodge button -- pure positional play (Strikers 1945 style). Speed and precision are the defense.
4. **Collect medals and power-ups** -- destroyed enemies and formations drop gold medals (score) and power-up items (weapon level).
5. **Power up** -- collecting P icons increases your weapon level (up to 4 levels). Each level adds more bullets/spread to your normal shot. Dying drops you back to level 1.
6. **Fight the boss** -- each level ends with a large boss that transforms mid-fight into a more dangerous second form.
7. **Advance** -- next level, faster enemies, denser patterns.

---

## Power-Up System

### Weapon Power Levels

The player's normal shot has 4 power levels. Collecting a "P" power-up icon advances one level. Dying resets to level 1.

| Level | Effect (applies to all planes) |
|---|---|
| **Level 1** | Base shot pattern (as described per plane) |
| **Level 2** | +1 bullet stream or wider spread |
| **Level 3** | +1 more stream, bullets deal more damage |
| **Level 4 (MAX)** | Full power -- dense spread, high damage, visually impressive |

At MAX power, collecting additional P icons awards bonus score instead.

### Pickup Types

| Pickup | Sprite Source | Effect |
|---|---|---|
| **P (Power)** | `tile_0006.png` or `tile_0007.png` (star/medal shape) | Increase weapon level by 1 |
| **B (Bomb)** | `tile_0004.png` or `tile_0005.png` (bomb shape) | Add 1 super attack stock (max 3) |
| **Gold Medal** | `tile_0007.png` (gold-colored sprite) | Score bonus -- value increases if collected at the top of the screen (2000 pts at top vs. 200 pts at bottom) |

### Medal Scoring System

This is the Strikers 1945 depth layer for skilled players:

- Certain enemies and all mid-bosses drop **gold medals** when destroyed.
- Medals drift downward. Their point value depends on **where on the screen the player collects them:**
  - Collected in the top 25% of the screen: **2000 points**
  - Collected in the middle 50%: **1000 points**
  - Collected in the bottom 25%: **200 points**
- This creates a risk/reward loop: aggressive players push to the top of the screen to grab medals at maximum value, but the top of the screen is where enemies spawn -- it is the most dangerous position.
- A skilled player weaving through spawning enemies to grab top-value medals is the Strikers 1945 equivalent of style points.

---

## Enemy Design

### Design Principles
- **Fast and aggressive.** Enemies arrive quickly and in volume. Strikers 1945 is denser than 1943 -- the player should always feel pressure.
- **Formations are opportunities.** Destroying a complete formation (before any enemy escapes) awards a bonus medal drop. This rewards aggression and pattern recognition.
- **Overlapping waves.** Unlike 1943's sequential waves, Strikers 1945 overlaps -- a new wave begins before the previous one is fully cleared. The player is always shooting.

### Enemy Types

| Type | Sprite | Behavior | HP |
|---|---|---|---|
| **Fodder** | `ship_0008.png` - `ship_0011.png` (rotated 180) | Fly in formations (V, line, arc), no shooting. Fast, fragile. | 1 hit |
| **Shooters** | `ship_0012.png` - `ship_0013.png` (rotated 180) | Fly in pairs or trios, fire aimed single bullets at the player. | 2-3 hits |
| **Dive Bombers** | `ship_0014.png` - `ship_0015.png` (rotated 180) | Swoop down in an arc, fire a burst at the bottom of their arc, then fly off-screen. | 3-4 hits |
| **Heavy Planes** | `ship_0016.png` - `ship_0017.png` (rotated 180) | Slow, tanky. Fire spread patterns. Drop guaranteed P or B pickup on death. | 8-12 hits |
| **Ground Turrets** | Built from tile sprites (structure tiles) | Stationary targets on scrolling terrain. Fire aimed bursts. Cannot be dodged by position -- must be destroyed. | 5-6 hits |
| **Mid-Boss** | `ship_0018.png` or `ship_0019.png` | Appears mid-level. Tough, fires complex patterns. Drops medals on death. Not a full boss -- more like a mini-encounter. | 20-30 hits |

---

## Boss Design

Bosses are the highlight. Each level ends with a boss that **transforms** mid-fight.

### Boss Principles
- **Phase 1: Military vehicle.** A large plane, tank, or warship. Attacks are aggressive but conventional -- aimed shots, spread patterns, bullet streams.
- **Transformation.** At 50% health, the boss breaks apart and reassembles into a more fantastical form -- a mech, a multi-segmented machine, a weapon platform. This is the signature Strikers 1945 moment. The transformation should be visually dramatic: parts fly apart, reassemble, the boss grows larger.
- **Phase 2: Transformed form.** New attack patterns, more dangerous, often with bullet patterns that require precise threading. The boss is also more vulnerable -- exposed weak points glow or flash.
- **Targetable components.** Turrets, wings, and weapon pods can be destroyed individually to reduce the boss's firepower. Destroying all components before the core awards a bonus.

### Boss Roster

| Level | Boss Phase 1 | Sprite | Boss Phase 2 (Transformed) | Attack Style |
|---|---|---|---|---|
| 1 | **Heavy Bomber** | `ship_0020.png` (scaled 3x) | Splits into a spread-wing mech form (composite of multiple sprites) | Phase 1: aimed triple shots. Phase 2: sweeping laser arms, bullet fans |
| 2 | **Flying Fortress** | `ship_0021.png` (scaled 3x) | Collapses into a compact spinning fortress | Phase 1: turret ring fire. Phase 2: spiral bullet patterns, homing missiles |
| 3 | **Giant Bomber** | `ship_0022.png` (scaled 3x) | Unfolds into a towering mech walker (tall composite) | Phase 1: carpet bomb drops. Phase 2: stomping shockwaves (horizontal bullet lines), arm cannons |
| 4 | **Stealth Fighter** | `ship_0023.png` (scaled 3x) | Splits into twin attack drones that coordinate | Phase 1: fast strafing runs. Phase 2: two targets, crossing bullet streams, must destroy both |
| 5 (Final) | **Super Fortress** | Composite of `ship_0020.png` + `ship_0021.png` parts (scaled 4x) | Transforms into a massive mech with 4 targetable limbs + core | Phase 1: everything at once. Phase 2: limbs attack independently, destroying each simplifies the fight, core exposed when all limbs down |

### Boss Transformation Visual

Since we are working with sprites rather than custom animation, the transformation effect should be achieved through:
1. The Phase 1 sprite flashes rapidly
2. Screen flash (brief white overlay)
3. Phase 1 sprite breaks into 4-6 pieces that fly outward
4. Pieces converge into new positions forming the Phase 2 arrangement
5. Phase 2 begins attacking immediately -- no mercy window

---

## Movement and Controls Feel

| Input | Action |
|---|---|
| Directional input (WASD / D-pad / stick) | Move ship |
| Fire button (tap) | Fire normal shot |
| Fire button (hold ~1.5s then release) | Charge shot -- normal fire continues while charging, release for charge attack |
| Super/bomb button | Deploy super attack (plane-specific, uses 1 stock) |

### Feel Targets
- **Movement:** Immediate. No acceleration ramp -- the ship moves at full speed the instant the player presses a direction, and stops the instant they release. Strikers 1945 movement is snappier than 1943. The player is a cursor with a plane on it.
- **Fire rate:** Fast. Normal shots should feel like a stream, not individual bullets. At max power, the screen should be full of the player's bullets.
- **Charge feedback:** The ship visually pulses or glows brighter as charge builds. At full charge, a distinct flash or aura signals "ready to release." This feedback must be readable even in a busy screen.
- **Super attacks:** Screen-filling spectacle. Brief invincibility during activation. The super should feel like the biggest thing happening on screen for its 2-3 second duration.

### Movement Speed
The player moves at a fixed speed (no speed power-ups). The speed should be fast enough to cross the screen in roughly 1 second -- fast enough to dodge dense patterns, slow enough that precision is still required.

---

## Level Structure and Progression

### 5 levels, each 2-4 minutes long

Shorter levels than 1943 -- Strikers 1945 pacing is faster and more intense. A full campaign run is 12-20 minutes.

| Level | Terrain Tiles | Setting | Mid-Boss | Boss |
|---|---|---|---|---|
| 1 | Water/ocean tiles (`tile_0072` - `tile_0083`) | Pacific Ocean -- open water with small islands | Heavy plane | Heavy Bomber -> Mech |
| 2 | Grass/forest tiles (`tile_0024` - `tile_0047`) | European countryside -- green fields, forests, rivers | Ground turret cluster | Flying Fortress -> Spinning Fortress |
| 3 | Desert tiles (`tile_0048` - `tile_0071`) | North Africa -- sand, roads, desert structures | Twin heavy planes | Giant Bomber -> Mech Walker |
| 4 | Runway/base tiles (`tile_0096` - `tile_0119`) | Enemy airfield -- runways, hangars, fences | Ace fighter (fast mid-boss) | Stealth Fighter -> Twin Drones |
| 5 | Mixed (all tile types) | Final assault -- terrain shifts from water to land to enemy fortress | All previous mid-boss types | Super Fortress -> Final Mech |

### Wave Pacing Within a Level
Strikers 1945 pacing is faster and more overlapping than 1943:
- **Waves overlap.** A new wave starts arriving before the last enemy of the previous wave is gone. The player is never not shooting.
- **Brief breathing moments** exist only before the mid-boss and before the end boss. These are 1-2 seconds max.
- **Density escalates** throughout the level. Early waves are 4-6 enemies. Late waves are 10-15 enemies with shooters mixed in.
- **Ground targets** (turrets, structures) are mixed into terrain sections, creating an additional threat layer during scrolling.

### Death and Continue
- The player has **3 lives** (Strikers 1945 style, not a health bar).
- Each hit kills the player. The plane explodes, and after a 1-second respawn delay, the player reappears at the bottom of the screen with brief invincibility (2 seconds).
- Dying resets weapon power to level 1 and removes 1 bomb stock.
- Losing all 3 lives ends the run. The player can continue (restarting the current level with 3 lives and score reset) or return to the title screen.
- Extra lives are not awarded during gameplay -- 3 lives is the budget for the entire campaign. This creates genuine tension.

---

## Score System

Score is prominent in Strikers 1945 -- it is displayed large at the top of the screen.

| Source | Points |
|---|---|
| Fodder enemy | 100-200 |
| Shooter enemy | 300-500 |
| Dive bomber | 400-600 |
| Heavy plane | 1000 |
| Ground turret | 500 |
| Mid-boss | 5000 |
| Boss (per phase) | 10000 |
| Boss (full destruction bonus) | 20000 |
| Formation clear bonus | 1000 per complete formation |
| Gold medal (top of screen) | 2000 |
| Gold medal (mid screen) | 1000 |
| Gold medal (bottom of screen) | 200 |
| Power-up at MAX power | 1000 |

### High Score
A local high score is tracked and displayed on the title screen. Beating the high score should feel like an event.

---

## Pacing

- **Session length:** 12-20 minutes for a full campaign. Individual levels are 2-4 minutes.
- **Intensity:** High and sustained. Strikers 1945 is noticeably more aggressive than 1943. The player should feel pushed from the start of level 2 onward.
- **Breathing room:** Minimal. Brief pauses before mid-boss and boss only. The rest of the level is continuous combat.
- **Difficulty curve:** Level 1 is approachable. By level 3, the player should be using charge attacks and supers tactically. Level 5 should require near-perfect play to survive on 3 lives.
- **Death penalty:** Harsh but recoverable. Losing power level stings, but skilled players can power back up within one level if they play aggressively. The 3-life limit creates genuine stakes for the full campaign.

---

## Scope

This is a **complete small game** -- a full vertical shmup campaign.

- 4 selectable player planes with unique shot patterns and charge attacks
- 5 levels with distinct scrolling terrain built from tile assets
- 5 enemy types + ground turrets
- 5 mid-boss encounters
- 5 end-level boss encounters with transformation phases
- Charge shot system
- 4-level weapon power-up progression
- Super attack system (per-plane)
- Medal scoring system with positional value
- Plane select screen
- Title screen with high score display
- 3-life system with continue option
- All visuals from Kenney Pixel Shmup pack -- no custom art required

---

## Moments to Design For

- **Plane select:** The player studies the four planes, picks one, and the game begins. Each plane should feel meaningfully different within 10 seconds of gameplay.
- **First charge release:** The player holds fire, sees the glow build, releases -- the charge attack tears through a formation. The "oh, I should be using this constantly" realization.
- **Top-screen medal grab:** A medal drops from a destroyed formation. The player darts to the top of the screen, grabs it at 2000 points, and barely dodges the next incoming wave. Risk rewarded.
- **Boss transformation:** The heavy bomber shudders, breaks apart -- pieces fly to new positions and lock together into a mech. The attack pattern changes completely. The fight just got real.
- **Super attack deployment:** The screen is dense with bullets. The player triggers their super -- for the Spitfire, a ring of fire expands outward, dissolving every bullet and damaging every enemy. Three seconds of breathing room earned.
- **Final boss, final phase:** All four limbs destroyed. The core is exposed, firing desperate spiral patterns. The player charges one last shot and releases it into the weak point. Victory.

---

## Out of Scope

- Co-op / multiplayer (single player only for initial version)
- Persistent meta-progression or unlocks between campaigns
- Procedural level generation (levels are hand-designed wave sequences)
- Story cutscenes or dialogue (brief mission briefing text between levels is acceptable)
- Online leaderboards
- Audio (deferred until FlatRedBall2 audio is implemented)
- More than 4 selectable planes in the initial version (expandable to 8 later using remaining sprites)
