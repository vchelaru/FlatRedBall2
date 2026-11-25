using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using AnimationEditor.DocumentMatrix;
using AnimationEditor.Views.Controls;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// #2006: the actions of <see cref="DocAction"/> that need the window. After the action every document holds exactly what
/// it should, in order, in its own file and in its own tab, and no other file changed. The rest, and <c>.achj</c>, which
/// the window treats as <c>.achx</c>, is in <c>DocumentWriteMatrixTests</c> (Core).
/// </summary>
public class DocumentWriteMatrixScenarioTests
{
    private static readonly DocAction[] Actions = Enum.GetValues<DocAction>().Where(action => action > DocAction.PushToTileset).ToArray();

    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The pairs that have the command and that reach their own code; a pair more costs about 0.6 s. Where what is in front
    /// only has to be something else, the achx pane and the sequence pane are the two kinds.
    /// </summary>
    private static bool Applies(DocAction action, DocType front, DocType background)
    {
        if (front == DocType.Achj || background == DocType.Achj) return false;
        bool eitherPaneInFront = front is DocType.Achx or DocType.Seqj;
        return action switch
        {
            DocAction.CutChain => background != DocType.Seqj,
            DocAction.CutFrame => front != DocType.Seqj && background != DocType.Seqj,
            // A tileset holds neither a shape nor a second texture to cut.
            DocAction.CutChainATilesetCannotStore => front is DocType.Tsx or DocType.Achx && background is DocType.Achx or DocType.Untitled,
            // What is in front plays no part in what the source pushes.
            DocAction.CutChainOutOfPushSource => front == DocType.Achx && background == DocType.Achx,
            DocAction.CutShape => front != DocType.Seqj && background is (DocType.Achx or DocType.Untitled),
            // Cutting a sequence removes it at once, like text, and only another .seqj takes it.
            DocAction.CutSequence => front == DocType.Seqj && background == DocType.Seqj,
            // Onto another format is refused before this, in Core's rows.
            DocAction.SaveAsOntoOpenTab => background != DocType.Untitled && (front == background || (front == DocType.Untitled && background == DocType.Achx)),
            DocAction.CloseTabWithUnsavedEdits or DocAction.Detach => eitherPaneInFront,
            DocAction.CloseBehindOutsideChange => front is (DocType.Achx or DocType.Tsx or DocType.Seqj) && background == DocType.Achx,
            // Both tabs are asked about, whichever is in front.
            DocAction.CloseWindowWithUnsavedEdits => front <= background,
            DocAction.LeaveDuringRename => background is DocType.Achx or DocType.Seqj,
            DocAction.ConvertOpenTabToAchj or DocAction.ConvertOpenPushSource => eitherPaneInFront && background == DocType.Achx,
            // What was recovered is an Untitled document.
            DocAction.RecoverAtStartup => front == DocType.Untitled,
            _ => true,
        };
    }

    /// <summary>The issue of a row that is a real bug and how it fails, or null. Such a row is skipped while it fails so; the fix deletes its line.</summary>
    private static (string Issue, string Failure)? KnownBug(DocAction action, DocType front, DocType background) => (action, front, background) switch
    {
        _ => null,
    };

    public static IEnumerable<object[]> Rows() => Documents.Rows(Actions, Applies);

    [AvaloniaTheory]
    [MemberData(nameof(Rows))]
    public async Task Action_KeepsEveryDocument(DocAction action, DocType front, DocType background)
    {
        if (KnownBug(action, front, background) is not { } bug)
        {
            await RunAsync(action, front, background);
            return;
        }
        try
        {
            await RunAsync(action, front, background);
        }
        catch (Exception ex) when (ex.Message.Contains(bug.Failure))
        {
            Assert.Skip(bug.Issue);
        }
        Assert.Fail($"{bug.Issue} is fixed: delete its line from KnownBug.");
    }

