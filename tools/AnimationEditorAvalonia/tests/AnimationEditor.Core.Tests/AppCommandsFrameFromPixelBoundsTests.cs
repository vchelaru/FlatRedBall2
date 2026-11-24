using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsFrameFromPixelBoundsTests
{
    private readonly TestServices ctx = TestHelpers.SetupFreshAcls();

    private AnimationChainSave MakeChain(string name = "Chain")
    {
        var chain = new AnimationChainSave { Name = name };
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        return chain;
    }

    // ── Frame content ─────────────────────────────────────────────────────

    [Fact]
    public void AddsFrameToChain()
    {
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "tex.png", 0, 0, 64, 64);
        Assert.Single(chain.Frames);
    }

    [Fact]
    public void Frame_HasCorrectTextureName()
    {
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "sprites/hero.png", 0, 0, 32, 32);
        Assert.Equal("sprites/hero.png", chain.Frames[0].TextureName);
    }

    [Fact]
    public void Frame_LeftCoordinate_IsPixelOverWidth()
    {
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "t.png", 32, 0, 96, 64);
        Assert.Equal(32f, chain.Frames[0].LeftCoordinate, precision: 5);
    }

    [Fact]
    public void Frame_RightCoordinate_IsMaxXOverWidth()
    {
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "t.png", 0, 0, 96, 64);
        Assert.Equal(96f, chain.Frames[0].RightCoordinate, precision: 5);
    }

    [Fact]
    public void Frame_TopCoordinate_IsMinYOverHeight()
    {
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "t.png", 0, 16, 64, 80);
        Assert.Equal(16f, chain.Frames[0].TopCoordinate, precision: 5);
    }

    [Fact]
    public void Frame_BottomCoordinate_IsMaxYOverHeight()
    {
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "t.png", 0, 0, 64, 80);
        Assert.Equal(80f, chain.Frames[0].BottomCoordinate, precision: 5);
    }

    [Fact]
    public void Frame_FrameLength_IsDefaultPointOne()
    {
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "t.png", 0, 0, 64, 64);
        Assert.Equal(0.1f, chain.Frames[0].FrameLength, precision: 5);
    }

    [Fact]
    public void Frame_ShapesSave_StaysNull()
    {
        // #937: a freshly-created frame stays shapeless until a shape is actually added, so it
        // saves without an empty shapesSave/ShapeCollectionSave block.
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "t.png", 0, 0, 64, 64);
        Assert.Null(chain.Frames[0].ShapesSave);
    }

    // ── Selection ─────────────────────────────────────────────────────────

    [Fact]
    public void NewFrame_IsSelected()
    {
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "t.png", 0, 0, 64, 64);
        Assert.Same(chain.Frames[0], ctx.SelectedState.SelectedFrame);
    }

    // ── Events ───────────────────────────────────────────────────────────

    [Fact]
    public void FiresAnimationChainsChanged()
    {
        bool fired = false;
        ctx.ApplicationEvents.AnimationChainsChanged += () => fired = true;
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "t.png", 0, 0, 64, 64);
        Assert.True(fired);
    }

    [Fact]
    public void FiresRefreshChainNodeRequested()
    {
        AnimationChainSave? received = null;
        ctx.AppCommands.RefreshChainNodeRequested += c => received = c;
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "t.png", 0, 0, 64, 64);
        Assert.Same(chain, received);
    }

    // ── Non-square texture ────────────────────────────────────────────────

    [Fact]
    public void NonSquareTexture_UVsComputedCorrectly()
    {
        // 200 wide × 100 tall; region from (50,25) to (150,75)
        var chain = MakeChain();
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "wide.png", 50, 25, 150, 75);
        var f = chain.Frames[0];
        Assert.Equal(50f, f.LeftCoordinate,   precision: 5);
        Assert.Equal(150f, f.RightCoordinate,  precision: 5);
        Assert.Equal(25f,  f.TopCoordinate,    precision: 5);
        Assert.Equal(75f,  f.BottomCoordinate, precision: 5);
    }
}
