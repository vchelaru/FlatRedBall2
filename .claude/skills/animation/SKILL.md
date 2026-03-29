---
name: animation
description: "Sprite Animation in FlatRedBall2. Use when working with AnimationChain, AnimationChainList, .achx files, Sprite.PlayAnimation, frame-based texture flipping, looping/non-looping animations, or AnimationFinished events."
---

# Sprite Animation in FlatRedBall2

Sprites support frame-based animation via `AnimationChain` / `AnimationChainList`. Animations are driven automatically by `Screen.Update` — no per-frame call needed in game code.

---

## Runtime Types

- `AnimationFrame` — texture + source rectangle (pixel coords) + flip flags + `FrameLength` (seconds)
- `AnimationChain : List<AnimationFrame>` — named sequence; `TotalLength` = sum of all `FrameLength`s
- `AnimationChainList : List<AnimationChain>` — string indexer for lookup by name

---

## Loading from a .achx File

`.achx` is an XML format. Load with `AnimationChainListSave` from `FlatRedBall2.Animation.Content`:

```csharp
using FlatRedBall2.Animation.Content;

var animations = AnimationChainListSave
    .FromFile("Content/Characters/player.achx")
    .ToAnimationChainList(ContentManager);

sprite.AnimationChains = animations;
sprite.PlayAnimation("Walk");
```

`ToAnimationChainList` has two overloads:

- **`ToAnimationChainList(ContentManager)`** — loads textures via the content pipeline (`.xnb` files). Strip the extension from texture paths in the `.achx`.
- **`ToAnimationChainList(GraphicsDevice)`** — loads textures as raw image files (`.png`). Use this with the animation template, which references `.png` directly.

**Gotcha — FileRelativeTextures**: when `true` (the default), texture names in the .achx are relative to the .achx file itself. The loader prepends the .achx directory. If your paths are off, check that the .achx and textures share the expected relative layout.

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
| `Animate` | `false` | Set to `false` to pause mid-animation |
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

## Gotchas

- **`AnimationChains` must be set before `PlayAnimation`** — calling `PlayAnimation` on a sprite with null `AnimationChains` is a silent no-op.
- **Non-looping animation stops on the last frame** — `Animate` is set to `false` automatically; call `PlayAnimation` again to restart.
- **Animation is paused when the screen is paused** — `AnimateSelf` is called inside the `!IsPaused` block in `Screen.Update`.
- **`TextureScale` applies to the source frame size** — if `TextureScale` is set (default `1f`), `Width`/`Height` are recalculated automatically when the frame changes. This keeps animated sprites correctly sized without manual width/height assignment.
