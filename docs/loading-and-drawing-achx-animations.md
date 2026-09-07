# Loading and Drawing .achx/.achj Animations in MonoGame

The Animation Editor saves animations as `.achx` (XML) or `.achj` (JSON) files. The `FlatRedBall.AnimationChain.MonoGame` NuGet package loads and plays these files in any MonoGame project. It has no dependency on the FlatRedBall2 engine or a game project structure — it works in a plain `Game` class.

This page assumes you already have a `.achx` or `.achj` file and its spritesheet PNG, exported from Animation Editor.

## Install the package

```
dotnet add package FlatRedBall.AnimationChain.MonoGame
```

Targeting KNI or Blazor WASM instead? Use `FlatRedBall.AnimationChain.KNI`.

## Add your files to the project

Copy the `.achx`/`.achj` file and its PNG into your project's `Content` folder, next to each other.

Set both files' **Copy to Output Directory** property to **Copy if newer**. In the `.csproj`:

```xml
<ItemGroup>
  <Content Include="Content\hero.achx">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="Content\AnimatedSpritesheet.png">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Neither file goes through the MGCB content pipeline. `AchxLoader` reads the `.achx`/`.achj` as raw text and loads the PNG with `Texture2D.FromStream`, so don't add either one to `Content.mgcb`.

## Load the animation in code

`AchxLoader` reads the file and returns a ready-to-play `AnimationChainList<AnimationFrame>`. An `AnimationPlayer<AnimationFrame>` drives playback — it tracks the current chain and frame time.

```csharp
private AchxLoader _loader;
private AnimationPlayer<AnimationFrame> _player;

protected override void LoadContent()
{
    _loader = new AchxLoader(GraphicsDevice);
    var animations = _loader.Load("Content/hero.achx");

    _player = new AnimationPlayer<AnimationFrame>(animations);
    _player.Play("Walk");
}

protected override void Update(GameTime gameTime)
{
    _player.Update(gameTime.ElapsedGameTime);
    base.Update(gameTime);
}
```

`Play` looks up the chain by the name given in Animation Editor. Calling `Play` with the currently-playing chain's name is a no-op, so it's safe to call every frame from gameplay code that hasn't changed animation state.

## Draw the animation in code

There are two ways to draw a frame: let the package draw it for you, or read the current frame yourself and draw it with your own `SpriteBatch.Draw` call.

{% tabs %}
{% tab title="DrawAnimation (quickest)" %}
`SpriteBatchExtensions.DrawAnimation` draws the player's current frame with a regular `SpriteBatch`:

```csharp
protected override void Draw(GameTime gameTime)
{
    GraphicsDevice.Clear(Color.CornflowerBlue);

    _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    _spriteBatch.DrawAnimation(_player, position: new Vector2(400, 300), scale: 4f);
    _spriteBatch.End();

    base.Draw(gameTime);
}
```

`SamplerState.PointClamp` keeps pixel art sharp at a non-1x `scale`. The `position` argument is a screen-pixel anchor point; `DrawAnimation` centers the frame on it by default, matching Animation Editor's preview. It also applies flip flags, authored per-frame color/alpha, and `ColorOperation.Add` (via a pixel shader) automatically.
{% endtab %}

{% tab title="Manual SpriteBatch.Draw" %}
`AnimationPlayer<AnimationFrame>.CurrentFrame` exposes the texture and source rectangle directly, so you can draw it with your own `SpriteBatch.Draw` call instead — useful when you need your own rotation, origin, blend state, or custom effect:

```csharp
protected override void Draw(GameTime gameTime)
{
    GraphicsDevice.Clear(Color.CornflowerBlue);

    var frame = _player.CurrentFrame;
    if (frame?.Texture != null)
    {
        var effects = SpriteEffects.None;
        if (frame.FlipHorizontal) effects |= SpriteEffects.FlipHorizontally;
        if (frame.FlipVertical) effects |= SpriteEffects.FlipVertically;

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(
            frame.Texture,
            position: new Vector2(400, 300) + new Vector2(frame.RelativeX, frame.RelativeY),
            sourceRectangle: frame.SourceRectangle?.ToXnaRectangle(),
            color: Color.White,
            rotation: 0f,
            origin: Vector2.Zero,
            scale: 4f,
            effects: effects,
            layerDepth: 0f);
        _spriteBatch.End();
    }

    base.Draw(gameTime);
}
```

`SourceRectangle` is a renderer-agnostic `PixelRectangle` — `ToXnaRectangle()` converts it to a MonoGame `Rectangle`. This path skips `DrawAnimation`'s extras: you apply flip flags yourself (shown above), and authored per-frame color/alpha and `ColorOperation.Add` aren't applied unless you read those fields (`frame.Red`/`Green`/`Blue`/`Alpha`/`ColorOperation`) and handle them yourself.
{% endtab %}
{% endtabs %}

Release the loader's cached textures when you're done with it:

```csharp
protected override void UnloadContent()
{
    _loader.Dispose();
}
```

## Next steps

For frame origin/offset details (`RelativeX`/`RelativeY`), authored color and `ColorOperation.Add`, and loading from a byte stream in a browser (Blazor WASM/KNI) instead of the filesystem, see the package's own README: [FlatRedBall.AnimationChain.MonoGame on NuGet](https://www.nuget.org/packages/FlatRedBall.AnimationChain.MonoGame).
