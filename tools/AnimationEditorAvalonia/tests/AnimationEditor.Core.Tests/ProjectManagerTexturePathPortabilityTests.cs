using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

// #936: a texture set before the project's first save has no achx folder to relativize
// against, so ComputeStorePath (correctly) leaves it absolute -- but nothing ever re-resolved
// that absolute path once a folder became known, so it stayed absolute (and OS-native-slashed)
// forever, mixed in with properly-relative forward-slashed entries from other frames. Saving
// must self-heal: every frame's TextureName is re-normalized against the save target's folder
// each time SaveAnimationChainList(string) runs.
//
// These tests build the absolute TextureName with Path.Combine (OS-native separator) rather
// than a forced backslash literal: they round-trip through a real achx directory computed by
// System.IO.Path on whichever OS the test runs on, so an artificially backslash-mangled path
// would not actually be "absolute" on Linux and would exercise the wrong code path. The
// separator-normalization behavior itself (backslash literals -> forward slash) is covered
// platform-independently in TexturePathHelperTests using drive-letter literals.
public class ProjectManagerTexturePathPortabilityTests
{
    [Fact]
    public void SaveAnimationChainList_AbsoluteTextureNameInSaveFolder_RewrittenToSimpleRelative()
    {
        var pm = new ProjectManager();
        // These fixtures reference textures that don't exist on disk -- this file is about
        // TextureName path rewriting, not on-disk pixel-coordinate conversion (#1135), so stay in UV.
        pm.OnDiskCoordinateType = TextureCoordinateType.UV;
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Props.achx");

        var frame = new AnimationFrameSave { TextureName = Path.Combine(dir.Path, "items.png") };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        var reloaded = AnimationChainListSave.FromFile(achxPath);
        Assert.Equal("items.png", reloaded.AnimationChains.Single().Frames.Single().TextureName);
    }

    [Fact]
    public void SaveAnimationChainList_AbsoluteTextureNameInSubfolder_RewrittenToForwardSlashRelative()
    {
        var pm = new ProjectManager();
        // These fixtures reference textures that don't exist on disk -- this file is about
        // TextureName path rewriting, not on-disk pixel-coordinate conversion (#1135), so stay in UV.
        pm.OnDiskCoordinateType = TextureCoordinateType.UV;
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Props.achx");
        var absoluteTexture = Path.Combine(dir.Path, "Sprites", "items.png");

        var frame = new AnimationFrameSave { TextureName = absoluteTexture };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        var reloaded = AnimationChainListSave.FromFile(achxPath);
        Assert.Equal("Sprites/items.png", reloaded.AnimationChains.Single().Frames.Single().TextureName);
    }

    [Fact]
    public void SaveAnimationChainList_MixedAbsoluteAndRelativeTextureNames_AllForwardSlashAfterSave()
    {
        var pm = new ProjectManager();
        // These fixtures reference textures that don't exist on disk -- this file is about
        // TextureName path rewriting, not on-disk pixel-coordinate conversion (#1135), so stay in UV.
        pm.OnDiskCoordinateType = TextureCoordinateType.UV;
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Props.achx");

        var frameAbsolute = new AnimationFrameSave { TextureName = Path.Combine(dir.Path, "items.png") };
        var frameRelative = new AnimationFrameSave { TextureName = "items.png" };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frameAbsolute);
        chain.Frames.Add(frameRelative);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        var reloadedFrames = AnimationChainListSave.FromFile(achxPath).AnimationChains.Single().Frames;
        Assert.All(reloadedFrames, f => Assert.Equal("items.png", f.TextureName));
    }

    [Fact]
    public void SaveAnimationChainList_RewritesInMemoryTextureName_NotJustSavedFile()
    {
        var pm = new ProjectManager();
        // These fixtures reference textures that don't exist on disk -- this file is about
        // TextureName path rewriting, not on-disk pixel-coordinate conversion (#1135), so stay in UV.
        pm.OnDiskCoordinateType = TextureCoordinateType.UV;
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Props.achx");

        var frame = new AnimationFrameSave { TextureName = Path.Combine(dir.Path, "items.png") };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        // The next save (or any in-memory consumer, e.g. the property panel) must see the
        // healed relative path too, not just the bytes written to disk.
        Assert.Equal("items.png", frame.TextureName);
    }

    [Fact]
    public void SaveAnimationChainList_TextureNameCasePreserved_WhenRewrittenToRelative()
    {
        var pm = new ProjectManager();
        // These fixtures reference textures that don't exist on disk -- this file is about
        // TextureName path rewriting, not on-disk pixel-coordinate conversion (#1135), so stay in UV.
        pm.OnDiskCoordinateType = TextureCoordinateType.UV;
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Props.achx");

        var frame = new AnimationFrameSave { TextureName = Path.Combine(dir.Path, "MyStuff", "Hero.PNG") };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        var reloaded = AnimationChainListSave.FromFile(achxPath);
        Assert.Equal("MyStuff/Hero.PNG", reloaded.AnimationChains.Single().Frames.Single().TextureName);
    }

    [Fact]
    public void SaveAnimationChainList_AlreadyRelativeBackslashTextureName_NormalizedToForwardSlash()
    {
        var pm = new ProjectManager();
        // These fixtures reference textures that don't exist on disk -- this file is about
        // TextureName path rewriting, not on-disk pixel-coordinate conversion (#1135), so stay in UV.
        pm.OnDiskCoordinateType = TextureCoordinateType.UV;
        using var dir = new TestHelpers.TempDir();
        var achxPath = Path.Combine(dir.Path, "Props.achx");

        var frame = new AnimationFrameSave { TextureName = @"Sprites\items.png" };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(chain);

        pm.SaveAnimationChainList(achxPath);

        var reloaded = AnimationChainListSave.FromFile(achxPath);
        Assert.Equal("Sprites/items.png", reloaded.AnimationChains.Single().Frames.Single().TextureName);
    }
}
