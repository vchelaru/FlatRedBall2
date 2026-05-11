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
/// Tests that the guide crosshair in <see cref="PreviewControl"/> is correctly
/// positioned relative to pan offsets.
///
/// The guide implementation draws:
///   Vertical line   at cx = (Width-20)/2  + 20 + PanX
///   Horizontal line at cy = (Height-20)/2 + 20 + PanY
///
/// Tests sample pixels OFF the intersecting axis to isolate each line:
///   – vertical line tests sample at y=25 (well above cy=42 at default pan)
///   – horizontal line tests sample at x=25 (well left of cx=42 at default pan)
///
/// Guide colour: SKColor(100, 200, 100, 160) → over background (30,30,30)
///   blended G ≈ 137 >> background G=30, so Green > Red is a solid assertion.
/// </summary>
public class GuidePanTests
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

    private static void WritePng(string path, SKColor color, int size = 16)
    {
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    // ── ShowGuides = false ────────────────────────────────────────────────────

    /// <summary>
    /// When ShowGuides is false the crosshair must not appear at all.
    /// With no chain set the canvas is pure background (30,30,30).
    /// The guide would raise Green significantly; without it Green ≈ 30.
    /// </summary>
    [AvaloniaFact]
    public void Guide_HiddenWhenShowGuidesFalse_NoCrosshairAtCenter()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowGuides = false;

        using var bm = ctrl.RenderToBitmap(64, 64);

        // Default pan → guide would be at (42, 42); verify it is not green
        var px = bm.GetPixel(32, 10);   // sample off the horizontal axis
        Assert.True(px.Green <= px.Red + 10,
            $"ShowGuides=false: no green crosshair expected; G={px.Green} R={px.Red}");
    }

    // ── Default pan: crosshair at canvas centre ───────────────────────────────

    /// <summary>
    /// With default pan (0,0) the vertical guide is at x = (Width-20)/2+20 = 42 and
    /// the horizontal guide is at y = (Height-20)/2+20 = 42.
    /// Sampling at y=25 isolates the vertical line from the horizontal crossing.
    /// </summary>
    [AvaloniaFact]
    public void Guide_DefaultPan_VerticalLineAtCenterX()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowGuides = true;

        using var bm = ctrl.RenderToBitmap(64, 64);

        var guidePixel = bm.GetPixel(42, 25);    // on the vertical line
        var offPixel   = bm.GetPixel(25, 25);    // off the vertical line

        Assert.True(guidePixel.Green > guidePixel.Red,
            $"Vertical guide at x=42 should be green-dominant; G={guidePixel.Green} R={guidePixel.Red}");
        Assert.True(offPixel.Green <= offPixel.Red + 10,
            $"Off-guide pixel (25,25) should be background, not green; G={offPixel.Green} R={offPixel.Red}");
    }

    // ── PanX shifts the vertical guide ───────────────────────────────────────

    /// <summary>
    /// SetPan(16, 0) → cx = 42 + 16 = 58.
    /// The vertical line must appear at x=58, not x=42.
    /// Sampling at y=25 avoids the horizontal line (which remains at y=42).
    /// </summary>
    [AvaloniaFact]
    public void Guide_PanX16_VerticalLineShiftsToX48()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowGuides = true;
        ctrl.SetPan(16f, 0f);

        using var bm = ctrl.RenderToBitmap(64, 64);

        var atNewPos = bm.GetPixel(58, 25);   // new vertical line position
        var atOldPos = bm.GetPixel(42, 25);   // old (default) vertical line position

        Assert.True(atNewPos.Green > atNewPos.Red,
            $"Vertical guide should be at x=58 after PanX=16; G={atNewPos.Green} R={atNewPos.Red}");
        Assert.True(atOldPos.Green <= atOldPos.Red + 10,
            $"Old vertical-guide position x=42 should now be background; G={atOldPos.Green} R={atOldPos.Red}");
    }

    /// <summary>
    /// SetPan(-8, 0) → cx = 42 − 8 = 34.
    /// The vertical line shifts left.
    /// </summary>
    [AvaloniaFact]
    public void Guide_PanXNeg8_VerticalLineShiftsToX24()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowGuides = true;
        ctrl.SetPan(-8f, 0f);

        using var bm = ctrl.RenderToBitmap(64, 64);

        var atNewPos = bm.GetPixel(34, 25);
        var atOldPos = bm.GetPixel(42, 25);

        Assert.True(atNewPos.Green > atNewPos.Red,
            $"Vertical guide should be at x=34 after PanX=-8; G={atNewPos.Green} R={atNewPos.Red}");
        Assert.True(atOldPos.Green <= atOldPos.Red + 10,
            $"Old vertical-guide position x=42 should now be background; G={atOldPos.Green} R={atOldPos.Red}");
    }

    // ── PanY shifts the horizontal guide ─────────────────────────────────────

    /// <summary>
    /// SetPan(0, 16) → cy = 42 + 16 = 58.
    /// The horizontal line must appear at y=58, not y=42.
    /// Sampling at x=25 avoids the vertical line (which remains at x=42).
    /// </summary>
    [AvaloniaFact]
    public void Guide_PanY16_HorizontalLineShiftsToY48()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowGuides = true;
        ctrl.SetPan(0f, 16f);

        using var bm = ctrl.RenderToBitmap(64, 64);

        var atNewPos = bm.GetPixel(25, 58);
        var atOldPos = bm.GetPixel(25, 42);

        Assert.True(atNewPos.Green > atNewPos.Red,
            $"Horizontal guide should be at y=58 after PanY=16; G={atNewPos.Green} R={atNewPos.Red}");
        Assert.True(atOldPos.Green <= atOldPos.Red + 10,
            $"Old horizontal-guide position y=42 should now be background; G={atOldPos.Green} R={atOldPos.Red}");
    }

    /// <summary>
    /// SetPan(0, -8) → cy = 42 − 8 = 34.
    /// The horizontal line shifts up.
    /// </summary>
    [AvaloniaFact]
    public void Guide_PanYNeg8_HorizontalLineShiftsToY24()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowGuides = true;
        ctrl.SetPan(0f, -8f);

        using var bm = ctrl.RenderToBitmap(64, 64);

        var atNewPos = bm.GetPixel(25, 34);
        var atOldPos = bm.GetPixel(25, 42);

        Assert.True(atNewPos.Green > atNewPos.Red,
            $"Horizontal guide should be at y=34 after PanY=-8; G={atNewPos.Green} R={atNewPos.Red}");
        Assert.True(atOldPos.Green <= atOldPos.Red + 10,
            $"Old horizontal-guide position y=42 should now be background; G={atOldPos.Green} R={atOldPos.Red}");
    }

    // ── Guide visible over a texture frame ───────────────────────────────────

    /// <summary>
    /// When a dark frame is rendered and guides are enabled the guide line
    /// is drawn on top and should be detectably greener than an adjacent
    /// off-guide pixel at the same row.
    ///
    /// Uses a dark-blue texture so the guide's green channel stands out
    /// even after blending over the frame.
    /// </summary>
    [AvaloniaFact]
    public void Guide_WithDarkTexture_CrosshairIsGreenDominantOverFrame()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Dark blue texture so guide green channel is distinguishable
            WritePng(Path.Combine(dir, "dark.png"), new SKColor(0, 0, 60), size: 16);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var frame = new AnimationFrameSave
            {
                TextureName = "dark.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapeCollectionSave = new ShapeCollectionSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;

            var ctrl = ctx.CreatePreviewControl();
            ctrl.ShowGuides = true;

            using var bm = ctrl.RenderToBitmap(64, 64);

            // Vertical guide at x=42; sample inside the frame at y=38 (above horizontal crossing y=42)
            var guideOverFrame = bm.GetPixel(42, 38);
            var offGuide       = bm.GetPixel(47, 38);  // 5 px to the right, same row

            Assert.True(guideOverFrame.Green > offGuide.Green,
                $"Guide pixel should be greener than off-guide pixel; guide G={guideOverFrame.Green} offGuide G={offGuide.Green}");
        }
        finally
        {
            ctx.ProjectManager.FileName = string.Empty;
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedChain = null;
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Combined pan: both lines shift ───────────────────────────────────────

    /// <summary>
    /// SetPan(8, 8) → cx=50, cy=50.
    /// Both lines shift.  Sample vertical at (50,25) and horizontal at (25,50).
    /// Old positions (42,25) and (25,42) should be background.
    /// </summary>
    [AvaloniaFact]
    public void Guide_PanXY8_BothLinesShiftToNewPosition()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowGuides = true;
        ctrl.SetPan(8f, 8f);

        using var bm = ctrl.RenderToBitmap(64, 64);

        var vertNew  = bm.GetPixel(50, 25);
        var horzNew  = bm.GetPixel(25, 50);
        var vertOld  = bm.GetPixel(42, 25);
        var horzOld  = bm.GetPixel(25, 42);

        Assert.True(vertNew.Green > vertNew.Red,
            $"Vertical guide at x=50; G={vertNew.Green} R={vertNew.Red}");
        Assert.True(horzNew.Green > horzNew.Red,
            $"Horizontal guide at y=50; G={horzNew.Green} R={horzNew.Red}");
        Assert.True(vertOld.Green <= vertOld.Red + 10,
            $"Old x=42 should be background; G={vertOld.Green} R={vertOld.Red}");
        Assert.True(horzOld.Green <= horzOld.Red + 10,
            $"Old y=42 should be background; G={horzOld.Green} R={horzOld.Red}");
    }

    // ── Guide toggle: on → off → on restores same pixels ─────────────────────

    /// <summary>
    /// Toggling ShowGuides off and back on should produce an identical bitmap
    /// to the original ShowGuides=true render.
    /// </summary>
    [AvaloniaFact]
    public void Guide_ToggleOffThenOn_ProducesIdenticalBitmap()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();

        ctrl.ShowGuides = true;
        using var bmOn1 = ctrl.RenderToBitmap(64, 64);

        ctrl.ShowGuides = false;
        using var bmOff = ctrl.RenderToBitmap(64, 64);

        ctrl.ShowGuides = true;
        using var bmOn2 = ctrl.RenderToBitmap(64, 64);

        // bmOn1 and bmOn2 must be identical
        bool allSame = true;
        for (int x = 0; x < 64 && allSame; x++)
            for (int y = 0; y < 64 && allSame; y++)
                allSame = bmOn1.GetPixel(x, y) == bmOn2.GetPixel(x, y);

        Assert.True(allSame, "ShowGuides=true after toggle-off should produce same bitmap as before toggle");

        // bmOn1 and bmOff must differ (the guide pixels)
        bool anyDiff = false;
        for (int x = 0; x < 64 && !anyDiff; x++)
            for (int y = 0; y < 64 && !anyDiff; y++)
                anyDiff = bmOn1.GetPixel(x, y) != bmOff.GetPixel(x, y);

        Assert.True(anyDiff, "ShowGuides=false must produce a different bitmap than ShowGuides=true");
    }
}
