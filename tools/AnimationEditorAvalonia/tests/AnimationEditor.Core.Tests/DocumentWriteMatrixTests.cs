using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Export;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Sequences;
using AnimationEditor.DocumentMatrix;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Text.Json.Nodes;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// #2006: every action that writes a document, for every type of document in front and behind it. After the action every
/// document holds exactly what it should, in order, in its own file and in its own tab, and no file that should not
/// change has other bytes. The actions that need the window are in <c>DocumentWriteMatrixScenarioTests</c>.
/// </summary>
public class DocumentWriteMatrixTests
{
    private static readonly DocAction[] Actions = Enum.GetValues<DocAction>().Where(action => action <= DocAction.PushToTileset).ToArray();

    /// <summary>False only where the editor has no such command for the pair.</summary>
    private static bool Applies(DocAction action, DocType front, DocType background) => action switch
    {
        // What is behind plays no part.
        DocAction.SaveAsToAnotherFolder or DocAction.FailedSaveAs => background == DocType.Achx,
        // A name has an extension, so the target is always some file format.
        DocAction.SaveAsOntoAnotherFormat => background != DocType.Untitled,
        // The cross-file cut only saves file-backed tabs of the achx pane.
        DocAction.SaveBackgroundDocument => background is not (DocType.Untitled or DocType.Seqj),
        DocAction.Reload or DocAction.OutsideChangeOverUnsavedEdits => front != DocType.Untitled,
        // A sequence tab has no export to these formats, and no Untitled form to recover.
        DocAction.Export or DocAction.WriteRecovery => front != DocType.Seqj,
        DocAction.ConvertToAchj => front == DocType.Achx,
        DocAction.PushToTileset => front is DocType.Achx or DocType.Achj && background == DocType.Tsx,
        _ => true,
    };

    /// <summary>The issue of a row that is a real bug and how the row fails, or null. The fix deletes the line.</summary>
    private static (string Issue, string Failure)? KnownBug(DocAction action, DocType front) => (action, front) switch
    {
        (DocAction.OutsideChangeOverUnsavedEdits, DocType.Seqj) => ("#2030", "a refused save says so"),
        (DocAction.SaveAsToAnotherFolder, DocType.Achx or DocType.Achj) => ("#2031", "the new file names files that are not there"),
        (DocAction.SaveAsToAnotherFolder, DocType.Tsx) => ("#2016", "the new file names files that are not there"),
        _ => null,
    };

    public static IEnumerable<object[]> Rows() =>
        Documents.Rows(Actions, (action, front, background) => Applies(action, front, background) && KnownBug(action, front) is null);

    public static IEnumerable<object[]> KnownBugRows() =>
        Documents.Rows(Actions, (action, front, background) => Applies(action, front, background) && KnownBug(action, front) is not null);

