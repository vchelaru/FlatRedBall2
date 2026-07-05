using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Covers <see cref="ProjectManager.ResolveFilesPanelRoot"/>. Root resolution, in order:
/// (1) if <see cref="AnimationChainListSave.ProjectFile"/> resolves to a directory that
/// exists — the referenced project file itself need not exist, since a relative link
/// authored against a source layout commonly goes stale once the .achx is copied to a
/// build-output folder — use that directory (or its <c>Content</c> subfolder, if present);
/// (2) otherwise walk up from the .achx's own folder to the nearest ancestor literally named
/// <c>Content</c> — the convention every FlatRedBall content pipeline copies assets into;
/// (3) otherwise the .achx's own folder.
/// </summary>
public class ProjectManagerFilesPanelRootTests
{
    [Fact]
    public void ResolveFilesPanelRoot_WhenNoFileLoaded_ReturnsNull()
    {
        var sut = new ProjectManager();

        Assert.Null(sut.ResolveFilesPanelRoot());
    }

    [Fact]
    public void ResolveFilesPanelRoot_WhenNoProjectFileAndNoContentAncestor_ReturnsAchxFolder()
    {
        using var temp = new TempDir();

        var sut = new ProjectManager
        {
            FileName = Path.Combine(temp.Path, "Anim.achx")
        };

        var root = sut.ResolveFilesPanelRoot();

        Assert.Equal(new FilePath(temp.Path + "/").Standardized, new FilePath(root!).Standardized);
    }

    [Fact]
    public void ResolveFilesPanelRoot_WhenAchxIsNestedUnderContent_ReturnsContentFolder()
    {
        using var temp = new TempDir();

        var contentDir = Path.Combine(temp.Path, "Content");
        var achxDir = Path.Combine(contentDir, "Entities", "Enemy");
        Directory.CreateDirectory(achxDir);
        File.WriteAllText(Path.Combine(contentDir, "ChibiCthulhuTiles.png"), "");

        var sut = new ProjectManager
        {
            FileName = Path.Combine(achxDir, "Byakhee.achx")
        };

        var root = sut.ResolveFilesPanelRoot();

        Assert.Equal(new FilePath(contentDir + "/").Standardized, new FilePath(root!).Standardized);
    }

    [Fact]
    public void ResolveFilesPanelRoot_MatchesContentFolderNameCaseInsensitively()
    {
        using var temp = new TempDir();

        var contentDir = Path.Combine(temp.Path, "CONTENT");
        var achxDir = Path.Combine(contentDir, "Sub");
        Directory.CreateDirectory(achxDir);

        var sut = new ProjectManager
        {
            FileName = Path.Combine(achxDir, "Anim.achx")
        };

        var root = sut.ResolveFilesPanelRoot();

        Assert.Equal(new FilePath(contentDir + "/").Standardized, new FilePath(root!).Standardized);
    }

    [Fact]
    public void ResolveFilesPanelRoot_WhenProjectFileExists_UsesItsContentFolder()
    {
        using var temp = new TempDir();

        var achxDir = Path.Combine(temp.Path, "Content", "Animations");
        Directory.CreateDirectory(achxDir);
        var projectDir = temp.Path;
        var contentDir = Path.Combine(projectDir, "Content");
        File.WriteAllText(Path.Combine(projectDir, "Game.gluj"), "<Project/>");

        var sut = new ProjectManager
        {
            FileName = Path.Combine(achxDir, "Anim.achx"),
            AnimationChainListSave = new AnimationChainListSave { ProjectFile = "../../Game.gluj" }
        };

        var root = sut.ResolveFilesPanelRoot();

        Assert.Equal(new FilePath(contentDir + "/").Standardized, new FilePath(root!).Standardized);
    }

    [Fact]
    public void ResolveFilesPanelRoot_WhenReferencedProjectFileIsMissingButItsFolderExists_StillUsesThatFolder()
    {
        // Mirrors a real repro: a build-output .achx (under a non-"Content"-named path)
        // whose ProjectFile points at a project file that was never copied alongside it,
        // but the project's own folder (and its Content subfolder) still exists on disk.
        using var temp = new TempDir();

        var achxDir = Path.Combine(temp.Path, "Build", "Nested");
        Directory.CreateDirectory(achxDir);
        var projectDir = Path.Combine(temp.Path, "Project");
        var contentDir = Path.Combine(projectDir, "Content");
        Directory.CreateDirectory(contentDir);
        // Note: no "Game.glux" file is written — only the folder exists.

        var sut = new ProjectManager
        {
            FileName = Path.Combine(achxDir, "Anim.achx"),
            AnimationChainListSave = new AnimationChainListSave { ProjectFile = "../../Project/Game.glux" }
        };

        var root = sut.ResolveFilesPanelRoot();

        Assert.Equal(new FilePath(contentDir + "/").Standardized, new FilePath(root!).Standardized);
    }

    [Fact]
    public void ResolveFilesPanelRoot_PreservesLeadingSlashOnUnixStyleAbsolutePaths()
    {
        // Regression: the Content-ancestor walk-up must not lose a Unix-style absolute
        // path's leading '/' when reconstructing the truncated path — losing it turns
        // the result into a relative path, which FilePath then resolves against
        // Environment.CurrentDirectory instead of the real root. Constructed directly
        // (no real filesystem paths) so this reproduces on any host OS: Path.IsPathRooted
        // treats a leading '/' as rooted on both Windows and Linux, so FilePath won't
        // rewrite it, letting this test exercise the Linux-only code path everywhere.
        var sut = new ProjectManager
        {
            FileName = "/tmp/AnimationEditorCoreTests/proj/Content/Entities/Enemy/Byakhee.achx"
        };

        var root = sut.ResolveFilesPanelRoot();

        Assert.Equal("/tmp/AnimationEditorCoreTests/proj/Content/", root);
    }

    [Fact]
    public void ResolveFilesPanelRoot_WhenProjectFileFolderDoesNotExist_FallsBackToContentAncestor()
    {
        using var temp = new TempDir();

        var contentDir = Path.Combine(temp.Path, "Content");
        var achxDir = Path.Combine(contentDir, "Entities", "Enemy");
        Directory.CreateDirectory(achxDir);

        var sut = new ProjectManager
        {
            FileName = Path.Combine(achxDir, "Byakhee.achx"),
            AnimationChainListSave = new AnimationChainListSave { ProjectFile = "../../../../NoSuchFolder/game.glux" }
        };

        var root = sut.ResolveFilesPanelRoot();

        Assert.Equal(new FilePath(contentDir + "/").Standardized, new FilePath(root!).Standardized);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnimationEditorCoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
