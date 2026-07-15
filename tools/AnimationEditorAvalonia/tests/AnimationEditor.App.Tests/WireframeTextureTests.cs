using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
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
    private static TestServices ResetSingletons() {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        ctx.AppState.OffsetMultiplier             = 1f;
        return ctx;
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

    // ── Undecodable texture must not crash (issue #479) ───────────────────────

    /// <summary>
    /// Regression for issue #479: <see cref="SKBitmap.Decode"/> returns <c>null</c> (it does
    /// not throw) for a file that exists but cannot be decoded as an image — a zero-byte file,
    /// a corrupt/truncated PNG, a mislabeled non-image, or a locked file. The old code handed
    /// that null straight to <c>SKImage.FromBitmap</c>, which threw <see cref="ArgumentNullException"/>
    /// on the dispatcher and terminated the process.
    ///
    /// <see cref="WireframeControl.LoadTexture"/> must instead fail gracefully: not throw, and
    /// leave the control in a coherent unloaded state — even when a valid texture was loaded first.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_LoadTexture_UndecodableFile_DoesNotThrow_LeavesUnloaded()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var goodPng = WriteSolidPng(dir, "good.png", SKColors.Red, size: 32);

            // Zero-byte file: exists on disk, but SKBitmap.Decode returns null.
            var badPath = System.IO.Path.Combine(dir, "corrupt.png");
            System.IO.File.WriteAllBytes(badPath, Array.Empty<byte>());

            var ctrl = ctx.CreateWireframeControl();

            // Establish a loaded texture first, so we also verify the failed load clears it.
            ctrl.LoadTexture(goodPng);
            Assert.NotNull(ctrl.LoadedTexturePath);

            var ex = Record.Exception(() => ctrl.LoadTexture(badPath));
            Assert.Null(ex);   // must not throw (was ArgumentNullException via SKImage.FromBitmap)

            Assert.Null(ctrl.LoadedTexturePath);   // coherent unloaded state
            Assert.Equal((0, 0), ctrl.BitmapSize);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// <see cref="WireframeControl.LoadTexture"/> reports success via its return value so callers
    /// can commit the texture name only when the image actually displays (issue #479 rollback):
    /// <c>true</c> for a valid image and for an empty/clear path, <c>false</c> for an undecodable
    /// file or a path that isn't on disk.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_LoadTexture_ReturnsLoadOutcome()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var goodPng = WriteSolidPng(dir, "good.png", SKColors.Green, size: 16);
            var badPath = System.IO.Path.Combine(dir, "corrupt.png");
            System.IO.File.WriteAllBytes(badPath, Array.Empty<byte>());
            var missingPath = System.IO.Path.Combine(dir, "does-not-exist.png");

            var ctrl = ctx.CreateWireframeControl();

            Assert.True(ctrl.LoadTexture(goodPng),      "valid image should load");
            Assert.False(ctrl.LoadTexture(badPath),     "undecodable file should report failure");
            Assert.False(ctrl.LoadTexture(missingPath), "missing file should report failure");
            Assert.True(ctrl.LoadTexture(null),         "empty path is an intentional clear, not a failure");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── LoadTexture from frame TextureName ────────────────────────────────────

    /// <summary>
    /// After calling <see cref="WireframeControl.LoadTexture"/> with a red PNG,
    /// the rendered bitmap must be dominated by red inside the texture region.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_LoadTexture_RendersCorrectColor()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var redPng = WriteSolidPng(dir, "red.png", SKColors.Red, size: 32);

            var ctrl = ctx.CreateWireframeControl();
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
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var redPng  = WriteSolidPng(dir, "red.png",  SKColors.Red,  size: 32);
            var bluePng = WriteSolidPng(dir, "blue.png", SKColors.Blue, size: 32);
            ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

            var frame = new AnimationFrameSave
            {
                TextureName     = "red.png", FrameLength = 0.1f,
                LeftCoordinate  = 0f, TopCoordinate    = 0f,
                RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave(),
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;

            var ctrl = ctx.CreateWireframeControl();
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
            ctx.SelectedState.SelectedFrame  = null;
            ctx.SelectedState.SelectedChain  = null;
            ctx.ProjectManager.FileName      = string.Empty;
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
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var redPng  = WriteSolidPng(dir, "red.png",  SKColors.Red,  size: 32);
            var greenPng = WriteSolidPng(dir, "green.png", new SKColor(0, 200, 0), size: 32);
            ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

            var frameRed = new AnimationFrameSave
            {
                TextureName = "red.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave(),
            };
            var frameGreen = new AnimationFrameSave
            {
                TextureName = "green.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave(),
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frameRed);
            chain.Frames.Add(frameGreen);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            var ctrl = ctx.CreateWireframeControl();

            // Select red frame — pixel (6,6): inside 32×32 frame, outside the top-left
            // resize handle (0±5px) and the origin crosshair centred at (16,16) ± 8px arm.
            ctx.SelectedState.SelectedFrame = frameRed;
            ctrl.RefreshAll();
            ctrl.SetCamera(0f, 0f, 1f);
            using var bmRed = ctrl.RenderToBitmap(64, 64);
            var redPx = bmRed.GetPixel(6, 6);

            // Select green frame
            ctx.SelectedState.SelectedFrame = frameGreen;
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
            ctx.SelectedState.SelectedFrame  = null;
            ctx.SelectedState.SelectedChain  = null;
            ctx.ProjectManager.FileName      = string.Empty;
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
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            // Single 64×64 texture shared by both frames
            var png = WriteSolidPng(dir, "sheet.png", SKColors.Blue, size: 64);
            ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

            // Frame 1: left half (UV 0.0–0.5)
            var frame1 = new AnimationFrameSave
            {
                TextureName      = "sheet.png", FrameLength = 0.1f,
                LeftCoordinate   = 0f,  TopCoordinate    = 0f,
                RightCoordinate  = 0.5f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave(),
            };
            // Frame 2: right half (UV 0.5–1.0)
            var frame2 = new AnimationFrameSave
            {
                TextureName      = "sheet.png", FrameLength = 0.1f,
                LeftCoordinate   = 0.5f, TopCoordinate    = 0f,
                RightCoordinate  = 1f,   BottomCoordinate = 1f,
                ShapesSave = new ShapesSave(),
            };
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frame1);
            chain.Frames.Add(frame2);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var ctrl = ctx.CreateWireframeControl();

            // Select frame 1 and load
            ctx.SelectedState.SelectedFrame = frame1;
            ctrl.RefreshAll();
            ctrl.SetCamera(0f, 0f, 1f);

            var rectsAfterFrame1 = ctrl.GetFrameRects();
            Assert.Single(rectsAfterFrame1);
            var sel1 = rectsAfterFrame1[0];
            Assert.True(sel1.IsSelected, "Frame 1 should be selected");
            Assert.Equal(0f,  sel1.Bounds.Left,  1f);
            Assert.Equal(32f, sel1.Bounds.Right, 1f);   // 0.5 * 64 = 32

            // Now select frame 2 (same texture!) and refresh
            ctx.SelectedState.SelectedFrame = frame2;
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
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            ctx.ProjectManager.FileName     = string.Empty;
            System.IO.Directory.Delete(dir, true);
        }
    }

    // ── Empty chain borrows a texture so Ctrl+click can add frames (issue #618) ─

    /// <summary>
    /// Issue #618: when the selected chain has no frames of its own, the wireframe must
    /// borrow the first texture referenced by any other chain so the user has something to
    /// Ctrl+click on to seed the first frame. Without this, an empty chain resolves to no
    /// texture and <see cref="WireframeControl.RefreshAll"/> clears the wireframe, making
    /// Ctrl+click-to-add a silent no-op.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_RefreshAll_EmptySelectedChain_BorrowsTextureFromOtherChain()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            WriteSolidPng(dir, "hero.png", SKColors.Magenta, size: 32);
            ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

            var textured = new AnimationChainSave { Name = "Walk" };
            textured.Frames.Add(new AnimationFrameSave
            {
                TextureName = "hero.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave(),
            });
            var empty = new AnimationChainSave { Name = "NewChain" };   // no frames
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(textured);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(empty);

            // Select the empty chain — nothing in it references a texture.
            ctx.SelectedState.SelectedChain = empty;

            var ctrl = ctx.CreateWireframeControl();
            ctrl.RefreshAll();

            Assert.NotNull(ctrl.LoadedTexturePath);
            Assert.True(ctrl.LoadedTexturePath!.EndsWith("hero.png", System.StringComparison.OrdinalIgnoreCase),
                $"Empty chain should borrow hero.png; got: {ctrl.LoadedTexturePath}");
        }
        finally
        {
            ctx.SelectedState.SelectedChain = null;
            ctx.ProjectManager.FileName     = string.Empty;
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
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, "tex.png", SKColors.Cyan);
            var ctrl = ctx.CreateWireframeControl();

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
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, "sprite.png", SKColors.Yellow, size: 48);
            var ctrl = ctx.CreateWireframeControl();

            Assert.Equal((0, 0), ctrl.BitmapSize);

            ctrl.LoadTexture(png);

            Assert.Equal((48, 48), ctrl.BitmapSize);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── LoadTexture(path, knownBitmap) -- browser-wasm support (#614) ─────────
    // The browser build has no filesystem to read a texture from; it already has every
    // dropped/picked PNG decoded via ThumbnailService. Mirrors ProjectManager's earlier
    // knownTextureSizes fix (#535) for the same "no disk in the browser" constraint.

    private static SKBitmap MakeSolidBitmap(SKColor color, int size = 16)
    {
        var bm = new SKBitmap(size, size);
        bm.Erase(color);
        return bm;
    }

    [AvaloniaFact]
    public void Wireframe_LoadTexture_WithKnownBitmap_DoesNotReadFromDisk()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreateWireframeControl();
        using var knownBitmap = MakeSolidBitmap(SKColors.Red, size: 32);

        // "sample/player.png" is never written to disk -- if this fell back to a disk read
        // it would fail (File.Exists is false) and report load failure.
        bool loaded = ctrl.LoadTexture("sample/player.png", knownBitmap);

        Assert.True(loaded);
        Assert.Equal((32, 32), ctrl.BitmapSize);
    }

    [AvaloniaFact]
    public void Wireframe_LoadTexture_WithKnownBitmap_RendersCorrectColor()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreateWireframeControl();
        using var knownBitmap = MakeSolidBitmap(SKColors.Red, size: 32);

        ctrl.LoadTexture("sample/player.png", knownBitmap);
        ctrl.SetCamera(0f, 0f, 1f);

        using var bm = ctrl.RenderToBitmap(64, 64);
        var px = bm.GetPixel(16, 16);
        Assert.True(px.Red > 150, $"Should render the known bitmap's red; R={px.Red}");
    }

    [AvaloniaFact]
    public void Wireframe_LoadTexture_WithKnownBitmap_DoesNotDisposeCallerOwnedBitmap()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var otherPng = WriteSolidPng(dir, "other.png", SKColors.Blue, size: 16);
            var ctrl = ctx.CreateWireframeControl();
            var knownBitmap = MakeSolidBitmap(SKColors.Red, size: 32); // not disposed by this test

            ctrl.LoadTexture("sample/player.png", knownBitmap);
            // Switch to a real disk-backed texture -- must not dispose the caller-owned bitmap
            // above, since ThumbnailService's cache (not this control) owns its lifetime.
            ctrl.LoadTexture(otherPng);

            var ex = Record.Exception(() => knownBitmap.GetPixel(0, 0));
            Assert.Null(ex);
            knownBitmap.Dispose(); // caller's responsibility, proving the control never touched it
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// End-to-end: RefreshAll() (the method SelectionChanged/AchxLoaded actually call) must
    /// resolve a ThumbnailService-seeded texture and render it, with no file on disk at all --
    /// the real shape of the browser-wasm case (#614), not just the LoadTexture unit-level check.
    /// </summary>
    [AvaloniaFact]
    public void Wireframe_RefreshAll_UsesThumbnailServiceSeededTexture_WithNoFileOnDisk()
    {
        var ctx = ResetSingletons();
        var frame = new AnimationFrameSave
        {
            TextureName = "seeded.png", FrameLength = 0.1f,
            LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
            ShapesSave = new ShapesSave(),
        };
        var chain = new AnimationChainSave { Name = "Test" };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        using var bitmap = MakeSolidBitmap(SKColors.Red, size: 32);
        ctx.ThumbnailService.SeedTexture("seeded.png", bitmap);

        var ctrl = new AnimationEditor.App.Controls.WireframeControl();
        ctrl.InitializeServices(
            ctx.SelectedState, ctx.AppState, ctx.AppCommands, ctx.ApplicationEvents,
            ctx.ProjectManager, ctx.UndoManager, ctx.PendingCutState, ctx.ObjectFinder,
            thumbnailService: ctx.ThumbnailService);

        ctrl.RefreshAll();
        ctrl.SetCamera(0f, 0f, 1f);

        using var bm = ctrl.RenderToBitmap(64, 64);
        var px = bm.GetPixel(16, 16);
        Assert.True(px.Red > 150, $"Should render the seeded texture; R={px.Red}");
    }
}
