using System;
using System.Diagnostics;
using System.Reflection;
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
    // ── Precompiled shader version guard ──────────────────────────────────
    //
    // FlatRedBall2 ships precompiled apos-shapes.xnb files so that macOS/Linux
    // users don't need Wine for shader compilation. This constant must match the
    // version of Apos.Shapes whose shader was used to produce those XNBs.
    //
    // ONLY UPDATE THIS AFTER:
    //   1. Updating the Apos.Shapes NuGet version in FlatRedBall2.csproj
    //   2. Rebuilding a sample on each platform (DesktopGL, BlazorGL) so the
    //      content pipeline compiles fresh apos-shapes.xnb files
    //   3. Copying the new XNBs into src/PrecompiledShaders/<platform>/
    //   4. Verifying each sample runs correctly with the new shaders
    //
    internal const string AposShapesVersion = "0.6.8";

    /// <summary>The shared singleton instance.</summary>
    public static readonly ShapesBatch Instance = new();

    private ShapeBatch? _shapeBatch;

    // Called by FlatRedBallService.Initialize so the shader effect is loaded
    // before any shape Draw() call can occur.
    internal void Initialize(GraphicsDevice graphicsDevice, ContentManager content)
    {
        ValidateAposShapesVersion();
        _shapeBatch = new ShapeBatch(graphicsDevice, content);
    }

    [Conditional("DEBUG")]
    private static void ValidateAposShapesVersion()
    {
        var aposVersion = typeof(ShapeBatch).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        // InformationalVersion may include "+commitHash" suffix — compare only the version prefix
        if (aposVersion != null)
        {
            var plusIndex = aposVersion.IndexOf('+');
            if (plusIndex >= 0)
                aposVersion = aposVersion[..plusIndex];
        }

        if (aposVersion != null && aposVersion != AposShapesVersion)
        {
            throw new InvalidOperationException(
                $"Apos.Shapes version mismatch: engine expects {AposShapesVersion} " +
                $"(precompiled shader version) but found {aposVersion}. " +
                $"The precompiled XNBs in src/PrecompiledShaders/ must be rebuilt. " +
                $"See the comment on ShapesBatch.AposShapesVersion for instructions.");
        }
    }

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
    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera)
        => _shapeBatch!.Begin();

    /// <inheritdoc/>
    public void End(SpriteBatch spriteBatch)
    {
        try
        {
            _shapeBatch!.End();
        }
        catch (NotSupportedException ex) when (ex.Message.Contains("ThirtyTwoBits"))
        {
            throw new NotSupportedException(
                "Too many shapes for GraphicsProfile.Reach (16-bit index buffer limit exceeded). " +
                "Either reduce the number of visible shapes (e.g. clean up off-screen tiles) or " +
                "switch to HiDef: set graphics.GraphicsProfile = GraphicsProfile.HiDef " +
                "in your Game1 constructor before Initialize().",
                ex);
        }
    }
}