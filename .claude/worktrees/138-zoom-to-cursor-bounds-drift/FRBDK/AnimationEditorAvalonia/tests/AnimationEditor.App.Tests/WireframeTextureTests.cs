using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests that <see cref="WireframeControl"/> loads and displays the correct
/// texture when the selected frame's texture changes or when the user selects
/// a different frame.
///
/// Tutorial steps covered:
/// • "Set the TextureName property" → wireframe loads and shows the assigned texture.
/// • "Once you have changed the texture, you may need to adjust your coordinates."
///   → After a texture change + RefreshAll, the wireframe reflects the new texture.
/// • Switching SelectedFrame to one with a different texture reloads the wireframe.
/// </summary>
public class WireframeTextureTests
{
    private static void ResetSingletons()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        ProjectManager.Self.FileName               = null;
        SelectedState.Self.SelectedChain           = null;
        SelectedState.Self.SelectedFrame           = null;
        SelectedState.Self.SelectedNodes           = new System.Collections.Generic.List<object>();
        AppCommands.Self.DoOnUiThread              = a => a();
        AppCommands.Self.FileDialogService         = NullFileDialogService.Instance;
        AppState.Self.OffsetMultiplier             = 1f;
    }

    private static string WriteSolidPng(string dir, string name, SKColor color, int size = 32)
    {
        var path = System.IO.Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    // ── LoadTexture from frame TextureName ────────────────────────────────────

    /// <summary>
    /// After calling <see cref="WireframeControl.LoadTexture"/> with a red PNG,
    /// the rendered bitmap must be dominated by red inside the texture region.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_LoadTexture_RendersCorrectColor()
    {
        ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var redPng = WriteSolidPng(dir, "red.png", SKColors.Red, size: 32);

            var ctrl = new WireframeControl();
            ctrl.LoadTexture(redPng);
            ctrl.SetCamera(0f, 0f, 1f); // 1:1 mapping, texture at (0,0)

            using var bm = ctrl.RenderToBitmap(64, 64);

            // Centre of the 32×32 red texture at (16, 16) should be red-dominant
            var px = bm.GetPixel(16, 16);
            Assert.True(px.Red > 150,
                $"Wireframe should show red texture; R={px.Red} G={px.Green} B={px.Blue}");
            Assert.True(px.Red > px.Green + 50,
                $"Red should dominate; R={px.Red} G={px.Green}");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── RefreshAll picks up TextureName change ────────────────────────────────

    /// <summary>
    /// When the selected frame's <c>TextureName</c> changes (tutorial: "change texture
    /// via dropdown"), calling <see cref="WireframeControl.RefreshAll"/> must load
    /// the new texture and render its colour.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_RefreshAll_AfterTextureChange_RendersNewTexture()
    {
        ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var redPng  = WriteSolidPng(dir, "red.png",  SKColors.Red,  size: 32);
            var bluePng = WriteSolidPng(dir, "blue.png", SKColors.Blue, size: 32);
            ProjectManager.Self.FileName = System.IO.Path.Combine(dir, "test.achx");

            var frame = new AnimationFrameSave
            {
                TextureName     = "red.png", FrameLength = 0.1f,
                LeftCoordinate  = 0f, TopCoordinate    = 0f,
                RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave(),
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;
            SelectedState.Self.SelectedFrame = frame;

            var ctrl = new WireframeControl();
            ctrl.RefreshAll();
            ctrl.SetCamera(0f, 0f, 1f);

            using var bmRed = ctrl.RenderToBitmap(64, 64);
            // Pixel (6,6): inside frame, safely outside the top-left resize handle (0±5px)
            // and the origin crosshair centred at (16,16) ± 8px arm.
            var redPx = bmRed.GetPixel(6, 6);
            Assert.True(redPx.Red > 150,
                $"Initial texture should be red; R={redPx.Red}");

            // Now change the frame's texture to blue and refresh
            frame.TextureName = "blue.png";
            ctrl.RefreshAll();
            ctrl.SetCamera(0f, 0f, 1f);

            using var bmBlue = ctrl.RenderToBitmap(64, 64);
            var bluePx = bmBlue.GetPixel(6, 6);
            Assert.True(bluePx.Blue > 150,
                $"After texture change to blue, render should be blue; B={bluePx.Blue}");
            Assert.True(bluePx.Blue > bluePx.Red + 50,
                $"Blue should dominate after texture change; B={bluePx.Blue} R={bluePx.Red}");
        }
        finally
        {
            SelectedState.Self.SelectedFrame  = null;
            SelectedState.Self.SelectedChain  = null;
            ProjectManager.Self.FileName      = string.Empty;
            System.IO.Directory.Delete(dir, true);
        }
    }

    // ── Selecting a different frame loads its texture ─────────────────────────

    /// <summary>
    /// Tutorial: a chain can have two frames using different textures.
    /// When the user selects the second frame, the wireframe must switch to
    /// that frame's texture.
    ///
    /// Implementation path: <c>SelectedState.SelectionChanged</c> →
    /// <c>WireframeControl.RefreshAll()</c> (via AppCommands.RefreshWireframeRequested).
    /// Here we drive it directly via <see cref="WireframeControl.RefreshAll"/>.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_SelectDifferentFrame_LoadsFrameTexture()
    {
        ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var redPng  = WriteSolidPng(dir, "red.png",  SKColors.Red,  size: 32);
            var greenPng = WriteSolidPng(dir, "green.png", new SKColor(0, 200, 0), size: 32);
            ProjectManager.Self.FileName = System.IO.Path.Combine(dir, "test.achx");

            var frameRed = new AnimationFrameSave
            {
                TextureName = "red.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave(),
            };
            var frameGreen = new AnimationFrameSave
            {
                TextureName = "green.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave(),
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frameRed);
            chain.Frames.Add(frameGreen);
            ProjectManager.Self.AnimationChainListSave.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;

            var ctrl = new WireframeControl();

            // Select red frame — pixel (6,6): inside 32×32 frame, outside the top-left
            // resize handle (0±5px) and the origin crosshair centred at (16,16) ± 8px arm.
            SelectedState.Self.SelectedFrame = frameRed;
            ctrl.RefreshAll();
            ctrl.SetCamera(0f, 0f, 1f);
            using var bmRed = ctrl.RenderToBitmap(64, 64);
            var redPx = bmRed.GetPixel(6, 6);

            // Select green frame
            SelectedState.Self.SelectedFrame = frameGreen;
            ctrl.RefreshAll();
            ctrl.SetCamera(0f, 0f, 1f);
            using var bmGreen = ctrl.RenderToBitmap(64, 64);
            var greenPx = bmGreen.GetPixel(6, 6);

            Assert.True(redPx.Red > 150,
                $"First frame (red): R={redPx.Red}");
            Assert.True(greenPx.Green > 150,
                $"Second frame (green): G={greenPx.Green}");
            Assert.True(greenPx.Green > greenPx.Red + 50,
                $"Green should dominate after switching to green frame; G={greenPx.Green} R={greenPx.Red}");
        }
        finally
        {
            SelectedState.Self.SelectedFrame  = null;
            SelectedState.Self.SelectedChain  = null;
            ProjectManager.Self.FileName      = string.Empty;
            System.IO.Directory.Delete(dir, true);
        }
    }

    // ── Selecting a different frame on the same texture moves the bounding box ──

    /// <summary>
    /// Regression test: when two frames share the same texture, selecting the
    /// second frame must move the selection bounding box to that frame's UV region.
    ///
    /// Before the fix, <see cref="WireframeControl.LoadTexture"/> returned early
    /// ("same path") without calling <see cref="WireframeControl.RefreshFrames"/>,
    /// so the bounding box stayed on frame 1's coordinates even after selecting frame 2.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_SelectSecondFrameSameTexture_BoundingBoxMovesToFrame2()
    {
        ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            // Single 64×64 texture shared by both frames
            var png = WriteSolidPng(dir, "sheet.png", SKColors.Blue, size: 64);
            ProjectManager.Self.FileName = System.IO.Path.Combine(dir, "test.achx");

            // Frame 1: left half (UV 0.0–0.5)
            var frame1 = new AnimationFrameSave
            {
                TextureName      = "sheet.png", FrameLength = 0.1f,
                LeftCoordinate   = 0f,  TopCoordinate    = 0f,
                RightCoordinate  = 0.5f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave(),
            };
            // Frame 2: right half (UV 0.5–1.0)
            var frame2 = new AnimationFrameSave
            {
                TextureName      = "sheet.png", FrameLength = 0.1f,
                LeftCoordinate   = 0.5f, TopCoordinate    = 0f,
                RightCoordinate  = 1f,   BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave(),
            };
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frame1);
            chain.Frames.Add(frame2);
            ProjectManager.Self.AnimationChainListSave.AnimationChains.Add(chain);

            var ctrl = new WireframeControl();

            // Select frame 1 and load
            SelectedState.Self.SelectedFrame = frame1;
            ctrl.RefreshAll();
            ctrl.SetCamera(0f, 0f, 1f);

            var rectsAfterFrame1 = ctrl.GetFrameRects();
            Assert.Single(rectsAfterFrame1);
            var sel1 = rectsAfterFrame1[0];
            Assert.True(sel1.IsSelected, "Frame 1 should be selected");
            Assert.Equal(0f,  sel1.Bounds.Left,  1f);
            Assert.Equal(32f, sel1.Bounds.Right, 1f);   // 0.5 * 64 = 32

            // Now select frame 2 (same texture!) and refresh
            SelectedState.Self.SelectedFrame = frame2;
            ctrl.RefreshAll();

            var rectsAfterFrame2 = ctrl.GetFrameRects();
            Assert.Single(rectsAfterFrame2);
            var sel2 = rectsAfterFrame2[0];
            Assert.True(sel2.IsSelected, "Frame 2 should be selected");
            Assert.Equal(32f, sel2.Bounds.Left,  1f);   // 0.5 * 64 = 32
            Assert.Equal(64f, sel2.Bounds.Right, 1f);   // 1.0 * 64 = 64
        }
        finally
        {
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            ProjectManager.Self.FileName     = string.Empty;
            System.IO.Directory.Delete(dir, true);
        }
    }

    // ── LoadedTexturePath reflects current texture ────────────────────────────

    /// <summary>
    /// <see cref="WireframeControl.LoadedTexturePath"/> must return the normalised
    /// absolute path of the currently loaded PNG, or null when nothing is loaded.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_LoadedTexturePath_ReflectsLoadedTexture()
    {
        ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, "tex.png", SKColors.Cyan);
            var ctrl = new WireframeControl();

            Assert.Null(ctrl.LoadedTexturePath);

            ctrl.LoadTexture(png);

            Assert.NotNull(ctrl.LoadedTexturePath);
            Assert.True(ctrl.LoadedTexturePath!.EndsWith("tex.png",
                System.StringComparison.OrdinalIgnoreCase),
                $"LoadedTexturePath should end with 'tex.png'; got: {ctrl.LoadedTexturePath}");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── BitmapSize matches loaded texture ─────────────────────────────────────

    /// <summary>
    /// <see cref="WireframeControl.BitmapSize"/> must return the pixel dimensions
    /// of the loaded texture (used by MainWindow to compute UV coords from pixel bounds).
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_BitmapSize_MatchesLoadedTextureDimensions()
    {
        ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, "sprite.png", SKColors.Yellow, size: 48);
            var ctrl = new WireframeControl();

            Assert.Equal((0, 0), ctrl.BitmapSize);

            ctrl.LoadTexture(png);

            Assert.Equal((48, 48), ctrl.BitmapSize);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
