// Assembly-level attribute wires Avalonia.Headless to the TestAppBuilder.
// AvaloniaTestApplicationAttribute lives in Avalonia.Headless (not Avalonia.Headless.XUnit).
using Avalonia;
using Avalonia.Headless;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(AnimationEditor.Views.Tests.TestAppBuilder))]
// Mirrors AnimationEditor.App.Tests: the shared headless Compositor/render loop enforces
// single-thread affinity, so parallel test collections race to initialize it. Serial only.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AnimationEditor.Views.Tests;

/// <summary>
/// Factory consumed by [AvaloniaTestApplication] to build the headless app. AnimationEditor.Views
/// is a library with no Application of its own (unlike AnimationEditor.App, which App.Tests reuses
/// directly) -- <see cref="TestApp"/> is a minimal stand-in that just registers FluentTheme, since
/// that's what AnimationEditor.Browser's own App.axaml does for these controls at runtime.
/// </summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
