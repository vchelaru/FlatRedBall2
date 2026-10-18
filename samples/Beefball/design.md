# Beefball — Game Design Document

## One-Sentence Pitch
Two-player, top-down air-hockey: each player steers a round paddle ("beef") with acceleration and a cooldown dash, and knocks a small red puck into the other player's goal. A port of the FlatRedBall 1 sample, restyled as a soft neon-vector arena.

## Core Mechanics (gameplay carried over from FRB1)
- Arena is 800x600 world units, centered on the origin, Y+ up. Top and bottom walls span the full width. The left and right walls each have a 200-unit-tall opening (y in -100..100) leading to a goal pocket.
- **Players** (circle, radius 16): acceleration-driven movement (`Acceleration = input * 340`), `Drag = 1`. **Dash**: on a button press while the stick/keys are held, set velocity to `normalized direction * 600`; 2-second cooldown. During cooldown a smaller circle on the player grows from 0 to full radius as a cooldown indicator; it disappears when ready.
- **Puck** (circle, radius 6, red): `Drag = 0.4`, starts stationary at the center. Player-vs-puck is a bounce where the puck is much lighter than the player (FRB1 used elasticity 1 and masses of roughly 1 for the player, 0.3 for the puck) so a hit sends it flying.
- Players and puck bounce off the walls (elasticity 1) and players bounce off each other. Players cannot enter goal pockets; the puck can.
- **Scoring**: the puck entering a goal scores for the player who owns the *opposite* side (Player 1 starts left and scores in the right goal; Player 2 starts right and scores in the left goal). On a goal: show a short "GOAL" moment (~1.5 s, play continues frozen), score increments, then both players return to start (P1 x=-180, P2 x=+180, y=0), velocities zero, puck at the center, stationary.
- **Match**: first to 5 wins. Show "PLAYER N WINS", then Enter returns to the title screen.

## Screens
1. **TitleScreen** — game name, "Press Enter to start", control hints. Gum, code-only.
2. **GameScreen** — the arena, HUD, goal/win banners. Esc returns to title.

## Controls
- Player 1: W/A/S/D move, B dash. Gamepad 0 left stick, A to dash (use the gamepad when connected, otherwise keyboard).
- Player 2: arrow keys move, Right Shift dash. Gamepad 1 left stick, A to dash (same rule).
- Enter: start / return to title. Esc: back to title.

## Visual Direction: "Neon Bloom" (soft)
Reference mockup: https://claude.ai/artifact/Urw8bwMVyR2E2EVhEoFsQy (Neon Bloom tab; the user wants the glow soft, never harsh).
- **All visuals are engine shapes** (circles, rectangles, polygons/lines). No textures, no sprites, no shaders. HUD is Gum.
- Near-black background `#04050b`. Subtle floor: a barely visible team-tinted wash on each half (cyan left, magenta right) and a very faint grid. Faint white center line and center circle (radius ~72).
- Palette: Team 1 / left = cyan `#35f0ff`. Team 2 / right = magenta `#ff3ea5`. Puck = red-orange `#ff6a3d` with a near-white core. Walls blend from cyan on the left to magenta on the right (a violet in the middle). Goal pockets use the team color of the side they sit on.
- **Glow is faked with shapes**: behind each glowing object, stack 2-3 larger, filled, low-alpha shapes of the same color (e.g. radius x1.4 at ~14% alpha, x2.2 at ~7%). Walls get a wider, very faint outline layer behind the crisp line. Keep it soft; if in doubt, dimmer. Players have a dark filled body with a bright colored ring and a small white center dot.
- **Puck trail**: short-lived, fading, shrinking circles spawned behind the puck while it moves (pooled via a Factory), orange-to-red.
- **Hit sparks**: on puck hits and wall hits, a small burst of tiny circles flying outward and fading in under a second. A brief expanding ring around the player when it hits the puck.
- **Goal moment**: the scored goal pocket pulses brighter, a radial wash in the scorer's color fades out, large "GOAL!" Gum label pops in (tween scale/alpha) with "TEAM N SCORES" beneath.
- HUD (Gum): "TEAM 1  n" top-left area (x~200 of 800), "TEAM 2  n" top-right area (x~600 of 800), label small and letterspaced-look, score large, in the team colors.

## Tunables
All gameplay numbers (movement speed, dash speed, dash cooldown, drag values, radii, masses/elasticities, score to win, goal-pause seconds) and the palette colors live in a JSON content file per the `content-boundary` skill, with hot-reload (`content-hot-reload`) so they can be tuned while the game runs. Arena geometry (the six wall rectangles and two goal pockets) is fixed in code.

## Scope Boundary
- No audio. No AI opponent. No online play. No sprite or tilemap content. No custom shaders or post-processing (the engine has none; glow is stacked shapes). No pause menu.
