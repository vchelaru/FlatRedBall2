using AnimationEditor.App.Controls;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression guard for #321: free panning in the Preview panel must not allow
/// the entity origin to drift permanently off-screen.
/// </summary>
public class PreviewPanLimitTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private const float ViewW = 400f - 20f; // 380
    private const float ViewH = 300f - 20f; // 280
    private static float MaxPanX => ViewW / 2f;   //  190
    private static float MinPanX => -(ViewW / 2f); // -190
    private static float MaxPanY => ViewH / 2f;   //  140
    private static float MinPanY => -(ViewH / 2f); // -140

    /// <summary>
    /// Regression guard for #321: zooming repeatedly toward the far-right edge
    /// drives _panX negative without limit; ClampPan must stop it at the floor.
    /// </summary>
    [AvaloniaFact]
    public void WheelZoom_TowardFarRight_ClampsPanX()
    {
        var ctx  = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));

        for (int i = 0; i < 10; i++)
            ctrl.SimulateWheelZoom(370, 150, zoomIn: true);

        Assert.True(ctrl.PanOffset.X >= MinPanX,
            $"PanX={ctrl.PanOffset.X:F1} went below clamp floor {MinPanX:F1} (#321)");
    }

    /// <summary>
    /// Regression guard for #321 (left direction): zooming repeatedly toward the
    /// far-left edge drives _panX positive without limit; ClampPan must cap it.
    /// </summary>
    [AvaloniaFact]
    public void WheelZoom_TowardFarLeft_ClampsPanX()
    {
        var ctx  = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));

        for (int i = 0; i < 10; i++)
            ctrl.SimulateWheelZoom(30, 150, zoomIn: true);

        Assert.True(ctrl.PanOffset.X <= MaxPanX,
            $"PanX={ctrl.PanOffset.X:F1} exceeded clamp ceiling {MaxPanX:F1} (#321)");
    }

    /// <summary>
    /// SetPan must preserve exact values (relied on by CenterOnEntityPoint) —
    /// it must NOT apply clamping.
    /// </summary>
    [AvaloniaFact]
    public void SetPan_DoesNotClamp_ExactValuePreserved()
    {
        var ctx  = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));

        ctrl.SetPan(9999f, -9999f);

        Assert.Equal(9999f,  ctrl.PanOffset.X);
        Assert.Equal(-9999f, ctrl.PanOffset.Y);
    }
}
