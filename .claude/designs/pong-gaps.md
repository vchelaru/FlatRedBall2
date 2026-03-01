# Pong Build — Gaps & Resolutions

Work through each gap before implementing Pong. For each item: decide the fix, make the change, check it off.

---

## 1. Text / Score Rendering

**Problem:** No skill or engine class for drawing text. `Sprite` requires a `Texture2D`. An agent has no path to rendering numbers without deep code inspection.

**Options:**
- A) Add `TextRenderable` to the engine (`src/Rendering/TextRenderable.cs`) + a skill section
- B) Document the 7-segment digit-entity pattern in the skill (no engine change)
- C) Add a `SpriteFont` content pipeline setup guide + skill

---

## 2. Camera

**Problem:** No skill covers `Screen.Camera`. Needed for Pong: background color, world bounds, screen shake.

**Key facts to document:**
- `Camera.BackgroundColor` — sets clear color
- `Camera.TargetWidth` / `TargetHeight` — defines world unit scale (default 1280×720)
- World bounds: X ∈ [-TargetWidth/2, TargetWidth/2], Y ∈ [-TargetHeight/2, TargetHeight/2]
- Screen shake: set `Camera.VelocityX`/`VelocityY` for a frame or two

**Options:**
- A) New `camera.md` skill
- B) Add a Camera section to `physics-and-movement.md`


---

## 3. Single-Entity Collision Overload

**Problem:** Skills only show `Factory<A>` vs `Factory<B>`. Pong has one ball vs two paddles — needs the `(A single, IEnumerable<B> list)` overload, which exists but isn't documented.

**Fix:** Add an example to `collision-relationships.md`.


---

## 4. Screen Transitions with Data

**Problem:** `MoveToScreen<T>()` calls `new T()` — no way to pass arguments. An agent has no documented pattern for sharing state between screens (e.g., game mode, winner).

**Options:**
- A) Document the static-class workaround (e.g., `static class GameState`)
- B) Add a `MoveToScreen<T>(Action<T> configure)` overload to the engine
- C) Both

---

## 5. Window Resolution / World Bounds

**Problem:** Default MonoGame window is 800×480. An agent designing a 1280×720 game must set `_graphics.PreferredBackBufferWidth/Height` in `Game1`, but nothing documents this.

**Fix:** Document the resolution setup pattern, ideally in `Game1.cs` comments or a getting-started note.

---

## 6. Content / Asset Loading

**Problem:** No skill covers `ContentManagerService.Load<T>()`, the `.mgcb` pipeline, or adding a `SpriteFont`. The `Content/Content.mgcb` file exists but is empty with no guidance.

**Options:**
- A) Add a `content-and-assets.md` skill
- B) Add inline comments to the empty `Content.mgcb`
- C) Pre-populate `Content.mgcb` with a default font ready to use

**Decision:** _
