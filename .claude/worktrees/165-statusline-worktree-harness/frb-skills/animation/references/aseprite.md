# Aseprite (.ase / .aseprite) Loading Reference

Load Aseprite files directly at runtime via the `AsepriteDotNet` library. No intermediate conversion to `.achx` is needed.

## API — `FlatRedBall2.Content.Aseprite`

**`AsepriteFileLoader.Load(string absoluteFileName)`** — loads a `.ase` or `.aseprite` file from disk. Returns an `AsepriteFile` (from `AsepriteDotNet.Aseprite`).

**`file.ToAnimationChainList(GraphicsDevice)`** — runtime conversion. Processes the file into a packed spritesheet, creates a `Texture2D` in GPU memory, and returns an `AnimationChainList` with pixel-coordinate `SourceRectangle` per frame. Each Aseprite **tag** becomes one `AnimationChain`; the tag name becomes the chain name used in `PlayAnimation`.

**`file.ToAnimationChainListSave(string textureFileName)`** — offline conversion. Returns an `AnimationChainListSave` with `CoordinateType = Pixel`. Every frame's `TextureName` is set to the provided `textureFileName`. Use this if you need to generate `.achx` XML without a GPU. The caller is responsible for saving the spritesheet texture separately.

## Tag → Chain Mapping

- Each Aseprite tag maps 1:1 to an `AnimationChain`
- Tag name = chain name (use this in `sprite.PlayAnimation("TagName")`)
- Frame durations are converted from Aseprite's milliseconds to seconds
- **Untagged files** fall back to a single chain named `AsepriteFileExtensions.UntaggedChainName` (`"Default"`) containing every frame in order. For multiple distinct animations, define tags in Aseprite — one tag per animation, named what you'll pass to `PlayAnimation`.

## Gotchas

- **Premultiplied alpha**: `AsepriteFileLoader.Load` uses premultiplied alpha by default, matching MonoGame's expectation. If colors look wrong, check that your `SpriteBatch` blend state is `BlendState.AlphaBlend` (the default).
- **Spritesheet is in-memory only**: `ToAnimationChainList` creates the packed atlas as a `Texture2D` in GPU memory. It is not saved to disk. All frames share this single texture.
- **Duplicate frame merging**: The processor merges visually identical frames in the atlas to save texture space. This is transparent — frame indices still resolve correctly.
