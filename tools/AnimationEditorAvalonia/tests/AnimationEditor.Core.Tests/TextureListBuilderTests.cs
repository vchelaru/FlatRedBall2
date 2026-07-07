using AnimationEditor.Core.Data;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TextureListBuilderTests
{
    private static AnimationChainListSave EmptyAcls() => new();

    private static AnimationChainListSave AclsWithTextures(params string?[] textureNames)
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Chain" };
        foreach (var t in textureNames)
            chain.Frames.Add(new AnimationFrameSave { TextureName = t! });
        acls.AnimationChains.Add(chain);
        return acls;
    }

    // ── Null / empty ─────────────────────────────────────────────────────

    [Fact]
    public void NullAcls_ReturnsEmpty()
    {
        var result = TextureListBuilder.GetAvailableTextures(null);
        Assert.Empty(result);
    }

    [Fact]
    public void EmptyAcls_ReturnsEmpty()
    {
        var result = TextureListBuilder.GetAvailableTextures(EmptyAcls());
        Assert.Empty(result);
    }

    // ── Single texture ────────────────────────────────────────────────────

    [Fact]
    public void SingleTexture_ReturnedInList()
    {
        var acls = AclsWithTextures("hero.png");
        var result = TextureListBuilder.GetAvailableTextures(acls);
        Assert.Single(result);
        Assert.Equal("hero.png", result[0]);
    }

    // ── Deduplication ─────────────────────────────────────────────────────

    [Fact]
    public void DuplicateTextures_DeduplicatedCaseInsensitively()
    {
        var acls = AclsWithTextures("hero.png", "Hero.png", "HERO.PNG");
        var result = TextureListBuilder.GetAvailableTextures(acls);
        Assert.Single(result);
    }

    // ── Multiple distinct ─────────────────────────────────────────────────

    [Fact]
    public void MultipleDistinctTextures_AllReturned()
    {
        var acls = AclsWithTextures("apple.png", "banana.png", "cherry.png");
        var result = TextureListBuilder.GetAvailableTextures(acls);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void MultipleDistinctTextures_ReturnedSorted()
    {
        var acls = AclsWithTextures("cherry.png", "apple.png", "banana.png");
        var result = TextureListBuilder.GetAvailableTextures(acls);
        Assert.Equal("apple.png", result[0]);
        Assert.Equal("banana.png", result[1]);
        Assert.Equal("cherry.png", result[2]);
    }

    // ── Null / empty texture names ────────────────────────────────────────

    [Fact]
    public void NullTextureNames_Ignored()
    {
        var acls = AclsWithTextures(null, "valid.png", null);
        var result = TextureListBuilder.GetAvailableTextures(acls);
        Assert.Single(result);
        Assert.Equal("valid.png", result[0]);
    }

    [Fact]
    public void EmptyTextureNames_Ignored()
    {
        var acls = AclsWithTextures("", " ", "valid.png");
        var result = TextureListBuilder.GetAvailableTextures(acls);
        Assert.Single(result);
    }

    // ── Multiple chains ───────────────────────────────────────────────────

    [Fact]
    public void TexturesAcrossChains_AllCollected()
    {
        var acls = new AnimationChainListSave();
        var c1 = new AnimationChainSave { Name = "Walk" };
        c1.Frames.Add(new AnimationFrameSave { TextureName = "walk.png" });
        var c2 = new AnimationChainSave { Name = "Jump" };
        c2.Frames.Add(new AnimationFrameSave { TextureName = "jump.png" });
        acls.AnimationChains.Add(c1);
        acls.AnimationChains.Add(c2);

        var result = TextureListBuilder.GetAvailableTextures(acls);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void TexturesAcrossChains_Deduplicated()
    {
        var acls = new AnimationChainListSave();
        var c1 = new AnimationChainSave { Name = "Walk" };
        c1.Frames.Add(new AnimationFrameSave { TextureName = "shared.png" });
        var c2 = new AnimationChainSave { Name = "Run" };
        c2.Frames.Add(new AnimationFrameSave { TextureName = "shared.png" });
        acls.AnimationChains.Add(c1);
        acls.AnimationChains.Add(c2);

        var result = TextureListBuilder.GetAvailableTextures(acls);
        Assert.Single(result);
    }

    // ── GetFirstTextureName (borrow for empty chain — issue #618) ─────────

    [Fact]
    public void GetFirstTextureName_NullAcls_ReturnsNull()
    {
        Assert.Null(TextureListBuilder.GetFirstTextureName(null));
    }

    [Fact]
    public void GetFirstTextureName_NoTexturedFrames_ReturnsNull()
    {
        // Empty/whitespace texture names and frameless chains contribute nothing.
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Empty" });
        acls.AnimationChains.Add(AclsWithTextures("", " ").AnimationChains[0]);

        Assert.Null(TextureListBuilder.GetFirstTextureName(acls));
    }

    [Fact]
    public void GetFirstTextureName_EmptyChainThenTexturedChain_ReturnsFirstTextureInListOrder()
    {
        // The #618 scenario: the selected (first) chain has no frames, so the texture
        // must be borrowed from a later chain — the first one referenced, in list order.
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Empty" });
        var textured = new AnimationChainSave { Name = "Walk" };
        textured.Frames.Add(new AnimationFrameSave { TextureName = "hero.png" });
        textured.Frames.Add(new AnimationFrameSave { TextureName = "later.png" });
        acls.AnimationChains.Add(textured);

        Assert.Equal("hero.png", TextureListBuilder.GetFirstTextureName(acls));
    }
}
