using AnimationEditor.Core.Export;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Sequences;
using AnimationEditor.DocumentMatrix;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// #2006: every action that writes a document, for every type of document in front and behind it. Whatever the action,
/// each file on disk is still in its own format, nothing a document held is gone, and no other file changed.
/// The actions that need the window are in <c>DocumentWriteMatrixScenarioTests</c>.
/// </summary>
public class DocumentWriteMatrixTests
{
    private static readonly DocAction[] Actions = Enum.GetValues<DocAction>().Where(action => action <= DocAction.PushToTileset).ToArray();

    /// <summary>False only where the editor has no such command for the pair.</summary>
    private static bool Applies(DocAction action, DocType front, DocType background) => action switch
    {
        // A name has an extension, so the target is always some file format.
        DocAction.SaveAsOntoAnotherFormat => background != DocType.Untitled,
        // The cross-file cut only saves file-backed tabs of the achx pane.
        DocAction.SaveBackgroundDocument => background is not (DocType.Untitled or DocType.Seqj),
        DocAction.Reload => front != DocType.Untitled,
        // A sequence tab has no export to these formats, and no Untitled form to recover.
        DocAction.Export or DocAction.WriteRecovery => front != DocType.Seqj,
        DocAction.ConvertToAchj => front == DocType.Achx,
        DocAction.PushToTileset => front is DocType.Achx or DocType.Achj && background == DocType.Tsx,
        _ => true,
    };

    public static IEnumerable<object[]> Rows() => Documents.Rows(Actions, Applies);

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Action_KeepsEveryDocument(DocAction action, DocType front, DocType background)
    {
        using var editor = new Editor();
        Document back = await editor.OpenAsync(background, Documents.BackgroundName);
        Document current = await editor.OpenAsync(front, Documents.FrontName);
        var before = Documents.FileBytes(editor.Folder);
        var held = editor.DurableContent();

        // What the action is expected to write, and the content it adds.
        var written = new List<string?>();
        var added = new List<string>();
        switch (action)
        {
            case DocAction.Save:
                editor.Ctx.AppCommands.AutosaveOnChange = false;
                added.Add(editor.Edit(current));
                await editor.SaveAsync(current);
                written.Add(current.Path);
                break;
            case DocAction.Autosave:
                added.Add(editor.Edit(current));
                written.Add(current.Path);
                break;
            case DocAction.SaveAs:
                string copy = Path.Combine(editor.Folder, "copy" + (front == DocType.Untitled ? ".achj" : front.Extension()));
                added.Add(editor.Edit(current));
                written.Add(current.Path);
                await editor.SaveAsAsync(current, copy);
                Documents.ReadAsItsOwnFormat(copy).ShouldHold(added);
                break;
            case DocAction.SaveAsOntoAnotherFormat:
                string target = Documents.Write(editor.Folder, background, "closed");
                byte[] old = File.ReadAllBytes(target);
                await editor.SaveAsAsync(current, target);
                if (File.ReadAllBytes(target).AsSpan().SequenceEqual(old))
                    editor.Messages.ShouldNotBeEmpty("nothing was saved and nothing said so");
                else
                    Documents.ReadAsItsOwnFormat(target).ShouldHold(editor.ContentOf(current));
                break;
            case DocAction.SaveBackgroundDocument:
                var (left, top) = Documents.NewCell(back.Name);
                back.Tab!.CachedEditorModel!.AnimationChains[0].Frames.Add(new AnimationFrameSave
                {
                    TextureName = "sheet.png",
                    FrameLength = 0.1f,
                    LeftCoordinate = left,
                    RightCoordinate = left + 16,
                    TopCoordinate = top,
                    BottomCoordinate = top + 16,
                });
                editor.Ctx.AppCommands.SaveDocument(back.Tab).ShouldBeTrue();
                added.Add(Documents.NewCellFact(back.Name));
                written.Add(back.Path);
                break;
            case DocAction.Reload:
                File.WriteAllText(current.Path!, File.ReadAllText(current.Path!).Replace("front", "reloaded"));
                before = Documents.FileBytes(editor.Folder);
                held = editor.DurableContent();
                editor.Reload(current);
                editor.ContentOf(current).ShouldBe(Documents.ReadAsItsOwnFormat(current.Path!), ignoreOrder: true);
                break;
            case DocAction.SwitchTabs:
                added.Add(editor.Edit(current));
                await editor.BringToFrontAsync(back);
                added.Add(editor.Edit(back));
                await editor.BringToFrontAsync(current);
                written.AddRange([current.Path, back.Path]);
                break;
            case DocAction.WriteRecovery:
                added.Add(editor.Edit(current));
                written.Add(current.Path);
                editor.Ctx.IoManager.RecoveryFileExists().ShouldBe(front == DocType.Untitled);
                if (front == DocType.Untitled)
                    Documents.ReadAsItsOwnFormat(editor.Ctx.IoManager.RecoveryFilePath).ShouldHold(editor.ContentOf(current));
                break;
            case DocAction.Export:
                string export = Path.Combine(editor.Folder, "export.json");
                editor.Ctx.AppCommands.FileDialogService = new StubFileDialogService(export);
                await editor.Ctx.AppCommands.ExportAsync(ExportFormat.PixiJs);
                File.Exists(export).ShouldBeTrue();
                break;
            case DocAction.ConvertToAchj:
                var result = AchxToAchjConverter.Convert(current.Path!);
                result.Status.ShouldBe(AchxConversionStatus.Converted);
                Documents.ReadAsItsOwnFormat(result.TargetPath!).ShouldBe(Documents.ReadAsItsOwnFormat(current.Path!), ignoreOrder: true);
                break;
            case DocAction.PushToTileset:
                editor.Ctx.AppCommands.AddAssociatedTiledTileset(back.Path!);
                added.Add(editor.Edit(current));
                written.AddRange([current.Path, back.Path]);
                break;
            default:
                throw new InvalidOperationException($"{action} has no case here.");
        }

        Documents.Changed(before).Except(written.Select(Path.GetFileName)).ShouldBeEmpty("a file the action is not about changed");
        var now = editor.DurableContent();
        now.ShouldHold(held, "content the documents held is gone");
        if (editor.Messages.Count == 0)
            now.ShouldHold(added, "an edit was not saved and nothing said so");
    }

