---
name: animation
description: "Sprite animation in FlatRedBall2. Use for AnimationChain, AnimationChainList, AnimationPlayer, AchxLoader, .achx/.achj files, Aseprite/.ase loading, Sprite.PlayAnimation, frame-based texture flipping, looping/non-looping animations, AnimationFinished/AnimationLooped/FrameChanged and named frame events (FrameEventRaised), and per-frame collision shapes (hitboxes/hurtboxes, static object collision)."
---

# Sprite Animation in FlatRedBall2

Sprites animate via `AnimationChain` / `AnimationChainList`, driven automatically by `Screen.Update` — no per-frame call needed in game code.

## Quick setup: adding animation content

1. Create a folder in your `*.Common` project's `Content` directory (for example `Content/Animations`).
2. Copy your source spritesheet PNG into that folder.
3. Open AnimationEditor and save a new `.achx` in the same folder as the PNG.
4. Ensure your project copies these files to output (for example `Content/Animations/**` in `.csproj`; see `references/platformer-template.md`).

`FileRelativeTextures` in `.achx` expects relative texture paths, so keeping `.achx` and texture content together avoids broken lookups.

## Runtime Types

- `AnimationFrame` — texture + source rectangle (pixel coords) + flip flags + `FrameLength` (TimeSpan) + per-frame `RelativeX/Y` offsets + optional `Shapes` collection + optional `Events` (`AnimationFrameEvent`: `Name`, free-form `Data`)
- `AnimationChain : List<AnimationFrame>` — named sequence; `TotalLength` = sum of `FrameLength`s
- `AnimationChainList : List<AnimationChain>` — string indexer for lookup by name; the unit of "ownership" for per-frame shapes (see Topics)

## Standalone AnimationChain.MonoGame/.Common Package

The types above live in `FlatRedBall2.Animation` and back `Sprite.PlayAnimation`. A separate
generic package (namespaces `FlatRedBall.AnimationChain` / `FlatRedBall2.AnimationEditorCommon`) —
`AnimationChainList<TFrame>`, `AnimationPlayer<TFrame>`, `AchxLoader`
(`src/AnimationChain.MonoGame/AchxLoader.cs`) — is for raw MonoGame projects with no FRB2
`Screen`/`Sprite`. Same names, different (generic) shape — don't conflate the two; see
`samples/AnimationChainSample`.

## Sprite Playback API

| Member | Default | Notes |
|---|---|---|
| `AnimationChains` | `null` | Assign before `PlayAnimation`; without a parent entity, `RelativeX/Y` and per-frame shapes don't behave usefully |
| `PlayAnimation(string name)` | — | Looks up by name; no-op if not found |
| `PlayAnimation(AnimationChain chain)` | — | Play a specific chain directly |
| `Animate` | `false` | Auto-managed. **Do not write to express idle state** — see Gotchas |
| `IsLooping` | seeded from `AnimationChain.Loop` on `PlayAnimation` | overridable per-instance after `PlayAnimation` — set it before that and it gets reseeded |
| `AnimationSpeed` | `1f` | Multiplier |
| `CurrentAnimation` | — | Read-only; returns the active chain |
| `AnimationFinished` | — | Fires when a non-looping animation ends |
| `AnimationLooped` | — | Fires when a looping chain wraps. Never fires for non-looping chains or from `PlayAnimation` |
| `FrameChanged` | — | Fires when the displayed frame changes, including `PlayAnimation` to a different chain (not re-playing the same one). `CurrentFrame` is already the new frame |
| `FrameEventRaised` | — | Fires per `AnimationFrameEvent` when its frame is entered. Branch on `e.Name`, never on a frame index, so reordering frames in the AnimationEditor can't break it. Skipped frames still fire; the XML doc has the loop/frame-0 rules |
| `FrameControlledProperties` | `SpriteFrameProperties.All` | `[Flags]` of `Flip`, `Offset`, `Color`: the groups frames write on each frame change. Clear a flag to own that group in game code |
| `AddColor` | `Vector3.Zero` | Public, -1..1 per channel. Frames assign it (non-Add frames reset it to zero) while the `Color` group is on. The shader adds it first, then `Color` and `Alpha` multiply the sum |

`AnimationLooped` and `FrameChanged` each fire at most once per update, even if a large delta spans several loops or frames (`FrameEventRaised` still reports every frame crossed). Order within an update: `FrameChanged`, `FrameEventRaised`, `AnimationLooped`, `AnimationFinished`; a handler that switches animation suppresses the rest for the old chain.

`PlayAnimation` resets time to frame 0 and sets `Animate = true`. Calling it every frame with the same chain restarts on frame 0 every tick — guard with `CurrentAnimation?.Name != "Run"`.

## Building Chains in Code

There is no builder API; it's plain object init. Construct `AnimationFrame` instances with `Texture`, `SourceRectangle`, and `FrameLength`, add them to an `AnimationChain` (with a `Name`), add chains to an `AnimationChainList`, assign the list to `Sprite.AnimationChains`, then `PlayAnimation`. For hitboxes/hurtboxes per frame, see Topics.

