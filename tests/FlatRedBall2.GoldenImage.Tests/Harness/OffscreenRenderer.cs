using FlatRedBall2.Diagnostics;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.GoldenImage;

/// <summary>Renders a <see cref="Screen"/> to an offscreen target and reads the pixels back.</summary>
public static class OffscreenRenderer
{
    /// <summary>
    /// Draws <paramref name="screen"/> through the engine's real <c>Screen.Draw</c> path.
    /// </summary>
    /// <param name="cameraX">World X at the center of the image.</param>
    /// <param name="cameraY">World Y at the center of the image (Y+ is up).</param>
    /// <param name="background">
    /// Defaults to opaque dark blue. Keep it opaque: PNGs hold the premultiplied read-back, so
    /// semi-transparent output is hard to judge by eye.
    /// </param>
    /// <remarks>One world unit is one pixel (the camera's orthogonal height equals the image height).</remarks>
    public static PixelImage Render(
        GraphicsDevice device, Screen screen, int width, int height,
        float cameraX = 0f, float cameraY = 0f, Color? background = null)
    {
        using var target = new RenderTarget2D(device, width, height);
        using var spriteBatch = new SpriteBatch(device);

        var camera = new Camera { X = cameraX, Y = cameraY };
        camera.ApplyToHostRect(new Viewport(0, 0, width, height), orthogonalHeight: height);

        device.SetRenderTarget(target);
        device.Clear(background ?? new Color(24, 32, 64));
        try
        {
            screen.Draw(spriteBatch, new RenderDiagnostics(), camera);
        }
        finally
        {
            device.SetRenderTarget(null);
        }

        var pixels = new Color[width * height];
        target.GetData(pixels);
        return new PixelImage(width, height, pixels);
    }
}
