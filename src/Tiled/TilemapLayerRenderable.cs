using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Tilemaps;
using MonoGame.Extended.Tilemaps.Rendering;
using MonoGame.Extended.ViewportAdapters;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Tiled;

/// <summary>
/// Renders a single <see cref="TilemapTileLayer"/> from a <see cref="Tilemap"/>,
/// integrating with FlatRedBall2's Z-ordered rendering pipeline. Delegates to
/// <see cref="TilemapSpriteBatchRenderer"/> for actual tile drawing, which handles
/// frustum culling and all tile flip/rotation combinations automatically.
/// </summary>
public class TileMapLayerRenderable : IRenderable, IAttachable
{
    private readonly TilemapSpriteBatchRenderer _renderer;
    private readonly TilemapTileLayer _layer;
    private OrthographicCamera? _orthoCam;

    /// <param name="renderer">
    /// A shared renderer with the tilemap already loaded via <see cref="TilemapSpriteBatchRenderer.LoadTilemap"/>.
    /// Reuse the same renderer for all layers of the same tilemap.
    /// </param>
    public TileMapLayerRenderable(TilemapSpriteBatchRenderer renderer, TilemapTileLayer layer)
    {
        _renderer = renderer;
        _layer = layer;
    }

    /// <summary>Delegates to the underlying <see cref="TilemapTileLayer.IsVisible"/>.</summary>
    public bool IsVisible
    {
        get => _layer.IsVisible;
        set => _layer.IsVisible = value;
    }

    // IAttachable
    /// <inheritdoc/>
    public Entity? Parent { get; set; }
    /// <inheritdoc/>
    public float X { get; set; }
    /// <inheritdoc/>
    public float Y { get; set; }
    /// <inheritdoc/>
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    /// <inheritdoc/>
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    /// <inheritdoc/>
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;
    /// <inheritdoc/>
    public void Destroy() { }

    // IRenderable
    /// <inheritdoc/>
    public float Z { get; set; }
    /// <inheritdoc/>
    public Layer? Layer { get; set; }
    /// <inheritdoc/>
    public IRenderBatch Batch { get; set; } = TiledRenderBatch.Instance;
    /// <inheritdoc/>
    public string? Name { get; set; }

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible) return;

        var gd = spriteBatch.GraphicsDevice;
        if (_orthoCam == null)
            _orthoCam = new OrthographicCamera(new DefaultViewportAdapter(gd));

        // Map FRB camera (Y-up world space) to an OrthographicCamera in Tiled's Y-down space.
        // Derivation: matching FRB's screen transform (Translate → Scale with Y-flip → Translate)
        // to OrthographicCamera's view matrix (Translate → Scale → Translate) yields:
        //   mgexPosition.X = camX - mapX - vpW/2
        //   mgexPosition.Y = mapY - camY - vpH/2
        //   mgexZoom       = (vpW / targetW) * frbZoom
        float vpW = gd.Viewport.Width;
        float vpH = gd.Viewport.Height;
        _orthoCam.Position = new Vector2(
            camera.X - AbsoluteX - vpW / 2f,
            AbsoluteY - camera.Y - vpH / 2f);
        _orthoCam.Zoom = vpW / camera.TargetWidth * camera.Zoom;

        _renderer.DrawLayer(spriteBatch, _orthoCam, _layer.Name);
    }
}
