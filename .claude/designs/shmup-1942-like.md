# Shmup — Game Design Document

> This GDD is complete enough to begin implementation.

## One-Sentence Pitch
An abstract geometric shoot 'em up where raw firepower and readable enemy patterns make every shot feel like it matters.

---

## Player Experience Goals

- Every bullet fired should *register* — the player must feel the impact of each shot, not just see numbers tick down.
- Pattern recognition should feel like a skill being exercised, not a puzzle being solved. The player reads the field, repositions, and punishes formations decisively.
- Firepower progression should feel like a reward for mastery — each upgrade amplifies an already-satisfying weapon, not a rescue from an unsatisfying one.
- By the end of a session, the player should feel like they went on a journey: escalating stakes, harder patterns, a sense of arrival at something climactic.

---

## Tone and Mood

Abstract and geometric — no sprites, no textures. Everything is rectangles and circles. Color carries all the expressive weight that art direction would carry in a traditional shmup.

This is not a cold, minimalist aesthetic. It should feel energetic and readable — think Geometry Wars: busy but never confusing, because the shapes and colors *are* the information. The visual language should make the player feel like they are commanding a force of nature through a hostile, vivid geometry.

---

## Core Feel Pillars

1. **Raw firepower** — Guns should feel good to fire from the very first second. Mashing or holding fire is satisfying, never frustrating.
2. **Visceral hit feedback** — Fast bullets, directional impact trails that peel off in the bullet's travel direction (glancing/piercing feel). Every hit registers.
3. **Readable patterns** — One or two things to track at a time. Formations are intentional and learnable. Never bullet hell.
4. **Earned escalation** — The player starts capable and grows into something formidable.
5. **Journey feel** — Levels are not just score stages. There is a sense of progression, escalation, and arrival.

---

## Player Ship

### Movement
Velocity-based with a small acceleration ramp. The ship does not teleport to cursor position — it accelerates up to a max speed, giving it a tiny sense of heft and mass. Stopping should also have a brief deceleration, not instant halt. The ship should feel nimble but not weightless — precision is possible, but you feel like you are piloting something.

### Health
The player has a health bar, not lives. Displayed as a Gum UI element. When health reaches zero, the run ends — no continues, no mid-level checkpoints. The player returns to the start. Taking a hit should feel meaningful but not immediately fatal; the health bar creates tension that builds across a level without being a one-strike death system.

### Starting Weapon
Dual guns — one firing from the left side of the ship, one from the right. Both fire simultaneously. Bullets travel fast. This is not a weak starting state; it is the floor of an escalating arsenal.

### Firepower Upgrade Path (ideas, not final)
Pickups dropped by enemies or end-of-wave bonuses. Upgrades persist across the full run — earned firepower carries into every subsequent level. Suggested progression direction:
- **Width**: Add additional gun positions (spread to 3, 4, 5 guns)
- **Rate**: Increase fire rate up to a satisfying ceiling
- **Power**: Bullets pierce or split on impact
- **Special**: A charged shot, bomb, or screen-clearing ability (one use, earned)

The upgrade path is not final — but the principle is: each upgrade should make the player feel demonstrably more powerful, not just incrementally better. Upgrades are a reward for survival and mastery, not a crutch.

---

## Enemy Design Philosophy

### Formations over chaos
Enemies arrive in readable formations — rows, V-shapes, flanking pairs. The player should be able to look at a formation and immediately know the threat and the opportunity.

### One or two things at a time
No more than two distinct threats on screen demanding simultaneous attention. A wave of enemies plus one shooter, not five shooter types converging from all angles. Cognitive load is kept low enough that the player can focus on *shooting well*, not just surviving.

### Learnable patterns
Enemy movement and firing patterns repeat with recognizable cues. A player on their second run should recognize a wave they saw before and know exactly what to do. Pattern recognition should feel like a skill that compounds.

### Enemy types (to be designed, but guiding principles)
- **Fodder**: Simple shapes (small rectangles or circles), fly in formation, no shooting. Exist to be shredded satisfyingly.
- **Shooters**: Slightly larger, distinct color, fire single slow projectiles on a pattern. Require repositioning.
- **Heavies**: Large rectangles, take multiple hits, move slowly. Feel like a target worth focusing.
- **Boss**: End-of-level. Multiple attack phases. A test of everything the player has learned in that level.