## Prompt handoff template

When you want an AI assistant to wire animation selection to gameplay state, describe the mapping explicitly:

"Use `Player.achx` for `PlayerSprite`. Play `Idle` when speed is 0, `Run` when grounded and moving, `Jump` when vertical velocity is positive, and `Fall` when vertical velocity is negative. Do not restart the same chain every frame; only switch when the target chain name changes."

## Hot-Reload

`AnimationChainList.TryReloadFrom(path, content)` patches a list in place by chain-name match. Live `Sprite.CurrentAnimation` references keep playing with new frames. **Every sprite must share one list instance** — re-parsing per spawn defeats hot-reload. Wire via `WatchContentDirectory` — see `content-hot-reload`.

## Topics (load on demand)

| When you need to… | Read |
|---|---|
| Load Aseprite `.ase`/`.aseprite` files | `references/aseprite.md` |
| Load `.achx`/`.achj` or author one by hand | `references/achx-authoring.md` |
| Load Adobe Animate atlas XML | `references/adobe-animate.md` |
| Add per-frame shapes (hitboxes/hurtboxes, or a static object's fixed collision box) | `references/per-frame-shapes.md` |
| Drop in the bundled platformer animation template | `references/platformer-template.md` |
| Pick animation state in a platformer (state → chain mapping) | `platformer-movement` skill |

## Gotchas

- **Never pause animation to express "still" state.** Animation runs continuously. If a state should look motionless (idle, hanging on a ladder, holding a charge), the **content author** authors a chain that looks still — a 1-frame chain, or a multi-frame chain with subtle motion (breath, blinking). Game code that flips `_sprite.Animate = false` bakes a content decision into engine-driving code and forecloses author choices the artist may want later. Only `PlayAnimation` (sets true) and the non-looping end-of-chain hook (sets false) should write `Animate`. If you reach for `_sprite.Animate = …`, you want a different chain.
- **`AnimationChains` must be set before `PlayAnimation`** — otherwise silent no-op.
- **Standalone package: `TryReloadFrom` and `TryReload` are different methods, not synonyms.** `AnimationChainList<AnimationFrame>.TryReloadFrom` (instance) wants a frame-factory `Func<AnimationFrameSave, AnimationFrame>`; the `TryReload` extension in `AnimationChainListSaveExtensions` wants a texture-loader `Func<string, Texture2D?>`. Passing a texture-loader lambda to `TryReloadFrom` won't compile — use `TryReload`.
- **Non-looping animation stops on the last frame** — `Animate` flips false; call `PlayAnimation` again to restart.
- **Animation is paused when the screen is paused** — `AnimateSelf` runs inside the `!IsPaused` block in `Screen.Update`.
- **By default every frame switch overwrites `Sprite.X`/`Y`, the flip flags, and `AddColor`.** `FrameControlledProperties` defaults to `All`, so `_booster.Y = -10` in `CustomInitialize` snaps back to the frame's `RelativeX`/`RelativeY` on the next switch, and a manual `FlipHorizontal` or hit-flash `AddColor` is lost. Clear the matching flag (`Offset`, `Flip`, `Color`) to own that group; re-enabling it applies the current frame at once.
- **Per-frame `Red`/`Green`/`Blue` do nothing without a `ColorOperation`; `Alpha` never needs one.** `AnimationFrame` has no single color property. `ColorOperation.Multiply` scales `Sprite.Color` by the channels (unset = 255), `ColorOperation.Add` assigns `Sprite.AddColor` (unset = 0, negative darkens) while the `Color` group is on, and `Alpha` always multiplies in on its own. See `SpriteFrameColor.Apply`. An `AddColor` you set in code for a hit flash is overwritten by the next frame change, so clear `SpriteFrameProperties.Color` from `FrameControlledProperties` first.
- **A `null` color channel or `ColorOperation` inherits from the previous frame, but only when loaded from `.achx`/`.achj`.** `EffectiveFrameColor.ResolveAll` fills nulls from the latest earlier frame in the same chain at load time. Frames built in code draw exactly as set.
- **A frame's offset and color can glide to the next frame, not just snap.** `Sprite` evaluates `KeyframeEvaluator` (`AnimationChain.Common`) every update from the frame's `OffsetCurve`/`ColorCurve`, so `X`/`Y` and the draw color change mid-frame; clear `Offset`/`Color` in `FrameControlledProperties` to opt out. Frames built in code get a curve by setting those fields (default is snap). Named shapes glide their offset and size the same way (`per-frame-shapes.md`). File fields: `achx-authoring.md`.
- **`TextureScale` recalculates `Width`/`Height` per frame** — when `TextureScale` is non-null, frame switches recompute dimensions from the source rect. Don't manually assign `Width`/`Height` if `TextureScale` is in play.
- **`.achx` isn't only for motion.** A chain can have a single frame and still be the right tool — it pairs a sprite's texture/offset with its collision box for a static object (a tree, a sign) that never animates. Going from that single frame to a real multi-frame animation later needs no code change; `PlayAnimation` and shape reconciliation behave the same either way.
