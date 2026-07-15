using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #718: hovering a frame region on the wireframe shows a "Frame N" label, matching the
/// tree view's 1-based naming (<see cref="AnimationEditor.Core.ViewModels.TreeBuilder.BuildFrameHeader"/>).
/// </summary>
public class WireframeHoverLabelTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        ctx.AppState.OffsetMultiplier             = 1f;
        return ctx;
    }

    private static string WriteSolidPng(string dir, SKColor color, int size = 100, string name = "sprite.png")
    {
        var path = System.IO.Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// Two-frame chain: frame A (0,0,20,20) is at index 0, frame B (60,60,80,80) is at index 1 —
    /// so hovering B must resolve to "Frame 2", not "Frame 1". Camera at (0,0,1) so texture
    /// pixels == screen pixels.
    /// </summary>
    private static (WireframeControl ctrl, string dir) BuildCtrlWithTwoFrames(TestServices ctx)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.DarkGray);

        var frameA = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.0f, TopCoordinate    = 0.0f,
            RightCoordinate  = 0.2f, BottomCoordinate = 0.2f,
            ShapesSave = new ShapesSave(),
        };
        var frameB = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.6f, TopCoordinate    = 0.6f,
            RightCoordinate  = 0.8f, BottomCoordinate = 0.8f,
            ShapesSave = new ShapesSave(),
        };

        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");
        ctx.SelectedState.SelectedChain = chain;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();

        return (ctrl, dir);
    }

    [AvaloniaFact]
    public void GetHoverLabelForScreenPoint_OverSecondFrame_ReturnsFrame2()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithTwoFrames(ctx);
        try
        {
            var label = ctrl.GetHoverLabelForScreenPoint(70f, 70f); // inside frame B (60,60,80,80)
            Assert.Equal("Frame 2", label);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void GetHoverLabelForScreenPoint_OutsideAnyFrame_ReturnsNull()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithTwoFrames(ctx);
        try
        {
            var label = ctrl.GetHoverLabelForScreenPoint(40f, 40f); // gap between the two frames
            Assert.Null(label);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Frame box sits with its left edge off-screen (negative X, e.g. panned so the box's
    /// left edge is at -30) — the tag must clamp to the canvas's left edge (0), not draw
    /// off-screen alongside it.
    /// </summary>
    [Fact]
    public void ComputeHoverTagRect_FrameLeftEdgeOffScreen_ClampsTagToLeftEdge()
    {
        var frameBounds = new SKRect(-30f, 50f, 10f, 90f);

        var tagRect = WireframeControl.ComputeHoverTagRect(frameBounds, tagWidth: 50f, tagHeight: 18f);

        Assert.Equal(0f, tagRect.Left);
    }

    /// <summary>
    /// Frame box sits with its top edge off-screen (negative Y) — same clamp, on the other axis,
    /// already covered informally by manual testing; asserted here so both edges are locked in.
    /// </summary>
    [Fact]
    public void ComputeHoverTagRect_FrameTopEdgeOffScreen_ClampsTagToTopEdge()
    {
        var frameBounds = new SKRect(50f, -30f, 90f, 10f);

        var tagRect = WireframeControl.ComputeHoverTagRect(frameBounds, tagWidth: 50f, tagHeight: 18f);

        Assert.Equal(0f, tagRect.Top);
    }

    /// <summary>
    /// Frame box fully on-screen: the tag anchors at the frame's top-left corner, growing
    /// upward, with no clamping applied.
    /// </summary>
    [Fact]
    public void ComputeHoverTagRect_FrameFullyOnScreen_AnchorsAtFrameTopLeft()
    {
        var frameBounds = new SKRect(50f, 50f, 90f, 90f);

        var tagRect = WireframeControl.ComputeHoverTagRect(frameBounds, tagWidth: 50f, tagHeight: 18f);

        Assert.Equal(50f, tagRect.Left);
        Assert.Equal(32f, tagRect.Top);
    }
}
