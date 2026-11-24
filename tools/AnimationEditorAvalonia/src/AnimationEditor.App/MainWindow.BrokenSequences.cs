using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
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
        var projectFiles = _projectManager.ProjectFolderPath is null ? null
            : ProjectPanel.IncludedEntries.Select(entry => entry.File).OfType<DiskEditorFile>().Select(file => file.FullPath).ToList();
        // A tab with unsaved edits stands for its file. It is matched below, on the thread that edits it.
        var unsaved = _sequencePane.UnsavedDocuments.ToDictionary(open => open.Path);
        var files = await Task.Run(async () =>
        {
            await BrokenSequenceScanGate;
            return BrokenSequenceNotice.FilesToScan(animationPath, projectFiles)
                .Select(path => new BrokenSequenceFile(path,
                    unsaved.ContainsKey(new FilePath(path)) ? [] : SeqjChainUsageScanner.FindMatches(path, animationPath, removed.Names)))
                .ToList();
        });

        // The tab may have closed meanwhile, and a failed save has its own banner: the file still has the chains.
        var tab = _tabManager.Tabs.FirstOrDefault(open => open.Path == new FilePath(animationPath));
        if (tab is null || _achxPane.HasFailedSave(tab)) return;

        var message = BrokenSequenceNotice.Message(removed, files.Select(file => file with
        {
            Blocks = (unsaved.TryGetValue(new FilePath(file.Path), out var open)
                    ? SeqjChainUsageScanner.FindMatches(open.Save, file.Path, animationPath, removed.Names)
                    : file.Blocks)
                .Where(block => removed.Document.AnimationChains.All(chain => chain.Name != block.Chain)).ToList(), // not undone meanwhile
        }));
        if (message is not null) ShowProblem(message);
    }
}
