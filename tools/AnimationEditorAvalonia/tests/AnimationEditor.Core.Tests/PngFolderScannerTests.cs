using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class PngFolderScannerTests
{
    [Fact]
    public void Scan_EmptyFolder_ReturnsEmpty()
    {
        using var dir = new TempDirectory();
        Assert.Empty(PngFolderScanner.Scan(dir.Path));
    }

    [Fact]
    public void Scan_MixedExtensions_ReturnsOnlyPngCaseInsensitive()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(Path.Combine(dir.Path, "hero.png"), "png");
        File.WriteAllText(Path.Combine(dir.Path, "enemy.PNG"), "png");
        File.WriteAllText(Path.Combine(dir.Path, "notes.txt"), "txt");
        File.WriteAllText(Path.Combine(dir.Path, "photo.jpg"), "jpg");

        var groups = PngFolderScanner.Scan(dir.Path);

        Assert.Single(groups);
        Assert.Equal("(root)", groups[0].Label);
        Assert.Equal(2, groups[0].Files.Count);
        Assert.Equal(["enemy.PNG", "hero.png"], groups[0].Files.Select(f => f.FileName).OrderBy(n => n).ToArray());
    }

    [Fact]
    public void Scan_NestedSubfolders_GroupsBySubfolder()
    {
        using var dir = new TempDirectory();
        var spritesDir = Path.Combine(dir.Path, "Sprites");
        Directory.CreateDirectory(spritesDir);
        File.WriteAllText(Path.Combine(dir.Path, "root.png"), "png");
        File.WriteAllText(Path.Combine(spritesDir, "hero.png"), "png");

        var groups = PngFolderScanner.Scan(dir.Path);

        Assert.Equal(2, groups.Count);
        Assert.Equal("(root)", groups[0].Label);
        Assert.Equal("root.png", groups[0].Files.Single().FileName);
        Assert.Equal("Sprites", groups[1].Label);
        Assert.Equal("hero.png", groups[1].Files.Single().FileName);
    }

    [Fact]
    public void Scan_NullFolder_ReturnsEmpty()
    {
        Assert.Empty(PngFolderScanner.Scan(null));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ae-png-scan-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