    [Fact]
    public void EveryAction_HasRows()
    {
        var rows = Rows().Select(row => (DocAction)row[0]).ToHashSet();
        Actions.Where(action => !rows.Contains(action)).ShouldBeEmpty();
    }

    /// <summary>An open document: a tab of the achx pane, or a <c>.seqj</c> in its own document.</summary>
    private sealed record Document(DocType Type, string Name, string? Path, TabEntry? Tab, SequenceDocument? Sequences);

    /// <summary>The tabs of one window over the real Core commands, switched the way <c>AchxTabPane</c> does.</summary>
    private sealed class Editor : IDisposable
    {
        private readonly TestHelpers.TempDir _dir = new();
        private readonly List<Document> _open = new();
        // The achx pane's live document. A .seqj in front leaves it as it was.
        private Document? _live;

        public Editor()
        {
            Ctx.AppCommands.SaveFailed += Messages.Add;
            Ctx.AppCommands.SaveAsRefused += Messages.Add;
            Ctx.AppCommands.TsxSaveCompletedWithWarnings += warnings => Messages.AddRange(warnings);
            Ctx.AppCommands.LoadFailed += (path, ex) => Messages.Add($"{path}: {ex.Message}");
            Ctx.AppCommands.HotReloadFailed += (path, reason) => Messages.Add($"{path}: {reason}");
        }

        public TestServices Ctx { get; } = new();

        public string Folder => _dir.Path;

        /// <summary>Everything the editor told the user.</summary>
        public List<string> Messages { get; } = new();

