using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests that user-created guides snap to the nearest pixel boundary (integer
/// world coordinate) when placed or dragged.
///
/// Coordinate system recap:
///   world X/Y = pixel offset from the animation origin.
///   At zoom=2, default pan (0,0), 64×64 control:
///     cx = cy = (64-20)/2 + 20 + 0 = 42
///     screenX/Y 43 → world = (43-42)/2 = 0.5 → snapped to 1
///     snapped guide renders at screen 42 + 1*2 = 44
///
/// User-created guide colour: SKColor(0, 200, 255, 200) — cyan.
/// After alpha-blending over the background (30,30,30):
///   Blue ≈ 206, Red ≈ 7  → assert Blue > Red to detect guide pixels.
/// </summary>
public class GuideSnapToPixelTests
{
    private static TestServices ResetSingletons() {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;
        ctx.SelectedState.SelectedFrame = null;
        ctx.SelectedState.SelectedNodes = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        ctx.AppState.OffsetMultiplier = 1f;
        return ctx;
    }

    // ── Horizontal guide (HGuide) world-Y storage ─────────────────────────────

    /// <summary>
    /// At zoom=2, screenY=43 maps to worldY=0.5; must snap to 1.
    /// </summary>
    [AvaloniaFact]
    public void HGuide_AtFractionalScreenY_SnapsToNearestPixel()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetZoomPercent(200); // zoom=2 produces easy-to-reason fractional worlds

        // centerY = (64-20)/2 + 20 = 42; (43-42)/2 = 0.5 → snapped to 1
        ctrl.SimulateAddHGuide(screenY: 43f, controlHeight: 64f);

        Assert.Single(ctrl.HGuides);
        Assert.Equal(1f, ctrl.HGuides[0]);
    }

    /// <summary>
    /// At zoom=2, screenY=44 maps to worldY=1.0 (exact integer); must store 1.
    /// </summary>
    [AvaloniaFact]
    public void HGuide_AtExactPixelScreenY_StoresIntegerUnchanged()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetZoomPercent(200);

        // (44-42)/2 = 1.0 — already integer
        ctrl.SimulateAddHGuide(screenY: 44f, controlHeight: 64f);

        Assert.Single(ctrl.HGuides);
        Assert.Equal(1f, ctrl.HGuides[0]);
    }

    /// <summary>
    /// Negative fractional: screenY=41 → worldY = (41-42)/2 = -0.5 → snapped to -1
    /// (MidpointRounding.AwayFromZero rounds half-integers away from zero).
    /// </summary>
    [AvaloniaFact]
    public void HGuide_AtNegativeFractionalScreenY_SnapsAwayFromZero()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetZoomPercent(200);

        // (41-42)/2 = -0.5 → snapped to -1 (AwayFromZero, not banker's 0)
        ctrl.SimulateAddHGuide(screenY: 41f, controlHeight: 64f);

        Assert.Single(ctrl.HGuides);
        Assert.Equal(-1f, ctrl.HGuides[0]);
    }

    // ── Vertical guide (VGuide) world-X storage ───────────────────────────────

    /// <summary>
    /// At zoom=2, screenX=43 maps to worldX=0.5; must snap to 1.
    /// </summary>
    [AvaloniaFact]
    public void VGuide_AtFractionalScreenX_SnapsToNearestPixel()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetZoomPercent(200);

        ctrl.SimulateAddVGuide(screenX: 43f, controlWidth: 64f);

        Assert.Single(ctrl.VGuides);
        Assert.Equal(1f, ctrl.VGuides[0]);
    }

    // ── Drag snapping ─────────────────────────────────────────────────────────

    /// <summary>
    /// Dragging an existing guide to a fractional screen position must also snap.
    /// </summary>
    [AvaloniaFact]
    public void HGuide_Drag_SnapsToNearestPixel()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetZoomPercent(200);

        // Place guide at integer world 0 (screenY=42 → worldY=0)
        ctrl.SimulateAddHGuide(screenY: 42f, controlHeight: 64f);
        Assert.Equal(0f, ctrl.HGuides[0]);

        // Drag to fractional position: screenY=43 → worldY=0.5 → snapped to 1
        ctrl.SimulateDragHGuide(idx: 0, screenY: 43f, controlHeight: 64f);

        Assert.Equal(1f, ctrl.HGuides[0]);
    }

    /// <summary>
    /// Dragging a vertical guide to a fractional screen position must snap.
    /// </summary>
    [AvaloniaFact]
    public void VGuide_Drag_SnapsToNearestPixel()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetZoomPercent(200);

        ctrl.SimulateAddVGuide(screenX: 42f, controlWidth: 64f);
        Assert.Equal(0f, ctrl.VGuides[0]);

        ctrl.SimulateDragVGuide(idx: 0, screenX: 43f, controlWidth: 64f);

        Assert.Equal(1f, ctrl.VGuides[0]);
    }

    // ── Rendering: guide appears at snapped pixel, not fractional position ─────

    /// <summary>
    /// A vertical guide placed at screenX=43 (worldX=0.5 unsnapped, 1 snapped) at
    /// zoom=2 must render at screen X=44 (cx 42 + 1*2), not X=43 (cx 42 + 0.5*2).
    /// Cyan user guides have Blue > Red after blending over the dark background.
    /// </summary>
    [AvaloniaFact]
    public void VGuide_SnappedWorldX1_RendersAtExpectedScreenPixel()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetZoomPercent(200);

        ctrl.SimulateAddVGuide(screenX: 43f, controlWidth: 64f);

        using var bm = ctrl.RenderToBitmap(64, 64);

        // Sample at y=25 to stay above the origin crosshair (cy=42)
        var atSnapped   = bm.GetPixel(44, 25);   // world=1 → screen 44  (should be cyan)
        var atUnsnapped = bm.GetPixel(43, 25);   // world=0.5 → screen 43 (must be background)

        Assert.True(atSnapped.Blue > atSnapped.Red,
            $"Guide should render at x=44 (snapped world=1); B={atSnapped.Blue} R={atSnapped.Red}");
        Assert.True(atUnsnapped.Blue <= atUnsnapped.Red + 10,
            $"x=43 (unsnapped) should be background; B={atUnsnapped.Blue} R={atUnsnapped.Red}");
    }

    /// <summary>
    /// A horizontal guide placed at screenY=43 (worldY=0.5 unsnapped, 1 snapped) at
    /// zoom=2 must render at screen Y=44, not Y=43.
    /// </summary>
    [AvaloniaFact]
    public void HGuide_SnappedWorldY1_RendersAtExpectedScreenPixel()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetZoomPercent(200);

        ctrl.SimulateAddHGuide(screenY: 43f, controlHeight: 64f);

        using var bm = ctrl.RenderToBitmap(64, 64);

        // Sample at x=25 to stay left of the origin crosshair (cx=42)
        var atSnapped   = bm.GetPixel(25, 44);
        var atUnsnapped = bm.GetPixel(25, 43);

        Assert.True(atSnapped.Blue > atSnapped.Red,
            $"Guide should render at y=44 (snapped world=1); B={atSnapped.Blue} R={atSnapped.Red}");
        Assert.True(atUnsnapped.Blue <= atUnsnapped.Red + 10,
            $"y=43 (unsnapped) should be background; B={atUnsnapped.Blue} R={atUnsnapped.Red}");
    }
}
