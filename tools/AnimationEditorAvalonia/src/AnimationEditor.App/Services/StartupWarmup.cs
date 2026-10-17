using AnimationEditor.Core.IO;
using System;
using System.Threading;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Services;

/// <summary>
/// Runs work that doesn't need the UI thread on the thread pool while Avalonia is still
/// initializing the platform on the main thread, so the window constructor finds it already done:
/// the settings read and the one-time cost of loading the SVG stack that the toolbar icons use.
/// </summary>
internal static class StartupWarmup
{
    // Any valid SVG; the first parse is what pays for loading the SVG stack, not the content.
    private const string WarmupSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\" viewBox=\"0 0 16 16\">" +
        "<path d=\"M2 2h12v12H2z\" fill=\"none\" stroke=\"currentColor\"/></svg>";

    private static SettingsPreload? _settings;

    /// <summary>Starts all startup warm-ups. Call once, before Avalonia initializes.</summary>
    public static void Begin(FilePath settingsFile)
    {
        PreloadSettings(settingsFile);
        Task.Run(WarmSvg);
    }

    /// <summary>Starts reading <paramref name="settingsFile"/> in the background.</summary>
    public static SettingsPreload PreloadSettings(FilePath settingsFile)
    {
        var preload = new SettingsPreload(settingsFile);
        Volatile.Write(ref _settings, preload);
        return preload;
    }

    /// <summary>
    /// The preloaded settings for <paramref name="settingsFile"/>, or null when none were started
    /// for that path (or already taken), in which case the caller reads the file itself.
    /// </summary>
    public static Core.Models.AppSettingsModel? TakeSettings(FilePath settingsFile) =>
        Volatile.Read(ref _settings)?.Take(settingsFile);

    /// <summary>
    /// Parses a throwaway SVG so the SVG libraries are loaded and JIT-warm before the window's
    /// icons need them. Best effort: returns false on failure, since the icons load fine without it.
    /// </summary>
    internal static bool WarmSvg()
    {
        try
        {
            return Avalonia.Svg.Skia.SvgSource.LoadFromSvg(WarmupSvg) is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
