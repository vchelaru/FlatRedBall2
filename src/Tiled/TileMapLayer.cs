using FlatRedBall2.Rendering;

namespace FlatRedBall2.Tiled;

/// <summary>
/// A single tile layer within a <see cref="TileMap"/>. Provides per-layer control over
/// Z-order, visibility, and rendering layer without exposing MonoGame.Extended types.
/// </summary>
/// <remarks>
/// Position is owned by the parent <see cref="TileMap"/> — setting <see cref="TileMap.X"/>
/// or <see cref="TileMap.Y"/> propagates to all layers automatically.
/// </remarks>
public class TileMapLayer
{
    internal TileMapLayerRenderable Renderable { get; }

    internal TileMapLayer(string name, TileMapLayerRenderable renderable)
    {
        Name = name;
        Renderable = renderable;
    }

    /// <summary>
    /// Internal constructor for unit testing — creates a TileMapLayer without a renderable.
    /// Z is stored directly instead of delegating to a renderable.
    /// </summary>
    internal TileMapLayer(string name)
    {
        Name = name;
        Renderable = null!;
        _testZ = 0f;
        _isTestMode = true;
    }

    private float _testZ;
    private readonly bool _isTestMode;

    /// <summary>The layer name as defined in the TMX file.</summary>
    public string Name { get; }

    /// <summary>
    /// Z-order for rendering. Set automatically by <see cref="TileMap"/> on construction:
    /// layers are spaced 1 apart in TMX order, with "GameplayLayer" at Z = 0 if it exists.
    /// Override this to interleave sprites or other renderables between tile layers.
    /// </summary>
    public float Z
    {
        get => _isTestMode ? _testZ : Renderable.Z;
        set
        {
            if (_isTestMode)
                _testZ = value;
            else
                Renderable.Z = value;
        }
    }

    /// <summary>Whether this layer is drawn. Defaults to the TMX layer's visibility.</summary>
    public bool IsVisible
    {
        get => Renderable.IsVisible;
        set => Renderable.IsVisible = value;
    }

    /// <summary>
    /// The FlatRedBall2 rendering layer. Set by <see cref="Screen.Add(TileMapLayer, Layer?)"/>
    /// or manually for fine-grained render-pass control.
    /// </summary>
    public Layer? Layer
    {
        get => Renderable.Layer;
        set => Renderable.Layer = value;
    }
}
