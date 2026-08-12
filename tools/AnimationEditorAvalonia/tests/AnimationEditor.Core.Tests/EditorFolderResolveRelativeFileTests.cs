using AnimationEditor.Core.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #839: IEditorFolder.ResolveRelativeFileAsync's default implementation (used by any
// IEditorFolder that doesn't override it, e.g. the browser build's folder adapters) walks
// subfolders by name and stops at a ".." segment, since IEditorFolder has no way to reach a
// parent folder. DiskEditorFolder overrides it with full System.IO resolution (see
// DiskEditorFolderResolveRelativeFileTests in AnimationEditor.App.Tests).
public class EditorFolderResolveRelativeFileTests
{
    // Default interface methods are only visible through the interface type -- calling one on a
    // variable statically typed as the concrete class (as test setup naturally does, to reach
    // Files/Subfolders) does not compile. This helper is the one place that upcasts.
    private static Task<IEditorFile?> Resolve(IEditorFolder folder, string relativePath) =>
        folder.ResolveRelativeFileAsync(relativePath);

    [Fact]
    public async Task ResolveRelativeFileAsync_SameFolderFile_ReturnsFile()
    {
        var root = new FakeEditorFolder("Content");
        var png = new FakeEditorFile("hero.png");
        root.Files.Add(png);

        var result = await Resolve(root, "hero.png");

        Assert.Same(png, result);
    }

    [Fact]
    public async Task ResolveRelativeFileAsync_FileInSubfolder_WalksDownAndReturnsFile()
    {
        var root = new FakeEditorFolder("Content");
        var textures = new FakeEditorFolder("Textures");
        var png = new FakeEditorFile("hero.png");
        textures.Files.Add(png);
        root.Subfolders.Add(textures);

        var result = await Resolve(root, "Textures/hero.png");

        Assert.Same(png, result);
    }

    [Fact]
    public async Task ResolveRelativeFileAsync_ParentDirectorySegment_ReturnsNull()
    {
        var root = new FakeEditorFolder("Content");

        var result = await Resolve(root, "../Shared/hero.png");

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveRelativeFileAsync_FileDoesNotExist_ReturnsNull()
    {
        var root = new FakeEditorFolder("Content");

        var result = await Resolve(root, "missing.png");

        Assert.Null(result);
    }
}
