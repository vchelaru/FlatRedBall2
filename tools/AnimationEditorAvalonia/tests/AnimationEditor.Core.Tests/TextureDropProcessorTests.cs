using AnimationEditor.Core.DragDrop;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

// What each result then does to the document is TextureDropApplierTests'.
public class TextureDropProcessorTests
{
    private static AnimationChainSave ChainOf(int frames)
    {
        var chain = new AnimationChainSave();
        for (int i = 0; i < frames; i++) chain.Frames.Add(new AnimationFrameSave { TextureName = "old.png" });
        return chain;
    }

    private static string Achx => TestPaths.Abs("Project", "Animations", "Player.achx");

    [Fact]
    public void ComputePngDrop_OnFrame_UpdatesThatFrame_WithARelativeForwardSlashPath()
    {
        var chain = ChainOf(2);

        var result = TextureDropProcessor.ComputePngDrop(
            chain, chain.Frames[0], TestPaths.Abs("Project", "Content", "Sprites", "Hero.png"), Achx, createFrameOnCtrl: false);

        Assert.Equal((TextureDropResult.UpdatedFrame, "../Content/Sprites/Hero.png"), result);
        Assert.All(chain.Frames, frame => Assert.Equal("old.png", frame.TextureName));
    }

    [Theory]
    [InlineData(2, false, TextureDropResult.UpdatedChainFrames)]
    [InlineData(1, true, TextureDropResult.CreatedFrame)]
    [InlineData(0, false, TextureDropResult.CreatedFrame)]
    public void ComputePngDrop_OnChain_UpdatesItsFrames_OrCreatesOneOnCtrlOrWhenEmpty(int frames, bool ctrl, TextureDropResult expected)
    {
        var result = TextureDropProcessor.ComputePngDrop(
            ChainOf(frames), null, TestPaths.Abs("Project", "Content", "Shared.png"), Achx, ctrl);

        Assert.Equal((expected, "../Content/Shared.png"), result);
    }

    [Fact]
    public void ComputePngDrop_SameDirectory_ReturnsFileNameOnly()
    {
        var chain = ChainOf(1);

        var (_, path) = TextureDropProcessor.ComputePngDrop(
            chain, chain.Frames[0], TestPaths.Abs("Project", "Tex.png"), TestPaths.Abs("Project", "Player.achx"), createFrameOnCtrl: false);

        Assert.Equal("Tex.png", path);
    }

    // Drag-drop reads the OS drag payload's real-case path directly, never the lowercased
    // WireframeControl.LoadedTexturePath cache key (#941).
    [Fact]
    public void ComputePngDrop_MixedCaseOnDiskTexture_PreservesExactCase()
    {
        var result = TextureDropProcessor.ComputePngDrop(
            ChainOf(0), null, TestPaths.Abs("Project", "Content", "Items.PNG"), Achx, createFrameOnCtrl: false);

        Assert.Equal((TextureDropResult.CreatedFrame, "../Content/Items.PNG"), result);
    }

    // An unsaved project has no folder to be relative to, so the absolute path is kept.
    [Theory]
    [InlineData(0, TextureDropResult.CreatedFrame)]
    [InlineData(1, TextureDropResult.UpdatedChainFrames)]
    public void ComputePngDrop_NullAchx_OnChain_UsesAbsolutePath(int frames, TextureDropResult expected)
    {
        var dropped = TestPaths.AltAbs("Downloads", "sprite.png");

        Assert.Equal((expected, dropped), TextureDropProcessor.ComputePngDrop(ChainOf(frames), null, dropped, null, false));
    }

    [Fact]
    public void ComputePngDrop_NullAchx_OnFrame_UsesAbsolutePath()
    {
        var dropped = TestPaths.AltAbs("Downloads", "sprite.png");

        Assert.Equal((TextureDropResult.UpdatedFrame, dropped),
            TextureDropProcessor.ComputePngDrop(null, new AnimationFrameSave(), dropped, null, false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComputePngDrop_NonPng_IsIgnored(bool saved)
    {
        var result = TextureDropProcessor.ComputePngDrop(
            ChainOf(1), null, TestPaths.Abs("Project", "Content", "NotTexture.jpg"), saved ? Achx : null, createFrameOnCtrl: false);

        Assert.Equal((TextureDropResult.NotApplied, (string?)null), result);
    }

    [Fact]
    public void ComputePngDrop_NullChainAndFrame_IsIgnored() =>
        Assert.Equal((TextureDropResult.NotApplied, (string?)null),
            TextureDropProcessor.ComputePngDrop(null, null, TestPaths.AltAbs("Downloads", "sprite.png"), null, false));
}
