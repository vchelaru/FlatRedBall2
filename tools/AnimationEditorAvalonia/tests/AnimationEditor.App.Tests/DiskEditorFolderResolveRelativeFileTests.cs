using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

// Issue #839: DiskEditorFolder overrides IEditorFolder.ResolveRelativeFileAsync's default
// (subfolder-only) walk with real System.IO resolution, so it also handles ".." -- needed to
// resolve a frame's TextureName when it points outside the .achx's own folder.
public class DiskEditorFolderResolveRelativeFileTests
{
    private static Task<IEditorFile?> Resolve(IEditorFolder folder, string relativePath) =>
        folder.ResolveRelativeFileAsync(relativePath);

    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "AnimationEditorTests", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task ResolveRelativeFileAsync_ParentDirectorySegment_ResolvesRealFile()
    {
        var root = MakeTempDir();
        var achxDir = Path.Combine(root, "Chains");
        var sharedDir = Path.Combine(root, "Shared");
        Directory.CreateDirectory(achxDir);
        Directory.CreateDirectory(sharedDir);
        var texturePath = Path.Combine(sharedDir, "hero.png");
        File.WriteAllText(texturePath, "fake png bytes");

        var result = await Resolve(new DiskEditorFolder(achxDir), "../Shared/hero.png");

        Assert.NotNull(result);
        Assert.Equal("hero.png", result!.Name);
    }

    [Fact]
    public async Task ResolveRelativeFileAsync_FileDoesNotExist_ReturnsNull()
    {
        var root = MakeTempDir();

        var result = await Resolve(new DiskEditorFolder(root), "missing.png");

        Assert.Null(result);
    }
}
