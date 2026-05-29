using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsFrameTests
{
    // ── AddFrame ─────────────────────────────────────────────────────────────

    [Fact]
    public void AddFrame_AddsFrameToChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run");

        ctx.AppCommands.AddFrame(chain);

        Assert.Single(chain.Frames);
    }

    [Fact]
    public void AddFrame_DefaultsToFullUvCoordinates()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run");

        ctx.AppCommands.AddFrame(chain);
        var frame = chain.Frames[0];

        Assert.Equal(0f, frame.LeftCoordinate);
        Assert.Equal(1f, frame.RightCoordinate);
        Assert.Equal(0f, frame.TopCoordinate);
        Assert.Equal(1f, frame.BottomCoordinate);
    }

    [Fact]
    public void AddFrame_DefaultsFrameLengthTo0Point1()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Idle");

        ctx.AppCommands.AddFrame(chain);

        Assert.Equal(0.1f, chain.Frames[0].FrameLength);
    }

    [Fact]
    public void AddFrame_WithTextureName_SetsTextureOnNewFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");

        ctx.AppCommands.AddFrame(chain, "hero.png");

        Assert.Equal("hero.png", chain.Frames[0].TextureName);
    }

    [Fact]
    public void AddFrame_WithoutTextureName_SetsEmptyString()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");

        ctx.AppCommands.AddFrame(chain);

        Assert.Equal(string.Empty, chain.Frames[0].TextureName);
    }

    [Fact]
    public void AddFrame_InitializesShapesSave()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Attack");

        ctx.AppCommands.AddFrame(chain);

        Assert.NotNull(chain.Frames[0].ShapesSave);
    }

    [Fact]
    public void AddFrame_SetsSelectedFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Jump");

        ctx.AppCommands.AddFrame(chain);

        Assert.Same(chain.Frames[0], ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void AddFrame_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "X");
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.AddFrame(chain);
            Assert.True(fired, "AnimationChainsChanged not raised after AddFrame.");
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    [Fact]
    public void AddFrame_MultipleFrames_AllAppendedInOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run");

        ctx.AppCommands.AddFrame(chain, "a.png");
        ctx.AppCommands.AddFrame(chain, "b.png");
        ctx.AppCommands.AddFrame(chain, "c.png");

        Assert.Equal(3, chain.Frames.Count);
        Assert.Equal("a.png", chain.Frames[0].TextureName);
        Assert.Equal("b.png", chain.Frames[1].TextureName);
        Assert.Equal("c.png", chain.Frames[2].TextureName);
    }

    // ── MoveFrame ────────────────────────────────────────────────────────────

    [Fact]
    public void MoveFrame_Delta1_MovesFrameDown()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];

        ctx.AppCommands.MoveFrame(frameA, chain, +1);

        Assert.Equal(frameB, chain.Frames[0]);
        Assert.Equal(frameA, chain.Frames[1]);
    }

    [Fact]
    public void MoveFrame_DeltaNeg1_MovesFrameUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        var frameA = chain.Frames[0];
        var frameB = chain.Frames[1];

        ctx.AppCommands.MoveFrame(frameB, chain, -1);

        Assert.Equal(frameB, chain.Frames[0]);
        Assert.Equal(frameA, chain.Frames[1]);
    }

    [Fact]
    public void MoveFrame_AtBottom_DoesNotMoveBelowEnd()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        var lastFrame = chain.Frames[2];

        ctx.AppCommands.MoveFrame(lastFrame, chain, +1);

        Assert.Equal(lastFrame, chain.Frames[2]);
    }

    [Fact]
    public void MoveFrame_AtTop_DoesNotMoveAboveStart()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        var firstFrame = chain.Frames[0];

        ctx.AppCommands.MoveFrame(firstFrame, chain, -1);

        Assert.Equal(firstFrame, chain.Frames[0]);
    }

    // ── MoveFrameToTop / MoveFrameToBottom ───────────────────────────────────

    [Fact]
    public void MoveFrameToTop_MovesFrameToFirstPosition()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        var lastFrame = chain.Frames[2];

        ctx.AppCommands.MoveFrameToTop(lastFrame, chain);

        Assert.Equal(lastFrame, chain.Frames[0]);
    }

    [Fact]
    public void MoveFrameToBottom_MovesFrameToLastPosition()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        var firstFrame = chain.Frames[0];

        ctx.AppCommands.MoveFrameToBottom(firstFrame, chain);

        Assert.Equal(firstFrame, chain.Frames[2]);
    }

    [Fact]
    public void MoveFrameToTop_AlreadyAtTop_IsIdempotent()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        var firstFrame = chain.Frames[0];

        ctx.AppCommands.MoveFrameToTop(firstFrame, chain);

        Assert.Equal(firstFrame, chain.Frames[0]);
        Assert.Equal(3, chain.Frames.Count);
    }

    [Fact]
    public void MoveFrameToBottom_AlreadyAtBottom_IsIdempotent()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        var lastFrame = chain.Frames[2];

        ctx.AppCommands.MoveFrameToBottom(lastFrame, chain);

        Assert.Equal(lastFrame, chain.Frames[2]);
        Assert.Equal(3, chain.Frames.Count);
    }

    [Fact]
    public void MoveFrame_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 2);
        var frame = chain.Frames[0];
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.MoveFrame(frame, chain, +1);
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    // ── DuplicateFrame ───────────────────────────────────────────────────────

    [Fact]
    public void DuplicateFrame_CreatesDeepCopy()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var source = chain.Frames[0];

        var copy = ctx.AppCommands.DuplicateFrame(source, chain);

        Assert.NotNull(copy);
        Assert.NotSame(source, copy);
    }

    [Fact]
    public void DuplicateFrame_CopiesTextureName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        chain.Frames[0].TextureName = "hero.png";

        var copy = ctx.AppCommands.DuplicateFrame(chain.Frames[0], chain);

        Assert.Equal("hero.png", copy!.TextureName);
    }

    [Fact]
    public void DuplicateFrame_CopiesUvCoordinates()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        var source = chain.Frames[0];
        source.LeftCoordinate   = 0.1f;
        source.RightCoordinate  = 0.5f;
        source.TopCoordinate    = 0.2f;
        source.BottomCoordinate = 0.8f;
        source.FrameLength      = 0.25f;
        source.RelativeX        = 3f;
        source.RelativeY        = -2f;

        var copy = ctx.AppCommands.DuplicateFrame(source, chain);

        Assert.Equal(0.1f,  copy!.LeftCoordinate);
        Assert.Equal(0.5f,  copy.RightCoordinate);
        Assert.Equal(0.2f,  copy.TopCoordinate);
        Assert.Equal(0.8f,  copy.BottomCoordinate);
        Assert.Equal(0.25f, copy.FrameLength);
        Assert.Equal(3f,    copy.RelativeX);
        Assert.Equal(-2f,   copy.RelativeY);
    }

    [Fact]
    public void DuplicateFrame_CopiesFlipFlags()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Idle", 1);
        chain.Frames[0].FlipHorizontal = true;
        chain.Frames[0].FlipVertical   = true;

        var copy = ctx.AppCommands.DuplicateFrame(chain.Frames[0], chain);

        Assert.True(copy!.FlipHorizontal);
        Assert.True(copy.FlipVertical);
    }

    [Fact]
    public void DuplicateFrame_CopiesShapes()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Attack", 1);
        chain.Frames[0].ShapesSave!.Shapes.Add(
            new AARectSave { Name = "HitBox", ScaleX = 4, ScaleY = 4 });

        var copy = ctx.AppCommands.DuplicateFrame(chain.Frames[0], chain);

        Assert.Single(copy!.ShapesSave!.AARectSaves);
        Assert.Equal("HitBox", copy.ShapesSave!.AARectSaves.First().Name);
        Assert.NotSame(chain.Frames[0].ShapesSave!.Shapes[0], copy.ShapesSave.Shapes[0]);
    }

    [Fact]
    public void DuplicateFrame_InsertsImmediatelyAfterSource()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 3);
        var source = chain.Frames[1]; // middle frame

        var copy = ctx.AppCommands.DuplicateFrame(source, chain);

        Assert.Equal(4, chain.Frames.Count);
        Assert.Equal(2, chain.Frames.IndexOf(copy!));
    }

    [Fact]
    public void DuplicateFrame_InsertsAfterLastFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 2);
        var source = chain.Frames[1]; // last frame

        var copy = ctx.AppCommands.DuplicateFrame(source, chain);

        Assert.Equal(3, chain.Frames.Count);
        Assert.Same(copy, chain.Frames[2]);
    }

    [Fact]
    public void DuplicateFrame_SetsSelectedFrameToTheCopy()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);

        var copy = ctx.AppCommands.DuplicateFrame(chain.Frames[0], chain);

        Assert.Same(copy, ctx.SelectedState.SelectedFrame);
    }

    [Fact]
    public void DuplicateFrame_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Run", 1);
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.DuplicateFrame(chain.Frames[0], chain);
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    [Fact]
    public void DuplicateFrame_WhenSourceNotInChain_ReturnsNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var orphan = new AnimationFrameSave();

        var result = ctx.AppCommands.DuplicateFrame(orphan, chain);

        Assert.Null(result);
    }
}
