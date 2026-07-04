using AnimationEditor.App.Services;
using Avalonia;
using System;
using System.IO;

namespace AnimationEditor.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Install crash logging first so even a failure during startup gets recorded.
        CrashLogging.Install(AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

        string? fileArg = args.Length >= 1 && File.Exists(args[0]) ? args[0] : null;

        var singleInstance = new SingleInstanceServer();
        if (!singleInstance.IsOwner)
        {
            // Another instance is running — forward the file path and exit.
            if (fileArg != null)
                SingleInstanceServer.SendToRunningInstanceAsync(fileArg).GetAwaiter().GetResult();
            singleInstance.Dispose();
            return;
        }

        // We are the primary instance. Start the pipe listener before Avalonia so requests
        // that arrive during startup are queued in the server.
        singleInstance.StartListening();

        // Set the Dock label BEFORE Avalonia calls [NSApplication sharedApplication]
        // (inside UsePlatformDetect). The Dock caches the process name at that point;
        // calling setProcessName: afterwards has no visible effect on the Dock label.
        MacOSDockIcon.SetProcessName("Animation Editor");

        // Give the process a stable Windows shell identity before any window is created, so
        // the (unpinned) taskbar button can resolve the exe's icon instead of a blank default.
        WindowsTaskbarIdentity.Set();
        App.SingleInstance = singleInstance;
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Record main-thread startup/run crashes, then let the process die as before.
            CrashLogging.LogCrash("Main", ex);
            throw;
        }
        finally
        {
            singleInstance.Dispose();
        }
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
