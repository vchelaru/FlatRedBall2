using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Covers <see cref="ProjectManager.ResolveFilesPanelRoot"/>: the Files panel should
/// browse the linked project's Content folder when one resolves, and fall back to the
/// loaded .achx's own folder otherwise. Resolution only ever uses the project file's
/// *location* on disk — never its internal format — so this keeps working even for
/// project file formats other than the FRB1 .gluj this repo currently supports.
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
    public void ResolveFilesPanelRoot_WhenNoProjectFileSet_ReturnsAchxFolder()
    {
        using var temp = new TempDir();

        var sut = new ProjectManager
        {
            FileName = Path.Combine(temp.Path, "Anim.achx"),
            AnimationChainListSave = new AnimationChainListSave()
        };

        var root = sut.ResolveFilesPanelRoot();

        Assert.Equal(new FilePath(temp.Path + "/").Standardized, new FilePath(root!).Standardized);
    }

    [Fact]
    public void ResolveFilesPanelRoot_WhenProjectFileResolvesWithContentFolder_ReturnsContentFolder()
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
    public void ResolveFilesPanelRoot_WhenProjectFileResolvesWithoutContentFolder_ReturnsProjectFolder()
    {
        using var temp = new TempDir();

        var projectDir = Path.Combine(temp.Path, "Project");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "Game.gluj"), "<Project/>");

        var sut = new ProjectManager
        {
            FileName = Path.Combine(temp.Path, "Anim.achx"),
            AnimationChainListSave = new AnimationChainListSave { ProjectFile = "Project/Game.gluj" }
        };

        var root = sut.ResolveFilesPanelRoot();

        Assert.Equal(new FilePath(projectDir + "/").Standardized, new FilePath(root!).Standardized);
    }

    [Fact]
    public void ResolveFilesPanelRoot_WhenProjectFileDoesNotResolve_FallsBackToAchxFolder()
    {
        using var temp = new TempDir();

        var sut = new ProjectManager
        {
            FileName = Path.Combine(temp.Path, "Anim.achx"),
            AnimationChainListSave = new AnimationChainListSave { ProjectFile = "../NoSuchProject.gluj" }
        };

        var root = sut.ResolveFilesPanelRoot();

        Assert.Equal(new FilePath(temp.Path + "/").Standardized, new FilePath(root!).Standardized);
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
