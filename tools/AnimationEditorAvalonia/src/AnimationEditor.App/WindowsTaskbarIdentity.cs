using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Velopack.Locators;

namespace AnimationEditor.App;

/// <summary>
/// Gives the running process a stable Windows shell identity (AppUserModelID) so the
/// taskbar can associate the button with the executable's icon.
///
/// <para><b>Why</b> — an unpackaged .NET process with no explicit AppUserModelID gets a
/// system-derived identity that carries no icon resource, so an <i>unpinned</i> taskbar
/// button can fall back to the blank default icon. Pinning writes a shortcut that carries
/// its own AppUserModelID + icon, which is why pinning appears to "fix" the icon and
/// unpinning reverts it. Setting an explicit, stable id here gives every launch the same
/// shell identity the taskbar can hang the <c>.exe</c>'s icon on, pinned or not.</para>
///
/// <para>The id is namespaced to match <c>WindowsFileAssociationService.ProgId</c>'s root
/// so the taskbar identity and the <c>.achx</c> file-association identity stay consistent.
/// It must be stable across launches and contain no spaces.</para>
///
/// <para><b>Managed (Velopack) installs are exempt</b> — <c>VelopackApp.Run()</c> (called
/// earlier in <c>Main</c>) already assigns the process an AppUserModelID derived from the
/// pack id, matching what <c>vpk pack</c> baked into the Start Menu/Desktop shortcut it
/// created. Overriding that here with this class's own fixed id would desync the running
/// process from the pinned shortcut's identity and split the taskbar pin into two icons
/// (#1179) instead of one. Only a launch Velopack didn't assign an id for — a local/dev
/// build with no pack id to derive from — needs this fallback.</para>
///
/// <para>Call once at the very start of <c>Main()</c>, before Avalonia creates any window.
/// Safe to call on non-Windows platforms (returns immediately).</para>
/// </summary>
internal static class WindowsTaskbarIdentity
{
    /// <summary>Stable AppUserModelID for the editor. Shares the <c>FlatRedBall.AnimationEditor</c>
    /// root with <see cref="Services.WindowsFileAssociationService.ProgId"/>.</summary>
    private const string AppUserModelId = "FlatRedBall.AnimationEditor";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appId);

    /// <summary>
    /// True when this class's own AppUserModelID should be applied: Velopack has not already
    /// assigned the process one. Pure decision logic, split out so it's testable without a
    /// real Velopack locator or Windows shell interop.
    /// </summary>
    internal static bool ShouldSetExplicitId(string? velopackAssignedAppUserModelId) =>
        velopackAssignedAppUserModelId is null;

    /// <summary>
    /// Assigns the stable AppUserModelID to the current process, unless Velopack already
    /// assigned one for a managed install. No-op off Windows, and any failure is swallowed —
    /// a missing/mismatched shell identity only affects the taskbar icon, never the app's
    /// ability to run.
    /// </summary>
    public static void Set()
    {
        if (!OperatingSystem.IsWindows())
            return;

        SetWindows();
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindows()
    {
        try
        {
            string? velopackAssignedId = VelopackLocator.CreateDefaultForPlatform().AppUserModelId;
            if (!ShouldSetExplicitId(velopackAssignedId))
                return;

            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set AppUserModelID: {e}");
        }
    }
}
