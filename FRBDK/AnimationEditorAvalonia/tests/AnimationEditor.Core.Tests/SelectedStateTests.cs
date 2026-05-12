using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class SelectedStateTests
{
    // ── SelectedChain ─────────────────────────────────────────────────────────

    [Fact]
    public void SelectedChain_WhenSet_ClearsSelectedFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 1);
        ctx.SelectedState.SelectedFrame = chain.Frames[0];

        // Setting chain should clear frame
        var otherChain = TestHelpers.MakeChain(acls, "Idle");
        ctx.SelectedState.SelectedChain = otherChain;

        Assert.Null(ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void SelectedChain_WhenSet_ClearsSelectedRectangle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Box" };
        frame.ShapesSave!.AARectSaves.Add(rect);
        ctx.SelectedState.SelectedRectangle = rect;

        var otherChain = TestHelpers.MakeChain(acls, "Idle");
        ctx.SelectedState.SelectedChain = otherChain;

        Assert.Null(ctx.SelectedState.SelectedRectangle);
    }

    [Fact]
    public void SelectedChain_WhenSet_ClearsSelectedCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 1);
        var frame = chain.Frames[0];
        var circle = new CircleSave { Name = "Ring", Radius = 5 };
        frame.ShapesSave!.CircleSaves.Add(circle);
        ctx.SelectedState.SelectedCircle = circle;

        var otherChain = TestHelpers.MakeChain(acls, "Idle");
        ctx.SelectedState.SelectedChain = otherChain;

        Assert.Null(ctx.SelectedState.SelectedCircle);
    }

    [Fact]
    public void SelectedChain_WhenSet_FiresSelectionChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run");
        bool fired = false;
        ctx.SelectedState.SelectionChanged += () => fired = true;

        ctx.SelectedState.SelectedChain = chain;

        Assert.True(fired);
    }

    [Fact]
    public void SelectedChain_WhenSetToNull_StillFiresSelectionChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run");
        ctx.SelectedState.SelectedChain = chain;

        bool fired = false;
        ctx.SelectedState.SelectionChanged += () => fired = true;
        ctx.SelectedState.SelectedChain = null;

        Assert.True(fired);
    }

    // ── SelectedFrame ─────────────────────────────────────────────────────────

    [Fact]
    public void SelectedFrame_WhenSet_AutoFindsParentChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 2);
        var frame = chain.Frames[1];

        ctx.SelectedState.SelectedFrame = frame;

        Assert.Same(chain, ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public void SelectedFrame_WhenSet_ClearsSelectedRectangle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Box" };
        frame.ShapesSave!.AARectSaves.Add(rect);
        ctx.SelectedState.SelectedRectangle = rect;

        ctx.SelectedState.SelectedFrame = frame;

        Assert.Null(ctx.SelectedState.SelectedRectangle);
    }

    [Fact]
    public void SelectedFrame_WhenSet_ClearsSelectedCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        var frame = chain.Frames[0];
        var circle = new CircleSave { Name = "Ring", Radius = 5 };
        frame.ShapesSave!.CircleSaves.Add(circle);
        ctx.SelectedState.SelectedCircle = circle;

        ctx.SelectedState.SelectedFrame = frame;

        Assert.Null(ctx.SelectedState.SelectedCircle);
    }

    [Fact]
    public void SelectedFrame_WhenSet_FiresSelectionChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 1);
        bool fired = false;
        ctx.SelectedState.SelectionChanged += () => fired = true;

        ctx.SelectedState.SelectedFrame = chain.Frames[0];

        Assert.True(fired);
    }

    [Fact]
    public void SelectedFrame_WhenFrameNotInAnyChain_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var standaloneFrame = TestHelpers.MakeFrame();

        // Should not throw even if frame isn't in ACLS
        ctx.SelectedState.SelectedFrame = standaloneFrame;

        Assert.Same(standaloneFrame, ctx.SelectedState.SelectedFrame);
    }

    // ── SelectedRectangle / SelectedCircle mutual exclusion ───────────────────

    [Fact]
    public void SelectedRectangle_WhenSet_ClearsSelectedCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var circle = new CircleSave { Name = "Ring", Radius = 5 };
        ctx.SelectedState.SelectedCircle = circle;

        var rect = new AARectSave { Name = "Box" };
        ctx.SelectedState.SelectedRectangle = rect;

        Assert.Null(ctx.SelectedState.SelectedCircle);
        Assert.Same(rect, ctx.SelectedState.SelectedRectangle);
    }

    [Fact]
    public void SelectedCircle_WhenSet_ClearsSelectedRectangle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var rect = new AARectSave { Name = "Box" };
        ctx.SelectedState.SelectedRectangle = rect;

        var circle = new CircleSave { Name = "Ring", Radius = 5 };
        ctx.SelectedState.SelectedCircle = circle;

        Assert.Null(ctx.SelectedState.SelectedRectangle);
        Assert.Same(circle, ctx.SelectedState.SelectedCircle);
    }

    [Fact]
    public void SelectedRectangle_WhenSet_FiresSelectionChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        ctx.SelectedState.SelectionChanged += () => fired = true;
        var rect = new AARectSave { Name = "Box" };

        ctx.SelectedState.SelectedRectangle = rect;

        Assert.True(fired);
    }

    [Fact]
    public void SelectedCircle_WhenSet_FiresSelectionChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        ctx.SelectedState.SelectionChanged += () => fired = true;
        var circle = new CircleSave { Name = "Ring", Radius = 5 };

        ctx.SelectedState.SelectedCircle = circle;

        Assert.True(fired);
    }

    // ── SelectedShape ─────────────────────────────────────────────────────────

    [Fact]
    public void SelectedShape_WhenRectangleSelected_ReturnsRectangle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var rect = new AARectSave { Name = "Box" };
        ctx.SelectedState.SelectedRectangle = rect;

        Assert.Same(rect, ctx.SelectedState.SelectedShape);
    }

    [Fact]
    public void SelectedShape_WhenCircleSelected_ReturnsCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var circle = new CircleSave { Name = "Ring", Radius = 5 };
        ctx.SelectedState.SelectedCircle = circle;

        Assert.Same(circle, ctx.SelectedState.SelectedShape);
    }

    [Fact]
    public void SelectedShape_WhenNothingSelected_ReturnsNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        Assert.Null(ctx.SelectedState.SelectedShape);
    }

    // ── SelectedTextureName ───────────────────────────────────────────────────

    [Fact]
    public void SelectedTextureName_WhenFrameSelectedWithTexture_ReturnsFrameTextureName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 1);
        chain.Frames[0].TextureName = "hero_run.png";
        ctx.SelectedState.SelectedFrame = chain.Frames[0];

        Assert.Equal("hero_run.png", ctx.SelectedState.SelectedTextureName);
    }

    [Fact]
    public void SelectedTextureName_WhenChainSelectedAndHasFrames_ReturnsFirstFrameTexture()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 2);
        chain.Frames[0].TextureName = "sheet.png";
        chain.Frames[1].TextureName = "other.png";
        ctx.SelectedState.SelectedChain = chain;

        Assert.Equal("sheet.png", ctx.SelectedState.SelectedTextureName);
    }

    [Fact]
    public void SelectedTextureName_WhenNothingSelected_ReturnsNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        Assert.Null(ctx.SelectedState.SelectedTextureName);
    }

    // ── SelectedFrames / multi-select ─────────────────────────────────────────

    [Fact]
    public void SelectedFrames_WhenSelectedNodesContainFrames_ReturnsThoseFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        var f0 = chain.Frames[0];
        var f2 = chain.Frames[2];
        ctx.SelectedState.SelectedNodes = new List<object> { f0, f2 };

        var frames = ctx.SelectedState.SelectedFrames;

        Assert.Contains(f0, frames);
        Assert.Contains(f2, frames);
    }

    [Fact]
    public void SelectedFrames_WhenNodesEmpty_FallsBackToSingleSelectedFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 2);
        ctx.SelectedState.SelectedNodes = new List<object>();
        ctx.SelectedState.SelectedFrame = chain.Frames[1];

        var frames = ctx.SelectedState.SelectedFrames;

        Assert.Contains(chain.Frames[1], frames);
    }

    [Fact]
    public void SelectedFrames_WhenNothingSelected_ReturnsEmptyEnumerable()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.SelectedState.SelectedNodes = new List<object>();

        var frames = ctx.SelectedState.SelectedFrames;

        Assert.Empty(frames);
    }
}
