---
name: animation
description: "Sprite Animation in FlatRedBall2. Use when working with AnimationChain, AnimationChainList, .achx files, Aseprite .ase/.aseprite files, Sprite.PlayAnimation, frame-based texture flipping, looping/non-looping animations, or AnimationFinished events."
---

# Sprite Animation in FlatRedBall2

Sprites support frame-based animation via `AnimationChain` / `AnimationChainList`. Animations are driven automatically by `Screen.Update` — no per-frame call needed in game code.

---

## Runtime Types

- `AnimationFrame` — texture + source rectangle (pixel coords) + flip flags + `FrameLength` (seconds)
- `AnimationChain : List<AnimationFrame>` — named sequence; `TotalLength` = sum of all `FrameLength`s
- `AnimationChainList : List<AnimationChain>` — string indexer for lookup by name

---

## Loading Animations

Two source formats are supported. Both produce an `AnimationChainList` ready to assign to `sprite.AnimationChains`.

### From Aseprite (.ase / .aseprite) — preferred for new art

Load directly at runtime — no intermediate conversion step. Each Aseprite **tag** becomes one `AnimationChain`. See `references/aseprite.md` for details, gotchas, and the full API.

```csharp
using FlatRedBall2.Content.Aseprite;
var animations = AsepriteFileLoader
    .Load("Content/Characters/player.aseprite")
    .ToAnimationChainList(GraphicsDevice);
```

### From .achx (XML)

Load with `AnimationChainListSave` from `FlatRedBall2.Animation.Content`. See `references/achx-authoring.md` for the XML schema and coordinate format.

```csharp
using FlatRedBall2.Animation.Content;
var animations = AnimationChainListSave
    .FromFile("Content/Characters/player.achx")
    .ToAnimationChainList(Engine.Content);
```

`ToAnimationChainList` takes a `ContentManagerService` and routes each frame's `TextureName` through `Load<Texture2D>`: names with an extension (e.g. `"player.png"`) load directly from disk and participate in PNG hot-reload via `Engine.Content.TryReload`; bare names (no extension) go through the xnb pipeline.

**Gotcha — FileRelativeTextures**: when `true` (the default), texture names in the .achx are relative to the .achx file itself. If your paths are off, check that the .achx and textures share the expected relative layout.

---

## Building Animations in Code

```csharp
using FlatRedBall2.Animation;

var frame1 = new AnimationFrame
{
    Texture        = ContentManager.Load<Texture2D>("Characters/player"),
    SourceRectangle = new Rectangle(0, 0, 32, 32),
    FrameLength    = 0.1f,
};
var frame2 = new AnimationFrame { /* ... */ };

var walkChain = new AnimationChain { Name = "Walk" };
walkChain.Add(frame1);
walkChain.Add(frame2);

var chains = new AnimationChainList();
chains.Add(walkChain);

sprite.AnimationChains = chains;
sprite.PlayAnimation("Walk");
```

---

## Playback API on Sprite

| Member | Default | Notes |
|---|---|---|
| `AnimationChains` | `null` | Assign before calling `PlayAnimation` |
| `PlayAnimation(string name)` | — | Looks up by name; no-op if not found |
| `PlayAnimation(AnimationChain chain)` | — | Play a specific chain directly |
| `Animate` | `false` | Auto-managed by `PlayAnimation` and the non-looping end-of-chain. **Do not write to this from game code to express idle/still/hanging state** — see Gotchas |
| `IsLooping` | `true` | Set to `false` for one-shot animations |
| `AnimationSpeed` | `1f` | Multiplier; `2f` = double speed |
| `CurrentAnimation` | — | Read-only; returns the active `AnimationChain` |
| `AnimationFinished` | — | `event Action?` — fires when non-looping animation ends |

`PlayAnimation` resets time to frame 0 and sets `Animate = true`. Call it every time you switch animations (including re-triggering the same one from the start).

---

## Platformer Animation Template

A ready-made `.achx` and spritesheet live in `.claude/templates/AnimationChains/`. When a game needs character animations, copy these two files into the game's content directory:

1. `PlatformerAnimations.achx` → rename to match the character (e.g., `Player.achx`)
2. `AnimatedSpritesheet.png` → copy alongside the `.achx`

The `.achx` references the `.png` by relative path (`FileRelativeTextures` is `true`), so they must be in the same directory. Add a `.csproj` include to copy them to output:

```xml
<ItemGroup>
  <Content Include="Content/Animations/**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

**What the template provides:** 48 animation chains for a 16x32 character on a shared spritesheet. Includes idle, walk, run, jump, fall, duck, kick, slide, skid, climb, wall-slide, swim, look-up, victory, and shoot variants (left/right pairs). Also includes non-character chains: Coin, Block, InteractiveBlock, Fireball, Shot, BouncyPlatform, and particle effects.

**Customizing:** Delete chains you don't need, rename chains to match your game's conventions, and adjust `FrameLength` for timing. If using a different spritesheet, update `TextureName` and frame coordinates in each `<Frame>`. See the `references/achx-authoring.md` reference for the XML schema.

## Platformer Animations

FRB2 does not provide an engine-level animation controller for platformers. Animation state selection is game code — see the `platformer-movement` skill for the recommended pattern (a pattern match on `PlatformerBehavior` state + facing suffix).

## Gotchas

- **Never pause animation to express "still" state.** Animation runs continuously. If a state should appear motionless (idle, hanging on a ladder, holding a charge), the **content author** makes a chain that *looks* still — a 1-frame chain, or better, a multi-frame chain with subtle motion (hair blowing, eyes blinking, breath rise/fall). Game code switching `_sprite.Animate = false` is a layering violation: it bakes a content decision (is this state animated?) into engine-driving code, and it forecloses content choices the author may want later. The only correct writers of `Animate` are `PlayAnimation` (sets true) and the non-looping end-of-chain hook (sets false). If you find yourself reaching for `_sprite.Animate = ...`, you want a different chain instead.
- **`AnimationChains` must be set before `PlayAnimation`** — calling `PlayAnimation` on a sprite with null `AnimationChains` is a silent no-op.
- **Non-looping animation stops on the last frame** — `Animate` is set to `false` automatically; call `PlayAnimation` again to restart.
- **Animation is paused when the screen is paused** — `AnimateSelf` is called inside the `!IsPaused` block in `Screen.Update`.
- **`TextureScale` applies to the source frame size** — if `TextureScale` is set (default `1f`), `Width`/`Height` are recalculated automatically when the frame changes. This keeps animated sprites correctly sized without manual width/height assignment.
