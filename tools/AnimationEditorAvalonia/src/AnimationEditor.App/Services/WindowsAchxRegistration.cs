using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AnimationEditor.App.Services;

/// <summary>One <c>HKEY_CURRENT_USER</c> value to write. A null <paramref name="Name"/> is the key's default value.</summary>
internal readonly record struct RegistryValueWrite(string SubKey, string? Name, string Value);

/// <summary>
/// The per-user registry entries that make Windows treat the editor as a handler for
/// <c>.achx</c>/<c>.achj</c>: the ProgId, the extension mappings, and the
/// <c>Capabilities</c> + <c>RegisteredApplications</c> entries that list the editor on the
/// Default apps page. Written by the Velopack install/update hooks and by "Set as default";
/// removed by the uninstall hook. Only installed builds call this, since a portable exe's
/// path isn't stable enough to register.
/// </summary>
internal static class WindowsAchxRegistration
{
    /// <summary>The value name under <c>HKCU\Software\RegisteredApplications</c>, and the
    /// <c>registeredAppUser</c> argument for the Default apps deep-link.</summary>
    internal const string RegisteredAppName = "FlatRedBall AnimationEditor";

    private const string AppKey = @"Software\FlatRedBall\AnimationEditor";
    private const string CapabilitiesKey = AppKey + @"\Capabilities";
    private const string RegisteredApplicationsKey = @"Software\RegisteredApplications";
    private const string ClassesKey = @"Software\Classes";

    private static readonly string[] Extensions =
        { WindowsFileAssociationService.Extension, WindowsFileAssociationService.SecondaryExtension };

    /// <summary>Every value <see cref="Register"/> writes for <paramref name="exePath"/>.</summary>
    internal static IReadOnlyList<RegistryValueWrite> BuildWrites(string exePath)
    {
        const string progId = WindowsFileAssociationService.ProgId;
        string progIdKey = $@"{ClassesKey}\{progId}";

        var writes = new List<RegistryValueWrite>
        {
            new(progIdKey, null, "FlatRedBall Animation Chain"),
            new($@"{progIdKey}\DefaultIcon", null, $"\"{exePath}\",0"),
            new($@"{progIdKey}\shell\open\command", null, WindowsFileAssociationService.BuildOpenCommand(exePath)),
            new(CapabilitiesKey, "ApplicationName", "AnimationEditor"),
            new(CapabilitiesKey, "ApplicationDescription", "Edits FlatRedBall animation chain files."),
            new(RegisteredApplicationsKey, RegisteredAppName, CapabilitiesKey),
        };

        foreach (string extension in Extensions)
        {
            // The extension's default value is only a fallback: a hash-protected UserChoice
            // the user picked still wins, so this never overrides their choice.
            writes.Add(new($@"{ClassesKey}\{extension}", null, progId));
            writes.Add(new($@"{ClassesKey}\{extension}\OpenWithProgids", progId, string.Empty));
            writes.Add(new($@"{CapabilitiesKey}\FileAssociations", extension, progId));
        }

        return writes;
    }

    [SupportedOSPlatform("windows")]
    internal static void Register(string exePath)
    {
        try
        {
            foreach (var write in BuildWrites(exePath))
            {
                using var key = Registry.CurrentUser.CreateSubKey(write.SubKey);
                key.SetValue(write.Name ?? string.Empty, write.Value);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to register .achx association: {e}");
        }

        NotifyAssociationsChanged();
    }

    /// <summary>Removes only our entries. An extension's default value is cleared only when it
    /// still points at our ProgId, so another app the user chose keeps working.</summary>
    [SupportedOSPlatform("windows")]
    internal static void Unregister()
    {
        const string progId = WindowsFileAssociationService.ProgId;
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesKey}\{progId}", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(AppKey, throwOnMissingSubKey: false);

            using (var registered = Registry.CurrentUser.OpenSubKey(RegisteredApplicationsKey, writable: true))
                registered?.DeleteValue(RegisteredAppName, throwOnMissingValue: false);

            foreach (string extension in Extensions)
            {
                using var extensionKey = Registry.CurrentUser.OpenSubKey($@"{ClassesKey}\{extension}", writable: true);
                if (extensionKey is null)
                    continue;

                if (WindowsFileAssociationService.IsOurProgId(extensionKey.GetValue(null) as string))
                    extensionKey.DeleteValue(string.Empty, throwOnMissingValue: false);

                using var openWith = extensionKey.OpenSubKey("OpenWithProgids", writable: true);
                openWith?.DeleteValue(progId, throwOnMissingValue: false);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to unregister .achx association: {e}");
        }

        NotifyAssociationsChanged();
    }

    // Tells Explorer to drop its cached associations so icons and Open with update without a
    // sign-out.
    private static void NotifyAssociationsChanged() =>
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
