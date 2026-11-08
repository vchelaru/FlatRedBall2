using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using Avalonia;
using System;
using System.IO;
using System.Runtime.Versioning;
using Velopack;

namespace AnimationEditor.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Install crash logging first so even a failure during startup, or a Velopack update hook, gets recorded.
        CrashLogging.Install(AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), args);

        // Managed installs must initialize Velopack before any other app code so an already-applied
        // update can complete its bootstrap work. Local builds and legacy archive downloads
        // pass through without changing their launch behavior.
        var velopack = VelopackApp.Build().SetAutoApplyOnStartup(false);
        if (OperatingSystem.IsWindows())
            AddAchxAssociationHooks(velopack);
        velopack.Run();

        string? fileArg = args.Length >= 1 && File.Exists(args[0]) ? args[0] : null;

        var singleInstance = new SingleInstanceServer();
        AppLog.Info(singleInstance.IsOwner
            ? "Single-instance mutex acquired: this process is the primary."
            : "Single-instance mutex already held: this is a secondary launch.");
        if (!singleInstance.IsOwner)
        {
            // Another instance is running. A successful hand-off to a healthy primary means exit
            // immediately without paying Avalonia's startup cost (#1049), after raising its window
            // at the OS level: an older primary ignores a plain re-launch, and silence looks like
            // nothing happened. A reachable primary that is frozen or has no window, and a failed
            // hand-off (slow-but-fine or frozen, ambiguous), fall through into the Avalonia boot
            // below, which shows a recovery dialog (see App.OnFrameworkInitializationCompleted).
            bool reachedPrimary = SingleInstanceServer.SendToRunningInstanceAsync(fileArg).GetAwaiter().GetResult();
            AppLog.Info($"Hand-off to the primary over the pipe: {(reachedPrimary ? "reached" : "FAILED")} (file arg: {fileArg ?? "none"}).");
            if (reachedPrimary)
            {
                var hang = PrimaryInstanceHangChecker.Check(reachedPrimary: true);
                var action = SingleInstanceRecoveryDecision.Decide(reachedPrimary: true, hang);
                AppLog.Info($"Primary classified {hang}; second-launch action {action}.");
                if (action == SingleInstanceRecoveryAction.SilentExit)
                {
                    PrimaryInstanceHangChecker.ActivatePrimaryWindow();
                    singleInstance.Dispose();
                    AppLog.Info("Main returning: secondary launch handed off to a healthy primary.");
                    return;
                }
                App.RecoveryActionForReachedPrimary = action;
            }
            else
            {
                AppLog.Info("Booting Avalonia to show a recovery dialog (hand-off failed, so the primary is slow or frozen).");
            }
        }
        else
        {
            AppLifecycle.OwnerStarted();

            // We are (immediately) the primary instance. Start the pipe listener before Avalonia
            // so requests that arrive during startup are queued in the server.
            singleInstance.StartListening();
        }

        // We are booting Avalonia: start the work it doesn't need to wait for, so it runs on the
        // thread pool while the main thread initializes the platform (see StartupWarmup).
        StartupWarmup.Begin(AppSettingsLocation.ForApplicationDataRoot(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));

        // Set the Dock label BEFORE Avalonia calls [NSApplication sharedApplication]
        // (inside UsePlatformDetect). The Dock caches the process name at that point;
        // calling setProcessName: afterwards has no visible effect on the Dock label.
        MacOSDockIcon.SetProcessName("AnimationEditor");

        // Give the process a stable Windows shell identity before any window is created, so
        // the (unpinned) taskbar button can resolve the exe's icon instead of a blank default.
        WindowsTaskbarIdentity.Set();
        App.SingleInstance = singleInstance;
        try
        {
            AppLog.Info("Starting the Avalonia desktop lifetime.");
            int exitCode = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            AppLifecycle.MainReturning(exitCode);
        }
        catch (Exception ex)
        {
            // Record main-thread startup/run crashes, then let the process die as before.
            CrashLogging.LogCrash("Main", ex);
            throw;
        }
        finally
        {
            // App.SingleInstance may have been replaced with a freshly-acquired instance if the
            // user restarted a frozen primary (App.RestartAndTakeOver) — dispose whichever one
            // is current, not the local var captured before that could have happened.
            App.SingleInstance?.Dispose();
        }
    }

    // Registers .achx/.achj against the installed exe so the editor shows up in Default apps
    // and Open with; uninstall removes only our entries.
    [SupportedOSPlatform("windows")]
    private static void AddAchxAssociationHooks(VelopackApp velopack) =>
        velopack
            .OnAfterInstallFastCallback(_ => { AppLog.Info("Velopack hook: after install."); RegisterAchxAssociation(); })
            .OnAfterUpdateFastCallback(_ => { AppLog.Info("Velopack hook: after update."); RegisterAchxAssociation(); })
            .OnBeforeUninstallFastCallback(_ => { AppLog.Info("Velopack hook: before uninstall."); WindowsAchxRegistration.Unregister(); });

    [SupportedOSPlatform("windows")]
    private static void RegisterAchxAssociation()
    {
        // The update hook also runs for a self-updating portable build; skip that one, same
        // gate as Settings' "Set as default".
        if (Environment.ProcessPath is { } exe && VelopackInstallState.IsSetupInstall())
            WindowsAchxRegistration.Register(exe);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
