# The Great Koala vs Pickle Journey — Game Design Document

## One-Sentence Pitch
A geometric arcade shooter where a pink square fights upward through escalating platform layouts to destroy a dancing green rectangle that never stops shooting back.

---

## Player Experience Goals

- The player should feel **powerful and on the offensive** at all times — they have a gun, they know where the enemy is, and they are coming for it.
- Dodging enemy bullets should feel like reading a puzzle, not like being overwhelmed — the enemy's pattern is learnable and the windows are real.
- Clearing a level should feel earned: "I read the pattern, I made it through, I landed the shots."
- Later levels should create a satisfying tension between aggressive advancement and careful positioning.

---

## Tone and Mood

- **Visual**: Clean, geometric, no frills. Flat colors on a dark or neutral background. No gradients, no particles beyond bullets. The simplicity is the aesthetic.
- **Audio** (deferred): Upbeat, slightly retro arcade feel. Enemy death should have a satisfying crunch. Bullet sounds should be punchy.
- **Overall atmosphere**: Arcade cabinet energy. Score-chasing, pattern-learning, "one more level."

---

## Core Loop

1. Player enters level. Enemy is visible across the platforming layout.
2. Player platforms toward the enemy while dodging incoming bullets.
3. Player shoots enemy when in range or in a gap of the enemy's fire pattern.
4. Enemy dies — level cleared. Next level loads.
5. If player takes 3 hits — restart current level.

The loop repeats with escalating platform complexity each level.

---

## Player Mechanics

### Movement (Controller)
| Action          | Input                        |
|-----------------|------------------------------|
| Move left/right | Left stick or D-pad          |
| Jump            | A button (or equivalent)     |
| Shoot           | Right trigger or face button |

- Movement is **snappy and responsive** — no floaty inertia. The player should feel precise and in control.
- The player faces left or right based on movement direction. Facing direction determines bullet direction.
- The player can stand still and shoot in the last direction they faced.

### Jumping
- Single jump only (no double jump, at least in early levels — see TODOs).
- Jump height is fixed and consistent — predictable arc so players can learn platform spacing.
- No variable jump height (no "tap for small jump, hold for big jump") — keeps controls simple.

### Shooting
- Shoots a bullet in the direction the player is currently facing (left or right).
- Bullets travel in a straight horizontal line at a fixed speed.
- Bullets are destroyed on contact with platforms and walls.
- Bullets deal damage to the enemy on contact.
- No cooldown constraint specified — default to a short fire rate (e.g., ~3–4 shots per second) to prevent spam feeling cheap. Tune in playtesting.

### Health
- Player has **3 hit points**.
- Taking a hit removes 1 HP. Visual feedback (flash, shake — see TODOs).
- At 0 HP: level restarts from the beginning.
- HP does **not** carry between levels — player starts each level with full 3 HP.

---

## Enemy Mechanics

### Identity
- Visual: A **green rectangle** — taller than it is wide, visually distinct from the square player.
- Positioned somewhere across the level, typically elevated or at the far end.

### Behavior Pattern (Cycle)
The enemy runs a looping 3-phase cycle:

1. **Pace** — The enemy moves a short distance left and right a few times. Slow and readable.
2. **Hop** — The enemy hops in place once or twice. Telegraphs that a shoot phase is coming.
3. **Shoot** — The enemy fires a burst of bullets at the player's current position, then returns to Pace.

The cycle timing should be tuned so that the player has clear windows to advance or shoot during the Pace and Hop phases, but must dodge during the Shoot phase.

### Enemy Bullets
- Fired in a **burst** (e.g., 2–4 bullets per shoot phase, with a short interval between each).
- Bullets travel toward the player's position at the moment of firing (not homing — they continue in that direction).
- **Bullets pass through platforms and walls.** The player cannot take cover behind geometry. They must move out of the bullet's path.
- Bullets do not expire quickly — they should travel far enough to cross the level.

### Enemy Health
- Specific HP value is a tuning decision (see TODOs), but intended to require **multiple shots to kill** — enough that the player has to manage positioning across several shoot windows, not just rush in and burst it down.
- Suggested starting point: 5–8 hits. Tune per level.
- Enemy has a visual feedback response to being hit (flash green-to-white or similar).
- Enemy death: plays a visible death animation or effect, then the level-clear state triggers.

### Enemy Movement
- The enemy's pacing covers a small horizontal range — it is not trying to escape or chase. It is a turret with personality.
- Enemy does **not** fall off platforms.

---

## Level Progression

