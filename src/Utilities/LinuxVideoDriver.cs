using System;
using System.Runtime.InteropServices;

namespace FlatRedBall2.Utilities;

/// <summary>
/// Decides the SDL_VIDEODRIVER value that makes a Linux build prefer native Wayland,
/// falling back to X11 (via SDL's comma-separated driver list) when Wayland isn't
/// available, without overriding a value the user already set.
/// </summary>
/// <remarks>
/// MonoGame/KNI's <c>Game</c> constructor calls SDL_Init before any engine code runs, so
/// <see cref="ApplyWaylandFallback"/> must be called at the very top of <c>Program.cs</c>,
/// before constructing the game. Calling it later (e.g. from <c>CustomInitialize</c>) has
/// no effect, since SDL has already picked a video driver by then.
/// </remarks>
public static class LinuxVideoDriver
{
    /// <summary>
    /// Returns the SDL_VIDEODRIVER value to set, or null if nothing should change.
    /// Pure decision logic, separated from <see cref="ApplyWaylandFallback"/> so it can be
    /// unit tested without touching real environment variables or the OS platform.
    /// </summary>
    /// <param name="isLinux">Whether the current OS is Linux.</param>
    /// <param name="existingSdlVideoDriver">The current value of the SDL_VIDEODRIVER environment variable, if any.</param>
    public static string? GetVideoDriverToSet(bool isLinux, string? existingSdlVideoDriver)
    {
        if (!isLinux || !string.IsNullOrEmpty(existingSdlVideoDriver))
        {
            return null;
        }

        return "wayland,x11";
    }

    /// <summary>
    /// Sets SDL_VIDEODRIVER to prefer Wayland with an X11 fallback on Linux, unless it's
    /// already set. No-op on non-Linux platforms. Must be called before constructing the
    /// <c>Game</c> instance; see the remarks on <see cref="LinuxVideoDriver"/>.
    /// </summary>
    public static void ApplyWaylandFallback()
    {
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var existing = Environment.GetEnvironmentVariable("SDL_VIDEODRIVER");
        var toSet = GetVideoDriverToSet(isLinux, existing);

        if (toSet != null)
        {
            // Environment.SetEnvironmentVariable only updates .NET's own private
            // environment-variable table, not the process's real C environment. SDL2 is a
            // native library that reads SDL_VIDEODRIVER via libc getenv(), so it would never
            // observe a value set through Environment.SetEnvironmentVariable. Call libc's
            // setenv() directly so the value is visible to native code in this process.
            setenv("SDL_VIDEODRIVER", toSet, overwrite: 1);
        }
    }

    [DllImport("libc", EntryPoint = "setenv", CallingConvention = CallingConvention.Cdecl)]
    private static extern int setenv(string name, string value, int overwrite);
}
