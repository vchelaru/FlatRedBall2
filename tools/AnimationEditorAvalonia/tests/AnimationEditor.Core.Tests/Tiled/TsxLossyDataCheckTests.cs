using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// Fresh-eyes pass #17 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): a Tiled tile
/// animation holds only a rect sequence and durations. Everything else an achx frame can carry
/// (flips, offsets, color, collision shapes) and a non-looping chain are dropped on a native-tsx
/// save without a word -- and several UI paths still let a user create them in a tsx project
/// (frame context menu "Add Rectangle"/"Add Circle", "Duplicate flipped", the Loop toggle, paste).
/// </summary>
public class TsxLossyDataCheckTests
{
    private static AnimationChainListSave WithChain(AnimationChainSave chain)
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        return acls;
    }

    private static AnimationChainSave CleanChain(string name = "Walk")
    {
        var chain = new AnimationChainSave { Name = name };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f, RightCoordinate = 0.25f, BottomCoordinate = 0.25f });
        return chain;
    }

    [Fact]
    public void Warnings_CleanChain_ReturnsNone()
    {
        Assert.Empty(TsxLossyDataCheck.Warnings(WithChain(CleanChain())));
    }

    public static TheoryData<string, Action<AnimationFrameSave>, string> FrameEdits => new()
    {
        { "flip horizontal", f => f.FlipHorizontal = true, "flip" },
        { "flip vertical", f => f.FlipVertical = true, "flip" },
        { "flip diagonal", f => f.FlipDiagonal = true, "flip" },
        { "relative x", f => f.RelativeX = 3f, "offset" },
        { "relative y", f => f.RelativeY = -2f, "offset" },
        { "red", f => f.Red = 255, "color" },
        { "alpha", f => f.Alpha = 128, "color" },
        { "color operation", f => f.ColorOperation = FlatRedBall2.Animation.ColorOperation.Add, "color" },
        { "rectangle shape", f => { f.ShapesSave = new ShapesSave(); f.ShapesSave.Shapes.Add(new AARectSave { Name = "Hit" }); }, "shape" },
        { "circle shape", f => { f.ShapesSave = new ShapesSave(); f.ShapesSave.Shapes.Add(new CircleSave { Name = "Hurt" }); }, "shape" },
    };

    [Theory]
    [MemberData(nameof(FrameEdits))]
    public void Warnings_FrameCarriesAchxOnlyData_NamesChainAndKind(string _, Action<AnimationFrameSave> edit, string expectedKind)
    {
        var chain = CleanChain("Walk");
        edit(chain.Frames[0]);

        var warning = Assert.Single(TsxLossyDataCheck.Warnings(WithChain(chain)));

        Assert.Contains("\"Walk\"", warning);
        Assert.Contains(expectedKind, warning);
    }

    [Fact]
    public void Warnings_EmptyShapesSave_ReturnsNone()
    {
        var chain = CleanChain();
        chain.Frames[0].ShapesSave = new ShapesSave();

        Assert.Empty(TsxLossyDataCheck.Warnings(WithChain(chain)));
    }

    [Fact]
    public void Warnings_NonLoopingChain_NamesChainAndLoop()
    {
        var chain = CleanChain("Die");
        chain.Loop = false;

        var warning = Assert.Single(TsxLossyDataCheck.Warnings(WithChain(chain)));

        Assert.Contains("\"Die\"", warning);
        Assert.Contains("loop", warning);
    }

    // One warning per chain, however many frames or kinds of data it carries.
    [Fact]
    public void Warnings_SeveralKindsAcrossFrames_OneWarningPerChain()
    {
        var chain = CleanChain("Walk");
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f, RightCoordinate = 0.25f, BottomCoordinate = 0.25f });
        chain.Frames[0].FlipHorizontal = true;
        chain.Frames[1].RelativeX = 1f;
        chain.Loop = false;

        var warning = Assert.Single(TsxLossyDataCheck.Warnings(WithChain(chain)));

        Assert.Contains("flip", warning);
        Assert.Contains("offset", warning);
        Assert.Contains("loop", warning);
    }
}
