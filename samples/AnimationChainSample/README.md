# AnimationChainSample

Small sample showing `.achx` animation playback with `AnimationPlayer`.

## Run locally

```bash
dotnet run --project samples/AnimationChainSample/AnimationChainSample.csproj
```

## Controls

- **Space**: cycle animation chain
- **R**: reload `Content/hero.achx`
- **Escape**: exit

## XNA Fiddle notes

1. Upload both `Content/AnimatedSpritesheet.png` and `Content/hero.achx`.
2. The sample loads `hero.achx` through `TitleContainer.OpenStream("Content/hero.achx")`, so it does not depend on direct `File.OpenRead` access.
3. Press **R** to re-read the `.achx` stream and apply updated frame data.
