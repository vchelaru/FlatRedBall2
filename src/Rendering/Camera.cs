using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Gum.Forms.Controls;
using Gum.Wireframe;
using MonoGameGum.GueDeriving;
using NumericsVector2 = System.Numerics.Vector2;

namespace FlatRedBall2.Rendering;

/// <summary>
/// World-space view origin. The camera's <see cref="X"/>/<see cref="Y"/> sit at the center
/// of the visible area; <see cref="Left"/>/<see cref="Right"/>/<see cref="Top"/>/<see cref="Bottom"/>
/// derive from that center plus <see cref="OrthogonalWidth"/>/<see cref="OrthogonalHeight"/> and
/// <see cref="Zoom"/>.
/// <para>
/// Camera velocity and acceleration are integrated each frame by the engine
/// (same second-order kinematic step as <see cref="Entity"/>: <c>pos += vel*dt + acc*dt²/2</c>),
/// so a moving camera can be set up via <see cref="VelocityX"/>/<see cref="VelocityY"/> instead
/// of mutating <see cref="X"/>/<see cref="Y"/> by hand each frame.
/// </para>
/// <para>
/// <b>Y+ is up</b> in world space; the camera transform (see <see cref="GetTransformMatrix"/>)
/// flips Y to screen space (Y+ down) for rendering.
/// </para>
/// </summary>
public class Camera
{
    /// <summary>World-space X of the center of the visible area.</summary>
    public float X { get; set; }

    /// <summary>World-space Y of the center of the visible area (Y+ up).</summary>
    public float Y { get; set; }

    /// <summary>X velocity in world units / second. Integrated each frame by the engine.</summary>
    public float VelocityX { get; set; }

    /// <summary>Y velocity in world units / second (Y+ up). Integrated each frame by the engine.</summary>
    public float VelocityY { get; set; }

    /// <summary>X acceleration in world units / second². Integrated each frame by the engine.</summary>
    public float AccelerationX { get; set; }

    /// <summary>Y acceleration in world units / second² (Y+ up). Integrated each frame by the engine.</summary>
    public float AccelerationY { get; set; }

    /// <summary>Color cleared to the back buffer before any renderable draws. Default <c>Black</c>.</summary>
    public Color BackgroundColor { get; set; } = Color.Black;

    /// <summary>World units visible horizontally at <see cref="Zoom"/> = 1. Managed by the engine from <see cref="DisplaySettings"/>; use <see cref="Zoom"/> for runtime zoom.</summary>
    public int OrthogonalWidth { get; internal set; } = 1280;

    /// <summary>World units visible vertically at <see cref="Zoom"/> = 1. Managed by the engine from <see cref="DisplaySettings"/>; use <see cref="Zoom"/> for runtime zoom.</summary>
    public int OrthogonalHeight { get; internal set; } = 720;

    /// <summary>
    /// This camera's region inside the engine's host viewport, expressed as fractions of the
    /// host rect (0..1). Defaults to the full host rect. Set to <c>(0, 0, 0.5f, 1f)</c> for a
    /// left-half split-screen player, etc. The engine recomputes <see cref="Viewport"/> from
    /// this each frame, so resizing the window keeps the split clean automatically.
    /// <para>
    /// Aspect note: each camera's <see cref="OrthogonalWidth"/> is derived from its viewport's
    /// pixel aspect ratio. If you want a specific world aspect, pick normalized coordinates
    /// that produce that pixel aspect — there is no per-camera letterbox.
    /// </para>
    /// </summary>
    public NormalizedRectangle NormalizedViewport { get; set; } = NormalizedRectangle.FullViewport;

    /// <summary>World-space X coordinate of the left edge of the visible area. Accounts for <see cref="Zoom"/>.</summary>
    public float Left => X - OrthogonalWidth / (2f * Zoom);

    /// <summary>World-space X coordinate of the right edge of the visible area. Accounts for <see cref="Zoom"/>.</summary>
    public float Right => X + OrthogonalWidth / (2f * Zoom);

    /// <summary>World-space Y coordinate of the bottom edge of the visible area. Accounts for <see cref="Zoom"/> (Y+ up).</summary>
    public float Bottom => Y - OrthogonalHeight / (2f * Zoom);

    /// <summary>World-space Y coordinate of the top edge of the visible area. Accounts for <see cref="Zoom"/> (Y+ up).</summary>
    public float Top => Y + OrthogonalHeight / (2f * Zoom);

