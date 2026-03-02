# Shatter — Game Design Document

## One-Sentence Pitch
A tense, score-chasing Breakout clone where skilled ball placement feeds a dual-multiplier system that rewards threading the ball deep into brick clusters.

## Player Experience Goals
- Feel tense and focused at all times — the stakes are always the score, and the score is always on the line.
- Experience genuine satisfaction from threading a precise shot behind a cluster of bricks and watching the active multiplier explode.
- Leave each run thinking "I could have scored higher" — the kind of feeling that drives one more attempt.

## Tone and Mood
- Sleek, arcade-style polish: dark background, shiny bricks with visible depth/sheen, glowing ball trail.
- Brick appearance reflects hit count — tougher bricks look more solid/vivid; cracked or dimmed as they take damage.
- Score and multiplier numbers are prominent and satisfying to watch climb.
- No storyline — pure arcade focus.

## Core Loop
1. Launch ball with a skillful angle from the paddle.
2. Watch it climb into bricks, building the **active multiplier** with each consecutive hit.
3. The ball returns — a paddle hit resets the active multiplier, but increments the **passive multiplier**.
4. Repeat: survive longer, aim smarter, thread the ball deeper to maximize the combined score.
5. Clear the level to advance. Lose all lives and the run ends with a final score.

## Dual Multiplier System
- **Passive multiplier** — increments each time the paddle hits the ball. Resets only on death (life lost). Rewards survival.
- **Active multiplier** — increments for each consecutive brick hit. Resets when the ball contacts the paddle. Rewards keeping the ball in the bricks.
- **Score per brick** = brick base value × passive multiplier × active multiplier.
- The tension: every paddle hit is both progress (passive goes up) and sacrifice (active resets). The dream is a massive passive multiplier sending the ball into a huge cluster.

## Movement and Controls Feel
- Paddle movement: snappy and responsive — pure reflexes.
- Ball angle: skill-based. Where the ball lands on the paddle affects its outgoing angle. Edges produce sharp angles; center produces straight shots. Players learn to aim.
- Ball speed: increases gradually as a level progresses (classic Breakout escalation), adding urgency.

## Brick Design
- Bricks have variable hit counts (e.g., 1–5 hits to destroy).
- Visual state reflects remaining hits — color, sheen, or cracks show durability at a glance.
- No special powers or effects — just hits.
- Level layouts deliberately include **gaps and clusters** that reward threading the ball behind the front row: the player who aims well gets disproportionately rewarded.

## Win / Lose / Progression
- **Lives**: 3 lives per run. Losing the ball costs a life and resets the passive multiplier.
- **Level clear**: destroy all destructible bricks to advance to the next level.
- **Multiple levels**: each level has a handcrafted brick layout designed around the "sneak behind" fantasy — increasingly complex clusters and channels as levels progress.
- **Run ends**: when all lives are gone. Final score is recorded.
- **High score**: tracked and displayed. The primary long-term goal.

## Pacing
- Tense throughout — ball speed creep within levels ensures no level feels slow.
- Punishing on life loss (passive multiplier resets) but not crushing — 3 lives keeps hope alive.
- Short session length fitting a prototype: aim for ~3–5 levels.

## Scope
- Small prototype.
- Core deliverables: paddle + ball physics, brick grid with multi-hit support, dual multiplier system, 3–5 handcrafted levels, lives system, score display, visual polish (ball trail, shiny/cracked bricks).

## Moments to Design For
- **The deep shot**: ball threads through a gap into a dense cluster and bounces through 8+ bricks while the active multiplier climbs — player watches, heart in throat, hoping it doesn't return too soon.
- **The big paddle hit**: after a long survival streak, the passive multiplier is huge — the next launch into a cluster will be massive.
- **The near-miss save**: reflexive last-second paddle contact that keeps a precious passive multiplier alive.

## Out of Scope
- Power-ups or special brick effects
- Multi-ball
- Storyline or characters
- Audio (prototype)
- Networked leaderboards
