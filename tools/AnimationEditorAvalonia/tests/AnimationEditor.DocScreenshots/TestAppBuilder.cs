using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;
using Xunit;

// Assembly-level attribute wires Avalonia.Headless to the TestAppBuilder.
[assembly: AvaloniaTestApplication(typeof(AnimationEditor.DocScreenshots.TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// Separate assembly from <c>AnimationEditor.App.Tests</c> specifically so it can set
/// <see cref="AvaloniaHeadlessPlatformOptions.UseHeadlessDrawing"/> to <c>false</c>. That
/// option defaults to <c>true</c> (a no-op drawing recorder, for speed) across the other
/// ~90 App.Tests files, which never need real pixels — they either assert on control-tree
/// state directly or go through WireframeControl/PreviewControl's own SkiaSharp
/// <c>RenderToBitmap</c>, which draws straight onto a canvas and bypasses Avalonia's
/// compositor entirely. <see cref="ScreenshotCapture"/> instead captures Avalonia's own
/// composited output for an arbitrary visual, so it needs the compositor to actually run.
/// </summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<global::AnimationEditor.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
