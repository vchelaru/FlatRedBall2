using System;
using System.IO;
using AnimationEditor.App.Services;
using AnimationEditor.Core.Logging;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The diagnostics a future <c>log.txt</c> relies on to explain a lingering editor process: every window that opens and
/// closes, and whether the previous primary exited cleanly. Both write through the process-wide <see cref="AppLog"/>, so
/// each test points it at a temp file and puts it back.
/// </summary>
public sealed class AppDiagnosticsLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"frb-applog-{Guid.NewGuid():N}");
    private string LogPath => Path.Combine(_dir, "log.txt");

    public AppDiagnosticsLogTests()
    {
        Directory.CreateDirectory(_dir);
        AppLog.Initialize(new FileLogger(LogPath), LogPath);
    }

    public void Dispose()
    {
        AppLog.Initialize(NullLogger.Instance, null);
        Directory.Delete(_dir, recursive: true);
    }

    [AvaloniaFact]
    public void AWindowThatOpensAndCloses_IsLoggedBothWays()
    {
        WindowLog.Install();
        var window = new Window { Title = "Probe dialog" };

        window.Show();
        window.Close();

        var log = File.ReadAllText(LogPath);
        Assert.Contains("Window opened: Window \"Probe dialog\"", log);
        Assert.Contains("Window closed: Window \"Probe dialog\"", log);
    }

    [Fact]
    public void OwnerStarted_AfterARunThatNeverShutDownCleanly_WarnsAboutIt()
    {
        AppLifecycle.OwnerStarted();
        Assert.Contains("Previous run ended cleanly", File.ReadAllText(LogPath));

        AppLifecycle.OwnerStarted();

        var log = File.ReadAllText(LogPath);
        Assert.Contains("[Warning]", log);
        Assert.Contains("did not shut down cleanly", log);
        Assert.Contains($"pid={Environment.ProcessId}", log);
    }

    [Fact]
    public void AppLog_WithNoLogInstalled_IsANoOp()
    {
        AppLog.Initialize(NullLogger.Instance, null);

        AppLog.Info("nothing listens");
        AppLog.Error("still nothing", new InvalidOperationException("x"));
    }
}
