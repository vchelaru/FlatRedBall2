---
name: animation
description: "Sprite animation in FlatRedBall2. Use for AnimationChain, AnimationChainList, .achx files, Aseprite/.ase loading, Sprite.PlayAnimation, frame-based texture flipping, looping/non-looping animations, AnimationFinished events, and per-frame collision shapes (hitboxes/hurtboxes)."
---

# Sprite Animation in FlatRedBall2

Sprites animate via `AnimationChain` / `AnimationChainList`, driven automatically by `Screen.Update` — no per-frame call needed in game code.

## Runtime Types

- `AnimationFrame` — texture + source rectangle (pixel coords) + flip flags + `FrameLength` (TimeSpan) + per-frame `RelativeX/Y` offsets + optional `Shapes` collection
- `AnimationChain : List<AnimationFrame>` — named sequence; `TotalLength` = sum of `FrameLength`s
- `AnimationChainList : List<AnimationChain>` — string indexer for lookup by name; the unit of "ownership" for per-frame shapes (see Topics)

## Sprite Playback API

| Member | Default | Notes |
|---|---|---|
| `AnimationChains` | `null` | Assign before `PlayAnimation`; without a parent entity, `RelativeX/Y` and per-frame shapes don't behave usefully |
| `PlayAnimation(string name)` | — | Looks up by name; no-op if not found |
| `PlayAnimation(AnimationChain chain)` | — | Play a specific chain directly |
| `Animate` | `false` | Auto-managed. **Do not write to express idle state** — see Gotchas |
| `IsLooping` | `true` | `false` for one-shot |
| `AnimationSpeed` | `1f` | Multiplier |
| `CurrentAnimation` | — | Read-only; returns the active chain |
| `AnimationFinished` | — | Fires when a non-looping animation ends |

`PlayAnimation` resets time to frame 0 and sets `Animate = true`. Calling it every frame with the same chain restarts on frame 0 every tick — guard with `CurrentAnimation?.Name != "Run"`.

## Building Chains in Code

There is no builder API; it's plain object init. Construct `AnimationFrame` instances with `Texture`, `SourceRectangle`, and `FrameLength`, add them to an `AnimationChain` (with a `Name`), add chains to an `AnimationChainList`, assign the list to `Sprite.AnimationChains`, then `PlayAnimation`. For hitboxes/hurtboxes per frame, see Topics.

## Hot-Reload

`AnimationChainList.TryReloadFrom(path, content)` patches a list in place by chain-name match. Live `Sprite.CurrentAnimation` references keep playing with new frames. **Every sprite must share one list instance** — re-parsing per spawn defeats hot-reload. Wire via `WatchContentDirectory` — see `content-hot-reload`.

## Topics (load on demand)

| When you need to… | Read |
|---|---|
| Load Aseprite `.ase`/`.aseprite` files | `references/aseprite.md` |
| Load `.achx` XML or author one by hand | `references/achx-authoring.md` |
| Load Adobe Animate atlas XML | `references/adobe-animate.md` |
| Add per-frame shapes (hitboxes, hurtboxes that come and go with frames) | `references/per-frame-shapes.md` |
| Drop in the bundled platformer animation template | `references/platformer-template.md` |
| Pick animation state in a platformer (state → chain mapping) | `platformer-movement` skill |

## Gotchas

- **Never pause animation to express "still" state.** Animation runs continuously. If a state should look motionless (idle, hanging on a ladder, holding a charge), the **content author** authors a chain that looks still — a 1-frame chain, or a multi-frame chain with subtle motion (breath, blinking). Game code that flips `_sprite.Animate = false` bakes a content decision into engine-driving code and forecloses author choices the artist may want later. Only `PlayAnimation` (sets true) and the non-looping end-of-chain hook (sets false) should write `Animate`. If you reach for `_sprite.Animate = …`, you want a different chain.
- **`AnimationChains` must be set before `PlayAnimation`** — otherwise silent no-op.
- **Non-looping animation stops on the last frame** — `Animate` flips false; call `PlayAnimation` again to restart.
- **Animation is paused when the screen is paused** — `AnimateSelf` runs inside the `!IsPaused` block in `Screen.Update`.
- **`Sprite.X` and `Sprite.Y` are overwritten on every frame switch.** Each `AnimationFrame` carries `RelativeX`/`RelativeY` (default `0`), and advancing assigns those unconditionally. Code like `_booster.Y = -10` in `CustomInitialize` works for exactly one frame, then snaps to `0`. To offset an animated sprite relative to its parent entity, bake the offset into each frame's `RelativeX`/`RelativeY` (in the `.achx` or in code), or attach the sprite to a child entity whose own `X`/`Y` carries the offset.
- **`TextureScale` recalculates `Width`/`Height` per frame** — when `TextureScale` is non-null, frame switches recompute dimensions from the source rect. Don't manually assign `Width`/`Height` if `TextureScale` is in play.
