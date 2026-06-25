---
name: skill-creator
description: FlatRedBall2 skill authoring — the map-and-landmines philosophy and the 20% damped-growth rule. Triggers: writing or revising a SKILL.md, splitting a bloated skill, friction reports from a build session.
---

# Skill Creator

Create and maintain skills for FlatRedBall2. The philosophy below overrides generic skill-authoring advice — on any conflict, this file wins.

## Mental Model

A skill is **a map and a list of landmines**, not an encyclopedia. It points the agent at the right class/method/doc and warns about what isn't obvious from reading them. It does *not* teach C#, restate XML docs, or walk through standard programming tasks. If a fact already lives in source or an XML doc, **point at it, don't restate it.**

A good skill answers three things and stops: **where** the relevant code lives, **what gotchas** aren't obvious from reading it, and **what patterns** recur. Every line is re-read into context on every load, in every future session, and the whole skill set competes for one budget — a skill that says *less* but points *accurately* beats a thorough one.

## Growing a Skill — Damped Response (the "20% rule")

A skill is rarely written whole; it grows as questions pull on it. **Don't answer a question 100% inside the skill.** Treat demand as an elastic pull and the skill as an object resting in sand: a question pulls toward a fuller answer, the skill responds **damped** (moves part-way, not all the way), and **retains** its new position — the sand means it doesn't snap back. Over many questions this settles the skill at the best *average* of real demand without overfitting to any one question. It's a leaky integrator of demand, not a transcript of the last conversation.

**Default: a 100% pull moves ~20%.** When a question *could* be answered in full inside the skill, add only its broad orienting fifth — a concrete signpost plus a one-sentence shape of the answer — not the whole walkthrough. Because the position is retained, a genuinely recurring question reaches full coverage in a few pulls, while a one-off never bloats the skill past its signpost. This is also why a thin, broad skill is cheap and reversible: easy to add, easy to delete if demand never returns.

Why damped: chasing every specific detail down into the skill bloats it, scatters its focus, rots, and front-loads context future agents won't need. Most questions are one-offs. Let the skill grow only where demand actually, repeatedly pulls.

**Two exceptions — place these by hand, at full strength, not through the elastic:**

1. **Landmines.** A non-obvious, expensive-to-rediscover gotcha that *isn't* evident from the source you point at is a sharp fact, not a sample to be averaged. Damping it smears a precise truth into a vague gesture — the worst outcome. State it fully and firmly.
2. **Bimodal pull.** When a skill is dragged toward a low-density middle between two genuinely distinct sub-topics, don't settle in the valley — it serves neither. Split into two skills, each with its own focus. A pull toward the empty middle *is* the signal to fission.

**Signpost quality bar.** A nudge must name *where to look* — a file, class, or relationship — not merely assert that something exists. "Frame color interacts with the preview" raises a question without reducing search cost; "see the tint path in `<Control>.cs` — per-frame RGB multiplies because `<reason>`" reduces it. A vague signpost is worse than none: it costs context and resolves nothing.

## Do

1. **Lead with roadsigns.** The top should answer "for task X, go to class/method Y."
2. **Document gotchas.** Non-obvious behavior, order-of-operations, silent failures, things that contradict intuition. Highest-value content, and exempt from damping — see Landmines.
3. **Name non-obvious members the agent wouldn't guess exist.** "`TileShapes` has `Raycast` for line-of-sight checks." Saves a file read.
4. **Cross-reference when a workflow spans skills.** "Combine with `isDefaultCollision: false` from `entities-and-factories`." Two sub-topics that keep cross-referencing into a low-density middle are a fission candidate (see Bimodal pull).
5. **Prefer prose over code.** A sentence that names the types and the sequence is usually enough.
6. **Treat "names mislead, here's the incantation" as API feedback, not skill content.** FRB2 is not public — the engine is expected to churn. File the API fix; do not paper over a misleading name with a code sample.
7. **Explain the why.** Firm language backed by consequence ("the engine reference isn't injected until factory creation") works; firm language without reason feels arbitrary and the model may ignore it.

## Don't

