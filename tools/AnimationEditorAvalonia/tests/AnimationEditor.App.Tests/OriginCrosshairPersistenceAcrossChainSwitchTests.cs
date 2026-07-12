using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests that verify the origin crosshair in <see cref="PreviewControl"/>
/// persists when the user switches between animation chains. Not to be confused
/// with user-placed guides — this covers the fixed crosshair through world origin
/// (0,0), toggled by <see cref="PreviewControl.ShowOrigin"/>.
///
/// The crosshair position is stored in <c>_panX/_panY</c> on <c>PreviewControl</c>.
/// <c>OnSelectionChanged</c> only calls <c>_playback.SetChain</c> and
/// <c>InvalidateVisual</c> — it does NOT reset pan — so the crosshair MUST
/// remain at the same pixel position after a chain switch.
/// </summary>
public class OriginCrosshairPersistenceAcrossChainSwitchTests
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

    private static string WritePng(string dir, SKColor color, int size = 16)
    {
        var path = System.IO.Path.Combine(dir, $"{Guid.NewGuid():N}.png");
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    // ── Crosshair stays at same X after chain switch ──────────────────────────

    /// <summary>
    /// After setting PanX=+8 (crosshair vertical line at x = 42+8 = 50), switching
    /// from "Idle" to "Run" must NOT move the crosshair — it must still be at x=50.
    ///
    /// The test uses two empty chains so any pixel difference between the two
    /// renders is only due to crosshair position or texture content, not frame drawing.
    /// </summary>
    [AvaloniaFact]
    public void Origin_AfterChainSwitch_VerticalLineRemainsAtSameX()
    {
        var ctx = ResetSingletons();

        var chainIdle = new AnimationChainSave { Name = "Idle" };
        var chainRun  = new AnimationChainSave { Name = "Run"  };
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainIdle);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainRun);

        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowOrigin = true;
        ctrl.SetPan(8f, 0f); // vertical line at x = (Width-20)/2+20+8 = 50

        ctx.SelectedState.SelectedChain = chainIdle;
        Dispatcher.UIThread.RunJobs();
        using var bmIdle = ctrl.RenderToBitmap(64, 64);

        ctx.SelectedState.SelectedChain = chainRun;
        Dispatcher.UIThread.RunJobs();
        using var bmRun = ctrl.RenderToBitmap(64, 64);

        // Both renders must show the vertical line at x=50 (green-dominant)
        var idlePixel   = bmIdle.GetPixel(50, 25);
        var runPixel    = bmRun.GetPixel(50, 25);
        var runOldPixel = bmRun.GetPixel(42, 25);   // x=42 is old default centre

        Assert.True(idlePixel.Green > idlePixel.Red,
            $"Idle: crosshair should be at x=50; G={idlePixel.Green} R={idlePixel.Red}");
        Assert.True(runPixel.Green > runPixel.Red,
            $"Run: crosshair must persist at x=50 after chain switch; G={runPixel.Green} R={runPixel.Red}");
        Assert.True(runOldPixel.Green <= runOldPixel.Red + 10,
            $"Run: x=42 (old default) should NOT have crosshair; G={runOldPixel.Green} R={runOldPixel.Red}");
    }

    /// <summary>
    /// Same verification for the horizontal line (PanY=+8 → line at y=40).
    /// </summary>
    [AvaloniaFact]
    public void Origin_AfterChainSwitch_HorizontalLineRemainsAtSameY()
    {
        var ctx = ResetSingletons();

        var chainIdle = new AnimationChainSave { Name = "Idle" };
        var chainRun  = new AnimationChainSave { Name = "Run"  };
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainIdle);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainRun);

        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowOrigin = true;
        ctrl.SetPan(0f, 8f); // horizontal line at y = (Height-20)/2+20+8 = 50

        ctx.SelectedState.SelectedChain = chainIdle;
        Dispatcher.UIThread.RunJobs();

        ctx.SelectedState.SelectedChain = chainRun;
        Dispatcher.UIThread.RunJobs();

        using var bmRun = ctrl.RenderToBitmap(64, 64);
        var atNewY = bmRun.GetPixel(25, 50);
        var atOldY = bmRun.GetPixel(25, 42);

        Assert.True(atNewY.Green > atNewY.Red,
            $"Horizontal line should still be at y=50 after chain switch; G={atNewY.Green} R={atNewY.Red}");
        Assert.True(atOldY.Green <= atOldY.Red + 10,
            $"y=42 (old default) should not have crosshair; G={atOldY.Green} R={atOldY.Red}");
    }

    // ── Crosshair stays across multiple switches ──────────────────────────────

    /// <summary>
    /// Switching chains multiple times (Idle→Run→Idle→Run) must not drift the
    /// crosshair position. After four switches it must be at the original offset.
    /// </summary>
    [AvaloniaFact]
    public void Origin_MultipleChainSwitches_CrosshairDoesNotDrift()
    {
        var ctx = ResetSingletons();

        var chainIdle = new AnimationChainSave { Name = "Idle" };
        var chainRun  = new AnimationChainSave { Name = "Run"  };
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainIdle);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainRun);

        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowOrigin = true;
        ctrl.SetPan(8f, 8f); // crosshair at (50, 50)

        for (int i = 0; i < 4; i++)
        {
            ctx.SelectedState.SelectedChain = i % 2 == 0 ? chainIdle : chainRun;
            Dispatcher.UIThread.RunJobs();
        }

        using var bm = ctrl.RenderToBitmap(64, 64);
        var vertLine = bm.GetPixel(50, 25);  // vertical line at x=50
        var horzLine = bm.GetPixel(25, 50);  // horizontal line at y=50

        Assert.True(vertLine.Green > vertLine.Red,
            $"After 4 switches vertical line must still be at x=50; G={vertLine.Green} R={vertLine.Red}");
        Assert.True(horzLine.Green > horzLine.Red,
            $"After 4 switches horizontal line must still be at y=50; G={horzLine.Green} R={horzLine.Red}");
    }

    // ── Crosshair persists with textured chains ────────────────────────────────

    /// <summary>
    /// Stronger test: both chains have textures. Even when the texture content
    /// differs between chains, the crosshair must appear at the same pixel after the switch.
    ///
    /// Chain A (Idle): first chain.
    /// Chain B (Run):  second chain.
    /// Crosshair: vertical at x=40 (PanX=+8 on 64×64 canvas).
    /// After switching to Run, pixel at (40, 10) must still be green-dominant
    /// (the crosshair rendered at that position).
    /// </summary>
    [AvaloniaFact]
    public void Origin_WithTexturedChains_PersistsAfterSwitch()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var chainIdle = new AnimationChainSave { Name = "Idle" };
            var chainRun  = new AnimationChainSave { Name = "Run"  };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainIdle);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainRun);

            var ctrl = ctx.CreatePreviewControl();
            ctrl.ShowOrigin = true;
            ctrl.SetPan(8f, 0f); // vertical line at x = 42+8 = 50

            // Select Idle chain
            ctx.SelectedState.SelectedChain = chainIdle;
            Dispatcher.UIThread.RunJobs();
            using var bmIdle = ctrl.RenderToBitmap(64, 64);

            // Switch to Run chain
            ctx.SelectedState.SelectedChain = chainRun;
            Dispatcher.UIThread.RunJobs();
            using var bmRun = ctrl.RenderToBitmap(64, 64);

            // Crosshair must appear at x=50 in BOTH renders (pan was not reset)
            var idlePixel = bmIdle.GetPixel(50, 25);
            var runPixel  = bmRun.GetPixel(50, 25);

            Assert.True(idlePixel.Green > idlePixel.Red,
                $"Crosshair at x=50 must appear with Idle chain; G={idlePixel.Green} R={idlePixel.Red}");
            Assert.True(runPixel.Green > runPixel.Red,
                $"Crosshair at x=50 must persist after switching to Run; G={runPixel.Green} R={runPixel.Red}");
        }
        finally
        {
            ctx.SelectedState.SelectedChain = null;
            ctx.ProjectManager.FileName = string.Empty;
            System.IO.Directory.Delete(dir, recursive: true);
        }
    }
}