    private static async Task RunAsync(DocAction action, DocType front, DocType background)
    {
        if (action == DocAction.RecoverAtStartup)
        {
            await RecoverAtStartup(background);
            return;
        }

        using var editor = new AnimationEditorHarness();
        // A folder each: an unwritable file on Linux is an unwritable folder. A cut needs both on the one texture.
        bool apart = action is DocAction.CloseTabWithUnsavedEdits or DocAction.CloseBehindOutsideChange or DocAction.CloseWindowWithUnsavedEdits
            or DocAction.ConvertOpenTabToAchj or DocAction.ConvertOpenPushSource;
        AnimationChainSave chain = Documents.ChainFor(background, Documents.BackgroundName);
        if (action == DocAction.CutChainATilesetCannotStore) chain = Documents.RichChain(Documents.BackgroundName, "other.png");
        else if (front == DocType.Tsx && action is DocAction.CutChain or DocAction.CutFrame) chain = Documents.PlainChain(Documents.BackgroundName);
        Tab back = await OpenAsync(editor, background, Documents.BackgroundName, apart, chain);
        Tab current = await OpenAsync(editor, front, Documents.FrontName, apart, Documents.ChainFor(front, Documents.FrontName));
        var tabs = new List<Tab> { back, current };
        var before = Documents.FileBytes(editor.ProjectFolder);
        var written = new List<string?>();
        string? tileset = null;
        if (action is DocAction.CutChainOutOfPushSource or DocAction.ConvertOpenPushSource)
        {
            tileset = Documents.Write(back.Folder, DocType.Tsx, Documents.FrontName);
            editor.Services.IoManager.AddAssociatedTiledTilesetPath(back.Path!, tileset);
            written.Add(tileset);
        }

        switch (action)
        {
            case DocAction.CutChain:
            case DocAction.CutChainATilesetCannotStore:
            case DocAction.CutChainOutOfPushSource:
            case DocAction.CutFrame:
            case DocAction.CutShape:
                editor.ClickTab(back.Label);
                SelectCut(editor, action);
                editor.Press(Key.X, RawInputModifiers.Control);
                editor.Wait(TimeSpan.FromMilliseconds(50));
                editor.ClickTab(current.Label);
                if (front != DocType.Seqj) SelectTarget(editor, action);
                editor.Press(Key.V, RawInputModifiers.Control);
                editor.Wait(TimeSpan.FromMilliseconds(100));
                if (front == DocType.Tsx && action == DocAction.CutChainATilesetCannotStore)
                {
                    // A copy in the tab, which the file cannot store, and the banner says so. The source keeps it.
                    editor.ProblemText.ShouldNotBeNull();
                    editor.Project.AnimationChains.Count.ShouldBe(3);
                    current.OnlyTheFile = true;
                }
                else if (front != DocType.Seqj && !(front == DocType.Tsx && action == DocAction.CutShape))
                {
                    MoveCut(action, back.Expected, current.Expected);
                }
                break;
            case DocAction.CutSequence:
                editor.ClickTab(back.Label);
                SelectSequence(editor);
                editor.Press(Key.X, RawInputModifiers.Control);
                editor.Wait(TimeSpan.FromMilliseconds(50));
                editor.ClickTab(current.Label);
                SelectSequence(editor);
                editor.Press(Key.V, RawInputModifiers.Control);
                editor.Wait(TimeSpan.FromMilliseconds(100));
                // After the selected one.
                current.Expected.Sequences.Sequences.Insert(1, back.Expected.Sequences.Sequences[0]);
                back.Expected.Sequences.Sequences.RemoveAt(0);
                break;
            case DocAction.SaveAsOntoOpenTab:
                editor.Dialogs.AnswerNextSaveFile(back.Path);
                editor.ClickMenu("MenuSaveAs");
                editor.Wait(TimeSpan.FromMilliseconds(50));
                editor.ProblemText.ShouldNotBeNull().ShouldContain(Path.GetFileName(back.Path)!);
                editor.TabLabels.ShouldContain(current.Label);
                break;
            case DocAction.CloseTabWithUnsavedEdits:
                EditWithoutSaving(editor, back);
                editor.ClickTab(current.Label);
                AnswerSave(editor, back);
                editor.CloseTab(back.Label);
                (await editor.WaitUntilAsync(() => editor.Tabs.Tabs.Count == 1, Soon)).ShouldBeTrue("the tab did not close");
                break;
            case DocAction.CloseBehindOutsideChange:
                EditWithoutSaving(editor, current);
                File.WriteAllText(current.Path!, File.ReadAllText(current.Path!).Replace("Walk-front", "Walk-outside").Replace("Idle-front", "Idle-outside"));
                byte[] outside = File.ReadAllBytes(current.Path!);
                editor.CloseTab(back.Label);
                (await editor.WaitUntilAsync(() => editor.Tabs.Tabs.Count == 1, Soon)).ShouldBeTrue("the tab did not close");
                // The edits stay in the tab and the outside change in the file.
                if (front == DocType.Seqj) editor.SequenceNames.ShouldBe(current.Expected.Sequences.Sequences.Select(sequence => sequence.Name));
                else Documents.Lines(editor.Project, current.Folder).ShouldBe(current.Expected.Lines(current.Folder));
                File.ReadAllBytes(current.Path!).ShouldBe(outside);
                written.Add(current.Path);
                tabs.Remove(current);
                break;
            case DocAction.CloseWindowWithUnsavedEdits:
                EditWithoutSaving(editor, back);
                EditWithoutSaving(editor, current);
                // Sequence tabs are asked about first, then the others in tab order.
                foreach (Tab tab in tabs.OrderBy(tab => tab.Type != DocType.Seqj)) AnswerSave(editor, tab);
                editor.Window.Close();
                (await editor.WaitUntilAsync(() => !editor.Window.IsVisible, Soon)).ShouldBeTrue("the window did not close");
                break;
            case DocAction.Detach:
                editor.ClickTab(back.Label);
                Edit(editor, back);
                editor.ClickTab(current.Label);
                await DetachAsync(editor, back);
                break;
            case DocAction.LeaveDuringRename:
                BeginRename(editor, current);
                editor.ClickTab(back.Label);
                await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
                break;
            case DocAction.ConvertOpenTabToAchj:
            case DocAction.ConvertOpenPushSource:
                EditWithoutSaving(editor, back);
                editor.ClickTab(current.Label);
                editor.Services.ProjectManager.ProjectFolderPath = editor.ProjectFolder;
                editor.Window.DeleteToRecycleBin = path =>
                {
                    File.Delete(path);
                    return null;
                };
                editor.Dialogs.AnswerNextEditorDialog(_ => true);
                await editor.Window.ConvertProjectFileToAchjAsync(Path.GetRelativePath(editor.ProjectFolder, back.Path!).Replace('\\', '/'));
                editor.Layout();
                written.Add(back.Path);
                back.Path = Path.ChangeExtension(back.Path, ".achj");
                break;
            default:
                throw new InvalidOperationException($"{action} has no case here.");
        }

        foreach (Tab tab in tabs) ShouldBeAsExpected(editor, tab);
        // The tileset has what its source's file now has, beside its own animation.
        if (tileset != null)
            Documents.TileAnimations(tileset).ShouldBe(action == DocAction.ConvertOpenPushSource
                ? ["3: 0@100 1@250", "4: 4@200", "8: 8@100 9@250 10@100", "12: 12@200"]
                : ["3: 0@100 1@250", "4: 4@200", "12: 12@200"]);
        // Only a document that now holds something else was written.
        written.AddRange(tabs.Where(tab => tab.Path != null && !tab.Expected.Lines(tab.Folder).SequenceEqual(tab.Original)).Select(tab => tab.Path));
        Documents.Changed(before, editor.ProjectFolder).Except(written.Select(Path.GetFileName)).ShouldBeEmpty("a file that should not change did");
    }

