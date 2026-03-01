# Repository Guidelines

## What Is This?

FlatRedBall2 is a 2D game engine/framework written in C# on .NET, built on top of MonoGame. It integrates Gum (UI) and Tiled (level editing) as dependencies. The project is currently in the architecture/design phase — see `ARCHITECTURE.md` for the full design.

## AI-Usability Goals

This project serves dual purposes: building a game engine AND evaluating how well AI assistants can work with it. **Game samples are not just games — they are AI usability tests for FlatRedBall2.**

Three layers of AI-usability (in priority order):

1. **API design** — Is the API clear, intuitive, and hard to misuse?
2. **XML documentation** — Is it succinct, adds clarification beyond the name, avoids redundancy, and calls out gotchas?
3. **Skills/command files** — Do they guide to the right location, explain high-level concepts, and flag gotchas?

### Post-Task Reflection (Required for Game Dev Tasks)

After completing any game development task, reflect and suggest concrete improvements:
- Did completing this task require excessive context or guesswork?
- Would a cleaner API design have prevented confusion?
- Are there missing, unclear, or redundant XML doc comments?
- Should a skill/command file be created or updated?

Make suggestions even if minor. **High churn on docs and skills is expected and desired — we want it perfect.**

### Keeping Docs and Skills Accurate (Critical)

Because churn is high, XML docs and skill files can easily become out-of-date. **If you ever encounter anything inaccurate or outdated in XML docs or skill files while working on any task, flag it immediately and fix it.** Stale guidance is worse than no guidance — it actively misleads future AI sessions.

## Agent Workflow

For every task, invoke the appropriate agent from `.claude/agents/` before proceeding. The agent's instructions provide guidelines for how the task should be performed. Before doing any work, announce which agent you are using such as "Invoking coder agent for this task..."

Available agents:
- **coder** — Writing or modifying code and unit tests for new features or bugs
- **qa** — Reviewing production code for correctness, edge cases, and regressions (does not write tests); also assists with manual testing and playtest checklists
- **refactoring-specialist** — Refactoring and improving code structure
- **docs-writer** — Writing or updating documentation
- **product-manager** — Breaking down tasks and tracking progress
- **security-auditor** — Security reviews and vulnerability assessments

Select the agent that best matches the task at hand. For tasks that span multiple concerns (e.g., implement a feature and write tests), invoke the relevant agents in sequence.

## Code Style

See `.claude/code-style.md` for all code style rules. Read that file before writing or editing any code.
