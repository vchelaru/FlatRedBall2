using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsSaveAsTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir;
    private readonly TestServices ctx;

    public AppCommandsSaveAsTests()
    {
        _dir = new TestHelpers.TempDir();
        ctx = TestHelpers.SetupFreshAcls();
    }

    public void Dispose() => _dir.Dispose();

    // ── Dialog cancelled ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenDialogCancelled_DoesNotSaveFile()
    {
        ctx.AppCommands.FileDialogService = new StubFileDialogService(null);

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.Empty(Directory.GetFiles(_dir.Path, "*.achx"));
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenDialogCancelled_DoesNotUpdateFileName()
    {
        ctx.AppCommands.FileDialogService = new StubFileDialogService(null);
        ctx.ProjectManager.FileName = null;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.Null(ctx.ProjectManager.FileName);
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenDialogCancelled_DoesNotFireSaveAsCompleted()
    {
        ctx.AppCommands.FileDialogService = new StubFileDialogService(null);
        bool fired = false;
        ctx.AppCommands.SaveAsCompleted += _ => fired = true;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.False(fired);
    }

    // ── Dialog confirms ───────────────────────────────────────────────────────

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenPathReturned_SavesFile()
    {
        var target = Path.Combine(_dir.Path, "out.achx");
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });
        ctx.ProjectManager.AnimationChainListSave = acls;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.True(File.Exists(target));
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenPathReturned_UpdatesProjectManagerFileName()
    {
        var target = Path.Combine(_dir.Path, "out.achx");
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);
        ctx.ProjectManager.AnimationChainListSave = acls;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.Equal(target, ctx.ProjectManager.FileName);
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenPathReturned_FiresSaveAsCompletedWithPath()
    {
        var target = Path.Combine(_dir.Path, "out.achx");
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);
        ctx.ProjectManager.AnimationChainListSave = acls;
        string? received = null;
        ctx.AppCommands.SaveAsCompleted += p => received = p;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.Equal(target, received);
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenPathReturned_FiresCurrentFileChangedWithPath()
    {
        var target = Path.Combine(_dir.Path, "out.achx");
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);
        ctx.ProjectManager.AnimationChainListSave = ctx.Acls;
        string? received = null;
        ctx.ApplicationEvents.CurrentFileChanged += p => received = p;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.Equal(target, received);
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_WhenDialogCancelled_DoesNotFireCurrentFileChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppCommands.FileDialogService = new StubFileDialogService(null);
        bool fired = false;
        ctx.ApplicationEvents.CurrentFileChanged += _ => fired = true;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.False(fired);
    }

    // ── Default extension (#872: .achj is the default for new files, .achx is preserved) ──────

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_NewFile_RequestsAchjDefaultExtension()
    {
        var dialog = new CapturingFileDialogService(Path.Combine(_dir.Path, "out.achj"));
        ctx.AppCommands.FileDialogService = dialog;
        ctx.ProjectManager.FileName = null;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.Equal("achj", dialog.RequestedDefaultExtension);
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_AchxFileAlreadyLoaded_RequestsAchxDefaultExtension()
    {
        var loadedPath = Path.Combine(_dir.Path, "loaded.achx");
        var dialog = new CapturingFileDialogService(Path.Combine(_dir.Path, "out.achx"));
        ctx.AppCommands.FileDialogService = dialog;
        ctx.ProjectManager.FileName = loadedPath;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.Equal("achx", dialog.RequestedDefaultExtension);
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_AchjFileAlreadyLoaded_RequestsAchjDefaultExtension()
    {
        var loadedPath = Path.Combine(_dir.Path, "loaded.achj");
        var dialog = new CapturingFileDialogService(Path.Combine(_dir.Path, "out.achj"));
        ctx.AppCommands.FileDialogService = dialog;
        ctx.ProjectManager.FileName = loadedPath;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.Equal("achj", dialog.RequestedDefaultExtension);
    }

    // ── File-type dropdown offers both formats (#973) ──────────────────────────

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_AchxFileAlreadyLoaded_OffersBothAchjAndAchxChoices()
    {
        var loadedPath = Path.Combine(_dir.Path, "loaded.achx");
        var dialog = new CapturingFileDialogService(Path.Combine(_dir.Path, "out.achx"));
        ctx.AppCommands.FileDialogService = dialog;
        ctx.ProjectManager.FileName = loadedPath;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.Equal(
            new[] { "achj", "achx" },
            dialog.RequestedFileTypeChoices!.Select(c => c.Extension).OrderBy(e => e));
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_NewFile_OffersBothAchjAndAchxChoices()
    {
        var dialog = new CapturingFileDialogService(Path.Combine(_dir.Path, "out.achj"));
        ctx.AppCommands.FileDialogService = dialog;
        ctx.ProjectManager.FileName = null;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        Assert.Equal(
            new[] { "achj", "achx" },
            dialog.RequestedFileTypeChoices!.Select(c => c.Extension).OrderBy(e => e));
    }

    [Fact]
    public async Task SaveCurrentAnimationChainListAsync_SavedFile_ContainsChainData()
    {
        var target = Path.Combine(_dir.Path, "data.achx");
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Run" });
        ctx.ProjectManager.AnimationChainListSave = acls;

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        var xml = File.ReadAllText(target);
        Assert.Contains("Run", xml);
    }
}


/// <summary>Test double that returns a pre-configured path (or null) from every dialog.</summary>
internal sealed class StubFileDialogService : IFileDialogService
{
    private readonly string? _path;

    public StubFileDialogService(string? path) => _path = path;

    public Task<string?> PickSaveFileAsync(string title, string defaultExtension, IReadOnlyList<FileTypeChoice> fileTypeChoices)
        => Task.FromResult(_path);

    public Task<string?> PickOpenFileAsync(string title, string defaultExtension, string fileTypeDescription)
        => Task.FromResult(_path);
}

/// <summary>Test double that records the requested default extension/file-type choices and returns a fixed path.</summary>
internal sealed class CapturingFileDialogService : IFileDialogService
{
    private readonly string? _path;

    public CapturingFileDialogService(string? path) => _path = path;

    public string? RequestedDefaultExtension { get; private set; }
    public IReadOnlyList<FileTypeChoice>? RequestedFileTypeChoices { get; private set; }

    public Task<string?> PickSaveFileAsync(string title, string defaultExtension, IReadOnlyList<FileTypeChoice> fileTypeChoices)
    {
        RequestedDefaultExtension = defaultExtension;
        RequestedFileTypeChoices = fileTypeChoices;
        return Task.FromResult(_path);
    }

    public Task<string?> PickOpenFileAsync(string title, string defaultExtension, string fileTypeDescription)
        => Task.FromResult(_path);
}
