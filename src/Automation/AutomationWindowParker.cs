using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace FlatRedBall2.Automation;

/// <summary>The native window operations <see cref="AutomationWindowParker"/> needs. Faked in tests.</summary>
internal interface IWindowPlacer
{
    /// <summary>The game's main window, or <see cref="IntPtr.Zero"/> if it does not exist yet.</summary>
    IntPtr MainWindow { get; }

    /// <summary>Whether <paramref name="window"/> currently owns keyboard focus.</summary>
    bool IsForeground(IntPtr window);

    /// <summary>The nearest visible window beneath <paramref name="window"/> in z-order, or zero if none.</summary>
    IntPtr WindowBelow(IntPtr window);

    /// <summary>Moves <paramref name="window"/> far off every monitor and to the bottom of the z-order, without activating it.</summary>
    void MoveOffScreenToBottom(IntPtr window);

    /// <summary>Gives keyboard focus to <paramref name="window"/>.</summary>
    void Focus(IntPtr window);
}

/// <summary>
/// Implements <c>--frb-auto-hide</c>: keeps the game window off-screen and at the back of the
/// z-order so an agent-driven run does not cover the user's work, and hands focus back to the
/// window that lost it. Opt-in; a plain <c>--frb-auto</c> run shows the window as always.
/// </summary>
/// <remarks>
/// The window is created (and takes focus) before any game code runs, so this can only react
/// afterwards. The engine also re-centers the window whenever it sizes it, so the move repeats on
/// every <see cref="Tick"/>; focus is handed back once, and only while the game window still holds
/// it, so a user who has already moved on is not interrupted.
/// </remarks>
internal sealed class AutomationWindowParker
{
    internal const string Flag = "--frb-auto-hide";

    private readonly IWindowPlacer _placer;
    private bool _focusHandled;

    internal AutomationWindowParker(IWindowPlacer placer) => _placer = placer;

    /// <summary>Whether the command line asks for the window to be hidden.</summary>
    internal static bool IsHideRequested(IEnumerable<string> args) => args.Contains(Flag);

    /// <summary>The placer for the current OS: real Win32 calls on Windows, otherwise none (the flag is Windows-only).</summary>
    internal static AutomationWindowParker? CreateForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new AutomationWindowParker(new Win32WindowPlacer()) : null;

    /// <summary>
    /// Marks <paramref name="game"/> active even though its window is unfocused. Gum skips all Forms
    /// input while <c>Game.IsActive</c> is false, and giving focus back to the user's window is what
    /// makes it false, so a hidden run would otherwise ignore every injected click. MonoGame's
    /// <c>Game.IsActive</c> is read-only; the flag lives on its internal <c>Platform</c> object, which
    /// window focus events write, hence reflection. Runs only under <c>--frb-auto-hide</c>. Returns
    /// whether a setter was found and called.
    /// </summary>
#pragma warning disable IL2075 // Intentional reflection; automation mode is compiled out of Release consumer builds and never runs under AOT.
    internal static bool ForceActive(object? game)
    {
        if (game == null)
            return false;
        if (TrySetIsActive(game))
            return true;
        var platform = GetNonPublicMember(game, "Platform");
        return platform != null && TrySetIsActive(platform);
    }

    private const System.Reflection.BindingFlags Declared = System.Reflection.BindingFlags.Instance
        | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
        | System.Reflection.BindingFlags.DeclaredOnly;

    // Each level of the hierarchy is searched on its own: reflecting through the most-derived type
    // hides a base class's internal members when the base lives in another assembly (MonoGame's Game).
    private static bool TrySetIsActive(object target)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var setter = type.GetProperty("IsActive", Declared)?.GetSetMethod(nonPublic: true);
            if (setter != null)
            {
                setter.Invoke(target, new object[] { true });
                return true;
            }
        }
        return false;
    }

    private static object? GetNonPublicMember(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Declared);
            if (field != null)
                return field.GetValue(target);
            var property = type.GetProperty(name, Declared);
            if (property != null)
                return property.GetValue(target);
        }
        return null;
    }
#pragma warning restore IL2075

    /// <summary>Call once per frame.</summary>
    internal void Tick()
    {
        var window = _placer.MainWindow;
        if (window == IntPtr.Zero)
            return;

        if (!_focusHandled)
        {
            _focusHandled = true;
            // Read what is beneath BEFORE moving: moving to the bottom changes the answer.
            if (_placer.IsForeground(window))
            {
                var below = _placer.WindowBelow(window);
                _placer.MoveOffScreenToBottom(window);
                if (below != IntPtr.Zero)
                    _placer.Focus(below);
                return;
            }
        }

        _placer.MoveOffScreenToBottom(window);
    }
}

internal sealed class Win32WindowPlacer : IWindowPlacer
{
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint GwHwndNext = 2;
    private const int OffScreen = -32000;
    private static readonly IntPtr HwndBottom = (IntPtr)1;

    public IntPtr MainWindow => System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

    public bool IsForeground(IntPtr window) => GetForegroundWindow() == window;

    public IntPtr WindowBelow(IntPtr window)
    {
        for (var next = GetWindow(window, GwHwndNext); next != IntPtr.Zero; next = GetWindow(next, GwHwndNext))
        {
            // Skip invisible, minimized and untitled windows (shell helpers, tray popups): focusing
            // one of those would not give the user their app back.
            if (IsWindowVisible(next) && !IsIconic(next) && GetWindowTextLength(next) > 0)
                return next;
        }
        return IntPtr.Zero;
    }

    public void MoveOffScreenToBottom(IntPtr window) =>
        SetWindowPos(window, HwndBottom, OffScreen, OffScreen, 0, 0, SwpNoSize | SwpNoActivate);

    public void Focus(IntPtr window) => SetForegroundWindow(window);

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
