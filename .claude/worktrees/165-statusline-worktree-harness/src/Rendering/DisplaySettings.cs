using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering;

/// <summary>
/// Controls whether the game runs in a normal window or fills the screen without a title bar or border.
/// FlatRedBall2 does not support exclusive (hardware) fullscreen because it causes display-mode switches
/// and related problems on modern systems.
/// </summary>
public enum WindowMode
{
    /// <summary>Normal OS window with a title bar and border.</summary>
    Windowed,

    /// <summary>
    /// Borderless window that covers the entire display at its native resolution.
    /// Avoids the display-mode switch of true fullscreen while still hiding the desktop.
    /// </summary>
    FullscreenBorderless,
}

/// <summary>
/// How the visible world responds when the window resizes (orthogonal to <see cref="AspectPolicy"/>).
/// </summary>
public enum ResizeMode
{
    /// <summary>
    /// The same world area is always visible along the dominant axis; a larger window just rescales
    /// pixels. Combined with <see cref="AspectPolicy.Locked"/> this also fixes the non-dominant axis,
    /// so the entire design world stays put no matter how the window is shaped.
    /// </summary>
    StretchVisibleArea,

    /// <summary>
    /// Pixels-per-world-unit is fixed; a larger window reveals more world. Useful for pixel-art games
    /// that want sprites to render at exactly N native pixels regardless of window size.
    /// </summary>
    IncreaseVisibleArea,
}

/// <summary>
/// Whether the camera's visible world is locked to a specific aspect ratio (with letterbox/pillarbox bars
/// on aspect mismatch) or freely fills the window (no bars; the world's aspect follows the window's).
/// </summary>
public enum AspectPolicy
{
    /// <summary>
    /// The visible world is constrained to a fixed aspect ratio. The viewport is centered inside the
    /// window with letterbox/pillarbox bars filling the remainder. The fixed ratio comes from
    /// <see cref="DisplaySettings.FixedAspectRatio"/>; if that is <c>null</c>, the ratio is derived from
    /// <see cref="DisplaySettings.ResolutionWidth"/> ÷ <see cref="DisplaySettings.ResolutionHeight"/>.
    /// </summary>
    Locked,

    /// <summary>
    /// The viewport fills the window with no bars. The visible world's aspect follows the window's,
    /// with one design axis (per <see cref="DisplaySettings.DominantAxis"/>) pinned to its
    /// <c>Resolution*</c> value. Resizing along the non-dominant axis changes how much world is visible.
    /// </summary>
    Free,
}

/// <summary>
/// Which design axis stays at its <c>Resolution*</c> value when the visible world's aspect is allowed
/// to differ from the design's. Consulted when <see cref="DisplaySettings.AspectPolicy"/> is
/// <see cref="AspectPolicy.Free"/>, or when an explicit <see cref="DisplaySettings.FixedAspectRatio"/>
/// differs from the design ratio under <see cref="AspectPolicy.Locked"/>.
/// </summary>
public enum DominantAxis
{
    /// <summary>Width is pinned to <see cref="DisplaySettings.ResolutionWidth"/>; height tracks the effective aspect.</summary>
    Width,

    /// <summary>Height is pinned to <see cref="DisplaySettings.ResolutionHeight"/>; width tracks the effective aspect.</summary>
    Height,
}

/// <summary>
/// Display configuration that controls both camera behavior and (optionally) window properties.
/// <para>
/// The engine owns a default instance (<see cref="FlatRedBallService.DisplaySettings"/>) that is set once
/// at startup and governs every screen that does not override it. Each <see cref="Screen"/> may declare
/// its own instance via <see cref="Screen.PreferredDisplaySettings"/> to override the camera properties
/// (zoom, aspect policy, resize mode, dominant axis, fixed ratio) for that screen alone.
/// </para>
/// <para>
/// <b>Window properties</b> (<see cref="WindowMode"/>, <see cref="PreferredWindowWidth"/>,
/// <see cref="PreferredWindowHeight"/>, <see cref="AllowUserResizing"/>) are only applied automatically
/// when a screen is the <em>starting</em> screen. They are never changed automatically during mid-game
/// screen transitions so the window does not pop or resize while the player is playing. To change window
/// properties at runtime (e.g. a settings menu fullscreen toggle), call
/// <see cref="FlatRedBallService.ApplyWindowSettings"/> directly.
/// </para>
/// </summary>
public class DisplaySettings
{
    /// <summary>
    /// Preferred window width in pixels. Applied only when this screen is the starting screen.
    /// <c>null</c> leaves the window at whatever size it was set to in the game's constructor.
    /// </summary>
    public int? PreferredWindowWidth { get; set; }

    /// <summary>
    /// Preferred window height in pixels. Applied only when this screen is the starting screen.
    /// <c>null</c> leaves the window at whatever size it was set to in the game's constructor.
    /// </summary>
    public int? PreferredWindowHeight { get; set; }

    /// <summary>
    /// Whether the game runs in a normal window or borderless fullscreen.
    /// Applied only when this screen is the starting screen.
    /// When <see cref="WindowMode.FullscreenBorderless"/>, the window expands to the native display
    /// resolution regardless of <see cref="PreferredWindowWidth"/>/<see cref="PreferredWindowHeight"/>.
    /// </summary>
    public WindowMode WindowMode { get; set; } = WindowMode.Windowed;

