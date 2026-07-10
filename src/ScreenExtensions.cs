using System;
using Gum.Forms.Controls;
using Gum.Wireframe;
using FlatRedBall2.Rendering;

namespace FlatRedBall2;

/// <summary>
/// Extension methods on <see cref="Screen"/> that benefit from generic type inference at the
/// call site (cleaner than equivalent instance methods that would force explicit type args).
/// </summary>
public static class ScreenExtensions
{
    /// <summary>
    /// Adds a Forms control to the primary camera HUD (`screen.Add(...)`), which is
    /// camera-scoped and scales with <see cref="Rendering.Camera.PixelsPerUnit"/>.
    /// </summary>
    public static void AddCameraUi(this Screen screen, FrameworkElement element, Layer? layer = null)
        => screen.Add(element, layer);

    /// <summary>
    /// Adds a Gum visual to the primary camera HUD (`screen.Add(...)`), which is
    /// camera-scoped and scales with <see cref="Rendering.Camera.PixelsPerUnit"/>.
    /// </summary>
    public static void AddCameraUi(this Screen screen, GraphicalUiElement visual, Layer? layer = null)
        => screen.Add(visual, layer);

    /// <summary>
    /// Adds a Forms control to the screen overlay (`screen.AddOverlay(...)`), drawn once
    /// after all camera passes at full-window scale.
    /// </summary>
    public static void AddScreenOverlay(this Screen screen, FrameworkElement element)
        => screen.AddOverlay(element);

    /// <summary>
    /// Adds a Gum visual to the screen overlay (`screen.AddOverlay(...)`), drawn once
    /// after all camera passes at full-window scale.
    /// </summary>
    public static void AddScreenOverlay(this Screen screen, GraphicalUiElement visual)
        => screen.AddOverlay(visual);

    /// <summary>
    /// Restarts the current screen using <paramref name="newConfigure"/> instead of the previously
    /// retained callback. The new callback fully replaces the retained one — both for this restart
    /// and for any future <c>RestartScreen()</c> call that doesn't supply its own. Extension form
    /// lets <typeparamref name="T"/> be inferred from the receiver, so callers write
    /// <c>playerScreen.RestartScreen(s =&gt; s.LevelIndex++)</c> without explicit type args.
    /// </summary>
    public static void RestartScreen<T>(this T screen, Action<T> newConfigure, RestartMode mode = RestartMode.DeathRetry)
        where T : Screen
        => screen.Engine.RequestScreenRestart(s => newConfigure((T)s), mode);
}
