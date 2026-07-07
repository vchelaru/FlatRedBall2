using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Wiring tests for the PNG viewer tab (issue #604): double-clicking a PNG in the Files panel
/// routes through <see cref="MainWindow.OpenPngAsTab"/>, which must show the PNG pane in place of
/// the achx editor and swap back when an achx tab takes over.
/// </summary>
public class PngTabTests
{
    // A 1×1 transparent PNG — enough for the tab-open path; the visibility swap does not depend
    // on the decode succeeding, but a real file keeps the test honest.
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static string WritePng(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, Convert.FromBase64String(OnePixelPngBase64));
        return path;
    }

    // A real SkiaSharp-encoded PNG (the 1×1 base64 fixture above doesn't decode in this environment),
    // for tests that assert the image actually loads.
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

    private static string WriteAchx(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Walk.png", FrameLength = 0.1f });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
        return path;
    }

    [AvaloniaFact]
    public async Task OpenPngAsTab_ShowsPngPane_HidesEditor()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pngPath = WritePng(dir, "sheet.png");

            window.OpenPngAsTab(pngPath);
            Dispatcher.UIThread.RunJobs();
            await window.WhenPngTabLoaded();   // drain the off-thread git + decode before cleanup deletes dir

            Assert.True(window.FindControl<PngPreviewControl>("PngPane")!.IsVisible);
            Assert.False(window.FindControl<Grid>("AchxEditorPane")!.IsVisible);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task OpenAchxAfterPng_SwapsBackToEditorPane()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pngPath = WritePng(dir, "sheet.png");
            var achxPath = WriteAchx(dir, "hero.achx");

            window.OpenPngAsTab(pngPath);
            Dispatcher.UIThread.RunJobs();
            await window.WhenPngTabLoaded();   // drain the off-thread git + decode before cleanup deletes dir
            await window.OpenFileAsTab(achxPath);
            Dispatcher.UIThread.RunJobs();

            Assert.False(window.FindControl<PngPreviewControl>("PngPane")!.IsVisible);
            Assert.True(window.FindControl<Grid>("AchxEditorPane")!.IsVisible);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task OpenPngAsTab_HidesEditingTabsAndFiles_SelectsDiff()
    {
        // A read-only PNG has no animations, no editable properties, and no undo history — and for now
        // we don't offer file navigation from a PNG either. Hide the ANIMATIONS tree, Inspector,
        // History, and Files tabs; show only the Diff tab and select it.
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            window.OpenPngAsTab(WritePng(dir, "sheet.png"));
            Dispatcher.UIThread.RunJobs();
            await window.WhenPngTabLoaded();   // drain the off-thread git + decode before cleanup deletes dir

            Assert.False(window.FindControl<Grid>("AnimationsBlock")!.IsVisible);
            Assert.False(window.FindControl<TabItem>("InspectorTab")!.IsVisible);
            Assert.False(window.FindControl<TabItem>("HistoryTab")!.IsVisible);
            Assert.False(window.FindControl<TabItem>("FilesTab")!.IsVisible);
            Assert.True(window.FindControl<TabItem>("DiffBlameTab")!.IsVisible);

            var tabs = window.FindControl<TabControl>("SidebarTabs")!;
            Assert.Same(window.FindControl<TabItem>("DiffBlameTab"), tabs.SelectedItem);

            // The Animations block's row must collapse to zero so the Diff tab fills the column
            // rather than floating below an empty gap.
            Assert.Equal(0, window.FindControl<Grid>("LeftPanelGrid")!.RowDefinitions[0].Height.Value);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task OpenPngAsTab_LoadsHistoryOffUiThread_ResolvesLoadingState()
    {
        // The git history load runs off the UI thread (so a slow `git log --follow` doesn't freeze
        // the app). Right after opening, the panel shows a transient "Loading…" status; after the
        // background load marshals its result back, that status resolves to a terminal message.
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            window.OpenPngAsTab(WritePng(dir, "sheet.png"));

            var status = window.FindControl<TextBlock>("DiffBlameStatus")!;
            string loading = status.Text ?? "";   // set synchronously to the transient loading message

            // Pump the dispatcher until the off-thread load posts its result back (bounded so a hang
            // fails the test rather than spinning forever).
            for (int i = 0; i < 200 && (status.Text ?? "") == loading; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            // The temp dir isn't a git repo, so the load resolves to a terminal status — the point is
            // that it changed off the loading state, proving the async load completed and marshaled back.
            Assert.NotEqual(loading, status.Text);

            await window.WhenPngTabLoaded();   // drain the off-thread decode before cleanup deletes dir
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task LoadTextureAsync_DecodesOffThread_ShowsBlankThenImage()
    {
        // The image decodes off the UI thread: the control blanks synchronously (no bitmap yet) and
        // the decoded image appears only after the awaited decode completes — so the tab can show
        // immediately instead of stalling on a large SKBitmap.Decode.
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteRealPng(dir, "sheet.png", 3, 5);
            var control = new PngPreviewControl();

            Task<bool> load = control.LoadTextureAsync(png);
            // Still on the UI thread here: the decode's install continuation can't have run yet.
            Assert.Equal((0, 0), control.BitmapSize);

            bool shown = await load;

            Assert.True(shown);
            Assert.Equal((3, 5), control.BitmapSize);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task OpenAchxAfterPng_RestoresAnimationInspectorAndHistory()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            window.OpenPngAsTab(WritePng(dir, "sheet.png"));
            Dispatcher.UIThread.RunJobs();
            await window.WhenPngTabLoaded();   // drain the off-thread git + decode before cleanup deletes dir
            await window.OpenFileAsTab(WriteAchx(dir, "hero.achx"));
            Dispatcher.UIThread.RunJobs();

            Assert.True(window.FindControl<Grid>("AnimationsBlock")!.IsVisible);
            Assert.True(window.FindControl<TabItem>("InspectorTab")!.IsVisible);
            Assert.True(window.FindControl<TabItem>("HistoryTab")!.IsVisible);
            Assert.True(window.FindControl<TabItem>("FilesTab")!.IsVisible);   // Files comes back for the achx editor
            Assert.True(window.FindControl<Grid>("LeftPanelGrid")!.RowDefinitions[0].Height.Value > 0);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
