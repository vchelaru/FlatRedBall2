# FlatRedBall2 — Plan Index

This is the table of contents for multi-phase work in this repo. Each entry below points at an
**initiative folder** containing one markdown document per phase.

Use this folder for work that is too large for a single PR and needs to be sequenced across
several independently-shippable chunks. One-off changes do not need a plan document — just do
the work.

## How this folder is organized

```
plan/
  plan.md                                  <- this file (the index)
  <issue#>-<initiative-slug>/              <- one folder per initiative/epic
    phase-01-<slug>.md
    phase-02-<slug>.md
    ...
```

Rules:

- **`plan.md` is the index, never the content.** One row per phase, linking to the phase doc.
  Never inline phase detail here.
- **One document per phase.** A phase doc is self-contained: it restates the problem, the
  proposed resolution, and every step as a checkbox. A reader should not need the GitHub issue
  open to work the phase.
- **Phase docs are living.** Check boxes off as work lands. Add discovered work as new checkboxes
  rather than silently expanding an existing one.
- **Write the next phase doc when the previous phase is stable**, not up front. Writing all
  phases before any code exists produces documents that are wrong by the time they are read.
- **Status values:** `Not started` / `In progress` / `Blocked` / `Landed`. Update the row here
  when a phase's status changes.
- **When an initiative completes**, keep the folder — phase docs are the record of *why* a
  design turned out the way it did. Mark every row `Landed`.

Relationship to the other markdown folders:

| Folder | Holds |
|---|---|
| `plan/` | Multi-phase implementation plans (this folder) |
| `design/TODOS.md` | Small, actionable open items that don't warrant a plan |
| `design/*.md` | Design write-ups for a single subsystem |
| `.claude/designs/` | Game design documents for sample games |

## Initiatives

### [Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2](804-glue-project-loader/)

Tracking issue: [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804)

Flip the FRB1/Glue relationship: instead of Glue generating C# that FRB1 compiles, FRB2 loads
the JSON project files Glue produces and builds Screens/Entities from them directly, at runtime,
via reflection. No codegen step.

| Phase | Document | Status |
|---|---|---|
| 1 | [Foundations + Screens/Entities skeleton](804-glue-project-loader/phase-01-foundations.md) | Implemented |
| 2 | [NamedObjects](804-glue-project-loader/phase-02-namedobjects.md) | Implemented |
| 3 | [CustomVariables](804-glue-project-loader/phase-03-customvariables.md) | Implemented |
| 4 | [Referenced files / assets](804-glue-project-loader/phase-04-referenced-files.md) | Implemented |
| 5 | [Gum integration](804-glue-project-loader/phase-05-gum-integration.md) | Planned |
| 6 | [Inheritance](804-glue-project-loader/phase-06-inheritance.md) | Implemented |
| 7 | [States & categories](804-glue-project-loader/phase-07-states.md) | Implemented |
| 8 | [Factories / spawning](804-glue-project-loader/phase-08-factories.md) | Partly implemented — spawning by name; pooling deferred |
| 9 | [Collision relationships](804-glue-project-loader/phase-09-collision-relationships.md) | Implemented |
| 10 | [Tiled (TMX)](804-glue-project-loader/phase-10-tiled.md) | Partly implemented |
| 11 | [Top-down movement](804-glue-project-loader/phase-11-topdown-movement.md) | Partly implemented — CSV reader and value mapping; input wiring deferred |
| 12 | [Platformer movement](804-glue-project-loader/phase-12-platformer-movement.md) | Implemented — values reach the behaviour; input wiring deferred |
| 13 | [Camera / display setup](804-glue-project-loader/phase-13-camera-display.md) | Implemented |
| 14 | [Name-based navigation and instantiation API](804-glue-project-loader/phase-14-navigation-api.md) | Partly implemented — project context and creation by name |

**Status of the epic: 9 of 14 phases fully implemented, 4 partly (8, 10, 11, 14), 1 not started (5).**
A project loads; its shapes and sprites appear at the authored size and position; the values an
author tuned in Glue's variable grid are applied, including ones that tunnel into a contained
object; a derived element arrives as the union of its inheritance chain; states apply; sprites load
their textures and animations; collision relationships and camera/display setup both work.
DoorsDemo's door renders from its `.glej` with no hand-written C#, and `Level1`'s tile map draws
with collision built from the authored tile types.

**What's left before a level plays:** platformer/top-down input wiring (Phases 11/12 apply values to
the movement behaviour but don't wire input yet), factory pooling (Phase 8), the rest of the
name-based navigation API — indexer, XML docs, skill file (Phase 14) — and Gum integration
(Phase 5, not started).

The phase list mirrors issue #804 and **is not exhaustive** — expect it to grow as implementation
surfaces schema corners the issue did not anticipate. Add new phases rather than cramming unrelated
work into an existing one.

### Conventions inside this initiative

- **Gotchas are numbered `G<n>` and never renumbered.** Ranges are reserved per phase — Phase 1 owns
  G1–G19, Phase 2 G21–G26, then G30–G39 for Phase 3, G40–G49 for Phase 4, and so on to G140–G149 for
  Phase 14. Cross-phase references are common and must stay stable.
- **Decisions are numbered `D<n>`** in the same scheme: D1–D14 for Phases 1–2, then D30+ per phase.
- **Each phase doc carries a progress metric.** The unmapped-type warning count is pinned by a test
  (`tests/FlatRedBall2.Tests/Glue/GlueTypeMapTests.cs:68`), and each phase records what it drives the
  count down to. Current: DoorsDemo **4**, down from 13 at Phase 2. Note Phase 6 *raised* it to 18
  first — its derived start-up screen began honestly carrying all nine objects it inherits — and
  Phases 10, 9 and 13 then took it to 4. What remains has no FRB2 type at all (`Text`, `ShapeCollection`).

### Fixture caveat that affects Phases 7, 8 and 11

The richest FRB1 reference data is **not** in `Samples/` — it is `Tests/TestProjectDesktopNet6/`,
which carries 44 derived elements (including three-level chains), categorized states, pooled/
not-pooled factory combinations, and the only top-down entities in the repo. Issue #804 states that
no sample uses `BaseEntity` and suggests authoring a fixture; that is true of `Samples/` and wrong
repo-wide.

**The catch:** that project is `FileVersion` 54 and writes `SourceClassType` in **short form**
(`Sprite`, `Circle`, `PositionedObjectList<T>`) in 111 files, while `GlueTypeMap` keys only on the
fully-qualified form. It mixes both forms, so it is not a clean version gate. Any phase vendoring
from it must either teach the type map to accept short names or hand-edit the vendored copies and
record that in the fixtures README.
