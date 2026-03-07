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

/// <summary>How the visible world area responds when the window is resized.</summary>
public enum ResizeMode
{
    /// <summary>The same world area is always visible; the view scales to fill the window.</summary>
    StretchVisibleArea,

    /// <summary>The pixels-per-world-unit ratio stays fixed; a larger window shows more of the world.</summary>
    IncreaseVisibleArea,
}

/// <summary>
/// Display configuration that controls both camera behavior and (optionally) window properties.
/// <para>
/// The engine owns a default instance (<see cref="FlatRedBallService.DisplaySettings"/>) that is set once
/// at startup and governs every screen that does not override it.
/// Each <see cref="Screen"/> may declare its own instance via <see cref="Screen.PreferredDisplaySettings"/>
/// to override the camera properties (zoom, resize mode, aspect ratio) for that screen alone.
/// </para>
/// <para>
/// <b>Window properties</b> (<see cref="WindowMode"/>, <see cref="PreferredWindowWidth"/>,
/// <see cref="PreferredWindowHeight"/>, <see cref="AllowUserResizing"/>) are only applied
/// automatically when a screen is the <em>starting</em> screen (i.e., passed to
/// <see cref="FlatRedBallService.Start{T}"/>). They are never changed automatically during
/// mid-game screen transitions so the window does not pop or resize while the player is playing.
/// To change window properties at runtime (e.g., a settings menu fullscreen toggle), call
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
    /// When <see cref="WindowMode.FullscreenBorderless"/>, the window expands to the native
    /// display resolution regardless of <see cref="PreferredWindowWidth"/>/<see cref="PreferredWindowHeight"/>.
    /// </summary>
    public WindowMode WindowMode { get; set; } = WindowMode.Windowed;

    /// <summary>
    /// Whether the player can drag the window border to resize it.
    /// Applied only when this screen is the starting screen.
    /// Ignored when <see cref="WindowMode"/> is <see cref="WindowMode.FullscreenBorderless"/>.
    /// </summary>
    public bool AllowUserResizing { get; set; } = true;

    /// <summary>
    /// World units visible horizontally at <see cref="Zoom"/> = 1.
    /// Used as the fixed world width for <see cref="ResizeMode.StretchVisibleArea"/>.
    /// </summary>
    public int ResolutionWidth { get; set; } = 1280;

    /// <summary>
    /// World units visible vertically at <see cref="Zoom"/> = 1.
    /// Used as the fixed world height for <see cref="ResizeMode.StretchVisibleArea"/>.
    /// </summary>
    public int ResolutionHeight { get; set; } = 720;

    /// <summary>
    /// Initial camera zoom. Copied to <see cref="Camera.Zoom"/> at the start of each screen.
    /// At 1.0, one world unit equals one pixel (at the reference window size).
    /// At 2.0, one world unit equals two pixels — world objects appear twice as large.
    /// For <see cref="ResizeMode.IncreaseVisibleArea"/>, this also fixes the pixels-per-world-unit
    /// ratio so that resizing the window reveals more world rather than stretching.
    /// </summary>
    public float Zoom { get; set; } = 1f;

    /// <summary>How the view responds when the window is resized.</summary>
    public ResizeMode ResizeMode { get; set; } = ResizeMode.StretchVisibleArea;

    /// <summary>
    /// Enforces a fixed aspect ratio with letterbox or pillarbox bars. <c>null</c> fills the entire window.
    /// Example: <c>16f / 9f</c> for widescreen.
    /// </summary>
    public float? FixedAspectRatio { get; set; }

    /// <summary>Color of letterbox/pillarbox bars. Only used when <see cref="FixedAspectRatio"/> is set.</summary>
    public Color LetterboxColor { get; set; } = Color.Black;

    /// <summary>
    /// Computes the viewport rectangle that fits the target aspect ratio inside the given window dimensions,
    /// centered with letterbox or pillarbox bars. Returns the full window viewport when <see cref="FixedAspectRatio"/> is null.
    /// </summary>
    internal Viewport ComputeDestinationViewport(int windowWidth, int windowHeight)
    {
        if (!FixedAspectRatio.HasValue)
            return new Viewport(0, 0, windowWidth, windowHeight);

        float aspect = FixedAspectRatio.Value;
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
