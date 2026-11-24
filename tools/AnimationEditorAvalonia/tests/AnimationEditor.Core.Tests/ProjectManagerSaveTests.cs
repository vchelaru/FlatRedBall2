using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectManagerSaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AnimationEditorCoreTests", Guid.NewGuid().ToString("N"));

    public ProjectManagerSaveTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    // Minimal 24-byte fake PNG (valid signature + IHDR width/height fields).
    private static byte[] MakeFakePng(int width, int height)
    {
        var b = new byte[24];
        b[0] = 0x89; b[1] = 0x50; b[2] = 0x4E; b[3] = 0x47;
        b[4] = 0x0D; b[5] = 0x0A; b[6] = 0x1A; b[7] = 0x0A;
        b[8] = 0; b[9] = 0; b[10] = 0; b[11] = 13;
        b[12] = 0x49; b[13] = 0x48; b[14] = 0x44; b[15] = 0x52;
        b[16] = (byte)(width >> 24); b[17] = (byte)(width >> 16);
        b[18] = (byte)(width >> 8);  b[19] = (byte)width;
        b[20] = (byte)(height >> 24); b[21] = (byte)(height >> 16);
        b[22] = (byte)(height >> 8);  b[23] = (byte)height;
        return b;
    }

    private string AchxPath(string name) => Path.Combine(_dir, name);

    [Fact]
    public void SaveAnimationChainList_Document_WritesThatDocumentInTheGivenFormat_NotTheCurrentOne()
    {
        var pm = new ProjectManager();
        var current = new AnimationChainListSave();
        current.AnimationChains.Add(new AnimationChainSave { Name = "Current" });
        pm.AnimationChainListSave = current;
        pm.OnDiskCoordinateType = TextureCoordinateType.Pixel;
        var other = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var otherChain = new AnimationChainSave { Name = "Other" };
        otherChain.Frames.Add(new AnimationFrameSave { TextureName = "sprite.png", LeftCoordinate = 0.25f, RightCoordinate = 0.5f });
        other.AnimationChains.Add(otherChain);
        string path = AchxPath("other.achx");

        pm.SaveAnimationChainList(other, path, TextureCoordinateType.UV);

        var saved = AnimationChainListSave.FromFile(path);
        Assert.Equal("Other", Assert.Single(saved.AnimationChains).Name);
        Assert.Equal(TextureCoordinateType.UV, saved.CoordinateType);
        Assert.Equal(0.25f, saved.AnimationChains[0].Frames[0].LeftCoordinate);
        Assert.Same(current, pm.AnimationChainListSave);
        Assert.Equal(0.25f, otherChain.Frames[0].LeftCoordinate);
    }

    // #1135: a Pixel save needs no texture size (#1952), so a missing texture no longer blocks one and every region is
    // written as it is. See the UV save below for the case that still needs one.
    [Fact]
    public void SaveAnimationChainList_PixelWithAMissingTexture_WritesTheRegions()
    {
        File.WriteAllBytes(Path.Combine(_dir, "sprite.png"), MakeFakePng(32, 64));
        var pm = new ProjectManager();
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "sprite.png", RightCoordinate = 32f, BottomCoordinate = 64f });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "missing.png", RightCoordinate = 16f, BottomCoordinate = 24f });
        pm.AnimationChainListSave = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        pm.AnimationChainListSave.AnimationChains.Add(chain);
        string path = AchxPath("mixed.achx");

        pm.SaveAnimationChainList(path);

        var saved = AnimationChainListSave.FromFile(path).AnimationChains[0].Frames;
        Assert.Equal((32f, 64f), (saved[0].RightCoordinate, saved[0].BottomCoordinate));
        Assert.Equal((16f, 24f), (saved[1].RightCoordinate, saved[1].BottomCoordinate));
    }

    // Converting to fractions needs every size, and a frame left in pixels under a UV header would be a lie (#1135). The
    // failed save must not have written the file or changed any frame, including the one whose texture did resolve.
    [Fact]
    public void SaveAnimationChainList_Throws_WhenUvSaveCannotResolveATextureSize()
    {
        File.WriteAllBytes(Path.Combine(_dir, "sprite.png"), MakeFakePng(32, 64));
        var pm = new ProjectManager { OnDiskCoordinateType = TextureCoordinateType.UV };
        var known = new AnimationFrameSave { TextureName = "sprite.png", RightCoordinate = 32f, BottomCoordinate = 64f };
        var unresolved = new AnimationFrameSave { TextureName = "missing.png", RightCoordinate = 16f, BottomCoordinate = 24f };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(known);
        chain.Frames.Add(unresolved);
        pm.AnimationChainListSave = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        pm.AnimationChainListSave.AnimationChains.Add(chain);
        string path = AchxPath("mixed.achx");

        Assert.Throws<InvalidOperationException>(() => pm.SaveAnimationChainList(path));

        Assert.False(File.Exists(path));
        Assert.Equal(TextureCoordinateType.Pixel, pm.AnimationChainListSave.CoordinateType);
        Assert.Equal((32f, 64f), (known.RightCoordinate, known.BottomCoordinate));
        Assert.Equal((16f, 24f), (unresolved.RightCoordinate, unresolved.BottomCoordinate));
    }

    [Fact]
    public void SaveAnimationChainList_WritesUvCoordinates_WhenOnDiskFormatIsUv()
    {
        var pm = new ProjectManager();
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        string path = AchxPath("uv.achx");
        pm.LoadAnimationChain(new FilePath(path), preParsed);

        pm.SaveAnimationChainList(path);

        Assert.Equal(TextureCoordinateType.UV, AnimationChainListSave.FromFile(path).CoordinateType);
    }

    // #937: a frame that never had a <ShapeCollectionSave> element on disk (ShapesSave == null
    // straight out of ParseXml/ParseJson) must round-trip through LoadAnimationChain and back out
    // to Save/SaveJson still null, not a force-populated empty ShapesSave. AnimationChainListSave's
    // own writer already omits the wrapper correctly for a null ShapesSave (see
    // Save_FrameWithNullShapes_OmitsShapeCollectionElement in AchxSerializationTests) -- this test
    // guards the AnimationEditor-side load path, which used to defeat that by unconditionally
    // doing `frame.ShapesSave ??= new ShapesSave()` on every loaded frame.
    [Fact]
    public void SaveAnimationChainList_OmitsShapeCollection_ForFrameThatNeverHadShapes()
    {
        var pm = new ProjectManager();
        var frame = new AnimationFrameSave { TextureName = "hero.png", ShapesSave = null };
        var chain = new AnimationChainSave { Name = "Idle" };
        chain.Frames.Add(frame);
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        preParsed.AnimationChains.Add(chain);
        string path = AchxPath("noshapes.achx");

        pm.LoadAnimationChain(new FilePath(path), preParsed);
        pm.SaveAnimationChainList(path);

        Assert.DoesNotContain("ShapeCollectionSave", File.ReadAllText(path));
    }

    // #937 follow-up: AppCommands.AddFrame/AddFrameFromPixelBounds create *new* frames with an
    // eagerly-allocated empty ShapesSave, a second source of the same bloat the load-path fix
    // above addressed -- confirmed live by opening the app, slicing a sprite sheet into frames,
    // and saving: every frame still got an empty shapesSave block, since these frames are never
    // loaded from disk at all. AddFrameCommand.Do() calls SaveCurrentAnimationChainList()
    // immediately, so the empty block is baked in on the very first save.
    [Fact]
    public void SaveAnimationChainList_OmitsShapeCollection_ForFrameCreatedViaAddFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Coin");

        ctx.AppCommands.AddFrame(chain, "items.png");

        string path = AchxPath("coin.achx");
        ctx.ProjectManager.SaveAnimationChainList(path);

        Assert.DoesNotContain("ShapeCollectionSave", File.ReadAllText(path));
    }
}
