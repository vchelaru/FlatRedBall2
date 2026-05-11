using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that dragging a frame handle in the wireframe panel immediately
/// signals the preview panel to repaint — issue #129.
///
/// Bug: <see cref="MainWindow"/> subscribed to <see cref="WireframeControl.FrameLiveUpdated"/>
/// but only called <c>RefreshPropertyPanel()</c>. It did not call
/// <see cref="AppCommands.RefreshAnimationFrameDisplay"/>, so the preview
/// control was never told to repaint until the drag was released.
///
/// Fix: <c>OnFrameLiveUpdated</c> also calls
/// <see cref="AppCommands.RefreshAnimationFrameDisplay"/> so that
/// <see cref="AppCommands.RefreshAnimationFrameDisplayRequested"/> is raised
/// on every pointer-move during the drag.
/// </summary>
public class PreviewLiveUpdateTests
{
    private static MainWindow CreateWindow()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        ProjectManager.Self.FileName               = null;
        SelectedState.Self.SelectedChain           = null;
        SelectedState.Self.SelectedFrame           = null;
        SelectedState.Self.SelectedNodes           = new System.Collections.Generic.List<object>();
        AppCommands.Self.DoOnUiThread              = a => a();
        AppCommands.Self.FileDialogService         = NullFileDialogService.Instance;
        AppCommands.Self.ConfirmAsync              = (_, _) => Task.FromResult(true);

        var window = new MainWindow();
        window.Show();
        return window;
    }

    private static WireframeControl GetWireframe(MainWindow w)
        => w.FindControl<WireframeControl>("WireframeCtrl")
           ?? throw new InvalidOperationException("WireframeCtrl not found");

    private static string WriteSolidPng(string dir, SKColor color, int size = 64)
    {
        var path = System.IO.Path.Combine(dir, "sprite.png");
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    // ── Live drag → preview refresh requested ────────────────────────────────

    /// <summary>
    /// When the user drags a handle, <see cref="WireframeControl.FrameLiveUpdated"/>
    /// fires on every pointer-move. <see cref="MainWindow"/> must forward this to
    /// <see cref="AppCommands.RefreshAnimationFrameDisplay"/> so that
    /// <see cref="PreviewControl"/> (which listens to
    /// <see cref="AppCommands.RefreshAnimationFrameDisplayRequested"/>) gets a
    /// repaint request before the drag is released.
    /// </summary>
    [AvaloniaFact]
    public void HandleDrag_WhileDragging_RaisesRefreshAnimationFrameDisplayRequested()
    {
        var dir    = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var window = CreateWindow();
        try
        {
            var png  = WriteSolidPng(dir, SKColors.Gray);
            var achx = System.IO.Path.Combine(dir, "test.achx");
            ProjectManager.Self.FileName = achx;

            var chain = new AnimationChainSave { Name = "Test" };
            var frame = new AnimationFrameSave
            {
                TextureName      = "sprite.png",
                FrameLength      = 0.1f,
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave(),
            };
            chain.Frames.Add(frame);
            ProjectManager.Self.AnimationChainListSave!.AnimationChains.Add(chain);
            SelectedState.Self.SelectedChain = chain;
            SelectedState.Self.SelectedFrame = frame;

            var wireframe = GetWireframe(window);
            wireframe.LoadTexture(png);
            wireframe.SetCamera(0f, 0f, 1f);
            wireframe.RefreshFrames();
            Dispatcher.UIThread.RunJobs();

            bool refreshRequested = false;
            AppCommands.Self.RefreshAnimationFrameDisplayRequested += () => refreshRequested = true;

            // Simulate dragging the TopLeft handle 8 px inward — fires FrameLiveUpdated
            wireframe.SimulateHandleDrag(HandleKind.TopLeft,
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   8f, endScreenY:   8f);

            Assert.True(refreshRequested,
                "AppCommands.RefreshAnimationFrameDisplayRequested should be raised " +
                "during a handle drag so that PreviewControl repaints immediately.");
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
}
