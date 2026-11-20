using AnimationEditor.Core.DragDrop;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TextureDropProcessorTests
{
    [Fact]
    public void ApplyPngDrop_OnFrame_AssignsOnlyThatFrameTexture()
    {
        var chain = new AnimationChainSave();
        var frameA = new AnimationFrameSave { TextureName = "oldA.png" };
        var frameB = new AnimationFrameSave { TextureName = "oldB.png" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);

        var result = TextureDropProcessor.ApplyPngDrop(
            chain,
            frameA,
            TestPaths.Abs("Project", "Content", "NewTex.png"),
            TestPaths.Abs("Project", "Animations", "Player.achx"),
            createFrameOnCtrl: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.UpdatedFrame, result);
        Assert.Equal("../Content/NewTex.png", frameA.TextureName);
        Assert.Equal("oldB.png", frameB.TextureName);
    }

    [Fact]
    public void ApplyPngDrop_OnChainWithoutCtrl_AssignsAllFrames()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave { TextureName = "oldA.png" });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "oldB.png" });

        var result = TextureDropProcessor.ApplyPngDrop(
            chain,
            null,
            TestPaths.Abs("Project", "Content", "Shared.png"),
            TestPaths.Abs("Project", "Animations", "Player.achx"),
            createFrameOnCtrl: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.UpdatedChainFrames, result);
        Assert.All(chain.Frames, frame => Assert.Equal("../Content/Shared.png", frame.TextureName));
    }

    [Fact]
    public void ApplyPngDrop_OnChainWithCtrl_CreatesNewFrame()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave { TextureName = "oldA.png" });

        var result = TextureDropProcessor.ApplyPngDrop(
            chain,
            null,
            TestPaths.Abs("Project", "Content", "NewFrameTex.png"),
            TestPaths.Abs("Project", "Animations", "Player.achx"),
            createFrameOnCtrl: true, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.CreatedFrame, result);
        Assert.Equal(2, chain.Frames.Count);
        Assert.Equal("../Content/NewFrameTex.png", chain.Frames.Last().TextureName);
    }

    [Fact]
    public void ApplyPngDrop_OnEmptyChainWithoutCtrl_CreatesNewFrame()
    {
        var chain = new AnimationChainSave();

        var result = TextureDropProcessor.ApplyPngDrop(
            chain,
            null,
            TestPaths.Abs("Project", "Content", "FirstFrameTex.png"),
            TestPaths.Abs("Project", "Animations", "Player.achx"),
            createFrameOnCtrl: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.CreatedFrame, result);
        Assert.Single(chain.Frames);
        Assert.Equal("../Content/FirstFrameTex.png", chain.Frames[0].TextureName);
    }

    // #937: a frame created by dropping a PNG stays shapeless until a shape is actually added,
    // same as AppCommands.AddFrame/AddFrameFromPixelBounds/BatchFrameBuilder -- otherwise it saves
    // with an empty shapesSave/ShapeCollectionSave block.
    [Fact]
    public void ApplyPngDrop_CreatedFrame_HasNullShapesSave()
    {
        var chain = new AnimationChainSave();

        TextureDropProcessor.ApplyPngDrop(
            chain,
            null,
            TestPaths.Abs("Project", "Content", "FirstFrameTex.png"),
            TestPaths.Abs("Project", "Animations", "Player.achx"),
            createFrameOnCtrl: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Null(chain.Frames[0].ShapesSave);
    }

    [Fact]
    public void ApplyPngDrop_NonPng_IsIgnored()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave { TextureName = "oldA.png" });

        var result = TextureDropProcessor.ApplyPngDrop(
            chain,
            null,
            TestPaths.Abs("Project", "Content", "NotTexture.jpg"),
            TestPaths.Abs("Project", "Animations", "Player.achx"),
            createFrameOnCtrl: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.NotApplied, result);
        Assert.Single(chain.Frames);
        Assert.Equal("oldA.png", chain.Frames[0].TextureName);
    }

    // ── TD06: relative path computation ──────────────────────────────────────

    [Fact]
    public void ApplyPngDrop_SameDirectory_ReturnsFileNameOnly()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave());

        TextureDropProcessor.ApplyPngDrop(
            chain,
            chain.Frames[0],
            TestPaths.Abs("Project", "Tex.png"),
            TestPaths.Abs("Project", "Player.achx"),
            createFrameOnCtrl: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        // Same folder → just the filename, no directory prefix
        Assert.Equal("Tex.png", chain.Frames[0].TextureName);
    }

    [Fact]
    public void ApplyPngDrop_Subdirectory_ReturnsRelativeSubPath()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave());

        TextureDropProcessor.ApplyPngDrop(
            chain,
            chain.Frames[0],
            TestPaths.Abs("Project", "Content", "Sprites", "Hero.png"),
            TestPaths.Abs("Project", "Animations", "Player.achx"),
            createFrameOnCtrl: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        // FRB FileManager.MakeRelative normalises to forward slashes
        Assert.Equal("../Content/Sprites/Hero.png", chain.Frames[0].TextureName);
    }

    [Fact]
    public void ApplyPngDrop_RelativePathUsesForwardSlashes()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave());

        TextureDropProcessor.ApplyPngDrop(
            chain,
            chain.Frames[0],
            TestPaths.Abs("Project", "Content", "NewTex.png"),
            TestPaths.Abs("Project", "Animations", "Player.achx"),
            createFrameOnCtrl: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        var tex = chain.Frames[0].TextureName;
        Assert.DoesNotContain(@"\", tex);
        Assert.Contains("/", tex);
    }

    // ── Case preservation (issue #941) ────────────────────────────────────────
    // Drag-drop reads the OS drag payload's real-case path directly (never through the
    // lowercased WireframeControl.LoadedTexturePath cache key), so it was never affected by
    // #941 -- this locks that in as a regression guard.

    [Fact]
    public void ApplyPngDrop_MixedCaseOnDiskTexture_PreservesExactCase()
    {
        var chain = new AnimationChainSave();

        var result = TextureDropProcessor.ApplyPngDrop(
            chain,
            null,
            TestPaths.Abs("Project", "Content", "Items.PNG"),
            TestPaths.Abs("Project", "Animations", "Player.achx"),
            createFrameOnCtrl: false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.CreatedFrame, result);
        Assert.Equal("../Content/Items.PNG", chain.Frames[0].TextureName);
    }

    // ── Unsaved project (achxFileName == null) ────────────────────────────────
    // Root cause: these all previously returned NotApplied, silently breaking
    // drag-drop on projects that hadn't been saved yet.

    [Fact]
    public void ApplyPngDrop_NullAchx_EmptyChain_CreatesFrame()
    {
        var chain = new AnimationChainSave();

        var result = TextureDropProcessor.ApplyPngDrop(chain, null,
            TestPaths.AltAbs("Downloads", "sprite.png"), null, false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.CreatedFrame, result);
    }

    [Fact]
    public void ApplyPngDrop_NullAchx_EmptyChain_UsesAbsolutePath()
    {
        var chain = new AnimationChainSave();
        TextureDropProcessor.ApplyPngDrop(chain, null,
            TestPaths.AltAbs("Downloads", "sprite.png"), null, false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TestPaths.AltAbs("Downloads", "sprite.png"), chain.Frames[0].TextureName);
    }

    [Fact]
    public void ApplyPngDrop_NullAchx_ChainWithFrames_UpdatesAllFrames()
    {
        var chain = new AnimationChainSave();
        chain.Frames.Add(new AnimationFrameSave { TextureName = "old.png" });

        var result = TextureDropProcessor.ApplyPngDrop(chain, null,
            TestPaths.AltAbs("Downloads", "sprite.png"), null, false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.UpdatedChainFrames, result);
        Assert.Equal(TestPaths.AltAbs("Downloads", "sprite.png"), chain.Frames[0].TextureName);
    }

    [Fact]
    public void ApplyPngDrop_NullAchx_TargetFrame_UpdatesThatFrame()
    {
        var frame = new AnimationFrameSave();

        var result = TextureDropProcessor.ApplyPngDrop(null, frame,
            TestPaths.AltAbs("Downloads", "sprite.png"), null, false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.UpdatedFrame, result);
        Assert.Equal(TestPaths.AltAbs("Downloads", "sprite.png"), frame.TextureName);
    }

    [Fact]
    public void ApplyPngDrop_NullAchx_NonPng_IsIgnored()
    {
        var chain = new AnimationChainSave();

        var result = TextureDropProcessor.ApplyPngDrop(chain, null,
            TestPaths.AltAbs("Downloads", "sprite.jpg"), null, false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.NotApplied, result);
    }

    [Fact]
    public void ApplyPngDrop_NullAchx_NullChainAndFrame_IsIgnored()
    {
        var result = TextureDropProcessor.ApplyPngDrop(null, null,
            TestPaths.AltAbs("Downloads", "sprite.png"), null, false, AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds);

        Assert.Equal(TextureDropResult.NotApplied, result);
    }
}
