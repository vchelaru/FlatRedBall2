using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector2 = System.Numerics.Vector2;

namespace FlatRedBall2.Rendering;

public class Camera
{
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float AccelerationX { get; set; }
    public float AccelerationY { get; set; }

    public Color BackgroundColor { get; set; } = Color.Black;

    /// <summary>World units visible horizontally. Managed by the engine; use <see cref="Zoom"/> for runtime zoom.</summary>
    public int TargetWidth { get; internal set; } = 1280;

    /// <summary>World units visible vertically. Managed by the engine; use <see cref="Zoom"/> for runtime zoom.</summary>
    public int TargetHeight { get; internal set; } = 720;

    /// <summary>
    /// Runtime zoom factor. Values greater than 1 zoom in (fewer world units visible);
    /// values less than 1 zoom out (more world units visible). Reset to <see cref="DisplaySettings.Zoom"/> on each screen start.
    /// </summary>
    public float Zoom { get; set; } = 1f;

    /// <summary>
    /// Screen pixels per world unit, accounting for both the viewport size and <see cref="Zoom"/>.
    /// Use this for pixel-perfect calculations such as snapping camera position to the nearest pixel
    /// (<c>snapInterval = 1f / PixelsPerUnit</c>).
    /// </summary>
    public float PixelsPerUnit => _viewport.Height / (float)TargetHeight * Zoom;

    private Viewport _viewport;

    internal Viewport Viewport => _viewport;

    internal void SetViewport(Viewport viewport) => _viewport = viewport;

    internal void PhysicsUpdate(float dt)
    {
        float halfDt2 = 0.5f * dt * dt;
        X += VelocityX * dt + AccelerationX * halfDt2;
        Y += VelocityY * dt + AccelerationY * halfDt2;
        VelocityX += AccelerationX * dt;
        VelocityY += AccelerationY * dt;
    }

    public NumericsVector2 WorldToScreen(NumericsVector2 worldPosition)
    {
        var vpW = (float)_viewport.Width;
        var vpH = (float)_viewport.Height;
        var scaleX = vpW / TargetWidth * Zoom;
        var scaleY = vpH / TargetHeight * Zoom;
        return new NumericsVector2(
            (worldPosition.X - X) * scaleX + vpW / 2f,
            -(worldPosition.Y - Y) * scaleY + vpH / 2f);
    }

    public NumericsVector2 ScreenToWorld(NumericsVector2 screenPosition)
    {
        var vpW = (float)_viewport.Width;
        var vpH = (float)_viewport.Height;
        var scaleX = vpW / TargetWidth * Zoom;
        var scaleY = vpH / TargetHeight * Zoom;
        return new NumericsVector2(
            (screenPosition.X - vpW / 2f) / scaleX + X,
            -(screenPosition.Y - vpH / 2f) / scaleY + Y);
    }

    public Matrix GetTransformMatrix()
    {
        var vpW = (float)_viewport.Width;
        var vpH = (float)_viewport.Height;
        return Matrix.CreateTranslation(-X, -Y, 0)
            * Matrix.CreateScale(vpW / TargetWidth * Zoom, -(vpH / TargetHeight * Zoom), 1f)
            * Matrix.CreateTranslation(vpW / 2f, vpH / 2f, 0f);
    }
}