    [Fact]
    public void EveryAction_HasRows()
    {
        var rows = Rows().Select(row => (DocAction)row[0]).ToHashSet();
        Actions.Where(action => !rows.Contains(action)).ShouldBeEmpty();
    }

    /// <summary>An open document.</summary>
    private sealed class Tab
    {
        public required DocType Type { get; init; }
        public required string Name { get; init; }
        public required string Folder { get; init; }

        /// <summary>The file name, or the Untitled name.</summary>
        public required string Label { get; init; }

        public required Expected Expected { get; init; }

        /// <summary>What it held when it was opened.</summary>
        public required List<string> Original { get; init; }

        /// <summary>The file it should be in after the action; null while it is Untitled.</summary>
        public string? Path { get; set; }

        /// <summary>The tab holds more than its file, and the editor said so.</summary>
        public bool OnlyTheFile { get; set; }
    }

    private static async Task<Tab> OpenAsync(AnimationEditorHarness editor, DocType type, string name, bool ownFolder, AnimationChainSave chain)
    {
        string folder = ownFolder ? Path.Combine(editor.ProjectFolder, name) : editor.ProjectFolder;
        Documents.WriteTextures(folder);
        Expected expected = Expected.Fixture(folder, type, name, chain);
        string? path = null;
        if (type == DocType.Untitled)
        {
            editor.Dialogs.AnswerNextSaveFile(null);
            editor.Press(Key.N, RawInputModifiers.Control); // File > New asks where to save.
            editor.Wait(TimeSpan.FromMilliseconds(50));
            editor.Services.AppCommands.PasteChains(Expected.Fixture(folder, type, name, chain).Chains);
            editor.Wait(TimeSpan.FromMilliseconds(50));
        }
        else
        {
            path = Documents.Write(folder, type, name, chain);
            await editor.OpenAsync(path);
        }
        return new Tab
        {
            Type = type,
            Name = name,
            Folder = folder,
            Label = editor.Tabs.ActiveTab!.DisplayName,
            Expected = expected,
            Original = expected.Lines(folder),
            Path = path,
        };
    }

