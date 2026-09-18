using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// <c>RefreshTextureCombo</c>/<c>SyncTextureCombo</c> fed <c>WireframeControl.LoadTexture</c> a
/// <see cref="AnimationEditor.Core.Paths.FilePath.Standardized"/> (lowercased) path instead of the
/// case-preserved <c>FullPath</c>. Browsing a correctly-cased texture in one document poisons the
/// shared <c>WireframeControl</c>'s remembered identity, and a Ctrl+click on an unrelated, brand-new
/// empty document -- which borrows that identity per #618 -- bakes the lowercased name into the new
/// frame. Windows' case-insensitive filesystem never complains, so nothing catches it until the
/// .achx is diffed.
/// </summary>
public class TextureCasePreservationTests
{
    private static void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static void WriteSolidPng(string path, int size = 16)
    {
        using var bm = new SKBitmap(size, size);
        bm.Erase(SKColors.Red);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    [AvaloniaFact]
    public void CtrlClick_OnNewEmptyDocument_AfterBrowsingMixedCaseTextureElsewhere_PreservesCase()
    {
        var ctx = TestHelpers.BuildServices();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteSolidPng(Path.Combine(dir, "MixedCase.png"));

            var window = ctx.CreateMainWindow();
            window.Show();
            FlushUi();

            // Document A: an existing chain correctly referencing "MixedCase.png".
            var textured = new AnimationChainSave { Name = "Has" };
            textured.Frames.Add(new AnimationFrameSave
            {
                TextureName = "MixedCase.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave(),
            });
            var aclsA = new AnimationChainListSave();
            aclsA.AnimationChains.Add(textured);
            ctx.ProjectManager.FileName = Path.Combine(dir, "A.achx");
            ctx.ProjectManager.AnimationChainListSave = aclsA;

            // Populate the texture combo -- what browsing/switching to document A does in real
            // usage, and where the case gets lost today.
            typeof(MainWindow)
                .GetMethod("RefreshTextureCombo", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, null);
            FlushUi();

            // Document B: a brand-new, unsaved, entirely empty document (File -> New Animation)
            // with a chain that has no frames -- mirrors the real repro exactly.
            var emptyChain = new AnimationChainSave { Name = "Empty" };
            var aclsB = new AnimationChainListSave();
            aclsB.AnimationChains.Add(emptyChain);
            ctx.ProjectManager.FileName = null;
            ctx.ProjectManager.AnimationChainListSave = aclsB;
            ctx.SelectedState.SelectedChain = emptyChain;
            FlushUi();

            var ctrl = window.FindControl<WireframeControl>("WireframeCtrl")!;
            Assert.True(ctrl.BitmapSize.Width > 0, "Wireframe should still show the last-loaded texture (#618).");

            ctrl.SimulatePlainCtrlClick(8, 8);
            FlushUi();

            var newFrame = Assert.Single(emptyChain.Frames);
            Assert.Contains("MixedCase.png", newFrame.TextureName);
            Assert.DoesNotContain("mixedcase.png", newFrame.TextureName);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
