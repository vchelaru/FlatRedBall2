using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App;

public partial class MainWindow
{
    /// <summary>The last scan <see cref="WarnAboutBrokenSequences"/> started, so a test can wait for it.</summary>
    internal Task BrokenSequenceScan { get; private set; } = Task.CompletedTask;

    /// <summary>Holds a scan's result back until it completes, so a test can act while the scan runs.</summary>
    internal Task BrokenSequenceScanGate { get; set; } = Task.CompletedTask;

    /// <summary>
    /// Names the sequence blocks that play a chain the file at <paramref name="animationPath"/> just lost, in one problem banner.
    /// The files are read off the UI thread and nothing is written.
    /// </summary>
    private void WarnAboutBrokenSequences(string? animationPath, RemovedChainNames removed)
    {
        if (string.IsNullOrEmpty(animationPath) || removed.Names.Count == 0) return; // an Untitled document has no file to play from
        BrokenSequenceScan = WarnAboutBrokenSequencesAsync(animationPath, removed);
    }

    private async Task WarnAboutBrokenSequencesAsync(string animationPath, RemovedChainNames removed)
    {
        var files = await FindSequenceBlocksAsync(animationPath,
            (sequences, path) => SeqjChainUsageScanner.FindMatches(sequences, path, animationPath, removed.Names));

        // The tab may have closed meanwhile, and a failed save has its own banner: the file still has the chains.
        var tab = _tabManager.Tabs.FirstOrDefault(open => open.Path == new FilePath(animationPath));
        if (tab is null || _achxPane.HasFailedSave(tab)) return;

        var message = BrokenSequenceNotice.Message(removed, files.Select(file => file with
        {
            Blocks = file.Blocks.Where(block => removed.Document.AnimationChains.All(chain => chain.Name != block.Chain)).ToList(), // not undone meanwhile
        }));
        if (message is not null) ShowProblem(message);
    }

    /// <summary>The names of the chains in the file at <paramref name="path"/>, read before it is removed; none when it cannot be read.</summary>
    private static IReadOnlyCollection<string> ChainNamesIn(string path)
    {
        try { return AnimationFileLoader.Load(path).List.AnimationChains.Select(chain => chain.Name).ToList(); }
        catch { return []; }
    }

    /// <summary>
    /// The banner text naming the sequence blocks that played a chain of the file just deleted at <paramref name="path"/>,
    /// or null when none did. <paramref name="chains"/> is its <see cref="ChainNamesIn"/>.
    /// </summary>
    private async Task<string?> BrokenByDeletedFileAsync(string path, IReadOnlyCollection<string> chains) =>
        BrokenSequenceNotice.Message("Deleting", path, await FindSequenceBlocksAsync(path,
            (sequences, seqj) => SeqjChainUsageScanner.FindMatches(sequences, seqj, path, chains)));

    private async Task<List<BrokenSequenceFile>> FindSequenceBlocksAsync(
        string animationPath, Func<SequenceListSave, string, IReadOnlyList<SeqjChainMatch>> find)
    {
        var projectFiles = _projectManager.ProjectFolderPath is null ? null
            : ProjectPanel.IncludedEntries.Select(entry => entry.File).OfType<DiskEditorFile>().Select(file => file.FullPath).ToList();
        var files = await Task.Run(async () =>
        {
            await BrokenSequenceScanGate;
            return BrokenSequenceNotice.FilesToScan(animationPath, projectFiles)
                .Select(path => new BrokenSequenceFile(path, SeqjChainUsageScanner.FindMatches(path, sequences => find(sequences, path))))
                .ToList();
        });

        // A tab that now has unsaved edits stands for its file.
        var unsaved = _sequencePane.UnsavedDocuments.ToDictionary(open => open.Path);
        return files.Select(file => unsaved.TryGetValue(new FilePath(file.Path), out var open)
            ? file with { Blocks = find(open.Save, file.Path) }
            : file).ToList();
    }
}
