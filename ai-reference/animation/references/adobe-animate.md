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

## Pivot → RelativeX/Y

Adobe Animate's `pivotX`/`pivotY` per-frame attributes (pixel coords from the source rect's top-left, Y-down) are converted into `AnimationFrame.RelativeX`/`RelativeY` at load time so the pivot pixel lands at the entity's origin. A bottom-center pivot (typical "feet" anchor) keeps a multi-size character planted at the entity's position across frames. Frames without `pivotX`/`pivotY` get zero offsets.

The conversion: `RelativeX = srcW/2 - pivotX`; `RelativeY = pivotY - srcH/2` (sign flip baked in for Adobe Y-down → world Y-up).
