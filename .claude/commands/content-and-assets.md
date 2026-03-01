# Content and Assets in FlatRedBall2

> **Status: Under Construction.** The content pipeline (`ContentManagerService.Load<T>()`, `.mgcb` setup, custom textures, custom fonts) is not yet documented and the workflow is not finalized. The guidance below covers what works today.

## Text / Fonts — Use Gum Labels

Do not load `SpriteFont` manually. Gum's default font is loaded automatically by `FlatRedBallService.Initialize` — no `.mgcb` setup required.

For any on-screen text (scores, labels, menus, game-over messages), use a Gum `Label`:

```csharp
using Gum.Forms.Controls;

var scoreLabel = new Label();
scoreLabel.Text = "0";
scoreLabel.X = 20;   // screen pixels from left
scoreLabel.Y = 20;   // screen pixels from TOP (Gum is Y-down)
AddGumRenderable(new GumRenderable(scoreLabel.Visual));

// Update each frame in CustomActivity:
scoreLabel.Text = _score.ToString();
```

See `gum-integration.md` for full Label and layout details.

## Graphics — Use Shapes Instead of Sprites

For games that don't need photographic art, shapes are the right choice. They require no content files and are ready to use immediately.

```csharp
// Paddle / wall — axis-aligned rectangle
var rect = new AxisAlignedRectangle
{
    Width = 20,
    Height = 120,
    Color = Color.White,
    Visible = true,
};
AddChild(rect);

// Ball — circle
var circle = new Circle
{
    Radius = 10,
    Color = Color.White,
    Visible = true,
};
AddChild(circle);
```

See `shapes.md` for all shape types, visual properties, and gotchas.

## Custom Textures / Sprites — Not Yet Supported

Loading a `Texture2D` via `ContentManager.Load<Texture2D>("path")` and rendering it through `Sprite` is plumbed but not documented or tested end-to-end. Do not rely on it until this skill is updated.
