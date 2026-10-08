---
name: docs-writing
description: "Writing FlatRedBall2 user docs in this repo's own docs/ folder (GitBook-synced). Triggers: new FlatRedBall2 tutorial/how-to pages, docs/SUMMARY.md, docs/.gitbook/assets."
---

# FlatRedBall2 Docs Writing Reference

FlatRedBall2's user docs live in **this repo's own `docs/` folder** — not the sibling `FlatRedBallDocs` repo (that's classic FlatRedBall's Glue/Editor-based docs, a different product), and not `.claude/skills/` (AI-facing, not user-facing). `docs/` is already connected to a live GitBook space — its git history has `GitBook:`/`GITBOOK-1` sync commits, so a push to `main` publishes.

## Where FlatRedBall2 Docs Live

- Nav: `docs/SUMMARY.md`, grouped under `##` section headings with nested bullets. A page not listed there is unreachable even if it exists on disk.
- AnimationEditor tool docs (the desktop app's UI) live in `docs/animationeditor/` and `docs/animationeditor/how-to/`. Code docs (loading the file format in a game) live in `docs/animationeditor/api/` under the **Code** entry. Keep the two kinds apart, as Gum does (`Gum/.claude/skills/gum-docs-writing/SKILL.md`, "Tool Docs vs Code Docs").
- Images live in `docs/.gitbook/assets/`, referenced relatively (`../.gitbook/assets/foo.png`).

## Match the Style Already In Use

Images use GitBook's `<figure><img src="..." alt="..."><figcaption></figcaption></figure>` block, as in `docs/animationeditor/quick-start.md`. No page uses `{% hint %}` or `{% tabs %}`; don't introduce them unless the user asks. Heading levels nest normally (`#` → `##` → `###`, no skipped levels). Procedures follow `quick-start.md`: bold UI element names, one action per step.

## No Claudese

Cut these when writing or reviewing docs prose:

- **Em-dash restatement** — "X, not Y" or "X — the Y version" says the same thing twice. State it once.
- **"It's not X, it's Y" framing** — say what it is, don't set up and knock down a strawman.
- **Hedge/filler transitions** — "essentially," "in other words," "that said" — cut them, the next sentence works alone.

## Imperative vs. Descriptive: Match the Sentence to the Job

Two different jobs want two different moods, and mixing them up produces docs that either bury the actual click-through steps or bark commands where an explanation was needed:

- **Procedural steps** (numbered how-to lists) → imperative mood, verb-first: "Click **Add Animation Chain**." "Set `SourceFile` to the `.achx` path."
- **Explanatory prose** (what something does, why it matters) → subject-led, present tense, active voice, with the API/UI element as the grammatical subject: "The `AnimationChainList` stores every chain for an entity" — not "You should store your chains in an `AnimationChainList`." An imperative here hides the actual subject and reads as a command where a reader wanted a fact.
