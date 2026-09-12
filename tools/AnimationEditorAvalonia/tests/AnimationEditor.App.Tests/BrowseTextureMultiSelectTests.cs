using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1104: "Browse for Texture" only retextured <c>_selectedState.SelectedFrame</c> (the
/// primary frame), so committing a new texture with multiple frames selected silently left every
/// other selected frame untouched. Same root cause as #1103 (texture combo box), different entry
/// point. <c>ApplyPickedTextureAsync</c> is the extracted, dialog-free core of
/// <c>BrowseForFrameTexture</c> — see that method for the untestable OS file-picker residue.
/// </summary>
public class BrowseTextureMultiSelectTests
{
    private static void WriteSolidPng(string path, SKColor color, int size = 16)
    {
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    [AvaloniaFact]
    public async Task ApplyPickedTextureAsync_MultipleFramesSelected_AppliesToAll()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        // Untitled in-memory project: ShouldPromptToCopyForProject short-circuits to false on an
        // empty FileName, so the copy-prompt dialog branch (which itself needs a dialog) never runs.
        ctx.ProjectManager.FileName = null;

        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var pickedPngPath = Path.Combine(dir, "new.png");
        WriteSolidPng(pickedPngPath, SKColors.Green);

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
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
            Dispatcher.UIThread.RunJobs();

            await window.ApplyPickedTextureAsync(pickedPngPath);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(pickedPngPath, f0.TextureName);
            Assert.Equal(pickedPngPath, f1.TextureName);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