1. **Don't restate what already lives in source or XML docs.** The agent sees the XML when it reads the class. Point, don't copy.
2. **Don't include code samples for single-class usage.** `sprite.Texture = ...` — the name carries it.
3. **Don't write step-by-step walkthroughs for standard programming tasks.** State machines, cooldowns, list iteration — agents can do these.
4. **Don't include game-specific logic.** Score systems, wave spawning, enemy AI, upgrade trees — not engine knowledge.
5. **Don't include time-sensitive info** — versions, dates, migration notes. It rots, and the skill is re-read forever.
6. **Don't add "here's what not to do" sections unless the wrong path is actively tempting.**
7. **Don't restate what's obvious from the method name.** `Destroy()` destroys.
8. **Don't reach for ALWAYS / NEVER / ALL CAPS as emphasis.** Reframe with the consequence — it lands harder than capitalization.
9. **Don't pad with motivation, encouragement, or flavor.** Every line must pull weight.

### The test before adding anything

"If I remove this line, would the next agent be meaningfully worse off?" If they'd figure it out by reading the class they were already going to read, cut it. This is the damped default as a checklist: most pulls don't clear the bar at full strength — landmines do.

### Code samples

A code sample earns its place only if **both** are true:
- The pattern spans 2+ classes in a non-obvious sequence, AND
- Prose describing the sequence would be longer or less clear than the code.

**Hard limit:** 8 lines per `csharp` block. A longer block requires the marker `<!-- skill-creator: allow-long-csharp reason="..." -->` on the preceding line with a concrete reason.

Pre-save check: Does this snippet teach engine behavior or just C# syntax? Could 30–70% of its lines be cut? Is it already in another skill (cross-reference instead)?

## Frontmatter

- **name**: lowercase kebab-case, max 64 chars, no `anthropic`/`claude`. Prefer a gerund (`managing-entities`) or noun phrase (`entity-management`). Avoid `helper`, `utils`, `tools`.
- **description**: its **only job** is to tell future-Claude *when this skill is relevant* — a trigger, not a summary. It's loaded into every session's skill listing, so it pays context forever.
  - **One sentence, under ~250 chars** (ideally under 200). The body covers the rest.
  - **Drop boilerplate** — no "Reference guide for…", "Load this when…", "Covers FRB2's…". That it's a skill is implicit; those phrases are dead weight on every entry.
  - **Lead with the topic, then triggers:** `<Topic> — <one-line hook>. Triggers: <3–8 distinctive identifiers, file paths, or scenarios>.`
  - Pick the *distinctive* triggers — class names, file paths, method names. Generic words ("file", "system", "behavior") don't help; the rest belongs in the body.

  Good: `description: A* enemy navigation on a tile grid. Triggers: TileNodeNetwork, TileNode, aligning nodes with TileShapes.`
  Bad: `description: Reference guide for pathfinding. Load this when working on enemy navigation, A*, TileNodeNetwork, TileNode, grids, or movement.`

## Anatomy and progressive disclosure

```
skill-name/
├── SKILL.md (required)
└── references/ (optional, for overflow content)
```

Three loading levels:
1. **Metadata** (name + description) — always in context.
2. **SKILL.md body** — loaded whenever the skill triggers. Aim under 100 lines; hard ceiling 500.
3. **references/** — loaded only when SKILL.md points to them.

SKILL.md trends toward a **router**: a short summary of each topic with a pointer to the reference and when to read it. Extract a section into `references/` when it serves only a subset of invocations — keeping niche content inline taxes every unrelated load. Keep references one level deep (`SKILL.md → foo.md`, never `→ bar.md`); the agent may only preview second-level files and miss content.

## Writing style

Imperative. Lead with the *why* behind each rule — the model generalizes from reasoning to new edge cases, which is the whole point of using an LLM. Use firm language when a sequence breaks things if violated (lifecycle hooks, init order) or the engine has an opinionated pattern; use flexible language ("consider", "typically") when multiple approaches are valid. Either way, state the consequence of deviation.

## Process — creating or iterating

There is no automated eval harness; the human reviews skill output by hand.

1. **Read the ground truth first** — the relevant source and its XML docs — so you can point instead of restate.
2. **Skim a few existing skills** under `frb-skills/` and `.claude/skills/` to match style and depth.
3. **Write only the non-obvious distillation.** Apply the damped default; place landmines at full strength.
4. **When updating**, identify the friction (what did the agent get wrong or waste time on?), then decide where the fix belongs — routing/cross-cutting gap → skill edit; single member unclear → XML doc edit; misleading name needing an "incantation" → **API bug, file it** — and move the skill ~20% toward it.
5. **Re-read the whole skill with fresh eyes** and cut anything that no longer pulls its weight. Skills drift toward bloat; prune on every visit.

### Package and return (optional)

If `present_files` is available, package the final skill with `python -m scripts.package_skill <path>` and hand the resulting `.skill` file to the user.
