using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AchxFolderTreeBuilderTests
{
    [Fact]
    public void Build_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(AchxFolderTreeBuilder.Build(Array.Empty<AchxFileEntry>()));
    }

    [Fact]
    public void Build_NestedFolders_BuildsCollapsibleHierarchy()
    {
        var root = new FakeEditorFolder("Content");
        var sprites = new FakeEditorFolder("Sprites");
        var rootFile = new FakeEditorFile("root.achx");
        var heroFile = new FakeEditorFile("hero.achx");
        var files = new[]
        {
            new AchxFileEntry(rootFile, root, "root.achx"),
            new AchxFileEntry(heroFile, sprites, "Sprites/hero.achx"),
        };

        var tree = AchxFolderTreeBuilder.Build(files);

        Assert.Equal(2, tree.Count);
        Assert.True(tree[0].IsFolder);
        Assert.Equal("Sprites", tree[0].Name);
        Assert.False(tree[0].Children[0].IsFolder);
        Assert.Equal("hero.achx", tree[0].Children[0].Name);
        Assert.Same(heroFile, tree[0].Children[0].Entry!.File);
        Assert.False(tree[1].IsFolder);
        Assert.Equal("root.achx", tree[1].Name);
        Assert.Same(rootFile, tree[1].Entry!.File);
    }

    [Fact]
    public void Build_RootFile_ReturnsSingleFileNode()
    {
        var root = new FakeEditorFolder("Content");
        var file = new FakeEditorFile("hero.achx");
        var files = new[] { new AchxFileEntry(file, root, "hero.achx") };

        var tree = AchxFolderTreeBuilder.Build(files);

        Assert.Single(tree);
        Assert.False(tree[0].IsFolder);
        Assert.Equal("hero.achx", tree[0].Name);
        Assert.Same(file, tree[0].Entry!.File);
    }

    // ── RelativePath (issue #841 follow-up: "Reveal in File Manager" on a folder row) ────────────────

    [Fact]
    public void Build_TopLevelFolder_RelativePathIsJustTheFolderName()
    {
        var sprites = new FakeEditorFolder("Sprites");
        var heroFile = new FakeEditorFile("hero.achx");
        var files = new[] { new AchxFileEntry(heroFile, sprites, "Sprites/hero.achx") };

        var tree = AchxFolderTreeBuilder.Build(files);

        Assert.Equal("Sprites", tree[0].RelativePath);
    }

    [Fact]
    public void Build_NestedFolder_RelativePathIncludesEveryAncestorSegment()
    {
        var enemies = new FakeEditorFolder("Enemies");
        var heroFile = new FakeEditorFile("hero.achx");
        var files = new[] { new AchxFileEntry(heroFile, enemies, "Sprites/Enemies/hero.achx") };

        var tree = AchxFolderTreeBuilder.Build(files);

        var spritesFolder = tree[0];
        Assert.Equal("Sprites", spritesFolder.RelativePath);
        var enemiesFolder = spritesFolder.Children[0];
        Assert.Equal("Sprites/Enemies", enemiesFolder.RelativePath);
    }

    [Fact]
    public void Build_File_RelativePathMatchesTheEntrysOwnRelativePath()
    {
        var root = new FakeEditorFolder("Content");
        var file = new FakeEditorFile("hero.achx");
        var files = new[] { new AchxFileEntry(file, root, "hero.achx") };

        var tree = AchxFolderTreeBuilder.Build(files);

        Assert.Equal("hero.achx", tree[0].RelativePath);
    }
}
