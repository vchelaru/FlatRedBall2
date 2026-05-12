using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall.Content.AnimationChain;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests that right-clicking near a guide in <see cref="PreviewControl"/>
/// removes it.
///
/// The control is arranged to 64×64 before each test so that
/// <see cref="Control.Bounds"/> matches the explicit size passed to
/// <see cref="PreviewControl.RenderToBitmap"/>, keeping hit-test and
/// render coordinate systems consistent.
///
/// With Bounds 64×64 and default pan/zoom:
///   CenterX = (64-20)/2 + 20 = 42
///   CenterY = (64-20)/2 + 20 = 42
/// A guide at world position 0 maps to screen position 42 on that axis.
/// </summary>
public class GuideRightClickRemoveTests
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

    private static PreviewControl MakeArrangedControl()
    {
        var ctrl = new PreviewControl();
        ctrl.ShowGuides = true;
        // Arrange to a known size so GetCenterX/Y match the 64×64 render size.
        ctrl.Measure(new Size(64, 64));
        ctrl.Arrange(new Rect(0, 0, 64, 64));
        return ctrl;
    }

    // ── Horizontal guide removed ──────────────────────────────────────────────

    /// <summary>
    /// Right-clicking on a horizontal guide (world Y=0 → screen Y=42)
    /// must remove it from the guide list.
    /// </summary>
    [AvaloniaFact]
    public void SimulateRightClick_NearHGuide_HGuideIsRemoved()
    {
        ResetSingletons();
        var ctrl = MakeArrangedControl();
        ctrl.AddHGuide(0f);    // world Y=0 → screen Y=42
        Assert.Equal(1, ctrl.HGuideCount);

        ctrl.SimulateRightClick(30f, 42f);  // within hit distance of screen Y=42

        Assert.Equal(0, ctrl.HGuideCount);
    }

    // ── No removal when click misses all guides ───────────────────────────────

    /// <summary>
    /// Right-clicking well away from any guide must leave the guide list unchanged.
    /// </summary>
    [AvaloniaFact]
    public void SimulateRightClick_AwayFromGuide_GuideIsRetained()
    {
        ResetSingletons();
        var ctrl = MakeArrangedControl();
        ctrl.AddHGuide(0f);    // world Y=0 → screen Y=42
        Assert.Equal(1, ctrl.HGuideCount);

        ctrl.SimulateRightClick(30f, 20f);  // screen Y=20, dist from guide = 22 > hit threshold

        Assert.Equal(1, ctrl.HGuideCount);
    }

    // ── Vertical guide removed ────────────────────────────────────────────────

    /// <summary>
    /// Right-clicking on a vertical guide (world X=0 → screen X=42)
    /// must remove it from the guide list.
    /// </summary>
    [AvaloniaFact]
    public void SimulateRightClick_NearVGuide_VGuideIsRemoved()
    {
        ResetSingletons();
        var ctrl = MakeArrangedControl();
        ctrl.AddVGuide(0f);    // world X=0 → screen X=42
        Assert.Equal(1, ctrl.VGuideCount);

        ctrl.SimulateRightClick(42f, 30f);  // within hit distance of screen X=42

        Assert.Equal(0, ctrl.VGuideCount);
    }
}
