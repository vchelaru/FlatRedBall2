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
