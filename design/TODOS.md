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

## TileShapeCollection.AddRectangleBorder convenience
**Priority: Soon** — building an arena/play-field wall ring (every game with bounded play area: arcade brawler, bumper-ball arena, top-down shooter map edges) currently takes ~12 lines of cell-loop boilerplate per side. The `AutoEvalBallPartitionSample` had to write:

```csharp
for (int c = 0; c < cols; c++) { tiles.AddTileAtCell(c, 0); tiles.AddTileAtCell(c, rows - 1); }
for (int r = 1; r < rows - 1; r++) { tiles.AddTileAtCell(0, r); tiles.AddTileAtCell(cols - 1, r); }
```

Add `TileShapeCollection.AddRectangleBorder(int colMin, int rowMin, int colMax, int rowMax)` (or a thickness-parameterized variant) so a one-liner replaces the four-loop dance. Skip if the natural pattern in actual games is to source walls from a TMX file — if Tiled is the canonical authoring path for level edges, the code-only convenience is a sharp tool for sample/test code only and may not earn its keep.
