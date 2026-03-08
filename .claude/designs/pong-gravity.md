# Pong: Gravity Wells — Game Design Document

## One-Sentence Pitch
A two-player Pong variant where gravity wells drift onto the field during long rallies, bending the ball's path — and skilled players can thread the ball through a black hole to teleport it out of a paired white hole.

## Player Experience Goals
- Early rallies feel clean and readable — classic Pong tension.
- As a rally extends, the field grows increasingly dangerous and unpredictable.
- A goal scored brings genuine relief: the field resets to calm.
- Skilled players should feel rewarded for learning to read and exploit the wormholes.
- The game should feel *alive* — responsive, punchy, and visually satisfying (juice is the long-term differentiator).

## Tone and Mood
Sleek and high-energy. Dark background, glowing ball and paddles, pulsing gravity wells. The aesthetic should feel like a neon physics toy. Audio and visual feedback should make every hit, bend, and teleport feel significant.

## Core Mechanics

### Paddles
- One paddle per player, on opposite sides of the field.
- Paddles move vertically only.
- Standard Pong movement: up/down input, capped speed.

### Ball
- Single ball in play at all times.
- Launches at game start and after each point.
- Speed may increase slightly with each paddle hit (TBD during v1 tuning).
- Ball trajectory is bent by nearby gravity wells (see below).

### Scoring
- A point is scored when the ball passes a player's side boundary.
- First to a target score (e.g., 7 points) wins the match.
- After each point: ball resets to center, gravity wells clear.

## Gravity Wells Mechanic

### Spawning
- Wells spawn on a timer during active play (suggested starting interval: every 5–8 seconds, tunable).
- Each well that spawns is paired: one **black hole** and one **white hole**, placed at random positions in the play field (avoid paddle zones).
- Timer resets when a point is scored — the field always starts a rally clean.
- No hard cap defined yet; let playtesting determine whether to cap at 2–3 pairs or let chaos escalate freely.

### Gravitational Effect
- Black holes exert a gravitational pull on the ball as it passes nearby.
- Pull strength increases with proximity (inverse-square or tunable falloff).
- White holes have no pull — they are exit points only.

### Wormhole Teleportation
- If the ball enters a black hole (crosses its center threshold), it is immediately ejected from the paired white hole.
- Exit direction: the ball fires out of the white hole in a direction determined by its entry trajectory into the black hole (or a fixed random arc — TBD during v1 tuning).
- This is intentional player interaction: a skilled player can aim for a black hole to teleport the ball to an unexpected position on the field.
- Both players are equally able to exploit or fall victim to wormholes.

## Win / Lose / Progression
- Match ends when one player reaches the target score.
- No carry-over between matches — each game is self-contained.
- Loss stings, win feels earned. No softening.

## Pacing
- Rally pacing: calm open, escalating chaos, sharp reset on score.
- Session length: short (5–10 minutes per match).
- Punishing enough that goals feel meaningful; forgiving enough that losing a point to a lucky wormhole doesn't feel unfair.

## Scope

### V1 — Core Mechanics (First Pass)
- Two-player local play (same keyboard or two gamepads).
- Paddles, ball, bouncing, scoring, win condition.
- Gravity wells: timer-based spawning, gravitational pull, reset on score.
- Black hole → white hole teleportation.
- Minimal placeholder visuals — functional over pretty.

### Deferred — Juice Pass
- Screen shake on goals, teleportation, and hard hits.
- Particle effects: ball trails, well pulsing, goal explosion.
- Sound effects for every meaningful event (hit, bend, teleport, score).
- Visual polish: glow, neon aesthetic, animated well appearance/disappearance.
- Screen distortion near black holes.

## Moments to Design For
- The first time a ball bends visibly around a well — players should notice and react.
- The first successful intentional wormhole shot — should feel like a trick play.
- A rally where three or four pairs are active — maximum chaos, maximum tension.
- The goal that ends a long chaotic rally — relief and release.

## Out of Scope
- Single-player / AI opponent (v1 is local two-player only).
- Online multiplayer.
- Power-ups beyond the gravity wells mechanic.
- Multiple balls.
