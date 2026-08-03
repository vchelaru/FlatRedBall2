# Arctic Crossing — Game Design Document

## One-Sentence Pitch

A chill-but-surprising 2D platformer obby where a blocky humanoid hops across moving ice blocks over the Atlantic Ocean to reach a distant mountain, meeting quirky arctic animals along the way.

## Player Experience Goals

- **Primary feeling:** Relaxed flow punctuated by sudden "oh no!" moments — the rhythm of calm-then-panic is the heartbeat of the game.
- **Memorable takeaway:** Players should remember the journey — the ocean stretching out, the mountain growing closer, and the time a seal knocked them off an ice block they thought was safe.
- **Accessibility:** Easy to pick up. A new player should understand what to do within seconds of starting a level.

## Tone and Mood

- **Visual:** Colorful and blocky. All characters, platforms, and scenery are built from engine shapes (rectangles, circles, polygons) with bold, saturated colors. The winter theme uses whites, icy blues, and cool purples for the environment, with warm pops of color on characters and NPCs to make them stand out.
- **Audio feel:** Light and upbeat background music with a wintry vibe (bells, soft synths). Sound effects are snappy and satisfying — a crisp jump sound, a splash on falling, a cheerful checkpoint chime.
- **Atmosphere:** Playful and inviting, not grim or survival-focused. The Atlantic Ocean is a bright blue expanse, not a dark threatening void. Snowflakes drift. The mountain in the background grows as you progress. It should feel like an adventure, not a death march.

## Core Loop

1. **Jump** across platforms (ice blocks, icebergs, snow ledges) that move, tilt, shrink, or break.
2. **Reach the checkpoint** to lock in progress and feel safe.
3. **Encounter a surprise** — an ice block cracks, a seal slides through, wind pushes you — react or fall.
4. **Fall?** Splash into the water, lose a life, respawn at checkpoint. Shake it off, try again.
5. **Reach the phase exit** to advance toward the mountain.

The pull to keep going comes from two things: the mountain visibly getting closer (progress feels tangible), and curiosity about what weird obstacle or animal encounter is around the next corner.

## Movement and Controls Feel

- **Snappy and responsive.** Jumps should feel precise — the player needs to trust that the character will go where they aim. No floaty drift.
- **Air control:** Moderate. The player can adjust mid-air, but not wildly — enough to correct a slightly off jump, not enough to reverse direction.
- **Ground movement:** Quick acceleration, quick stop. The character should feel nimble on small ice platforms.
- **Ice physics (optional per platform):** Some platforms can have a slight slide factor to sell the "ice" feel, but this should be a deliberate design choice per platform, not a global default. Slippery controls on every surface would be frustrating — use it sparingly for flavor.
- **No wall jumps or advanced movement.** Keep it simple: run, jump, that's it. Complexity comes from the level design, not the moveset.

## Player Character

- **Shape construction:** A blocky humanoid built from rectangles and circles.
  - Body: tall rectangle (torso), smaller rectangle (legs), small rectangle (arms).
  - Head: square or circle on top.
  - Color palette: player-chosen or preset palette (warm colors to contrast the cold environment).
- **Male/female selection:** Offered at game start or on the menu screen. Visual difference can be subtle — different color palette, slightly different proportions, or a small accessory shape (e.g., a triangle "bow" or different head shape). Keep it simple since everything is blocky shapes.
- **No gameplay difference** between male/female — purely cosmetic.

## NPC Characters (All Blocky Shapes)

Each animal is built from colored rectangles, circles, and polygons. They exist in the world to add personality, provide light interaction, and occasionally create obstacles or help.

### Polar Bear
- **Shape:** Large white rectangle body, small circle ears, circle snout.
- **Role:** Friendly guide / hint-giver. Appears at the start of new phases or near tricky sections. Stands on large stable platforms. Could display a short text hint when the player gets close.
- **Personality:** Calm, encouraging. The "you got this" energy.

### Penguin
- **Shape:** Small black rectangle body, white rectangle belly, orange triangle beak, orange triangle feet.
- **Role:** Environmental comedy and light obstacle. Penguins waddle back and forth on platforms, and the player has to navigate around them. They don't hurt the player, but bumping into one can nudge you toward an edge. Occasionally a penguin slides on its belly across a platform at speed — a surprise moment.
- **Personality:** Oblivious and cheerful. They're just vibing.

