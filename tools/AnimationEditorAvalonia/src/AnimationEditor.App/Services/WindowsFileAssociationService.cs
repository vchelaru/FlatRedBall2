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
/// (<see cref="WindowsAchxRegistration"/>) and then opens the system default-apps settings for the user to confirm.</para>
///
/// <para>Pure helpers (<see cref="BuildOpenCommand"/>, <see cref="IsOurProgId"/>) are unit-tested;
/// the registry reads/writes and the settings deep-link are the thin untested wiring.</para>
/// </summary>
internal sealed class WindowsFileAssociationService : IFileAssociationService
{
    /// <summary>The primary file extension this editor handles, including the leading dot.
    /// Default-handler status (<see cref="GetStatus"/>/<see cref="IsDefault"/>) is tracked for
    /// this extension only; <see cref="SecondaryExtension"/> is registered to the same ProgId
    /// alongside it but doesn't affect that status.</summary>
    internal const string Extension = ".achx";

    /// <summary>The .achj (JSON) extension, registered to the same ProgId as <see cref="Extension"/>
    /// so double-clicking either format opens this editor.</summary>
    internal const string SecondaryExtension = ".achj";

    /// <summary>
    /// The per-user ProgId the editor registers under <c>HKCU\Software\Classes</c>. Namespaced
    /// to avoid colliding with any system or third-party handler for the same extension.
    /// </summary>
    internal const string ProgId = "FlatRedBall.AnimationEditor.achx";

    private const string ClassesRoot = @"HKEY_CURRENT_USER\Software\Classes";

    /// <param name="isInstalled">Whether this process is a Velopack Setup install. Portable and
    /// dev builds pass false so <see cref="RegisterAsDefault"/> never registers their path.</param>
    public WindowsFileAssociationService(bool isInstalled)
    {
        CanRegisterAsDefault = isInstalled;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool CanRegisterAsDefault { get; }

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
    private void RegisterAsDefaultWindows()
    {
        string? exe = Environment.ProcessPath;
        if (!CanRegisterAsDefault || string.IsNullOrEmpty(exe))
            return;

        // The install hook already registered; rewriting repairs entries removed since.
        WindowsAchxRegistration.Register(exe);
        OpenDefaultAppsSettings();
    }

    private static void OpenDefaultAppsSettings()
    {
        try
        {
            // registeredAppUser opens the editor's own Default apps page on Windows 11 (2023-04
            // CU and later); older builds ignore it and show the Default apps list.
            string uri = "ms-settings:defaultapps?registeredAppUser="
                + Uri.EscapeDataString(WindowsAchxRegistration.RegisteredAppName);
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to open default-apps settings: {e}");
        }
    }
}
