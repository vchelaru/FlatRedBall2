using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using System.IO;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectManagerLoadTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // ── Missing file ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadAnimationChain_MissingFile_ThrowsFileNotFoundException()
    {
        var pm = new ProjectManager();

        Assert.Throws<FileNotFoundException>(
            () => pm.LoadAnimationChain(new FilePath(TestPaths.Abs("does", "not", "exist.achx"))));
    }

    [Fact]
    public void LoadAnimationChain_MissingFile_DoesNotChangeAnimationChainListSave()
    {
        var pm = new ProjectManager();
        pm.AnimationChainListSave = null;

        try { pm.LoadAnimationChain(new FilePath(TestPaths.Abs("does", "not", "exist.achx"))); }
        catch (FileNotFoundException) { }

        Assert.Null(pm.AnimationChainListSave);
    }

    // ── Corrupt file ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadAnimationChain_CorruptFile_ThrowsException()
    {
        var pm = new ProjectManager();
        var path = Path.Combine(_dir.Path, "corrupt.achx");
        File.WriteAllText(path, "this is not valid xml");

        Assert.ThrowsAny<Exception>(
            () => pm.LoadAnimationChain(new FilePath(path)));
    }

    [Fact]
    public void LoadAnimationChain_CorruptFile_DoesNotChangeAnimationChainListSave()
    {
        var pm = new ProjectManager();
        var sentinel = new AnimationChainListSave();
        pm.AnimationChainListSave = sentinel;
        var path = Path.Combine(_dir.Path, "corrupt2.achx");
        File.WriteAllText(path, "this is not valid xml");

        try { pm.LoadAnimationChain(new FilePath(path)); }
        catch { }

        Assert.Same(sentinel, pm.AnimationChainListSave);
    }

    // ── Git conflict markers ──────────────────────────────────────────────────

    [Fact]
    public void LoadAnimationChain_ConflictMarkerFile_ThrowsInvalidDataException()
    {
        var pm = new ProjectManager();
        var path = Path.Combine(_dir.Path, "conflict.achx");
        File.WriteAllText(path,
            "<<<<<<< HEAD\n<?xml version=\"1.0\"?><AnimationChainList />\n=======\n<?xml version=\"1.0\"?><AnimationChainList />\n>>>>>>>");

        Assert.Throws<System.IO.InvalidDataException>(
            () => pm.LoadAnimationChain(new FilePath(path)));
    }

    [Fact]
    public void LoadAnimationChain_ConflictMarkerFile_MessageMentionsGitConflict()
    {
        var pm = new ProjectManager();
        var path = Path.Combine(_dir.Path, "conflict2.achx");
        File.WriteAllText(path,
            "<<<<<<< HEAD\n<?xml version=\"1.0\"?><AnimationChainList />\n=======\n<?xml version=\"1.0\"?><AnimationChainList />\n>>>>>>>");

        var ex = Assert.Throws<System.IO.InvalidDataException>(
            () => pm.LoadAnimationChain(new FilePath(path)));
        Assert.Contains("conflict", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadAnimationChain_ConflictMarkerFile_DoesNotChangeAnimationChainListSave()
    {
        var pm = new ProjectManager();
        var sentinel = new AnimationChainListSave();
        pm.AnimationChainListSave = sentinel;
        var path = Path.Combine(_dir.Path, "conflict3.achx");
        File.WriteAllText(path,
            "<<<<<<< HEAD\n<?xml version=\"1.0\"?><AnimationChainList />\n=======\n<?xml version=\"1.0\"?><AnimationChainList />\n>>>>>>>");

        try { pm.LoadAnimationChain(new FilePath(path)); }
        catch { }

        Assert.Same(sentinel, pm.AnimationChainListSave);
    }

    // ── Pre-parsed content, no file on disk (#535 M2: browser has no filesystem) ────────────

    [Fact]
    public void LoadAnimationChain_PreParsedGivenAndFileDoesNotExist_DoesNotThrow()
    {
        var pm = new ProjectManager();
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };

        pm.LoadAnimationChain(new FilePath(TestPaths.Abs("bundled", "sample.achx")), preParsed);
    }

    [Fact]
    public void LoadAnimationChain_PreParsedGivenAndFileDoesNotExist_SetsAnimationChainListSaveToPreParsed()
    {
        var pm = new ProjectManager();
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };

        pm.LoadAnimationChain(new FilePath(TestPaths.Abs("bundled", "sample.achx")), preParsed);

        Assert.Same(preParsed, pm.AnimationChainListSave);
    }

    // ── Pre-parsed Pixel content with known texture sizes, no file on disk ─────────────────
    // (#535 M3 follow-up: browser Open Folder/drag-drop has decoded textures in memory but no
    // filesystem to read a PNG header from -- LoadAnimationChain must accept the already-known
    // sizes instead of silently skipping the pixel-to-UV conversion.)

    [Fact]
    public void LoadAnimationChain_PixelCoordinatesWithKnownTextureSizes_ConvertsToUvWithoutReadingDisk()
    {
        var pm = new ProjectManager();
        var frame = new AnimationFrameSave
        {
            TextureName = "sprite.png",
            LeftCoordinate = 0f,
            RightCoordinate = 32f,
            TopCoordinate = 0f,
            BottomCoordinate = 64f,
        };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        preParsed.AnimationChains.Add(chain);

        var knownTextureSizes = new Dictionary<string, (int Width, int Height)>
        {
            ["sprite.png"] = (32, 64),
        };

        // "sprite.png" is never written to disk -- if this fell back to reading a PNG header
        // from `TestPaths.Abs("browser", ...)` it would fail and skip the conversion.
        pm.LoadAnimationChain(
            new FilePath(TestPaths.Abs("browser", "does-not-exist.achx")),
            preParsed,
            knownTextureSizes);

        Assert.Equal(0f, frame.LeftCoordinate);
        Assert.Equal(1f, frame.RightCoordinate);
        Assert.Equal(0f, frame.TopCoordinate);
        Assert.Equal(1f, frame.BottomCoordinate);
    }

    [Fact]
    public void LoadAnimationChain_PixelCoordinatesWithKnownTextureSizes_SetsOnDiskCoordinateTypeToPixel()
    {
        var pm = new ProjectManager();
        var frame = new AnimationFrameSave { TextureName = "sprite.png" };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        preParsed.AnimationChains.Add(chain);

        var knownTextureSizes = new Dictionary<string, (int Width, int Height)>
        {
            ["sprite.png"] = (32, 64),
        };

        pm.LoadAnimationChain(
            new FilePath(TestPaths.Abs("browser", "does-not-exist2.achx")),
            preParsed,
            knownTextureSizes);

        Assert.Equal(TextureCoordinateType.Pixel, pm.OnDiskCoordinateType);
    }

    // ── TextureName with a subfolder prefix, known sizes keyed by bare filename ────────────
    // (Browser folder enumeration is non-recursive, so BrowserProjectLoader/BrowserFolderWatcher
    // key knownTextureSizes by bare file name. A .achx authored with FileRelativeTextures can
    // still reference "Textures/sprite.png" -- the lookup must fall back to matching by bare
    // filename instead of silently missing and falling through to a disk read that always fails
    // in the browser.)

    [Fact]
    public void LoadAnimationChain_TextureNameHasSubfolderPrefix_StillResolvesFromBareFilenameKnownSizes()
    {
        var pm = new ProjectManager();
        var frame = new AnimationFrameSave
        {
            TextureName = "Textures/sprite.png",
            LeftCoordinate = 0f,
            RightCoordinate = 32f,
            TopCoordinate = 0f,
            BottomCoordinate = 64f,
        };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        preParsed.AnimationChains.Add(chain);

        // Keyed by bare filename, as the browser's non-recursive folder enumeration produces.
        var knownTextureSizes = new Dictionary<string, (int Width, int Height)>
        {
            ["sprite.png"] = (32, 64),
        };

        pm.LoadAnimationChain(
            new FilePath(TestPaths.Abs("browser", "does-not-exist3.achx")),
            preParsed,
            knownTextureSizes);

        Assert.Equal(0f, frame.LeftCoordinate);
        Assert.Equal(1f, frame.RightCoordinate);
        Assert.Equal(0f, frame.TopCoordinate);
        Assert.Equal(1f, frame.BottomCoordinate);
    }
}
