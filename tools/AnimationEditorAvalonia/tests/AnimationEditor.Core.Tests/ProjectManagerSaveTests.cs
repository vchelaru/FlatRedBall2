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

    // #1135: a frame whose texture size can't be resolved (missing/unbuilt PNG) must not silently
    // fall through the Pixel conversion loop -- the old behavior left that one frame's
    // coordinates in UV scale while still flipping acls.CoordinateType to Pixel, producing a file
    // whose header lies about that frame's coordinate scale. Saving to Pixel format is a persisted
    // artifact, so this must fail loudly instead, and it must not mutate any frame (including ones
    // whose texture *did* resolve) on the way to failing.
    [Fact]
    public void SaveAnimationChainList_Throws_WhenPixelSaveCannotResolveATextureSize()
    {
        File.WriteAllBytes(Path.Combine(_dir, "sprite.png"), MakeFakePng(32, 64));
        var pm = new ProjectManager();
        var knownFrame = new AnimationFrameSave
        {
            TextureName = "sprite.png",
            LeftCoordinate = 0f,
            RightCoordinate = 1f,
            TopCoordinate = 0f,
            BottomCoordinate = 1f,
        };
        var unresolvedFrame = new AnimationFrameSave
        {
            TextureName = "missing.png",
            LeftCoordinate = 0f,
            RightCoordinate = 0.5f,
            TopCoordinate = 0f,
            BottomCoordinate = 0.5f,
        };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(knownFrame);
        chain.Frames.Add(unresolvedFrame);
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        preParsed.AnimationChains.Add(chain);

        // Loaded as UV (no conversion needed on load), so the failure surfaces on save, not load.
        string path = AchxPath("mixed.achx");
        pm.LoadAnimationChain(new FilePath(path), preParsed);
        pm.OnDiskCoordinateType = TextureCoordinateType.Pixel;

        Assert.ThrowsAny<Exception>(() => pm.SaveAnimationChainList(path));

        // The failed save must not have mutated anything, including the frame that did resolve.
        Assert.Equal(TextureCoordinateType.UV, pm.AnimationChainListSave!.CoordinateType);
        Assert.Equal(0f, knownFrame.LeftCoordinate);
        Assert.Equal(1f, knownFrame.RightCoordinate);
        Assert.Equal(0f, unresolvedFrame.LeftCoordinate);
        Assert.Equal(0.5f, unresolvedFrame.RightCoordinate);
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

        // "items.png" isn't a real file on disk -- this test is about the ShapesSave omission,
        // not on-disk pixel-coordinate conversion (#1135), so stay in UV.
        ctx.ProjectManager.OnDiskCoordinateType = TextureCoordinateType.UV;

        string path = AchxPath("coin.achx");
        ctx.ProjectManager.SaveAnimationChainList(path);

        Assert.DoesNotContain("ShapeCollectionSave", File.ReadAllText(path));
    }
}