    /// <summary>The document is whole in its file, or while it is Untitled in its tab, and in its tab when that is open.</summary>
    private static void ShouldBeAsExpected(AnimationEditorHarness editor, Tab tab)
    {
        if (tab.Path != null) Documents.ShouldHold(tab.Path, tab.Expected);
        TabEntry? open = editor.Window.IsVisible ? editor.Tabs.Tabs.SingleOrDefault(entry => entry.DisplayName == tab.Label) : null;
        if (tab.Path is null) open.ShouldNotBeNull($"{tab.Label} is in no tab and in no file");
        if (open is null || tab.Type == DocType.Seqj || tab.OnlyTheFile) return;
        AnimationChainListSave held = open == editor.Tabs.ActiveTab ? editor.Project : open.CachedEditorModel!;
        Documents.Lines(held, tab.Folder).ShouldBe(tab.Expected.Lines(tab.Folder), $"{tab.Label} in its tab");
    }

    /// <summary>Takes what <see cref="SelectCut"/> picks out of one document and puts it where <see cref="SelectTarget"/> points in the other.</summary>
    private static void MoveCut(DocAction action, Expected from, Expected to)
    {
        AnimationChainSave chain = from.Chains[0];
        AnimationFrameSave frame = chain.Frames[0];
        switch (action)
        {
            case DocAction.CutFrame:
                chain.Frames.Remove(frame);
                to.Chains[0].Frames.Add(frame);
                break;
            case DocAction.CutShape:
                var shape = (ShapeSave)frame.ShapesSave!.Shapes[0];
                frame.ShapesSave.Remove(shape);
                (to.Chains[0].Frames[0].ShapesSave ??= new ShapesSave()).Add(shape);
                break;
            default:
                from.Chains.Remove(chain);
                to.Chains.Add(chain);
                break;
        }
    }

    /// <summary>Clicks what <paramref name="action"/> cuts in the document in front.</summary>
    private static void SelectCut(AnimationEditorHarness editor, DocAction action)
    {
        AnimationChainSave chain = editor.Project.AnimationChains[0];
        object item = chain;
        if (action is DocAction.CutFrame or DocAction.CutShape)
        {
            editor.Expand(chain);
            item = chain.Frames[0];
        }
        if (action == DocAction.CutShape)
        {
            editor.Expand(chain.Frames[0]);
            item = chain.Frames[0].ShapesSave!.Shapes[0];
        }
        editor.ClickRow(item);
    }

    /// <summary>Clicks where the cut is pasted in the document in front.</summary>
    private static void SelectTarget(AnimationEditorHarness editor, DocAction action)
    {
        AnimationChainSave chain = editor.Project.AnimationChains[0];
        if (action != DocAction.CutShape)
        {
            // A chain is pasted after the selected one, a frame into it.
            editor.ClickRow(action == DocAction.CutFrame ? chain : editor.Project.AnimationChains.Last());
            return;
        }
        editor.Expand(chain);
        editor.ClickRow(chain.Frames[0]);
    }

    private static Control SequenceRow(AnimationEditorHarness editor) => (Control)editor.Control<ListBox>("SequenceList").ContainerFromIndex(0)!;

    private static void SelectSequence(AnimationEditorHarness editor) => editor.Click(SequenceRow(editor));

    /// <summary>Adds a frame, or a sequence, to the document in front.</summary>
    private static void Edit(AnimationEditorHarness editor, Tab tab)
    {
        if (tab.Type == DocType.Seqj)
        {
            editor.Click(editor.Control<Button>("AddSequenceButton"));
            editor.Press(Key.Enter);
            tab.Expected.Sequences.Sequences.Add(new SequenceSave { Name = editor.SequenceNames.Last() });
            return;
        }
        var (left, top) = Documents.NewCell(tab.Name);
        AnimationChainSave chain = editor.Project.AnimationChains[0];
        editor.Services.AppCommands.AddFrameFromPixelBounds(chain, chain.Frames[0].TextureName, left, top, left + 16, top + 16);
        editor.Layout();
        tab.Expected.AddFrame(tab.Name);
    }

