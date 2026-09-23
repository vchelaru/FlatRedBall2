using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Verifies that <see cref="IAppCommands.OpenAchxWorkflowAsync"/> correctly sequences
/// the open flow (load data, notify WireframeCtrl/PreviewCtrl via AchxLoaded,
/// then raise CurrentFileChanged and AvailableTexturesChanged for the UI layer)
/// — all without any Avalonia dependency.
/// </summary>
[Collection("SequentialSingletons")]
public class OpenAchxWorkflowTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir;
    private readonly TestServices _ctx;

    public OpenAchxWorkflowTests()
    {
        _dir = new TestHelpers.TempDir();
        _ctx = TestHelpers.SetupFreshAcls();
    }

    public void Dispose() => _dir.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a minimal pixel-format .achx (no frames, no textures) so the UV gate
    /// is bypassed and the tests focus purely on workflow sequencing.
    /// </summary>
    private string WriteMinimalAchx(string chainName = "Idle")
    {
        var path = Path.Combine(_dir.Path, "test.achx");
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave { Name = chainName });
        acls.Save(path);
        return path;
    }

    /// <summary>Same as <see cref="WriteMinimalAchx"/> but written as .achj (JSON).</summary>
    private string WriteMinimalAchj(string chainName = "Idle")
    {
        var path = Path.Combine(_dir.Path, "test.achj");
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave { Name = chainName });
        acls.SaveJson(path);
        return path;
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenAchxWorkflow_WithValidFile_LoadsChainIntoProjectManager()
    {
        var path = WriteMinimalAchx("Walk");

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        Assert.NotNull(_ctx.ProjectManager.AnimationChainListSave);
        Assert.Single(_ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        Assert.Equal("Walk", _ctx.ProjectManager.AnimationChainListSave.AnimationChains[0].Name);
    }

    [Fact]
    public async Task OpenAchxWorkflow_WithValidFile_SetsProjectManagerFileName()
    {
        var path = WriteMinimalAchx();

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        Assert.Equal(new FilePath(path), new FilePath(_ctx.ProjectManager.FileName!));
    }

    // #878: the preview-parse step that reads CoordinateType before committing to a full load
    // must dispatch on extension the same way ProjectManager.LoadAnimationChain does -- otherwise
    // a .achj (JSON) file hits the XML parser and the open fails with an XmlException toast.
    [Fact]
    public async Task OpenAchxWorkflow_WithValidAchjFile_LoadsChainIntoProjectManager()
    {
        var path = WriteMinimalAchj("Walk");

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        Assert.NotNull(_ctx.ProjectManager.AnimationChainListSave);
        Assert.Single(_ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        Assert.Equal("Walk", _ctx.ProjectManager.AnimationChainListSave.AnimationChains[0].Name);
    }

    [Fact]
    public async Task OpenAchxWorkflow_WithValidAchjFile_DoesNotRaiseLoadFailed()
    {
        var path = WriteMinimalAchj();
        Exception? failure = null;
        _ctx.AppCommands.LoadFailed += (_, ex) => failure = ex;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        Assert.Null(failure);
    }

    // ── Event ordering ────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenAchxWorkflow_AchxLoadedFiresAfterDataIsLoaded()
    {
        var path = WriteMinimalAchx("Run");
        FilePath? fileNameAtNotification = null;
        _ctx.ApplicationEvents.AchxLoaded += _ =>
            fileNameAtNotification = _ctx.ProjectManager.FileName;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        // AchxLoaded fires after LoadAnimationChain sets the FileName
        Assert.Equal(new FilePath(path), fileNameAtNotification);
    }

    [Fact]
    public async Task OpenAchxWorkflow_WithValidFile_FiresAchxLoadedWithPath()
    {
        var path = WriteMinimalAchx();
        string? received = null;
        _ctx.ApplicationEvents.AchxLoaded += p => received = p;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        Assert.Equal(new FilePath(path), new FilePath(received!));
    }

    // ── CurrentFileChanged ────────────────────────────────────────────────────

    [Fact]
    public async Task OpenAchxWorkflow_WithValidFile_FiresCurrentFileChangedWithPath()
    {
        var path = WriteMinimalAchx();
        string? received = null;
        _ctx.ApplicationEvents.CurrentFileChanged += p => received = p;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        Assert.Equal(new FilePath(path), new FilePath(received!));
    }

    [Fact]
    public async Task OpenAchxWorkflow_CurrentFileChangedFiresAfterDataIsLoaded()
    {
        var path = WriteMinimalAchx("Jump");
        FilePath? fileNameAtNotification = null;
        _ctx.ApplicationEvents.CurrentFileChanged += _ =>
            fileNameAtNotification = _ctx.ProjectManager.FileName;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        Assert.Equal(new FilePath(path), fileNameAtNotification);
    }

    // ── AvailableTexturesChanged ───────────────────────────────────────────────

    [Fact]
    public async Task OpenAchxWorkflow_WithValidFile_FiresAvailableTexturesChanged()
    {
        var path = WriteMinimalAchx();
        bool fired = false;
        _ctx.ApplicationEvents.AvailableTexturesChanged += () => fired = true;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        Assert.True(fired);
    }

    // ── Load failure (corrupt / missing file) ─────────────────────────────────

    [Fact]
    public async Task OpenAchxWorkflow_WithNonExistentFile_FiresLoadFailed()
    {
        var path = Path.Combine(_dir.Path, "ghost.achx");
        bool loadFailedFired = false;
        _ctx.AppCommands.LoadFailed += (_, _) => loadFailedFired = true;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        Assert.True(loadFailedFired);
    }

    [Fact]
    public async Task OpenAchxWorkflow_WithNonExistentFile_DoesNotFireSuccessEvents()
    {
        var path = Path.Combine(_dir.Path, "ghost.achx");
        bool currentFileFired = false;
        bool texturesFired = false;
        _ctx.ApplicationEvents.CurrentFileChanged += _ => currentFileFired = true;
        _ctx.ApplicationEvents.AvailableTexturesChanged += () => texturesFired = true;

        await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        Assert.False(currentFileFired);
        Assert.False(texturesFired);
    }

    // ── Return value: did a document end up loaded? ──────────────────────────
    // The window registers a tab before it runs the workflow, so it needs to know when the
    // workflow refused (declined conversion, missing textures, unreadable file) to take that
    // tab back; a tab labelled with a file that was never loaded otherwise stays behind.

    [Fact]
    public async Task OpenAchxWorkflow_WithValidFile_ReturnsTrue()
    {
        var path = WriteMinimalAchx();

        bool opened = await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        opened.ShouldBeTrue();
    }

    [Fact]
    public async Task OpenAchxWorkflow_UvFileAndUserDeclinesConversion_ReturnsFalse()
    {
        var path = Path.Combine(_dir.Path, "legacy.achx");
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Idle" });
        acls.Save(path);
        _ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(false);

        bool opened = await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        opened.ShouldBeFalse();
        _ctx.ProjectManager.FileName.ShouldBeNull();
    }

    [Fact]
    public async Task OpenAchxWorkflow_UnreadableFile_ReturnsFalse()
    {
        var path = Path.Combine(_dir.Path, "broken.achx");
        File.WriteAllText(path, "<AnimationChainArrayS");

        bool opened = await _ctx.AppCommands.OpenAchxWorkflowAsync(path);

        opened.ShouldBeFalse();
    }
}
