using System;
using System.IO;
using AnimationEditor.App;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// Spike for #636's open question: does headless capture work for modal dialogs?
/// Answer: yes — <c>ShowDialog</c> returns a <see cref="System.Threading.Tasks.Task"/> that only
/// completes when the dialog closes, so a synchronous test calls it without awaiting, flushes the
/// dispatcher (which runs layout for the now-open dialog window), captures it like any other
/// <see cref="Avalonia.Controls.TopLevel"/>, then closes the dialog to complete the pending Task.
/// </summary>
public class DialogScreenshotSpikeTests
{
    [AvaloniaFact]
    public void AboutDialog_CanBeCapturedHeadlessly()
    {
        var ctx = TestHelpers.BuildServices();
        var owner = ctx.CreateMainWindow();
        owner.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var dialog = MainWindow.BuildAboutWindow();
            var shownTask = dialog.ShowDialog(owner);
            Dispatcher.UIThread.RunJobs();

            var outputDir = Path.Combine(Path.GetTempPath(), "AnimationEditorDocScreenshots", Guid.NewGuid().ToString("N"));
            var outputPath = Path.Combine(outputDir, "about-dialog.png");
            try
            {
                ScreenshotCapture.Capture(dialog, outputPath);

                Assert.True(File.Exists(outputPath));
                using var bitmap = SKBitmap.Decode(outputPath);
                Assert.True(bitmap.Width > 0 && bitmap.Height > 0);

                var first = bitmap.GetPixel(0, 0);
                bool hasVisibleContent = false;
                for (int y = 0; y < bitmap.Height && !hasVisibleContent; y += 4)
                    for (int x = 0; x < bitmap.Width; x += 4)
                        if (bitmap.GetPixel(x, y) != first) { hasVisibleContent = true; break; }
                Assert.True(hasVisibleContent, "About dialog capture was a single flat color.");
            }
            finally
            {
                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, recursive: true);
            }

            dialog.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.True(shownTask.IsCompleted);
        }
        finally { owner.Close(); }
    }
}
