using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Verifies the hot-reload path in <see cref="AppCommands.ReloadAchxFromDisk"/>:
/// mangled files surface <see cref="IAppCommands.HotReloadFailed"/> and leave the
/// in-memory state untouched; valid files load normally.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsHotReloadTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = TestHelpers.SetupFreshAcls();

    public void Dispose() => _dir.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string WriteMinimalAchx(string chainName = "Idle")
    {
        var path = Path.Combine(_dir.Path, $"{chainName}.achx");
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave { Name = chainName });
        acls.Save(path);
        return path;
    }

    private string WriteConflictMarkerFile()
    {
        var path = Path.Combine(_dir.Path, "conflict.achx");
        File.WriteAllText(path,
            "<<<<<<< HEAD\n<?xml version=\"1.0\"?><AnimationChainList />\n=======\n<?xml version=\"1.0\"?><AnimationChainList />\n>>>>>>>");
        return path;
    }

    private string WriteCorruptFile()
    {
        var path = Path.Combine(_dir.Path, "corrupt.achx");
        File.WriteAllText(path, "this is not valid xml");
        return path;
    }

    // ── Mangled file: fires HotReloadFailed ───────────────────────────────────

    [Fact]
    public void ReloadAchxFromDisk_ConflictMarkerFile_FiresHotReloadFailed()
    {
        string? capturedPath = null;
        _ctx.AppCommands.HotReloadFailed += (path, _) => capturedPath = path;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        Assert.NotNull(capturedPath);
    }

    [Fact]
    public void ReloadAchxFromDisk_ConflictMarkerFile_MessageMentionsConflict()
    {
        string? capturedMsg = null;
        _ctx.AppCommands.HotReloadFailed += (_, msg) => capturedMsg = msg;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        Assert.NotNull(capturedMsg);
        Assert.Contains("conflict", capturedMsg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReloadAchxFromDisk_CorruptFile_FiresHotReloadFailed()
    {
        bool fired = false;
        _ctx.AppCommands.HotReloadFailed += (_, __) => fired = true;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteCorruptFile());

        Assert.True(fired);
    }

    [Fact]
    public void ReloadAchxFromDisk_MangledFile_DoesNotChangeAnimationChainListSave()
    {
        var sentinel = new AnimationChainListSave();
        _ctx.ProjectManager.AnimationChainListSave = sentinel;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        Assert.Same(sentinel, _ctx.ProjectManager.AnimationChainListSave);
    }

    [Fact]
    public void ReloadAchxFromDisk_MangledFile_DoesNotClearUndoStack()
    {
        // Put something on the undo stack first.
        var path = WriteMinimalAchx("Before");
        _ctx.AppCommands.LoadAnimationChain(path);
        _ctx.AppCommands.AddAnimationChainWithName("Extra");

        bool wasCleared = false;
        _ctx.UndoManager.StackChanged += () => wasCleared = true;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        // StackChanged fires on every push/pop/clear — but since reload aborted,
        // the undo stack was not touched by ReloadAchxFromDisk.
        // Reset the flag after a potential false-fire from the AddAnimationChainWithName above.
        wasCleared = false;
        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        Assert.False(wasCleared);
    }

    [Fact]
    public void ReloadAchxFromDisk_MangledFile_DoesNotFireLoadFailed()
    {
        bool loadFailedFired = false;
        _ctx.AppCommands.LoadFailed += (_, __) => loadFailedFired = true;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteConflictMarkerFile());

        Assert.False(loadFailedFired);
    }

    // ── Valid file: loads normally ────────────────────────────────────────────

    [Fact]
    public void ReloadAchxFromDisk_ValidFile_LoadsNewContent()
    {
        var path = WriteMinimalAchx("Walk");

        _ctx.AppCommands.ReloadAchxFromDisk(path);

        var acls = _ctx.ProjectManager.AnimationChainListSave;
        Assert.NotNull(acls);
        Assert.Single(acls!.AnimationChains);
        Assert.Equal("Walk", acls.AnimationChains[0].Name);
    }

    [Fact]
    public void ReloadAchxFromDisk_ValidFile_DoesNotFireHotReloadFailed()
    {
        bool fired = false;
        _ctx.AppCommands.HotReloadFailed += (_, __) => fired = true;

        _ctx.AppCommands.ReloadAchxFromDisk(WriteMinimalAchx("Run"));

        Assert.False(fired);
    }
}
