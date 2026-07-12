using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for <see cref="PreviewControl.ShowUserGuides"/> — the toggle that hides/shows
/// user-placed ruler guides (distinct from <see cref="PreviewControl.ShowOrigin"/>, which
/// controls the unrelated origin crosshair).
/// </summary>
public class PreviewControlShowUserGuidesTests
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

    private static PreviewControl MakeArrangedControl(TestServices ctx)
    {
        var ctrl = ctx.CreatePreviewControl();
        ctrl.Measure(new Size(64, 64));
        ctrl.Arrange(new Rect(0, 0, 64, 64));
        return ctrl;
    }

    [AvaloniaFact]
    public void GetGuideCursorAt_GuidesHidden_ReturnsNull()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeArrangedControl(ctx);
        ctrl.AddHGuide(0f);   // world Y=0 → screen Y=42
        ctrl.ShowUserGuides = false;

        var cursor = ctrl.GetGuideCursorAt(30f, 42f);

        Assert.Null(cursor);
    }

    [AvaloniaFact]
    public void GuidesChanged_OnAddHGuide_Fires()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        int fireCount = 0;
        ctrl.GuidesChanged += () => fireCount++;

        ctrl.AddHGuide(5f);

        Assert.Equal(1, fireCount);
    }

    [AvaloniaFact]
    public void ShowUserGuides_DefaultsToTrue()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();

        Assert.True(ctrl.ShowUserGuides);
    }

    /// <summary>
    /// A hidden guide must render identically to having no guide at all — not just
    /// "less visible" but fully absent from the canvas.
    /// </summary>
    [AvaloniaFact]
    public void ShowUserGuides_False_GuideLineNotRenderedInBitmap()
    {
        var ctx = ResetSingletons();
        var ctrlBaseline = ctx.CreatePreviewControl();
        using var bmBaseline = ctrlBaseline.RenderToBitmap(64, 64);

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SetGuides(hGuides: new[] { 0f }, vGuides: new[] { 0f });
        ctrl.ShowUserGuides = false;
        using var bmHidden = ctrl.RenderToBitmap(64, 64);

        bool allSame = true;
        for (int x = 0; x < 64 && allSame; x++)
            for (int y = 0; y < 64 && allSame; y++)
                allSame = bmBaseline.GetPixel(x, y) == bmHidden.GetPixel(x, y);

        Assert.True(allSame, "ShowUserGuides=false must render identically to having no guides at all");
    }

    /// <summary>
    /// Matches Photoshop: placing a new guide while existing guides are hidden reveals
    /// them all again, so the user never silently stacks up guides they can't see.
    /// </summary>
    [AvaloniaFact]
    public void ShowUserGuides_FalseThenAddHGuide_AutoRevealsGuides()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.ShowUserGuides = false;

        ctrl.AddHGuide(0f);

        Assert.True(ctrl.ShowUserGuides);
    }

    [AvaloniaFact]
    public void SimulateRightClick_GuidesHidden_DoesNotRemoveGuide()
    {
        var ctx = ResetSingletons();
        var ctrl = MakeArrangedControl(ctx);
        ctrl.AddHGuide(0f);   // world Y=0 → screen Y=42
        ctrl.ShowUserGuides = false;

        bool removed = ctrl.SimulateRightClick(30f, 42f);

        Assert.False(removed);
        Assert.Equal(1, ctrl.HGuideCount);
    }
}
