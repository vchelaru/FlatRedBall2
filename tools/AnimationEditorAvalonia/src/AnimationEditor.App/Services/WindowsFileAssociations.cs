using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AnimationEditor.App.Services;

/// <summary>One <c>HKEY_CURRENT_USER</c> value to write. A null <paramref name="Name"/> is the key's default value.</summary>
internal readonly record struct RegistryValueWrite(string SubKey, string? Name, string Value);

/// <summary>A file type the editor registers: one ProgId that the listed extensions map to.</summary>
internal sealed record FileTypeRegistration(string ProgId, string Description, IReadOnlyList<string> Extensions);

/// <summary>
/// The per-user registry entries that make Windows treat the editor as a handler for
/// <c>.achx</c>, <c>.achj</c> and <c>.seqj</c>: one ProgId per <see cref="FileTypes"/> entry, the
/// extension mappings, and the <c>Capabilities</c> + <c>RegisteredApplications</c> entries that
/// list the editor in Default apps and Open with. Written by the Velopack install/update hooks and
/// removed by the uninstall hook. Portable and dev builds never register, because their exe path
/// isn't stable. Every type opens with the exe's own icon and passes the file path as the argument.
///
/// <para>Windows hash-protects the user's own choice of default app, so this never overrides an
/// app the user picked; they switch with Open with &gt; Always or Settings &gt; Default apps.</para>
/// </summary>
internal static class WindowsFileAssociations
{
    /// <summary>The ProgId <c>.achx</c> and <c>.achj</c> map to. Namespaced to avoid colliding with
    /// any other handler for the same extensions.</summary>
    internal const string ProgId = "FlatRedBall.AnimationEditor.achx";

    /// <summary>The ProgId <c>.seqj</c> maps to.</summary>
    internal const string SequenceProgId = "FlatRedBall.AnimationEditor.seqj";

    /// <summary>The value name under <c>HKCU\Software\RegisteredApplications</c>.</summary>
    internal const string RegisteredAppName = "FlatRedBall AnimationEditor";

    private const string AppKey = @"Software\FlatRedBall\AnimationEditor";
    private const string CapabilitiesKey = AppKey + @"\Capabilities";
    private const string RegisteredApplicationsKey = @"Software\RegisteredApplications";
    private const string ClassesKey = @"Software\Classes";

    /// <summary>Every file type registered, in order.</summary>
    internal static readonly IReadOnlyList<FileTypeRegistration> FileTypes = new[]
    {
        new FileTypeRegistration(ProgId, "FlatRedBall Animation Chain", new[] { ".achx", ".achj" }),
        new FileTypeRegistration(SequenceProgId, "FlatRedBall Animation Sequence", new[] { ".seqj" }),
    };

    /// <summary>The command a double-click runs: Windows substitutes the file's path for <c>%1</c>.</summary>
    internal static string OpenCommand(string exePath) => $"\"{exePath}\" \"%1\"";

    /// <summary>Every value <see cref="Register"/> writes for <paramref name="exePath"/>.</summary>
    internal static IReadOnlyList<RegistryValueWrite> BuildWrites(string exePath)
    {
        var writes = new List<RegistryValueWrite>
        {
            new(CapabilitiesKey, "ApplicationName", "AnimationEditor"),
            new(CapabilitiesKey, "ApplicationDescription", "Edits FlatRedBall animation chain and sequence files."),
            new(RegisteredApplicationsKey, RegisteredAppName, CapabilitiesKey),
        };

        foreach (var type in FileTypes)
        {
            string progIdKey = $@"{ClassesKey}\{type.ProgId}";
            writes.Add(new(progIdKey, null, type.Description));
            writes.Add(new($@"{progIdKey}\DefaultIcon", null, $"\"{exePath}\",0"));
            writes.Add(new($@"{progIdKey}\shell\open\command", null, OpenCommand(exePath)));

            foreach (string extension in type.Extensions)
            {
                // The extension's default value is only a fallback: a hash-protected UserChoice
                // the user picked still wins, so this never overrides their choice.
                writes.Add(new($@"{ClassesKey}\{extension}", null, type.ProgId));
                writes.Add(new($@"{ClassesKey}\{extension}\OpenWithProgids", type.ProgId, string.Empty));
                writes.Add(new($@"{CapabilitiesKey}\FileAssociations", extension, type.ProgId));
            }
        }

        return writes;
    }

    /// <param name="keyPrefix">Prepended to every key path. Production passes none; tests pass a
    /// throwaway key under HKCU so they never touch the real associations (and skip the Explorer
    /// notification).</param>
    [SupportedOSPlatform("windows")]
    internal static void Register(string exePath, string keyPrefix = "")
    {
        try
        {
            foreach (var write in BuildWrites(exePath))
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPrefix + write.SubKey);
                key.SetValue(write.Name ?? string.Empty, write.Value);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to register file associations: {e}");
        }

        if (keyPrefix.Length == 0)
            NotifyAssociationsChanged();
    }

    /// <summary>Removes only our entries. An extension's default value is cleared only when it
    /// still points at our ProgId, so another app the user chose keeps working.</summary>
    [SupportedOSPlatform("windows")]
    internal static void Unregister(string keyPrefix = "")
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPrefix + AppKey, throwOnMissingSubKey: false);

            using (var registered = Registry.CurrentUser.OpenSubKey(keyPrefix + RegisteredApplicationsKey, writable: true))
                registered?.DeleteValue(RegisteredAppName, throwOnMissingValue: false);

            foreach (var type in FileTypes)
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"{keyPrefix}{ClassesKey}\{type.ProgId}", throwOnMissingSubKey: false);

                foreach (string extension in type.Extensions)
                {
                    using var extensionKey = Registry.CurrentUser.OpenSubKey($@"{keyPrefix}{ClassesKey}\{extension}", writable: true);
                    if (extensionKey is null)
                        continue;

                    if (string.Equals(extensionKey.GetValue(null) as string, type.ProgId, StringComparison.OrdinalIgnoreCase))
                        extensionKey.DeleteValue(string.Empty, throwOnMissingValue: false);

                    using var openWith = extensionKey.OpenSubKey("OpenWithProgids", writable: true);
                    openWith?.DeleteValue(type.ProgId, throwOnMissingValue: false);
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to unregister file associations: {e}");
        }

        if (keyPrefix.Length == 0)
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
