using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Views.Tests;

// Issue #770: recursive .achx tree for Open Project Folder. Platform-agnostic (no Window
// dependency, unlike desktop's FilesPanelControl) -- see ProjectPanelControl's doc comment.
public class ProjectPanelControlTests
{
    [AvaloniaFact]
    public void SetEntries_DefaultExcludesBinObj()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entries = new[]
        {
            new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx"),
            new AchxFileEntry(new FakeFile("stale.achx"), root, "bin/stale.achx"),
        };

        control.SetEntries(entries);

        Assert.Single(control.TreeRoots);
        Assert.Equal("hero.achx", control.TreeRoots[0].Name);
    }

    [AvaloniaFact]
    public void SetEntries_NoEntries_ShowsEmptyMessage()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();

        control.SetEntries(System.Array.Empty<AchxFileEntry>());

        Assert.True(control.EmptyMessage.IsVisible);
    }

    [AvaloniaFact]
    public void SelectingFileNode_RaisesFileSelected()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entry = new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx");
        control.SetEntries(new[] { entry });
        AchxFileEntry? selected = null;
        control.FileSelected += e => selected = e;

        control.ProjectTree.SelectedItem = control.TreeRoots[0];

        Assert.Same(entry, selected);
    }

    [AvaloniaFact]
    public void UncheckExcludeBinObj_ReincludesBinObjEntries()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entries = new[]
        {
            new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx"),
            new AchxFileEntry(new FakeFile("stale.achx"), root, "bin/stale.achx"),
        };
        control.SetEntries(entries);

        control.ExcludeBinObjCheck.IsChecked = false;

        Assert.Equal(2, control.TreeRoots.Count);
    }

    [AvaloniaFact]
    public void TypingInSearch_FiltersTreeToMatches()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entries = new[]
        {
            new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx"),
            new AchxFileEntry(new FakeFile("enemy.achx"), root, "enemy.achx"),
        };
        control.SetEntries(entries);

        control.ProjectSearchBox.SearchBox.Text = "hero";

        Assert.Single(control.TreeRoots);
        Assert.Equal("hero.achx", control.TreeRoots[0].Name);
    }

    [AvaloniaFact]
    public void SelectingFilteredResult_ClearsSearchAndRevealsFullTreeWithSelection()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var hero = new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx");
        var enemy = new AchxFileEntry(new FakeFile("enemy.achx"), root, "enemy.achx");
        control.SetEntries(new[] { hero, enemy });
        control.ProjectSearchBox.SearchBox.Text = "hero";

        var selections = new List<AchxFileEntry>();
        control.FileSelected += e => selections.Add(e);
        control.ProjectTree.SelectedItem = control.TreeRoots[0];

        Assert.Equal([hero], selections); // fired exactly once, not re-fired by the reveal step
        Assert.False(control.ProjectSearchBox.SearchBox.IsVisible); // search collapsed
        Assert.Equal(2, control.TreeRoots.Count); // full tree restored
        Assert.Same(hero, ((AnimationEditor.Views.Controls.AchxTreeNodeVm)control.ProjectTree.SelectedItem!).Entry);
    }

    private sealed class FakeFile : IEditorFile
    {
        public FakeFile(string name) => Name = name;
        public string Name { get; }
        public Task<Stream> OpenReadAsync() => throw new System.NotSupportedException();
        public Task<Stream> OpenWriteAsync() => throw new System.NotSupportedException();
        public Task<FolderEntrySnapshot> GetBasicPropertiesAsync() =>
            Task.FromResult(new FolderEntrySnapshot(null, null));
    }

    private sealed class FakeFolder : IEditorFolder
    {
        public FakeFolder(string name) => Name = name;
        public string Name { get; }
#pragma warning disable CS1998 // no subfolders/items to enumerate -- these entries are hand-built for the tree tests above
        public async IAsyncEnumerable<IEditorFile> GetItemsAsync() { yield break; }
        public async IAsyncEnumerable<IEditorFolder> GetSubfoldersAsync() { yield break; }
#pragma warning restore CS1998
        public Task<IEditorFile?> GetFileAsync(string name) => Task.FromResult<IEditorFile?>(null);
    }
}
