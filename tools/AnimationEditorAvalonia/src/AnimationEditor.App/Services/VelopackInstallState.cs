using System;
using System.Diagnostics;
using Velopack;

namespace AnimationEditor.App.Services;

/// <summary>Whether this process was installed by the Velopack Setup.exe, as opposed to the
/// Velopack portable zip, a legacy archive, or a local dev build.</summary>
internal static class VelopackInstallState
{
    internal static bool IsSetupInstall()
    {
        try
        {
            // Constructing an UpdateManager only reads the local install layout; no network.
            var updateManager = new UpdateManager(ApplicationUpdateSource.ForCurrentBuild());
            return updateManager.IsInstalled && !updateManager.IsPortable;
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to detect Velopack install state: {e}");
            return false;
        }
    }
}
