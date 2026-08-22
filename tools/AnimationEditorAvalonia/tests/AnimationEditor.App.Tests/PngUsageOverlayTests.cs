using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Wiring tests for the PNG usage overlay (issue #953): the "Analyze Image Usage" toggle in the PNG
/// tab's toolbar scans the project for chains referencing the open PNG and overlays their frame
/// rects; clicking a rect navigates to the owning chain.
/// </summary>
public class PngUsageOverlayTests
{
    private static string WriteRealPng(string dir, string fileName, int width, int height)
    {
        var path = Path.Combine(dir, fileName);
        using var bmp = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
        bmp.Erase(new SkiaSharp.SKColor(200, 120, 60, 255));
        using var image = SkiaSharp.SKImage.FromBitmap(bmp);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static string WriteAchx(
        string dir, string fileName, string chainName, string textureName,
        float left, float top, float right, float bottom)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = textureName, FrameLength = 0.1f,
            LeftCoordinate = left, TopCoordinate = top, RightCoordinate = right, BottomCoordinate = bottom,
        });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
        return path;
    }

    // Pumps the dispatcher until the overlay status text moves off the transient "Scanning…"
    // message, or the bounded attempt count runs out (fails the assertions below, not hangs).
    private static async Task WaitForScanToFinishAsync(TextBlock status)
    {
        string scanning = "Scanning project for usage…";
        for (int i = 0; i < 200 && status.Text == scanning; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    [AvaloniaFact]
    public async Task PngUsageOverlayToggle_ChainReferencesOpenPng_ShowsMatchingRegion()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pngPath = WriteRealPng(dir, "hero.png", 64, 64);
            WriteAchx(dir, "walk.achx", "Walk", "hero.png", left: 0, top: 0, right: 32, bottom: 16);
            await window.OpenProjectFolderForTestAsync(dir);

            window.OpenPngAsTab(pngPath);
            Dispatcher.UIThread.RunJobs();
            await window.WhenPngTabLoaded();

            var toggle = window.FindControl<ToggleButton>("PngUsageOverlayToggle")!;
            var status = window.FindControl<TextBlock>("PngUsageOverlayStatus")!;
            toggle.IsChecked = true;
            await WaitForScanToFinishAsync(status);

            var pngPane = window.FindControl<PngPreviewControl>("PngPane")!;
            var group = Assert.Single(pngPane.UsageRegions);
            Assert.Equal("Walk", group.Chain.Name);
            var rect = Assert.Single(group.Rects);
            Assert.Equal(0f, rect.Left, precision: 1);
            Assert.Equal(0f, rect.Top, precision: 1);
            Assert.Equal(32f, rect.Right, precision: 1);
            Assert.Equal(16f, rect.Bottom, precision: 1);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task PngUsageOverlayToggle_UncheckedAfterScan_HidesRegions()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pngPath = WriteRealPng(dir, "hero.png", 32, 32);
            WriteAchx(dir, "walk.achx", "Walk", "hero.png", left: 0, top: 0, right: 32, bottom: 32);
            await window.OpenProjectFolderForTestAsync(dir);

            window.OpenPngAsTab(pngPath);
            Dispatcher.UIThread.RunJobs();
            await window.WhenPngTabLoaded();

            var toggle = window.FindControl<ToggleButton>("PngUsageOverlayToggle")!;
            var status = window.FindControl<TextBlock>("PngUsageOverlayStatus")!;
            var pngPane = window.FindControl<PngPreviewControl>("PngPane")!;
            toggle.IsChecked = true;
            await WaitForScanToFinishAsync(status);
            Assert.NotEmpty(pngPane.UsageRegions);

            toggle.IsChecked = false;

            Assert.Empty(pngPane.UsageRegions);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task UsageRegionsClicked_SingleMatch_NavigatesToOwningAchxAndSelectsChain()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pngPath = WriteRealPng(dir, "hero.png", 64, 64);
            WriteAchx(dir, "walk.achx", "Walk", "hero.png", left: 0, top: 0, right: 64, bottom: 64);
            await window.OpenProjectFolderForTestAsync(dir);

            window.OpenPngAsTab(pngPath);
            Dispatcher.UIThread.RunJobs();
            await window.WhenPngTabLoaded();

            var toggle = window.FindControl<ToggleButton>("PngUsageOverlayToggle")!;
            var status = window.FindControl<TextBlock>("PngUsageOverlayStatus")!;
            var pngPane = window.FindControl<PngPreviewControl>("PngPane")!;
            toggle.IsChecked = true;
            await WaitForScanToFinishAsync(status);
            Assert.NotEmpty(pngPane.UsageRegions);

            pngPane.SimulateUsageRegionClick(32f, 32f);   // inside the 0,0-64,64 rect

            // Navigation opens the achx tab asynchronously (fire-and-forget from the click handler) --
            // pump until the editor pane swaps back in, or fail out via the bounded loop.
            var achxPane = window.FindControl<Grid>("AchxEditorPane")!;
            for (int i = 0; i < 200 && !achxPane.IsVisible; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Assert.True(achxPane.IsVisible);
            Assert.False(pngPane.IsVisible);
            Assert.Equal("Walk", ctx.SelectedState.SelectedChain?.Name);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
