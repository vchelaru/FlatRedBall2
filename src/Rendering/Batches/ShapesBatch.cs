using System;
using Apos.Shapes;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering.Batches;

/// <summary>
/// IRenderBatch that delegates to Apos.Shapes for anti-aliased primitive rendering
/// (filled/outlined rectangles, circles, lines, polygons).
/// Initialized once during FlatRedBallService.Initialize().
/// </summary>
public class ShapesBatch : IRenderBatch
{
    public static readonly ShapesBatch Instance = new();

    private ShapeBatch? _shapeBatch;

    // Called by FlatRedBallService.Initialize so the shader effect is loaded
    // before any shape Draw() call can occur.
    internal void Initialize(GraphicsDevice graphicsDevice, ContentManager content)
        => _shapeBatch = new ShapeBatch(graphicsDevice, content);

    // Exposed so shape Draw() methods can issue primitives directly.
    // Only valid between Begin() and End().
    internal ShapeBatch Shapes => _shapeBatch
        ?? throw new InvalidOperationException(
            "ShapesBatch.Instance has not been initialized. Call FlatRedBallService.Initialize() first.");

    /// <inheritdoc/>
    public bool FlipsY => false; // Shapes convert world→screen via camera.WorldToScreen() themselves

    // Apos.Shapes manages its own pixel-space projection internally.
    // Shape Draw() methods convert world coordinates to screen pixels via camera.WorldToScreen()
    // before submitting to Apos.Shapes, so no view matrix is needed here.
    public void Begin(SpriteBatch spriteBatch, Camera camera)
        => _shapeBatch!.Begin();

    public void End(SpriteBatch spriteBatch) => _shapeBatch!.End();
}