### Level 1 — Tutorial Feel
- Simple staircase layout: platforms arranged as ascending steps going up and to the right.
- Enemy is at the top-right.
- No complex jumps. Player learns: move, jump, shoot, dodge.
- Enemy fires at a relaxed pace.

### Level 2–3 — Introducing Variety
- Platforms at mixed heights. Some gaps require more precise jumping.
- Enemy may be at a middle elevation, not necessarily the highest point.
- Enemy shoot burst size or pacing speed increases slightly.

### Level 4+ — Escalating Complexity
- Layouts become less linear. Platforms may be offset, narrow, or require the player to backtrack or choose routes.
- Some platforms may be at a height that forces the player into the bullet path temporarily.
- Enemy may have a shorter rest window between cycles (tighter dodge requirements).

### Escalation Levers (Tuning Handles)
- Platform layout complexity
- Number of bullets per burst
- Speed of enemy pacing
- Length of Pace/Hop/Shoot phases (faster cycle = less time to react)
- Enemy HP

---

## Win / Lose Conditions

### Per-Level Win
- Enemy HP reaches 0.
- Level-clear state plays, then next level loads.

### Per-Level Lose
- Player takes 3 hits.
- Level restarts from the beginning (no checkpoint mid-level).
- Player HP resets to 3.

### Overall Win
- Player clears all levels.
- End screen or victory state. (Content TBD — see TODOs.)

### Overall Lose
- There is no global game-over or lives system in this design. Each level is self-contained. The player retries only the current level.

---

## Visual Style

| Element         | Description                                                  |
|-----------------|--------------------------------------------------------------|
| Player          | Pink square. Solid fill. No outline or minimal outline.      |
| Enemy           | Green rectangle (taller than wide). Solid fill.              |
| Platforms       | Simple grey or white rectangles. No texture.                 |
| Player bullet   | Small pink rectangle or dot.                                 |
| Enemy bullet    | Small green rectangle or dot, slightly larger than player's. |
| Background      | Dark neutral (dark grey or near-black). No decoration.       |
| HP display      | Three small pink squares in a corner (one lost per hit).     |
| Level indicator | Simple text or number in a corner.                           |

The geometric constraint is a feature, not a limitation — commit to it fully. No organic shapes anywhere.

---

## Pacing

- **Session length**: Each level should be completable in under 2 minutes once the player understands the pattern. Retry loops should be short.
- **Difficulty curve**: Gentle in levels 1–2, meaningful challenge by level 3–4.
- **Forgiving or punishing**: Moderately forgiving (3 HP, no global lives) — but restarts are instant and levels are short, so retries don't feel expensive.
- **Overall feel**: Fast-to-learn, rewarding-to-master. Arcade sensibility.

---

## Scope

- Small-to-medium prototype. A complete vertical slice is: 3–5 levels, full player and enemy mechanics, working controller input.
- No save system needed — levels are short enough to replay from level 1.
- No menu system required for a first prototype — jump straight to Level 1 on launch.

---

## Moments to Design For

- **The first dodge** — Level 1 should have a moment where a bullet comes and the player clearly needs to move. Make it slow enough that they can react, but visible enough that they know it's a close call.
- **The first kill** — The enemy death should feel satisfying. A pop, a flash, something. Don't let it be silent.
- **The read** — Later levels should have a moment where the player watches the enemy cycle once before committing to advance. Reward patience with a safe window.
- **The clutch** — A moment late in a level where the player is at 1 HP, the enemy is close to death, and they have to land one more shot. This is the emotional peak of the game.

---

## Open Questions / TODOs

- **Enemy HP per level** — Needs playtesting to find the right number. Start at 6 and tune.
- **Bullet fire rate (player)** — Start at ~3 shots/sec. Tune to prevent trivial spam.
- **Enemy bullet speed** — Needs calibration. Fast enough to require real movement, slow enough to be readable.
- **Shoot cycle timing** — Duration of Pace, Hop, and Shoot phases needs tuning per level.
- **Double jump** — Consider unlocking as a level-progression mechanic in later levels. Deferred.
- **Level count** — 5 levels for prototype. Expand if scope allows.
- **Victory state** — What happens after the final level? Text screen, loop back, or a simple "You Win" for now.
- **Hit feedback** — Player flash on damage, screen shake (optional). Spec when implementing.
- **Audio** — Fully deferred. Out of scope for prototype.
- **Enemy bullet angle** — Do enemy bullets always fire horizontally at the player's current Y, or do they fire at an angle toward the player's exact position? Recommend: fire toward player's exact position for more interesting dodging. Confirm before implementing.
