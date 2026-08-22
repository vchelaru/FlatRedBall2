using AnimationEditor.Core.IO;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #860: TextureName was the one frame property without multi-select batch-edit parity
/// (see #571 for every other field). <c>ApplyTextureName</c> only ever read/wrote
/// <c>_selectedState.SelectedFrame</c> (the primary frame), so committing a new texture path
/// with multiple frames selected silently left every other selected frame untouched.
/// </summary>
public class FrameTextureNameMultiSelectTests
{
    private static (MainWindow Window, TestServices Ctx, string Dir) CreateWindowWithTextures()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

        WriteSolidPng(dir, "a.png", SKColors.Red);
        WriteSolidPng(dir, "b.png", SKColors.Blue);

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx, dir);
    }

    private static void WriteSolidPng(string dir, string name, SKColor color, int size = 16)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    private static void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ApplyTextureName_MultipleFramesSelected_AppliesToAll()
    {
        var (window, ctx, dir) = CreateWindowWithTextures();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave
            {
                TextureName = "a.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave(),
            };
            var f1 = new AnimationFrameSave
            {
                TextureName = "a.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave(),
            };
            chain.Frames.AddRange(new[] { f0, f1 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = f0;
            ctx.SelectedState.SelectedNodes = new List<object> { f0, f1 };
            FlushUi();

            var propTextureName = window.FindControl<TextBox>("PropTextureName")!;
            propTextureName.Focus();
            FlushUi();
            propTextureName.Text = "b.png";
            // Move focus away to raise LostFocus, which ApplyTextureName is wired to. PropFrameLen
            // is a FlankerNumericField (#963); focus lands on its inner ValueBox TextBox.
            window.FindControl<FlankerNumericField>("PropFrameLen")!
                .GetVisualDescendants().OfType<TextBox>().First().Focus();
            FlushUi();

            Assert.Equal("b.png", f0.TextureName);
            Assert.Equal("b.png", f1.TextureName);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
