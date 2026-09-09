---
name: docs-writing
description: Writing FlatRedBall2 user docs in this repo's own docs/ folder (GitBook-synced). Triggers: new FlatRedBall2 tutorial/how-to pages, docs/SUMMARY.md, docs/images.
---

# FlatRedBall2 Docs Writing Reference

FlatRedBall2's user docs live in **this repo's own `docs/` folder** — not the sibling `FlatRedBallDocs` repo (that's classic FlatRedBall's Glue/Editor-based docs, a different product), and not `.claude/skills/` (AI-facing, not user-facing). `docs/` is already connected to a live GitBook space — its git history has `GitBook:`/`GITBOOK-1` sync commits, so a push to `main` publishes.

## Where FlatRedBall2 Docs Live

- Nav: `docs/SUMMARY.md` — a flat list today (`Setup`, `Animation Editor`, `Your First Animation`), no nesting yet. A page not listed there is unreachable even if it exists on disk.
- Every page in `docs/` today is an **Animation Editor tool doc** (the desktop app's UI — see `your-first-animation.md`). A runtime/code doc (loading a file format in a game, an engine API) is a different kind of page and needs its own new section in `SUMMARY.md` — mirrors Gum's own tool-docs-vs-code-docs split (`Gum/.claude/skills/gum-docs-writing/SKILL.md`, "Tool Docs vs Code Docs"). Don't fold a code doc into the Animation Editor section.
- Images live in a flat `docs/images/`, referenced with a plain relative path (`images/foo.png`). There is no `.gitbook/assets/` folder or path convention here — that's specific to Gum/FlatRedBallDocs' GitBook setup; don't port it.

## Match the Plain-Markdown Style Already In Use

`docs/` uses plain GitHub-flavored markdown — no `{% hint %}`, `{% tabs %}`, or `<figure>` GitBook syntax appears anywhere in it yet. Don't introduce those unless the user asks for them. Heading levels nest normally (`#` → `##` → `###`, no skipped levels). For procedure style, match `your-first-animation.md`'s "Steps" section: numbered steps, bold UI element names, one action per step.

## No Claudese

Cut these when writing or reviewing docs prose:

- **Em-dash restatement** — "X, not Y" or "X — the Y version" says the same thing twice. State it once.
- **"It's not X, it's Y" framing** — say what it is, don't set up and knock down a strawman.
- **Hedge/filler transitions** — "essentially," "in other words," "that said" — cut them, the next sentence works alone.

## Imperative vs. Descriptive: Match the Sentence to the Job

Two different jobs want two different moods, and mixing them up produces docs that either bury the actual click-through steps or bark commands where an explanation was needed:

- **Procedural steps** (numbered how-to lists) → imperative mood, verb-first: "Click **Add Animation Chain**." "Set `SourceFile` to the `.achx` path."
- **Explanatory prose** (what something does, why it matters) → subject-led, present tense, active voice, with the API/UI element as the grammatical subject: "The `AnimationChainList` stores every chain for an entity" — not "You should store your chains in an `AnimationChainList`." An imperative here hides the actual subject and reads as a command where a reader wanted a fact.
