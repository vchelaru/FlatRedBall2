using AnimationEditor.App.Controls;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows.Input;

namespace AnimationEditor.App;

public partial class App : Application
{
    /// <summary>
    /// Set by <see cref="Program.Main"/> before Avalonia starts. Used to wire the
    /// single-instance file-open pipe to the running <see cref="MainWindow"/>.
    /// </summary>
    internal static SingleInstanceServer? SingleInstance { get; set; }

    /// <summary>
    /// Set by <see cref="Program.Main"/> when the pipe hand-off reached a primary that is frozen or has
    /// no window; null when the hand-off failed and the action must be worked out here.
    /// </summary>
    internal static SingleInstanceRecoveryAction? RecoveryActionForReachedPrimary { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Name = TitleBarHelper.AppName;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Catch UI-thread crashes (e.g. the SKImage.FromBitmap crash in #479, which came
        // through the dispatcher). The background-thread handlers are installed in Program.Main.
        Services.CrashLogging.InstallDispatcherHandler();
        Services.WindowLog.Install();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += (_, _) => Services.AppLifecycle.ShutdownSignal("Lifetime shutdown requested");
            desktop.Exit += (_, e) => Services.AppLifecycle.ShutdownSignal($"Lifetime exit, code {e.ApplicationExitCode}");

            // Program.Main only reaches here as a non-owner when its pipe hand-off to the
            // primary instance failed (#1049) — show a recovery dialog instead of the normal
            // MainWindow/DI graph, which would be overkill for a two-button prompt.
            if (SingleInstance is { IsOwner: false })
                HandleUnreachablePrimary(desktop);
            else
                StartAsOwner(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Shows the recovery dialog matching why the primary could not be used. Called only when
    /// <see cref="SingleInstance"/> is not the mutex owner: its pipe hand-off failed, or it
    /// reached a primary that is frozen or window-less, in <see cref="Program.Main"/>.
    /// </summary>
    private void HandleUnreachablePrimary(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var action = RecoveryActionForReachedPrimary
            ?? SingleInstanceRecoveryDecision.Decide(reachedPrimary: false, PrimaryInstanceHangChecker.Check());
        AppLog.Info($"Recovery dialog for a secondary launch: action {action}.");

        switch (action)
        {
            case SingleInstanceRecoveryAction.ShowBusyMessage:
                ShowRecoveryDialog(desktop, SingleInstanceRecoveryWindow.CreateBusyDialog(), onRestartConfirmed: null);
                break;

            case SingleInstanceRecoveryAction.OfferRestart:
                ShowRecoveryDialog(desktop, SingleInstanceRecoveryWindow.CreateHungDialog(),
                    onRestartConfirmed: () => RestartAndTakeOver(desktop));
                break;

            case SingleInstanceRecoveryAction.OfferRestartNoWindow:
                ShowRecoveryDialog(desktop, SingleInstanceRecoveryWindow.CreateNoWindowDialog(),
                    onRestartConfirmed: () => RestartAndTakeOver(desktop));
                break;

            default:
                // SilentExit only happens for a healthy reachable primary, and Program.Main
                // already returned in that branch without booting Avalonia.
                desktop.Shutdown(0);
                break;
        }
    }

    private static void ShowRecoveryDialog(
        IClassicDesktopStyleApplicationLifetime desktop,
        SingleInstanceRecoveryWindow window,
        Action? onRestartConfirmed)
    {
        desktop.MainWindow = window;
        window.Closed += (_, _) =>
        {
            AppLog.Info($"Recovery dialog closed (restart requested: {window.RestartRequested}).");
            if (window.RestartRequested && onRestartConfirmed != null)
                onRestartConfirmed();
            else
                desktop.Shutdown(0);
        };
        window.Show();
    }

    /// <summary>
    /// Kills the frozen primary, reacquires the single-instance mutex, and falls through into
    /// the normal owner startup — all within this process, without a second Avalonia boot.
    /// </summary>
    private void RestartAndTakeOver(IClassicDesktopStyleApplicationLifetime desktop)
    {
        AppLog.Info("Restarting: killing the other instance and taking over.");
        PrimaryInstanceHangChecker.KillOtherInstance();

        SingleInstance?.Dispose();
        SingleInstance = new SingleInstanceServer();

        if (!SingleInstance.IsOwner)
        {
            // Race: something else still holds the mutex — give up cleanly rather than looping.
            AppLog.Warn("Restart gave up: the single-instance mutex is still held after killing the other instance.");
            desktop.Shutdown(0);
            return;
        }

        AppLog.Info("Restart took over: single-instance mutex acquired.");
        AppLifecycle.OwnerStarted();
        SingleInstance.StartListening();
        StartAsOwner(desktop);
    }

    private void StartAsOwner(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var services = BuildServices();
        var window = services.GetRequiredService<MainWindow>();
        window.Icon = LoadAppIcon();
        desktop.MainWindow = window;
        RegisterNativeMenu(window);
        window.Closing += (_, e) => AppLog.Info($"MainWindow closing (reason {e.CloseReason}, programmatic {e.IsProgrammatic}).");
        window.Closed += (_, _) => AppLifecycle.ShutdownSignal(
            $"MainWindow closed ({desktop.Windows.Count} other window(s) still open)");

        // Wire single-instance IPC: file paths received from a second process open as tabs
        // and bring the window forward (#1009) -- otherwise it opens behind whatever else
        // currently has focus.
        if (SingleInstance != null)
        {
            SingleInstance.FileOpenRequested += path =>
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await window.OpenFileAsTab(path);
                    window.BringToForeground();
                });
            SingleInstance.ActivationRequested += () =>
                Dispatcher.UIThread.Post(() => { if (window.IsVisible) window.BringToForeground(); });
        }

        // Post the Dock icon update to the next UI tick. Avalonia's macOS backend
        // may clear NSApplication.applicationIconImage during window assignment
        // (when WindowDecorations="None" bypasses the native title-bar path), so
        // we set it AFTER Avalonia's own initialisation has finished.
        Dispatcher.UIThread.Post(SetMacOSDockIcon);
        window.Show();
    }

    private static void SetMacOSDockIcon()
    {
        using var stream = AssetLoader.Open(
            new Uri("avares://AnimationEditor/Assets/icons/achx-icon-256.png"));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        MacOSDockIcon.Set(ms.ToArray());
    }

    private static WindowIcon LoadAppIcon()
    {
        using var stream = AssetLoader.Open(
            new Uri("avares://AnimationEditor/Assets/icons/achx-icon-256.png"));
        return new WindowIcon(new Bitmap(stream));
    }

    /// <summary>Gives <paramref name="window"/> its own macOS menu bar mirror; every MainWindow needs one, since the in-window menu is hidden on a Mac.</summary>
    private static void RegisterNativeMenu(MainWindow window) =>
        NativeMenu.SetMenu(window, window.CreateNativeMenu());

    /// <param name="settingsRoot">
    /// Application-data root for the window's settings and thumbnail cache; null is the user's real
    /// folder. Tests pass a temp folder.
    /// </param>
    /// <param name="recoveryFilePath">
    /// Crash-recovery file the window clears on close; null is the shared one in the temp folder. Tests pass their own,
    /// since closing a window deletes it.
    /// </param>
    internal static MainWindow CreateDetachedWindow(string? settingsRoot = null, string? recoveryFilePath = null)
    {
        var services = BuildServices(settingsRoot, recoveryFilePath);
        var window = services.GetRequiredService<MainWindow>();
        window.Icon = LoadAppIcon();
        RegisterNativeMenu(window);
        return window;
    }

    private static ServiceProvider BuildServices(string? settingsRoot = null, string? recoveryFilePath = null)
    {
        settingsRoot ??= Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        recoveryFilePath ??= System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnimationEditor_Recovery.achx");
        var sc = new ServiceCollection();

        sc.AddSingleton<ProjectManager>();
        sc.AddSingleton<IProjectManager>(sp => sp.GetRequiredService<ProjectManager>());

        sc.AddSingleton<ApplicationEvents>();
        sc.AddSingleton<IApplicationEvents>(sp => sp.GetRequiredService<ApplicationEvents>());

        sc.AddSingleton<SelectedState>(sp =>
            new SelectedState(sp.GetRequiredService<IProjectManager>()));
        sc.AddSingleton<ISelectedState>(sp => sp.GetRequiredService<SelectedState>());

        sc.AddSingleton<AppState>(sp =>
            new AppState(sp.GetRequiredService<IApplicationEvents>(),
                         sp.GetRequiredService<ISelectedState>()));
        sc.AddSingleton<IAppState>(sp => sp.GetRequiredService<AppState>());

        sc.AddSingleton<IoManager>(sp =>
            new IoManager(sp.GetRequiredService<IAppState>(),
                recoveryFilePath));
        sc.AddSingleton<IIoManager>(sp => sp.GetRequiredService<IoManager>());

        sc.AddSingleton<ObjectFinder>(sp =>
            new ObjectFinder(sp.GetRequiredService<IProjectManager>()));
        sc.AddSingleton<IObjectFinder>(sp => sp.GetRequiredService<ObjectFinder>());

        sc.AddSingleton<UndoManager>();
        sc.AddSingleton<IUndoManager>(sp => sp.GetRequiredService<UndoManager>());

        sc.AddSingleton<PendingCutState>();
        sc.AddSingleton<IPendingCutState>(sp => sp.GetRequiredService<PendingCutState>());

        sc.AddSingleton<AppCommands>(sp =>
            new AppCommands(
                sp.GetRequiredService<IProjectManager>(),
                sp.GetRequiredService<ISelectedState>(),
                sp.GetRequiredService<IApplicationEvents>(),
                sp.GetRequiredService<IIoManager>(),
                sp.GetRequiredService<IObjectFinder>(),
                sp.GetRequiredService<IUndoManager>()));
        sc.AddSingleton<IAppCommands>(sp => sp.GetRequiredService<AppCommands>());

        sc.AddSingleton<ThumbnailService>(sp =>
            new ThumbnailService(sp.GetRequiredService<IProjectManager>()));

        sc.AddSingleton<ProjectTreeThumbnailService>(sp =>
            new ProjectTreeThumbnailService(ProjectThumbnailCacheLocation.ForApplicationDataRoot(settingsRoot)));

        sc.AddSingleton<IApplicationUpdater, VelopackApplicationUpdater>();
        sc.AddSingleton(_ => PlatformWheelInput.CreateForHost());

        sc.AddTransient<MainWindow>(sp => new MainWindow(
            sp.GetRequiredService<IProjectManager>(),
            sp.GetRequiredService<ISelectedState>(),
            sp.GetRequiredService<IAppCommands>(),
            sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<IApplicationEvents>(),
            sp.GetRequiredService<IIoManager>(),
            sp.GetRequiredService<IObjectFinder>(),
            sp.GetRequiredService<IUndoManager>(),
            sp.GetRequiredService<IPendingCutState>(),
            sp.GetRequiredService<ThumbnailService>(),
            sp.GetRequiredService<ProjectTreeThumbnailService>(),
            settingsRoot,
            sp.GetRequiredService<IApplicationUpdater>(),
            useMacOSChrome: OperatingSystem.IsMacOS(),
            commandModifier: CommandModifier.ForHost(OperatingSystem.IsMacOS()),
            wheelInput: sp.GetRequiredService<PlatformWheelInput>()));

        return sc.BuildServiceProvider();
    }
}
