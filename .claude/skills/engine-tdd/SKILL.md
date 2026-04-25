---
name: engine-tdd
description: "Test-first discipline for FlatRedBall2 engine changes. Triggers whenever editing any file under src/ for a behavior change (bug fix or feature). Not for XML docs, renames, style-only edits, or sample code."
---

# Engine Changes Require a Failing Test First

Behavior changes in `src/` require a failing test in `tests/FlatRedBall2.Tests/` **before** the source edit. Write it, run it, watch it fail, then fix.

No "the cause is obvious, I'll skip the test" exception — that reasoning is how silent regressions ship. If you're about to edit `src/` without a failing test open, stop.

Exceptions: XML docs, style-only edits, pure renames, dead-code removal.

## API Design — Flag Before Implementing

Before adding any new `public` or `virtual` member to an engine base class (`Screen`, `Entity`, `FlatRedBallService`, etc.), stop and flag it as an API design decision. Ask before writing code. New public/virtual surface is a footgun risk — it implies intent to users, shows up in IntelliSense, and is hard to remove once shipped.
