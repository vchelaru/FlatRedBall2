# Collision XML doc questions

### ShapeCollection.cs:6 — `ShapeCollection` class
**Question:** Is `ShapeCollection` intended for game-code use, or is it dead/legacy and slated for removal?
**Context:** A grep for `new ShapeCollection` across the codebase (src + tests) returns zero hits — no production code, no tests, no samples instantiate it. The class is `public`, supports four `Add` overloads (rect/circle/line/polygon), and implements `ICollidable` with "treated as static geometry" semantics. The much-richer `TileShapeCollection` covers the only real static-geometry use case (tile maps). Several skill files reference `TileShapeCollection` extensively but none reference `ShapeCollection`. If kept, the class needs a positioning story (currently `AbsoluteX`/`AbsoluteY` always return 0, so contained shapes' world positions come from their own X/Y — undocumented and surprising).
**Proposed doc (skip if you have nothing):** Already added a class summary that contrasts it with `TileShapeCollection`. If the answer is "remove it," delete the class entirely instead.

### Polygon.cs:54 — `SetPoints` Y-axis convention for Adobe-Animate-style content
**Question:** `SetPoints` accepts `IEnumerable<Vector2>`. The XML says points are "relative to the polygon's position." What's the expected winding/Y convention? `BuildConvexParts` calls `IsConvexList` which assumes CCW winding in a Y-up coordinate space (matches the engine-wide convention), and `EarClipToLocalTriangles` flips index order if `SignedArea < 0`. So callers can pass either winding for triangulation. But the `IsConvexList` shortcut returns `false` for CW-wound polygons (because the winding signs don't match), forcing them through ear-clip + Hertel-Mehlhorn even when convex. Is that intentional, or is the shortcut buggy?
**Context:** Looks like `IsConvexList` should test "all crosses same sign" not "any positive AND any negative." It does. So a CW convex polygon has all-negative crosses → `hasNeg && !hasPos` → returns `true`. So the shortcut works for both windings. Re-reading: this is fine, no bug. Withdrawing.

### CollisionRelationship.cs:103 — `AllowDuplicatePairs` — is this game-code-facing?
**Question:** The summary I'd write is "fires `CollisionOccurred` for both `(a,b)` and `(b,a)` orderings on self-collision." Existing comment is good. The property is mostly self-documenting. Genuine uncertainty: are there real game-code reasons to enable this (e.g. when each side runs damage/effect logic and both must trigger), or is it primarily for tests/internal? The existing one-sentence summary is probably sufficient — flagging only because "duplicate pairs" is jargony.
**Proposed doc:** Leave as-is.

### CollisionDispatcher.cs:25 — `CollidesWith` — special-cased Line handling
**Question:** Worth documenting as a public-facing gotcha that `Line` vs non-`AxisAlignedRectangle` shapes uses an intersection-only path (no separation), or is this purely an internal implementation detail since `CollisionDispatcher` is internal?
**Context:** Class is `internal static`, so no external visibility. The same caveat is captured on `Line.GetSeparationVector` which I documented. Nothing to add here.