        public async Task<Document> OpenAsync(DocType type, string name)
        {
            Documents.WriteTextures(Folder);
            string? path = type == DocType.Untitled ? null : Documents.Write(Folder, type, name);
            TabEntry? tab = type == DocType.Seqj ? null : new TabEntry(new FilePath(path ?? "untitled:" + name));
            if (type == DocType.Untitled)
            {
                tab!.CachedEditorModel = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
                tab.CachedEditorModel.AnimationChains.Add(Documents.Chain(name, Path.Combine(Folder, "sheet.png")));
            }
            var document = new Document(type, name, path, tab, type == DocType.Seqj ? SequenceDocument.Load(path!) : null);
            _open.Add(document);
            await BringToFrontAsync(document);
            return document;
        }

        public async Task BringToFrontAsync(Document document)
        {
            if (document.Tab is not { } tab) return;
            if (_live is { Tab: { } leaving })
            {
                leaving.UndoSnapshot = Ctx.UndoManager.TakeSnapshot();
                Ctx.AppCommands.CaptureTabEditorState(leaving);
            }
            _live = document;
            if (document.Type == DocType.Untitled)
            {
                Ctx.ProjectManager.ResetToBlankDocument();
                Ctx.ProjectManager.AnimationChainListSave = tab.CachedEditorModel;
                Ctx.UndoManager.Clear();
            }
            else
            {
                (await Ctx.AppCommands.ActivateTabContentAsync(tab)).ShouldBeTrue();
            }
            if (tab.UndoSnapshot != null) Ctx.UndoManager.RestoreSnapshot(tab.UndoSnapshot);
        }

        /// <summary>Adds a frame (or a sequence) through the real commands and returns the fact it adds.</summary>
        public string Edit(Document document)
        {
            if (document.Sequences is { } sequences)
                return "sequence " + sequences.AddSequence(Ctx.UndoManager, "Added-" + document.Name);
            var (left, top) = Documents.NewCell(document.Name);
            string texture = document.Path == null ? Path.Combine(Folder, "sheet.png") : "sheet.png";
            Ctx.AppCommands.AddFrameFromPixelBounds(
                Ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0], texture, left, top, left + 16, top + 16);
            return Documents.NewCellFact(document.Name);
        }

        /// <summary>File &gt; Save. An Untitled document asks for a file.</summary>
        public Task SaveAsync(Document document)
        {
            if (document.Type == DocType.Untitled) return SaveAsAsync(document, Path.Combine(Folder, "saved.achj"));
            if (document.Sequences is { } sequences) sequences.SaveToDisk();
            else Ctx.AppCommands.SaveCurrentAnimationChainList();
            return Task.CompletedTask;
        }

        public async Task SaveAsAsync(Document document, string path)
        {
            if (document.Sequences is { } sequences)
            {
                if (sequences.SaveAs(path) is { } error) Messages.Add(error);
                return;
            }
            Ctx.AppCommands.FileDialogService = new StubFileDialogService(path);
            await Ctx.AppCommands.SaveCurrentAnimationChainListAsync();
        }

        public void Reload(Document document)
        {
            if (document.Sequences is { } sequences) sequences.ReloadFromDisk();
            else Ctx.AppCommands.ReloadAchxFromDisk(document.Path!);
        }

        /// <summary>What the editor holds for <paramref name="document"/>.</summary>
        public HashSet<string> ContentOf(Document document) =>
            document.Sequences is { } sequences ? Documents.Content(sequences.Save)
            : Documents.Content(document == _live ? Ctx.ProjectManager.AnimationChainListSave : document.Tab!.CachedEditorModel);

        /// <summary>What the files hold, plus the Untitled documents, which closing asks about.</summary>
        public HashSet<string> DurableContent() =>
            Documents.FileContent(Folder).Concat(_open.Where(document => document.Type == DocType.Untitled).SelectMany(ContentOf)).ToHashSet();

        public void Dispose()
        {
            Ctx.IoManager.DeleteRecoveryFile();
            _dir.Dispose();
        }
    }
}
