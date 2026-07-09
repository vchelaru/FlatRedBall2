using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// Off-screen PNG capture for documentation screenshots. Generalizes the
/// <c>RenderToBitmap</c> pattern already used by <c>WireframeControl</c>/<c>PreviewControl</c>
/// (which draw directly onto an SkiaSharp canvas) to ANY Avalonia visual — tree view, inspector
/// panel, dialogs, or a full <see cref="Window"/> — by capturing the owning
/// <see cref="TopLevel"/>'s rendered frame (<see cref="HeadlessWindowExtensions.CaptureRenderedFrame"/>,
/// the same headless-specific API <c>Avalonia.Headless</c> itself uses) and, for a sub-control,
/// cropping to that control's bounds.
/// </summary>
/// <remarks>
/// <para>
/// Requires this project's <see cref="TestAppBuilder"/>, which sets
/// <c>AvaloniaHeadlessPlatformOptions.UseHeadlessDrawing = false</c> — with the (default) true
/// no-op drawing recorder, <c>CaptureRenderedFrame</c> always returns null. A plain
/// <see cref="RenderTargetBitmap"/> rendered by hand (<c>new RenderTargetBitmap(size).Render(visual)</c>)
/// has the same problem for a different reason: it never pumps the headless render-timer tick, so it
/// silently writes an empty file instead of throwing. Route everything through
/// <c>CaptureRenderedFrame</c> instead of trying to render visuals directly.
/// </para>
/// <para>
/// Must be called on the UI thread after layout has run for <paramref name="visual"/> — e.g.
/// after <c>Window.Show()</c> and <c>Avalonia.Threading.Dispatcher.UIThread.RunJobs()</c>.
/// </para>
/// </remarks>
internal static class ScreenshotCapture
{
    /// <summary>
    /// Captures <paramref name="visual"/> to a PNG at <paramref name="outputPath"/>, creating
    /// parent directories as needed. For a <see cref="TopLevel"/> (a <see cref="Window"/> or
    /// dialog) the full client area is captured, chrome included. For any other <see cref="Control"/>,
    /// the owning <see cref="TopLevel"/> is captured and cropped down to that control's bounds.
    /// </summary>
    public static void Capture(Visual visual, string outputPath)
    {
        var root = visual as TopLevel ?? TopLevel.GetTopLevel(visual)
            ?? throw new InvalidOperationException($"'{visual}' is not attached to a TopLevel.");

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = root.CaptureRenderedFrame()
            ?? throw new InvalidOperationException(
                "Headless renderer produced no frame to capture — is the window/dialog shown?");

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        if (ReferenceEquals(visual, root))
        {
            frame.Save(fullPath);
            return;
        }

        var control = (Control)visual;
        if (control.Bounds.Width < 1 || control.Bounds.Height < 1)
        {
            throw new InvalidOperationException(
                $"Cannot capture '{control}': bounds are {control.Bounds}. Ensure it is visible " +
                "and laid out (Window.Show() + Dispatcher.UIThread.RunJobs()) before capturing.");
        }

        var topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException($"'{control}' is not visible under its root.");

        var sourceRect = new Rect(topLeft, control.Bounds.Size);
        var pixelSize = new PixelSize((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height));

        using var cropped = new RenderTargetBitmap(pixelSize);
        using (var ctx = cropped.CreateDrawingContext())
            ctx.DrawImage(frame, sourceRect, new Rect(pixelSize.ToSize(1)));
        cropped.Save(fullPath);
    }
}
