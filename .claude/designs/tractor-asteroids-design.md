# Tractor Asteroids — Game Design Document

## One-Sentence Pitch
Classic Asteroids with a tractor beam that lets you grab asteroid fragments and fling them as skill-shot projectiles.

## Player Experience Goals
- The player should feel the tension and flow of classic arcade Asteroids.
- The tractor beam should create moments of "trick shot" satisfaction — grabbing debris mid-drift, spinning, and flinging it into a big target.
- The player should leave thinking "I want to get better at that beam fling."

## Tone and Mood
- **Visual**: Retro neon vector-line aesthetic. Glowing outlines on a dark background. Particles on impacts. The tractor beam is a visible energy tether.
- **Audio**: Synth-retro sound effects — thruster hum, laser zaps, a satisfying "clunk" when the beam grabs a fragment, and a punchy impact sound on fling hits.
- **Atmosphere**: Arcade cabinet energy. No story, no lore. Pure play.

## Core Loop
1. **Thrust and rotate** through a field of drifting asteroids.
2. **Shoot bullets** to break large asteroids into smaller fragments.
3. **Grab fragments** with the tractor beam — the fragment orbits the ship.
4. **Fling the fragment** at a bigger asteroid for bonus damage and score.
5. **Survive the wave**, then face a harder one.

The tractor beam is the differentiator: debris is not just a hazard, it is ammunition. Breaking rocks is farming, not just clearing.

## Movement and Controls Feel
- **Rotation**: Snappy, immediate. Left/right to rotate.
- **Thrust**: Forward thrust only. Momentum-based, floaty drift in space. No friction — classic Asteroids inertia.
- **Screen wrapping**: Ship and all objects wrap around screen edges.
- **Tractor beam**: Hold a button to activate. Grabs the nearest small fragment within a short range. The fragment orbits the ship at a fixed distance while held. Release to fling it in the direction the ship is facing.

## Win / Lose / Progression
- **Lives**: Three lives. Lose one on collision with any asteroid or fragment.
- **Failure feel**: Should sting — a brief invincibility flash on respawn, but no other forgiveness. Classic arcade stakes.
- **Waves**: Each wave spawns more asteroids, and they drift faster. No end state — play until you die.
- **Score**: Points for destroying asteroids. Bullet kills give base points. Flung-fragment kills give a score multiplier (2x or 3x). Chain reactions (fragment shrapnel hitting other asteroids) give bonus points.
- **High score**: Persistent high score displayed. That is the only progression.

## Pacing
- **Speed**: Starts moderate, escalates per wave. By wave 5+, the screen should feel dangerously full.
- **Punishment**: Moderate. Losing a life resets your position but not the wave. Game over means starting from wave 1.
- **Session length**: 2-5 minutes per run. Quick restart.

## Key Design Tension: The Tractor Beam
The beam creates a risk/reward tradeoff:
- **Upside**: Flung fragments deal more damage than bullets and award more points.
- **Downside**: While holding a fragment, the player cannot fire bullets. The orbiting fragment also makes the ship's collision profile slightly larger.
- **Skill expression**: Timing the grab, aiming the fling, and releasing at the right moment is the core skill to master. Easy to use, hard to use well.
- **Chain reactions**: When a flung fragment shatters an asteroid, the resulting debris flies outward with force. If those pieces hit other asteroids, they can trigger chain breaks for bonus score. This creates emergent, satisfying chaos.

## Scope
- **Size**: Jam-sized. One screen, one ship type, one weapon (bullets + tractor beam), asteroids in 3 sizes (large, medium, small/fragment).
- **Screens**: Title/start, gameplay, game over. Minimal UI — score and lives displayed during play.
- **No**: Power-ups, upgrade systems, multiplayer, menus beyond start/game-over, persistent progression beyond high score.

## Moments to Design For
- Grabbing a fragment while drifting sideways, spinning 180 degrees, and flinging it perfectly into a large asteroid.
- A chain reaction where one flung fragment sets off a cascade of shattering rocks.
- The "oh no" moment when the screen is full of debris and you have to thread the needle.
- The satisfying screen-clear stillness after finishing a tough wave.

## Out of Scope
- Power-ups or pickups
- Ship upgrades or unlocks
- Multiplayer (co-op or competitive)
- Story, narrative, or campaign mode
- Complex menus or settings