    /// <summary>
    /// Runtime zoom factor. Values greater than 1 zoom in (fewer world units visible);
    /// values less than 1 zoom out (more world units visible). Reset to 1 on each screen start —
    /// screens wanting a non-default starting zoom assign in <see cref="Screen.CustomInitialize"/>.
    /// Most games leave this at 1 and express their on-screen scale via the
    /// <see cref="DisplaySettings.PreferredWindowWidth"/>-vs-<see cref="DisplaySettings.ResolutionWidth"/> ratio;
    /// reserve runtime <c>Zoom</c> changes for cinematic effects (boss zoom-in, screen shake, etc.).
    /// </summary>
    public float Zoom { get; set; } = 1f;

    /// <summary>
    /// Screen pixels per world unit, accounting for both the viewport size and <see cref="Zoom"/>.
    /// Use this for pixel-perfect calculations such as snapping camera position to the nearest pixel
    /// (<c>snapInterval = 1f / PixelsPerUnit</c>).
    /// </summary>
    public float PixelsPerUnit => _viewport.Height / (float)OrthogonalHeight * Zoom;

    private Viewport _viewport;

    internal Viewport Viewport => _viewport;

    internal void SetViewport(Viewport viewport) => _viewport = viewport;

    // ---------- HUD root (per-camera Gum tree) ----------

    private GraphicalUiElement? _hudRoot;
    private float _lastHudCanvasWidth = float.NaN;
    private float _lastHudCanvasHeight = float.NaN;

    /// <summary>
    /// This camera's lazily-created Gum root. Visuals added via <see cref="Add(GraphicalUiElement, FlatRedBall2.Rendering.Layer?)"/>
    /// are parented under this element and laid out against the camera's own canvas dimensions —
    /// each split-screen viewport gets its own design-space anchor frame instead of sharing
    /// <c>Cameras[0]</c>'s.
    /// </summary>
    public GraphicalUiElement HudRoot => _hudRoot ??= new ContainerRuntime();

    /// <summary>
    /// The screen this camera is associated with. Set by <see cref="Screen"/> when this camera is
    /// part of <see cref="Screen.Cameras"/>; required by <see cref="Add(GraphicalUiElement, FlatRedBall2.Rendering.Layer?)"/>
    /// to register the GumRenderable on the correct render list.
    /// </summary>
    internal FlatRedBall2.Screen? Screen { get; set; }

    /// <summary>Test-only counter incremented every time <see cref="EnsureHudLayout"/> performs a real layout pass.</summary>
    internal int HudLayoutCallCount { get; private set; }

    /// <summary>
    /// Calls <c>UpdateLayout</c> on <see cref="HudRoot"/> only when the supplied canvas dims differ
    /// from the dims used on the previous call. The single gated entry point for HUD layout —
    /// callers must not call <c>UpdateLayout</c> directly on <see cref="HudRoot"/>.
    /// </summary>
    internal void EnsureHudLayout(float canvasWidth, float canvasHeight)
    {
        if (canvasWidth == _lastHudCanvasWidth && canvasHeight == _lastHudCanvasHeight)
            return;
        _lastHudCanvasWidth = canvasWidth;
        _lastHudCanvasHeight = canvasHeight;
        HudRoot.UpdateLayout();
        HudLayoutCallCount++;
    }

    /// <summary>
    /// Adds <paramref name="visual"/> to this camera's HUD. The visual is parented under
    /// <see cref="HudRoot"/> (so its layout is computed against this camera's canvas dimensions)
    /// and registered on <see cref="Screen"/>'s render list with this camera as the owner —
    /// only this camera's draw pass will render it.
    /// </summary>
    public void Add(GraphicalUiElement visual, Layer? layer = null)
    {
        if (Screen == null)
            throw new System.InvalidOperationException(
                "Camera.Add requires the camera to be part of a Screen. Add the camera to Screen.Cameras first.");
        HudRoot.Children.Add(visual);
        Screen.AddGumForCamera(visual, this, layer);
    }

    /// <summary>Adds a Gum Forms control to this camera's HUD. See <see cref="Add(GraphicalUiElement, Layer?)"/>.</summary>
    public void Add(FrameworkElement element, Layer? layer = null) => Add(element.Visual, layer);

