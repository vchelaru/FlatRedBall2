using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The "borrow a texture to seed the first frame" fallback (#618) only fired while an empty
/// *chain* was selected in an otherwise-textured document. It did nothing for a chain-less
/// document (nothing to select, so RefreshAll never ran) and, worse, actively blanked the
/// canvas the moment that document's first empty chain was selected — RefreshAll found no
/// texture anywhere in the new document and cleared whatever was already showing. This left
/// users staring at a blank canvas right after creating their first chain, with no way to seed
/// a frame by Ctrl+clicking. The fix: when there's nothing to borrow, RefreshAll leaves the
/// currently-loaded texture in place instead of clearing it, so the same "default" texture
/// carries through an empty document, an empty chain, and into that chain's first frame.
/// </summary>
public class WireframeDefaultTexturePersistsTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new System.Collections.Generic.List<object>();
        return ctx;
    }

    private static string WriteSolidPng(string dir, string name, int size = 64)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(SKColors.Red);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [AvaloniaFact]
    public void SelectingEmptyChainInDocumentWithNoTextureAnywhere_KeepsPreviouslyLoadedTexture()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, "sheet.png");
            var ctx = ResetSingletons();
            var ctrl = ctx.CreateWireframeControl();

            // A texture is already showing, e.g. carried over from the previously active tab.
            ctrl.LoadTexture(png);
            Assert.Equal((64, 64), ctrl.BitmapSize);

            // Switch to a brand-new, entirely empty document (0 chains -- nothing anywhere in it
            // to borrow a texture from) with nothing selected. Mirrors opening a new .achx.
            ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
            ctx.SelectedState.SelectedChain = null;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((64, 64), ctrl.BitmapSize);

            // Add the document's first chain (still zero frames) and select it -- this is what
            // actually fires SelectionChanged/RefreshAll for the first time in the new document.
            var emptyChain = new AnimationChainSave { Name = "NewAnimation" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(emptyChain);
            ctx.SelectedState.SelectedChain = emptyChain;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal((64, 64), ctrl.BitmapSize);
            Assert.EndsWith("sheet.png", ctrl.LoadedTexturePathCasePreserved);
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void CtrlClick_OnDefaultTextureCarriedIntoEmptyChain_SeedsFirstFrameWithIt()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var png = WriteSolidPng(dir, "Items.png");
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")
                ?? throw new InvalidOperationException("WireframeCtrl not found");
            wireframe.LoadTexture(png);
            wireframe.SetCamera(0f, 0f, 1f);

            var chain = new AnimationChainSave { Name = "NewAnimation" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal((64, 64), wireframe.BitmapSize);   // default survived selecting the empty chain

            wireframe.SimulatePlainCtrlClick(32, 32);

            Assert.Single(chain.Frames);
            Assert.EndsWith("Items.png", chain.Frames[0].TextureName);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// The tree's "Add Frame" menu/button (<c>AppCommands.AddFrame</c>, no explicit texture) is a
    /// second, independent entry point from Ctrl+click -- it resolves its own texture from the
    /// document instead of reading the wireframe canvas directly. The manual repro that surfaced
    /// this used this exact path (right-click the chain -> Add Frame) and still got a textureless
    /// frame after the RefreshAll fix, because AddFrame never consulted the canvas at all.
    /// </summary>
    [AvaloniaFact]
    public void AddFrameButton_OnEmptyChainWithDefaultTextureShowing_UsesTheDefaultTexture()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = ResetSingletons();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var png = WriteSolidPng(dir, "Items.png");
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")
                ?? throw new InvalidOperationException("WireframeCtrl not found");
            wireframe.LoadTexture(png);

            var chain = new AnimationChainSave { Name = "NewAnimation" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal((64, 64), wireframe.BitmapSize);   // default survived selecting the empty chain

            ctx.AppCommands.AddFrame(chain);   // the "Add Frame" tree menu item's exact call

            Assert.Single(chain.Frames);
            Assert.EndsWith("Items.png", chain.Frames[0].TextureName);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