    // Passes while the row fails the way its issue says, so the fix has to delete its line.
    [Theory]
    [MemberData(nameof(KnownBugRows))]
    public async Task KnownBug_StillFails(DocAction action, DocType front, DocType background)
    {
        var (issue, failure) = KnownBug(action, front)!.Value;
        Exception? thrown = await Record.ExceptionAsync(() => Action_KeepsEveryDocument(action, front, background));
        Assert.True(thrown != null, $"{issue} is fixed: delete its line from KnownBug.");
        Assert.Contains(failure, thrown.Message);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Action_KeepsEveryDocument(DocAction action, DocType front, DocType background)
    {
        using var editor = new Editor();
        // A folder each, as on Linux an unwritable file is an unwritable folder. A tileset and its source share the texture.
        bool apart = action != DocAction.PushToTileset;
        Document back = await editor.OpenAsync(background, Documents.BackgroundName, apart);
        Document current = await editor.OpenAsync(front, Documents.FrontName, apart);
        var before = Documents.FileBytes(editor.Folder);
        // The files whose bytes may change, and the new ones.
        var written = new List<string?>();

        switch (action)
        {
            case DocAction.Save:
                editor.Ctx.AppCommands.AutosaveOnChange = false;
                editor.Edit(current);
                if (front == DocType.Untitled) await editor.SaveAsAsync(current, editor.InFolderOf(current, "saved.achj"));
                else editor.Save(current);
                written.Add(current.Path);
                break;
            case DocAction.Autosave:
                editor.Edit(current);
                written.Add(current.Path);
                break;
            case DocAction.SaveAs:
                string? original = current.Path;
                string copy = editor.InFolderOf(current, "copy" + (front == DocType.Untitled ? ".achj" : front.Extension()));
                await editor.SaveAsAsync(current, copy);
                // Later edits go to the new file, and only there.
                editor.Edit(current);
                current.Path.ShouldBe(copy);
                if (original != null) File.ReadAllBytes(original).ShouldBe(before[original]);
                written.Add(copy);
                break;
            case DocAction.SaveAsToAnotherFolder:
                string folder = Directory.CreateDirectory(Path.Combine(editor.Folder, "moved")).FullName;
                string moved = Path.Combine(folder, Documents.FrontName + (front == DocType.Untitled ? ".achj" : front.Extension()));
                await editor.SaveAsAsync(current, moved);
                Documents.MissingReferences(moved).ShouldBeEmpty("the new file names files that are not there");
                // They are still the files they were, and stay so when it is edited.
                editor.ShouldBeAsExpected();
                editor.Edit(current);
                editor.Messages.ShouldBeEmpty();
                written.Add(moved);
                break;
            case DocAction.SaveAsOntoAnotherFormat:
                string target = Documents.Write(editor.InFolderOf(current, ""), background, "closed");
                byte[] old = File.ReadAllBytes(target);
                bool takesIt = front == background || (front is not (DocType.Tsx or DocType.Seqj) && background is (DocType.Achx or DocType.Achj));
                await editor.SaveAsAsync(current, target, refused: !takesIt);
                File.ReadAllBytes(target).AsSpan().SequenceEqual(old).ShouldBe(!takesIt);
                editor.Messages.Count.ShouldBe(takesIt ? 0 : 1);
                written.AddRange([target, "clips.achx"]);
                break;
            case DocAction.FailedSaveAs:
                editor.Edit(current);
                string? kept = editor.Ctx.ProjectManager.FileName;
                var saveState = editor.Ctx.UndoManager.SaveState;
                foreach (Obstacle obstacle in Enum.GetValues<Obstacle>().Where(obstacle => Blocks(obstacle, front)))
                {
                    // A folder of its own, so a texture name rewritten for it is wrong where the document still is.
                    string blockedFolder = Directory.CreateDirectory(Path.Combine(editor.Folder, obstacle.ToString())).FullName;
                    string blocked = Path.Combine(blockedFolder, "blocked" + (front == DocType.Untitled ? ".achj" : front.Extension()));
                    using (Block(obstacle, blocked))
                        await editor.SaveAsAsync(current, blocked, refused: true);
                    if (current.Sequences is { } sequences) sequences.HasUnsavedEdits.ShouldBeFalse(obstacle.ToString());
                    else
                    {
                        editor.Ctx.ProjectManager.FileName.ShouldBe(kept, obstacle.ToString());
                        editor.Ctx.UndoManager.SaveState.ShouldBe(saveState, obstacle.ToString());
                        editor.Ctx.IoManager.RecoveryFileExists().ShouldBe(front == DocType.Untitled, obstacle.ToString());
                    }
                    Directory.EnumerateFiles(blockedFolder).Where(file => file != blocked).ShouldBeEmpty($"{obstacle}: nothing is left beside the target");
                    editor.ShouldBeAsExpected();
                    editor.Messages.Count.ShouldBe(1, $"{obstacle}: a failed save says so, once");
                    editor.Messages[0].ShouldStartWith("Could not save blocked.", Case.Sensitive, obstacle.ToString());
                    editor.Messages.Clear();
                }
                editor.Ctx.UndoManager.Undo();
                editor.Ctx.UndoManager.Redo();
                // The document still saves where it did.
                if (front == DocType.Untitled) Documents.ShouldHold(editor.Ctx.IoManager.RecoveryFilePath, current.Expected);
                editor.Messages.ShouldBeEmpty();
                written.Add(current.Path);
                foreach (string extension in new[] { ".achx", ".achj", ".tsx", ".seqj" }) written.Add("blocked" + extension);
                break;
            case DocAction.SaveBackgroundDocument:
                var cell = Documents.NewCell(back.Name);
                var frames = back.Tab!.CachedEditorModel!.AnimationChains[0].Frames;
                frames.Add(new AnimationFrameSave
                {
                    TextureName = frames[0].TextureName,
                    FrameLength = 0.1f,
                    LeftCoordinate = cell.Left,
                    RightCoordinate = cell.Left + 16,
                    TopCoordinate = cell.Top,
                    BottomCoordinate = cell.Top + 16,
                });
                editor.Ctx.AppCommands.SaveDocument(back.Tab).ShouldBeTrue();
                back.Expected.AddFrame(back.Name);
                editor.ShouldBeAsExpected();
                // What is in front is still itself: it saves as its own type, to its own tiles.
                editor.Edit(current);
                written.AddRange([back.Path, current.Path]);
                break;
            case DocAction.Reload:
                current.Expected = ChangeOutside(current);
                editor.Reload(current);
                written.Add(current.Path);
                break;
            case DocAction.SwitchTabs:
                editor.Edit(current);
                await editor.BringToFrontAsync(back);
                editor.Edit(back);
                editor.ShouldBeAsExpected();
                // Not loaded again from disk: the undo history holds these objects.
                var model = current.Tab?.CachedEditorModel;
                await editor.BringToFrontAsync(current);
                if (model != null) editor.Ctx.ProjectManager.AnimationChainListSave.ShouldBeSameAs(model);
                written.AddRange([current.Path, back.Path]);
                break;
            case DocAction.UndoAfterSwitch:
                Expected untouched = editor.Copy(current);
                editor.Edit(current);
                await editor.BringToFrontAsync(back);
                editor.Edit(back);
                await editor.BringToFrontAsync(current);
                editor.Ctx.UndoManager.Undo();
                current.Expected = untouched;
                editor.ShouldBeAsExpected();
                editor.Ctx.UndoManager.Redo();
                editor.AddToExpected(current);
                written.AddRange([current.Path, back.Path]);
                break;
            case DocAction.OutsideChangeOverUnsavedEdits:
                Documents.SetWritable(current.Path!, false);
                try { editor.Edit(current); }
                finally { Documents.SetWritable(current.Path!, true); }
                editor.Messages.ShouldNotBeEmpty("the save was meant to fail");
                ChangeOutside(current);
                byte[] outside = File.ReadAllBytes(current.Path!);
                await editor.BringToFrontAsync(back);
                await editor.BringToFrontAsync(current);
                editor.Messages.Clear();
                editor.Save(current);
                // The edits stay in the tab and the outside change stays in the file, and the editor says so.
                File.ReadAllBytes(current.Path!).ShouldBe(outside, "the save overwrote the outside change");
                editor.Messages.ShouldNotBeEmpty("a refused save says so");
                editor.LinesOf(current).ShouldBe(editor.ExpectedLines(current), "the unsaved edits are gone");
                written.Add(current.Path);
                editor.Forget(current);
                break;
            case DocAction.WriteRecovery:
                editor.Edit(current);
                editor.Ctx.IoManager.RecoveryFileExists().ShouldBe(front == DocType.Untitled);
                if (front == DocType.Untitled) Documents.ShouldHold(editor.Ctx.IoManager.RecoveryFilePath, current.Expected);
                written.Add(current.Path);
                break;
            case DocAction.Export:
                string export = editor.InFolderOf(current, "export.json");
                editor.Ctx.AppCommands.FileDialogService = new StubFileDialogService(export);
                await editor.Ctx.AppCommands.ExportAsync(ExportFormat.PixiJs);
                JsonNode sheet = JsonNode.Parse(File.ReadAllText(export))!;
                var keys = sheet["animations"]!["Walk-front"]!.AsArray().Select(key => (string)key!).ToList();
                // An undeclared flip is a warning, not another rectangle.
                keys.Select(key => sheet["frames"]![key]!["frame"]!.ToJsonString())
                    .ShouldBe(["{\"x\":0,\"y\":0,\"w\":16,\"h\":16}", "{\"x\":16,\"y\":0,\"w\":16,\"h\":16}"]);
                ((string)sheet["meta"]!["image"]!).ShouldEndWith("sheet.png");
                break;
            case DocAction.ConvertToAchj:
                var result = AchxToAchjConverter.Convert(current.Path!);
                result.Status.ShouldBe(AchxConversionStatus.Converted);
                Documents.ShouldHold(result.TargetPath!, current.Expected);
                written.Add(result.TargetPath);
                break;
            case DocAction.PushToTileset:
                editor.Ctx.AppCommands.AddAssociatedTiledTileset(back.Path!);
                editor.Edit(current);
                // Its tiles and lengths reach the tileset, beside the tileset's own animation. A tileset something is pushed
                // to does not open as a document.
                Documents.TileAnimations(back.Path!).ShouldBe(["0: 0@100 1@250 2@100", "4: 4@200", "11: 8@100 9@250", "12: 12@200"]);
                written.AddRange([current.Path, back.Path]);
                editor.Forget(back);
                break;
            default:
                throw new InvalidOperationException($"{action} has no case here.");
        }

        editor.ShouldBeAsExpected();
        Documents.Changed(before, editor.Folder).Except(written.Select(Path.GetFileName)).ShouldBeEmpty("a file the action is not about changed");
    }

    /// <summary>What makes the write of a Save As fail.</summary>
    private enum Obstacle { ReadOnly, Locked, MissingFolder, IsADirectory }

    private static bool Blocks(Obstacle obstacle, DocType type) => obstacle switch
    {
        // Elsewhere a file held open can still be replaced.
        Obstacle.Locked => OperatingSystem.IsWindows(),
        // A sequence's Save As makes the folder.
        Obstacle.MissingFolder => type != DocType.Seqj,
        _ => true,
    };

    /// <summary>Puts <paramref name="obstacle"/> at <paramref name="path"/> until disposed.</summary>
    private static IDisposable Block(Obstacle obstacle, string path)
    {
        string folder = Path.GetDirectoryName(path)!;
        switch (obstacle)
        {
            case Obstacle.ReadOnly:
                File.WriteAllText(path, "");
                Documents.SetWritable(path, false);
                return new Unblock(() => Documents.SetWritable(path, true));
            case Obstacle.Locked:
                return new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            case Obstacle.MissingFolder:
                Directory.Delete(folder);
                return new Unblock(() => Directory.CreateDirectory(folder));
            default:
                Directory.CreateDirectory(path);
                return new Unblock(() => { });
        }
    }

    private sealed class Unblock(Action action) : IDisposable
    {
        public void Dispose() => action();
    }

    /// <summary>Another program renames what is in the file, and the content that leaves.</summary>
    private static Expected ChangeOutside(Document document)
    {
        File.WriteAllText(document.Path!, File.ReadAllText(document.Path!).Replace("Walk-front", "Walk-outside").Replace("Arms", "Outside"));
        return Documents.Read(document.Path!);
    }

    [Fact]
    public void EveryAction_HasRows()
    {
        var rows = Rows().Select(row => (DocAction)row[0]).ToHashSet();
        Actions.Where(action => !rows.Contains(action)).ShouldBeEmpty();
    }

    /// <summary>An open document: a tab of the achx pane, or a <c>.seqj</c> in its own document.</summary>
    private sealed class Document
    {
        public required DocType Type { get; init; }
        public required string Name { get; init; }
        public required string Folder { get; set; }
        public required Expected Expected { get; set; }
        public string? Path { get; set; }
        public TabEntry? Tab { get; init; }
        public SequenceDocument? Sequences { get; init; }
        public UndoSnapshot? Undo { get; set; }
    }

    /// <summary>The tabs of one window over the real Core commands, switched the way the tab panes do.</summary>
    private sealed class Editor : IDisposable
    {
        private readonly TestHelpers.TempDir _dir = new();
        private readonly List<Document> _open = new();
        private Document? _front;
        // The achx pane's live document. A .seqj in front leaves it as it was.
        private Document? _live;

        public Editor()
        {
            Ctx.AppCommands.SaveFailed += Messages.Add;
            Ctx.AppCommands.SaveAsRefused += Messages.Add;
            Ctx.AppCommands.TsxSaveCompletedWithWarnings += (_, warnings) => Messages.AddRange(warnings);
            Ctx.AppCommands.LoadFailed += (path, ex) => Messages.Add($"{path}: {ex.Message}");
            Ctx.AppCommands.HotReloadFailed += (path, reason) => Messages.Add($"{path}: {reason}");
            Documents.Write(Folder, DocType.Achx, "bystander");
        }

        public TestServices Ctx { get; } = new();

        public string Folder => _dir.Path;

        /// <summary>Everything the editor told the user.</summary>
        public List<string> Messages { get; } = new();

        public string InFolderOf(Document document, string file) => Path.Combine(document.Folder, file);

        public async Task<Document> OpenAsync(DocType type, string name, bool ownFolder)
        {
            string folder = ownFolder ? Path.Combine(Folder, name) : Folder;
            Documents.WriteTextures(folder);
            string? path = type == DocType.Untitled ? null : Documents.Write(folder, type, name);
            AnimationChainSave chain = Documents.ChainFor(type, name);
            var document = new Document
            {
                Type = type,
                Name = name,
                Folder = folder,
                Path = path,
                Expected = Expected.Fixture(folder, type, name, chain),
                Tab = type == DocType.Seqj ? null : new TabEntry(new FilePath(path ?? "untitled:" + name)),
                Sequences = type == DocType.Seqj ? SequenceDocument.Load(path!) : null,
            };
            if (type == DocType.Untitled) document.Tab!.CachedEditorModel = Expected.Fixture(folder, type, name, chain).List;
            if (document.Sequences is { } sequences) sequences.SaveFailed += Messages.Add;
            _open.Add(document);
            await BringToFrontAsync(document);
            return document;
        }

        public async Task BringToFrontAsync(Document document)
        {
            if (_front != null)
            {
                _front.Undo = Ctx.UndoManager.TakeSnapshot();
                if (_front.Tab != null) Ctx.AppCommands.CaptureTabEditorState(_front.Tab);
            }
            _front = document;
            if (document.Tab is null)
            {
                if (document.Sequences!.ReloadIfChangedOnDisk().Changed) document.Undo = null;
                Ctx.UndoManager.Clear();
            }
            else if (document.Type == DocType.Untitled)
            {
                _live = document;
                Ctx.ProjectManager.ResetToBlankDocument();
                Ctx.ProjectManager.AnimationChainListSave = document.Tab.CachedEditorModel;
                Ctx.UndoManager.Clear();
            }
            else
            {
                _live = document;
                document.Tab.UndoSnapshot = document.Undo;
                (await Ctx.AppCommands.ActivateTabContentAsync(document.Tab)).ShouldBeTrue();
                document.Undo = document.Tab.UndoSnapshot;
            }
            if (document.Undo != null) Ctx.UndoManager.RestoreSnapshot(document.Undo);
        }

        /// <summary>Adds a frame (or a sequence) to the document in front through the real commands.</summary>
        public void Edit(Document document)
        {
            if (document.Sequences is { } sequences)
            {
                sequences.AddSequence(Ctx.UndoManager, "Added-" + document.Name);
            }
            else
            {
                AnimationChainSave chain = Ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];
                var (left, top) = Documents.NewCell(document.Name);
                Ctx.AppCommands.AddFrameFromPixelBounds(chain, chain.Frames[0].TextureName, left, top, left + 16, top + 16);
            }
            AddToExpected(document);
        }

        /// <summary>What <see cref="Edit"/> leaves.</summary>
        public void AddToExpected(Document document)
        {
            if (document.Sequences is null) document.Expected.AddFrame(document.Name);
            else document.Expected.Sequences.Sequences.Add(new SequenceSave { Name = "Added-" + document.Name });
        }

        public Expected Copy(Document document) =>
            document.Sequences is null ? Expected.Of(document.Expected.List) : Expected.Of(document.Expected.Sequences);

        /// <summary>File &gt; Save of a document with a file.</summary>
        public void Save(Document document)
        {
            if (document.Sequences is { } sequences) sequences.SaveToDisk();
            else Ctx.AppCommands.SaveCurrentAnimationChainList();
        }

        public async Task SaveAsAsync(Document document, string path, bool refused = false)
        {
            if (document.Sequences is { } sequences)
            {
                if (sequences.SaveAs(path) is { } error) Messages.Add(error);
            }
            else
            {
                Ctx.AppCommands.FileDialogService = new StubFileDialogService(path);
                await Ctx.AppCommands.SaveCurrentAnimationChainListAsync();
            }
            if (refused) return;
            document.Path = path;
            document.Folder = Path.GetDirectoryName(path)!;
        }

        public void Reload(Document document)
        {
            if (document.Sequences is { } sequences) sequences.ReloadFromDisk();
            else Ctx.AppCommands.ReloadAchxFromDisk(document.Path!);
        }

        /// <summary>The action already compared <paramref name="document"/>.</summary>
        public void Forget(Document document) => _open.Remove(document);

        /// <summary>Every document holds what it should in its file and in the editor.</summary>
        public void ShouldBeAsExpected()
        {
            foreach (Document document in _open)
            {
                if (document.Path != null) Documents.ShouldHold(document.Path, document.Expected);
                LinesOf(document).ShouldBe(ExpectedLines(document), $"{document.Name} in the editor");
            }
        }

        public List<string> ExpectedLines(Document document) => document.Expected.Lines(document.Folder);

        /// <summary>What the editor holds for <paramref name="document"/>.</summary>
        public List<string> LinesOf(Document document) =>
            document.Sequences is { } sequences ? Documents.Lines(sequences.Save, document.Folder)
            : Documents.Lines((document == _live ? Ctx.ProjectManager.AnimationChainListSave : document.Tab!.CachedEditorModel)!, document.Folder);

        public void Dispose()
        {
            Ctx.IoManager.DeleteRecoveryFile();
            _dir.Dispose();
        }
    }
}
