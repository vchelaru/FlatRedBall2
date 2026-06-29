using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using AnimationEditor.Core.IO;
using Microsoft.Win32;

namespace AnimationEditor.App.Services;

/// <summary>
/// Windows implementation of <see cref="IFileAssociationService"/>. Registers a per-user
/// ProgId under <c>HKCU\Software\Classes</c> and detects the current default by reading the
/// extension's <c>UserChoice</c>.
///
/// <para>Modern Windows (8+) hash-protects <c>HKCU\…\.achx\UserChoice</c>, so an app cannot
/// silently force itself as the default. <see cref="RegisterAsDefault"/> therefore registers
/// the ProgId and then opens the system default-apps settings for the user to confirm.</para>
///
/// <para>Pure helpers (<see cref="BuildOpenCommand"/>, <see cref="IsOurProgId"/>) are unit-tested;
/// the registry reads/writes and the settings deep-link are the thin untested wiring.</para>
/// </summary>
internal sealed class WindowsFileAssociationService : IFileAssociationService
{
    /// <summary>The file extension this editor handles, including the leading dot.</summary>
    internal const string Extension = ".achx";

    /// <summary>
    /// The per-user ProgId the editor registers under <c>HKCU\Software\Classes</c>. Namespaced
    /// to avoid colliding with any system or third-party handler for the same extension.
    /// </summary>
    internal const string ProgId = "FlatRedBall.AnimationEditor.achx";

    private const string ClassesRoot = @"HKEY_CURRENT_USER\Software\Classes";

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsDefault() => GetStatus() == AchxFileAssociationStatus.AssociatedWithThisBuild;

    public AchxFileAssociationStatus GetStatus()
    {
        if (!OperatingSystem.IsWindows())
            return AchxFileAssociationStatus.NotSupported;

        return GetStatusWindows();
    }

    public void RegisterAsDefault()
    {
        if (!OperatingSystem.IsWindows())
            return;

        RegisterAsDefaultWindows();
    }

    /// <summary>Builds the <c>shell\open\command</c> value: the quoted exe followed by the quoted
    /// <c>%1</c> argument placeholder Windows substitutes with the launched file path.</summary>
    internal static string BuildOpenCommand(string exePath) => $"\"{exePath}\" \"%1\"";

    /// <summary>Whether a ProgId read from the registry is the one this editor registers
    /// (case-insensitive; <c>null</c>/empty means no association and returns false).</summary>
    internal static bool IsOurProgId(string? registeredProgId) =>
        string.Equals(registeredProgId, ProgId, StringComparison.OrdinalIgnoreCase);

    [SupportedOSPlatform("windows")]
    private static AchxFileAssociationStatus GetStatusWindows()
    {
        string? activeProgId = ReadActiveProgId();
        bool isOurProgId = IsOurProgId(activeProgId);
        string? registeredExe = isOurProgId ? ReadOpenCommandExe(activeProgId!) : null;
        string? currentExe = Environment.ProcessPath;
        bool registeredExeExists = !string.IsNullOrEmpty(registeredExe) && File.Exists(registeredExe);

        return AchxFileAssociationEvaluator.Classify(
            isOurProgId, registeredExe, currentExe, registeredExeExists);
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadActiveProgId()
    {
        var userChoice = Registry.GetValue(
            $@"{ClassesRoot}\{Extension}\UserChoice", "ProgId", null) as string;
        if (!string.IsNullOrEmpty(userChoice))
            return userChoice;

        return Registry.GetValue($@"{ClassesRoot}\{Extension}", null, null) as string;
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadOpenCommandExe(string progId)
    {
        var openCommand = Registry.GetValue(
            $@"{ClassesRoot}\{progId}\shell\open\command", null, null) as string;
        return FileAssociationCommandLine.TryParseExePath(openCommand);
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterAsDefaultWindows()
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return;

        try
        {
            string progIdKey = $@"{ClassesRoot}\{ProgId}";
            Registry.SetValue(progIdKey, null, "FlatRedBall Animation Chain");
            Registry.SetValue($@"{progIdKey}\DefaultIcon", null, $"\"{exe}\",0");
            Registry.SetValue($@"{progIdKey}\shell\open\command", null, BuildOpenCommand(exe));

            // Point the extension at our ProgId. On modern Windows this is only a fallback —
            // an existing hash-protected UserChoice still wins, which is why we open the
            // default-apps settings below for the user to confirm.
            Registry.SetValue($@"{ClassesRoot}\{Extension}", null, ProgId);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to register .achx ProgId: {e}");
        }

        OpenDefaultAppsSettings();
    }

    private static void OpenDefaultAppsSettings()
    {
        try
        {
            // ms-settings: is the only reliable, version-stable deep-link; there is no
            // per-extension page across Windows versions, so we land on Default apps.
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to open default-apps settings: {e}");
        }
    }
}
