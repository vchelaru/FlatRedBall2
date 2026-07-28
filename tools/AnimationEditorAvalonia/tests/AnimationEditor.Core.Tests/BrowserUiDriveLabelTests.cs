using System.Linq;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// #690 A2: expected History labels for the Browser UI-drive smoke path
/// (Add Animation → Add Frame → open History). Kept in Core so Playwright asserts the same
/// strings the UndoManager actually stores — not hand-seeded UI models.
/// </summary>
public class BrowserUiDriveLabelTests
{
    [Fact]
    public void SmokePath_AddAnimationThenAddFrame_ProducesAssertableDescriptions()
    {
        var expectedAddAnimation = "Add Animation 'NewAnimation'";
        var expectedAddFrame = "Add Frame to 'ColorCycle'";

        var ctx = TestHelpers.SetupFreshAcls();
        var colorCycle = TestHelpers.MakeChain(ctx.Acls, "ColorCycle", 1);
        ctx.SelectedState.SelectedChain = colorCycle;

        ctx.AppCommands.AddFrame(colorCycle);
        ctx.AppCommands.AddNewAnimationChain();

        var descriptions = ctx.UndoManager.UndoHistory.Select(c => c.Description).ToList();
        Assert.Contains(expectedAddFrame, descriptions);
        Assert.Contains(expectedAddAnimation, descriptions);
    }
}
