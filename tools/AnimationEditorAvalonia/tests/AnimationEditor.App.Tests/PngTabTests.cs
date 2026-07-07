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
    public void OpenPngAsTab_ShowsPngPane_HidesEditor()
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
    public void OpenPngAsTab_HidesEditingTabsAndFiles_SelectsDiff()
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
