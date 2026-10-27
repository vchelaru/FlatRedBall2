using System;
using Apos.Shapes;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering.Batches;

/// <summary>
/// IRenderBatch that delegates to Apos.Shapes for anti-aliased primitive rendering
/// (filled/outlined rectangles, circles, lines, polygons).
/// Initialized once during FlatRedBallService.Initialize().
/// </summary>
public class ShapesBatch : IRenderBatch
{
    /// <summary>The shared singleton instance.</summary>
    public static readonly ShapesBatch Instance = new();

    private GraphicsDevice? _graphicsDevice;
    private ShapeBatch? _shapeBatch;
    private bool _initialized;

    // Called by FlatRedBallService.Initialize. Apos.Shapes' shader is embedded in its
    // assembly (0.7.2+), so no ContentManager / content pipeline is involved. The ShapeBatch
    // itself is created on first use, so a game that never draws a shape never loads the shader or
    // allocates its buffers.
    internal void Initialize(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _shapeBatch = null;
        _initialized = true;
    }

    internal bool IsShapeBatchCreated => _shapeBatch != null;

    // Exposed so shape Draw() methods can issue primitives directly.
    // Only valid between Begin() and End().
    internal ShapeBatch Shapes
    {
        get
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "ShapesBatch.Instance has not been initialized. Call FlatRedBallService.Initialize() first.");
            }
            return _shapeBatch ??= new ShapeBatch(_graphicsDevice!);
        }
    }

    /// <inheritdoc/>
    public bool FlipsY => false; // Shapes convert world→screen via camera.WorldToScreen() themselves

    // Does not override IRenderBatch.InternalDrawCallCount: Apos.Shapes' ShapeBatch (checked up
    // to 0.8.1) exposes no draw-call/state-change counter to read. Revisit if a future version adds one.

    // Apos.Shapes manages its own pixel-space projection internally.
    // Shape Draw() methods convert world coordinates to screen pixels via camera.WorldToScreen()
    // before submitting to Apos.Shapes, so no view matrix is needed here.
    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera)
        => Shapes.Begin();

    /// <inheritdoc/>
    public void End(SpriteBatch spriteBatch)
    {
        try
        {
            Shapes.End();
        }
        catch (NotSupportedException ex) when (ex.Message.Contains("ThirtyTwoBits"))
        {
            throw new NotSupportedException(
                "Too many shapes for GraphicsProfile.Reach (16-bit index buffer limit exceeded). " +
                "Either reduce the number of visible shapes (e.g. clean up off-screen tiles) or " +
                "set graphics.GraphicsProfile = GraphicsProfile.FL10_0 " +
                "in your Game1 constructor before Initialize().",
                ex);
        }
    }
}