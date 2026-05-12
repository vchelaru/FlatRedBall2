using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>All tests that mutate singleton state run in the same xUnit collection to prevent parallelism.</summary>
[Collection("SequentialSingletons")]
public class AppCommandsChainTests
{
    // ── AddAnimationChain ────────────────────────────────────────────────────

    [Fact]
    public async Task AddAnimationChain_AddsChainToAcls()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;

        await ctx.AppCommands.AddAnimationChain();

        Assert.Single(acls.AnimationChains);
    }

    [Fact]
    public async Task AddAnimationChain_UsesUniqueNamesWhenCalledMultipleTimes()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;

        await ctx.AppCommands.AddAnimationChain();
        await ctx.AppCommands.AddAnimationChain();
        await ctx.AppCommands.AddAnimationChain();

        var names = acls.AnimationChains.Select(c => c.Name).ToList();
        Assert.Equal(3, names.Distinct().Count());
    }

    [Fact]
    public async Task AddAnimationChain_SetsSelectedChain()
    {
        var ctx = TestHelpers.SetupFreshAcls();

        await ctx.AppCommands.AddAnimationChain();

        Assert.NotNull(ctx.SelectedState.SelectedChain);
    }

    [Fact]
    public async Task AddAnimationChain_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            await ctx.AppCommands.AddAnimationChain();
            Assert.True(fired, "AnimationChainsChanged was not raised.");
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    [Fact]
    public async Task AddAnimationChain_WhenAclsIsNull_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.AnimationChainListSave = null;

        // Should silently do nothing
        await ctx.AppCommands.AddAnimationChain();
    }

    [Fact]
    public async Task AddAnimationChain_UsesNameFromPrompt()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.PromptStringAsync = (_, _, _) => Task.FromResult<string?>("WalkRight");

        await ctx.AppCommands.AddAnimationChain();

        Assert.Equal("WalkRight", acls.AnimationChains[0].Name);
    }

    [Fact]
    public async Task AddAnimationChain_WhenCancelled_DoesNotAdd()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.PromptStringAsync = (_, _, _) => Task.FromResult<string?>(null);

        await ctx.AppCommands.AddAnimationChain();

        Assert.Empty(acls.AnimationChains);
    }

    // ── MoveChain ────────────────────────────────────────────────────────────

    [Fact]
    public void MoveChain_Delta1_MovesChainDown()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "A");
        var chainB = TestHelpers.MakeChain(acls, "B");

        ctx.AppCommands.MoveChain(chainA, +1);

        Assert.Equal(chainB, acls.AnimationChains[0]);
        Assert.Equal(chainA, acls.AnimationChains[1]);
    }

    [Fact]
    public void MoveChain_DeltaNeg1_MovesChainUp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "A");
        var chainB = TestHelpers.MakeChain(acls, "B");

        ctx.AppCommands.MoveChain(chainB, -1);

        Assert.Equal(chainB, acls.AnimationChains[0]);
        Assert.Equal(chainA, acls.AnimationChains[1]);
    }

    [Fact]
    public void MoveChain_AtBottom_DoesNotMoveBelowEnd()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "A");
        var chainB = TestHelpers.MakeChain(acls, "B");

        ctx.AppCommands.MoveChain(chainB, +1); // already at end

        Assert.Equal(chainA, acls.AnimationChains[0]);
        Assert.Equal(chainB, acls.AnimationChains[1]);
    }

    [Fact]
    public void MoveChain_AtTop_DoesNotMoveAboveStart()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "A");
        TestHelpers.MakeChain(acls, "B");

        ctx.AppCommands.MoveChain(chainA, -1); // already at top

        Assert.Equal(chainA, acls.AnimationChains[0]);
    }

    // ── MoveChainToTop / MoveChainToBottom ───────────────────────────────────

    [Fact]
    public void MoveChainToTop_MovesChainToFirstPosition()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        TestHelpers.MakeChain(acls, "A");
        TestHelpers.MakeChain(acls, "B");
        var chainC = TestHelpers.MakeChain(acls, "C");

        ctx.AppCommands.MoveChainToTop(chainC);

        Assert.Equal(chainC, acls.AnimationChains[0]);
    }

    [Fact]
    public void MoveChainToBottom_MovesChainToLastPosition()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "A");
        TestHelpers.MakeChain(acls, "B");
        TestHelpers.MakeChain(acls, "C");

        ctx.AppCommands.MoveChainToBottom(chainA);

        Assert.Equal(chainA, acls.AnimationChains[2]);
    }

    // ── FlipChainHorizontally ────────────────────────────────────────────────

    [Fact]
    public void FlipChainHorizontally_TogglesFlipHorizontalOnAllFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 3);
        foreach (var f in chain.Frames) f.FlipHorizontal = false;

        ctx.AppCommands.FlipChainHorizontally(chain);

        Assert.All(chain.Frames, f => Assert.True(f.FlipHorizontal));
    }

    [Fact]
    public void FlipChainHorizontally_TogglesBack_WhenCalledTwice()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 2);

        ctx.AppCommands.FlipChainHorizontally(chain);
        ctx.AppCommands.FlipChainHorizontally(chain);

        Assert.All(chain.Frames, f => Assert.False(f.FlipHorizontal));
    }

    [Fact]
    public void FlipChainHorizontally_MixedFlags_TogglesEachIndividually()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk", 2);
        chain.Frames[0].FlipHorizontal = true;
        chain.Frames[1].FlipHorizontal = false;

        ctx.AppCommands.FlipChainHorizontally(chain);

        Assert.False(chain.Frames[0].FlipHorizontal);
        Assert.True(chain.Frames[1].FlipHorizontal);
    }

    // ── FlipChainVertically ──────────────────────────────────────────────────

    [Fact]
    public void FlipChainVertically_TogglesFlipVerticalOnAllFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Jump", 2);
        foreach (var f in chain.Frames) f.FlipVertical = false;

        ctx.AppCommands.FlipChainVertically(chain);

        Assert.All(chain.Frames, f => Assert.True(f.FlipVertical));
    }

    // ── InvertFrameOrder ─────────────────────────────────────────────────────

    [Fact]
    public void InvertFrameOrder_ReversesFrameSequence()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 3);
        var original = chain.Frames.ToList();

        ctx.AppCommands.InvertFrameOrder(chain);

        Assert.Equal(original[2], chain.Frames[0]);
        Assert.Equal(original[1], chain.Frames[1]);
        Assert.Equal(original[0], chain.Frames[2]);
    }

    [Fact]
    public void InvertFrameOrder_OddFrameCount_MiddleFrameStaysMiddle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 5);
        var middle = chain.Frames[2];

        ctx.AppCommands.InvertFrameOrder(chain);

        Assert.Equal(middle, chain.Frames[2]);
    }

    [Fact]
    public void InvertFrameOrder_ThenInvertAgain_RestoresOriginalOrder()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run", 4);
        var original = chain.Frames.ToList();

        ctx.AppCommands.InvertFrameOrder(chain);
        ctx.AppCommands.InvertFrameOrder(chain);

        Assert.Equal(original, chain.Frames.ToList());
    }

    // ── SetAllFrameLengths ───────────────────────────────────────────────────

    [Fact]
    public void SetAllFrameLengths_AssignsDurationToEveryFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Idle", 4);

        ctx.AppCommands.SetAllFrameLengths(chain, 0.25f);

        Assert.All(chain.Frames, f => Assert.Equal(0.25f, f.FrameLength));
    }

    [Fact]
    public void SetAllFrameLengths_ZeroDuration_IsAllowed()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Idle", 2);

        ctx.AppCommands.SetAllFrameLengths(chain, 0f);

        Assert.All(chain.Frames, f => Assert.Equal(0f, f.FrameLength));
    }

    // ── DuplicateChain ───────────────────────────────────────────────────────

    [Fact]
    public void DuplicateChain_CreatesDeepCopyWithAllFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var source = TestHelpers.MakeChain(acls, "Walk", 3);

        var copy = ctx.AppCommands.DuplicateChain(source);

        Assert.NotNull(copy);
        Assert.Equal(3, copy!.Frames.Count);
        Assert.NotSame(source, copy);
        Assert.NotSame(source.Frames[0], copy.Frames[0]);
    }

    [Fact]
    public void DuplicateChain_CopiedFramesHaveSameTextureName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var source = TestHelpers.MakeChain(acls, "Walk", 2);
        source.Frames[0].TextureName = "alpha.png";
        source.Frames[1].TextureName = "beta.png";

        var copy = ctx.AppCommands.DuplicateChain(source);

        Assert.Equal("alpha.png", copy!.Frames[0].TextureName);
        Assert.Equal("beta.png", copy.Frames[1].TextureName);
    }

    [Fact]
    public void DuplicateChain_WithFlipH_TogglesFlipHorizontalOnAllCopiedFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var source = TestHelpers.MakeChain(acls, "WalkLeft", 2);
        source.Frames[0].FlipHorizontal = false;
        source.Frames[1].FlipHorizontal = true;

        var copy = ctx.AppCommands.DuplicateChain(source, flipH: true);

        Assert.True(copy!.Frames[0].FlipHorizontal);   // false → toggled → true
        Assert.False(copy.Frames[1].FlipHorizontal);   // true  → toggled → false
    }

    [Fact]
    public void DuplicateChain_WithFlipV_TogglesFlipVerticalOnAllCopiedFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var source = TestHelpers.MakeChain(acls, "JumpUp", 2);
        foreach (var f in source.Frames) f.FlipVertical = false;

        var copy = ctx.AppCommands.DuplicateChain(source, flipV: true);

        Assert.All(copy!.Frames, f => Assert.True(f.FlipVertical));
    }

    [Fact]
    public void DuplicateChain_CopiesShapesFromFrames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var source = TestHelpers.MakeChain(acls, "Attack", 1);
        source.Frames[0].ShapesSave!.AARectSaves.Add(
            new AARectSave { Name = "HitBox", ScaleX = 5, ScaleY = 5 });

        var copy = ctx.AppCommands.DuplicateChain(source);

        Assert.Single(copy!.Frames[0].ShapesSave!.AARectSaves);
        Assert.Equal("HitBox", copy.Frames[0].ShapesSave!.AARectSaves[0].Name);
    }

    [Fact]
    public void DuplicateChain_CopiedChainNameIsUnique()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var source = TestHelpers.MakeChain(acls, "Walk");

        var copy1 = ctx.AppCommands.DuplicateChain(source);
        var copy2 = ctx.AppCommands.DuplicateChain(source);

        Assert.NotEqual(copy1!.Name, copy2!.Name);
    }

    [Fact]
    public void DuplicateChain_WithCustomName_UsesProvidedName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var source = TestHelpers.MakeChain(acls, "WalkLeft");

        var copy = ctx.AppCommands.DuplicateChain(source, newName: "WalkRight");

        Assert.Equal("WalkRight", copy!.Name);
    }

    [Fact]
    public void DuplicateChain_AddsToAcls()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var source = TestHelpers.MakeChain(acls, "Walk");

        ctx.AppCommands.DuplicateChain(source);

        Assert.Equal(2, acls.AnimationChains.Count);
    }

    [Fact]
    public void DuplicateChain_WhenAclsIsNull_ReturnsNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.AnimationChainListSave = null;
        var orphan = new AnimationChainSave { Name = "Orphan" };

        var result = ctx.AppCommands.DuplicateChain(orphan);

        Assert.Null(result);
    }

    // ── SortAnimationsAlphabetically ─────────────────────────────────────────

    [Fact]
    public void SortAnimationsAlphabetically_SortsByNameAscending()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        TestHelpers.MakeChain(acls, "Zebra");
        TestHelpers.MakeChain(acls, "Alpha");
        TestHelpers.MakeChain(acls, "Mango");

        ctx.AppCommands.SortAnimationsAlphabetically();

        Assert.Equal("Alpha", acls.AnimationChains[0].Name);
        Assert.Equal("Mango", acls.AnimationChains[1].Name);
        Assert.Equal("Zebra", acls.AnimationChains[2].Name);
    }

    [Fact]
    public void SortAnimationsAlphabetically_AlreadySorted_IsIdempotent()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        TestHelpers.MakeChain(acls, "A");
        TestHelpers.MakeChain(acls, "B");
        TestHelpers.MakeChain(acls, "C");

        ctx.AppCommands.SortAnimationsAlphabetically();

        Assert.Equal("A", acls.AnimationChains[0].Name);
        Assert.Equal("B", acls.AnimationChains[1].Name);
        Assert.Equal("C", acls.AnimationChains[2].Name);
    }

    // ── DeleteAnimationChains ────────────────────────────────────────────────

    [Fact]
    public void DeleteAnimationChains_RemovesSpecifiedChains()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "A");
        var chainB = TestHelpers.MakeChain(acls, "B");
        var chainC = TestHelpers.MakeChain(acls, "C");

        ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { chainA, chainC });

        Assert.Single(acls.AnimationChains);
        Assert.Equal(chainB, acls.AnimationChains[0]);
    }

    [Fact]
    public void DeleteAnimationChains_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "X");
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.DeleteAnimationChains(new List<AnimationChainSave> { chain });
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    // ── FlipFrameHorizontally (F09) ──────────────────────────────────────────

    [Fact]
    public void FlipFrameHorizontally_TogglesFlipHorizontalOnFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { FlipHorizontal = false };

        ctx.AppCommands.FlipFrameHorizontally(frame);

        Assert.True(frame.FlipHorizontal);
    }

    [Fact]
    public void FlipFrameHorizontally_TogglesBackWhenCalledTwice()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { FlipHorizontal = false };

        ctx.AppCommands.FlipFrameHorizontally(frame);
        ctx.AppCommands.FlipFrameHorizontally(frame);

        Assert.False(frame.FlipHorizontal);
    }

    [Fact]
    public void FlipFrameHorizontally_DoesNotAffectFlipVertical()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { FlipHorizontal = false, FlipVertical = true };

        ctx.AppCommands.FlipFrameHorizontally(frame);

        Assert.True(frame.FlipVertical);
    }

    [Fact]
    public void FlipFrameHorizontally_RaisesAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave();
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.FlipFrameHorizontally(frame);
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    // ── FlipFrameVertically (F10) ────────────────────────────────────────────

    [Fact]
    public void FlipFrameVertically_TogglesFlipVerticalOnFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { FlipVertical = false };

        ctx.AppCommands.FlipFrameVertically(frame);

        Assert.True(frame.FlipVertical);
    }

    [Fact]
    public void FlipFrameVertically_TogglesBackWhenCalledTwice()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { FlipVertical = false };

        ctx.AppCommands.FlipFrameVertically(frame);
        ctx.AppCommands.FlipFrameVertically(frame);

        Assert.False(frame.FlipVertical);
    }

    [Fact]
    public void FlipFrameVertically_DoesNotAffectFlipHorizontal()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { FlipVertical = false, FlipHorizontal = true };

        ctx.AppCommands.FlipFrameVertically(frame);

        Assert.True(frame.FlipHorizontal);
    }

    [Fact]
    public void FlipFrameVertically_RaisesAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave();
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.FlipFrameVertically(frame);
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    // ── RenameChain (TV07) ────────────────────────────────────────────────────

    [Fact]
    public void RenameChain_UniqueNewName_ReturnsTrueAndUpdatesName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "OldName");

        bool result = ctx.AppCommands.RenameChain(chain, "NewName");

        Assert.True(result);
        Assert.Equal("NewName", chain.Name);
    }

    [Fact]
    public void RenameChain_SameName_ReturnsTrueWithoutChange()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Walk");

        bool result = ctx.AppCommands.RenameChain(chain, "Walk");

        Assert.True(result);
        Assert.Equal("Walk", chain.Name);
    }

    [Fact]
    public void RenameChain_DuplicateNameExistsOnOtherChain_ReturnsFalseAndNameUnchanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "Walk");
        var chainB = TestHelpers.MakeChain(acls, "Run");

        bool result = ctx.AppCommands.RenameChain(chainA, "Run");

        Assert.False(result);
        Assert.Equal("Walk", chainA.Name);
        Assert.Equal("Run",  chainB.Name);
    }

    [Fact]
    public void RenameChain_UniqueNewName_RaisesAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "A");
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.RenameChain(chain, "B");
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    [Fact]
    public void RenameChain_DuplicateName_DoesNotRaiseAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chainA = TestHelpers.MakeChain(acls, "Walk");
        TestHelpers.MakeChain(acls, "Run");
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.RenameChain(chainA, "Run");
            Assert.False(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    // ── RenameFrame / SetFrameTextureName (TV08) ───────────────────────────────

    [Fact]
    public void RenameFrame_SetsTextureNameOnFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new FlatRedBall2.Animation.Content.AnimationFrameSave
            { TextureName = "old.png" };

        ctx.AppCommands.RenameFrame(frame, "new.png");

        Assert.Equal("new.png", frame.TextureName);
    }

    [Fact]
    public void RenameFrame_RaisesAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new FlatRedBall2.Animation.Content.AnimationFrameSave();
        bool fired = false;
        void Handler() => fired = true;
        ctx.ApplicationEvents.AnimationChainsChanged += Handler;
        try
        {
            ctx.AppCommands.RenameFrame(frame, "hero.png");
            Assert.True(fired);
        }
        finally
        {
            ctx.ApplicationEvents.AnimationChainsChanged -= Handler;
        }
    }

    [Fact]
    public void RenameFrame_EmptyString_SetsEmptyTextureName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new FlatRedBall2.Animation.Content.AnimationFrameSave
            { TextureName = "something.png" };

        ctx.AppCommands.RenameFrame(frame, string.Empty);

        Assert.Equal(string.Empty, frame.TextureName);
    }
}
