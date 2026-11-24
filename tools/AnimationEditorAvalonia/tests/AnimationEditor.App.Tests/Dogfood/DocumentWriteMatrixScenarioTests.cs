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
/// #2006: the actions of <see cref="DocAction"/> that need the window, for every type of document in front and in a tab
/// behind it, plus an untouched third tab. Whatever the action, each file on disk is still in its own format, nothing a
/// document held is gone, and no other file changed. The rest is in <c>DocumentWriteMatrixTests</c> (Core).
/// </summary>
public class DocumentWriteMatrixScenarioTests
{
    private static readonly DocAction[] Actions = Enum.GetValues<DocAction>().Where(action => action > DocAction.PushToTileset).ToArray();

    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);

    /// <summary>False only where the editor has no such command for the pair.</summary>
    private static bool Applies(DocAction action, DocType front, DocType background) => action switch
    {
        DocAction.CutChain or DocAction.CutFrame => background != DocType.Seqj,
        // A tileset holds neither a shape nor a second texture to cut.
        DocAction.CutChainATilesetCannotStore or DocAction.CutShape => background is not (DocType.Seqj or DocType.Tsx),
        // Cutting a sequence removes it at once, like text, and only another .seqj takes it.
        DocAction.CutSequence => front == DocType.Seqj && background == DocType.Seqj,
        DocAction.SaveAsOntoOpenTab => background != DocType.Untitled,
        DocAction.ConvertOpenTabToAchj => background == DocType.Achx,
        // What was recovered is an Untitled document.
        DocAction.RecoverAtStartup => front == DocType.Untitled,
        _ => true,
    };

    /// <summary>The issue of a row that is a real bug, or null. Such a row is skipped while it fails; the fix deletes its line.</summary>
    private static string? KnownBug(DocAction action, DocType front, DocType background) => (action, front, background) switch
    {
        (DocAction.CutChainATilesetCannotStore, DocType.Tsx, _) => "#2002",
        (DocAction.ConvertOpenTabToAchj, _, _) => "#2021",
        (DocAction.LeaveDuringRename, DocType.Untitled, DocType.Seqj) => "#2023",
        _ => null,
    };

    public static IEnumerable<object[]> Rows() => Documents.Rows(Actions, Applies);

    [AvaloniaTheory]
    [MemberData(nameof(Rows))]
    public async Task Action_KeepsEveryDocument(DocAction action, DocType front, DocType background)
    {
        if (KnownBug(action, front, background) is not { } issue)
        {
            await RunAsync(action, front, background);
            return;
        }
        try
        {
            await RunAsync(action, front, background);
        }
        catch (Exception)
        {
            Assert.Skip(issue);
        }
        Assert.Fail($"{issue} is fixed: delete its line from KnownBug.");
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
        bool apart = action is DocAction.CloseTabWithUnsavedEdits or DocAction.CloseWindowWithUnsavedEdits or DocAction.ConvertOpenTabToAchj;
        await editor.OpenAsync(Documents.Write(editor.ProjectFolder, DocType.Achx, "bystander"));
        Tab back = await OpenAsync(editor, background, Documents.BackgroundName, apart, action switch
        {
            DocAction.CutShape => Documents.Chain(Documents.BackgroundName, withShape: true),
            DocAction.CutChainATilesetCannotStore => Documents.Chain(Documents.BackgroundName, "other.png", withShape: true),
            _ => null,
        });
        Tab current = await OpenAsync(editor, front, Documents.FrontName, apart);
        var before = Documents.FileBytes(editor.ProjectFolder);
        var held = DurableContent(editor);

        // What the action is expected to write, and the content it adds.
        var written = new List<Tab>();
        var added = new List<string>();
        switch (action)
        {
            case DocAction.CutChain:
            case DocAction.CutChainATilesetCannotStore:
            case DocAction.CutFrame:
            case DocAction.CutShape:
                editor.ClickTab(back.Label);
                object cut = Select(editor, action);
                var cutFacts = held.Where(fact => Holds(cut, fact)).ToList();
                cutFacts.ShouldNotBeEmpty();
                editor.Press(Key.X, RawInputModifiers.Control);
                editor.Wait(TimeSpan.FromMilliseconds(50));
                editor.ClickTab(current.Label);
                if (front != DocType.Seqj) SelectTarget(editor, action);
                editor.Press(Key.V, RawInputModifiers.Control);
                editor.Wait(TimeSpan.FromMilliseconds(100));
                written.AddRange([back, current]);
                // Not a silent no-op: a chain document takes the cut, and the source lets it go.
                if (front is not (DocType.Seqj or DocType.Tsx))
                {
                    Documents.Content(editor.Project).ShouldHold(cutFacts, "the paste did not land");
                    editor.ClickTab(back.Label);
                    Documents.Content(editor.Project).Intersect(cutFacts).ShouldBeEmpty("the source still has what was cut");
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
                editor.SequenceNames.ShouldContain(Documents.BackgroundName);
                written.AddRange([back, current]);
                break;
            case DocAction.SaveAsOntoOpenTab:
                editor.Dialogs.AnswerNextSaveFile(back.Path);
                editor.ClickMenu("MenuSaveAs");
                editor.Wait(TimeSpan.FromMilliseconds(50));
                editor.ProblemText.ShouldNotBeNull().ShouldContain(Path.GetFileName(back.Path)!);
                editor.TabLabels.ShouldContain(current.Label);
                break;
            case DocAction.CloseTabWithUnsavedEdits:
                added.Add(EditWithoutSaving(editor, back));
                editor.ClickTab(current.Label);
                AnswerSave(editor, back);
                editor.CloseTab(back.Label);
                (await editor.WaitUntilAsync(() => editor.Tabs.Tabs.Count == 2, Soon)).ShouldBeTrue("the tab did not close");
                written.Add(back);
                break;
            case DocAction.CloseWindowWithUnsavedEdits:
                added.Add(EditWithoutSaving(editor, back));
                added.Add(EditWithoutSaving(editor, current));
                // Sequence tabs are asked about first, then the others in tab order.
                foreach (Tab tab in new[] { back, current }.OrderBy(tab => tab.Type != DocType.Seqj)) AnswerSave(editor, tab);
                editor.Window.Close();
                (await editor.WaitUntilAsync(() => !editor.Window.IsVisible, Soon)).ShouldBeTrue("the window did not close");
                written.AddRange([back, current]);
                break;
            case DocAction.Detach:
                editor.ClickTab(back.Label);
                added.Add(Edit(editor, back));
                editor.ClickTab(current.Label);
                await DetachAsync(editor, back);
                written.Add(back);
                break;
            case DocAction.LeaveDuringRename:
                held.Remove(BeginRename(editor, current));
                editor.ClickTab(back.Label);
                await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
                added.Add((front == DocType.Seqj ? "sequence " : "chain ") + "Renamed");
                written.Add(current);
                break;
            case DocAction.ConvertOpenTabToAchj:
                added.Add(EditWithoutSaving(editor, back));
                editor.ClickTab(current.Label);
                editor.Services.ProjectManager.ProjectFolderPath = editor.ProjectFolder;
                editor.Window.DeleteToRecycleBin = path =>
                {
                    File.Delete(path);
                    return null;
                };
                editor.Dialogs.AnswerNextConfirm(true);
                AnswerSave(editor, back);
                await editor.Window.ConvertProjectFileToAchjAsync(Path.GetRelativePath(editor.ProjectFolder, back.Path!).Replace('\\', '/'));
                editor.Layout();
                written.Add(back);
                break;
            default:
                throw new InvalidOperationException($"{action} has no case here.");
        }

        Documents.Changed(before).Except(written.Select(tab => Path.GetFileName(tab.Path))).ShouldBeEmpty("a file the action is not about changed");
        var now = DurableContent(editor);
        now.ShouldHold(held, "content the documents held is gone");
        if (editor.ProblemText is null) now.ShouldHold(added, "an edit was not saved and nothing said so");
    }

    [Fact]
    public void EveryAction_HasRows()
    {
        var rows = Rows().Select(row => (DocAction)row[0]).ToHashSet();
        Actions.Where(action => !rows.Contains(action)).ShouldBeEmpty();
    }

    /// <summary>An open tab. Its label is the file name, or the Untitled name.</summary>
    private sealed record Tab(DocType Type, string Name, string? Path, string Label);

    private static async Task<Tab> OpenAsync(AnimationEditorHarness editor, DocType type, string name, bool ownFolder, AnimationChainSave? chain = null)
    {
        string folder = ownFolder ? Path.Combine(editor.ProjectFolder, name) : editor.ProjectFolder;
        Documents.WriteTextures(folder);
        if (type != DocType.Untitled)
        {
            string path = Documents.Write(folder, type, name, chain);
            await editor.OpenAsync(path);
            return new Tab(type, name, path, Path.GetFileName(path));
        }

        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.N, RawInputModifiers.Control); // File > New asks where to save.
        editor.Wait(TimeSpan.FromMilliseconds(50));
        chain ??= Documents.Chain(name);
        // An unsaved document refers to its textures by absolute path.
        foreach (AnimationFrameSave frame in chain.Frames) frame.TextureName = Path.Combine(folder, frame.TextureName);
        editor.Services.AppCommands.PasteChains([chain]);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        return new Tab(type, name, null, editor.Tabs.ActiveTab!.DisplayName);
    }

    /// <summary>What the files hold, plus the Untitled tabs, which closing asks about.</summary>
    private static HashSet<string> DurableContent(AnimationEditorHarness editor) =>
        Documents.FileContent(editor.ProjectFolder).Concat(editor.Tabs.Tabs
            .Where(tab => TabManager.IsUntitledSentinel(tab.Path.Original))
            .SelectMany(tab => Documents.Content(tab == editor.Tabs.ActiveTab ? editor.Project : tab.CachedEditorModel)))
            .ToHashSet();

    private static bool Holds(object item, string fact)
    {
        var list = new AnimationChainListSave();
        if (item is AnimationChainSave chain) list.AnimationChains.Add(chain);
        else if (item is AnimationFrameSave frame) list.AnimationChains.Add(new AnimationChainSave { Name = "", Frames = { frame } });
        else return fact == "shape " + ((ShapeSave)item).Name;
        return Documents.Content(list).Contains(fact);
    }

    /// <summary>Clicks what <paramref name="action"/> cuts in the document in front, and returns it.</summary>
    private static object Select(AnimationEditorHarness editor, DocAction action)
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
        return item;
    }

    /// <summary>Clicks where the cut is pasted in the document in front.</summary>
    private static void SelectTarget(AnimationEditorHarness editor, DocAction action)
    {
        AnimationChainSave chain = editor.Project.AnimationChains[0];
        if (action != DocAction.CutShape)
        {
            editor.ClickRow(chain);
            return;
        }
        editor.Expand(chain);
        editor.ClickRow(chain.Frames[0]);
    }

    private static Control SequenceRow(AnimationEditorHarness editor) => (Control)editor.Control<ListBox>("SequenceList").ContainerFromIndex(0)!;

    private static void SelectSequence(AnimationEditorHarness editor) => editor.Click(SequenceRow(editor));

    /// <summary>Adds a frame, or a sequence, to the document in front and returns the fact it adds.</summary>
    private static string Edit(AnimationEditorHarness editor, Tab tab)
    {
        if (tab.Type == DocType.Seqj)
        {
            editor.Click(editor.Control<Button>("AddSequenceButton"));
            editor.Press(Key.Enter);
            return "sequence " + editor.SequenceNames.Last();
        }
        var (left, top) = Documents.NewCell(tab.Name);
        AnimationChainSave chain = editor.Project.AnimationChains[0];
        editor.Services.AppCommands.AddFrameFromPixelBounds(chain, chain.Frames[0].TextureName, left, top, left + 16, top + 16);
        editor.Layout();
        return Documents.NewCellFact(tab.Name);
    }

    /// <summary>An edit no file has: the save fails, or the tab is Untitled. The file is writable again afterwards.</summary>
    private static string EditWithoutSaving(AnimationEditorHarness editor, Tab tab)
    {
        editor.ClickTab(tab.Label);
        if (tab.Path is null) return Edit(editor, tab);
        UnusualProjectScenarioTests.MakeUnwritable(tab.Path);
        try
        {
            string fact = Edit(editor, tab);
            editor.ProblemText.ShouldNotBeNull("the save was meant to fail");
            // Only what the editor says from here on counts as telling the user.
            editor.Notifications.ErrorBanner.IsVisible = false;
            editor.Layout();
            return fact;
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(tab.Path);
        }
    }

    /// <summary>Queues Save for the prompt about <paramref name="tab"/>, and the file an Untitled one goes to.</summary>
    private static void AnswerSave(AnimationEditorHarness editor, Tab tab)
    {
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
        if (tab.Path is null) editor.Dialogs.AnswerNextSaveFile(Path.Combine(editor.ProjectFolder, tab.Name + "-saved.achj"));
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
        }
    }

    /// <summary>Types a new name for the first chain or sequence of the document in front without ending the rename, and returns the fact it replaces.</summary>
    private static string BeginRename(AnimationEditorHarness editor, Tab tab)
    {
        if (tab.Type == DocType.Seqj)
        {
            editor.DoubleClickAt(editor.CenterOf(SequenceRow(editor)));
            InlineRenameBox box = editor.Window.GetVisualDescendants().OfType<InlineRenameBox>().Single(candidate => candidate.IsEditing);
            box.Focus();
            editor.Layout();
            box.SelectAll();
            editor.Type("Renamed");
            return "sequence " + tab.Name;
        }
        AnimationChainSave chain = editor.Project.AnimationChains[0];
        editor.ClickRow(chain);
        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Renamed");
        return "chain " + chain.Name;
    }

    /// <summary>The editor starts after a crash: an unsaved document to recover, and a tab of the last session.</summary>
    private static async Task RecoverAtStartup(DocType background)
    {
        string root = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "project"), recovery = Path.Combine(root, "recovery.achx"), settingsRoot = Path.Combine(root, "settings");
        Documents.WriteTextures(folder);
        var recovered = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        recovered.AnimationChains.Add(Documents.Chain(Documents.FrontName, Path.Combine(folder, "sheet.png")));
        recovered.Save(recovery);
        if (background != DocType.Untitled)
        {
            string path = Documents.Write(folder, background, Documents.BackgroundName);
            FilePath settings = AppSettingsLocation.ForApplicationDataRoot(settingsRoot);
            Directory.CreateDirectory(settings.GetDirectoryContainingThis().FullPath);
            File.WriteAllText(settings.FullPath, JsonSerializer.Serialize(new AppSettingsModel { OpenTabPaths = [path], ActiveTabPath = path }));
        }
        var before = Documents.FileBytes(folder);

        using var editor = new AnimationEditorHarness(settingsRoot, recovery);
        (await editor.WaitUntilAsync(() => editor.Tabs.ActiveTab is { IsRecoveredDocument: true }, Soon)).ShouldBeTrue("nothing was recovered");
        Documents.Content(editor.Project).ShouldHold(Documents.Content(recovered));
        string added = Edit(editor, new Tab(DocType.Untitled, Documents.FrontName, null, ""));

        Documents.Changed(before).ShouldBeEmpty();
        Documents.ReadAsItsOwnFormat(recovery).ShouldHold([added]);
        editor.Tabs.Tabs.Count.ShouldBe(background == DocType.Untitled ? 1 : 2);
    }
}
