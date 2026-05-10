using System.Reflection;
using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// End-to-end MainWindow integration tests that validate the complete
/// user workflow from the AnimationEditor tutorial.
///
/// These tests use a headless <see cref="MainWindow"/> instance so that
/// the full event wiring between the controls (WireframeCtrl, PreviewCtrl)
/// and <see cref="AppCommands"/> / <see cref="ApplicationEvents"/> is active.
///
/// Covered tutorial steps:
/// • Grid snap-click → frame added to chain with correct relative TextureName and UV coords
///   (tests <c>OnFrameCreatedFromRegion</c> in MainWindow, which the tutorial depends on
///   for building SpriteSheet animations by clicking cells)
/// • Preview plays after chain is selected (chain → preview content changes)
/// • Save creates file on disk (serialization round-trip through AppCommands)
/// </summary>
public class TutorialMainWindowIntegrationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MainWindow CreateWindow()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        ProjectManager.Self.FileName               = null;
        SelectedState.Self.SelectedChain           = null;
        SelectedState.Self.SelectedFrame           = null;
        SelectedState.Self.SelectedNodes           = new System.Collections.Generic.List<object>();
        AppCommands.Self.ConfirmAsync              = (_, _) => Task.FromResult(true);
        AppCommands.Self.FileDialogService         = NullFileDialogService.Instance;

        var window = new MainWindow();
        window.Show();
        return window;
    }

    private static WireframeControl GetWireframe(MainWindow w)
        => w.FindControl<WireframeControl>("WireframeCtrl")
           ?? throw new InvalidOperationException("WireframeCtrl not found");

    private static string WriteSolidPng(string dir, string name, SKColor color, int size = 64)
    {
        var path = System.IO.Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    // ── SpriteSheet grid click → frame added to chain ────────────────────────

    /// <summary>
    /// Tutorial step: "you can select the cell of a frame by clicking on the
    /// appropriate cell" (SpriteSheet mode, grid on, Ctrl+click creates the frame).
    ///
    /// In the Avalonia implementation, <c>SimulateGridSnapClick</c> fires
    /// <c>FrameCreatedFromRegion</c>, which <c>OnFrameCreatedFromRegion</c>
    /// in MainWindow handles by adding a frame to the selected chain.
    ///
    /// Verifies:
    /// • Frame is added to the chain
    /// • <c>TextureName</c> is a relative path (not absolute)
    /// • UV coords match the grid cell that was clicked (cell at snap(20,16) = (16,16) with size=16)
    /// • <c>SelectedFrame</c> is set to the new frame
    /// </summary>
    [AvaloniaFact]
    public void GridSnapClick_WithTextureAndChain_AddsFrameWithCorrectUVAndRelativePath()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var window = CreateWindow();
        try
        {
            var png  = WriteSolidPng(dir, "sprite.png", SKColors.Red, size: 64);
            var achx = System.IO.Path.Combine(dir, "test.achx");
            ProjectManager.Self.FileName = achx;

            var chain = new AnimationChainSave { Name = "Idle" };
            ProjectManager.Self.AnimationChainListSave.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;

            var wireframe = GetWireframe(window);
            wireframe.LoadTexture(png);
            wireframe.SetCamera(0f, 0f, 1f); // texture px == screen px
            wireframe.SetGrid(true, 16);

            // Snap-click at screen (20, 20) → snaps to grid cell (16,16)→(32,32)
            wireframe.SimulateGridSnapClick(20f, 20f);

            Assert.Single(chain.Frames);
            var frame = chain.Frames[0];

            // TextureName must be relative, not an absolute path
            Assert.False(System.IO.Path.IsPathRooted(frame.TextureName),
                $"TextureName must be relative, got: '{frame.TextureName}'");
            Assert.True(frame.TextureName.EndsWith("sprite.png", StringComparison.OrdinalIgnoreCase),
                $"TextureName should end with 'sprite.png', got: '{frame.TextureName}'");

            // UV: pixel (16,16)→(32,32) on 64×64 → UV (0.25, 0.25, 0.5, 0.5)
            Assert.Equal(0.25f, frame.LeftCoordinate,   precision: 4);
            Assert.Equal(0.25f, frame.TopCoordinate,    precision: 4);
            Assert.Equal(0.5f,  frame.RightCoordinate,  precision: 4);
            Assert.Equal(0.5f,  frame.BottomCoordinate, precision: 4);

            // SelectedFrame must be set
            Assert.Same(frame, SelectedState.Self.SelectedFrame);
        }
        finally
        {
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            ProjectManager.Self.FileName     = string.Empty;
            window.Close();
            System.IO.Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Multiple grid snap-clicks add multiple frames, each with the snapped
    /// UV coords for the respective cell. The second frame replaces the
    /// SelectedFrame each time.
    /// </summary>
    [AvaloniaFact]
    public void GridSnapClick_MultipleClicks_AddsMultipleFramesWithDistinctUVs()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var window = CreateWindow();
        try
        {
            var png  = WriteSolidPng(dir, "sheet.png", SKColors.Blue, size: 64);
            var achx = System.IO.Path.Combine(dir, "test.achx");
            ProjectManager.Self.FileName = achx;

            var chain = new AnimationChainSave { Name = "Run" };
            ProjectManager.Self.AnimationChainListSave.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;

            var wireframe = GetWireframe(window);
            wireframe.LoadTexture(png);
            wireframe.SetCamera(0f, 0f, 1f);
            wireframe.SetGrid(true, 16);

            // Click cell (0,0)→(16,16)
            wireframe.SimulateGridSnapClick(8f, 8f);
            // Click cell (16,0)→(32,16)
            wireframe.SimulateGridSnapClick(24f, 8f);

            Assert.Equal(2, chain.Frames.Count);

            var f0 = chain.Frames[0];
            var f1 = chain.Frames[1];

            // Cell (0,0)→(16,16) on 64px: UV (0, 0, 0.25, 0.25)
            Assert.Equal(0f,    f0.LeftCoordinate,   precision: 4);
            Assert.Equal(0.25f, f0.RightCoordinate,  precision: 4);

            // Cell (16,0)→(32,16): UV (0.25, 0, 0.5, 0.25)
            Assert.Equal(0.25f, f1.LeftCoordinate,   precision: 4);
            Assert.Equal(0.5f,  f1.RightCoordinate,  precision: 4);
        }
        finally
        {
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            ProjectManager.Self.FileName     = string.Empty;
            window.Close();
            System.IO.Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Frames can be created even when no .achx file has been saved yet (unsaved project).
    /// In that case <c>TextureName</c> stores the absolute texture path (matching the
    /// drag-and-drop behaviour) so the wireframe can still resolve the texture for display.
    /// </summary>
    [AvaloniaFact]
    public void GridSnapClick_NoAchxFileName_FrameAddedWithAbsoluteTextureName()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var window = CreateWindow();
        try
        {
            var png = WriteSolidPng(dir, "tex.png", SKColors.Green, size: 32);
            // Deliberately do NOT set ProjectManager.Self.FileName

            var chain = new AnimationChainSave { Name = "Idle" };
            ProjectManager.Self.AnimationChainListSave.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;

            var wireframe = GetWireframe(window);
            wireframe.LoadTexture(png);
            wireframe.SetCamera(0f, 0f, 1f);
            wireframe.SetGrid(true, 16);

            wireframe.SimulateGridSnapClick(8f, 8f);

            // Frame must be added even without a saved project file;
            // TextureName is the absolute path so DetermineTexturePath can resolve it.
            Assert.Single(chain.Frames);
            Assert.Equal(new FlatRedBall.IO.FilePath(png).Standardized,
                         new FlatRedBall.IO.FilePath(chain.Frames[0].TextureName).Standardized);
        }
        finally
        {
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            window.Close();
            System.IO.Directory.Delete(dir, true);
        }
    }

    // ── PropPixelX change updates wireframe frame rect ───────────────────────

    /// <summary>
    /// Issue #106: changing PropPixelX must immediately shift the wireframe
    /// frame rectangle — without waiting for the async RefreshWireframe event.
    ///
    /// Before the fix, <c>ApplyFramePixelCoords</c> called
    /// <c>AppCommands.Self.RefreshWireframe()</c>, which queued
    /// <c>Dispatcher.UIThread.InvokeAsync(RefreshAll)</c>.  The frame rect in
    /// the wireframe was still at the old position immediately after the spinner
    /// changed (and never updated in headless tests without <c>RunJobs()</c>).
    ///
    /// After the fix, <c>WireframeCtrl.RefreshFrames()</c> is called directly, so
    /// <c>GetFrameRects()[0].Bounds.Left</c> reflects the new X synchronously.
    /// </summary>
    [AvaloniaFact]
    public void PropPixelX_ValueChanged_UpdatesWireframeFrameRect()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var window = CreateWindow();
        try
        {
            var png  = WriteSolidPng(dir, "sprite.png", SKColors.Red, size: 64);
            var achx = System.IO.Path.Combine(dir, "test.achx");
            ProjectManager.Self.FileName = achx;
            AppState.Self.UnitType = UnitType.Pixel;

            // Frame: pixel (0,0,16,16) on a 64×64 texture → UV (0, 0, 0.25, 0.25)
            var frame = new AnimationFrameSave
            {
                TextureName      = "sprite.png",
                FrameLength      = 0.1f,
                LeftCoordinate   = 0f,    TopCoordinate    = 0f,
                RightCoordinate  = 0.25f, BottomCoordinate = 0.25f,
                ShapeCollectionSave = new ShapeCollectionSave(),
            };
            var chain = new AnimationChainSave { Name = "Idle" };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave.AnimationChains.Add(chain);

            // Load texture BEFORE selecting the frame so BitmapSize is non-zero
            // when RefreshPropertyPanel runs and populates PropPixelX.
            var wireframe = GetWireframe(window);
            wireframe.LoadTexture(png);
            wireframe.SetCamera(0f, 0f, 1f);
            wireframe.RefreshFrames();

            SelectedState.Self.SelectedChain = chain;
            SelectedState.Self.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs(); // flush InvokeAsync(RefreshPropertyPanel)

            var propX = window.FindControl<NumericUpDown>("PropPixelX")
                        ?? throw new InvalidOperationException("PropPixelX not found");

            // Act: set X = 16  (moves frame from pixel column 0 to column 16)
            propX.Value = 16m;

            // Assert: wireframe frame rect updated SYNCHRONOUSLY — no RunJobs() needed.
            // Bounds are in texture-pixel space: Left should equal 16.
            var rects = wireframe.GetFrameRects();
            Assert.Single(rects);
            Assert.Equal(16f, rects[0].Bounds.Left, precision: 1);
        }
        finally
        {
            SelectedState.Self.SelectedFrame = null;
            SelectedState.Self.SelectedChain = null;
            ProjectManager.Self.FileName     = string.Empty;
            window.Close();
            System.IO.Directory.Delete(dir, true);
        }
    }

    // ── AppCommands.SaveAs creates file on disk ───────────────────────────────

    /// <summary>
    /// Tutorial step: "Save this file to your computer."
    /// Calling <c>AppCommands.Self.SaveAsAnimationChain(path)</c> must create
    /// an XML file at the given path that can be read back.
    ///
    /// This mirrors the tutorial requirement that a file must exist on disk
    /// before textures can be added (because texture paths are relative to the achx).
    /// </summary>
    [AvaloniaFact]
    public void SaveAs_CreatesAchxFileOnDisk()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var window = CreateWindow();
        try
        {
            var achx = System.IO.Path.Combine(dir, "animations.achx");

            // Create a chain with one frame
            var chain = new AnimationChainSave { Name = "Idle" };
            var frame = new AnimationFrameSave
            {
                TextureName = "idle.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave(),
            };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave.AnimationChains.Add(chain);

            ProjectManager.Self.FileName = achx;
            AppCommands.Self.SaveCurrentAnimationChainList(achx);

            Assert.True(System.IO.File.Exists(achx),
                $"SaveAs should create the file at: {achx}");

            var content = System.IO.File.ReadAllText(achx);
            Assert.Contains("Idle", content);
            Assert.Contains("idle.png", content);
        }
        finally
        {
            ProjectManager.Self.FileName = string.Empty;
            window.Close();
            System.IO.Directory.Delete(dir, true);
        }
    }

    // ── Preview shows content after chain selected ────────────────────────────

    /// <summary>
    /// Tutorial step: "Once you have added all frames, you can view the animation
    /// as it will play in your game by clicking on the animation itself."
    ///
    /// After selecting a chain, <c>PreviewControl.RenderToBitmap</c> must show
    /// non-background content (i.e., the frame texture is rendered).
    /// </summary>
    [AvaloniaFact]
    public void Preview_ChainSelected_RendersFrameContent()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var window = CreateWindow();
        try
        {
            var png  = WriteSolidPng(dir, "idle.png", SKColors.Lime, size: 16);
            var achx = System.IO.Path.Combine(dir, "test.achx");
            ProjectManager.Self.FileName = achx;

            var frame = new AnimationFrameSave
            {
                TextureName = "idle.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave(),
            };
            var chain = new AnimationChainSave { Name = "Idle" };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave.AnimationChains.Add(chain);

            // Select chain → preview should start playing
            SelectedState.Self.SelectedChain = chain;

            var preview = window.FindControl<PreviewControl>("PreviewCtrl")
                          ?? throw new InvalidOperationException("PreviewCtrl not found");

            preview.PauseAutoPlayback();
            preview.Playback.SetChain(chain);
            preview.Playback.Play();

            using var bm = preview.RenderToBitmap(64, 64);

            // The background is (30,30,30). Lime green has high green channel.
            // Any pixel significantly brighter than the background means the frame rendered.
            bool anyBright = false;
            for (int x = 0; x < 64 && !anyBright; x++)
                for (int y = 0; y < 64 && !anyBright; y++)
                {
                    var px = bm.GetPixel(x, y);
                    anyBright = px.Green > 100;
                }

            Assert.True(anyBright,
                "Preview should show frame content after chain is selected");
        }
        finally
        {
            SelectedState.Self.SelectedFrame  = null;
            SelectedState.Self.SelectedChain  = null;
            ProjectManager.Self.FileName      = string.Empty;
            window.Close();
            System.IO.Directory.Delete(dir, true);
        }
    }
}