    /// <summary>
    /// Whether the player can resize the window by dragging its borders. Defaults to <c>false</c> —
    /// fixed-canvas is the safer default and avoids surprise aspect distortion on mid-play resize.
    /// <para>
    /// Applied only when this screen is the starting screen. Ignored when <see cref="WindowMode"/> is
    /// <see cref="WindowMode.FullscreenBorderless"/>. On KNI BlazorGL, set this to <c>true</c> when the
    /// host canvas is allowed to drive the back-buffer size; the engine still honors
    /// <see cref="AspectPolicy"/> and pillarbox/letterboxes inside the host-managed canvas.
    /// </para>
    /// </summary>
    public bool AllowUserResizing { get; set; } = false;

    /// <summary>
    /// Design world width in world units. Camera <c>Left</c>/<c>Right</c> derive from this when the
    /// visible-world width is pinned (see <see cref="AspectPolicy"/>, <see cref="DominantAxis"/>,
    /// <see cref="ResizeMode"/>).
    /// </summary>
    public int ResolutionWidth { get; set; } = 1280;

    /// <summary>
    /// Design world height in world units. Camera <c>Top</c>/<c>Bottom</c> derive from this when the
    /// visible-world height is pinned (see <see cref="AspectPolicy"/>, <see cref="DominantAxis"/>,
    /// <see cref="ResizeMode"/>).
    /// </summary>
    public int ResolutionHeight { get; set; } = 720;

    /// <summary>
    /// How the view responds when the window is resized. See enum members for details. Orthogonal
    /// to <see cref="AspectPolicy"/>: with <see cref="AspectPolicy.Locked"/> a window resize that
    /// preserves the locked aspect just rescales the viewport (Stretch) or reveals more world on both
    /// axes (Increase); with <see cref="AspectPolicy.Free"/> the non-dominant axis follows the window
    /// regardless of mode.
    /// </summary>
    public ResizeMode ResizeMode { get; set; } = ResizeMode.StretchVisibleArea;

    /// <summary>
    /// Whether the visible world is locked to a fixed aspect ratio (with bars on mismatch) or freely
    /// fills the window. Defaults to <see cref="AspectPolicy.Locked"/> — the safe choice that prevents
    /// pixel distortion and surprise extra world becoming visible when the window aspect changes.
    /// </summary>
    public AspectPolicy AspectPolicy { get; set; } = AspectPolicy.Locked;

    /// <summary>
    /// Aspect ratio (width ÷ height) used by <see cref="AspectPolicy.Locked"/>. <c>null</c> derives the
    /// ratio from <see cref="ResolutionWidth"/> ÷ <see cref="ResolutionHeight"/> — the typical case.
    /// Set explicitly to render the design at a different on-screen aspect (e.g. multiple internal
    /// resolutions sharing one display ratio). Ignored when <see cref="AspectPolicy"/> is
    /// <see cref="AspectPolicy.Free"/>.
    /// </summary>
    public float? FixedAspectRatio { get; set; }

    /// <summary>
    /// Which design axis stays at its <c>Resolution*</c> value when the visible-world aspect differs
    /// from the design's. Consulted under <see cref="AspectPolicy.Free"/> (window shape decides the
    /// other axis) and under <see cref="AspectPolicy.Locked"/> with an explicit
    /// <see cref="FixedAspectRatio"/> that differs from the design ratio.
    /// </summary>
    public DominantAxis DominantAxis { get; set; } = DominantAxis.Height;

    /// <summary>Color of letterbox/pillarbox bars. Only painted when <see cref="AspectPolicy"/> is <see cref="AspectPolicy.Locked"/>.</summary>
    public Color LetterboxColor { get; set; } = Color.Black;

    /// <summary>
    /// Effective target aspect ratio: <see cref="FixedAspectRatio"/> if set, else
    /// <see cref="ResolutionWidth"/> ÷ <see cref="ResolutionHeight"/>.
    /// </summary>
    internal float GetEffectiveAspectRatio()
        => FixedAspectRatio ?? (ResolutionWidth / (float)ResolutionHeight);

    /// <summary>
    /// Computes the camera's destination viewport rectangle inside the window. Under
    /// <see cref="AspectPolicy.Locked"/> this is centered with letterbox/pillarbox bars to enforce the
    /// effective aspect ratio. Under <see cref="AspectPolicy.Free"/> it fills the window.
    /// </summary>
    internal Viewport ComputeDestinationViewport(int windowWidth, int windowHeight)
    {
        if (AspectPolicy == AspectPolicy.Free)
            return new Viewport(0, 0, windowWidth, windowHeight);

        float aspect = GetEffectiveAspectRatio();
        float windowAspect = windowWidth / (float)windowHeight;

        int destW, destH, destX, destY;
        if (windowAspect > aspect)
        {
            // Window is wider than target — pillarbox bars on sides
            destH = windowHeight;
            destW = (int)(windowHeight * aspect);
            destX = (windowWidth - destW) / 2;
            destY = 0;
        }
        else
        {
            // Window is taller than target — letterbox bars on top/bottom
            destW = windowWidth;
            destH = (int)(windowWidth / aspect);
            destX = 0;
            destY = (windowHeight - destH) / 2;
        }

        return new Viewport(destX, destY, destW, destH);
    }
}
