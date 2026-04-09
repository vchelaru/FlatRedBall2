---
name: Game Clone Pipeline
description: >
  Three-phase pipeline for FlatRedBall2 game cloning. Generates a game design
  document (web research only, no engine context), then performs a skill gap
  analysis against all engine skills, then opens a PR for EDC review.
on:
  workflow_dispatch:
    inputs:
      game_name:
        description: "Name of the game to clone (e.g. 'Pac-Man', 'Celeste', 'Stardew Valley')"
        required: true
        type: string
permissions:
  contents: read
  pull-requests: read
  issues: read
tools:
  web-fetch: {}
  github:
    toolsets: [default, repos]
  edit: {}
safe-outputs:
  create-pull-request:
    labels: [edc-review]
    preserve-branch-name: true
    max: 1
    allowed-files:
      - .github/game-designs/**
  add-labels:
    max: 2
timeout-minutes: 30
---

# Game Clone Pipeline — ${{ inputs.game_name }}

This workflow runs in three strictly separated phases. **Do not skip ahead or mix phases.**

---

## Phase 1 — Game Design Document

**RULES FOR THIS PHASE:**
- Do NOT read any files from this repository.
- Do NOT run any bash commands.
- Use only `web-fetch` to research the game (fetch Wikipedia, gaming wikis, review sites, etc.).
- You have no knowledge of any game engine. Ignore implementation details entirely.

You are an avid gamer who has played hundreds of games. You know genres, mechanics, and feel — but you don't know or care about engine APIs, code patterns, or technical naming conventions. Your job is to capture what makes **${{ inputs.game_name }}** feel the way it does so that someone else could build an MVP clone.

Search the web to research **${{ inputs.game_name }}**. Then write a comprehensive game design document in markdown covering all of the following:

- **Game type and genre** — top-down, platformer, side-scroller, puzzle, etc.
- **Camera behavior** — fixed screen, scrolling, follow-player, zoom, room-to-room transitions
- **Core player mechanics** — how the player moves, what actions they can take, what abilities they have
- **Core game loop** — what the player is trying to do, win/lose conditions, scoring, progression
- **Enemy and NPC behavior** — movement patterns, aggression, interactions, spawning
- **HUD layout** — what information is shown on screen, where it appears, how it updates
- **UI and menus** — start screen, pause menu, game over, inventory, dialogue, transitions
- **Physics feel** — weight, speed, jump feel, gravity, how collision feels to the player
- **Audio and visual feedback** — screen shake, hit-flash, sound cues, particles, animations
- **Level structure** — how levels/rooms/worlds are organized, how the player moves between them
- **Key interactions** — pickups, doors, switches, hazards, special tiles, destructible objects

Write in the voice of an enthusiastic gamer. Use plain language. Describe what the player *sees and feels*, not how it would be coded.

Save this document to:
`.github/game-designs/${{ inputs.game_name }}.md`

---

## Phase 2 — Skill Gap Analysis

**Phase 1 is complete. You may now read repository files.**

Read every skill file in `.claude/skills/`. Use the GitHub tool to list all files matching `.claude/skills/*/SKILL.md` and read each one in full before continuing. Do not skip any.

Once you have read all skills, re-read the game design document you produced in Phase 1.

For each mechanic or system described in the design doc, answer:

1. Is there a skill that covers this system?
2. If yes — does it cover it *well enough*? Would an AI agent following only that skill be able to implement the mechanic without confusion or missing context?
3. If no — what specific guidance is missing that would prevent a coding agent from implementing it correctly?

**Only propose changes where there is a genuine gap** — do not add content just to be thorough. If a skill already covers it, leave it alone.

For each real gap, either:
- **Add to an existing skill** — append a new section or gotcha to the correct skill file
- **Create a new skill stub** — if the mechanic is a distinct system not covered anywhere, create `.claude/skills/{system-name}/SKILL.md` with a stub following the format of existing skills

Apply all proposed changes directly using the `edit` tool to write files. Do not list proposals without acting on them.

Keep a running list of every change you make — you will need it for Phase 3.

---

## Phase 3 — Pull Request

**CRITICAL: Call `create_pull_request` directly yourself — do NOT delegate this to a sub-agent or task tool. Sub-agents do not have access to the `create_pull_request` tool.**

Create a pull request with the following:

**Branch name**: `skill-sync/${{ inputs.game_name }}`

**Title**: `feat: skill sync for ${{ inputs.game_name }} clone`

**Body**:
```
## Game Design Summary

[3–5 bullet points summarizing the key mechanics from the Phase 1 design doc]

## Skill Changes

| Skill File | Change Type | Reason |
|------------|-------------|---------|
| [path] | Added section / New stub / Updated gotcha | [one sentence] |

## Next Step

EDC review is pending. Once the committee approves placement, label this PR `edc-approved` to merge.
```

Label the PR with `edc-review` once created.
