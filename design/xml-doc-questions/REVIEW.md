# XML Docs Pass — Open Questions for Review

Consolidated from the parallel docs pass. Items are grouped by whether they need a **decision** (likely affects API/code, not just docs) or a **doc-only call**.

---

## Decisions needed (affect code, not just docs)

### 1. `ShapeCollection` — keep, redesign, or delete?
**File:** `src/Collision/ShapeCollection.cs`
**Issue:** Zero usages anywhere — no production code, no tests, no samples instantiate it. It's `public`, has four `Add` overloads (rect/circle/line/polygon), and implements `ICollidable` with "treated as static geometry" semantics. `TileShapeCollection` covers the only real static-geometry use case.
**Bonus problem if kept:** `AbsoluteX`/`AbsoluteY` always return 0, so contained shapes' world positions come from their own X/Y — undocumented and surprising.
**Options:** (a) delete entirely before NuGet ships, (b) fix the positioning story and add usage examples, (c) leave as-is with the contrasting class summary the agent already wrote.

### 2. `IInputDevice` — ship, defer, or delete?
**File:** `src/Input/IInputDevice.cs`
**Issue:** Defines `IsActionDown(string)` / `WasActionPressed(string)`. Zero implementations, zero call sites in `src/`. Only mentioned in a refactoring design doc. Today's action-binding story is composing `IPressableInput` via `.Or()`.
**Options:** (a) delete before preview NuGet (clean surface), (b) ship as a forward-looking abstraction with the agent's "reserved for future" doc, (c) actually implement an action-binding system before 0.1.0.

### 3. `KeyboardInput2D` diagonal magnitude — contract on `I2DInput`?
**Files:** `src/Input/KeyboardInput2D.cs`, `src/Input/I2DInput.cs`
**Issue:** `KeyboardInput2D` returns √2 magnitude on diagonals (not normalized). Gamepad sticks naturally vary. Should `I2DInput` declare a contract — "always direction-only, callers must normalize" or "always pre-normalized"? Currently inconsistent across implementations.
**Options:** (a) leave per-implementation and document each; (b) declare a contract on `I2DInput` and conform implementations to it.

---

## Doc-only calls

### 4. `ICursor.PrimaryDown` / `PrimaryPressed` — naming implies secondary
**File:** `src/Input/Cursor.cs` / `src/Input/ICursor.cs`
**Issue:** "Primary" prefix implies "Secondary" exists or is planned, but only left-mouse and first-touch are exposed. Right-click, middle button, scroll wheel, multi-touch are absent.
**Suggestion:** If these are planned for the NuGet timeframe, add a TODO; otherwise the existing docs (which describe only what's implemented) are fine.

---

## Self-resolved during the pass (kept for audit trail, no action needed)

- `Polygon.SetPoints` winding convention — investigated, `IsConvexList` correctly handles both CW and CCW. No bug.
- `CollisionRelationship.AllowDuplicatePairs` — existing one-sentence summary is sufficient; "duplicate pairs" jargon noted but acceptable.
- `CollisionDispatcher.CollidesWith` Line special-casing — class is `internal static`, no public surface; same caveat documented on `Line.GetSeparationVector`.

---

## Coverage gaps explicitly skipped (next-pass candidates)

The Tiled/Content/UI/Diagnostics/Math/root agent flagged that it closed all `CS1591` gaps but did **not** verify every existing doc comment for accuracy on the two largest files:
- `src/Screen.cs` — 789 lines
- `src/FlatRedBallService.cs` — 558 lines

A targeted accuracy sweep on these would be reasonable before NuGet — they're the most-touched API surface in the engine.

---

## NuGet-readiness suggestion (from the agent)

Enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and treat `CS1591` (missing XML doc) as a tracked CI metric — it surfaces exactly the gaps consumers will see in IntelliSense. Worth doing before the first preview ships.