### Unicorn
- **Shape:** Rectangle body (pastel purple or pink — stands out against the arctic palette), triangle horn on head, small rectangle legs.
- **Role:** Rare and special. Appears in hidden or hard-to-reach spots. Interacting with a unicorn grants a bonus (extra life, bonus points, or a temporary sparkle trail). The unicorn is the "why is this here?" element that adds whimsy and reward for exploration.
- **Personality:** Mysterious and magical. Doesn't belong in the arctic, and that's the point.

### Seals
- **Shape:** Rounded rectangle (oval-ish) body, small circle flippers, circle nose.
- **Role:** Dynamic obstacle. Seals pop up from the water onto ice blocks, sometimes occupying space the player needs. They can also be sitting on ice blocks that sink slightly under their weight. Occasionally a seal barks and startles, which is purely cosmetic but adds atmosphere.
- **Personality:** Lazy but in the way. They don't mean to cause trouble.

## Level Structure — Phases

The game is divided into **phases**, each representing a stage of the Atlantic crossing. The mountain is visible in the background throughout, growing larger with each phase.

### Phase 1: The Departure (Tutorial)
- **Setting:** Near the shore. Ice blocks are large, stable, and close together.
- **Purpose:** Teach jumping, moving, checkpoints. Introduce the penguin (waddles around harmlessly). Polar bear gives a hint or two.
- **Pacing:** Very chill. Almost impossible to fail unless you try.
- **Ice behavior:** Static or slowly drifting platforms.

### Phase 2: Open Water
- **Setting:** Out on the Atlantic. No land visible behind, mountain small ahead.
- **Purpose:** Introduce moving platforms (ice blocks that drift left/right or up/down). Gaps get wider. First real challenge.
- **Pacing:** Mostly chill with a few tighter jumps. First "frantic moment" — an ice block cracks and sinks after you stand on it for 2 seconds.
- **New element:** Crumbling ice blocks (visual crack lines appear, then it breaks).
- **NPCs:** Penguins on some platforms. A seal pops up on one.

### Phase 3: The Ice Field
- **Setting:** Dense cluster of small ice blocks. Lots of options, lots of movement.
- **Purpose:** Platforming intensity increases. Multiple paths through the ice field. Some paths are easier but longer, some are short but risky.
- **Pacing:** Moderate tension throughout. A few rapid sequences of small jumps.
- **New element:** Wind gusts that push the player sideways (indicated by visual particle lines before they hit).
- **NPCs:** Penguin belly-slide across a platform (surprise obstacle). Unicorn hidden on a hard-to-reach platform off the main path.

### Phase 4: The Storm
- **Setting:** Dark blue-gray background. Snowflakes fall faster and thicker.
- **Purpose:** The difficulty spike. Platforms move faster, crumble quicker, and wind gusts are more frequent.
- **Pacing:** Frantic is the new normal, with brief calm checkpoints to breathe.
- **New element:** Tilting ice blocks (they rock back and forth, and the player slides toward the low side). Falling icicle obstacles (telegraphed by a shimmer/flash before they drop).
- **NPCs:** Polar bear at checkpoints, reassuring. Seals everywhere, being in the way.

### Phase 5: The Mountain Base (Finale)
- **Setting:** The mountain fills the background. Rocky ledges mixed with ice. Upward climbing section.
- **Purpose:** Vertical platforming — climbing up the mountain's base. The final push.
- **Pacing:** Alternating calm and frantic. The last few jumps before the summit should feel triumphant, not frustrating.
- **New element:** Vertical scrolling / upward progression. Falling platforms that only go down (one-shot jumps).
- **NPCs:** All animals appear near the summit in a celebration scene when you finish.

### Additional phases can be added to extend the game. Each phase should introduce at least one new platform behavior or obstacle to keep things fresh.

## Win / Lose / Progression

### Lives
- **3 lives per phase.** Displayed on screen (three small heart shapes or similar).
- **Losing a life:** Fall into the water. Short splash animation/sound. Respawn at the last checkpoint. Quick and painless — the "oops, try again" Roblox feel.
- **Losing all 3 lives:** Phase restarts from the beginning. Lives reset to 3. No game-over screen with fanfare — just a brief "Try Again?" prompt and drop them back at the phase start.
- **Gaining lives:** Rare. Finding the unicorn grants +1 life (up to max 5). This makes the unicorn hunt feel rewarding.

