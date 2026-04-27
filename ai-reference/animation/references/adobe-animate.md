# Adobe Animate TextureAtlas Loader

Adobe Animate's "Generate sprite sheet" export produces a `<TextureAtlas>` XML plus a single atlas PNG. Load with `AdobeAnimateAtlasSave`:

```csharp
using FlatRedBall2.Animation.Content;
var animations = AdobeAnimateAtlasSave
    .FromFile("Content/Characters/eyeball.xml")
    .ToAnimationChainList(Engine.Content, frameRate: 30f);
```

## Chain grouping

SubTextures are grouped into chains by stripping trailing digits from names: `Eyeball_Idle0000`, `Eyeball_Idle0001` → chain `Eyeball_Idle`. Within a chain, frames are sorted by name so `0000, 0001, 0002…` play in order.

The format has no per-frame duration, so `frameRate` is applied uniformly to every frame.

## Gotcha — pivot not applied

Adobe Animate's `pivotX`/`pivotY` per-frame attributes are parsed but ignored; `AnimationFrame` has no pivot field yet. Use `RelativeX`/`RelativeY` manually if you need to anchor a multi-size character at e.g. its feet.
