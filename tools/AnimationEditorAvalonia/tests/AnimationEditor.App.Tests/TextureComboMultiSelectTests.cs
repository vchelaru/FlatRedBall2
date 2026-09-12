using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1103: same class of bug as #860 (see <see cref="FrameTextureNameMultiSelectTests"/>), but
/// for <c>TextureCombo</c> instead of the <c>PropTextureName</c> text field. <c>OnTextureComboChanged</c>
/// only ever read/wrote <c>_selectedState.SelectedFrame</c> (the primary frame), so picking a texture
/// from the combo with multiple frames selected silently left every other selected frame untouched.
/// </summary>
public class TextureComboMultiSelectTests
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
    public void OnTextureComboChanged_MultipleFramesSelected_AppliesToAll()
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

            // A second, unselected chain referencing b.png so the combo has a texture to pick
            // that differs from the selected frames' current one.
            var otherChain = new AnimationChainSave { Name = "Other" };
            otherChain.Frames.Add(new AnimationFrameSave
            {
                TextureName = "b.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave(),
            });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(otherChain);

            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = f0;
            ctx.SelectedState.SelectedNodes = new List<object> { f0, f1 };
            FlushUi();

            // Populate TextureCombo.Items from the project's referenced textures. In real usage
            // this runs via AvailableTexturesChanged after a file load; there's no lighter-weight
            // public hook to trigger it against an in-memory project built directly in a test.
            typeof(MainWindow)
                .GetMethod("RefreshTextureCombo", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, null);
            FlushUi();

            var textureCombo = window.FindControl<ComboBox>("TextureCombo")!;
            string bStandardized = new FilePath(Path.Combine(dir, "b.png")).Standardized;
            string targetItem = textureCombo.Items.Cast<string>()
                .Single(s => new FilePath(s).Standardized == bStandardized);

            textureCombo.SelectedItem = targetItem;
            FlushUi();

            Assert.Equal("b.png", f0.TextureName);
            Assert.Equal("b.png", f1.TextureName);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