---

## Hit Feedback / Juice Spec

This is a first-class design concern, not a polish afterthought.

- **Bullet speed**: Fast. Bullets should cross the screen quickly enough that firing feels immediate.
- **Impact trail**: When a bullet strikes an enemy, a short burst of particles (or a simple shape flash) fires off in the bullet's direction of travel — as if the bullet glanced through or pierced. This is the signature visual effect of the game.
- **Enemy hit flash**: Struck enemies flash briefly (color invert or brightness spike) to confirm the hit.
- **Enemy death**: Enemies burst into their component shapes — a rectangle might shatter into smaller rectangles flying outward. Satisfying, not cluttered.
- **Sound**: FlatRedBall2 audio is currently stubbed and not yet implemented. Sound design is deferred until the stub is resolved.

All feedback should land within 1-2 frames of the event it represents. Delayed feedback kills the feel.

---

## Score System

Score exists but is subtle — it is never the main focus, but rewards clean play and extends replayability.

### Multiplier rules
A multiplier increases as long as both of the following conditions hold simultaneously:
- The player has not missed any enemies (all enemies in a wave were destroyed before leaving the screen)
- The player has not taken damage

Either condition breaking — a missed enemy *or* a hit taken — resets or reduces the multiplier. Both must be maintained together to keep it climbing.

This design ties the multiplier to the two core skills of the game: accuracy and evasion. It rewards mastery without making score the primary objective. A player who never thinks about score will still benefit from playing well; a player chasing score has a clear optimization target.

The score display should be present but visually understated — readable at a glance, never competing with the action.

---

## Level and Progression Structure

### Discrete levels with a journey arc
Not a score-attack loop. Levels are distinct stages with a beginning, middle, and end. The player should feel they are traveling somewhere — escalating difficulty, new enemy types introduced gradually, a boss encounter that punctuates each level.

### Progression principles
- Early levels: Introduce one or two enemy types, simple formations, low fire density. Let the player feel powerful against manageable threats.
- Mid levels: Combine enemy types, introduce shooters alongside fodder, patterns become layered.
- Late levels: Dense but still readable. Boss phases are multi-part. Upgrades earned earlier should feel essential, not optional.

### Pickups
Dropped in-level, not between levels. Destroying a heavy enemy or completing a wave cleanly might drop an upgrade. This keeps the reward loop tight and moment-to-moment.

### Death and restart
When health reaches zero, the run ends. No continues. The player restarts from level one, but with the knowledge of every pattern they encountered. The game is designed to be learned across multiple runs — death is informative, not punitive.

---

## Visual Language (Shapes and Color)

Since there are no sprites, shapes and color must do all the communication:

| Element | Shape | Color role |
|---|---|---|
| Player ship | Distinct rectangle (arrow-like) | Bright, constant — always readable |
| Player bullets | Thin fast rectangles | Player color, high contrast |
| Fodder enemies | Small circles or rectangles | One consistent color per wave type |
| Shooter enemies | Slightly larger, different shape | Warning color (e.g., orange) |
| Heavy enemies | Large rectangle | Bold, saturated — feels like a target |
| Enemy bullets | Small circles | High contrast to background, distinct from player bullets |
| Boss | Large, multi-part composite shape | Shifts color per phase |
| Hit effect | Short burst shapes in bullet direction | White or bullet color |
| Background | Dark, minimal | Lets foreground shapes read cleanly |
| Health bar | Gum UI element | Distinct from gameplay layer — HUD space |
| Score / multiplier | Gum UI element | Understated — present but not dominant |

Color should be consistent enough to be learned, bold enough to be exciting.

---

## Pacing

- **Session length**: Level-based, so a single level should be completable in 2-5 minutes. A full run through all levels is 20-40 minutes.
- **Pressure rhythm**: Not relentless. Waves arrive, are destroyed, and there is a brief breath before the next arrives. The breathing room is short enough to stay tense, long enough to reposition.
- **Difficulty**: Forgiving early, demanding late. Death should sting but not feel cheap. The player should always be able to identify what killed them.

---

## Open Questions / Deferred Decisions

- **Number of levels**: Not defined. Should be enough to feel like a complete arc — probably 5-8 levels for a first version.
- **Boss design specifics**: How many phases? Do bosses have readable tells before attacks? To be designed per-level.