    /// <summary>An edit no file has: the save fails, or the tab is Untitled. The file is writable again afterwards.</summary>
    private static void EditWithoutSaving(AnimationEditorHarness editor, Tab tab)
    {
        editor.ClickTab(tab.Label);
        if (tab.Path is null)
        {
            Edit(editor, tab);
            return;
        }
        Documents.SetWritable(tab.Path, false);
        try
        {
            Edit(editor, tab);
            editor.ProblemText.ShouldNotBeNull("the save was meant to fail");
            // It covers the tab strip.
            editor.Notifications.ErrorBanner.IsVisible = false;
            editor.Layout();
        }
        finally
        {
            Documents.SetWritable(tab.Path, true);
        }
    }

    /// <summary>Queues Save for the prompt about <paramref name="tab"/>, and the file an Untitled one goes to.</summary>
    private static void AnswerSave(AnimationEditorHarness editor, Tab tab)
    {
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
        if (tab.Path is not null) return;
        tab.Path = Path.Combine(tab.Folder, tab.Name + "-saved.achj");
        editor.Dialogs.AnswerNextSaveFile(tab.Path);
    }

    private static async Task DetachAsync(AnimationEditorHarness editor, Tab tab)
    {
        MainWindow? detached = null;
        string temp = Path.Combine(Path.GetTempPath(), "AnimationEditorTests", Guid.NewGuid().ToString("N"));
        editor.Window.DetachedWindowFactory = () => detached = App.CreateDetachedWindow(temp, Path.Combine(temp, "recovery.achx"));
        try
        {
            editor.TabFor(tab.Label).ContextMenu!.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Detach to New Window").Command!.Execute(null);
            if (tab.Path is null)
            {
                await editor.WaitAsync(TimeSpan.FromMilliseconds(50));
                editor.TabLabels.ShouldContain(tab.Label, "an Untitled tab has no file to move");
                return;
            }
            (await editor.WaitUntilAsync(() => detached is not null, Soon)).ShouldBeTrue("no window opened");
            editor.TabLabels.ShouldNotContain(tab.Label);
        }
        finally
        {
            detached?.CloseWithoutPrompt();
            editor.Layout();
            DeleteFolder(temp);
        }
    }

    private static void DeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
            // A file watcher may still hold it.
        }
    }

    /// <summary>Types a new name for the first chain or sequence of the document in front without ending the rename.</summary>
    private static void BeginRename(AnimationEditorHarness editor, Tab tab)
    {
        if (tab.Type == DocType.Seqj)
        {
            editor.DoubleClickAt(editor.CenterOf(SequenceRow(editor)));
            InlineRenameBox box = editor.Window.GetVisualDescendants().OfType<InlineRenameBox>().Single(candidate => candidate.IsEditing);
            box.Focus();
            editor.Layout();
            box.SelectAll();
            editor.Type("Renamed");
            tab.Expected.Sequences.Sequences[0].Name = "Renamed";
            return;
        }
        editor.ClickRow(editor.Project.AnimationChains[0]);
        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Renamed");
        tab.Expected.Chains[0].Name = "Renamed";
    }

    /// <summary>The editor starts after a crash: an unsaved document to recover, and a tab of the last session.</summary>
    private static async Task RecoverAtStartup(DocType background)
    {
        string root = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "project"), recovery = Path.Combine(root, "recovery.achx"), settingsRoot = Path.Combine(root, "settings");
        Documents.WriteTextures(folder);
        Expected recovered = Expected.Fixture(folder, DocType.Untitled, Documents.FrontName, Documents.RichChain(Documents.FrontName));
        recovered.List.Save(recovery);
        if (background != DocType.Untitled)
        {
            string path = Documents.Write(folder, background, Documents.BackgroundName);
            FilePath settings = AppSettingsLocation.ForApplicationDataRoot(settingsRoot);
            Directory.CreateDirectory(settings.GetDirectoryContainingThis().FullPath);
            File.WriteAllText(settings.FullPath, JsonSerializer.Serialize(new AppSettingsModel { OpenTabPaths = [path], ActiveTabPath = path }));
        }
        var before = Documents.FileBytes(folder);

        using (var editor = new AnimationEditorHarness(settingsRoot, recovery))
        {
            (await editor.WaitUntilAsync(() => editor.Tabs.ActiveTab is { IsRecoveredDocument: true }, Soon)).ShouldBeTrue("nothing was recovered");
            Documents.Lines(editor.Project, folder).ShouldBe(recovered.Lines(folder));
            Edit(editor, new Tab { Type = DocType.Untitled, Name = Documents.FrontName, Folder = folder, Label = "", Expected = recovered, Original = [] });

            Documents.Changed(before, folder).ShouldBeEmpty();
            Documents.ShouldHold(recovery, recovered);
            editor.Tabs.Tabs.Count.ShouldBe(background == DocType.Untitled ? 1 : 2);
        }
        DeleteFolder(root);
    }
}