### Checkpoints
- Placed every 3-5 platforming challenges within a phase.
- Visually distinct: a tall thin rectangle "flag" shape with a bright color, planted on a stable platform.
- Walking through a checkpoint triggers a visual flash and sound. The flag changes color to show it's been activated.
- Checkpoints persist within a phase attempt. If you lose all lives and restart the phase, checkpoints reset.

### Scoring
- **Points for distance:** Each platform landed on awards a small number of points.
- **Bonus for speed:** Completing a phase under a par time awards bonus points (optional — don't display a timer unless the player wants pressure).
- **Collectibles:** Small diamond/star shapes scattered on platforms. Not required, but reward exploration and risk-taking.
- **High score:** Saved per phase. Displayed on the phase select screen.

### Progression
- Completing a phase unlocks the next phase.
- A simple phase-select screen shows phases 1 through 5 (or more), with completed phases marked and the next one available.
- The mountain graphic on the phase-select screen fills in as phases are completed — a visual progress meter.

## Menu Flow

### Title Screen
- Game title "Arctic Crossing" displayed large.
- Background: the ocean and mountain in the distance, with drifting ice blocks and a penguin or two.
- Options: **Start Game**, **Phase Select** (if any phases completed), **Character Select**.
- Minimal and clean. Blocky shapes, bold colors.

### Character Select
- Choose male/female appearance.
- Simple side-by-side display of the two character shapes. Player picks one with left/right input and confirms.
- Could expand later to include color palette selection.

### Phase Select
- Shows all phases as a horizontal row of icons (or a simple map).
- Locked phases are grayed out. Completed phases show a checkmark and high score.
- Select a phase to play it.

### In-Game HUD
- **Lives:** top-left, shown as small shape icons.
- **Score:** top-right, numeric display.
- **Phase indicator:** top-center, "Phase 2: Open Water" (fades after a few seconds).
- Keep the HUD minimal — the game world should be the focus.

### Pause Menu
- Triggered by pause input (Escape / Start button).
- Options: **Resume**, **Restart Phase**, **Quit to Menu**.

## Pacing Summary

| Phase | Base Tension | Surprise Moments | New Mechanic |
|-------|-------------|-------------------|--------------|
| 1 — Departure | Very low | None | Basic jumping, checkpoints |
| 2 — Open Water | Low | Crumbling ice (first surprise) | Timed/crumbling platforms |
| 3 — Ice Field | Medium | Penguin belly-slide, wind | Wind gusts, multiple paths |
| 4 — Storm | High | Frequent crumbles, icicles | Tilting platforms, icicles |
| 5 — Mountain | High (with breathers) | Final vertical push | Vertical climbing, one-shot platforms |

## Scope

- **Project size:** Mid-sized sample project. Larger than a jam game, but scoped to be completable.
- **Phase count:** 5 phases for the initial version. Structure supports adding more.
- **Art:** All shapes — no textures or sprites needed. Rectangles, circles, polygons, and colors.
- **Target session length:** 15-30 minutes to complete all 5 phases on a first playthrough (with deaths). Individual phases are 3-6 minutes.

## Moments to Design For

- **The first crumble:** Phase 2, the player is standing on an ice block feeling safe, and it cracks and drops. This is the game's "oh, THIS kind of game" moment. It needs to be surprising but fair — give a brief visual/audio warning (crack lines, a creak sound) so the player can react.
- **Penguin belly-slide:** A penguin rockets across a platform on its belly right as the player is about to jump. Comedy and surprise. Should make the player laugh, not rage.
- **Finding the unicorn:** The player spots a pastel-colored shape on a far-off platform that doesn't look like it belongs. Reaching it should feel like a secret discovery. The reward (extra life) should feel generous.
- **The summit:** Reaching the top of the mountain. All the NPC animals are there. A brief celebratory scene — shapes bouncing, colors flashing. The player should feel like they accomplished something.
- **The storm shift:** When Phase 4 starts, the background darkens and snowfall intensifies. The mood shift should be immediate and dramatic — "things just got real."

## Out of Scope

- **Online multiplayer or co-op.** Single-player only.
- **Custom art or sprites.** Everything is engine shapes with colors.
- **Complex narrative or dialogue trees.** NPCs can show short text blurbs, but no branching conversations.
- **Procedural generation.** All levels are hand-designed for quality control over the platforming feel.
- **In-app purchases or monetization.** This is a complete game.
- **Mobile/touch controls.** Keyboard and gamepad only for now.
