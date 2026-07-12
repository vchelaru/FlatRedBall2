using AnimationEditor.Core.Demo;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class FeatureDemosTests
{
    [Fact]
    public void TryRun_UnknownName_ReturnsFalse()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var ran = FeatureDemos.TryRun(
            "does-not-exist", ctx.AppCommands, ctx.UndoManager, ctx.ApplicationEvents, "tex.png");

        Assert.False(ran);
        Assert.Empty(ctx.UndoManager.UndoHistory);
    }

    [Fact]
    public void TryRun_UndoLabels_RecordsCountAwareAndNamedEntries()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var ran = FeatureDemos.TryRun(
            FeatureDemos.UndoLabels, ctx.AppCommands, ctx.UndoManager, ctx.ApplicationEvents, "tex.png");

        Assert.True(ran);
        var labels = ctx.UndoManager.UndoHistory.Select(c => c.Description).ToList();
        Assert.Contains("Move 3 Frames Down", labels);
        Assert.Contains("Move Animation 'Idle' Down", labels);
        Assert.Contains("Delete Animation 'Doomed'", labels);
        Assert.Contains(labels, l => l.StartsWith("Move Rect 'Hitbox'"));
    }
}
