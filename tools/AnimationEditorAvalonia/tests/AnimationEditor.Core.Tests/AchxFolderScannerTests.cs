using AnimationEditor.Core.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AchxFolderScannerTests
{
    [Fact]
    public async Task ScanAsync_EmptyFolder_ReturnsEmpty()
    {
        var root = new FakeEditorFolder("Content");

        var entries = await AchxFolderScanner.ScanAsync(root);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task ScanAsync_MixedExtensions_ReturnsOnlyAchxCaseInsensitive()
    {
        var root = new FakeEditorFolder("Content");
        root.Files.Add(new FakeEditorFile("hero.achx"));
        root.Files.Add(new FakeEditorFile("enemy.ACHX"));
        root.Files.Add(new FakeEditorFile("notes.txt"));
        root.Files.Add(new FakeEditorFile("hero.png"));

        var entries = await AchxFolderScanner.ScanAsync(root);

        Assert.Equal(["enemy.ACHX", "hero.achx"], entries.Select(e => e.FileName).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task ScanAsync_NestedSubfolders_ReturnsRelativePathsAndParentFolder()
    {
        var root = new FakeEditorFolder("Content");
        var sprites = new FakeEditorFolder("Sprites");
        sprites.Files.Add(new FakeEditorFile("hero.achx"));
        root.Subfolders.Add(sprites);
        root.Files.Add(new FakeEditorFile("root.achx"));

        var entries = await AchxFolderScanner.ScanAsync(root);

        Assert.Equal(2, entries.Count);
        var rootEntry = entries.Single(e => e.FileName == "root.achx");
        Assert.Equal("root.achx", rootEntry.RelativePath);
        Assert.Same(root, rootEntry.ParentFolder);

        var nestedEntry = entries.Single(e => e.FileName == "hero.achx");
        Assert.Equal("Sprites/hero.achx", nestedEntry.RelativePath);
        Assert.Same(sprites, nestedEntry.ParentFolder);
    }
}
