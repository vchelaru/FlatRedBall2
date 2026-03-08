# Dead Run — Game Design Document

## One-Sentence Pitch
A top-down survival dash where the player must reach a goal on the other side of a zombie-filled map without being consumed.

## Player Experience Goals
The player should feel a sustained, building dread — not jump scares, but the creeping pressure of zombies closing in from multiple directions. Close calls should feel genuinely exciting: a near-miss is a small triumph, not just a near-failure. Reaching the goal should feel like relief and victory earned through nerve and movement.

## Tone and Mood
Tense and oppressive. The player is always outnumbered, always being hunted. No safe moment lasts. The pacing is urgent — standing still is a losing strategy.

## Core Loop
1. Player spawns at the start position
2. Zombies on the map immediately pursue the player
3. Player navigates through the map toward the goal, taking damage from contact
4. Player either reaches the goal (win) or loses all health (lose)
5. Restart and try again

## Movement and Controls Feel
Top-down, free 360-degree movement. The player should feel mobile and agile — their only real advantage over the zombies is speed and the ability to change direction quickly. Movement should feel responsive and precise so that close calls are a matter of skill, not input lag.

## Player
- Starts at a fixed spawn point on the map
- Has a visible health bar
- Takes damage on contact with zombies (contact damage, not one-hit kill)
- Wins by reaching the goal position
- Loses when health reaches zero

## Zombies
- Multiple zombies placed on the map at game start
- Behavior: beeline pursuit — once active, each zombie moves directly toward the player at all times
- Contact with a zombie drains the player's health
- Zombies do not die or despawn (in the base version)

## Win / Lose / Progression
- **Win**: Player reaches the goal with health remaining
- **Lose**: Player's health bar reaches zero
- No carry-over between attempts — each run is self-contained
- No progression system in the base version

## Pacing
Fast and unforgiving. The player is under pressure from the first second. The map is the only variable that creates breathing room — open areas are dangerous, tight spaces can be used to slip past zombies. Sessions should be short (under 2 minutes per attempt).

## Scope
Small prototype / jam-sized. The goal is a single playable map with a clear start, goal, and zombie placement. All complexity (map variety, zombie types, spawning, power-ups, scoring) is deferred.

## Moments to Design For
- The opening moment: zombies spot the player and the chase begins
- A close call where a zombie nearly cuts off the player's path
- The final stretch to the goal with zombies closing in from behind
- Reaching the goal — a clear, satisfying win state

## Deferred / Out of Scope (for now)
- Zombie patrol behavior or idle states
- Spawning zombies over time
- Player abilities (sprint, attacks, items)
- Multiple maps or level progression
- Scoring or leaderboards
- Zombie death / player combat
- Specific zombie speed, damage values, and health bar numbers — to be tuned during playtesting
