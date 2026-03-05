# Agent Pipeline Review — Issues Found

Review of all agent files, CLAUDE.md, skills, and cross-references.
Conducted by simulating a game creation flow (game-designer -> product-manager -> coder) and auditing all documentation for consistency.

---

## Critical Issues

### 1. Coder agent contradicts itself about `samples/`

**Files:** `coder.md:22` and `coder.md:56`

Line 22 says: "Do not look for existing patterns and conventions in `samples/`"
Line 56 says: "Working game samples (reference these for patterns before inventing new ones)"

These directly contradict each other. An agent following line 56 would violate line 22.

**Fix:** Remove the "reference these for patterns" language from line 56. Keep it as a neutral structural note: `- samples/ — Working game samples`

---

### 2. No instruction tells agents where to create game projects

**Files:** `game-designer.md`, `product-manager.md`, `coder.md`

The game-designer saves the GDD to `.claude/designs/` (correct). But nothing in the pipeline tells the product-manager or coder: "when building a game from a GDD, create the project under `samples/` using the `sample-project-setup` skill."

The `sample-project-setup` skill exists and is thorough, but no agent is told to invoke it. The connection is severed.

**Fix:** Add to `product-manager.md` (in the Engine Skill Awareness section):
> "When breaking down a game project, the first task should always be: Create the sample project under `samples/` (see `sample-project-setup` skill)."

Also add to `coder.md` (in Before editing):
> "For new game projects, create the project under `samples/` using the `sample-project-setup` skill before writing any game code."

---

### 3. Game-designer agent has unnecessary code-browsing tools

**File:** `game-designer.md:4`

The game-designer's tool list is `Read, Grep, Glob, Write, Bash`. It has full codebase browsing access despite being a pure design role that should never read source code. Its own instructions say "do not ask about technology choices, engines, frameworks" — but it has the tools to browse the engine anyway.

When run as a subagent, it may speculatively read engine source to "understand what's possible," which defeats the AI-usability test.

**Fix:** Reduce tool list to `Write, Bash`. It only needs `Write` to save the GDD and `Bash` to run `start ""` for opening the file.

---

## Moderate Issues

### 4. `platformer-movement` skill exists but is not listed in CLAUDE.md

**Files:** `CLAUDE.md` (Available Skills section), `.claude/skills/platformer-movement/SKILL.md`

The skill file exists, but CLAUDE.md's skill list (lines 23-36) does not include `platformer-movement`. Any agent reading CLAUDE.md to know which skills to invoke will never discover it.

**Fix:** Add to CLAUDE.md's Available Skills list:
> `- platformer-movement — Platformer mechanics, jumping, ground detection, PlatformerBehavior`

---

### 5. Coder agent references non-existent "Engine Structure" section in CLAUDE.md

**File:** `coder.md:54`

Line 54 says: "See CLAUDE.md 'Engine Structure' for the full file tree."

CLAUDE.md has no section called "Engine Structure." The closest is "Key Architecture Decisions" but it doesn't include a file tree.

**Fix:** Either add an "Engine Structure" section to CLAUDE.md with the file tree, or change coder.md to reference what actually exists: "See CLAUDE.md 'Key Architecture Decisions' for architecture context."

---

### 6. `code-style.md` references non-existent `ARCHITECTURE.md`

**File:** `.claude/code-style.md:3`

Line 3 says: "See `ARCHITECTURE.md` for the current design direction."

No `ARCHITECTURE.md` file exists in the repo. This is a dead reference.

**Fix:** Remove the reference or create the file.

---

### 7. `start ""` command won't work from subagents

**Files:** `game-designer.md:91`, `product-manager.md:35`

Both agents instruct: open the file with `start "" "<path>"` via Bash. But when these agents run as subagents (via the Agent tool), they run in a subprocess. The `start` command will execute, but the user won't necessarily see it — it depends on the execution environment. Additionally, the Agent tool's result is returned as text to the parent, not shown directly to the user.

This is a minor issue in practice (the parent agent can relay the path), but the instruction creates false confidence that the file was "shown" to the user.

**Fix:** Change to: "Save the file and report the full path. When running as a top-level agent, also open it with `start "" "<path>"` via Bash." Or simply: "Save the file and report the full path in your output."

