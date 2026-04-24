---
name: skill-change-logging
description: Log every FlatRedBall2 skill modification to .claude/skill_change_log.jsonl with exact added/removed line counts, motivating sample project, and concise reason. Use whenever creating, editing, deleting, or restructuring any skill.
---

# Skill Change Logging

Use this skill whenever a skill file changes — both the 3rd-party game-dev skills under `ai-reference/` and the 1st-party engine-contributor skills under `.claude/skills/`. Log the canonical path (e.g. `ai-reference/animation/SKILL.md`, not the symlinked path).

## Required Log File

- Path: `.claude/skill_change_log.jsonl`
- Format: one JSON object per line (JSONL)

## When to Log

Always append one entry per changed skill when any of these occur:
- `SKILL.md` content edits
- frontmatter `name` or `description` updates
- new skill creation
- skill deletion
- significant skill restructure (for example, moving sections into references)

Do not skip logging because a change is "small". If behavior, routing, or guardrails changed, log it.

## Required Fields Per Entry

Each JSON object must include:
- `id`: integer UTC epoch milliseconds at write time (append-only friendly)
- `timestamp_utc`: ISO-8601 UTC timestamp at millisecond precision (`YYYY-MM-DDTHH:mm:ss.fffZ`)
- `skill`: skill name (folder name or frontmatter name)
- `skill_file`: repo-relative path
- `change_type`: `create`, `update`, `delete`, or `restructure`
- `lines_added`: integer from `git diff --numstat`
- `lines_removed`: integer from `git diff --numstat`
- `line_ranges_added`: array of 1-based line ranges in the post-change file (e.g. `["42-57","89-89"]`)
- `line_ranges_removed`: array of 1-based line ranges in the pre-change file (e.g. `["40-52"]`)
- `motivating_sample_project`: sample project path that motivated the change, or `"none"` if not sample-driven
- `reason`: concise but complete need statement

## Logging Workflow

1. Make skill edits.
2. Run `git diff --numstat -- <skill-file-paths>`.
3. Run `git diff -U0 -- <skill-file-paths>` and capture hunk line ranges.
4. Set `id` to current UTC epoch milliseconds.
5. Append one JSON line per changed skill to `.claude/skill_change_log.jsonl`.
6. Keep reasons short, specific, and action-oriented.

## ID Rules

- IDs are global across the file (not per skill).
- IDs must be unique; they do not need to be contiguous.
- Use UTC epoch milliseconds to avoid reading the existing log for max-id lookup.
- If two entries are created in the same millisecond, increment by `+1` locally for subsequent entries in that write batch.

## Line Range Rules

- Use 1-based line numbers.
- Use `start-end` strings; for single lines use `N-N`.
- `line_ranges_added` refers to post-change file line positions (`+` hunks).
- `line_ranges_removed` refers to pre-change file line positions (`-` hunks).

## Timestamp Rule

- Use UTC only and include trailing `Z`.
- Normalize to millisecond precision for consistency (for example, `2026-04-22T06:30:00.123Z`).

## Reason Writing Rules

- State the observed friction or risk.
- State why the new instruction prevents repeat failure.
- Avoid paragraphs; 1-2 sentences max.

## Example Entry

```json
{"id":1776840000123,"timestamp_utc":"2026-04-21T00:00:00.123Z","skill":"orchestrator","skill_file":".claude/skills/orchestrator/SKILL.md","change_type":"update","lines_added":8,"lines_removed":2,"line_ranges_added":["117-124"],"line_ranges_removed":["117-118"],"motivating_sample_project":"samples/auto/AutoEvalSimCopterSample","reason":"Full sample build can fail due unrelated engine errors; added fallback guidance to validate generated sample with isolated build and explicit blocker attribution."}
```
