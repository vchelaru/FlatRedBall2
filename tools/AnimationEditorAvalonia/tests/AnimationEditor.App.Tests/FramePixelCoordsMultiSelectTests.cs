using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #860: unlike every other frame property, the PIXEL COORD fields (X/Y/W/H) were never
/// pinned by an App-layer test that drives the real property-panel Apply path — only
/// <c>InspectorPropertyUndoTests</c> exercises <c>IAppCommands.SetFramePixelRegion</c> directly,
/// bypassing <c>ApplyFramePixelCoords</c>'s dependency on <c>WireframeControl.BitmapSize</c>
/// (zero until a real bitmap is loaded). These tests drive the fields through
/// <see cref="MainWindow"/> exactly as a user would, with a real PNG on disk so the wireframe
/// actually loads a bitmap.
/// </summary>
public class FramePixelCoordsMultiSelectTests
{
    private static (MainWindow Window, TestServices Ctx, string Dir) CreateWindowWithTexture()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

        var pngPath = Path.Combine(dir, "sprite.png");
        using (var bm = new SKBitmap(64, 64))
        {
            bm.Erase(SKColors.Gray);
            using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(pngPath, data.ToArray());
        }

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx, dir);
    }

    private static void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ApplyFramePixelCoords_MultipleFramesSelected_AppliesPixelXToBothPreservingEachWidth()
    {
        var (window, ctx, dir) = CreateWindowWithTexture();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave
            {
                TextureName = "sprite.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, RightCoordinate = 16f,
                TopCoordinate = 0f, BottomCoordinate = 64f,
                ShapesSave = new ShapesSave(),
            };
            var f1 = new AnimationFrameSave
            {
                TextureName = "sprite.png", FrameLength = 0.1f,
                LeftCoordinate = 32f, RightCoordinate = 48f,
                TopCoordinate = 0f, BottomCoordinate = 64f,
                ShapesSave = new ShapesSave(),
            };
            chain.Frames.AddRange(new[] { f0, f1 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = f0;
            ctx.SelectedState.SelectedNodes = new List<object> { f0, f1 };
            FlushUi();

            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
            Assert.Equal((64, 64), wireframe.BitmapSize); // sanity: bitmap must be loaded for this section to work at all

            var propPixelX = window.FindControl<NumericUpDown>("PropPixelX")!;
            propPixelX.Value = 8m;
            FlushUi();

            // SetX preserves each frame's own width (16px for both here).
            Assert.Equal(8f, f0.LeftCoordinate, 3);
            Assert.Equal(24f, f0.RightCoordinate, 3);
            Assert.Equal(8f, f1.LeftCoordinate, 3);
            Assert.Equal(24f, f1.RightCoordinate, 3);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void ApplyFramePixelCoords_MultipleFramesSelected_AppliesPixelWToBoth()
    {
        var (window, ctx, dir) = CreateWindowWithTexture();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave
            {
                TextureName = "sprite.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, RightCoordinate = 16f,
                TopCoordinate = 0f, BottomCoordinate = 64f,
                ShapesSave = new ShapesSave(),
            };
            var f1 = new AnimationFrameSave
            {
                TextureName = "sprite.png", FrameLength = 0.1f,
                LeftCoordinate = 32f, RightCoordinate = 48f,
                TopCoordinate = 0f, BottomCoordinate = 64f,
                ShapesSave = new ShapesSave(),
            };
            chain.Frames.AddRange(new[] { f0, f1 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = f0;
            ctx.SelectedState.SelectedNodes = new List<object> { f0, f1 };
            FlushUi();

            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
            Assert.Equal((64, 64), wireframe.BitmapSize);

            var propPixelW = window.FindControl<NumericUpDown>("PropPixelW")!;
            propPixelW.Value = 24m;
            FlushUi();

            // SetWidth keeps each frame's own Left fixed, only Right moves.
            Assert.Equal(0f, f0.LeftCoordinate, 3);
            Assert.Equal(24f, f0.RightCoordinate, 3);
            Assert.Equal(32f, f1.LeftCoordinate, 3);
            Assert.Equal(56f, f1.RightCoordinate, 3);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
