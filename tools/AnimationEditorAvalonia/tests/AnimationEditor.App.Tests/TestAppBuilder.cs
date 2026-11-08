// Assembly-level attribute wires Avalonia.Headless to the TestAppBuilder.
// AvaloniaTestApplicationAttribute lives in Avalonia.Headless (not Avalonia.Headless.XUnit).
using Avalonia;
using Avalonia.Headless;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(AnimationEditor.App.Tests.TestAppBuilder))]
// Avalonia.Headless.XUnit's HeadlessUnitTestSession lazily creates a single shared
// headless Compositor/render loop on first [AvaloniaFact] use, and that object enforces
// single-thread affinity via Dispatcher.VerifyAccess. xUnit's default parallelization runs
// different test collections on separate worker threads, so two [AvaloniaFact] tests racing
// to initialize it concurrently throw "a different thread owns it" - flaky on CI runners with
// more parallel workers than a dev machine. Force serial execution to remove the race.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AnimationEditor.App.Tests;

/// <summary>
/// Factory consumed by [AvaloniaTestApplication] to build the headless app.
/// Uses the real <see cref="AnimationEditor.App.App"/> so that
/// <c>AvaloniaXamlLoader.Load(this)</c> runs in App.Initialize() and registers
/// the compiled XAML resources needed by <c>new MainWindow()</c>.
/// </summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<global::AnimationEditor.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .AfterSetup(_ => UnhandledExceptionLog.Install());
}

/// <summary>
/// Names the cause when the shared headless session dies mid-run: every later test then fails with only
/// "Cannot get KeyValueStorage on the idle test context", which hides the exception that ended it. Each unhandled
/// UI-thread, background-thread or unobserved-task exception goes to stderr and to <see cref="FilePath"/>, which CI
/// prints when the test step fails.
/// </summary>
internal static class UnhandledExceptionLog
{
    internal static string FilePath { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnimationEditorAppTests", "unhandled-exceptions.log");

    private static int _installed;

    internal static void Install()
    {
        if (System.Threading.Interlocked.Exchange(ref _installed, 1) == 1) return;
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) => Write("UI thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("AppDomain", e.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) => Write("Unobserved task", e.Exception);
    }

    private static void Write(string source, Exception? exception)
    {
        string text = $"{DateTime.UtcNow:O} [{source}] {exception}{Environment.NewLine}";
        try
        {
            Console.Error.WriteLine(text);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            System.IO.File.AppendAllText(FilePath, text);
        }
        catch
        {
            // Diagnostics must never become the failure.
        }
    }
}

