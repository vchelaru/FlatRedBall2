using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

public class DiskEditorFolderTests
{
    [Fact]
    public async Task GetItemsAsync_ReturnsOnlyFilesDirectlyInFolder()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(Path.Combine(dir.Path, "hero.achx"), "achx");
        Directory.CreateDirectory(Path.Combine(dir.Path, "Sprites"));
        var folder = new DiskEditorFolder(dir.Path);

        var names = await CollectNamesAsync(folder.GetItemsAsync());

        Assert.Equal(["hero.achx"], names);
    }

    [Fact]
    public async Task GetSubfoldersAsync_ReturnsOnlyDirectoriesDirectlyInFolder()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(Path.Combine(dir.Path, "hero.achx"), "achx");
        Directory.CreateDirectory(Path.Combine(dir.Path, "Sprites"));
        var folder = new DiskEditorFolder(dir.Path);

        var names = await CollectNamesAsync(folder.GetSubfoldersAsync());

        Assert.Equal(["Sprites"], names);
    }

    private static async Task<List<string>> CollectNamesAsync(IAsyncEnumerable<IEditorFile> items)
    {
        var names = new List<string>();
        await foreach (var item in items) names.Add(item.Name);
        return names;
    }

    private static async Task<List<string>> CollectNamesAsync(IAsyncEnumerable<IEditorFolder> items)
    {
        var names = new List<string>();
        await foreach (var item in items) names.Add(item.Name);
        return names;
    }

    private sealed class TempDirectory : System.IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ae-disk-folder-" + System.Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
