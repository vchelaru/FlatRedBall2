using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using AnimationEditor.Editing;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class FrameTextureEditTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnimationEditorEditingTests", Guid.NewGuid().ToString("N"));
    private readonly string _achxFolder;
    private readonly string _elsewhere;
    private readonly ProjectManager _pm = new();
    private readonly AppCommands _commands;
    private readonly UndoManager _undo = new();
    private readonly AnimationFrameSave _frame = new() { TextureName = "old.png" };

    private sealed class FakeProbe(bool decodes = true) : ITextureProbe
    {
        public List<string> Probed { get; } = new();
        public bool CanDecode(string absolutePath) { Probed.Add(absolutePath); return decodes; }
    }

    public FrameTextureEditTests()
    {
        _achxFolder = Path.Combine(_root, "proj");
        _elsewhere = Path.Combine(_root, "other");
        Directory.CreateDirectory(_achxFolder);
        Directory.CreateDirectory(_elsewhere);

        var events = new ApplicationEvents();
        var selected = new SelectedState(_pm);
        var appState = new AppState(events, selected);
        var io = new IoManager(appState, Path.Combine(_root, "recovery.achx"));
        _commands = new AppCommands(_pm, selected, events, io, new ObjectFinder(_pm), _undo);

        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(_frame);
        acls.AnimationChains.Add(chain);
        _pm.AnimationChainListSave = acls;
        _pm.FileName = Path.Combine(_achxFolder, "hero.achx");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static string Png(string folder, string name, string content = "png")
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static TextureCopyChooser Always(TextureCopyChoice choice) => _ => Task.FromResult(choice);

    [Fact]
    public void SetFromText_RelativeExistingFile_StoresRelativePath()
    {
        Png(_achxFolder, "new.png");

        var result = FrameTextureEdit.SetFromText(_pm, _commands, [_frame], "new.png", new FakeProbe());

        result.Success.ShouldBeTrue();
        result.ChangedItems.ShouldBe(["new.png"]);
        _frame.TextureName.ShouldBe("new.png");
    }

    [Fact]
    public void SetFromText_AbsolutePathInsideAchxFolder_StoresRelativePath()
    {
        var abs = Png(_achxFolder, "sub.png");

        var result = FrameTextureEdit.SetFromText(_pm, _commands, [_frame], abs, new FakeProbe());

        result.Success.ShouldBeTrue();
        _frame.TextureName.ShouldBe("sub.png");
    }

    [Fact]
    public void SetFromText_MissingFile_FailsWithNotFoundAndLeavesFrame()
    {
        var result = FrameTextureEdit.SetFromText(_pm, _commands, [_frame], "nope.png", new FakeProbe());

        result.Success.ShouldBeFalse();
        result.HasError(EditErrorCode.TextureNotFound).ShouldBeTrue();
        _frame.TextureName.ShouldBe("old.png");
        _undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFromText_UndecodableImage_FailsWithUndecodableAndLeavesFrame()
    {
        Png(_achxFolder, "bad.png", "not an image");

        var result = FrameTextureEdit.SetFromText(_pm, _commands, [_frame], "bad.png", new FakeProbe(decodes: false));

        result.Success.ShouldBeFalse();
        result.HasError(EditErrorCode.TextureUndecodable).ShouldBeTrue();
        result.Errors[0].ShouldContain("bad.png");
        _frame.TextureName.ShouldBe("old.png");
        _undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFromText_EmptyText_ClearsTextureWithoutProbing()
    {
        var probe = new FakeProbe();

        var result = FrameTextureEdit.SetFromText(_pm, _commands, [_frame], "  ", probe);

        result.Success.ShouldBeTrue();
        _frame.TextureName.ShouldBe(string.Empty);
        probe.Probed.ShouldBeEmpty();
    }

    [Fact]
    public void SetFromText_EmptyTextOnAlreadyEmptyFrame_AddsNoUndoEntry()
    {
        _frame.TextureName = "";

        FrameTextureEdit.SetFromText(_pm, _commands, [_frame], "", new FakeProbe()).Success.ShouldBeTrue();

        _undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFromText_SameTextAsDisplayed_ChangesNothing()
    {
        Png(_achxFolder, "old.png");

        var result = FrameTextureEdit.SetFromText(_pm, _commands, [_frame], "old.png", new FakeProbe());

        result.Success.ShouldBeTrue();
        result.ChangedItems.ShouldBeEmpty();
        _undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFromText_Undo_RestoresPreviousTexture()
    {
        Png(_achxFolder, "new.png");
        FrameTextureEdit.SetFromText(_pm, _commands, [_frame], "new.png", new FakeProbe());

        _undo.Undo();

        _frame.TextureName.ShouldBe("old.png");
    }

    [Fact]
    public void SetFromText_SeveralFrames_OneUndoStep()
    {
        Png(_achxFolder, "new.png");
        var second = new AnimationFrameSave { TextureName = "other.png" };

        FrameTextureEdit.SetFromText(_pm, _commands, [_frame, second], "new.png", new FakeProbe());
        _frame.TextureName.ShouldBe("new.png");
        second.TextureName.ShouldBe("new.png");

        _undo.Undo();
        _frame.TextureName.ShouldBe("old.png");
        second.TextureName.ShouldBe("other.png");
    }

    [Fact]
    public async Task SetFromPicked_InsideAchxFolder_NoPromptAndStoresRelativePath()
    {
        var picked = Png(_achxFolder, "inside.png");
        bool asked = false;

        var result = await FrameTextureEdit.SetFromPickedAsync(_pm, _commands, [_frame], picked, new FakeProbe(),
            _ => { asked = true; return Task.FromResult(TextureCopyChoice.Cancel); });

        result.Success.ShouldBeTrue();
        asked.ShouldBeFalse();
        _frame.TextureName.ShouldBe("inside.png");
    }

    [Fact]
    public async Task SetFromPicked_OutsideCopy_CopiesNextToAchxAndStoresRelativeName()
    {
        var picked = Png(_elsewhere, "far.png");

        var result = await FrameTextureEdit.SetFromPickedAsync(_pm, _commands, [_frame], picked, new FakeProbe(),
            Always(TextureCopyChoice.Copy));

        result.Success.ShouldBeTrue();
        File.Exists(Path.Combine(_achxFolder, "far.png")).ShouldBeTrue();
        _frame.TextureName.ShouldBe("far.png");
    }

    [Fact]
    public async Task SetFromPicked_OutsideKeepInPlace_StoresRelativePathBackOut()
    {
        var picked = Png(_elsewhere, "far.png");

        var result = await FrameTextureEdit.SetFromPickedAsync(_pm, _commands, [_frame], picked, new FakeProbe(),
            Always(TextureCopyChoice.KeepInPlace));

        result.Success.ShouldBeTrue();
        File.Exists(Path.Combine(_achxFolder, "far.png")).ShouldBeFalse();
        _frame.TextureName.Replace('\\', '/').ShouldBe("../other/far.png");
    }

    [Fact]
    public async Task SetFromPicked_Cancelled_ReportsCancelledAndChangesNothing()
    {
        var picked = Png(_elsewhere, "far.png");

        var result = await FrameTextureEdit.SetFromPickedAsync(_pm, _commands, [_frame], picked, new FakeProbe(),
            Always(TextureCopyChoice.Cancel));

        result.HasError(EditErrorCode.Cancelled).ShouldBeTrue();
        _frame.TextureName.ShouldBe("old.png");
        File.Exists(Path.Combine(_achxFolder, "far.png")).ShouldBeFalse();
    }

    [Fact]
    public async Task SetFromPicked_CopyFails_ReportsCopyFailedAndChangesNothing()
    {
        var picked = Png(_elsewhere, "far.png", "new bytes");
        Png(_achxFolder, "far.png", "different existing");   // Copy must not replace a different file

        var result = await FrameTextureEdit.SetFromPickedAsync(_pm, _commands, [_frame], picked, new FakeProbe(),
            Always(TextureCopyChoice.Copy));

        result.HasError(EditErrorCode.TextureCopyFailed).ShouldBeTrue();
        _frame.TextureName.ShouldBe("old.png");
        File.ReadAllText(Path.Combine(_achxFolder, "far.png")).ShouldBe("different existing");
    }

    [Fact]
    public async Task SetFromPicked_Undecodable_FailsWithUndecodable()
    {
        var picked = Png(_achxFolder, "bad.png", "x");

        var result = await FrameTextureEdit.SetFromPickedAsync(_pm, _commands, [_frame], picked, new FakeProbe(false),
            Always(TextureCopyChoice.Copy));

        result.HasError(EditErrorCode.TextureUndecodable).ShouldBeTrue();
        _frame.TextureName.ShouldBe("old.png");
    }

    [Fact]
    public async Task ResolveForProject_OutsideUseExisting_ReturnsExistingDestination()
    {
        var picked = Png(_elsewhere, "far.png", "a");
        var existing = Png(_achxFolder, "far.png", "b");

        var resolved = await FrameTextureEdit.ResolveForProjectAsync(_pm, picked, Always(TextureCopyChoice.UseExisting));

        resolved.Result.Success.ShouldBeTrue();
        resolved.AbsolutePath.ShouldBe(existing);
    }

    [Fact]
    public async Task ResolveForProject_Inside_ReturnsPathUnchanged()
    {
        var picked = Png(_achxFolder, "in.png");

        var resolved = await FrameTextureEdit.ResolveForProjectAsync(_pm, picked, Always(TextureCopyChoice.Cancel));

        resolved.AbsolutePath.ShouldBe(picked);
    }
}
