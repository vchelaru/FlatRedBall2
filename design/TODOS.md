# FlatRedBall2 — Todo

**No speculative items.** Every entry must be either (a) ready to work on now or (b) ready to discuss now. "Eventually," "someday," "maybe if it comes up" do not belong here — they're noise that buries real work and never gets revisited. If an idea is interesting but not actionable, let it surface again organically when a real use case appears; don't pre-emptively log it.

Open work only. When an item ships, delete it — don't leave a "landed" breadcrumb. Design decisions and historical context that outlive a TODO belong in skill files, XML docs, or commit messages, not here.

## Native AOT
**Priority: Eventual** — reflection-based code blocks AOT publishing.

- `TileMap.CreateEntities` uses reflection to map Tiled custom properties onto entity fields — needs a source generator or explicit-mapping path
- Other `Activator.CreateInstance` / `MakeGenericMethod` sites must be audited and replaced
- Flag any new reflection-heavy or AOT-hostile code for future cleanup

**How to test:** cheapest first step is `<IsAotCompatible>true</IsAotCompatible>` on `FlatRedBall2.csproj` — this turns on the AOT analyzers at regular `dotnet build` time, so every reflection site surfaces as an `IL2026`/`IL2070`/`IL2075`/`IL3050` warning without needing to publish. Full validation is `<PublishAot>true</PublishAot>` on a sample executable (e.g. a desktop launcher for ShmupSpace) followed by `dotnet publish -c Release -r win-x64`, then running the published binary and exercising the reflection codepaths (tilemap load, entity spawning) — AOT publishes with warnings, so runtime exercise is what confirms a path actually works.

## Documentation Site
**Priority: Soon** — Stand up a public docs site for FlatRedBall2. Today all guidance lives in skill files (AI-facing, in-repo) and inline XML docs; a human-facing site is the missing third leg.

Open questions:
- **Content sourcing.** Large portions of the skill files are high-quality prose that could seed the docs (e.g. "entities and factories," "collision relationships," "content-boundary"). Tension: skills are AI-optimized (terse, bullet-heavy), docs are human-optimized (more narrative, more examples). Do we fork the content, cross-reference, or generate one from the other?

## Multi-pass collision resolution for dense clusters
**Priority: Soon** — `CollisionRelationship.RunCollisions` resolves each pair once per frame. In tightly packed piles (validated visually with `AutoEvalBallPartitionSample` at 1500+ balls under gravity), one pass cannot fully separate balls that have multiple overlapping neighbors, so residual overlaps remain visible frame after frame.

- Add a `MaxIterations` setting on `CollisionRelationship` (default 1, opt in to higher for piles).
- Each iteration runs the pair sweep again over the *currently-overlapping* set; terminate early when an iteration produces zero separation movement.
- Validate against `AutoEvalBallPartitionSample` — overlaps in the pile should visibly disappear at MaxIterations=4 or so.
- Watch out: the simple "rerun the same sweep" approach fires `CollisionOccurred` / `CollisionStarted` events per-iteration, which is wrong. Need to gate event firing to only the first iteration (or hoist event firing out of the inner pair-resolution loop).

This is the proper fix for the "balls visibly overlap in dense piles" symptom. Single-pass impulse resolution is a fundamental limitation, not a partition bug — verified by `PartitionAxis_DenseClusterOf30Balls_CoversSameUniquePairsAsNaive` test.

## CollisionRelationship per-pair inner-loop optimization
**Priority: Soon** — `AutoEvalBallPartitionSample` profiling shows ~150ns/check in Release for circle-vs-circle ball pairs. The broad-phase prunes correctly (50k checks against a 1.9M naive baseline); the cost is per-check overhead. Concrete optimization candidates:

- **Skip `RecordContact` when no `CollisionStarted` / `CollisionEnded` subscribers.** Currently runs a `HashSet<(A,B)>` insert on every overlapping pair regardless of whether anyone listens. Tuple boxing + hashing is a meaningful chunk of the per-check cost. Gate behind a "has subscriber" boolean.
- **Inline the common Circle-vs-Circle case** in `CollisionDispatcher.GetSeparationVector`. The type-pair `switch` does runtime `is` checks on every call; for the dominant case (entity with one circle child vs. another), a fast path that skips the leaf-shape walk and the dispatch table is worthwhile.
- **Cache `Entity.BroadPhaseRadius`.** Currently recomputed on every read (loops over `_shapes`, calls `MathF.Sqrt`). Read once per pair in `RunSameListCollisionsSweep` and `RunPair`, twice if we count `aRight` / `bLeft`. Cache on Entity, invalidate when shapes are added/removed or the offset changes.

Validate before/after with the `FrameProfile.CollisionMs` reading in the sample at a fixed ball count (say, 500). Ship one optimization at a time so each can be measured.

## Sweep-and-prune iteration bias
**Priority: Eventual** — `RunSameListCollisionsSweep` always iterates pairs `(i, j)` with `i < j`. When a pair bounces, A=list[i] (lower X) is pushed left and B=list[j] (higher X) is pushed right. The leftmost ball in any cluster only ever gets pushed *left* by its pairings; the rightmost only *right*. Over many frames this slowly biases the pile shape — visually subtle, statistically real.

Cheap fix: alternate sweep direction every other frame (`i = N-1` down to `0`, `j = i-1` down to `0`). Keeps unique-pair coverage; no API change. Costs one bool field on `CollisionRelationship` and a sign flip on the loop bounds.

Only meaningful in scenes with hundreds of densely-packed dynamic bodies — bullet swarms, particle piles, the ball-partition stress test. Most game scenes won't notice.

## Naming audit and rename pass
**Priority: Soon** — Several types carry FRB1 naming baggage that doesn't fit FRB2's leaner API. Do a single coordinated rename pass to avoid churn from piecemeal renames.

Known candidates:
- `TileShapeCollection` → `TileShapes` — "Collection" suffix is redundant
- (Audit the rest of the public API for similar verbose/legacy names)

Do this as one branch so all renames land together and skill files, XML docs, samples, and tests update in lockstep.

