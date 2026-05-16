using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using System.IO;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectManagerLoadTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // ── Missing file ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadAnimationChain_MissingFile_ThrowsFileNotFoundException()
    {
        var pm = new ProjectManager();

        Assert.Throws<FileNotFoundException>(
            () => pm.LoadAnimationChain(new FilePath(@"C:\does\not\exist.achx")));
    }

    [Fact]
    public void LoadAnimationChain_MissingFile_DoesNotChangeAnimationChainListSave()
    {
        var pm = new ProjectManager();
        pm.AnimationChainListSave = null;

        try { pm.LoadAnimationChain(new FilePath(@"C:\does\not\exist.achx")); }
        catch (FileNotFoundException) { }

        Assert.Null(pm.AnimationChainListSave);
    }

    // ── Corrupt file ─────────────────────────────────────────────────────────

    [Fact]
    public void LoadAnimationChain_CorruptFile_ThrowsException()
    {
        var pm = new ProjectManager();
        var path = Path.Combine(_dir.Path, "corrupt.achx");
        File.WriteAllText(path, "this is not valid xml");

        Assert.ThrowsAny<Exception>(
            () => pm.LoadAnimationChain(new FilePath(path)));
    }

    [Fact]
    public void LoadAnimationChain_CorruptFile_DoesNotChangeAnimationChainListSave()
    {
        var pm = new ProjectManager();
        var sentinel = new AnimationChainListSave();
        pm.AnimationChainListSave = sentinel;
        var path = Path.Combine(_dir.Path, "corrupt2.achx");
        File.WriteAllText(path, "this is not valid xml");

        try { pm.LoadAnimationChain(new FilePath(path)); }
        catch { }

        Assert.Same(sentinel, pm.AnimationChainListSave);
    }

    // ── Git conflict markers ──────────────────────────────────────────────────

    [Fact]
    public void LoadAnimationChain_ConflictMarkerFile_ThrowsInvalidDataException()
    {
        var pm = new ProjectManager();
        var path = Path.Combine(_dir.Path, "conflict.achx");
        File.WriteAllText(path,
            "<<<<<<< HEAD\n<?xml version=\"1.0\"?><AnimationChainList />\n=======\n<?xml version=\"1.0\"?><AnimationChainList />\n>>>>>>>");

        Assert.Throws<System.IO.InvalidDataException>(
            () => pm.LoadAnimationChain(new FilePath(path)));
    }

    [Fact]
    public void LoadAnimationChain_ConflictMarkerFile_MessageMentionsGitConflict()
    {
        var pm = new ProjectManager();
        var path = Path.Combine(_dir.Path, "conflict2.achx");
        File.WriteAllText(path,
            "<<<<<<< HEAD\n<?xml version=\"1.0\"?><AnimationChainList />\n=======\n<?xml version=\"1.0\"?><AnimationChainList />\n>>>>>>>");

        var ex = Assert.Throws<System.IO.InvalidDataException>(
            () => pm.LoadAnimationChain(new FilePath(path)));
        Assert.Contains("conflict", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadAnimationChain_ConflictMarkerFile_DoesNotChangeAnimationChainListSave()
    {
        var pm = new ProjectManager();
        var sentinel = new AnimationChainListSave();
        pm.AnimationChainListSave = sentinel;
        var path = Path.Combine(_dir.Path, "conflict3.achx");
        File.WriteAllText(path,
            "<<<<<<< HEAD\n<?xml version=\"1.0\"?><AnimationChainList />\n=======\n<?xml version=\"1.0\"?><AnimationChainList />\n>>>>>>>");

        try { pm.LoadAnimationChain(new FilePath(path)); }
        catch { }

        Assert.Same(sentinel, pm.AnimationChainListSave);
    }
}
