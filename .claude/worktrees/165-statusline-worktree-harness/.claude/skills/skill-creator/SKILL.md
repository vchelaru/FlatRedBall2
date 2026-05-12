---
name: skill-creator
description: Create new skills, modify and improve existing ones. Use when the user wants to create a skill from scratch, update an existing skill, or revise a skill based on friction reports from a game-building session.
---

# Skill Creator

Create and maintain skills for FlatRedBall2. The philosophy below overrides generic skill-authoring advice — follow it.

## FRB2 Skill Philosophy

**Skills are concept + gotchas + roadsigns.** They route the agent to the right class, call out non-obvious behavior, and flag footguns. They do *not* teach C#, restate XML docs, or walk through standard programming tasks.

**Every line costs context on every invocation, in every future session.** The entire set of skills competes for the same budget. Prune accordingly.

### Do

1. **Lead with roadsigns.** The top of the skill should answer "for task X, go to class/method Y." Agents come here to be routed.
2. **Document gotchas.** Non-obvious behavior, order-of-operations requirements, silent failures, things that contradict intuition. This is the highest-value content.
3. **Name non-obvious members the agent wouldn't guess exist.** One-liner: "`TileShapes` has `Raycast` for line-of-sight checks." Saves a file read.
4. **Cross-reference when a workflow spans skills.** "For attack hitboxes, combine this with `isDefaultCollision: false` from `entities-and-factories`."
5. **Prefer prose over code.** A sentence that names the types and the sequence is usually enough.
6. **Treat "names mislead, here's the incantation" as API feedback, not skill content.** FRB2 is not public — the engine is expected to churn. File the API fix; do not paper over a misleading name with a code sample.
7. **Explain the why.** Firm language backed by consequence ("the engine reference isn't injected until factory creation") works. Firm language without reason feels arbitrary and the model may ignore it.

### Don't

1. **Don't duplicate XML docs.** If a property's purpose is clear from its XML comment, do not restate it. The agent sees the XML when it reads the class.
2. **Don't include code samples for single-class usage.** `sprite.Texture = ...` does not need a snippet — the name carries it.
3. **Don't write step-by-step walkthroughs for standard programming tasks.** State machines, cooldowns, list iteration — agents can do these.
4. **Don't include game-specific logic.** Score systems, wave spawning, enemy AI, upgrade trees — not engine knowledge.
5. **Don't add "here's what not to do" sections unless the wrong path is actively tempting.** If no one would reach for it, don't warn against it.
6. **Don't restate what's obvious from the method name.** `Destroy()` destroys. `Add()` adds.
7. **Don't reach for ALWAYS / NEVER / ALL CAPS as emphasis.** If you find yourself doing it, reframe with a reason — the consequence of getting it wrong is more compelling than capitalization.
8. **Don't pad with motivation, encouragement, or flavor.** Every line must pull weight.

### The test before adding anything

"If I remove this line, would the next agent be meaningfully worse off?" If they'd figure it out by reading the class they were already going to read, cut it.

### The test for code samples specifically

A code sample earns its place only if **both** are true:
- The pattern spans 2+ classes in a non-obvious sequence, AND
- Prose describing the sequence would be longer or less clear than the code.

**Hard limit:** 8 lines per `csharp` block. A longer block requires the marker `<!-- skill-creator: allow-long-csharp reason="..." -->` on the preceding line with a concrete reason.

**Pre-save checklist:**
- Does this snippet teach engine behavior or just C# syntax?
- Could 30-70% of the lines be deleted without losing the point?
- Is this pattern already documented in another skill? (Cross-reference instead of duplicating.)

---

## Writing a Skill

### Frontmatter

- **name**: Lowercase, hyphens, max 64 chars. No `anthropic` or `claude`. Prefer gerund (`managing-entities`) or noun phrase (`entity-management`). Avoid `helper`, `utils`, `tools`.
- **description**: Third person. States what the skill does AND when to use it. All "when to use" information lives here, not in the body. Claude tends to undertrigger skills, so descriptions can lean slightly pushy — include trigger phrases and contexts. Example good: "Processes Excel files and generates reports. Use whenever the user mentions xlsx, spreadsheets, or tabular data." Example bad: "I can help with Excel."

### Anatomy and progressive disclosure

```
skill-name/
├── SKILL.md (required)
└── references/ (optional, for overflow content)
```

Three loading levels:
1. **Metadata** (name + description) — always in context
2. **SKILL.md body** — loaded whenever the skill triggers; target under 300 lines
3. **references/** — loaded only when SKILL.md points to them

SKILL.md should trend toward a **router**: a short summary of each topic with a clear pointer to the reference and guidance on when to read it. A 50-line SKILL.md doesn't need references; a 400-line one does. Extract a section when it serves only a subset of the skill's invocations — keeping niche content inline taxes every unrelated invocation.

Keep references one level deep from SKILL.md (`SKILL.md → foo.md`, never `SKILL.md → foo.md → bar.md`). The agent may only preview second-level files and miss content.

### Writing style

Imperative form. Lead with the *why* behind each rule. Prefer explanation over rigid prescription — the model generalizes from reasoning to new edge cases, which is the whole point of using an LLM.

Use firm language when a specific sequence breaks things if violated (lifecycle hooks, initialization order) or when the engine has an opinionated pattern. Use flexible language ("consider", "typically") when multiple approaches are valid. Either way, state the consequence of deviation.

---

## Iterating on an Existing Skill

The human reviews skill output manually — there is no automated eval harness in this repo.

When updating a skill:

1. **Identify the friction.** What did the agent get wrong, or what did it waste time figuring out?
2. **Decide where the fix belongs** using the do/don't rules:
   - Routing or cross-cutting workflow gap → skill edit
   - Single property/method unclear → XML doc edit
   - Name was misleading, needed an "incantation" → **API bug, file it**
3. **Edit in the smallest diff that resolves the gap.**
4. **Re-read the whole skill with fresh eyes** and cut anything that no longer pulls its weight. Skills drift toward bloat; compensate by pruning on every visit.

### Package and return (optional)

If `present_files` is available, package the final skill with `python -m scripts.package_skill <path>` and hand the resulting `.skill` file to the user.
