using System.IO;
using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TexturePathHelperTests
{
    // ── ComputeStorePath ──────────────────────────────────────────────────────

    [Fact]
    public void ComputeStorePath_TextureInAchxFolder_ReturnsSimpleRelative()
    {
        string storePath = TexturePathHelper.ComputeStorePath(
            TestPaths.Abs("Project", "Animations", "Hero.png"),
            TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal("Hero.png", storePath);
    }

    [Fact]
    public void ComputeStorePath_TextureInSubfolder_ReturnsSubfolderRelative()
    {
        string storePath = TexturePathHelper.ComputeStorePath(
            TestPaths.Abs("Project", "Animations", "Sprites", "Hero.png"),
            TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal("Sprites/Hero.png", storePath);
    }

    [Fact]
    public void ComputeStorePath_TextureInSiblingFolder_ReturnsDotDotRelative()
    {
        string storePath = TexturePathHelper.ComputeStorePath(
            TestPaths.Abs("Project", "Content", "Hero.png"),
            TestPaths.AbsDir("Project", "Animations"));

        // Key fix: sibling-folder textures must be stored as "../Content/Hero.png",
        // not as the absolute path.
        Assert.Equal("../Content/Hero.png", storePath);
    }

    [Fact]
    public void ComputeStorePath_TextureInGrandparentSiblingFolder_ReturnsDotDotRelative()
    {
        string storePath = TexturePathHelper.ComputeStorePath(
            TestPaths.Abs("OtherProject", "Content", "Hero.png"),
            TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal("../../OtherProject/Content/Hero.png", storePath);
    }

    [Fact]
    public void ComputeStorePath_EmptyAchxFolder_NormalizesToForwardSlashes()
    {
        // #936: an unsaved (untitled) project has no achx folder yet, so the path can't be made
        // relative -- but it must still come out forward-slash-normalized, not whatever raw
        // separators the OS handed back, so it doesn't mix with forward-slash relative paths
        // once other frames in the same file *do* have a folder to relativize against.
        string storePath = TexturePathHelper.ComputeStorePath(
            @"C:\TestRoot\Project\Content\Hero.png", string.Empty);

        Assert.Equal("C:/TestRoot/Project/Content/Hero.png", storePath);
    }

    [Fact]
    public void ComputeStorePath_EmptyAchxFolder_PreservesCase()
    {
        // Lower-casing here would break on case-sensitive filesystems (Linux) once the
        // project is later saved and the path is used to look up the actual file on disk.
        string storePath = TexturePathHelper.ComputeStorePath(
            @"C:\TestRoot\MyStuff\Hero.PNG", string.Empty);

        Assert.Equal("C:/TestRoot/MyStuff/Hero.PNG", storePath);
    }

    // ── NormalizeStoredTextureName ────────────────────────────────────────────

    [Fact]
    public void NormalizeStoredTextureName_AbsolutePathInAchxFolder_ReturnsRelative()
    {
        string normalized = TexturePathHelper.NormalizeStoredTextureName(
            TestPaths.Abs("Project", "Animations", "Hero.png"),
            TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal("Hero.png", normalized);
    }

    [Fact]
    public void NormalizeStoredTextureName_AlreadyRelativeWithBackslashes_ConvertsToForwardSlashes()
    {
        // #936: legacy/FRB1-authored entries can already be relative but use OS-native
        // backslashes. These must not be treated as absolute (which would wrongly resolve them
        // against the process's current directory) -- just slash-normalized in place.
        string normalized = TexturePathHelper.NormalizeStoredTextureName(
            @"Sprites\Hero.png", TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal("Sprites/Hero.png", normalized);
    }

    [Fact]
    public void NormalizeStoredTextureName_AlreadyForwardSlashRelative_ReturnedUnchanged()
    {
        string normalized = TexturePathHelper.NormalizeStoredTextureName(
            "Sprites/Hero.png", TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal("Sprites/Hero.png", normalized);
    }

    [Fact]
    public void NormalizeStoredTextureName_AbsolutePathOnDifferentDrive_StaysAbsoluteButForwardSlashed()
    {
        // A genuinely unrelated root only exists as a concept on Windows (separate drive
        // letters) -- Linux/macOS have a single filesystem root, so a fake "AltRoot" there is
        // still expressible relative to "TestRoot" via a chain of "../". Use explicit
        // drive-letter literals so this exercises the real can't-relativize case on every host
        // OS (FilePath's drive-letter check in IsRelative/IsAbsolute is string-based, not an OS
        // API call).
        string normalized = TexturePathHelper.NormalizeStoredTextureName(
            @"D:\AltRoot\External\Hero.png", @"C:\TestRoot\Project\Animations");

        Assert.Equal("D:/AltRoot/External/Hero.png", normalized);
    }

    [Fact]
    public void NormalizeStoredTextureName_EmptyTextureName_ReturnsEmpty()
    {
        string normalized = TexturePathHelper.NormalizeStoredTextureName(
            string.Empty, TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void NormalizeStoredTextureName_NullTextureName_ReturnsEmpty()
    {
        string normalized = TexturePathHelper.NormalizeStoredTextureName(
            null, TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal(string.Empty, normalized);
    }

    // ── ComputeDisplayPath ────────────────────────────────────────────────────

    [Fact]
    public void ComputeDisplayPath_AlreadyRelative_ReturnedUnchanged()
    {
        string display = TexturePathHelper.ComputeDisplayPath(
            "../Content/Hero.png",
            TestPaths.Abs("Project", "Animations", "Player.achx"));

        Assert.Equal("../Content/Hero.png", display);
    }

    [Fact]
    public void ComputeDisplayPath_AbsolutePathInSiblingFolder_ReturnsRelative()
    {
        string display = TexturePathHelper.ComputeDisplayPath(
            TestPaths.Abs("Project", "Content", "Hero.png"),
            TestPaths.Abs("Project", "Animations", "Player.achx"));

        // Absolute paths stored in the .achx should be displayed as relative.
        Assert.Equal("../Content/Hero.png", display);
    }

    [Fact]
    public void ComputeDisplayPath_AbsolutePathInAchxFolder_ReturnsSimpleRelative()
    {
        string display = TexturePathHelper.ComputeDisplayPath(
            TestPaths.Abs("Project", "Animations", "Hero.png"),
            TestPaths.Abs("Project", "Animations", "Player.achx"));

        Assert.Equal("Hero.png", display);
    }

    [Fact]
    public void ComputeDisplayPath_NullPath_ReturnsEmpty()
    {
        string display = TexturePathHelper.ComputeDisplayPath(
            null,
            TestPaths.Abs("Project", "Animations", "Player.achx"));

        Assert.Equal(string.Empty, display);
    }

    [Fact]
    public void ComputeDisplayPath_EmptyPath_ReturnsEmpty()
    {
        string display = TexturePathHelper.ComputeDisplayPath(
            string.Empty,
            TestPaths.Abs("Project", "Animations", "Player.achx"));

        Assert.Equal(string.Empty, display);
    }

    [Fact]
    public void ComputeDisplayPath_NullAchxPath_ReturnsAbsoluteUnchanged()
    {
        var absolute = TestPaths.Abs("Project", "Content", "Hero.png");
        string display = TexturePathHelper.ComputeDisplayPath(absolute, null);

        Assert.Equal(absolute, display);
    }

    // ── ResolveDisplayPath ────────────────────────────────────────────────────

    [Fact]
    public void ResolveDisplayPath_EmptyDisplayPath_ReturnsEmpty()
    {
        string resolved = TexturePathHelper.ResolveDisplayPath(string.Empty, TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal(string.Empty, resolved);
    }

    [Fact]
    public void ResolveDisplayPath_AbsolutePath_ReturnedUnchanged()
    {
        var absolute = TestPaths.Abs("Project", "Content", "Hero.png");
        string resolved = TexturePathHelper.ResolveDisplayPath(absolute, TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal(absolute, resolved);
    }

    [Fact]
    public void ResolveDisplayPath_RelativePathEmptyAchxFolder_ReturnedUnchanged()
    {
        string resolved = TexturePathHelper.ResolveDisplayPath("Hero.png", string.Empty);

        Assert.Equal("Hero.png", resolved);
    }

    [Fact]
    public void ResolveDisplayPath_SimpleRelativePath_ResolvesToAbsolute()
    {
        string resolved = TexturePathHelper.ResolveDisplayPath("Hero.png", TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal(
            Path.GetFullPath(Path.Combine(TestPaths.AbsDir("Project", "Animations"), "Hero.png")),
            resolved);
    }

    [Fact]
    public void ResolveDisplayPath_DotDotRelativePath_ResolvesToAbsolute()
    {
        string resolved = TexturePathHelper.ResolveDisplayPath("../Content/Hero.png", TestPaths.AbsDir("Project", "Animations"));

        Assert.Equal(
            Path.GetFullPath(Path.Combine(TestPaths.AbsDir("Project", "Animations"), "../Content/Hero.png")),
            resolved);
    }
}