---

### 8. Game-designer removed handoff but product-manager still has one

**Files:** `game-designer.md` (end), `product-manager.md:74-76`

The maintainer removed the game-designer's "Handoff" section (hand off to product-manager), but the product-manager still has "Hand off to coder agent for implementation." This creates a broken chain:

- CLAUDE.md says game creation flow is: game-designer -> product-manager -> coder
- Game-designer no longer says to hand off to product-manager
- Product-manager says to hand off to coder

The game-designer now just ends with "Does this capture what you're going for?" with no direction about what happens next. The parent agent (or user) must know to invoke the product-manager next.

**Impact:** Low if the parent agent follows CLAUDE.md's workflow. But if the game-designer is invoked directly (not as a subagent), there's no breadcrumb for the user.

**Fix:** Either restore a subtle handoff hint in game-designer ("When you're happy with the design, the next step is task breakdown with the product-manager.") or accept that CLAUDE.md is the source of truth for the pipeline and agents don't need to know about each other.

---

## Minor Issues

### 9. Product-manager says "explore ideas thoroughly" but is used as a task-breakdown tool

**File:** `product-manager.md:17`

The Exploration Process section says to "engage in thorough back-and-forth with the user" and "ask questions to understand requirements deeply." But in the game creation pipeline, the PM receives a finished GDD and just needs to break it into tasks. The exploratory questioning mode doesn't fit this use case.

**Observation:** The PM agent is designed for two different jobs (feature exploration vs. game task breakdown) but the instructions optimize for the former. When invoked after a GDD, it may waste tokens asking questions that the GDD already answers.

**Fix:** Add a note: "If a Game Design Document already exists, skip the exploration process and proceed directly to task breakdown."

---

### 10. No agent is told to build/verify the project compiles

**Files:** All agent files

The coder agent says "The user will build and run tests themselves — do not run them via Bash." The QA agent reviews code but doesn't build. No agent in the pipeline verifies that the generated game project actually compiles.

For the AI-usability testing goal, a non-compiling game is a failed test. There's no checkpoint.

**Observation:** This may be intentional (the user builds manually). But it means a full game-creation pipeline can produce broken code with no feedback loop.

---

### 11. `.claude/designs/` accumulates stale files

**Observation:** The designs folder currently has 12 files from various design sessions. Nothing cleans these up. Over time this becomes noise.

**Suggestion:** Add a note somewhere that design files are ephemeral and can be deleted after implementation, or add a `.gitignore` entry if they shouldn't be committed.

---

### 12. QA agent says "use edit and execute only for creating minimal test files" but has no Edit or Write tools

**File:** `qa.md:21`

Line 21 says: "Use edit and execute only for creating minimal test files to verify/reproduce issues"
But the QA agent's tool list (line 4) is: `Read, Grep, Glob, Bash` — no `Edit` or `Write`.

The instruction references capabilities the agent doesn't have.

**Fix:** Either add `Edit, Write` to the tool list, or remove the instruction about creating test files.

---

## Summary

| # | Severity | Issue | Files |
|---|----------|-------|-------|
| 1 | Critical | Coder contradicts itself on samples/ | coder.md |
| 2 | Critical | No instruction to create games in samples/ | game-designer, PM, coder |
| 3 | Critical | Game-designer has unnecessary code-browsing tools | game-designer.md |
| 4 | Moderate | platformer-movement skill missing from CLAUDE.md | CLAUDE.md |
| 5 | Moderate | Coder references non-existent "Engine Structure" section | coder.md, CLAUDE.md |
| 6 | Moderate | code-style.md references non-existent ARCHITECTURE.md | code-style.md |
| 7 | Moderate | `start ""` won't work reliably from subagents | game-designer, PM |
| 8 | Moderate | Broken handoff chain (game-designer -> PM) | game-designer.md |
| 9 | Minor | PM explores when it should just break down tasks from GDD | product-manager.md |
| 10 | Minor | No build verification step in pipeline | all agents |
| 11 | Minor | Stale design files accumulate | .claude/designs/ |
| 12 | Minor | QA references tools it doesn't have | qa.md |
