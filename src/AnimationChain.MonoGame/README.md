# FlatRedBall.AnimationChain

A standalone library for loading and playing `.achx` sprite animation files created by the
[FlatRedBall Animation Editor](https://github.com/vchelaru/FlatRedBall2).
No FlatRedBall2 engine dependency required — drop it into any MonoGame or KNI project.

## Choose a variant

| Target platform | Package |
|---|---|
| MonoGame DesktopGL | `FlatRedBall.AnimationChain.MonoGame` |
| KNI / Blazor WASM | `FlatRedBall.AnimationChain.KNI` |

```sh
dotnet add package FlatRedBall.AnimationChain.MonoGame
# or
dotnet add package FlatRedBall.AnimationChain.KNI
```

## Quick start

```csharp
// LoadContent
_loader = new AchxLoader(GraphicsDevice);
_animations = _loader.Load("Content/player.achx");
_player = new AnimationPlayer(_animations);
_player.Play("Run");

// Update
_player.Update(gameTime.ElapsedGameTime);

// Draw
spriteBatch.DrawAnimation(_player, position, Color.White);

// UnloadContent / Dispose
_loader.Dispose();
```

Copy the `.achx` and its PNGs next to each other (`CopyToOutputDirectory`).
`AchxLoader` loads them with `Texture2D.FromStream`, not the MGCB pipeline.

## Drawing

`DrawAnimation` uses MonoGame `SpriteBatch` conventions:

- `position` is the **top-left** of the current frame's source rectangle
  (before per-frame offset), in screen pixels. Y increases downward.
- `scale` multiplies the source rectangle. A 16×32 frame at `scale: 3` is
  48×96 on screen.
- `AnimationFrame.RelativeX` / `RelativeY` are **unscaled source pixels**
  from the `.achx`. `DrawAnimation` applies them as `offset * scale`.
  Positive `RelativeY` moves the sprite down.
- If the `.achx` was authored in a Y-up world, negate `RelativeY` or flip Y
  in your camera transform.

To stand a character on a ground point `(x, y)` (feet on `y`):

```csharp
var frame = player.CurrentFrame;
int srcW = frame?.SourceRectangle?.Width ?? 0;
int srcH = frame?.SourceRectangle?.Height ?? 0;
float width = srcW * scale;
float height = srcH * scale;
spriteBatch.DrawAnimation(
    player,
    new Vector2(x - width / 2f, y - height),
    Color.White,
    scale: scale);
```

Empty pixels at the bottom of a cell still count in `SourceRectangle.Height`.
If idle-up/down hover while left/right look planted, set `RelativeY` on those
frames in the Animation Editor (or crop the source rect) so the shoes sit on
the last row of the cell.

Use `SamplerState.PointClamp` for pixel art.

## Playback notes

- `Play(name)` is a no-op if that chain is already playing, so it is safe every
  frame. Unknown names are ignored and the current animation continues.
- One `AchxLoader` owns and caches textures. Many `AnimationPlayer`s can share
  one `AnimationChainList`.
- `DrawAnimation` applies authored multiply color, alpha, and `ColorOperation.Add`
  (Add ends and restarts the `SpriteBatch` — see the method remarks).
  Per-frame **shapes** are data only. **`FlipDiagonal`** is authored and not
  applied (`SpriteEffects` has no diagonal option).
- This package is not the FlatRedBall2 engine player. `Sprite.PlayAnimation`
  uses a different origin and Y axis — do not mix the two type systems.

## Web / Blazor WASM (KNI)

The filesystem is unavailable in browser environments. Pre-fetch bytes and pass streams instead:

```csharp
var achxBytes = await httpClient.GetByteArrayAsync("Content/player.achx");
var texBytes  = await httpClient.GetByteArrayAsync("Content/player.png");
_animations = _loader.Load(
    new MemoryStream(achxBytes),
    texPath => new MemoryStream(texBytes));
```

## Key types

- **`AchxLoader`** — loads `.achx` files from disk or a stream; caches textures by path; `IDisposable`.
- **`AnimationPlayer`** — drives playback. Call `Play(name)`, `Update(elapsed)`, read `CurrentFrame`.
- **`SpriteBatchExtensions`** — `spriteBatch.DrawAnimation(player, position, color)` extension method.

## License

MIT — see [LICENSE](https://github.com/vchelaru/FlatRedBall2/blob/main/LICENSE).
