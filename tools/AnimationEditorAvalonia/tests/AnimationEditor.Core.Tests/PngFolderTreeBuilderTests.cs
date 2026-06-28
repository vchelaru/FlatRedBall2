using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class PngFolderTreeBuilderTests
{
    [Fact]
    public void Build_RootFile_ReturnsSingleFileNode()
    {
        var files = new[] { new PngFileEntry(@"C:\proj\hero.png", "hero.png") };

        var tree = PngFolderTreeBuilder.Build(files);

        Assert.Single(tree);
        Assert.False(tree[0].IsFolder);
        Assert.Equal("hero.png", tree[0].Name);
        Assert.Equal(@"C:\proj\hero.png", tree[0].AbsolutePath);
    }

    [Fact]
    public void Build_NestedFolders_BuildsCollapsibleHierarchy()
    {
        var files = new[]
        {
            new PngFileEntry(@"C:\proj\root.png", "root.png"),
            new PngFileEntry(@"C:\proj\Sprites\hero.png", "Sprites/hero.png"),
            new PngFileEntry(@"C:\proj\Sprites\Enemies\goblin.png", "Sprites/Enemies/goblin.png"),
        };

        var tree = PngFolderTreeBuilder.Build(files);

        Assert.Equal(2, tree.Count);
        Assert.True(tree[0].IsFolder);
        Assert.Equal("Sprites", tree[0].Name);
        Assert.True(tree[0].Children[0].IsFolder);
        Assert.Equal("Enemies", tree[0].Children[0].Name);
        Assert.Equal("goblin.png", tree[0].Children[0].Children[0].Name);
        Assert.Equal("hero.png", tree[0].Children[1].Name);
        Assert.False(tree[1].IsFolder);
        Assert.Equal("root.png", tree[1].Name);
    }

    [Fact]
    public void Build_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(PngFolderTreeBuilder.Build(Array.Empty<PngFileEntry>()));
    }
}
