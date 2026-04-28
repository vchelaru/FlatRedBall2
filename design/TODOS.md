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

## CollisionRelationship list-type tightening
**Priority: Soon** — `CollisionRelationship<A, B>` stores its operand collections as `IEnumerable<A>` / `IEnumerable<B>` (`src/Collision/CollisionRelationship.cs:36-37`).

- Pair-iteration path at `RunCollisions` `foreach (var a in _listA) foreach (var b in _listB)` boxes the struct enumerators of the underlying `List<T>`/`Factory<T>` to `IEnumerator<T>` — two enumerator allocations per relationship per frame.
- The same-list path already runtime-casts to `IReadOnlyList<A>` to recover indexed iteration, proving the runtime objects support it.
- Tighten the field types (and the `Screen.AddCollisionRelationship<A,B>` overloads) to `IReadOnlyList<A>` / `IReadOnlyList<B>`. Callers in the engine + samples already pass `Factory<T>` or `List<T>`; non-breaking for the common case.
- Also unlocks indexed access for a partition-aware broad phase (drop-in at index k for sort-and-sweep).

## Factory Object Pooling
**Priority: Soon** — `Factory<T>` currently allocates on `Create()` and discards on destroy. For SMB-style entity churn (fireballs, coin-pop particles, score popups, brick rubble) this generates avoidable GC pressure on hot paths.

- Opt-in pool mode on `Factory<T>`: destroyed instances return to a free list; subsequent `Create()` calls hand them back after a `Reset()` hook on the entity.
- Decide entity reset contract: does pooling require a `Reset()` virtual on `Entity`, or is the pattern "destroy clears state, CustomInitialize re-initializes"?
- Interaction with `IsSolidGrid` factory mode and entity-collision-relationship dispatch — pooled entities must not leave dangling references in collision lists.
