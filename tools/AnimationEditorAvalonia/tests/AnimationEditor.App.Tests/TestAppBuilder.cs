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
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

