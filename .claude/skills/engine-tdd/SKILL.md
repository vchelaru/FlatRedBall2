---
name: engine-tdd
description: "Test-first discipline for FlatRedBall2 engine changes. Triggers whenever editing any file under src/ for a behavior change (bug fix or feature). Not for XML docs, renames, style-only edits, or sample code."
---

# Engine Changes Require a Failing Test First

Behavior changes in `src/` require a failing test in `tests/FlatRedBall2.Tests/` **before** the source edit. Write it, run it, watch it fail, then fix.

No "the cause is obvious, I'll skip the test" exception — that reasoning is how silent regressions ship. If you're about to edit `src/` without a failing test open, stop.

Exceptions: XML docs, style-only edits, pure renames, dead-code removal.

## Shaders, Graphics, Color Values — Manual Test Is Never Skippable

Any change touching a shader, blend state, render target, or pixel color math requires either a pixel-verification test or the user manually checking rendered output — never "manual test: not needed." Passing unit tests on surrounding math (offset clamping, parameter selection) does not cover this.

For render-affecting `src/` changes, add a golden scene test and regenerate its PNG with the `golden-images-regenerate.yml` workflow (CI output only, never a local render). How: `tests/FlatRedBall2.GoldenImage.Tests/README.md`.

## API Design — Flag Before Implementing

Before adding any new `public` or `virtual` member to an engine base class (`Screen`, `Entity`, `FlatRedBallService`, etc.), stop and flag it as an API design decision. Ask before writing code. New public/virtual surface is a footgun risk — it implies intent to users, shows up in IntelliSense, and is hard to remove once shipped.
