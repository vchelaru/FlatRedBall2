# Repository Guidelines

## What Is This?

FlatRedBall2 is a 2D game engine/framework written in C# on .NET, built on top of MonoGame. It integrates Gum (UI) and Tiled (level editing) as dependencies. The project is currently in the architecture/design phase — see `ARCHITECTURE.md` for the full design.

## Agent Workflow

For every task, invoke the appropriate agent from `.claude/agents/` before proceeding. The agent's instructions provide guidelines for how the task should be performed. Before doing any work, announce which agent you are using such as "Invoking coder agent for this task..."

Available agents:
- **coder** — Writing or modifying code and unit tests for new features or bugs
- **qa** — Testing, reviewing changes, and verifying correctness
- **refactoring-specialist** — Refactoring and improving code structure
- **docs-writer** — Writing or updating documentation
- **product-manager** — Breaking down tasks and tracking progress
- **security-auditor** — Security reviews and vulnerability assessments

Select the agent that best matches the task at hand. For tasks that span multiple concerns (e.g., implement a feature and write tests), invoke the relevant agents in sequence.

## Code Style

See `.claude/code-style.md` for all code style rules. Read that file before writing or editing any code.
