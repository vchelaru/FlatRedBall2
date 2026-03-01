# Pong (CRT Edition) — Game Design Document

## One-Sentence Pitch
A faithful, juicy Pong clone with CRT warmth aesthetics — built as a learning project that feels surprisingly great to play.

---

## Player Experience Goals

- The player should feel a quiet, nostalgic satisfaction — like rediscovering an old game on a warm CRT screen in a dark room.
- Every hit, every score, every close rally should feel *tactile* and rewarding, not sterile.
- The game should be immediately playable with no learning curve, but feel better than it looks on paper because of the juice.

---

## Tone and Mood

- **Visual**: CRT warmth — white/amber/green phosphor-style elements on a dark background. Subtle scanline overlay. Soft glow on the ball and paddles. No sharp pixel edges; everything has a slight bloom.
- **Audio**: Simple, satisfying beeps and boops. Pitched hits (different tone for wall vs. paddle vs. score). Nothing complex — the sounds should feel like *the* Pong sounds, just slightly more satisfying.
- **Atmosphere**: Quiet and intimate. No busy UI. Minimal text. The game breathes.

---

## Core Loop

1. Ball launches from center at a fixed speed and random angle.
2. Players rally — each paddle hit redirects the ball (angle influenced by where on the paddle it hits).
3. A point scores when the ball passes a paddle and exits the screen.
4. Ball resets to center, brief pause, next round begins.
5. First to 7 points wins.

---

## Movement and Controls Feel

**Ball:**
- Launches at moderate speed. Increases speed slightly on each successive paddle hit within a rally (caps out to avoid unplayability).
- Angle of deflection depends on where the ball strikes the paddle — center = straight, edges = sharp angle. This gives players real control and is the primary skill expression.
- Speed resets to base on each new serve.

**Paddles:**
- Fluid acceleration — paddles do not snap to full speed instantly. They ease in and ease out.
- This gives movement a slightly weighty, physical feel rather than a digital on/off response.
- Paddles are bounded to the screen edges vertically.

**Controls:**
- Player 1 (left): W / S keys
- Player 2 (right): Up / Down arrow keys
- CPU opponent (if 1P mode): simple tracking AI with a small speed cap and occasional imprecision to keep it beatable

---

## Juice Elements

These are the moments that make the game feel alive. Each should be subtle — not chaotic — consistent with the lo-fi aesthetic.

| Moment | Juice |
|---|---|
| Ball hits paddle | Brief screen shake (very subtle). Paddle flashes or pulses briefly. Hit sound pitched to rally length (gets slightly higher each hit). |
| Ball hits wall (top/bottom) | Lighter shake. Wall-bounce sound. |
| Point scored | Bigger screen shake. Score number ticks up with a satisfying sound. Brief flash on the scoring side. Ball briefly leaves a fading trail as it exits. |
| Long rally | Ball leaves a short motion trail (phosphor persistence effect). Hit sounds climb in pitch. Subtle tension builds. |
| Win condition reached | Simple "winner" text appears with a glow effect. No fanfare — just a quiet, satisfying moment. |

---

## Player Modes

- **1 Player vs. CPU** — default mode. CPU tracks the ball with a slight speed limit and small positional error to keep it beatable at a comfortable skill level.
- **2 Player Local** — both players on the same keyboard. No AI.
- Mode selection: simple key prompt at the title/start screen ("1P" or "2P"). Keep it minimal.

---

## Win Condition and Scoring

- First to **7 points** wins.
- Score displayed at the top center of the screen in large, simple digits.
- After a win, a "Play Again?" prompt appears. Any key restarts.

---

## Pacing

- Fast once rallies develop, but controlled — no instant chaos.
- Ball speed ramp means early rallies feel relaxed; long rallies get tense.
- Points reset the pace, giving natural breathing room.
- A full game should take roughly **3–5 minutes**.
- Forgiving: no punishment beyond losing the point. No lives, no elimination.

---

## Scope

This is a **focused learning prototype** — not a commercial product. Scope is deliberately small.

**In scope:**
- One gameplay screen (no multiple levels or maps)
- 1P vs CPU and 2P local modes
- CRT visual filter (scanlines + glow — can be a simple post-process or overlay sprite)
- Core juice elements listed above
- Simple start screen and win screen
- Score tracking (first to 7)

**Stretch goals (only if core is solid):**
- Ball speed indicator (subtle visual cue)
- Configurable winning score
- Sound pitch ramp on long rallies

**Out of scope:**
- Online multiplayer
- AI difficulty settings
- Persistent stats or leaderboards
- Animations beyond basic juice effects
- Music (beeps only, no background track required)
- Mobile or controller support
