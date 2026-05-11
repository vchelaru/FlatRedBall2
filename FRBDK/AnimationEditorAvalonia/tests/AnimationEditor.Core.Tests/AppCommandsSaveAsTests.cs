using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall.Content.AnimationChain;
using System.IO;
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

    public Task<string?> PickSaveFileAsync(string title, string defaultExtension, string fileTypeDescription)
        => Task.FromResult(_path);

    public Task<string?> PickOpenFileAsync(string title, string defaultExtension, string fileTypeDescription)
        => Task.FromResult(_path);
}
