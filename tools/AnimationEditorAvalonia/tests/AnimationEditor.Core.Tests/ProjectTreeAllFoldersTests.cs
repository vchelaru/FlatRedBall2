using AnimationEditor.Core.IO;
using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Issue #1332: the Project tab's "Show all folders" toggle.</summary>
public class ProjectTreeAllFoldersTests
{
    [Fact]
    public async Task ScanProjectAsync_FolderWithoutAnimationFiles_IsListedInFolderPaths()
    {
        var root = new FakeEditorFolder("Content");
        var sprites = new FakeEditorFolder("Sprites");
        sprites.Subfolders.Add(new FakeEditorFolder("Enemies"));
        root.Subfolders.Add(sprites);

        var scan = await AchxFolderScanner.ScanProjectAsync(root);

        scan.Files.ShouldBeEmpty();
        scan.FolderPaths.ShouldBe(new[] { "Sprites", "Sprites/Enemies" });
    }

    [Fact]
    public void Build_FolderPathsWithoutFiles_AddsEmptyFolderNodesAlongsideFileFolders()
    {
        var hero = new AchxFileEntry(new FakeEditorFile("hero.achx"), new FakeEditorFolder("Sprites"), "Sprites/hero.achx");

        var tree = AchxFolderTreeBuilder.Build(new[] { hero }, new[] { "Sprites", "Sprites/Enemies", "Audio" });

        tree.Select(n => n.Name).ShouldBe(new[] { "Audio", "Sprites" });
        tree[0].IsFolder.ShouldBeTrue();
        tree[0].Children.ShouldBeEmpty();
        tree[1].Children.Select(n => n.Name).ShouldBe(new[] { "Enemies", "hero.achx" });
        tree[1].Children[0].RelativePath.ShouldBe("Sprites/Enemies");
    }

    [Fact]
    public void Select_ShowAllFoldersOff_ReturnsNoFolders()
    {
        var folders = ProjectTreeFolderFilter.Select(new[] { "Audio" }, showAllFolders: false, excludeBinObj: false, searchQuery: "");

        folders.ShouldBeEmpty();
    }

    [Fact]
    public void Select_ShowAllFoldersOn_DropsBinObjWhenExcludedAndEverythingWhileSearching()
    {
        var all = new[] { "Audio", "bin", "bin/Debug", "Sprites/obj" };

        ProjectTreeFolderFilter.Select(all, showAllFolders: true, excludeBinObj: true, searchQuery: "")
            .ShouldBe(new[] { "Audio" });
        ProjectTreeFolderFilter.Select(all, showAllFolders: true, excludeBinObj: false, searchQuery: "")
            .ShouldBe(all);
        // A search shows only matching files; empty folders would bury the matches.
        ProjectTreeFolderFilter.Select(all, showAllFolders: true, excludeBinObj: false, searchQuery: "hero")
            .ShouldBeEmpty();
    }
}
