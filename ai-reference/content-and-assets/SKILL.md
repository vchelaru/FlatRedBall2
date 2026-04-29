---
name: content-and-assets
description: "Content and Assets in FlatRedBall2. Use when working with loading textures, fonts, sprites, content pipeline, .mgcb setup, or ContentLoader. Also trigger when the user asks about displaying text (use Gum Labels) or graphics without custom art (use Shapes)."
---

# Content and Assets in FlatRedBall2

## Decision: Shapes, Sprites, or Gum?

| Need | Use | Content files required? |
|------|-----|------------------------|
| Simple geometry (paddles, walls, bullets) | Shapes (`AARect`, `Circle`, `Polygon`) | No |
| Textured game objects (ships, characters) | `Sprite` with `Texture2D` | Yes (`.mgcb` pipeline) |
| On-screen text (scores, labels, menus) | Gum `Label` or `TextRuntime` | No (default font auto-loaded) |

## Text / Fonts — Use Gum Labels

Gum's default font is loaded automatically — no `.mgcb` setup required. See the `gum-integration` skill for Label examples and full layout details.

## Graphics Without Art — Use Shapes

Shapes require no content files and are ready to use immediately.

```csharp
var rect = new AARect { Width = 20, Height = 120, Color = Color.White, IsVisible = true };
Add(rect);
```

See the `shapes` skill for all shape types and visual properties.

## Sprites and Textures

Load textures via MonoGame's content pipeline and render them with `Sprite`.

### Loading a Texture

```csharp
// Compiled xnb pipeline — bare asset name (no extension, as defined in the .mgcb):
var texture = Engine.Content.Load<Texture2D>("ship_0001");

// Raw PNG from disk — full path with extension. Participates in PNG hot-reload
// via Engine.Content.TryReload(path). See the content-hot-reload skill.
var bear = Engine.Content.Load<Texture2D>("Content/Bear.png");
```

`Load<Texture2D>` routes on the presence of a file extension:
- **Bare name** → MonoGame's xnb pipeline (requires a `.mgcb` entry). Not hot-reloadable.
- **Path with extension** → loaded directly from disk via `Texture2D.FromFile`, tracked for hot-reload. Requires the file to be copied to the build output (see Content Pipeline Setup below or use a `<Content Include="Content/*.png" CopyToOutputDirectory="PreserveNewest" />` item).

### Creating a Sprite

```csharp
var sprite = new Sprite
{
    Texture = texture,
    TextureScale = 1.5f,   // 1.5x the texture's pixel size
    IsVisible = true,
};
Add(sprite);
```

### TextureScale vs Explicit Sizing

`TextureScale` (default `1f`) controls how sprite dimensions are derived:

- **Non-null (default)** — `Width = textureWidth * TextureScale`, `Height = textureHeight * TextureScale`. Setting `Width`/`Height` directly is a no-op while `TextureScale` is set.
- **Null** — explicit mode. Set `TextureScale = null` first, then set `Width`/`Height` freely.

```csharp
// Pixel-art 2x upscale:
sprite.TextureScale = 2f;

// Explicit size (ignores texture dimensions):
sprite.TextureScale = null;
sprite.Width = 100;
sprite.Height = 50;
```

### Sprite Sheets (SourceRectangle)

Use `SourceRectangle` to render a sub-region of a texture:

```csharp
sprite.SourceRectangle = new Rectangle(0, 0, 32, 32);  // top-left 32x32 tile
```

When `TextureScale` is non-null, dimensions are recalculated from the source rectangle size.

### Sprite Properties

| Property | Default | Notes |
|----------|---------|-------|
| `IsVisible` | `true` | Shapes default to `false`; Sprite defaults to `true` |
| `Color` | `Color.White` | Tint color — `White` means no tint |
| `Alpha` | `1f` | Opacity (0 = transparent, 1 = opaque) |
| `Rotation` | `0` | Uses `Angle` type, same as entities |
| `FlipHorizontal` | `false` | Mirror horizontally |
| `FlipVertical` | `false` | Mirror vertically |

### Cleanup

```csharp
sprite.Destroy();   // removes from parent entity
```

## Content Pipeline Setup (.mgcb)

To use textures, you need a `Content/Content.mgcb` file in your sample project.

### 1. Create the Content directory and `.mgcb` file

```
samples/YourSample/Content/Content.mgcb
```

Minimal `.mgcb` content:

```
#----------------------------- Global Properties ----------------------------#

/outputDir:bin/$(Platform)
/intermediateDir:obj/$(Platform)
/platform:DesktopGL
/config:
/profile:Reach
/compress:False

#-------------------------------- References --------------------------------#


#---------------------------------- Content ---------------------------------#
```

### 2. Add a texture

Place the `.png` file in `Content/`, then add an entry to the `.mgcb`:

```
#begin mysprite.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:ColorKeyColor=255,0,255,255
/processorParam:ColorKeyEnabled=True
/processorParam:GenerateMipmaps=False
/processorParam:PremultiplyAlpha=True
/processorParam:ResizeToPowerOfTwo=False
/processorParam:MakeSquare=False
/processorParam:TextureFormat=Color
/build:mysprite.png
```

### 3. Load in code

```csharp
var tex = Engine.Content.Load<Texture2D>("mysprite");  // no extension
```

## Gotchas

- **`IsVisible` defaults differ** — Sprite defaults to `true`; shapes default to `false`. Forgetting `IsVisible = true` on a shape is a common source of invisible objects.
- **`TextureScale` wins over explicit `Width`/`Height`** — If you set `Width` and it doesn't take effect, check that `TextureScale` is `null`.
- **Content not found at runtime** — Verify the asset name matches the `.mgcb` entry (case-sensitive on Linux), and that `Content.RootDirectory = "Content"` is set in `Game1`.
- **For sprite animation**, see the `animation` skill — `Sprite.PlayAnimation`, `AnimationChainListSave`, and `.achx` loading are fully implemented.
- **Each screen gets its own ContentLoader** — assets are unloaded when the screen transitions. Re-load textures in each screen's `CustomInitialize` if needed.
