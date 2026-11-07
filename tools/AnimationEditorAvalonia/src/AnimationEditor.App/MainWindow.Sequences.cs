using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App;

// Spike: .seqj tabs. Mirrors the PNG tab path: a separate pane, its own document, no achx model.
public partial class MainWindow
{
    private SequenceModeHost? _sequenceHost;
    private Control? _inspectorAchxContent;
    private readonly Dictionary<string, SequenceDocument> _sequenceDocuments = new(StringComparer.OrdinalIgnoreCase);

    internal SequenceModeHost SequenceHost => _sequenceHost ??= CreateSequenceHost();

    private SequenceModeHost CreateSequenceHost()
    {
        var host = new SequenceModeHost(_undoManager, _thumbnailService, path => _ = LoadAnimationFileAsync(path));
        TopPane.Children.Add(host.ListPane);
        var editorParent = (Grid)AchxEditorPane.Parent!;
        Grid.SetColumn(host.EditorPane, Grid.GetColumn(AchxEditorPane));
        editorParent.Children.Add(host.EditorPane);
        return host;
    }

    /// <summary>Opens <paramref name="path"/> (a .seqj) as a tab, or focuses it.</summary>
    public void OpenSequenceAsTab(string path)
    {
        var leavingTab = _tabManager.ActiveTab;
        if (leavingTab is { Kind: TabKind.Achx })
        {
            _tabController.CaptureLeavingTab(leavingTab);
            SaveCompanionFile();
        }
        else
            CaptureLeavingSequenceTab(leavingTab);

        EnsureCurrentEditorContentHasTab();
        _tabManager.OpenOrFocus(new FilePath(path));
        ShowSequencePane(_tabManager.ActiveTab!);
        RebuildTabStrip();
    }

    /// <summary>File > New Sequence File: picks a path, writes a file with one empty sequence, opens it.</summary>
    private async Task NewSequenceFileAsync()
    {
        var path = await _appCommands.FileDialogService.PickSaveFileAsync("New Sequence File", "seqj",
            new[] { new FileTypeChoice("seqj", "Sequence (*.seqj)") });
        if (string.IsNullOrEmpty(path)) return;
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Sequence" });
        save.Save(path);
        OpenSequenceAsTab(path);
    }

    private void ShowSequencePane(TabEntry tab)
    {
        var host = SequenceHost;
        SetSidebarForPng(false); // restores Inspector/History if a PNG tab hid them
        AchxEditorPane.IsVisible = false;
        PngPaneGrid.IsVisible = false;
        PngPane.IsVisible = false;
        host.EditorPane.IsVisible = true;
        AnimationsBlock.IsVisible = false;
        DiffBlamePane.IsVisible = false;
        host.ListPane.IsVisible = true;
        if (InspectorTabContent.Child != host.InspectorPane)
        {
            _inspectorAchxContent = InspectorTabContent.Child;
            InspectorTabContent.Child = host.InspectorPane;
        }

        if (!_sequenceDocuments.TryGetValue(tab.Path.FullPath, out var document))
        {
            document = SequenceDocument.Load(tab.Path.FullPath);
            _sequenceDocuments[tab.Path.FullPath] = document;
        }
        // The undo stack is shared; each tab keeps its own history in its snapshot, like achx tabs.
        _undoManager.Clear();
        if (tab.UndoSnapshot is not null) _undoManager.RestoreSnapshot(tab.UndoSnapshot);
        host.Show(document);
        UpdateTitle();
    }

    /// <summary>Keeps a sequence tab's undo history when switching away, and empties the shared stack for the next tab.</summary>
    private void CaptureLeavingSequenceTab(TabEntry? leaving)
    {
        if (leaving is not { Kind: TabKind.Sequence }) return;
        leaving.UndoSnapshot = _undoManager.TakeSnapshot();
        _undoManager.Clear();
    }

    /// <summary>Leaves sequence mode if it is showing. Called by every path that shows another pane.</summary>
    private void HideSequencePane()
    {
        if (_sequenceHost is not { EditorPane.IsVisible: true } host) return;
        host.Hide();
        host.EditorPane.IsVisible = false;
        host.ListPane.IsVisible = false;
        AnimationsBlock.IsVisible = true;
        AchxEditorPane.IsVisible = true;
        if (_inspectorAchxContent is not null)
        {
            InspectorTabContent.Child = _inspectorAchxContent;
            _inspectorAchxContent = null;
        }
    }
}
