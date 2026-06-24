using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for <see cref="MainWindow.HandleHeaderTextDoubleTap"/> — issue #234.
/// Double-tapping a frame's text label must center the wireframe on the frame,
/// never start an inline rename; a chain's text label still inline-renames.
/// </summary>
public class HeaderTextDoubleTapTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private static string WriteSolidPng(string dir, string name, int width, int height)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(width, height);
        bm.Erase(SKColors.Blue);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [AvaloniaFact]
    public void HandleHeaderTextDoubleTap_ChainNode_BeginsInlineRename()
    {
        var ctx    = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();

        var vm = new TreeNodeVm { Data = new AnimationChainSave { Name = "Walk" } };

        window.HandleHeaderTextDoubleTap(vm);

        Assert.True(vm.IsEditing,
            "Double-tapping a chain's text label should start an inline rename — its name is meaningful.");

        window.Close();
    }

    [AvaloniaFact]
    public void HandleHeaderTextDoubleTap_FrameNode_CentersWireframeWithoutRenaming()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")
                ?? throw new InvalidOperationException("WireframeCtrl not found");
            wireframe.LoadTexture(WriteSolidPng(dir, "tex.png", 500, 500));
            Dispatcher.UIThread.RunJobs();

            // Frame UV 0.6–0.8 → a 100×100 region centred at texture pixel (350, 350).
            const float texCX = 350f, texCY = 350f;
            var frame = new AnimationFrameSave
            {
                LeftCoordinate  = 0.6f, TopCoordinate    = 0.6f,
                RightCoordinate = 0.8f, BottomCoordinate = 0.8f,
            };
            var vm = new TreeNodeVm { Data = frame };

            window.HandleHeaderTextDoubleTap(vm);
            Dispatcher.UIThread.RunJobs();

            Assert.False(vm.IsEditing,
                "Double-tapping a frame's text label must not start an inline rename.");

            // Centering scrolls the frame centre to the viewport centre at the current zoom
            // (screenX = panX + texX*zoom) → it centered on the frame.
            var (panX, panY, zoom) = wireframe.CameraState;
            float vpW = (float)wireframe.Bounds.Width;
            float vpH = (float)wireframe.Bounds.Height;
            Assert.Equal(vpW / 2f, panX + texCX * zoom, 1);
            Assert.Equal(vpH / 2f, panY + texCY * zoom, 1);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
