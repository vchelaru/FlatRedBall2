# Input subsystem — XML doc open questions

### IInputDevice.cs:1 — Entire interface
**Question:** `IInputDevice` defines `IsActionDown(string)` / `WasActionPressed(string)` but is not implemented or referenced anywhere in `src/` (only `.claude/designs/suggestions-refactoring-specialist.md` mentions it). Is this an in-progress action-binding API meant to ship, or dead code that should be removed before the first preview NuGet?
**Context:** No concrete `Keyboard`/`Gamepad`/`Cursor` implements it. Today, action-binding semantics are achieved by composing `IPressableInput` via `.Or()`.
**Proposed doc (already written):** Documented as a forward-looking abstraction reserved for a future action-binding system, with a note pointing users to `IKeyboard`/`IGamepad`/`IPressableInput` today. If the team decides to drop it instead, delete the file and the XML docs go with it.

### Cursor.cs — `PrimaryDown` / `PrimaryPressed` and gestures
**Question:** Are there plans to expose secondary mouse button (right-click), middle button, scroll wheel, or multi-touch (pinch/second finger) on `ICursor`? The "Primary" prefix implies "Secondary" exists or is planned, but there's no `SecondaryDown` today.
**Context:** Only the left mouse button and the first touch are exposed. Calling out the limitation in docs is fine, but if this is a known gap for the NuGet release it should probably be tracked in `design/TODOS.md`.
**Proposed doc (skip if nothing):** Not adding speculative docs. Current `ICursor` docs only describe what is implemented.

### KeyboardInput2D.cs — Diagonal magnitude
**Question:** Should `KeyboardInput2D` normalize diagonal input to unit length, or is the √2 magnitude on diagonals an intentional choice (matches the gamepad stick "circle in a square" behavior)?
**Context:** Documented the current behavior ("not normalized"). If the convention is "I2DInput is always direction-only, callers normalize for movement," that should be a contract on `I2DInput` rather than per-implementation. Worth a short discussion before NuGet.
**Proposed doc (already written):** Called out the non-normalized behavior in the class remarks so users know to normalize themselves.