    /// <summary>Removes a Gum visual previously added with <see cref="Add(GraphicalUiElement, Layer?)"/>.</summary>
    public void Remove(GraphicalUiElement visual)
    {
        HudRoot.Children.Remove(visual);
        Screen?.RemoveGumForCamera(visual);
    }

    /// <summary>Removes a Gum Forms control previously added with <see cref="Add(FrameworkElement, Layer?)"/>.</summary>
    public void Remove(FrameworkElement element) => Remove(element.Visual);

    /// <summary>
    /// Resolves <see cref="NormalizedViewport"/> against <paramref name="hostRect"/> to produce
    /// this camera's pixel <see cref="Viewport"/>, then sets <see cref="OrthogonalHeight"/> from
    /// <paramref name="orthogonalHeight"/> and derives <see cref="OrthogonalWidth"/> from the
    /// resulting pixel aspect. Called by the engine each frame and on resize.
    /// </summary>
    internal void ApplyToHostRect(Viewport hostRect, int orthogonalHeight)
    {
        int x = hostRect.X + (int)(NormalizedViewport.X * hostRect.Width);
        int y = hostRect.Y + (int)(NormalizedViewport.Y * hostRect.Height);
        int w = (int)(NormalizedViewport.Width * hostRect.Width);
        int h = (int)(NormalizedViewport.Height * hostRect.Height);
        _viewport = new Viewport(x, y, w, h);

        OrthogonalHeight = orthogonalHeight;
        OrthogonalWidth = h > 0 ? (int)(orthogonalHeight * (w / (float)h)) : orthogonalHeight;
    }

    internal void PhysicsUpdate(float dt)
    {
        float halfDt2 = 0.5f * dt * dt;
        X += VelocityX * dt + AccelerationX * halfDt2;
        Y += VelocityY * dt + AccelerationY * halfDt2;
        VelocityX += AccelerationX * dt;
        VelocityY += AccelerationY * dt;
    }

    /// <summary>
    /// Projects a world-space position to screen-space pixels (origin top-left, Y+ down).
    /// Accounts for camera position, viewport size, and <see cref="Zoom"/>. Inverse of
    /// <see cref="ScreenToWorld"/>.
    /// </summary>
    public NumericsVector2 WorldToScreen(NumericsVector2 worldPosition)
    {
        var vpW = (float)_viewport.Width;
        var vpH = (float)_viewport.Height;
        var scaleX = vpW / OrthogonalWidth * Zoom;
        var scaleY = vpH / OrthogonalHeight * Zoom;
        return new NumericsVector2(
            (worldPosition.X - X) * scaleX + vpW / 2f,
            -(worldPosition.Y - Y) * scaleY + vpH / 2f);
    }

    /// <summary>
    /// Unprojects a screen-space pixel position (origin top-left, Y+ down) to a world-space
    /// position (Y+ up). Useful for picking, mouse-to-world, etc. Inverse of <see cref="WorldToScreen"/>.
    /// </summary>
    public NumericsVector2 ScreenToWorld(NumericsVector2 screenPosition)
    {
        var vpW = (float)_viewport.Width;
        var vpH = (float)_viewport.Height;
        var scaleX = vpW / OrthogonalWidth * Zoom;
        var scaleY = vpH / OrthogonalHeight * Zoom;
        return new NumericsVector2(
            (screenPosition.X - vpW / 2f) / scaleX + X,
            -(screenPosition.Y - vpH / 2f) / scaleY + Y);
    }

    /// <summary>
    /// Builds the world-to-screen transform passed to <c>SpriteBatch.Begin</c> by
    /// world-space batches. Translates by <c>-Camera.Position</c>, scales by viewport-vs-target
    /// ratio times <see cref="Zoom"/>, flips Y (world Y+ up → screen Y+ down), and recenters
    /// in the viewport. <see cref="Sprite.Draw"/> compensates for the Y-flip via
    /// <see cref="IRenderBatch.FlipsY"/> so texture pixels remain upright.
    /// </summary>
    public Matrix GetTransformMatrix()
    {
        var vpW = (float)_viewport.Width;
        var vpH = (float)_viewport.Height;
        return Matrix.CreateTranslation(-X, -Y, 0)
            * Matrix.CreateScale(vpW / OrthogonalWidth * Zoom, -(vpH / OrthogonalHeight * Zoom), 1f)
            * Matrix.CreateTranslation(vpW / 2f, vpH / 2f, 0f);
    }
}
